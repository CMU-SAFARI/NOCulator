//#define DEBUG
//#define RETX_DEBUG
//#define memD

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

namespace ICSimulator
{
    public abstract class Router_Flit : Router
    {
        // injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;
		Queue<Flit>	[] ejectBuffer;

		public Router_Flit(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot = null;
            m_injectSlot2 = null;
			ejectBuffer = new Queue<Flit>[4];
			for (int n = 0; n < 4; n++)
				ejectBuffer[n] = new Queue<Flit>();
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
			if (Config.EjectBufferSize != -1)
			{								
				for (int dir =0; dir < 4; dir ++)
					if (linkIn[dir] != null && linkIn[dir].Out != null && linkIn[dir].Out.packet.dest.ID == ID && ejectBuffer[dir].Count < Config.EjectBufferSize)
					{
						ejectBuffer[dir].Enqueue(linkIn[dir].Out);
						linkIn[dir].Out = null;
					}
				int bestdir = -1;			
				for (int dir = 0; dir < 4; dir ++)
					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || ejectBuffer[dir].Peek().injectionTime < ejectBuffer[bestdir].Peek().injectionTime))
//					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || ejectBuffer[dir].Count > ejectBuffer[bestdir].Count))
//					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || Simulator.rand.Next(2) == 1))
						bestdir = dir;				
				if (bestdir != -1)
					acceptFlit(ejectBuffer[bestdir].Dequeue());
			}
			else 
			{
				int flitsTryToEject = 0;
            	for (int dir = 0; dir < 4; dir ++)
                	if (linkIn[dir] != null && linkIn[dir].Out != null && linkIn[dir].Out.dest.ID == ID)
               	 	{
						flitsTryToEject ++;
						if (linkIn[dir].Out.ejectTrial == 0)
							linkIn[dir].Out.firstEjectTrial = Simulator.CurrentRound;
						linkIn[dir].Out.ejectTrial ++;
					}
            	Simulator.stats.flitsTryToEject[flitsTryToEject].Add();            
				
				Flit f1 = null,f2 = null;
				for (int i = 0; i < Config.meshEjectTrial; i++)
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
                Flit inj_peek=null; 
                if(m_injectSlot2!=null)
                    inj_peek=m_injectSlot2;
                else if (m_injectSlot!=null)
                    inj_peek=m_injectSlot;
                if(inj_peek==null)
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

            // assign outputs
            for (int i = 0; i < 4 && input[i] != null; i++)
            {
                PreferredDirection pd = determineDirection(input[i], coord);
                int outDir = -1;
				bool deflect = false;
				if (Config.RandOrderRouting)
				{
				    int dimensionOrder = Simulator.rand.Next(2);  //0: x over y   1:y over x
				    if (dimensionOrder == 0)
					{
						if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                   		{
                        	linkOut[pd.xDir].In = input[i];
                        	outDir = pd.xDir;
                    	}
                    	else if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                    	{
                        	linkOut[pd.yDir].In = input[i];
                       		outDir = pd.yDir;
                    	}
                    	else 
                        	deflect = true;
					}
					else       //y over x
					{
						if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                    	{
                        	linkOut[pd.yDir].In = input[i];
                       		outDir = pd.yDir;
                    	}
                    	else if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                   		{
                        	linkOut[pd.xDir].In = input[i];
                        	outDir = pd.xDir;
                    	}
                    	else 
                        	deflect = true;
					}
				}
				else if (Config.DeflectOrderRouting)
				{
					if (input[i].routingOrder == false)
					{
						if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                   		{
                        	linkOut[pd.xDir].In = input[i];
                        	linkOut[pd.xDir].In.routingOrder = false;
                        	outDir = pd.xDir;
                    	}
                    	else if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                    	{
                        	linkOut[pd.yDir].In = input[i];
                        	linkOut[pd.yDir].In.routingOrder = true;
                       		outDir = pd.yDir;
                    	}
                    	else 
                        	deflect = true;
					}
					else       //y over x
					{
						if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                    	{
                        	linkOut[pd.yDir].In = input[i];
                        	linkOut[pd.yDir].In.routingOrder = true;
                       		outDir = pd.yDir;
                    	}
                    	else if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                   		{
                        	linkOut[pd.xDir].In = input[i];
                        	linkOut[pd.xDir].In.routingOrder = false;
                        	outDir = pd.xDir;
                    	}
                    	else 
                        	deflect = true;
					}
				}
				else //original Router
				{
                    if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                    {
                        linkOut[pd.xDir].In = input[i];
                        outDir = pd.xDir;
                    }
                    else if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                    {
                        linkOut[pd.yDir].In = input[i];
                        outDir = pd.yDir;
                    }
                    else 
                        deflect = true;
                }
                // deflect!
                if (deflect)
                {
                    input[i].Deflected = true;
                    int dir = 0;
                    if (Config.randomize_defl) dir = Simulator.rand.Next(4); // randomize deflection dir (so no bias)
                    for (int count = 0; count < 4; count++, dir = (dir + 1) % 4)
                        if (linkOut[dir] != null && linkOut[dir].In == null)
                        {
			                linkOut[dir].In = input[i];
                            outDir = dir;
                            if (dir == 0 || dir ==2)
                            	linkOut[dir].In.routingOrder = false;
                            else
                            	linkOut[dir].In.routingOrder = true;
                            break;
                        }
                    if (outDir == -1) throw new Exception(
                            String.Format("Ran out of outlinks in arbitration at node {0} on input {1} cycle {2} flit {3} c {4} neighbors {5} outcount {6}", coord, i, Simulator.CurrentRound, input[i], c, neighbors, outCount));
                }
            }
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
                return 0;
            if (f1 == null)
                return 1;
            if (f2 == null)
                return -1;

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

                if (golden1 && golden2)
                {
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
                    return 1;
                else
                    return (Simulator.rand.Next(2) == 1) ? 1 : -1;
            }
            else if (f1.state == Flit.State.Normal)
                return -1;
            else if (f2.state == Flit.State.Normal)
                return 1;
            else
                // both are placeholders
                return (Simulator.rand.Next(2) == 1) ? 1 : -1;
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_GP._rank(f1, f2);
        }
    }

    public class Router_Flit_Random : Router_Flit
    {
        public Router_Flit_Random(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Simulator.rand.Next(3) - 1; // one of {-1,0,1}
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
}
