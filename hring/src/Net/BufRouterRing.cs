//#define debug

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    /**
     * @brief This is an implementation of buffered ring network interface proposed in 
     * Ravindran et al. "A Performance Comparison of Hierarchical Ring- and Mesh-Connected Multiprocessor Networks",
     * HPCA 1997.
     *
     * << NIC >>
     * Although it's supposed to be using a wormhole flow control, it's actually more like a cut-through
     * flow control since its ring buffer size is exactly 1 packet in the paper.
     *
     * Injection policy:
     * 1. if the ring buffer is empty and no packet is being transmitted, injection of a new packet can
     *    bypass the ring buffer to the next node. Priority is given over to response packets over 
     *    request packets.
     * 2. Ring buffer is not empty, injection is not allowed. One exception is that once the header flit
     *    of the inject packet is started. Priority is given over to injection until it's done.
     * 
     * Ring buffer:
     * Header flit allocated it and a tail flit deallocates it.
     *
     * Arbitration:
     * As mentioned above, arbitration to the next node is always given to in-flight flit (ones in the buffer),
     * except for the case that a local injection has already started for a packet.
     * Arbitration always need to check the downstream buffer and see if it's available.
     *
     * Caveat: No bypassing.
     *
     * TODO: prioritization at the injection queue pool
     *
     **/ 
    public class WormholeBuffer
    {
        Queue<Flit> buf;
        // Due to pipelining, we need to increment the buffer count ahead of
        // time. Sort of like credit.
        int bufSize, lookAheadSize;
        bool bWorm;

        public WormholeBuffer(int size)
        {
            bufSize = size;
            lookAheadSize = 0;
            if (size < 1)
                throw new Exception("The buffer size must be at least 1-flit big.");
            buf = new Queue<Flit>();
            bWorm = false;
        }

        public void enqueue(Flit f)
        {
            if (buf.Count == bufSize)
                throw new Exception("Cannot enqueue into the ring buffer due to size limit.");

            buf.Enqueue(f);
            Simulator.stats.totalBufferEnqCount.Add();
        }

        public Flit dequeue()
        {
            Flit f = buf.Dequeue();
            lookAheadSize--;
            
            // Take care of the cases when queue becomes empty while there is
            // still more body flits coming in. This is needed because the
            // arbitration simply looks at the queue and see if it's empty. It
            // assumes the worm is gone if queue is empty. TODO: this doesn't
            // fix the deadlock case when buffer size is less than 2.
            if (f.isHeadFlit && f.packet.nrOfFlits != 1)
                bWorm = true;
            if (f.isTailFlit && f.packet.nrOfFlits != 1)
                bWorm = false;

            return f;
        }

        public bool IsWorm {get { return bWorm;} }
        
        public Flit peek()
        {
            Flit f = null;
            if (buf.Count > 0)
                f = buf.Peek(); 
            return f;
        }

        public bool canEnqNewFlit(Flit f)
        {
#if debug
            Console.WriteLine("cyc {0} flit asking for enq pkt ID {1} flitNr {2}", Simulator.CurrentRound, f.packet.ID, f.flitNr);
#endif
            if (lookAheadSize < bufSize)
            {
                lookAheadSize++;
                return true;
            }
            return false;
        }
    }

    public class Router_Node_Buffer : Router
    {
        Flit m_injectSlot_CW;
        Flit m_injectSlot_CCW;
        RC_Coord rc_coord;
        Queue<Flit>[] ejectBuffer;
        WormholeBuffer[] m_ringBuf;
        int starveCounter;
        int Local = 0;// defined in router_bridge

        public Router_Node_Buffer(Coord myCoord)
            : base(myCoord)
        {
            // the node Router is just a Ring node. A Flit gets ejected or moves straight forward
            linkOut = new Link[2];
            linkIn = new Link[2];
            m_injectSlot_CW = null;
            m_injectSlot_CCW = null;
            throttle[ID] = false;
            starved[ID] = false;
            starveCounter = 0;
            m_ringBuf = new WormholeBuffer[2];
            for (int i = 0; i < 2; i++)
                m_ringBuf[i] = new WormholeBuffer(Config.ringBufferSize);
        }

        public Router_Node_Buffer(RC_Coord RC_c, Coord c) : base(c)
        {
            linkOut = new Link[2];
            linkIn = new Link[2];
            m_injectSlot_CW = null;
            m_injectSlot_CCW = null;
            rc_coord = RC_c;
            ejectBuffer = new Queue<Flit> [2];
            for (int i = 0; i < 2; i++)
                ejectBuffer[i] = new Queue<Flit>();
            throttle[ID] = false;
            starved[ID] = false;
            starveCounter = 0;
            m_ringBuf = new WormholeBuffer[2];
            for (int i = 0; i < 2; i++)
                m_ringBuf[i] = new WormholeBuffer(Config.ringBufferSize);
        }

        // TODO: later on need to check ejection fifo
        public override bool creditAvailable(Flit f)
        {
            return m_ringBuf[f.packet.pktParity].canEnqNewFlit(f);
        }

        protected void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);
            m_n.receiveFlit(f);
        }

        Flit ejectLocal()
        {
            // eject locally-destined flit (highest-ranked, if multiple)
            Flit ret = null;
            int flitsTryToEject = 0;
            int bestDir = -1;


            // Check both directions
            for (int dir = 0; dir < 2; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                        linkIn[dir].Out.state != Flit.State.Placeholder &&
                        linkIn[dir].Out.dest.ID == ID)
                {
                    ret = linkIn[dir].Out;
                    bestDir = dir;
                    flitsTryToEject ++;
                }
            }

            if (bestDir != -1) linkIn[bestDir].Out = null;
            Simulator.stats.flitsTryToEject[flitsTryToEject].Add();

