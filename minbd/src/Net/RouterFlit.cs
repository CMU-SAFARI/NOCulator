//#define DEBUG
//#define RETX_DEBUG
//#define memD

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public abstract class Router_Flit : Router
    {
		// injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;
        
        ResubBuffer rBuf;

        public Router_Flit(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot  = null;
            m_injectSlot2 = null;
            rBuf = new ResubBuffer();
        }

        Flit handleGolden(Flit f)
        {
            if (f == null)
                return f;

            if (f.state == Flit.State.Normal)
                return f;

            if (f.state == Flit.State.Rescuer)
            {
                if (m_injectSlot == null)
                {
                    m_injectSlot = f;
                    f.state = Flit.State.Placeholder;
                }
                else
                    m_injectSlot.state = Flit.State.Carrier;

                return null;
            }

            if (f.state == Flit.State.Carrier)
            {
                f.state = Flit.State.Normal;
                Flit newPlaceholder = new Flit(null, 0);
                newPlaceholder.state = Flit.State.Placeholder;

                if (m_injectSlot != null)
                    m_injectSlot2 = newPlaceholder;
                else
                    m_injectSlot = newPlaceholder;

                return f;
            }

            if (f.state == Flit.State.Placeholder)
                throw new Exception("Placeholder should never be ejected!");

            return null;
        }

        // accept one ejected flit into rxbuf
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
            int bestDir = -1;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                    linkIn[dir].Out.state != Flit.State.Placeholder &&
                    linkIn[dir].Out.dest.ID == ID &&
                    (ret == null || rank(linkIn[dir].Out, ret) < 0))
                {
                    ret = linkIn[dir].Out;
                    bestDir = dir;
                }

            if (bestDir != -1) linkIn[bestDir].Out = null;
