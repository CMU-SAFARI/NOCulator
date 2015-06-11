using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class SortNode
    {
        public delegate int Steer(Flit f);
        public delegate int Rank(Flit f1, Flit f2);

        Steer m_s;
        Rank m_r;

        public Flit in_0, in_1, out_0, out_1;

        public SortNode(Steer s, Rank r)
        {
            m_s = s;
            m_r = r;
        }

        public void doStep()
        {
            Flit winner, loser;

            if (m_r(in_0, in_1) < 0)
            {
                winner = in_0;
                loser = in_1;
            }
            else
            {
                loser = in_0;
                winner = in_1;
            }

            if (winner != null) winner.sortnet_winner = true;
            if (loser != null) loser.sortnet_winner = false;

            int dir = m_s(winner);

            if (dir == 0)
            {
                out_0 = winner;
                out_1 = loser;
            }
            else
            {
                out_0 = loser;
                out_1 = winner;
            }
        }
    }

    public abstract class SortNet
    {
        public abstract void route(Flit[] input, out bool injected);
    }

    public class SortNet_CALF : SortNet
    {
        SortNode[] nodes;

        public SortNet_CALF(SortNode.Rank r)
        {
            nodes = new SortNode[4];

            SortNode.Steer stage1_steer = delegate(Flit f)
            {
                if (f == null)
                    return 0;

                return (f.prefDir == Simulator.DIR_UP || f.prefDir == Simulator.DIR_DOWN) ?
                    0 : // NS switch
                    1;  // EW switch
            };
           
            // node 0: {N,E} -> {NS, EW}
            nodes[0] = new SortNode(stage1_steer, r);
            // node 1: {S,W} -> {NS, EW}
            nodes[1] = new SortNode(stage1_steer, r);

            // node 2: {in_top,in_bottom} -> {N,S}
            nodes[2] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_UP) ? 0 : 1;
                    }, r);
            // node 3: {in_top,in_bottm} -> {E,W}
            nodes[3] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_RIGHT) ? 0 : 1;
                    }, r);
        }

        // takes Flit[5] as input; indices DIR_{UP,DOWN,LEFT,RIGHT} and 4 for local.
        // permutes in-place. input[4] is left null; if flit was injected, 'injected' is set to true.
        public override void route(Flit[] input, out bool injected)
        {
            injected = false;

            if (!Config.calf_new_inj_ej)
            {
                // injection: if free slot, insert flit
                if (input[4] != null)
                {
                    for (int i = 0; i < 4; i++)
                        if (input[i] == null)
                        {
                            input[i] = input[4];
                            injected = true;
                            break;
                        }

                    input[4] = null;
                }
            }

            if (Config.sortnet_twist)
            {
                nodes[0].in_0 = input[Simulator.DIR_UP];
                nodes[0].in_1 = input[Simulator.DIR_RIGHT];
                nodes[1].in_0 = input[Simulator.DIR_DOWN];
                nodes[1].in_1 = input[Simulator.DIR_LEFT];
            }
            else
            {
                nodes[0].in_0 = input[Simulator.DIR_UP];
                nodes[0].in_1 = input[Simulator.DIR_DOWN];
                nodes[1].in_0 = input[Simulator.DIR_LEFT];
                nodes[1].in_1 = input[Simulator.DIR_RIGHT];
            }
            nodes[0].doStep();
            nodes[1].doStep();
            nodes[2].in_0 = nodes[0].out_0;
            nodes[2].in_1 = nodes[1].out_0;
            nodes[3].in_0 = nodes[0].out_1;
            nodes[3].in_1 = nodes[1].out_1;
            nodes[2].doStep();
            nodes[3].doStep();
            input[Simulator.DIR_UP] = nodes[2].out_0;
            input[Simulator.DIR_DOWN] = nodes[2].out_1;
            input[Simulator.DIR_RIGHT] = nodes[3].out_0;
            input[Simulator.DIR_LEFT] = nodes[3].out_1;
        }
    }

    public class SortNet_COW : SortNet // Cheap Ordered Wiring?
    {
        SortNode[] nodes;

        public SortNet_COW(SortNode.Rank r)
        {
            nodes = new SortNode[6];

            SortNode.Steer stage1_steer = delegate(Flit f)
            {
                if (f == null) return 0;
                return (f.sortnet_winner) ? 0 : 1;
            };

            SortNode.Steer stage2_steer = delegate(Flit f)
            {
                if (f == null) return 0;
                return (f.prefDir == Simulator.DIR_UP || f.prefDir == Simulator.DIR_RIGHT) ?
                    0 : 1;
            };
           
            nodes[0] = new SortNode(stage1_steer, r);
            nodes[1] = new SortNode(stage1_steer, r);

            nodes[2] = new SortNode(stage2_steer, r);
            nodes[3] = new SortNode(stage2_steer, r);

            nodes[4] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_UP) ? 0 : 1;
                    }, r);
            nodes[5] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_DOWN) ? 0 : 1;
                    }, r);
        }

        // takes Flit[5] as input; indices DIR_{UP,DOWN,LEFT,RIGHT} and 4 for local.
        // permutes in-place. input[4] is left null; if flit was injected, 'injected' is set to true.
        public override void route(Flit[] input, out bool injected)
        {
            injected = false;

            if (!Config.calf_new_inj_ej)
            {
                // injection: if free slot, insert flit
                if (input[4] != null)
                {
                    for (int i = 0; i < 4; i++)
                        if (input[i] == null)
                        {
                            input[i] = input[4];
                            injected = true;
                            break;
                        }

                    input[4] = null;
                }
            }

            // NS, EW -> NS, EW
            if (!Config.sortnet_twist)
            {
                nodes[0].in_0 = input[Simulator.DIR_UP];
                nodes[0].in_1 = input[Simulator.DIR_RIGHT];
                nodes[1].in_0 = input[Simulator.DIR_DOWN];
                nodes[1].in_1 = input[Simulator.DIR_LEFT];
            }
            else
            {
                nodes[0].in_0 = input[Simulator.DIR_UP];
                nodes[0].in_1 = input[Simulator.DIR_DOWN];
                nodes[1].in_0 = input[Simulator.DIR_LEFT];
                nodes[1].in_1 = input[Simulator.DIR_RIGHT];
            }
            nodes[0].doStep();
            nodes[1].doStep();
            nodes[2].in_0 = nodes[0].out_0;
            nodes[3].in_0 = nodes[1].out_0;
            nodes[3].in_1 = nodes[0].out_1;
            nodes[2].in_1 = nodes[1].out_1;
            nodes[2].doStep();
            nodes[3].doStep();
            nodes[4].in_0 = nodes[2].out_0;
            nodes[4].in_1 = nodes[3].out_0;
            nodes[5].in_0 = nodes[2].out_1;
            nodes[5].in_1 = nodes[3].out_1;
            nodes[4].doStep();
            nodes[5].doStep();
            input[Simulator.DIR_UP] = nodes[4].out_0;
            input[Simulator.DIR_RIGHT] = nodes[4].out_1;
            input[Simulator.DIR_DOWN] = nodes[5].out_0;
            input[Simulator.DIR_LEFT] = nodes[5].out_1;
        }
    }

    public abstract class Router_SortNet : Router
    {
        // injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;
		Queue<Flit>[] ejectBuffer;
        SortNet m_sort;

        public Router_SortNet(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot = null;
            m_injectSlot2 = null;
			ejectBuffer = new Queue<Flit>[4];
			for (int n =0 ;n < 4; n++)
				ejectBuffer[n] = new Queue<Flit>();
            
			if (Config.sortnet_full)
                m_sort = new SortNet_COW(new SortNode.Rank(rank));
            else
                m_sort = new SortNet_CALF(new SortNode.Rank(rank));

            if (!Config.edge_loop)
                throw new Exception("SortNet (CALF) router does not support mesh without edge loop. Use -edge_loop option.");
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
        void acceptFlit(Flit f)
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

        Flit[] m_ej = new Flit[4] { null, null, null, null };
        int m_ej_rr = 0;

        Flit ejectLocalNew()
        {
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                        linkIn[dir].Out.dest.ID == ID &&
                        m_ej[dir] == null)
                {
                    m_ej[dir] = linkIn[dir].Out;
                    linkIn[dir].Out = null;
                }

            m_ej_rr++; m_ej_rr %= 4;

            Flit ret = null;
            if (m_ej[m_ej_rr] != null)
            {
                ret = m_ej[m_ej_rr];
                m_ej[m_ej_rr] = null;
            }

            return ret;
        }

        Flit[] input = new Flit[5]; // keep this as a member var so we don't
                                    // have to allocate on every step (why can't
                                     // we have arrays on the stack like in C?)

        protected override void _doStep()
        {
			if (Config.InfEjectBuffer)
			{
				for (int dir = 0; dir < 4; dir ++)
					if (linkIn[dir] != null && linkIn[dir].Out != null && linkIn[dir].Out.packet.dest.ID == ID) 
					{
						ejectBuffer[dir].Enqueue(linkIn[dir].Out);
						linkIn[dir].Out = null;
					}
				int bestdir = -1;
				for (int dir = 0; dir < 4; dir ++)
					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || ejectBuffer[dir].Peek().injectionTime < ejectBuffer[bestdir].Peek().injectionTime))
						bestdir = dir;
				if (bestdir != -1)
					acceptFlit(ejectBuffer[bestdir].Dequeue());
				
			}
			else if (Config.EjectBufferSize != -1)
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
					bestdir = dir;
				if (bestdir != -1)
					acceptFlit(ejectBuffer[bestdir].Dequeue());
			}
																																																																							
			else
			{
     	       	Flit eject = null;
        	    if (Config.calf_new_inj_ej)
            	    eject = ejectLocalNew();
  	          	else
				{
					Flit f1 = null,f2 = null;
					int flitsTryToEject = 0;
					for (int dir = 0; dir < 4; dir ++)
						if (linkIn[dir] != null && linkIn[dir].Out != null && linkIn[dir].Out.dest.ID == ID)
							flitsTryToEject ++;
					Simulator.stats.flitsTryToEject[flitsTryToEject].Add();

	        	    for (int i = 0; i < Config.meshEjectTrial; i++)
	            	{
		                eject = ejectLocal();
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
			}
            for (int dir = 0; dir < 4; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                    input[dir] = linkIn[dir].Out;
                    input[dir].inDir = dir;
                }
                else
                    input[dir] = null;
            }

            Flit inj = null;
            bool injected = false;

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

            input[4] = inj;
            if (inj != null)
                inj.inDir = -1;

            for (int i = 0; i < 5; i++)
                if (input[i] != null)
                {
                    PreferredDirection pd = determineDirection(input[i]);
                    if (pd.xDir != Simulator.DIR_NONE)
                        input[i].prefDir = pd.xDir;
                    else
                        input[i].prefDir = pd.yDir;
                }

            m_sort.route(input, out injected);

            //Console.WriteLine("---");
            for (int i = 0; i < 4; i++)
            {
                if (input[i] != null)
                {
                    //Console.WriteLine("input dir {0} pref dir {1} output dir {2} age {3}",
                    //        input[i].inDir, input[i].prefDir, i, Router_Flit_OldestFirst.age(input[i]));
                    input[i].Deflected = input[i].prefDir != i;
                }
            }

            if (Config.calf_new_inj_ej)
            {
                if (inj != null && input[inj.prefDir] == null)
                {
                    input[inj.prefDir] = inj;
                    injected = true;
                }
            }

            if (!injected)
            {
                if (m_injectSlot == null)
                    m_injectSlot = inj;
                else
                    m_injectSlot2 = inj;
            }
            else
                statsInjectFlit(inj);

          
            for (int dir = 0; dir < 4; dir++)
                if (input[dir] != null)
                {
                    if (linkOut[dir] == null)
                        throw new Exception(String.Format("router {0} does not have link in dir {1}",
                                    coord, dir));
                    linkOut[dir].In = input[dir];
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

        public override void visitFlits(Flit.Visitor fv)
        {
            if (m_injectSlot != null)
                fv(m_injectSlot);
            if (m_injectSlot2 != null)
                fv(m_injectSlot2);
        }

        //protected abstract int rank(Flit f1, Flit f2);
    }

    public class Router_SortNet_GP : Router_SortNet
    {
        public Router_SortNet_GP(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_GP._rank(f1, f2);
        }
    }

    public class Router_SortNet_OldestFirst : Router_SortNet
    {
        public Router_SortNet_OldestFirst(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_OldestFirst._rank(f1, f2);
        }
    }
}