#if debug
            if (ret != null)
                Console.WriteLine("ejecting flit {0} flitnr {1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            return ret;
        }

        // TODO: Caveat: THIS MIGHT NOT BE NECESSARY. Can put this in
        // do step. Imagine injeciton slow as part of fifo.
        // Injection into the ring:
        // TODO: need to check downstream buffer occupancy: 
        //       1) If eject next node check next node's ejeciton buffer
        //       2) If some other nodes, check next node's ring buffer
        // TODO: How do I take care of the case that the next cycle the ring buffer becomes occupied and it also wants
        // to eject at the same node?  
        // Ans: Since I already send the head, I have to continue sending.
        //
        public override bool canInjectFlit(Flit f)
        {
            if (throttle[ID])
                return false;
            bool can;

            // Body flits and tail flits have to follow the head flit
            // direction since we are using wormhole flow control
            if (!f.isHeadFlit)
            {
                f.parity = f.packet.pktParity;
            }

            if (f.parity == 0)
            {
                if (m_injectSlot_CW != null)
                    f.timeWaitToInject ++;
                can = m_injectSlot_CW == null;
            }
            else if (f.parity == 1)
            {
                if (m_injectSlot_CCW != null)
                    f.timeWaitToInject ++;
                can = m_injectSlot_CCW == null;
            }
            else if (f.parity == -1)
            {
                if (m_injectSlot_CW != null && m_injectSlot_CCW != null)
                    f.timeWaitToInject ++;
                can = (m_injectSlot_CW == null || m_injectSlot_CCW == null);
            }
            else throw new Exception("Unkown parity value!");

            // Being blocked in the injection queue
            // TODO: this starvation count seems incorrect here! Because there
            // is no guarantee on injection of m_injectSlot_CW and
            // m_injectSlow_CCW since these can just be imagined as a 1-flit
            // buffer.
            if (!can)
            {
                starveCounter ++;
                Simulator.stats.injStarvation.Add();
            }

            // TODO: this is for guaranteeing delievery?
            if (starveCounter == Config.starveThreshold)
                starved[ID] = true;

            return can;
        }

        public override void InjectFlit(Flit f)
        {
            //if (Config.topology != Topology.Mesh && Config.topology != Topology.MeshOfRings && f.parity != -1)
            //    throw new Exception("In Ring based topologies, the parity must be -1");

            starveCounter = 0;
            starved[ID] = false;

            if (f.parity == 0)
            {
                if (m_injectSlot_CW == null)
                    m_injectSlot_CW = f;
                else
                    throw new Exception("InjectFlit fault: The slot is empty!");
            }
            else if (f.parity == 1)
            {
                if (m_injectSlot_CCW == null)
                    m_injectSlot_CCW = f;
                else
                    throw new Exception("InjectFlit fault: The slot is empty!");
            }
            else
            {
                // Preference on which ring to route to. As long as there is
                // one ring open for injection, there's no correctness issue.
                int preference = -1;
                int dest = f.packet.dest.ID;
                int src = f.packet.src.ID;

                if (Config.topology == Topology.SingleRing)
                {
                    if ((dest - src + Config.N) % Config.N < Config.N / 2)
                        preference = 0;
                    else
                        preference = 1;
                }
                else if (dest / 4 == src / 4)
                {
                    if ((dest - src + 4) % 4 < 2)
                        preference = 0;
                    else if ((dest - src + 4) % 4 > 2)
                        preference = 1;
                }
                else if (Config.topology == Topology.HR_4drop)
                {
                    if (ID == 1 || ID == 2 || ID == 5 || ID == 6 || ID == 8 || ID == 11 || ID == 12 || ID == 15 )
                        preference = 0;
                    else
                        preference = 1;
                }
                else if (Config.topology == Topology.HR_8drop || Config.topology == Topology.HR_8_8drop)
                {
                    if (ID % 2 == 0)
                        preference = 0;
                    else
                        preference = 1;
                }
                else if (Config.topology == Topology.HR_8_16drop)
                {
                    if (dest / 8 == src / 8)
                    {
                        if ((dest - src + 8) % 8 < 4)
                            preference = 0;
                        else
                            preference = 1;
                    }
                    else
                    {
                        if (ID % 2 == 0)
                            preference = 1;
                        else
                            preference = 0;
                    }
                }

                if (Config.NoPreference)
                    preference = -1;
                if (preference == -1)
                    preference = Simulator.rand.Next(2);

                if (preference == 0)
                {
                    if (m_injectSlot_CW == null)
                        m_injectSlot_CW = f;
                    else if (m_injectSlot_CCW == null)
                        m_injectSlot_CCW = f;
                }
                else if (preference == 1)
                {
                    if (m_injectSlot_CCW == null)
                        m_injectSlot_CCW = f;
                    else if (m_injectSlot_CW == null)
                        m_injectSlot_CW = f;
                }
                else
                    throw new Exception("Unknown preference!");

                // Set the direction for the entire packet since we are doing
                // wormhole flow control in buffered ring
                if (f.isHeadFlit)
                {
                    if (m_injectSlot_CW == f)
                        f.packet.pktParity = 0;
                    else if (m_injectSlot_CCW == f)
                        f.packet.pktParity = 1;
                }
            }
#if debug
            if (f != null)
            {
                if (m_injectSlot_CW == f)
                    Console.WriteLine("Router Inject flit cyc {0} node coord {3} id {4} -> flit id {5} flitNr {1} :: CW pktParity {2}", Simulator.CurrentRound, f.flitNr, f.packet.pktParity, coord, ID, f.packet.ID);
                else if (m_injectSlot_CCW == f)
                    Console.WriteLine("Router Inject flit cyc {0} node coord {3} id {4} -> flit id {5} flitNr {1} :: CCW pktParity {2}", Simulator.CurrentRound, f.flitNr, f.packet.pktParity, coord, ID, f.packet.ID);
            }
#endif

        }

        protected override void _doStep()
        {
            // Record timing 
            for (int i = 0; i < 2; i++)
            {
                if (linkIn[i].Out != null && Config.N == 16)
                {
                    Flit f = linkIn[i].Out;
                    if (f.packet.src.ID / 4 == ID / 4)
                        f.timeInTheSourceRing+=1;
                    else if (f.packet.dest.ID / 4 == ID / 4)
                        f.timeInTheDestRing +=1;
                    else
                        f.timeInTheTransitionRing += 1;
                }
            }

            // Ejection to the local node
            Flit f1 = null, f2 = null;
            // This is using two ejection buffers, BUT there's only ejection
            // port to the local PE.
            if (Config.EjectBufferSize != -1 && Config.RingEjectTrial == -1)
            {
                for (int dir =0; dir < 2; dir ++)
                {
                    if (linkIn[dir].Out != null && linkIn[dir].Out.packet.dest.ID == ID && ejectBuffer[dir].Count < Config.EjectBufferSize)
                    {
                        ejectBuffer[dir].Enqueue(linkIn[dir].Out);
                        linkIn[dir].Out = null;
                    }
                }
                int bestdir = -1;
                for (int dir = 0; dir < 2; dir ++)
                {
                    //					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || ejectBuffer[dir].Peek().injectionTime < ejectBuffer[bestdir].Peek().injectionTime))
                    if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || ejectBuffer[dir].Count > ejectBuffer[bestdir].Count))
                        //					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || Simulator.rand.Next(2) == 1))
                        bestdir = dir;
                }
                if (bestdir != -1)
                    acceptFlit(ejectBuffer[bestdir].Dequeue());
            }
            else
            {
                for (int i = 0; i < Config.RingEjectTrial; i++)
                {
                    Flit eject = ejectLocal();
                    if (i == 0) f1 = eject;
                    else if (i == 1) f2 = eject;
                    if (eject != null)
                        acceptFlit(eject);
                }
                if (f1 != null && f2 != null && f1.packet == f2.packet)
                    Simulator.stats.ejectsFromSamePacket.Add(1);
                else if (f1 != null && f2 != null)
                    Simulator.stats.ejectsFromSamePacket.Add(0);
            }
            
            // Arbitration and flow control
            for (int dir = 0; dir < 2; dir++)
            {
                // Enqueue into the buffer
                if (linkIn[dir].Out != null)
                {
#if debug
                    Console.WriteLine("cyc {0} dir {2} node {4} id {5}:: enq {1} flitNr {3}", Simulator.CurrentRound, linkIn[dir].Out.packet.ID, dir, linkIn[dir].Out.flitNr, coord, ID);
#endif
                    m_ringBuf[dir].enqueue(linkIn[dir].Out);
                    linkIn[dir].Out = null;
                    Simulator.stats.flitsPassBy.Add(1);
                }
                
                // Arbitrate betwen local flit and buffered flit
                Flit winner = null;
                Flit _injSlot = (dir == 0) ? m_injectSlot_CW : m_injectSlot_CCW;
                Flit _bufHead = m_ringBuf[dir].peek();

#if debug
                Console.WriteLine("cyc {0} arb: buf_null {1} injslot_null {2}", Simulator.CurrentRound, _bufHead == null, _injSlot == null);
#endif

                if (_injSlot != null && _bufHead == null && m_ringBuf[dir].IsWorm == false)
                    winner = _injSlot;
                else if (_injSlot == null && _bufHead != null)
                    winner = _bufHead;
                else if (_injSlot != null && _bufHead != null)
                {
                    // Priority is always given to flit in the ring buffer,
                    // execpt if the header flit of the local injection
                    // buffer has already been sent downstream. This is to
                    // ensure wormhole flow control
                    if (_injSlot.isHeadFlit && _bufHead.isHeadFlit)
                        winner = _bufHead;
                    else if (_injSlot.isHeadFlit && !_bufHead.isHeadFlit)
                        winner = _bufHead;
                    else if (!_injSlot.isHeadFlit && _bufHead.isHeadFlit)
                        winner = _injSlot;
                    else
                        throw new Exception("Impossible for both flits in ring buffer and injection slot in active state.");
                }

                // Check downstream credit, if ring/ejection buffer is available then send it.
                if (winner != null)
                {
#if debug
                    Console.Write("cyc {0} :: dir {2} node_ID {4} arb winner {1} ishead {3} dest {5}-> ", Simulator.CurrentRound, winner.packet.ID, dir, winner.isHeadFlit, ID, winner.dest.ID);
                    if (winner == m_ringBuf[dir].peek())
                        Console.WriteLine("winner In ring buffer");
                    else if (winner == m_injectSlot_CW)
                        Console.WriteLine("winner In inj cw");
                    else if (winner == m_injectSlot_CCW)
                        Console.WriteLine("winner In inj ccw");
#endif
                    // TODO: not sure why we need to check whether linkOut.in
                    // is empty or not. Double check on this.
                    // Need to check downstream buffer occupancy: 
                    // 1) If eject next node check next node's ejeciton buffer
                    // 2) If some other nodes, check next node's ring buffer
                    if (checkDownStreamCredit(winner) && linkOut[dir].In == null) 
                    {
                        if (winner == m_ringBuf[dir].peek())
                            m_ringBuf[dir].dequeue();
                        else if (winner == m_injectSlot_CW)
                            m_injectSlot_CW = null;
                        else if (winner == m_injectSlot_CCW)
                            m_injectSlot_CCW = null;

                        linkOut[dir].In = winner;
                        Console.WriteLine("inject");
                        statsInjectFlit(winner);
                    }
                    else
                    {
                        // Being blocked
                    }
                }
            }
        }

        /* 
         * In order to check downstream ID, we need to know the topology and
         * whether we should check local ring's or global ring's buffer and
         * ejection buffer.
         *
         * TODO: currently we do not check ejection fifos since we do not have
         * them.
         * */
        bool checkDownStreamCredit(Flit f)
        {
            if (f == null)
                throw new Exception("Null flit checking credit.");

            if (Config.topology == Topology.SingleRing)
            {
                int _nextNodeID;
                if (f.packet.pktParity == 0)
                    _nextNodeID = (ID + 1) % Config.N;
                else
                    _nextNodeID = (ID == 0) ? Config.N-1 : (ID - 1) % Config.N;
#if debug
                Console.WriteLine("cyc {0} flit asking for credit ID {1} flitNr {2} dir {3} nextNodeID {4}", Simulator.CurrentRound, f.packet.ID, f.flitNr, f.packet.pktParity, _nextNodeID);
#endif
                // TODO: assume always successful ejection
                if (f.dest.ID == _nextNodeID)
                    return true;
                return Simulator.network.nodeRouters[_nextNodeID].creditAvailable(f);
            }
            else if (Config.topology == Topology.HR_4drop)
            {
                int subNodeID = ID % 4;
                int localRingID = ID / 4;
                int _nextNodeID, _nextBridgeID;

                // Determining if there's a bridge router between current
                // node and the next node
                if (f.packet.pktParity == 0)
                {
                    _nextNodeID = (subNodeID + 1) % 4 + localRingID * 4;
                    _nextBridgeID = Array.IndexOf(Simulator.network.GetCWnext, _nextNodeID);
#if debug2
                    Console.WriteLine("Bridge search cyc {0} CW : bridge index {1}", Simulator.CurrentRound, _nextBridgeID);
#endif
                }
                else
                {
                    _nextNodeID = (subNodeID == 0) ? 3 : subNodeID - 1;
                    _nextNodeID += localRingID * 4;
                    _nextBridgeID = Array.IndexOf(Simulator.network.GetCCWnext, _nextNodeID);
#if debug2
                    Console.WriteLine("Bridge search cyc {0} CW : bridge index {1}", Simulator.CurrentRound, _nextBridgeID);
#endif
                }
                
#if debug
                Console.WriteLine("cyc {0} flit asking for credit ID {1} flitNr {2} dir {3} nextNodeID {4} nextBridge {5}", Simulator.CurrentRound, f.packet.ID, f.flitNr, f.packet.pktParity, _nextNodeID, _nextBridgeID);
#endif
                // If bridge is there, check if we are ejecting to bridge
                if (_nextBridgeID != -1)
                {
                    if (Simulator.network.bridgeRouters[_nextBridgeID].productive(f, Local) == true)
                    {
                        return Simulator.network.bridgeRouters[_nextBridgeID].creditAvailable(f);
                    }
                }

                if (f.dest.ID == _nextNodeID)
                    return true;
                return Simulator.network.nodeRouters[_nextNodeID].creditAvailable(f);
            }
            else
                throw new Exception("Not Supported");

            return false;
        }
    }


    public class Router_Switch_Buffer : Router
    {
        int bufferDepth = 1;
        public static int[,] bufferCurD = new int[Config.N,4];
        Flit[,] buffers;// = new Flit[4,32];

        public Router_Switch_Buffer(int ID) : base()
        {
            coord.ID = ID;
            enable = true;
            m_n = null;
            buffers = new Flit[4,32]; // actual buffer depth is decided by bufferDepth
            for (int i = 0; i < 4; i++)
                for (int n = 0; n < bufferDepth; n++)
                    buffers[i, n] = null;
        }

        int Local_CW = 0;
        int Local_CCW = 1;
        int GL_CW = 2;
        int GL_CCW = 3;

        // different routing algorithms can be implemented by changing this function
        private bool productiveRouter(Flit f, int port)
        {
            //			Console.WriteLine("Net/RouterRing.cs");
            int RingID = ID / 4;
            if (Config.HR_NoBias)
            {
                if ((port == Local_CW || port == Local_CCW) && RingID == f.packet.dest.ID / 4)
                    return false;
                else if ((port == Local_CW || port == Local_CCW) && RingID != f.packet.dest.ID / 4)
                    return true;
                else if ((port == GL_CW || port == GL_CCW) && RingID == f.packet.dest.ID / 4)
                    return true;
                else if ((port == GL_CW || port == GL_CCW) && RingID != f.packet.dest.ID / 4)
                    return false;
                else
                    throw new Exception("Unknown port of switchRouter");
            }
            else if (Config.HR_SimpleBias)
            {
                if ((port == Local_CW || port == Local_CCW) && RingID == f.packet.dest.ID / 4)
                    return false;
                else if ((port == Local_CW || port == Local_CCW) && RingID != f.packet.dest.ID / 4)
                    //		if (RingID + f.packet.dest.ID / 4 == 3)	 //diagonal. can always inject
                    return true;
                /*		else if (RingID == 0 && destID == 1) return ID == 2 || ID == 1;
                        else if (RingID == 0 && destID == 2) return ID == 3 || ID == 0;
                        else if (RingID == 1 && destID == 0) return ID == 4 || ID == 5;
                        else if (RingID == 1 && destID == 3) return ID == 6 || ID == 7;
                        else if (RingID == 2 && destID == 0) return ID == 8 || ID == 9;
                        else if (RingID == 2 && destID == 3) return ID == 10 || ID == 11;
                        else if (RingID == 3 && destID == 1) return ID == 13 || ID == 14;
                        else if (RingID == 3 && destID == 2) return ID == 12 || ID == 15;
                        else
                        throw new Exception("Unknown src and dest in Hierarchical Ring");*/
                else if ((port == GL_CW || port == GL_CCW) && RingID == f.packet.dest.ID / 4)
                    return true;
                else if ((port == GL_CW || port == GL_CCW) && RingID != f.packet.dest.ID / 4)
                    return false;
                else
                    throw new Exception("Unknown port of switchRouter");

            }
            else
                throw new Exception("Unknow Routing Algorithm for Hierarchical Ring");
        }

        protected override void _doStep()
        {
            switchRouterStats();
            // consider 4 input ports seperately. If not productive, keep circling
            if (!enable) return;
            /*        	if (ID == 3)
                        {
                        if (linkIn[0].Out != null && linkIn[0].Out.packet.dest.ID == 11)
                        Console.WriteLine("parity : {0}, src:{1}", linkIn[0].Out.parity, linkIn[0].Out.packet.src.ID);
                        }*/
            for (int dir = 0; dir < 4; dir ++)
            {
                int i;
                for (i = 0; i < bufferDepth; i++)
                    if (buffers[dir, i] == null)
                        break;
                //Console.WriteLine("linkIn[dir] == null:{0},ID:{1}, dir:{2}", linkIn[dir] == null, ID, dir);
                bool productive = (linkIn[dir].Out != null)? productiveRouter(linkIn[dir].Out, dir) : false;
                //Console.WriteLine("productive: {0}", productive);
                if (linkIn[dir].Out != null && (!productive || i == bufferDepth)) // nonproductive or the buffer is full : bypass the router
                {
                    linkOut[dir].In = linkIn[dir].Out;
                    Console.WriteLine("transit");
                    linkIn[dir].Out = null;
                }
                else if (linkIn[dir].Out != null)    //productive direction and the buffer has empty space, add into buffer
                {
                    int k;
                    for (k = 0; k < bufferDepth; k++)
                    {
                        if (buffers[dir, k] == null)
                        {
                            buffers[dir, k] = linkIn[dir].Out;
                            linkIn[dir].Out = null;
                            break;
                        }
                        //Console.WriteLine("{0}", k);
                    }
                    if (k == bufferDepth)
                        throw new Exception("The buffer is full!!");
                }
            }
            // if there're extra slots in the same direction network, inject from the buffer
            for (int dir = 0; dir < 4; dir ++)
            {
                if (linkOut[dir].In == null)    // extra slot available
                {
                    int posdir = (dir+2) % 4;
                    if (buffers[posdir, 0] != null)
                    {
                        linkOut[dir].In = buffers[posdir, 0];
                        buffers[posdir, 0] = null;
                    }
                }
            }
            // if the productive direction with the same parity is not available. The direction with the other parity is also fine
            for (int dir = 0; dir < 4; dir ++)
            {
                int crossdir = 3 - dir;
                if (linkOut[dir].In == null)    // extra slot available
                {
                    if (buffers[crossdir, 0] != null)
                    {
                        // for HR_SimpleBias is the dir is not a local ring, can't change rotating direction
                        if (Config.HR_SimpleBias && (dir == GL_CW || dir == GL_CCW))
                            continue;
                        linkOut[dir].In = buffers[crossdir, 0];
                        Console.WriteLine("transit");
                        buffers[crossdir, 0] = null;
                    }
                }
            }
            if (Config.HR_NoBuffer)
            {
                for (int dir = 0; dir < 4; dir ++)
                {
                    if (buffers[dir, 0] != null)
                    {
                        if (linkOut[dir].In != null)
                            throw new Exception("The outlet of the buffer is blocked");
                        linkOut[dir].In = buffers[dir, 0];
                        buffers[dir, 0] = null;
                    }
                }
            }
            // move all the flits in the buffer if the head flit is null
            for (int dir = 0; dir < 4; dir ++)
            {
                if (buffers[dir, 0] == null)
                {
                    for (int i = 0; i < bufferDepth - 1; i++)
                        buffers[dir, i] = buffers[dir, i + 1];
                    buffers[dir, bufferDepth-1] = null;
                }
            }
            for (int dir = 0; dir < 4; dir ++)
            {
                int i;
                for (i = 0; i < bufferDepth; i++)
                    if (buffers[dir, i] == null)
                        break;
                bufferCurD[ID,dir] = i;
            }
        }

        void switchRouterStats()
        {
            for (int dir = 0; dir < 4; dir ++)
            {
                Flit f = linkIn[dir].Out;
                if (f != null && (dir == Local_CW || dir == Local_CCW))
                {
                    if (f.packet.src.ID / 4 == ID / 4)
                        f.timeInTheSourceRing ++;
                    else if (f.packet.dest.ID / 4 == ID / 4)
                        f.timeInTheDestRing ++;
                }
                else if (f != null && (dir == GL_CW || dir == GL_CCW))
                    f.timeInGR += 2;
            }
            return;
        }

        public override bool canInjectFlit(Flit f)
        {
            return false;
        }
        public override void InjectFlit(Flit f)
        {
            return;
        }
    }
}
