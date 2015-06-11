//#define LOG

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public abstract class Router
    {
        public Coord coord;
        public int ID { get { return coord.ID; } }
		public bool enable;
        public Link[] linkOut = new Link[4];
        public Link[] linkIn = new Link[4];
		public Link[] LLinkIn;
		public Link[] LLinkOut;
		public Link[] GLinkIn;
		public Link[] GLinkOut;
        public Router[] neigh = new Router[4];
        public int neighbors;
		public int RouterType = -1;

        protected string routerName;
        protected Node m_n;

        // Keep track of the current router's average queue length over the
        // last INJ_RATE_WIN cycles
        public const int AVG_QLEN_WIN=1000;
        public float avg_qlen;
        public int[] qlen_win;
        public int qlen_ptr;
        public int qlen_count;
        public ulong m_lastInj;
        public ulong last_starve, starve_interval;

        public ulong m_inject = 0;
        public ulong Inject { get { return m_inject; } }

        // --------------------------------------------------------------------

        public struct PreferredDirection
        {
            public int xDir;
            public int yDir;
        }

        public Router(Coord myCoord)
        {
            coord = myCoord;

	        m_n = Simulator.network.nodes[ID];
            routerName = "Router";

            neighbors = 0;

            m_lastInj = 0;
            last_starve = 0;
            starve_interval = 0;

            qlen_win = new int[AVG_QLEN_WIN];
        }
        public Router()
        {	
            routerName = "Router";
            qlen_win = new int[AVG_QLEN_WIN];
        }

        public void setNode(Node n)
        {
            m_n = n;
        }


        /********************************************************
         * PUBLIC API
         ********************************************************/

        // called from Network
        public void doStep()
        {
	        statsInput();
			
            _doStep();
	        //statsOutput();
        }

        protected abstract void _doStep(); // called from Network
		public virtual void Scalable_doStep() {return;}
        public abstract bool canInjectFlit(Flit f); // called from Processor
        public abstract void InjectFlit(Flit f); // called from Processor

        public virtual int rank(Flit f1, Flit f2) { return 0; }

        // finally, subclasses should call myProcessor.ejectFlit() whenever a flit
        // arrives (or is part of a reassembled packet ready to be delivered, or whatever)

        // also, subclasses should call statsInput() before removing
        // inputs, statsOutput() after placing outputs, and
        // statsInjectFlit(f) if a flit is injected,
        // and statsEjectFlit(f) if a flit is ejected.

        // Buffered hierarchical ring: check downstream ring/ejection buffer and see if it's available
		public virtual bool creditAvailable(Flit f) {return false;}
        // Whether it wants to into the global ring or loca ring
		public virtual bool productive(Flit f, int level) {return false;}

        // for flush/retry mechanisms: clear all router state.
        public virtual void flush() { }

		// only work for 4x4 network
		public static bool [] starved = new bool [Config.N + 16];
		public static bool [] now_starved = new bool [Config.N + 16];
		public static Queue<bool[]> starveDelay = new Queue<bool[]>();
		public static Queue<bool[]> throttleDelay = new Queue<bool[]>();
		public static bool [] throttle = new bool [Config.N];		
		public static int n = 0;
		public static int working = -1;
		public static void livelockFreedom()
		{
			bool [] starvetmp = new bool [Config.N + 16];
			bool [] throttletmp = new bool [Config.N];
			for (int i = 0; i < Config.N + 16; i++)
				starvetmp[i] = starved[i];
			starveDelay.Enqueue(starvetmp);
			if (starveDelay.Count > Config.starveDelay)
				now_starved = starveDelay.Dequeue();
			//			else
//				Console.WriteLine("starveDelay.Count:{0}", starveDelay.Count);

//			Console.WriteLine("n = {0}", n);
			if (now_starved[n])
			{				
/*				Console.WriteLine("cycle: {0}", Simulator.CurrentRound);
				for (int i = 0; i < 24; i++)
					Console.WriteLine("starved[{0}] = {1}\tnow_starved[{2}] = {3}", i, starved[i], i, now_starved[i]);
				Console.WriteLine("\n");

				for (int j = 0; j < starveDelay.Count; j++)
				{
					for (int i = 0; i < 24; i++)										
						Console.WriteLine("now_starved[{0}] = {1}", i, now_starved[i]);
					now_starved = starveDelay.Dequeue();
				}
*/

//				Console.ReadKey(true);
				if (throttletmp[(n+1) % Config.N] == false)
					Simulator.stats.starveTriggered.Add(1);
				Simulator.stats.allNodeThrottled.Add(1);
				for (int i = 0; i < Config.N; i++)
					if (i != n)
						throttletmp[i] = true;
				working = n;				
			}
			else 
				working = -1;
			if (working == -1)
			{
				for (int i = 0; i < Config.N; i++)
					throttletmp[i] = false;
				n = (n == Config.N + 8 - 1) ? 0 : n + 1;
			}
			throttleDelay.Enqueue(throttletmp);
			if (throttleDelay.Count > Config.starveDelay)
				throttle = throttleDelay.Dequeue();
//			for (int i = 0; i < Config.N; i++)
//				Console.WriteLine("delay {0}. throttle[{1}] : {2}", Config.starveDelay, i, throttle[i]);
//			Console.WriteLine("\n");
//			Console.ReadKey(true);
		}
        /********************************************************
         * ROUTING HELPERS
         ********************************************************/

        protected PreferredDirection determineDirection(Flit f)
        {
            return determineDirection(f, new Coord(0, 0));
        }

        protected PreferredDirection determineDirection(Flit f, Coord current)
        {
            PreferredDirection pd;
            pd.xDir = Simulator.DIR_NONE;
            pd.yDir = Simulator.DIR_NONE;

            if (f.state == Flit.State.Placeholder) return pd;

            //if (f.packet.ID == 238)
            //    Console.WriteLine("packet 238 at ID ({0},{1}), wants ({2},{3})", current.x, current.y, f.packet.dest.x, f.packet.dest.y);
            
            return determineDirection(f.dest);
        }

        protected PreferredDirection determineDirection(Coord c)
        {
            PreferredDirection pd;
            pd.xDir = Simulator.DIR_NONE;
            pd.yDir = Simulator.DIR_NONE;

            if(Config.torus) {
              int x_sdistance = Math.Abs(c.x - coord.x);
              int x_wdistance = Config.network_nrX - Math.Abs(c.x - coord.x);
              int y_sdistance = Math.Abs(c.y - coord.y);
              int y_wdistance = Config.network_nrY - Math.Abs(c.y - coord.y);
              bool x_dright, y_ddown;

              x_dright = coord.x < c.x;
              y_ddown = c.y < coord.y;

              if(c.x == coord.x)
                pd.xDir = Simulator.DIR_NONE;
              else if(x_sdistance < x_wdistance)
                pd.xDir = (x_dright) ? Simulator.DIR_RIGHT : Simulator.DIR_LEFT;
              else
                pd.xDir = (x_dright) ? Simulator.DIR_LEFT : Simulator.DIR_RIGHT;

              if(c.y == coord.y)
                pd.yDir = Simulator.DIR_NONE;
              else if(y_sdistance < y_wdistance)
                pd.yDir = (y_ddown) ? Simulator.DIR_DOWN : Simulator.DIR_UP;
              else
                pd.yDir = (y_ddown) ? Simulator.DIR_UP : Simulator.DIR_DOWN;

            } else {
              if (c.x > coord.x)
                  pd.xDir = Simulator.DIR_RIGHT;
              else if (c.x < coord.x)
                  pd.xDir = Simulator.DIR_LEFT;
              else
                  pd.xDir = Simulator.DIR_NONE;

              if (c.y > coord.y)
                  pd.yDir = Simulator.DIR_UP;
              else if (c.y < coord.y)
                  pd.yDir = Simulator.DIR_DOWN;
              else
                  pd.yDir = Simulator.DIR_NONE;
            }

            if (Config.dor_only && pd.xDir != Simulator.DIR_NONE)
                pd.yDir = Simulator.DIR_NONE;

            return pd;
        }

        // returns true if the direction is good for this packet. 
        protected bool isDirectionProductive(Coord dest, int direction)
        {
            bool answer = false;
            switch (direction)
            {
                case Simulator.DIR_UP: answer = (dest.y > coord.y); break;
                case Simulator.DIR_RIGHT: answer = (dest.x > coord.x); break;
                case Simulator.DIR_LEFT: answer = (dest.x < coord.x); break;
                case Simulator.DIR_DOWN: answer = (dest.y < coord.y); break;
                default: throw new Exception("This function shouldn't be called in this case!");
            }
            return answer;
        }

        protected int dimension_order_route(Flit f)
        {
            if (f.packet.dest.x < coord.x)
                return Simulator.DIR_LEFT;
            else if (f.packet.dest.x > coord.x)
                return Simulator.DIR_RIGHT;
            else if (f.packet.dest.y < coord.y)
                return Simulator.DIR_DOWN;
            else if (f.packet.dest.y > coord.y)
                return Simulator.DIR_UP;
            else //if the destination's coordinates are equal to the router's coordinates
                return Simulator.DIR_UP;
        }

        /********************************************************
         * STATISTICS
         ********************************************************/
        protected int incomingFlits;
        private void statsInput()
        {        	
            //int goldenCount = 0;
            incomingFlits = 0;
			if (Config.topology == Topology.Mesh)
				for (int i = 0; i < 4; i++)
					if (linkIn[i] != null && linkIn[i].Out != null)
						Simulator.stats.flitsToRouter.Add(1);
			if (Config.topology == Topology.HR_8drop || Config.topology == Topology.MeshOfRings)
			{
				if (this is Router_Node)
					for (int i = 0; i < 2; i++)
						if (linkIn[i].Out != null)
							Simulator.stats.flitsToHRnode.Add(1);
				if (this is Router_Bridge)
				{
					if (RouterType == 1)
					{	
						for (int i = 0; i < 2; i++)
							if (LLinkIn[i].Out != null)
								Simulator.stats.flitsToHRbridge.Add(1);
						for (int i = 0; i < 4; i++)
							if (GLinkIn[i].Out != null)
								Simulator.stats.flitsToHRbridge.Add(1);
					}
					else if (RouterType == 2)
					{
						for (int i = 0; i < 4; i++)
							if (LLinkIn[i].Out != null)
								Simulator.stats.flitsToHRbridge.Add(1);
						for (int i = 0; i < 8; i++)
							if (GLinkIn[i].Out != null)
								Simulator.stats.flitsToHRbridge.Add(1);
					}
					else
						throw new Exception("The RouterType should only be 1 or 2");
				}
			}
  /*          if (Config.ScalableRingClustered == false && Config.RingClustered == false && Config.TorusSingleRing == false && Config.HierarchicalRing == false && Config.Simple_HR == false)
            {
            	for (int i = 0; i < 4; i++)
           		{
               		if (linkIn[i] != null && linkIn[i].Out != null)
                	{
                	    linkIn[i].Out.Deflected = false;

                	    if (Simulator.network.golden.isGolden(linkIn[i].Out))
                	        goldenCount++;
                	    incomingFlits++;
                	}
            	}
            }*/
//            Simulator.stats.golden_pernode.Add(goldenCount);
//            Simulator.stats.golden_bycount[goldenCount].Add();
//
//            Simulator.stats.traversals_pernode[incomingFlits].Add();
        }

        private void statsOutput()
        {
            int deflected = 0;
            int unproductive = 0;
            int traversals = 0;
            int links = (Config.RingClustered || Config.ScalableRingClustered)? 2:4;
            for (int i = 0; i < links; i++)
            {
                if (linkOut[i] != null && linkOut[i].In != null)
                {
                    if (linkOut[i].In.Deflected)
                    {
                        // deflected! (may still be productive, depending on deflection definition/DOR used)
                        deflected++;
                        linkOut[i].In.nrOfDeflections++;
                        Simulator.stats.deflect_flit_byloc[ID].Add();

                        if (linkOut[i].In.packet != null)
                        {
                            Simulator.stats.deflect_flit_bysrc[linkOut[i].In.packet.src.ID].Add();
                            Simulator.stats.deflect_flit_byreq[linkOut[i].In.packet.requesterID].Add();
                        }
                    }

                    if (!isDirectionProductive(linkOut[i].In.dest, i))
                    {
                        //unproductive!
                        unproductive++;
                        Simulator.stats.unprod_flit_byloc[ID].Add();

                        if (linkOut[i].In.packet != null)
                            Simulator.stats.unprod_flit_bysrc[linkOut[i].In.packet.src.ID].Add();
                    }
                    traversals++;
                    //linkOut[i].In.deflectTest();
                }
            }

            Simulator.stats.deflect_flit.Add(deflected);
            Simulator.stats.deflect_flit_byinc[incomingFlits].Add(deflected);
            Simulator.stats.unprod_flit.Add(unproductive);
            Simulator.stats.unprod_flit_byinc[incomingFlits].Add(unproductive);
            Simulator.stats.flit_traversals.Add(traversals);

            int qlen = m_n.RequestQueueLen;

            qlen_count -= qlen_win[qlen_ptr];
            qlen_count += qlen;

            // Compute the average queue length
            qlen_win[qlen_ptr] = qlen;
            if(++qlen_ptr >= AVG_QLEN_WIN) qlen_ptr=0;

            avg_qlen = (float)qlen_count / (float)AVG_QLEN_WIN;
            

        }

        protected void statsInjectFlit(Flit f)
        {
            //if (f.packet.src.ID == 3) Console.WriteLine("inject flit: packet {0}, seq {1}",
            //        f.packet.ID, f.flitNr);

            Simulator.stats.inject_flit.Add();
            if (f.isHeadFlit) Simulator.stats.inject_flit_head.Add();
            if (f.packet != null)
            {
                Simulator.stats.inject_flit_bysrc[f.packet.src.ID].Add();
                //Simulator.stats.inject_flit_srcdest[f.packet.src.ID, f.packet.dest.ID].Add();
            }

            if (f.packet != null && f.packet.injectionTime == ulong.MaxValue)
                f.packet.injectionTime = Simulator.CurrentRound;
            f.injectionTime = Simulator.CurrentRound;

            ulong hoq = Simulator.CurrentRound - m_lastInj;
            m_lastInj = Simulator.CurrentRound;

            Simulator.stats.hoq_latency.Add(hoq);
            Simulator.stats.hoq_latency_bysrc[coord.ID].Add(hoq);

            m_inject++;
        }

        protected void statsEjectFlit(Flit f)
        {
            //if (f.packet.src.ID == 3) Console.WriteLine("eject flit: packet {0}, seq {1}",
            //        f.packet.ID, f.flitNr);

            // per-flit latency stats
            ulong net_latency = Simulator.CurrentRound - f.injectionTime;
            ulong total_latency = Simulator.CurrentRound - f.packet.creationTime;
            ulong inj_latency = total_latency - net_latency;

            Simulator.stats.flit_inj_latency.Add(inj_latency);
            Simulator.stats.flit_net_latency.Add(net_latency);
            Simulator.stats.flit_total_latency.Add(total_latency);
			Simulator.stats.ejectTrial.Add(f.ejectTrial);
			Simulator.stats.minNetLatency.Add(f.firstEjectTrial - f.injectionTime);
			if (f.ejectTrial > 1)
			{
				Simulator.stats.destDeflectedNetLatency.Add(net_latency);
				Simulator.stats.destDeflectedMinLatency.Add(f.firstEjectTrial - f.injectionTime);
				Simulator.stats.multiEjectTrialFlits.Add();
			}
			else if (f.ejectTrial == 1)
				Simulator.stats.singleEjectTrialFlits.Add();
			//else if (this is Router_Flit) 
			//	throw new Exception("The eject trial is incorrect");

			if (Config.N == 16)
			{
		//		if (net_latency > 10) 
		//			Console.WriteLine("src: {0}, dest:{1}, latency:{2}", f.packet.src.ID, f.packet.dest.ID, net_latency);
				if (f.packet.dest.ID / 4 == f.packet.src.ID / 4)
				{
					Simulator.stats.flitLocal.Add(1);
					Simulator.stats.netLatency_local.Add(net_latency);			
				}
				else if (Math.Abs(f.packet.dest.ID / 4 - f.packet.src.ID / 4) != 2) // one hop
				{
					Simulator.stats.flit1hop.Add(1);
					Simulator.stats.netLatency_1hop.Add(net_latency);
					Simulator.stats.timeInBuffer1hop.Add(f.timeSpentInBuffer);
					Simulator.stats.timeInTheDestRing.Add(f.timeInTheDestRing);
					Simulator.stats.timeInTheSourceRing.Add(f.timeInTheSourceRing);
					Simulator.stats.timeInGR1hop.Add(f.timeInGR);
				}
				else  // 2 hop 
				{
					Simulator.stats.flit2hop.Add(1);
					Simulator.stats.netLatency_2hop.Add(net_latency);
					Simulator.stats.timeInBuffer2hop.Add(f.timeSpentInBuffer);
					Simulator.stats.timeInTheDestRing.Add(f.timeInTheDestRing);
					Simulator.stats.timeInTheTransitionRing.Add(f.timeInTheTransitionRing);
					Simulator.stats.timeInGR2hop.Add(f.timeInGR);
					Simulator.stats.timeInTheSourceRing.Add(f.timeInTheSourceRing);
				}				
				Simulator.stats.timeWaitToInject.Add(f.timeWaitToInject);		
			}
			if (Config.N == 64)
			{
				if (f.packet.dest.ID / 4 == f.packet.src.ID / 4)
					Simulator.stats.flitLocal.Add(1);
				else if (f.packet.dest.ID /16 == f.packet.src.ID / 16)
					Simulator.stats.flitL1Global.Add(1);
			}

            Simulator.stats.eject_flit.Add();
            Simulator.stats.eject_flit_bydest[f.packet.dest.ID].Add();

            int minpath = Math.Abs(f.packet.dest.x - f.packet.src.x) + Math.Abs(f.packet.dest.y - f.packet.src.y);
            Simulator.stats.minpath.Add(minpath);
            Simulator.stats.minpath_bysrc[f.packet.src.ID].Add(minpath);

            //f.dumpDeflections();
            Simulator.stats.deflect_perdist[f.distance].Add(f.nrOfDeflections);
            if(f.nrOfDeflections!=0)
                Simulator.stats.deflect_perflit_byreq[f.packet.requesterID].Add(f.nrOfDeflections);
        }

        protected void statsEjectPacket(Packet p)
        {
            ulong net_latency = Simulator.CurrentRound - p.injectionTime;
            ulong total_latency = Simulator.CurrentRound - p.creationTime;

            Simulator.stats.net_latency.Add(net_latency);
            Simulator.stats.total_latency.Add(total_latency);
            Simulator.stats.net_latency_bysrc[p.src.ID].Add(net_latency);
            Simulator.stats.net_latency_bydest[p.dest.ID].Add(net_latency);
            //Simulator.stats.net_latency_srcdest[p.src.ID, p.dest.ID].Add(net_latency);
            Simulator.stats.total_latency_bysrc[p.src.ID].Add(total_latency);
            Simulator.stats.total_latency_bydest[p.dest.ID].Add(total_latency);
            //Simulator.stats.total_latency_srcdest[p.src.ID, p.dest.ID].Add(total_latency);
        }

        public override string ToString()
        {
            return routerName + " (" + coord.x + "," + coord.y + ")";
        }

        public string getRouterName()
        {
            return routerName;
        }

        public Router neighbor(int dir)
        {
            int x, y;
            switch (dir)
            {
                case Simulator.DIR_UP: x = coord.x; y = coord.y + 1; break;
                case Simulator.DIR_DOWN: x = coord.x; y = coord.y - 1; break;
                case Simulator.DIR_RIGHT: x = coord.x + 1; y = coord.y; break;
                case Simulator.DIR_LEFT: x = coord.x - 1; y = coord.y; break;
                default: return null;
            }
            // mesh, not torus: detect edge
            if (x < 0 || x >= Config.network_nrX || y < 0 || y >= Config.network_nrY) return null;

            return Simulator.network.routers[Coord.getIDfromXY(x, y)];
        }

        public void close()
        {
        }

        public virtual void visitFlits(Flit.Visitor fv)
        {
        }

        public void statsStarve(Flit f)
        {
            Simulator.stats.starve_flit.Add();
            Simulator.stats.starve_flit_bysrc[f.packet.src.ID].Add();

            if (last_starve == Simulator.CurrentRound - 1) {
              starve_interval++;
            } else {
              Simulator.stats.starve_interval_bysrc[f.packet.src.ID].Add(starve_interval);
              starve_interval = 0;
            }

            last_starve = Simulator.CurrentRound;
        }

        public int linkUtil()
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
                if (linkIn[i] != null && linkIn[i].Out != null)
                    count++;
            return count;
        }

        public double linkUtilNeighbors()
        {
            int tot = 0, used = 0;
            for (int dir = 0; dir < 4; dir++)
            {
                Router n = neighbor(dir);
                if (n == null) continue;
                tot += n.neighbors;
                used += n.linkUtil();
            }

            return (double)used / tot;
        }
    }
}
