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

        public Link[] linkOut = new Link[6];
        public Link[] linkIn = new Link[6];
        public Router[] neigh = new Router[6];
        public int neighbors;

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

        // --------------------------------------------------------------------

        public struct PreferredDirection
        {
            public int xDir;
            public int yDir;
			public int zDir;
        }

		public Router(RingCoord rc, bool isNode)
        {
        	int id = RingCoord.getIDfromRingID(rc.ID);
            coord = new Coord(id);

			if(isNode)
            	m_n = Simulator.network.nodes[ID];
            routerName = "Router";

            neighbors = 0;

            m_lastInj = 0;
            last_starve = 0;
            starve_interval = 0;

            qlen_win = new int[AVG_QLEN_WIN];
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
            statsOutput();
        }

        protected abstract void _doStep(); // called from Network

        public abstract bool canInjectFlit(Flit f); // called from Processor
        public abstract void InjectFlit(Flit f); // called from Processor

        public virtual int rank(Flit f1, Flit f2) { return 0; }

        // finally, subclasses should call myProcessor.ejectFlit() whenever a flit
        // arrives (or is part of a reassembled packet ready to be delivered, or whatever)

        // also, subclasses should call statsInput() before removing
        // inputs, statsOutput() after placing outputs, and
        // statsInjectFlit(f) if a flit is injected,
        // and statsEjectFlit(f) if a flit is ejected.

        // for flush/retry mechanisms: clear all router state.
        public virtual void flush() { }

        /********************************************************
         * ROUTING HELPERS
         ********************************************************/

        protected virtual PreferredDirection determineDirection(Flit f)
        {
            return determineDirection(f, new Coord(0, 0));
        }

        protected PreferredDirection determineDirection(Flit f, Coord current)
        {
            PreferredDirection pd;
            pd.xDir = Simulator.DIR_NONE;
            pd.yDir = Simulator.DIR_NONE;
			pd.zDir = Simulator.DIR_NONE;
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
            pd.zDir = Simulator.DIR_NONE;

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
                case Simulator.DIR_UP:    answer = (dest.y > coord.y); break;
                case Simulator.DIR_RIGHT: answer = (dest.x > coord.x); break;
                case Simulator.DIR_LEFT:  answer = (dest.x < coord.x); break;
                case Simulator.DIR_DOWN:  answer = (dest.y < coord.y); break;
                default: throw new Exception("This function shouldn't be called in this case!");
            }
            return answer;
        }

        protected virtual int dimension_order_route(Flit f)
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
        protected virtual void statsInput()
        {
            int goldenCount = 0;
            incomingFlits = 0;
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
            
            Simulator.stats.golden_pernode.Add(goldenCount);
            Simulator.stats.golden_bycount[goldenCount].Add();

            Simulator.stats.traversals_pernode[incomingFlits].Add();
            //Simulator.stats.traversals_pernode_bysrc[ID,incomingFlits].Add();
        }

        protected virtual void statsOutput()
        {
            int deflected = 0;
            int unproductive = 0;
            int traversals = 0;
            for (int i = 0; i < 4; i++)
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
                            Simulator.stats.total_deflected.Add();
                            
                            // Golden counters of deflections 
                            if(Simulator.network.golden.isGolden(linkOut[i].In)) {
                                Simulator.stats.golden_deflected.Add();
                                Simulator.stats.golden_deflect_flit_byloc[ID].Add();
                                Simulator.stats.golden_deflect_flit_bysrc[linkOut[i].In.packet.src.ID].Add();
                                Simulator.stats.golden_deflect_flit_byreq[linkOut[i].In.packet.requesterID].Add();
                            } 
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

        protected virtual void statsInjectFlit(Flit f)
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
        }

        protected virtual void statsEjectFlit(Flit f)
        {
            //if (f.packet.src.ID == 3) Console.WriteLine("eject flit: packet {0}, seq {1}",
            //        f.packet.ID, f.flitNr);

            // per-flit latency stats
            ulong net_latency = Simulator.CurrentRound - f.injectionTime;
            ulong total_latency = Simulator.CurrentRound - f.packet.creationTime;
            ulong inj_latency = total_latency - net_latency;

            Simulator.stats.flit_inj_latency.Add(inj_latency);
            Simulator.stats.flit_net_latency.Add(net_latency);
            Simulator.stats.flit_total_latency.Add(inj_latency);

            Simulator.stats.eject_flit.Add();
            Simulator.stats.eject_flit_bydest[f.packet.dest.ID].Add();

            int minpath = Math.Abs(f.packet.dest.x - f.packet.src.x) + Math.Abs(f.packet.dest.y - f.packet.src.y);
            Simulator.stats.minpath.Add(minpath);
            Simulator.stats.minpath_bysrc[f.packet.src.ID].Add(minpath);

            //f.dumpDeflections();
            Simulator.stats.deflect_perdist[f.distance].Add(f.nrOfDeflections);
            if(f.nrOfDeflections!=0)
                Simulator.stats.deflect_perflit_byreq[f.packet.requesterID].Add(f.nrOfDeflections);
                
            int shortest_path = Math.Abs(f.packet.src.x - f.packet.dest.x) + 
        						Math.Abs(f.packet.src.y - f.packet.dest.y);
            
            if(f.wasSilver)
            {
                Simulator.stats.silver_latency.Add(net_latency);
                Simulator.stats.silver_ejected.Add();
                Simulator.stats.silver_shortest_path.Add(shortest_path);
                Simulator.stats.silver_totalFlits.Add();
                Simulator.stats.silver_hops.Add(f.hops);
                Simulator.stats.silver_nrWasSilver.Add(f.nrWasSilver);
                Simulator.stats.silver_nrEnteredResubmitBuffer.Add(f.rebufEnteredCount);
                Simulator.stats.silver_nrOfDeflections.Add(f.nrWasDeflected);
                
            }


            if(f.wasInRebuf)
            {
            	Simulator.stats.rebuf_latency.Add(net_latency);
            	Simulator.stats.rebuf_ejected.Add();
            	Simulator.stats.rebuf_shortest_path.Add(shortest_path);
    			Simulator.stats.rebuf_totalFlits.Add();
    			Simulator.stats.rebuf_hops.Add(f.hops);
                Simulator.stats.rebuf_nrEnteredResubmitBuffer.Add(f.rebufEnteredCount);
                Simulator.stats.rebuf_nrOfDeflections.Add(f.nrWasDeflected);
            }
            else if(f.wasDeflected)
            {
                Simulator.stats.prio_latency.Add(net_latency);
            	Simulator.stats.prio_ejected.Add();
            	Simulator.stats.prio_shortest_path.Add(shortest_path);
    			Simulator.stats.prio_totalFlits.Add();
    			Simulator.stats.prio_hops.Add(f.hops);
                Simulator.stats.prio_nrOfDeflections.Add(f.nrWasDeflected);
      		}
            else
            {
            	Simulator.stats.none_latency.Add(net_latency);
            	Simulator.stats.none_ejected.Add();
            	Simulator.stats.none_shortest_path.Add(shortest_path);
    			Simulator.stats.none_totalFlits.Add();
    			Simulator.stats.none_hops.Add(f.hops);
                Simulator.stats.none_nrOfDeflections.Add(f.nrWasDeflected);
            }
        }

        protected virtual void statsEjectPacket(Packet p)
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
                case Simulator.DIR_UP:    x = coord.x;     y = coord.y + 1; break;
                case Simulator.DIR_DOWN:  x = coord.x;     y = coord.y - 1; break;
                case Simulator.DIR_RIGHT: x = coord.x + 1; y = coord.y;     break;
                case Simulator.DIR_LEFT:  x = coord.x - 1; y = coord.y;     break;
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

        public virtual void statsStarve(Flit f)
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
        
        public virtual int linkUtil()
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
