//#define DEBUG

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    class EpochWireAlloc // one per direction
    {
        ulong[,] epochDue;  // indexed by parity,epoch: cycle when epoch will be freed
        int[] currentEpoch; // indexed by parity: epoch# currently filling (0 or 1)
        int[] currentWire;  // indexed by parity: current wire in epoch

        public EpochWireAlloc()
        {
            epochDue = new ulong[2,2];
            currentEpoch = new int[2];
            currentWire = new int[2];
        }

        void checkEpoch(int parity)
        {
            if (currentWire[parity] >= Config.nack_nr / 2) // check if we ran out of wires.
            {
                // check if other epoch is expired.
                if (epochDue[parity, 1 - currentEpoch[parity]] <= Simulator.CurrentRound)
                {
                    // other epoch is expired: flip over to it.
                    currentEpoch[parity] = 1 - currentEpoch[parity];
                    epochDue[parity, currentEpoch[parity]] = 0;
                    currentWire[parity] = 0;
                }
            }
        }

        public int allocateNack(ulong due)
        {
            int parity = (int)(Simulator.CurrentRound & 1);

            checkEpoch(parity);

            // if no wires remaining, fail.
            if (currentWire[parity] >= Config.nack_nr / 2)
                return -1;

            // allocate wire
            if (epochDue[parity, currentEpoch[parity]] < due)
                epochDue[parity, currentEpoch[parity]] = due;
            return (currentEpoch[parity] * Config.nack_nr / 2) +
                currentWire[parity]++;
        }

        public bool nackAvailable()
        {
            int parity = (int)(Simulator.CurrentRound & 1);

            checkEpoch(parity);

            return currentWire[parity] < Config.nack_nr / 2;
        }
    }

    public class Router_SCARAB : Router
    {
        //Router_Flit_RXBuf m_rxbuf;
        //bool m_real_rxbuf;

        Flit m_injectSlot;

        int[] nackRouting; // indexed by parity, output port; -1 for unallocated, -2 for local

        // batch-based NACK wire allocation
//        EpochWireAlloc[] allocator;

        public Link[] nackOut = new Link[4*Config.nack_nr];
        public Link[] nackIn = new Link[4*Config.nack_nr];
        private double[] avgOutPriority = new double[4];

        //private Dictionary<Packet, ulong> nack_due = new Dictionary<Packet, ulong>();

        int[] wormRouting; // indexed by input port; -1 for no worm, -2 for dropping

        public int parity()
        {
            return (int)(Simulator.CurrentRound & 1);
        }

        public int nackNr(int dir, int nackWireNr)
        {
            return dir * Config.nack_nr + nackWireNr;
        }

        public int nackWireNr(int nackNr)
        {
            return nackNr % Config.nack_nr;
        }

        public int nackDir(int nackNr)
        {
            return nackNr / Config.nack_nr;
        }

        public Router_SCARAB(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot = null;

            nackRouting = new int[Config.nack_nr * 4];
            wormRouting = new int[5]; // fifth direction for local port

            for (int i = 0; i < Config.nack_nr * 4; i++)
                nackRouting[i] = -1;

            for (int i = 0; i < 5; i++)
                wormRouting[i] = -1;

/*
            allocator = new EpochWireAlloc[4];
            for (int i = 0; i < 4; i++)
                allocator[i] = new EpochWireAlloc();
*/
        }

        private ulong ejectingPacket = ulong.MaxValue;
        bool ejectLocal()
        {
#if DEBUG
            if (ID == 0) Console.WriteLine("------");
#endif
            bool noneEjected = true;

            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
#if DEBUG
                    Console.WriteLine("flit at {0}: {1}", coord, linkIn[dir].Out);
#endif
                    if (linkIn[dir].Out.dest.ID == ID)
                    {
                        Flit f = linkIn[dir].Out;
                        f.inDir = dir;
#if DEBUG
                        Console.WriteLine("candidate ejection {0} at {1}", f, coord);
#endif

                        if (f.packet.ID == ejectingPacket || (f.isHeadFlit && noneEjected && ejectingPacket == ulong.MaxValue) || Config.router.ejectMultipleCheat)
                        {
#if DEBUG
                            Console.WriteLine("ejecting at {0}: flit {1}", coord, f);
#endif
                            statsEjectFlit(f);
                            m_n.receiveFlit(f);

#if DEBUG
                            Console.WriteLine("ejecting flit {0} of packet {1} ({2}/{3} arrived)", f.flitNr, f.packet.ID, f.packet.nrOfArrivedFlits, f.packet.nrOfFlits);
#endif

                            if (f.packet.nrOfArrivedFlits != f.flitNr+1)
                                throw new Exception("Out of order ejection!");

                            if (f.isHeadFlit)
                            {
                                sendTeardown(f);
                                ejectingPacket = f.packet.ID;
#if DEBUG
                                Console.WriteLine("ejecting packet {0}",f.packet.ID);
#endif
                            }

                            if (f.isTailFlit)
                            {
                                ejectingPacket = ulong.MaxValue;
#if DEBUG
                                Console.WriteLine("no longer ejecting packet {0}", f.packet.ID);
#endif
                            }

                            noneEjected = false;
                        }
                        else
                            if (f.isHeadFlit)
                            {
#if DEBUG
                                Console.WriteLine("SECONDARY EJECTION DROPPED at proc {0} with dir {1}", ID, dir);
#endif
                                sendNack(f);
                                wormRouting[dir] = -2;

                                Simulator.stats.drop.Add();
                                Simulator.stats.drop_by_src[ID].Add();
                            }
                        linkIn[dir].Out = null;
                    }
                }
            return noneEjected;
        }

        Flit[] input = new Flit[4]; // keep this as a member var so we don't
                                    // have to allocate on every step (why can't
                                    // we have arrays on the stack like in C?)

        int RR = 0;
        int getRR() { RR++; if (RR > 3) RR = 0; return RR; }

        protected override void _doStep()
        {
            stepNacks();
            /* bool ejectedThisCycle = */ ejectLocal();

            for (int i = 0; i < 4; i++) input[i] = null;

            // first, propagate the non-head flits along their worm paths
            // (no truncation, so this is very simple)
            for (int dir = 0; dir < 4; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                    !linkIn[dir].Out.isHeadFlit)
                {
#if DEBUG
                    Console.WriteLine("non-head flit: {0}", linkIn[dir].Out);
#endif
                    Flit f = linkIn[dir].Out; // grab the input flit from the link
                    linkIn[dir].Out = null;

                    if (wormRouting[dir] == -1)
                    {
                        // AGH: worm not routed
                        throw new Exception("SHOULDN'T HAPPEN!");
                    }

                    if (wormRouting[dir] != -2) // if not dropping, propagate the flit
                        linkOut[wormRouting[dir]].In = f;
                    if (f.isTailFlit) // if last flit, close the wormhole
                        wormRouting[dir] = -1;
                }
            }
            if (m_injectSlot != null && !m_injectSlot.isHeadFlit)
            {
                linkOut[wormRouting[4]].In = m_injectSlot;
                if (m_injectSlot.isTailFlit)
                    wormRouting[4] = -1;
                m_injectSlot = null;
            }

            // grab inputs into a local array
            int c = 0;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                    linkIn[dir].Out.inDir = dir; // record this for below
                    input[c++] = linkIn[dir].Out;
                    linkIn[dir].Out = null;
                }

            // step 1: get possible-output vectors for each input
            bool[,] possible = new bool[4,4]; // (input,direction)
            int[] possible_count = new int[4];
            for (int i = 0; i < 4 && input[i] != null; i++)
            {
                PreferredDirection pd = determineDirection(input[i].dest);

                if (pd.xDir != Simulator.DIR_NONE &&
                    linkOut[pd.xDir].In == null)
                {
                    if (nackAvailable(pd.xDir))
                        possible[i,pd.xDir] = true;
                    else
                    {
                        Simulator.stats.nack_unavail.Add();
                        Simulator.stats.nack_unavail_by_src[ID].Add();
                    }

                }
                if (pd.yDir != Simulator.DIR_NONE &&
                    linkOut[pd.yDir].In == null)
                {
                    if (nackAvailable(pd.yDir))
                        possible[i,pd.yDir] = true;
                    else
                    {
                        Simulator.stats.nack_unavail.Add();
                        Simulator.stats.nack_unavail_by_src[ID].Add();
                    }
                }
            }
            // step 2: count possible requests per output
            for (int i = 0; i < 4; i++)
                for (int dir = 0; dir < 4; dir++)
                    if (possible[i,dir]) possible_count[dir]++;

            // step 3: if more than one possible for a given request, pick one with least
            //         requests; if tie, break randomly
            for (int i = 0; i < 4; i++)
            {
                int min_req = 10, min_req_j = -1;
                for (int j = 0; j < 4; j++)
                    if (possible[i,j])
                        if (possible_count[j] < min_req)
                        {
                            min_req_j = j;
                            min_req = possible_count[j];
                        }

                for (int j = 0; j < 4; j++) possible[i,j] = false;
                if (min_req_j != -1)
                    possible[i, min_req_j] = true;
            }
            // step 4,5: compute maximum priority requesting each output; set everyone
            // below this prio to false
            for (int dir = 0; dir < 4; dir++)
            {
                int max_prio = -1;
                for (int i = 0; i < 4; i++)
                    if (possible[i,dir])
                        if (input[i].packet.scarab_retransmit_count > max_prio)
                            max_prio = input[i].packet.scarab_retransmit_count;

                for (int i = 0; i < 4; i++)
                    if (possible[i,dir] && input[i].packet.scarab_retransmit_count < max_prio)
                        possible[i,dir] = false;
            }

            // step 6: select a winner in round-robin fashion
            int offset = getRR();
            int[] assignments = new int[4];
            for (int i = 0; i < 4; i++) assignments[i] = -1;
            for (int i_ = 0; i_ < 4; i_++)
            {
                int i = (i_ + offset) % 4;

                for (int dir = 0; dir < 4; dir++)
                    if (possible[i,dir])
                    {
                        assignments[i] = dir;
                        for (int j = 0; j < 4; j++)
                            possible[j,dir] = false;
                    }
            }

            //Flit oppBufferable = null;

            // assign outputs, choose a flit to opp. buffer if appropriate
            for (int i = 0; i < 4 && input[i] != null; i++)
            {
                int dir = assignments[i];
                if (dir == -1)
                {
                    // drop!
                    sendNack(input[i]);
                    wormRouting[input[i].inDir] = -2;

                    Simulator.stats.drop.Add();
                    Simulator.stats.drop_by_src[ID].Add();
                }
                else
                {
                    double decay = 0.875; //TODO parameterize
                    avgOutPriority[dir] = avgOutPriority[dir] * (1 - decay) + input[i].packet.scarab_retransmit_count * decay;

                    /*
                    if (Config.opp_buffering
                        && !ejectedThisCycle
                        && input[i].packet.nrOfFlits == 1
                        && myProcessor.msh.hasOppBufferSpace()
                        && input[i].packet.scarab_retransmit_count < avgOutPriority[dir]
                        )
                    {
                        // buffer opportunistically! (choose highest priority packet)
                        if (oppBufferable == null || input[i].packet.scarab_retransmit_count > oppBufferable.packet.scarab_retransmit_count)
                            oppBufferable = input[i];
                    }
                    */
                }
            }

            for (int i = 0; i < 4 && input[i] != null; i++)
            {
                int dir = assignments[i];
                if (dir == -1) continue;

                int nackWire;
                ulong due = Simulator.CurrentRound + 4 * (1 + Simulator.distance(coord, input[i].dest));
                //nack_due[input[i].packet] = due;

                /*
                if (input[i] == oppBufferable)
                {
                    Console.WriteLine("Opp Buffering flit!");
                    sendTeardown(oppBufferable);
                    myProcessor.ejectFlit(oppBufferable);

                    nackWire = allocateNack(dir, -2, due);
                }
                else
                */
                    nackWire = allocateNack(dir, nackNr(input[i].inDir, input[i].nackWire), due);

                if (nackWire == -1)
                    throw new Exception("shouldn't happen");

                input[i].nackWire = nackWire;
                linkOut[dir].In = input[i];
                wormRouting[input[i].inDir] = dir;
            }

            // now try to inject
            if (m_injectSlot != null)
            {
                PreferredDirection pd = determineDirection(m_injectSlot.dest);
                ulong due = Simulator.CurrentRound + 4 * (1 + Simulator.distance(coord, m_injectSlot.dest));
                //nack_due[m_injectSlot.packet] = due;
                
                if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                {
                    int nackWire = allocateNack(pd.xDir, -2, due);
                    if (nackWire != -1)
                    {
                        linkOut[pd.xDir].In = m_injectSlot;
                        m_injectSlot.nackWire = nackWire;
                        m_injectSlot = null;
                        wormRouting[4] = pd.xDir;
                    }
                }
                if (m_injectSlot != null && // check this again: only try y if x didn't work
                    pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                {
                    int nackWire = allocateNack(pd.yDir, -2, due);
                    if (nackWire != -1)
                    {
                        linkOut[pd.yDir].In = m_injectSlot;
                        m_injectSlot.nackWire = nackWire;
                        m_injectSlot = null;
                        wormRouting[4] = pd.yDir;
                    }
                }
            }
        }


        void stepNacks()
        {
//            int p = parity();

            for (int i = 0; i < Config.nack_nr * 4; i++)
            {
                if (nackIn[i] != null && nackIn[i].Out != null)
                {
                    if (nackRouting[i] == -2) // local?
                    {
                        /*
                        if (Config.nack_epoch)
                        {
                            if (nack_due[nackIn[i].Out.packet.scarab_retransmit] < Simulator.CurrentRound)
                            {
#if DEBUG
                                Console.WriteLine("late nack: due {0}, t = {1} (ID = {2})",
                                                  nack_due[nackIn[i].Out.packet.scarab_retransmit], Simulator.CurrentRound,
                                                  nackIn[i].Out.packet.scarab_retransmit.ID);
#endif
                            }
                        }
                        */


                        if (nackIn[i].Out.packet.scarab_is_nack)
                        {
                            Packet p = nackIn[i].Out.packet.scarab_retransmit;
                            p.nrOfArrivedFlits = 0;
                            m_n.queuePacket(p);
                        }
                        //else if (nackIn[i].Out.packet.scarab_is_teardown)
                        //    myProcessor.deliverTeardown(nackIn[i].Out.packet);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine("Proc {2} nack routing from in {0} to out {1}", i, nackRouting[i], ID);
#endif
                        nackOut[nackRouting[i]].In = nackIn[i].Out;
                    }

                    // now we tear it down (a NACK or TEARDOWN only comes once in a circuit)
                    nackRouting[i] = -1;
                }
            }
        }

        bool nackAvailable(int outDir)
        {
//            int p = parity();

//            if (Config.nack_epoch)
//                return allocator[outDir].nackAvailable();
//            else
//            {
                for (int wireNr = 0; wireNr < Config.nack_nr; wireNr++)
                {
                    int nNr = nackNr(outDir, wireNr);
                    if (nackOut[nNr] != null && nackRouting[nNr] == -1)
                        return true;
                }

                return false;
//            }
        }

        int allocateNack(int outDir, int inNr, ulong due)
        {
//            int p = parity();

            if (!nackAvailable(outDir))
                return -1;

//            if (Config.nack_epoch)
//            {
//                int nNr = allocator[outDir].allocateNack(due);
//                int outNr = nackNr(outDir, nNr);
//                if (nackRouting[outNr] != -1) throw new Exception("re-allocated wire!");
//                nackRouting[outNr] = inNr;
//                return nNr;
//            }
//            else
//            {
                int wireNr;
                for (wireNr = 0; wireNr < Config.nack_nr; wireNr++)
                {
                    int nNr = nackNr(outDir, wireNr);
                    if (nackOut[nNr] != null && nackRouting[nNr] == -1)
                        break;
                }

                int outNr = nackNr(outDir, wireNr);
                nackRouting[outNr] = inNr;

                return wireNr; // return wire#, not global nack#
//            }
        }

        void sendNack(Flit f)
        {
            int nNr = nackNr(f.inDir, f.nackWire);
//            Console.WriteLine("\tProc {4}:sendNack for ID {0}: inDir = {1}, nackWire = {2}, nNr = {3}",
//                              f.packet.ID, f.inDir, f.nackWire, nNr, ID);
            Packet p = new Packet(f.packet.request, 0, 1, coord, f.packet.src);
            p.scarab_retransmit = f.packet;
            f.packet.scarab_retransmit_count++;

            p.scarab_is_nack = true;

            nackOut[nNr].In = p.flits[0];
        }

        void sendTeardown(Flit f)
        {
//            Console.WriteLine("\tProc {3}:Teardown for ID {0}: inDir = {1}, nackWire = {2} (packet src {4}, dest {5})",
//                              f.packet.ID, f.inDir, f.nackWire, ID, f.packet.source.ID, f.packet.dest.ID);
            int nNr = nackNr(f.inDir, f.nackWire);
            Packet p = new Packet(f.packet.request, 0, 1, coord, f.packet.src);
            p.scarab_retransmit = f.packet; // don't actually retransmit, but keep this for debugging

            p.scarab_is_teardown = true;

/*
            // these two pieces of data are recorded to let a processor receiveing
            // a teardown whether it should look for an opp buffering slot to be cleared
            p.ID = f.packet.ID;
            p.source = f.packet.source;
 */


            nackOut[nNr].In = p.flits[0];   
        }

        public override bool canInjectFlit(Flit f)
        {
#if DEBUG
            Console.WriteLine("canInjectFlit at {0}: answer is {1}", coord, m_injectSlot == null);
#endif
            return m_injectSlot == null;
        }

        public override void InjectFlit(Flit f)
        {
            if (m_injectSlot != null)
                throw new Exception("Trying to inject twice in one cycle");

#if DEBUG
            Console.WriteLine("injectFlit at {0}: {1}", coord, f);
#endif

            statsInjectFlit(f);

            m_injectSlot = f;
        }
    }
}

//#endif