#if DEBUG
            if (ret != null)
                Console.WriteLine("ejecting flit {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            ret = handleGolden(ret);

            return ret;
        }

        Flit[] input = new Flit[4]; // keep this as a member var so we don't
        // have to allocate on every step (why can't
        // we have arrays on the stack like in C?)

        protected override void _doStep()
        {
            Flit[] eject = new Flit[4];
            eject[0] = eject[1] = eject[2] = eject[3] = null;
            
            int wantToEject = 0;
            for (int i = 0; i < 4; i++)
                if (linkIn[i] != null && linkIn[i].Out != null)
                {
                    if(linkIn[i].Out.dest.x == coord.x && linkIn[i].Out.dest.y == coord.y)
                        wantToEject++;
                }
            
            switch(wantToEject)
            {
                case 0: Simulator.stats.eject_0.Add(); break;
                case 1: Simulator.stats.eject_1.Add(); break;
                case 2: Simulator.stats.eject_2.Add(); break;
                case 3: Simulator.stats.eject_3.Add(); break;
                case 4: Simulator.stats.eject_4.Add(); break;
                default: throw new Exception("Eject problem");
            }
            
            for (int i = 0; i < Config.ejectCount; i++)
                eject[i] = ejectLocal();
            
            for (int i = 0; i < 4; i++) input[i] = null;

            // grab inputs into a local array so we can sort
            int c = 0;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                    input[c++] = linkIn[dir].Out;
                    linkIn[dir].Out.inDir = dir;
                    linkIn[dir].Out = null;
                }
            
            int before = 0;
            for (int i = 0; i < 4; i++)
                if (input[i] != null) before++;

            int reinjectedCount = 0;
            if (Config.resubmitBuffer)
            {
                if (!rBuf.isEmpty())
                    for (int i = 0; i < 4; i++)
                        if (reinjectedCount < Config.rebufInjectCount)
                        {
                            if (input[i] == null && !rBuf.isEmpty())
                            {
                                input[i] = rBuf.removeFlit();
                                input[i].nrInRebuf++;
                                reinjectedCount++;
                            }
                        }
            }
            
            int after = 0;
            for (int i = 0; i < 4; i++)
                if (input[i] != null) after++;
            if (before + reinjectedCount != after)
                throw new Exception ("Something weird happened on resubmit buffer");
            
            /*for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    if (input[i] == null || input[j] == null)
                        continue;
                    if (i != j && input[i].Equals(input[j]))
                        throw new Exception("DUPLICATING");
                }
            */
            // sometimes network-meddling such as flit-injection can put unexpected
            // things in outlinks...
            int outCount = 0;
            for (int dir = 0; dir < 4; dir++)
                if (linkOut[dir] != null && linkOut[dir].In != null)
                    outCount++;

            bool wantToInject = m_injectSlot2 != null || m_injectSlot != null;
            bool canInject = (c + outCount) < neighbors;
            bool starved = wantToInject && !canInject;
			
            if (starved)
            {
                Flit starvedFlit = null;
                if (starvedFlit == null) starvedFlit = m_injectSlot2;
                if (starvedFlit == null) starvedFlit = m_injectSlot;

                Simulator.controller.reportStarve(coord.ID);
                statsStarve(starvedFlit);
            }
            if (canInject && wantToInject)
            {
                Flit inj_peek = null; 
                if(m_injectSlot2 != null)
                    inj_peek = m_injectSlot2;
                else if (m_injectSlot != null)
                    inj_peek = m_injectSlot;
                if(inj_peek == null)
                    throw new Exception("Inj flit peek is null!!");

                if(!Simulator.controller.ThrottleAtRouter || Simulator.controller.tryInject(coord.ID))
                {
                    Flit inj = null;
                    if (m_injectSlot2 != null)
                    {
                        inj = m_injectSlot2;
                        m_injectSlot2 = null;
                    }
                    else if (m_injectSlot != null)
                    {
                        inj = m_injectSlot;
                        m_injectSlot = null;
                    }
                    else
                        throw new Exception("what???inject null flits??");
                    input[c++] = inj;
#if DEBUG
                    Console.WriteLine("injecting flit {0}.{1} at node {2} cyc {3}",
                            m_injectSlot.packet.ID, m_injectSlot.flitNr, coord, Simulator.CurrentRound);
#endif
#if memD
                    int r=inj.packet.requesterID;
                    if(r==coord.ID)
                        Console.WriteLine("inject flit at node {0}<>request:{1}",coord.ID,r);
                    else
                        Console.WriteLine("Diff***inject flit at node {0}<>request:{1}",coord.ID,r);
#endif
                    statsInjectFlit(inj);
                }
            }
            
            for (int i = 0; i < Config.ejectCount; i++)
                if (eject[i] != null)
                    acceptFlit(eject[i]);

            // inline bubble sort is faster for this size than Array.Sort()
            // sort input[] by descending priority. rank(a,b) < 0 iff a has higher priority.
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                    if (input[j] != null &&
                        (input[i] == null ||
                         rank(input[j], input[i]) < 0))
                    {
                        Flit t = input[i];
                        input[i] = input[j];
                        input[j] = t;
                    }
			
            //int resubmittedCount = 0;
            //int numDeflected = 0;
            int[] deflected = new int[4];
            deflected[0] = deflected[1] = deflected[2] = deflected[3] = -1;
                
            // assign outputs
            for (int i = 0; i < 4 && input[i] != null; i++)
            {
                PreferredDirection pd = determineDirection(input[i], coord);
                int outDir = -1;
                Flit tempFlit = input[i];
				

		        	
		        if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                {
                    linkOut[pd.xDir].In = input[i];
                    outDir = pd.xDir;
                    
                    /* Refresh wasInBuf */
               	 	if (Config.wasInRebufCycleRefresh)
		        		tempFlit.wasInRebuf = false;
                }

                else if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                {
                    linkOut[pd.yDir].In = input[i];
                    outDir = pd.yDir;
                    
                    /* Refresh wasInBuf */
                	if (Config.wasInRebufCycleRefresh)
		        		tempFlit.wasInRebuf = false;
                }

                // deflect!
                else
                {
                    input[i].Deflected = true;
                    int dir = 0;
					/*deflected[numDeflected] = i;
                    numDeflected++;
                    
                    */
                    
                    if (Config.randomize_defl) 
                    	dir = Simulator.rand.Next(4); // randomize deflection dir (so no bias)
                    
                    for (int count = 0; count < 4; count++, dir = (dir + 1) % 4) {
                    	if (linkOut[dir] != null && linkOut[dir].In == null) {

							linkOut[dir].In = input[i];
                        	outDir = dir;
                        	break;
						}    
                        
                    }
                    
                    if (outDir == -1) throw new Exception(
                            String.Format("Ran out of outlinks in arbitration at node {0} on input {1} cycle {2} flit {3} c {4} neighbors {5} outcount {6}", coord, i, Simulator.CurrentRound, input[i], c, neighbors, outCount));
                }
            }
            /*
            if (Config.resubmitBuffer)
                sortDeflected(deflected);
            
            for (int i = 0; i < 4; i++)
            {
                if (deflected[i] == -1)
                    break;
                else
                {
                    int dir = 0;
                    if (Config.randomize_defl)
                        dir = Simulator.rand.Next(4); 

                    for (int count = 0; count < 4; count++, dir = (dir + 1) % 4) {
                        if(linkOut[dir] != null && linkOut[dir].In == null) {
                            linkOut[dir].In = input[deflected[i]];
                            //outDir = dir;
                            break;
                        }
                    }
                }
            }
*/
        }

        public void sortDeflected(int[] deflected)
        {
            int [] newDeflected = new int[4];
            newDeflected[0] = newDeflected[1] = newDeflected[2] = newDeflected[3] = -1;
            for (int i = 0; i < 4; i++)
            {
                if (deflected[i] == -1)
                    break;
                else
                {
                    newDeflected[3] = deflected[i];
                    for (int j = 3; j > 0; j--)
                    {
                        bool swap = false;
                        if (newDeflected[j-1] == -1)
                            swap = true;
                        else
                        {
                            Flit f1 = input[newDeflected[j-1]];
                            Flit f2 = input[newDeflected[j]];
                            if (0 < deflPriority(f1, f2))
                            {
                                swap = true;
                            }
                        }
                        if (swap)
                        {
                            int temp = newDeflected[j-1];
                            newDeflected[j-1] = newDeflected[j];
                            newDeflected[j]   = temp;
                        }
                    }
                }
            }
            
            for (int i = 0; i < Config.rebufRemovalCount; i++)
            {
                int index = newDeflected[i];
                if (index == -1 || input[index] == null)
                    break;

                if (!rBuf.isFull())
                {
                    rBuf.addFlit(input[index]);
                    input[index] = null;
                }
            }
        }

        protected int deflPriority(Flit f1, Flit f2)
        {   
            if (f1 == null && f2 == null)
                return  0;
            else if (f1 == null)
                return  1;
            else if (f2 == null)
                return -1;

            int ret;
            switch (Config.resubmitBy)
            {
                case "Random":  ret = (1 == Simulator.rand.Next(2)) ? -1 : 1;
                                break;
                case "Bias":    ret = -1;
                                break;
                default: throw new Exception("Not a resubmit scheme");
            }
            return ret;
        }

        public override bool canInjectFlit(Flit f)
        {
            return m_injectSlot == null;
        }

        public override void InjectFlit(Flit f)
        {
            if (m_injectSlot != null)
                throw new Exception("Trying to inject twice in one cycle");

            m_injectSlot = f;
        }

        public override void flush()
        {
            m_injectSlot = null;
        }

        protected virtual bool needFlush(Flit f) { return false; }
    }

    public class Router_Flit_OldestFirst : Router_Flit
    {
        public Router_Flit_OldestFirst(Coord myCoord)
            : base(myCoord)
        {
        }

        protected override bool needFlush(Flit f)
        {
            return Config.cheap_of_cap != -1 && age(f) > (ulong)Config.cheap_of_cap;
        }

        public static ulong age(Flit f)
        {
            if (Config.net_age_arbitration)
                return Simulator.CurrentRound - f.packet.injectionTime;
            else
                return (Simulator.CurrentRound - f.packet.creationTime) /
                        (ulong)Config.cheap_of;
        }

        public static int _rank(Flit f1, Flit f2)
        {
            if (f1 == null && f2 == null) return 0;
            if (f1 == null) return  1;
            if (f2 == null) return -1;


            bool f1_resc = (f1.state == Flit.State.Rescuer) || (f1.state == Flit.State.Carrier);
            bool f2_resc = (f2.state == Flit.State.Rescuer) || (f2.state == Flit.State.Carrier);
            bool f1_place = (f1.state == Flit.State.Placeholder);
            bool f2_place = (f2.state == Flit.State.Placeholder);

            int c0 = 0;
			
            if      (f1_resc  &&  f2_resc) c0 =  0;
            else if              (f1_resc) c0 = -1;
            else if              (f2_resc) c0 =  1;
            else if (f1_place && f2_place) c0 =  0;
            else if             (f1_place) c0 =  1;
            else if             (f2_place) c0 = -1;

            int c1 = 0, c2 = 0;
            if (f1.packet != null && f2.packet != null)
            {
                c1 = -age(f1).CompareTo(age(f2));
                c2 = f1.packet.ID.CompareTo(f2.packet.ID);
            }

            int c3 = f1.flitNr.CompareTo(f2.flitNr);

            int zerosSeen = 0;
            foreach (int i in new int[] { c0, c1, c2, c3 })
            {
                if (i == 0)
                    zerosSeen++;
                else
                    break;
            }
            Simulator.stats.net_decisionLevel.Add(zerosSeen);

            return
				(c0 != 0) ? c0 : 
                (c1 != 0) ? c1 :
                (c2 != 0) ? c2 :
                 c3;
        }

        public override int rank(Flit f1, Flit f2)
        {
            return _rank(f1, f2);
        }

        public override void visitFlits(Flit.Visitor fv)
        {
            if (m_injectSlot != null)
                fv(m_injectSlot);
            if (m_injectSlot2 != null)
                fv(m_injectSlot2);
        }
    }

    public class Router_Flit_Prio : Router_Flit
    {
        public Router_Flit_Prio(Coord myCoord)
            : base(myCoord)
        {
        }

        protected override bool needFlush(Flit f)
        {
            return Config.cheap_of_cap != -1 && age(f) > (ulong)Config.cheap_of_cap;
        }

        public static ulong age(Flit f)
        {
            if (Config.net_age_arbitration)
                return Simulator.CurrentRound - f.packet.injectionTime;
            else
                return (Simulator.CurrentRound - f.packet.creationTime) /
                        (ulong)Config.cheap_of;
        }

        public static int _rank(Flit f1, Flit f2)
        {
            if (f1 == null && f2 == null) return 0;
            if (f1 == null) return 1;
            if (f2 == null) return -1;

            bool f1_resc = (f1.state == Flit.State.Rescuer) || (f1.state == Flit.State.Carrier);
            bool f2_resc = (f2.state == Flit.State.Rescuer) || (f2.state == Flit.State.Carrier);
            bool f1_place = (f1.state == Flit.State.Placeholder);
            bool f2_place = (f2.state == Flit.State.Placeholder);

            int c0 = 0;
            if (f1_resc && f2_resc)
                c0 = 0;
            else if (f1_resc)
                c0 = -1;
            else if (f2_resc)
                c0 = 1;
            else if (f1_place && f2_place)
                c0 = 0;
            else if (f1_place)
                c0 = 1;
            else if (f2_place)
                c0 = -1;

            int c1 = 0, c2 = 0;
            if (f1.packet != null && f2.packet != null)
            {
                //TODO: need to change here to take into account of the priority
                c1 = -age(f1).CompareTo(age(f2));
                c2 = f1.packet.ID.CompareTo(f2.packet.ID);
            }

            int c3 = f1.flitNr.CompareTo(f2.flitNr);

            int zerosSeen = 0;
            foreach (int i in new int[] { c0, c1, c2, c3 })
            {
                if (i == 0)
                    zerosSeen++;
                else
                    break;
            }
            Simulator.stats.net_decisionLevel.Add(zerosSeen);

            return
                (c0 != 0) ? c0 :
                (c1 != 0) ? c1 :
                (c2 != 0) ? c2 :
                c3;
        }

        public override int rank(Flit f1, Flit f2)
        {
            return _rank(f1, f2);
        }

        public override void visitFlits(Flit.Visitor fv)
        {
            if (m_injectSlot != null)
                fv(m_injectSlot);
            if (m_injectSlot2 != null)
                fv(m_injectSlot2);
        }
    }

    /*
      Golden Packet is conceptually like this:
      
      for mshr in mshrs:
        for node in nodes:
          prioritize (node,mshr) request packet over all others, for L cycles

      where L = X+Y for an XxY network. All non-prioritized packets are arbitrated
      arbitrarily, if you will (say, round-robin).
    */

    public class Router_Flit_GP : Router_Flit
    {    
        public Router_Flit_GP(Coord myCoord)
            : base(myCoord)
        {
        }

        public static int _rank(Flit f1, Flit f2)
        {
            // priority is:
            // 1. Carrier (break ties by dest ID)
            // 2. Rescuer (break ties by dest ID)
            // 3. Golden normal flits (break ties by flit no.)
            // 4. Non-golden normal flits (break ties arbitrarily)
            // 5. Placeholders

			
            if (f1 == null && f2 == null)
                return  0;

            if (f1 == null)
            {
            	if (Config.prioByInfection && f2.infected)
                	cure(f2);
                return  1;
            }
            
            if (f2 == null)
            {
                if (Config.prioByInfection && f1.infected)
                	cure(f1);
                return -1;
            }

            if (f1.state == Flit.State.Carrier && f2.state == Flit.State.Carrier)
                return f1.dest.ID.CompareTo(f2.dest.ID);
            else if (f1.state == Flit.State.Carrier)
                return -1;
            else if (f2.state == Flit.State.Carrier)
                return 1;

            if (f1.state == Flit.State.Rescuer && f2.state == Flit.State.Rescuer)
                return f1.dest.ID.CompareTo(f2.dest.ID);
            else if (f1.state == Flit.State.Carrier)
                return -1;
            else if (f2.state == Flit.State.Carrier)
                return 1;

            if (f1.state == Flit.State.Normal && f2.state == Flit.State.Normal)
            {
                bool golden1 = Simulator.network.golden.isGolden(f1),
                     golden2 = Simulator.network.golden.isGolden(f2);
				bool silver1 = false, 
                     silver2 = false;
                
                bool running1 = f1.hardstate == Flit.HardState.Running,
                     running2 = f2.hardstate == Flit.HardState.Running;
                bool excited1 = f1.hardstate == Flit.HardState.Excited,
                     excited2 = f2.hardstate == Flit.HardState.Excited;

                switch(Config.silverMode)
                {
                    case "epoch":
                        silver1 = Simulator.network.golden.isSilver(f1);
                        silver2 = Simulator.network.golden.isSilver(f2);
                        break;
                    case "random":
                        silver1 = f1.isSilver;
                        silver2 = f2.isSilver;
                        break;
                    case "none":
                        silver1 = false;
                        silver2 = false;
                        break;
                    default:
                        throw new Exception("Silver mode not yet implemented");
                }
                    
                if (golden1 && golden2) {
                    int g1 = Simulator.network.golden.goldenLevel(f1),
                        g2 = Simulator.network.golden.goldenLevel(f2);

                    if (g1 != g2)
                        return g1.CompareTo(g2);
                    else
                        return f1.flitNr.CompareTo(f2.flitNr);
                }
                else if (golden1)
                    return -1;
                else if (golden2)
                    return  1;
                else if (silver1 && silver2)
                    return  f1.flitNr.CompareTo(f2.flitNr);
                else if (silver1)
                    return -1;
                else if (silver2)
                    return  1;
                else if (running1 && running2)
                    return (1 == Simulator.rand.Next(2)) ? -1 : 1;
                else if (running1)
                    return -1;
                else if (running2)
                    return  1;
                else if (excited1 & excited2)
                    return (1 == Simulator.rand.Next(2)) ? -1 : 1;
                else if (excited1)
                    return -1;
                else if (excited2)
                    return  1;
                else {
                	f1.priority = 0;
                	f2.priority = 0;
                	
                	if (Config.deflPrio) {
                		f1.priority += (f1.wasDeflected) ? 1 : 0;
                		f2.priority += (f2.wasDeflected) ? 1 : 0;
                	}
                	
                	if (Config.initFlitPrio) {
                		f1.priority += (f1.initPrio > 1) ? 1 : 0;
                		f2.priority += (f2.initPrio > 1) ? 1 : 0;
                	}
                	
                	if (Config.packetPrio) {
                		f1.priority += (f1.packet.chip_prio > 1) ? 1 : 0;
                		f2.priority += (f2.packet.chip_prio > 1) ? 1 : 0;
                	}
                	
                	if (Config.rebufPrio) {
                		f1.priority += (f1.wasInRebuf) ? 1 : 0;
                		f2.priority += (f2.wasInRebuf) ? 1 : 0;
                	}
                	
                	if (Config.infectPrio) {
                		f1.priority += (f1.infected) ? 1 : 0;
                		f2.priority += (f2.infected) ? 1 : 0;
                	}
                	
                	if (Config.deflectOverInitPrio) {
                		f1.priority += (f1.wasDeflected) ? 2 : (f1.initPrio > 1) ? 1 : 0;
                		f2.priority += (f2.wasDeflected) ? 2 : (f2.initPrio > 1) ? 1 : 0;
                	}
                	
                	if (Config.distancePrio) {
                		int distance1 = Math.Abs(f1.currentX - f1.packet.dest.x) + Math.Abs(f1.currentY - f1.packet.dest.y); 
                		int distance2 = Math.Abs(f2.currentX - f2.packet.dest.x) + Math.Abs(f2.currentY - f2.packet.dest.y);
                		f1.priority += (distance1 < distance2) ? 1 : 0;
                		f2.priority += (distance1 > distance2) ? 1 : 0;
                	}
                	deflectInfect(f1, f2);
					
					if(f1.priority > f2.priority)
						return (Config.deprioritize) ?  1 : -1;
					else if (f1.priority < f2.priority)
						return (Config.deprioritize) ? -1 :  1;
					else
					{
						if (Config.prioComplexDist) 
							return complexDist(f1, f2);
						else 
                    		return (1 == Simulator.rand.Next(2)) ? -1 : 1;
                    }
                }
            }
            else if (f1.state == Flit.State.Normal)
                return -1;
            else if (f2.state == Flit.State.Normal)
                return  1;
            else // both are placeholders
                return (1 == Simulator.rand.Next(2)) ? -1 : 1;
        }
        
        protected static void updatePriority(Flit f)
        {
            int distance = Math.Abs(f.currentX - f.packet.dest.x) + Math.Abs(f.currentY - f.packet.dest.y); 
            
            if (Config.deflPrio)
                f.priority += (f.wasDeflected) ? 1 : 0;
            
            if (Config.initFlitPrio)
                f.priority += f.initPrio;
            
            if (Config.packetPrio)
                f.priority += f.packet.chip_prio;
            
            if (Config.rebufPrio)
                f.priority += (f.wasInRebuf) ? 1 : 0;
            
            if (Config.infectPrio)
                f.priority += (f.infected) ? 1 : 0;
            
            if (Config.deflectOverInitPrio)
                f.priority += (f.wasDeflected) ? 2 : (f.initPrio > 1) ? 1 : 0;
            
            if (Config.distancePrio)
                f.priority += distance;
        }

		protected static int complexDist(Flit f1, Flit f2)
		{
			int f1_dist = Math.Abs(f1.currentX - f1.packet.dest.x) + Math.Abs(f1.currentY - f1.packet.dest.y);
			int f2_dist = Math.Abs(f2.currentX - f2.packet.dest.x) + Math.Abs(f2.currentY - f2.packet.dest.y);
							
			if (f1_dist == 0 && f2_dist == 0) {
				Simulator.stats.prio_bothZeroDistance.Add();
				return (1 == Simulator.rand.Next(2)) ? -1 : 1;
			}
			else if (f1_dist == 0) {
				Simulator.stats.prio_oneZeroDistance.Add();
				return  1;
			}
			else if (f2_dist == 0) {
				Simulator.stats.prio_oneZeroDistance.Add();
				return -1;
			}
			else {
				Simulator.stats.prio_noneZeroDistance.Add();
		
				if (f1_dist < f2_dist)
					Simulator.stats.prio_f2GreaterDistance.Add();
				else if (f1_dist > f2_dist)
					Simulator.stats.prio_f1GreaterDistance.Add();
				else
					Simulator.stats.prio_noneGreaterDistance.Add();
				
				if (f1_dist == f2_dist)
					return (1 == Simulator.rand.Next(2)) ? -1 : 1;
				else {
					if(f1_dist < f2_dist)
						return -1;
					else
						return  1;
				}
			}
		}

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_GP._rank(f1, f2);
        }
        
        /*  
         * Deflection infection follows these rules:
         * 1. A certain % of flits are injected already infected
         * 2. Every time a non-infected flit "contacts" an infected flit 
         *      during a comparison the non-infected flit has a probability 
         *      of becoming infected.
         * 3. Infected flits have a probability of being "cured" every 
         	    time they are compared
         * 4. Infected flits are prioritized over non-inflected flits.
         */
        //Infects and cures flits
        protected static void deflectInfect(Flit f1, Flit f2)
        {
        	bool c1 = false;
        	bool c2 = false;
        	bool i1 = false;
        	bool i2 = false;
        	
        	if(f1 == null && f2 == null)
        		return;
        	else if(f1 != null)
        		c1 = cure(f1);
        	else if(f2 != null)
        		c2 = cure(f2);
        	else 
        	{
	        	if(f1.infected && f2.infected)
	        	{
	        		c1 = cure(f1);
	        		c2 = cure(f2);
	        	}
	        	else if (f1.infected)
	        	{
	        		i2 = infect(f2);
	        		c1 = cure(f1);
	        	}
	        	else if (f2.infected)
	        	{
	        		i1 = infect(f1);
	        		c2 = cure(f2);
	        	}
	    	}
	    	statsDeflectionInfection(f1, f2, c1, c2, i1, i2);
        }
        
        protected static void statsDeflectionInfection(Flit f1, Flit f2, bool cure1, bool cure2, bool infect1, bool infect2)
        {
        	if (f1 != null)
        	{
        		if (cure1)
        			Simulator.stats.cureCount.Add();	
        		if (infect1)
        			Simulator.stats.infectCount.Add();
        	}
        	if (f2 != null)
        	{
        		if (cure2)
        			Simulator.stats.cureCount.Add();
        		if (infect2)
        			Simulator.stats.infectCount.Add();
        		
        	}
        } 
        
        /* Infects flits */
        protected static bool infect(Flit f)
        {
        	if (f != null && !f.infected)
        	{
        		if (Simulator.rand.Next(0,100) < Config.infectionRate) {
					f.infected = true;
					return true;
				}
        	}
        	return false;
        }
        
        /* Cures flits */
        protected static bool cure(Flit f)
        {
        	if (f != null && f.infected)
        	{
        		if(Simulator.rand.Next(0,100) < Config.cureRate) {
        			f.infected = false;
        			return true;
        		}
        	}
        	return false;
        }
    }

    public class Router_Flit_Random : Router_Flit
    {
        public Router_Flit_Random(Coord myCoord)
            : base(myCoord)
        {
        }
        
		public static int _rank(Flit f1, Flit f2)
		{
			return (1 == Simulator.rand.Next(2)) ? -1 : 1;//Simulator.rand.Next(3) - 1; // one of {-1,0,1}
		}
		
        public override int rank(Flit f1, Flit f2)
        {
            return _rank(f1, f2);
        }
    }

    public class Router_Flit_Exhaustive : Router_Flit
    {
        const int LOCAL = 4;
        public Router_Flit_Exhaustive(Coord myCoord)
            : base(myCoord)
        {
        }

        protected override void _doStep()
        {
            int index;
            int bestPermutationProgress = -1;
            IEnumerable<int> bestPermutation = null;

            foreach (IEnumerable<int> thisPermutation in PermuteUtils.Permute<int>(new int[] { 0, 1, 2, 3, 4 }, 4))
            {
                index = 0;
                int thisPermutationProgress = 0;
                foreach (int direction in thisPermutation)
                {
                    Flit f = linkIn[index++].Out;
                    if (f == null)
                        continue;

                    if (direction == LOCAL)
                    {
                        if (f.dest.ID == this.ID)
                            thisPermutationProgress++;
                        else
                            goto PermutationDone; // don't allow ejection of non-arrived flits
                    }
                    else if (isDirectionProductive(f.packet.dest, direction))
                        thisPermutationProgress++;
                    //Console.Write(" " + direction);
                }

                if (thisPermutationProgress > bestPermutationProgress)
                {
                    bestPermutation = thisPermutation;
                    bestPermutationProgress = thisPermutationProgress;
                }
            PermutationDone:
                continue;
            }

            index = 0;
            foreach (int direction in bestPermutation)
            {
                Flit f = linkIn[index++].Out;
                //Console.Write(" {1}->{0}", direction, (f == null ? "null" : f.dest.ID.ToString()));
                if (direction == LOCAL)
                    this.acceptFlit(f);
                else
                {
                    if (f != null && !isDirectionProductive(f.packet.dest, direction))
                        f.Deflected = true;
                    linkOut[direction].In = f;
                }
            }
            //Console.WriteLine();
            //throw new Exception("Done!");
        }
    }

    public class Router_Flit_Ctlr : Router_Flit
    {
        public Router_Flit_Ctlr(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Simulator.controller.rankFlits(f1, f2);
        }
    }
    
	public class Router_Flit_ClosestFirst : Router_Flit
    {
        public Router_Flit_ClosestFirst(Coord myCoord)
            : base(myCoord)
        {
        }

        protected override bool needFlush(Flit f)
        {
            return Config.cheap_of_cap != -1 && distance(f) > (int)Config.cheap_of_cap;
        }

 		public static int distance(Flit f)
 		{
 			return Math.Abs(f.currentX - f.packet.dest.x) + Math.Abs(f.currentY - f.packet.dest.y); 
        }

        public static int _rank(Flit f1, Flit f2)
        {
            if (f1 == null && f2 == null) return  0;
            if (f1 == null) 			  return  1;
            if (f2 == null) 			  return -1;


            bool f1_resc  = (f1.state == Flit.State.Rescuer) || 
            				(f1.state == Flit.State.Carrier);
            bool f2_resc  = (f2.state == Flit.State.Rescuer) || 
            				(f2.state == Flit.State.Carrier);
            bool f1_place = (f1.state == Flit.State.Placeholder);
            bool f2_place = (f2.state == Flit.State.Placeholder);

            int c0 = 0;
			
            if      (f1_resc  &&  f2_resc) c0 =  0;
            else if              (f1_resc) c0 = -1;
            else if              (f2_resc) c0 =  1;
            else if (f1_place && f2_place) c0 =  0;
            else if             (f1_place) c0 =  1;
            else if             (f2_place) c0 = -1;

            int c1 = 0, c2 = 0;
            if (f1.packet != null && f2.packet != null)
            {
                c1 = -distance(f1).CompareTo(distance(f2));
                c2 = f1.packet.ID.CompareTo(f2.packet.ID);
            }

            int c3 = f1.flitNr.CompareTo(f2.flitNr);

            int zerosSeen = 0;
            foreach (int i in new int[] { c0, c1, c2, c3 })
            {
                if (i == 0)
                    zerosSeen++;
                else
                    break;
            }
            Simulator.stats.net_decisionLevel.Add(zerosSeen);

            return
				(c0 != 0) ? c0 : 
				//(cr != 0) ? cr :
                (c1 != 0) ? c1 :
                (c2 != 0) ? c2 :
                 c3;
        }

        public override int rank(Flit f1, Flit f2)
        {
            return _rank(f1, f2);
        }

        public override void visitFlits(Flit.Visitor fv)
        {
            if (m_injectSlot != null)
                fv(m_injectSlot);
            if (m_injectSlot2 != null)
                fv(m_injectSlot2);
        }
    }
}
