//#define DEBUG
//#define RETX_DEBUG
//#define memD

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public abstract class Router_New : Router
    {
		// injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;
        
        protected ResubBuffer rBuf;

        public Router_New(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot  = null;
            m_injectSlot2 = null;
            rBuf = new ResubBuffer();
        }

        public override bool hasLiveLock()
        {
            if (m_n.healthy == false)
                return false;
            for (int i = 0; i < 4; i++)
                if (linkIn[i] != null && linkIn[i].Out != null)
                    if (Simulator.CurrentRound - linkIn[i].Out.injectionTime > Config.livelock_thresh)
                        return true;

            for (int i = 0; i < rBuf.count(); i++)
                if (Simulator.CurrentRound - rBuf.getFlit(i).injectionTime > Config.livelock_thresh)
                    return true;
            return false;
        }

        protected Flit handleGolden(Flit f)
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
        protected virtual void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }

        protected virtual Flit ejectLocal()
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
#if DEBUG_
            if (ret != null)
                Console.WriteLine("ejecting flit {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            ret = handleGolden(ret);

            return ret;
        }

        protected Flit[] input = new Flit[4]; // keep this as a member var so we don't
        // have to allocate on every step (why can't
        // we have arrays on the stack like in C?)

        protected override void _doStep()
        {
            Flit[] eject = new Flit[4];
            eject[0] = eject[1] = eject[2] = eject[3] = null;

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
#if DEBUG_
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
            int numDeflected = 0;
            int[] deflected = new int[4];
            deflected[0] = deflected[1] = deflected[2] = deflected[3] = -1;
                
            // assign outputs
            for (int i = 0; i < 4 && input[i] != null; i++)
            {
                PreferredDirection pd = determineDirection(input[i], coord);
                //int outDir = -1;
                Flit tempFlit = input[i];
				

		        	
		        if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                {
                    linkOut[pd.xDir].In = input[i];
                //    outDir = pd.xDir;
                    
                    /* Refresh wasInBuf */
               	 	if (Config.wasInRebufCycleRefresh)
		        		tempFlit.wasInRebuf = false;
                }

                else if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                {
                    linkOut[pd.yDir].In = input[i];
                //    outDir = pd.yDir;
                    
                    /* Refresh wasInBuf */
                	if (Config.wasInRebufCycleRefresh)
		        		tempFlit.wasInRebuf = false;
                }

                // deflect!
                else
                {
                    input[i].Deflected = true;
                    //int dir = 0;
					deflected[numDeflected] = i;
                    numDeflected++;
                    
                    
                    /*
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
                    */
                }
            }
            
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

    public class Router_New_OldestFirst : Router_New
    {
        public Router_New_OldestFirst(Coord myCoord)
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

    public class Router_New_Prio : Router_New
    {
        public Router_New_Prio(Coord myCoord)
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

    public class Router_New_GP : Router_New
    {    
        public Router_New_GP(Coord myCoord)
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
                    case "face":
                        silver1 = f1.mazeIn.mode != MazeFlitsMode.normal;
                        silver2 = f2.mazeIn.mode != MazeFlitsMode.normal;
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
            return Router_New_GP._rank(f1, f2);
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

    public class Router_New_Random : Router_New
    {
        public Router_New_Random(Coord myCoord)
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

    public class Router_New_Exhaustive : Router_New
    {
        const int LOCAL = 4;
        public Router_New_Exhaustive(Coord myCoord)
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

    public class Router_New_Ctlr : Router_New
    {
        public Router_New_Ctlr(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Simulator.controller.rankFlits(f1, f2);
        }
    }
    
	public class Router_New_ClosestFirst : Router_New
    {
        public Router_New_ClosestFirst(Coord myCoord)
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


    public class Router_Maze : Router_New_GP
    {
        
        public enum SelectionAlgorithm
        {
            always0, always1, random, intuitive
        }

        public class MazeConfig : ConfigGroup
        {
            public SelectionAlgorithm selection = SelectionAlgorithm.random;
            public int feedbackQsize = 10;
            public override void finalize() { }
            protected override bool setSpecialParameter(string param, string val) { return false; }
        }

       

        MazeConfig configs = Config.maze;

        public Router_Maze(Coord myCoord)
            : base(myCoord)
        {
        }

        static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        static void BubbleSwap<T>(ref T[] lhs, int ind)
        {
            for (; ind < lhs.Length - 1; ind++)
                Swap(ref lhs[ind], ref lhs[ind + 1]);
        }

        
        protected override Flit ejectLocal()
        {
            // eject locally-destined flit (highest-ranked, if multiple)
            Flit ret = null;
            int bestDir = -1;
            for (int dir = 0; dir < 4; dir++)
                if (input[dir] != null &&
                    input[dir].state != Flit.State.Placeholder &&
                    (input[dir].dest.ID == ID || input[dir].mazeIn.mode == MazeFlitsMode.unreachable) &&
                    (ret == null || (ret.mazeIn.mode != MazeFlitsMode.unreachable && rank(input[dir], ret) < 0)))
                {
                    ret = input[dir];
                    bestDir = dir;
                }

            if (bestDir != -1)
            {
                input[bestDir] = null;
                //BubbleSwap(ref input, bestDir);
            }

#if DEBUG_
            if (ret != null)
                Console.WriteLine("ejecting flit {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            ret = handleGolden(ret);

            return ret;
        }

        Flit creatNewFlit(Coord src, Coord dst)
        {

            SynthPacket p = new SynthPacket(src, dst);
            return p.flits[0];
        }

        
        struct src_dst
        {
            public int srcID, dstID;
            public src_dst(int src_id, int dst_id)
            {
                srcID = src_id;
                dstID = dst_id;
            }
        };
        List<src_dst> partitioningInfo;
        // accept one ejected flit into rxbuf
        protected override void acceptFlit(Flit f)
        {

            if (f.destIsDisconnected)
                canReach[f.discDestCoord.ID] = false;

            if (f.mazeIn.mode == MazeFlitsMode.unreachable)
            {
                int srcID = f.src.ID, dstID = f.dest.ID;
                canReach[dstID] = false;
                if (partitioningInfo.Contains(new src_dst(srcID, dstID)) == false &&
                    feedBackQ.Count < configs.feedbackQsize)
                {
                    Flit nF = creatNewFlit(coord, f.src);

                    nF.destIsDisconnected = true;
                    nF.discDestCoord = f.dest;
                    feedBackQ.Enqueue(nF);
                    partitioningInfo.Add(new src_dst(srcID, dstID));
                }
            }
            

            if (f.injectedFromCTRL == false)
                base.acceptFlit(f);
        }

        
        Queue<Flit> feedBackQ;
        protected void learningController()
        {
            if (m_injectSlot == null && feedBackQ.Count != 0)//in case a dest in unreachable
            {
                Flit f = feedBackQ.Dequeue();
                f.injectedFromCTRL = true;
                InjectFlit(f);
            }
            return;


        }

        int calcRegion(Coord src, Coord dest)
        {
            return (dest.x > src.x && dest.y >= src.y) ? 0 :
                         (dest.x <= src.x && dest.y > src.y) ? 1 :
                         (dest.x < src.x && dest.y <= src.y) ? 2 : 3;
        }

        Flit[] permut = new Flit[4];

        protected override void _doStep()
        {
            if (m_n.healthy == false)
                return;

            //calls the learning controller
            learningController();

            // grab inputs into a local array so we can sort
            for (int i = 0; i < 4; i++) input[i] = null;
            int c = 0, before = 0;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                    input[dir] = linkIn[dir].Out;
                    input[dir].inDir = dir;
                    linkIn[dir].Out = null;
                    c++;
                }
            before = c;

            //eject-1 stages
            Flit[] eject = new Flit[4];
            eject[0] = eject[1] = eject[2] = eject[3] = null;

            int ejctCnt = 0;
            for (; ejctCnt < Config.ejectCount - 1; ejctCnt++)
            {
                eject[ejctCnt] = ejectLocal();
                if (eject[ejctCnt] == null)
                    break;
            }

            //buffer inject stage
            int reinjectedCount = 0;
            if (Config.resubmitBuffer)
            {
                c = before - ejctCnt;
                for (int dir = 0; !rBuf.isEmpty() && dir < 4 && c < healthyNeighbors && reinjectedCount < Config.rebufInjectCount; dir++)
                    if (input[dir] == null)
                    {
                        input[dir] = rBuf.removeFlit();
                        input[dir].nrInRebuf++;
                        c++;
                        reinjectedCount++;
                    }
            }
            

            //last eject stage (As there might be a local packet re-injected)
            eject[ejctCnt] = ejectLocal();
            if (eject[ejctCnt] != null)
                ejctCnt++;

            #region general checkups
            //Some general checkups
            int after = 0;
            for (int i = 0; i < 4; i++)
                if (input[i] != null) after++;
            if (after + ejctCnt != before + reinjectedCount)
                throw new Exception("Something weird happened on resubmit buffer");

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    if (input[i] == null || input[j] == null)
                        continue;
                    if (i != j && input[i].Equals(input[j]))
                        throw new Exception("DUPLICATING");
                }

            // sometimes network-meddling such as flit-injection can put unexpected
            // things in outlinks...
            int outCount = 0;
            for (int dir = 0; dir < 4; dir++)
                if (linkOut[dir] != null && linkOut[dir].In != null)// || !linkOut[dir].healthy))
                    throw new Exception("Some previous flits are not yet forwarded!!");
            //outCount++;
            #endregion

            //inject stage
            bool wantToInject = m_injectSlot2 != null || m_injectSlot != null;
            bool canInject = (c + outCount) < healthyNeighbors;
            bool starved = wantToInject && !canInject;

            if (starved)
            {
                Flit starvedFlit = null;
                starvedFlit = m_injectSlot2;
                if (starvedFlit == null) starvedFlit = m_injectSlot;

                Simulator.controller.reportStarve(coord.ID);
                statsStarve(starvedFlit);
            }
            if (canInject && wantToInject)
            {
                //Flit inj_peek = null;
                //if (m_injectSlot2 != null)
                //    inj_peek = m_injectSlot2;
                //else if (m_injectSlot != null)
                //    inj_peek = m_injectSlot;
                if (m_injectSlot2 == null && m_injectSlot == null)
                    throw new Exception("Inj flit peek is null!!");

                if (!Simulator.controller.ThrottleAtRouter || Simulator.controller.tryInject(coord.ID))
                {
                    Flit inj = null;
                    if (m_injectSlot2 != null)
                    {
                        inj = m_injectSlot2;
                        m_injectSlot2 = null;
                    }
                    else
                    {
                        inj = m_injectSlot;
                        m_injectSlot = null;
                    }

                    if (c >= healthyNeighbors)
                        throw new Exception("Injecting flits without having ports");

                    for (int dir = 0; dir < 4; dir++ )
                        if (input[dir] == null)
                        {
                            input[dir] = inj;
                            inj.inDir = Simulator.DIR_LOCAL;
                            c++;
                            break;
                        }

#if DEBUG_
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
                    if (inj.injectedFromCTRL)
                        inj.injectionTime = Simulator.CurrentRound;
                    else
                        statsInjectFlit(inj);
                }
            }

            //finalizing eject stage
            for (int i = 0; i < ejctCnt; i++)
                //if (eject[i] != null)
                {
                    acceptFlit(eject[i]);
                    eject[i] = null;
                }
            if (c == 2 && ((input[0] == null) == (input[1] == null)))
                Swap(ref input[1], ref input[2]);


            /********************************************************************************/
            ////////////////////////////// SECOND PIPLELINE STAGE ////////////////////////////
            /********************************************************************************/

            //Maze-routing Stage
            for (int i = 0; i < 4; i++)
                if (input[i] != null)
                    determineDirection(input[i], coord);


            //Selection Stage
            for (int i = 0; i < 4; i++)
                if (input[i] != null)
                    selectionFunction(ref input[i]);


            //Permutation Network
            int numDeflected = permutation_network();

            
            //Buffer Eject
            int bufEjctCnt = 0;
            if (Config.resubmitBuffer && numDeflected > 0)
            {
                for (int i = 0; i < Config.rebufRemovalCount && i < numDeflected && !rBuf.isFull(); i++)
                {
                    int ind = -1;
                    for (int dir = 0; dir < 4; dir++)
                        if (input[dir] != null && input[dir].Deflected)
                        {
                            if (ind == -1 || linkOut[dir] == null || linkOut[dir].healthy == false ||
                                (linkOut[ind] != null && linkOut[ind].healthy && input[ind].mazeIn.MDbest != 0 &&
                                (input[dir].mazeIn.MDbest == 0 || deflPriority(input[dir], input[ind]) < 0)))
                                ind = dir;
                        }
                    if (ind == -1 || input[ind] == null)
                        throw new Exception("Null flit to inject to side buffer!!!");

                    rBuf.addFlit(input[ind]);
                    input[ind].hopCnt++;
                    input[ind] = null;
                    bufEjctCnt++;
                }
            }


            //Deflection Stage
            for (int dir = 0; dir < 4; dir++ )
                if (input[dir] != null)
                {
                    if (linkOut[dir].healthy == false)
                        throw new Exception("Forwarding a flit to a failed link!!!");
                    else if(linkOut[dir].In != null)
                        throw new Exception("Previous flit is not consumed yet!!!");

                    if (input[dir].Deflected)
                    {
                        resetMazeHeads(ref input[dir], dir);
                        if (input[dir] == null)
                            continue;
                    }
                    else
                    {
                        int sel = (input[dir].dirOut[0] == dir) ? 0 : 1;

                        if (input[dir].enteredTraversalMode)
                        {
                            input[dir].mazeOut.mode = (sel == 0) ?
                                MazeFlitsMode.rightHand : MazeFlitsMode.leftHand;
                            input[dir].mazeOut.dirTrav = dir;
                        }
                    }
                    
                    input[dir].mazeIn = input[dir].mazeOut;
                    linkOut[dir].In = input[dir];
                    input[dir] = null;
                }
        }

        public int permutation_network()
        {
            //level 1:
            //N & E inputs
            if ((input[0] == null && input[1] == null) ||
                (input[1] == null && input[0].dirOut[0] == Simulator.DIR_NONE) ||
                (input[0] == null && input[1].dirOut[0] == Simulator.DIR_NONE) ||
                (input[0] != null && input[1] != null &&
                 input[0].dirOut[0] == Simulator.DIR_NONE && input[1].dirOut[0] == Simulator.DIR_NONE))
            {
                permut[0] = input[0];
                permut[2] = input[1];
            }
            else
            {
                int winner = (input[1] == null) ? 0 :
                             (input[0] == null) ? 1 :
                             (input[1].dirOut[0] == Simulator.DIR_NONE) ? 0 :
                             (input[0].dirOut[0] == Simulator.DIR_NONE) ? 1 :
                             (rank(input[0], input[1]) < 0) ? 0 : 1;

                int ind = (input[winner].dirOut[input[winner].sel] < 2) ? 0 : 2;
                permut[ind] = input[winner];
                permut[2 - ind] = input[1 - winner];
            }
            //S & W inputs
            if ((input[2] == null && input[3] == null) ||
                (input[3] == null && input[2].dirOut[0] == Simulator.DIR_NONE) ||
                (input[2] == null && input[3].dirOut[0] == Simulator.DIR_NONE) ||
                (input[2] != null && input[3] != null &&
                 input[2].dirOut[0] == Simulator.DIR_NONE && input[3].dirOut[0] == Simulator.DIR_NONE))
            {
                permut[1] = input[2];
                permut[3] = input[3];
            }
            else
            {
                int winner = (input[3] == null) ? 2 :
                             (input[2] == null) ? 3 :
                             (input[3].dirOut[0] == Simulator.DIR_NONE) ? 2 :
                             (input[2].dirOut[0] == Simulator.DIR_NONE) ? 3 :
                             (rank(input[2], input[3]) < 0) ? 2 : 3;

                int ind = (input[winner].dirOut[input[winner].sel] < 2) ? 1 : 3;
                permut[ind] = input[winner];
                permut[4 - ind] = input[5 - winner];
            }


            //level 2:
            //N & E permutes
            if ((permut[0] == null && permut[1] == null) ||
                (permut[1] == null && permut[0].dirOut[0] == Simulator.DIR_NONE) ||
                (permut[0] == null && permut[1].dirOut[0] == Simulator.DIR_NONE) ||
                (permut[0] != null && permut[1] != null &&
                 permut[0].dirOut[0] == Simulator.DIR_NONE && permut[1].dirOut[0] == Simulator.DIR_NONE))
            {
                input[0] = permut[0];
                input[1] = permut[1];
            }
            else
            {
                int winner = (permut[1] == null) ? 0 :
                             (permut[0] == null) ? 1 :
                             (permut[1].dirOut[0] == Simulator.DIR_NONE) ? 0 :
                             (permut[0].dirOut[0] == Simulator.DIR_NONE) ? 1 :
                             (rank(permut[0], permut[1]) < 0) ? 0 : 1;

                int ind = (permut[winner].dirOut[permut[winner].sel] == 0) ? 0 :
                          (permut[winner].dirOut[permut[winner].sel] == 1) ? 1 :
                          (permut[winner].dirOut[1 - permut[winner].sel] == 0) ? 0 :
                          (permut[winner].dirOut[1 - permut[winner].sel] == 1) ? 1 :
                          (permut[1-winner] != null && permut[1 - winner].dirOut[permut[1 - winner].sel] == 0) ? 1 :
                          (permut[1-winner] != null && permut[1 - winner].dirOut[permut[1 - winner].sel] == 1) ? 0 :
                          (permut[1-winner] != null && permut[1 - winner].dirOut[1 - permut[1 - winner].sel] == 0) ? 1 :
                          (permut[1-winner] != null && permut[1 - winner].dirOut[1 - permut[1 - winner].sel] == 1) ? 0 : winner;

                input[ind] = permut[winner];
                input[1 - ind] = permut[1 - winner];
            }
            //S & W permutes
            if ((permut[2] == null && permut[3] == null) ||
                (permut[3] == null && permut[2].dirOut[0] == Simulator.DIR_NONE) ||
                (permut[2] == null && permut[3].dirOut[0] == Simulator.DIR_NONE) ||
                (permut[2] != null && permut[3] != null &&
                 permut[2].dirOut[0] == Simulator.DIR_NONE && permut[3].dirOut[0] == Simulator.DIR_NONE))
            {
                input[2] = permut[2];
                input[3] = permut[3];
            }
            else
            {
                int winner = (permut[3] == null) ? 2 :
                             (permut[2] == null) ? 3 :
                             (permut[3].dirOut[0] == Simulator.DIR_NONE) ? 2 :
                             (permut[2].dirOut[0] == Simulator.DIR_NONE) ? 3 :
                             (rank(permut[2], permut[3]) < 0) ? 2 : 3;

                int ind = (permut[winner].dirOut[permut[winner].sel] == 2) ? 2 :
                          (permut[winner].dirOut[permut[winner].sel] == 3) ? 3 :
                          (permut[winner].dirOut[1 - permut[winner].sel] == 2) ? 2 :
                          (permut[winner].dirOut[1 - permut[winner].sel] == 3) ? 3 :
                          (permut[5-winner] != null && permut[5 - winner].dirOut[permut[5 - winner].sel] == 2) ? 3 :
                          (permut[5-winner] != null && permut[5 - winner].dirOut[permut[5 - winner].sel] == 3) ? 2 :
                          (permut[5-winner] != null && permut[5 - winner].dirOut[1 - permut[5 - winner].sel] == 2) ? 3 :
                          (permut[5-winner] != null && permut[5 - winner].dirOut[1 - permut[5 - winner].sel] == 3) ? 2 : winner;

                input[ind] = permut[winner];
                input[5 - ind] = permut[5 - winner];
            }

            //finalizing
            int numDeflected = 0;
            for (int dir = 0; dir < 4; dir++)
            {
                permut[dir] = null;
                if (input[dir] != null &&
                    input[dir].dirOut[0] != dir && input[dir].dirOut[1] != dir)
                {
                    numDeflected++;
                    input[dir].Deflected = true;
                    if (input[dir].mazeOut.mode == MazeFlitsMode.unreachable)
                        input[dir].mazeIn = input[dir].mazeOut;
                }
            }
            return numDeflected;
        }

        public override int rank(Flit f1, Flit f2)
        {
            return base.rank(f1, f2);
        }


        public void sortDeflected(ref int[] deflected)
        {

            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                {
                    int ii = deflected[i];
                    int jj = deflected[j];
                    if (jj != -1)
                        if (ii == -1 || (input[ii].mazeIn.MDbest != 0 &&
                            (input[jj].mazeIn.MDbest == 0 || deflPriority(input[jj], input[ii]) < 0)))
                            Swap(ref deflected[i], ref deflected[j]);
                }
        }

        public void resetMazeHeads(ref Flit f, int dir)
        {
            if (f.mazeOut.mode == MazeFlitsMode.unreachable)
                return;
            Coord nc = this.neighbor(dir).coord;
            f.mazeIn.MDbest = nc.MD(f.dest);
            f.mazeIn.mode = MazeFlitsMode.normal;
            f.mazeOut = f.mazeIn;
        }

        protected override PreferredDirection determineDirection(Flit f, Coord current)
        {
            PreferredDirection pd;
            pd.xDir = Simulator.DIR_NONE;
            pd.yDir = Simulator.DIR_NONE;
            pd.zDir = Simulator.DIR_NONE;
            f.dirOut[0] = f.dirOut[1] = Simulator.DIR_NONE;

            if (f.state == Flit.State.Placeholder) return pd;

            //if (f.packet.ID == 238)
            //    Console.WriteLine("packet 238 at ID ({0},{1}), wants ({2},{3})", current.x, current.y, f.packet.dest.x, f.packet.dest.y);

            return determineDirection(f);
        }

        protected override PreferredDirection determineDirection(Flit f)
        {
            PreferredDirection pd;
            pd.xDir = Simulator.DIR_NONE;
            pd.yDir = Simulator.DIR_NONE;
            pd.zDir = Simulator.DIR_NONE;
            f.enteredTraversalMode = false;

            //if (f.packet.ID == 5186926)
            //    f.outF.bestMD = f.inF.bestMD;

            if (coord.Equals(f.dest) || f.mazeIn.mode == MazeFlitsMode.unreachable)
            {
                f.mazeOut = f.mazeIn;
                return pd;
            }


            int[] odirs = f.dirOut;//new int[2] { Simulator.DIR_NONE, Simulator.DIR_NONE };

            int dX = Math.Abs(f.dest.x - coord.x);
            int dY = Math.Abs(f.dest.y - coord.y);

            bool[] isProductive = new bool[4] { (f.dest.y > this.coord.y) && linkOut[0].healthy,
                                                (f.dest.x > this.coord.x) && linkOut[1].healthy,
                                                (f.dest.y < this.coord.y) && linkOut[2].healthy,
                                                (f.dest.x < this.coord.x) && linkOut[3].healthy };

            bool hasProductive = isProductive[0] || isProductive[1] || isProductive[2] || isProductive[3];
            //bool[] vTurns = valid_turns(p.VC, entrancePoint);

            int region = calcRegion(coord, f.dest);

            f.mazeOut = f.mazeIn;
            if (f.mazeIn.MDbest == coord.MD(f.dest) && hasProductive)
            {

                int[] idxs = new int[] { 0, 3, 2, 1 };
                int i = idxs[region], idx = 0;
                if (isProductive[i]) odirs[idx++] = i;
                i = (i + 1) % 4;
                if (isProductive[i]) odirs[idx++] = i;

                f.mazeOut.MDbest = f.mazeIn.MDbest - 1;
                f.mazeOut.mode = MazeFlitsMode.normal;
            }
            else if (f.mazeIn.mode == MazeFlitsMode.rightHand ||
                     f.mazeIn.mode == MazeFlitsMode.leftHand)
            {

                int dir = ((f.mazeIn.mode == MazeFlitsMode.rightHand) ? f.inDir + 3 : f.inDir + 1) % 4;
                for (int i = 0; i < 4; i++)
                {
                    if (linkOut[dir] != null && linkOut[dir].healthy)
                    {
                        //If the path does not exist!
                        if (f.mazeIn.dirTrav == dir && f.mazeIn.nodeTrav.Equals(coord))
                            f.mazeOut.mode = MazeFlitsMode.unreachable;
                        else
                            odirs[0] = dir;
                        break;
                    }

                    if (f.mazeIn.mode == MazeFlitsMode.rightHand) dir = (dir + 3) % 4;
                    else dir = (dir + 1) % 4;
                }
            }
            else
            {
                int[] rightOrder = new int[] { 1 };
                int[] leftOrder = new int[] { 1 };
                switch (region)
                {
                    case 0:
                        rightOrder = new int[] { 0, 3, 2, 1 };
                        leftOrder = new int[] { 1, 2, 3, 0 };
                        break;
                    case 1:
                        rightOrder = new int[] { 3, 2, 1, 0 };
                        leftOrder = new int[] { 0, 1, 2, 3 };
                        break;
                    case 2:
                        rightOrder = new int[] { 2, 1, 0, 3 };
                        leftOrder = new int[] { 3, 0, 1, 2 };
                        break;
                    default:
                        rightOrder = new int[] { 1, 0, 3, 2 };
                        leftOrder = new int[] { 2, 3, 0, 1 };
                        break;
                }

                for (int i = 0; i < 4; i++)
                    if (this.linkOut[rightOrder[i]] != null && this.linkOut[rightOrder[i]].healthy)
                    {
                        odirs[0] = rightOrder[i];
                        break;
                    }

                for (int i = 0; i < 4; i++)
                    if (this.linkOut[leftOrder[i]] != null && this.linkOut[leftOrder[i]].healthy)
                    {
                        odirs[1] = leftOrder[i];
                        break;
                    }
                f.mazeOut.nodeTrav = coord;
                f.enteredTraversalMode = true;
            }

            pd.xDir = odirs[0];
            pd.yDir = odirs[1];

            return pd;
        }

        Rand selRand = new Rand(1024);

        protected void selectionFunction(ref Flit f)
        {
            int region = calcRegion(coord, f.dest);

            if (f.dirOut[0] == Simulator.DIR_NONE || f.dirOut[1] == Simulator.DIR_NONE)
            {
                f.sel = 0;
                return;
            }

            f.sel = selRand.Next(2);
            switch (configs.selection)
            {
                case SelectionAlgorithm.random:
                    break;
                case SelectionAlgorithm.always0:
                    f.sel = 0;
                    break;
                case SelectionAlgorithm.always1:
                    f.sel = 1;
                    break;
                case SelectionAlgorithm.intuitive:
                    if (((region == 0 || region == 3) && (coord.y > coord.x && coord.y + coord.x >= Config.network_nrX - 1)) ||
                    ((region == 3 || region == 2) && (coord.y <= coord.x && coord.y + coord.x > Config.network_nrX - 1)) ||
                    ((region == 2 || region == 1) && (coord.y < coord.x && coord.x + coord.y <= Config.network_nrX - 1)) ||
                    ((region == 1 || region == 0) && (coord.y >= coord.x && coord.x + coord.y < Config.network_nrX - 1)))
                    {
                        f.sel = 1;
                    }
                    else f.sel = 0;
                    break;
                default:
                    throw new Exception("Unknown selection function!");
            }
        }
    }

}
