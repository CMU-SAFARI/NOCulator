//#define CONNECTION
//#define STARVE
//#define FLIT
//#define INEJ
//#define INTERCONNECT
//#define LOOPS

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public abstract class RingRouter : Router
    {
        public RingCoord ringCoord;
        public int ringID { get { return ringCoord.ID; } }

        public bool clockwise;

		public RingRouter(Coord myCoord) : base (myCoord)
		{
			throw new Exception("You really can't make a ring router with a normal coordinate..., defeats the purpose of ringCoord");
		}	
	
        public RingRouter(RingCoord rc, bool isNode) : base (rc, isNode)
        {
            ringCoord = rc;
            routerName = "RingRouter";
        }

        /********************************************************
         * ROUTING HELPERS
         ********************************************************/

        protected override PreferredDirection determineDirection(Flit f)
        {
            return determineDirection(f, new RingCoord(0, 0, 0));
        }

        protected PreferredDirection determineDirection(Flit f, RingCoord current)
        {
            PreferredDirection pd;
            pd.xDir = Simulator.DIR_NONE;
            pd.yDir = Simulator.DIR_NONE;
            pd.zDir = Simulator.DIR_NONE;

            if (f.state == Flit.State.Placeholder) return pd;

            //if (f.packet.ID == 238)
            //    Console.WriteLine("packet 238 at ID ({0},{1}), wants ({2},{3})", current.x, current.y, f.packet.ringdest.x, f.packet.ringdest.y);
            
            return determineDirection(f.ringdest);
        }

		//TODO: fix for ring coordinates, if I didn't already
        protected PreferredDirection determineDirection(RingCoord c)
        {
            PreferredDirection pd;
            pd.xDir = Simulator.DIR_NONE;
            pd.yDir = Simulator.DIR_NONE;
            pd.zDir = Simulator.DIR_NONE;
            pd.zDir = ((c.z - ringCoord.z) >= 0) ? 1 : 0;

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

        protected override int dimension_order_route(Flit f)
        {
            if (f.packet.ringdest.x < ringCoord.x)
                return Simulator.DIR_LEFT;
            else if (f.packet.ringdest.x > ringCoord.x)
                return Simulator.DIR_RIGHT;
            else if (f.packet.ringdest.y < ringCoord.y)
                return Simulator.DIR_DOWN;
            else if (f.packet.ringdest.y > ringCoord.y)
                return Simulator.DIR_UP;
            else if (f.packet.ringdest.z > ringCoord.z)
            	return Simulator.DIR_CW;
            else if (f.packet.ringdest.z < ringCoord.z)
            	return Simulator.DIR_CCW;
            else //if the destination's coordinates are equal to the router's coordinates
                return Simulator.DIR_CW;
        }

        /********************************************************
         * STATISTICS
         ********************************************************/
        protected override void statsInput()
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

        protected override void statsOutput()
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

                    traversals++;
                    //linkOut[i].In.deflectTest();
                }
            }

            Simulator.stats.deflect_flit.Add(deflected);
            Simulator.stats.deflect_flit_byinc[incomingFlits].Add(deflected);
            Simulator.stats.unprod_flit.Add(unproductive);
            Simulator.stats.unprod_flit_byinc[incomingFlits].Add(unproductive);
            Simulator.stats.flit_traversals.Add(traversals);


            if(m_n == null)
            	return;
            	
            int qlen = m_n.RequestQueueLen;

            qlen_count -= qlen_win[qlen_ptr];
            qlen_count += qlen;

            // Compute the average queue length
            
            qlen_win[qlen_ptr] = qlen;
            if(++qlen_ptr >= AVG_QLEN_WIN) qlen_ptr=0;

            avg_qlen = (float)qlen_count / (float)AVG_QLEN_WIN;
            

        }

        protected override void statsInjectFlit(Flit f)
        {
            //if (f.packet.src.ID == 3) Console.WriteLine("inject flit: packet {0}, seq {1}",
            //        f.packet.ID, f.flitNr);

            Simulator.stats.inject_flit.Add();
            if (f.isHeadFlit) Simulator.stats.inject_flit_head.Add();
            if (f.packet != null)
            {
                Simulator.stats.inject_flit_bysrc[f.packet.src.ID].Add();
                //Simulator.stats.inject_flit_srcdest[f.packet.src.ID, f.packet.ringdest.ID].Add();
            }

            if (f.packet != null && f.packet.injectionTime == ulong.MaxValue)
                f.packet.injectionTime = Simulator.CurrentRound;
            f.injectionTime = Simulator.CurrentRound;

            ulong hoq = Simulator.CurrentRound - m_lastInj;
            m_lastInj = Simulator.CurrentRound;

            Simulator.stats.hoq_latency.Add(hoq);
            Simulator.stats.hoq_latency_bysrc[coord.ID].Add(hoq);
        }

        protected override void statsEjectFlit(Flit f)
        {
            //if (f.packet.src.ID == 3) Console.WriteLine("eject flit: packet {0}, seq {1}",
            //        f.packet.ID, f.flitNr)
			
            // per-flit latency stats
            ulong net_latency = Simulator.CurrentRound - f.injectionTime;
            ulong total_latency = Simulator.CurrentRound - f.packet.creationTime;
            ulong inj_latency = total_latency - net_latency;

            Simulator.stats.flit_inj_latency.Add(inj_latency);
            Simulator.stats.flit_net_latency.Add(net_latency);
            Simulator.stats.flit_total_latency.Add(inj_latency);

            Simulator.stats.eject_flit.Add();
            Simulator.stats.eject_flit_bydest[f.packet.dest.ID].Add();

            int minpath = Math.Abs(f.packet.ringdest.x - f.packet.ringsrc.x) + Math.Abs(f.packet.ringdest.y - f.packet.ringsrc.y);
            Simulator.stats.minpath.Add(minpath);
            Simulator.stats.minpath_bysrc[f.packet.src.ID].Add(minpath);

            //f.dumpDeflections();
            Simulator.stats.deflect_perdist[f.distance].Add(f.nrOfDeflections);
            if(f.nrOfDeflections!=0)
                Simulator.stats.deflect_perflit_byreq[f.packet.requesterID].Add(f.nrOfDeflections);
        }

        protected override void statsEjectPacket(Packet p)
        {
            ulong net_latency = Simulator.CurrentRound - p.injectionTime;
            ulong total_latency = Simulator.CurrentRound - p.creationTime;

            Simulator.stats.net_latency.Add(net_latency);
            Simulator.stats.total_latency.Add(total_latency);
            Simulator.stats.net_latency_bysrc[p.src.ID].Add(net_latency);
            Simulator.stats.net_latency_bydest[p.dest.ID].Add(net_latency);
            //Simulator.stats.net_latency_srcdest[p.src.ID, p.ringdest.ID].Add(net_latency);
            Simulator.stats.total_latency_bysrc[p.src.ID].Add(total_latency);
            Simulator.stats.total_latency_bydest[p.dest.ID].Add(total_latency);
            //Simulator.stats.total_latency_srcdest[p.src.ID, p.ringdest.ID].Add(total_latency);
            
           	
        }

        public override string ToString()
        {
            return routerName + " (" + ringCoord.x + "," + ringCoord.y + "," + ringCoord.z + ")";
        }

        public override void statsStarve(Flit f)
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
        
        public override int linkUtil()
        {
            int count = 0;
            for (int i = 0; i < 6; i++)
                if (linkIn[i] != null && linkIn[i].Out != null)
                    count++;
            return count;
        }

    }
   
    
    public class RingRouter_Simple : RingRouter
    {
        // injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;
		protected int injectedCount;
		protected int blockCount;
		
        public RingRouter_Simple(RingCoord rc, bool isNode)
            : base(rc, isNode)
        {
			routerName    = "Simple Ring Router";
            m_injectSlot  = null;
            m_injectSlot2 = null;
            injectedCount = 0;
            blockCount    = 0;
        }

        // accept one ejected flit into rxbuf
        void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }

        Flit ejectLocalNew()
        {
            Flit ret = null;
            int dir = (clockwise) ? Simulator.DIR_CCW : Simulator.DIR_CW;

#if CONNECTION            
            if (clockwise) {
            	if(linkIn[Simulator.DIR_CCW] == null || linkOut[Simulator.DIR_CW] == null)
            		throw new Exception(String.Format("{0} has no connection {1} {2} |", this, linkIn[Simulator.DIR_CCW], linkOut[Simulator.DIR_CW]));
            }
            else
            {
            	if(linkOut[Simulator.DIR_CCW] == null || linkIn[Simulator.DIR_CW] == null)
            		throw new Exception(String.Format("{0} has no connection {1} {2} |", this, linkIn[Simulator.DIR_CW], linkOut[Simulator.DIR_CCW]));
            }		
#endif
			
			// It's a placeholder so don't eject it
			if (linkIn[dir].Out == null || linkIn[dir].Out.initPrio == -1) {
				return null;
			}
			
            if(linkIn[dir].Out != null && linkIn[dir].Out.dest.ID == ID) {
            	ret = linkIn[dir].Out;
            	linkIn[dir].Out = null;
            }
            
#if INEJ
            if (ret != null)
                Console.WriteLine("> ejecting flit  {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, ringCoord, Simulator.CurrentRound);
#endif
            return ret;
        }

  		/*
  		 * Injects flits
  		 */      
        void inject(out bool injected)
        {
        	injected = false;
        	int tempDir = (clockwise) ? Simulator.DIR_CCW : Simulator.DIR_CW;
        	if (input[6] != null)
        	{
        		if (input[tempDir] == null)
        		{
        			injected = true;
        			input[tempDir] = input[6];
        			input[6] = null;
        		}
        	}
        	#if INEJ
            if (injected)
                Console.WriteLine("< injecting flit  {0}.{1} at node {2} cyc {3}", input[tempDir].packet.ID, input[tempDir].flitNr, ringCoord, Simulator.CurrentRound);
			#endif
        }
        
        void route()
        {
        	Flit[] temp = new Flit[7];
        	for(int i = 4; i < 6; i++)
        		temp[i] = input[i];
        	
        	int dir    = (clockwise) ? Simulator.DIR_CW  : Simulator.DIR_CCW;
        	int oppDir = (clockwise) ? Simulator.DIR_CCW :  Simulator.DIR_CW;	
        	
        	input[dir]  = temp[oppDir];
        	input[oppDir]= null;	
        }
        
        Flit[] input = new Flit[7]; // keep this as a member var so we don't
                                    // have to allocate on every step (why can't
                                     // we have arrays on the stack like in C?)

        protected override void _doStep()
        {
            /* Ejection selection and ejection */
            Flit eject = null;
            eject =  ejectLocalNew();
			
            /* Setup the inputs */
            for (int dir = 4; dir < 6; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                    input[dir] = linkIn[dir].Out;
                    input[dir].inDir = dir;
                }
                else
                    input[dir] = null;
            }
            
            for (int dir = 4; dir < 6; dir++)
            {
            	if (input[dir] != null && ringCoord.Equals(input[dir].packet.ringsrc))
            	{	
            		input[dir].priority += 1;
            		#if LOOPS
						Console.WriteLine("\t\t{0}.{1} at node {2} cyc {3} dest {4}\tPriority: {5}", input[dir].packet.ID, input[dir].flitNr, ringCoord, Simulator.CurrentRound, input[dir].ringdest, input[dir].priority);
            		#endif
            	}	
            }
            
            /* Injection */
            Flit inj = null;
            bool injected = false;
			
			/* Pick slot to inject */
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

            /* Port 4 becomes the injected line */
            input[6] = inj;

            /* If there is data, set the injection direction */
            if (inj != null)
                inj.inDir = -1;
			
            /* Inject and route flits in correct directions */
            if (injectedCount > Config.injectHoldThreshold)
            {
            	blockCount = Config.blockInjectCount; 
            	injectedCount = 0;
            }
            
            if (blockCount < 1)
            	inject(out injected);
            else
            	blockCount--;
            
            if (injected)
				injectedCount++;
			else if (Config.injectedCountReset)
				injectedCount = 0;
				
			#if STARVE
				if(!injected && input[6] != null)
					Console.WriteLine("Starved flit {0}.{1} at node {2} cyc {3}", input[6].packet.ID, input[6].flitNr, ringCoord, Simulator.CurrentRound);
			#endif
            if (injected && input[6] != null)
            	throw new Exception("Not removing injected flit from slot");
            
            route();
            
            /* If something wasn't injected, move the flit into the injection slots 
             *   If it was injected, take stats                                     
             */
            if (!injected)
            {
                if (m_injectSlot == null)
                    m_injectSlot = inj;
                else
                    m_injectSlot2 = inj;
            }
            else
                statsInjectFlit(inj);

            /* Put ejected flit in reassembly buffer */
            if (eject != null)
                acceptFlit(eject);
            
            
            /* Assign outputs */
            for (int dir = 4; dir < 6; dir++)
			{
                if (input[dir] != null)
                {
#if FLIT
        		Console.WriteLine("flit {0}.{1} at node {2} cyc {3}", input[dir].packet.ID, input[dir].flitNr, ringCoord, Simulator.CurrentRound);
#endif 
                    if (linkOut[dir] == null)
                        throw new Exception(String.Format("router {0} does not have link in dir {1}",
                                    coord, dir));
                    linkOut[dir].In = input[dir];
                }
#if FLIT
                //else if(dir == Simulator.DIR_LEFT || dir == Simulator.DIR_RIGHT)
      			//	Console.WriteLine("no flit at node {0} cyc {1}", coord, Simulator.CurrentRound);
#endif 
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
    }

    public class Connector : RingRouter
    {
    	public int connectionDirection;
    	public int injectPlaceholder;
    	//public Flit removedFlit;
    	public ResubBuffer rBuf;
    	
        public Connector(RingCoord rc, bool isNode)
            : base(rc, isNode)
        {
        	routerName = "Ring Connector";
        	ringCoord = rc;
        	connectionDirection = Simulator.DIR_NONE;
        	injectPlaceholder = 0;
        	rBuf = new ResubBuffer(3);
        }

        // accept one ejected flit into rxbuf
        void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }
        
        bool isRingProductive(Flit f, int dir)
        {
        	int cols = (Config.network_nrX / Config.ringWidth);
        	int rows = (Config.network_nrY / Config.ringHeight);
        	
        	if(f.initPrio == -1)
        		return true;
        		
           	switch(dir)  // TODO: Fix for torus
        	{
        		case Simulator.DIR_UP:		if (Config.torus)
        									{
        										int direct = (ringCoord.y - f.ringdest.y);
        										int indirect;
        										
        										if (direct < 0) {
        											indirect = (rows - f.ringdest.y + ringCoord.y);
        											return (Math.Abs(indirect) < Math.Abs(direct));
        										}
        										else {
        											indirect = (rows - ringCoord.y + f.ringdest.y);
        											return (Math.Abs(indirect) > Math.Abs(direct));
        										}
        											
        									}
        									else
        										return (ringCoord.y > f.ringdest.y);
        									
        		case Simulator.DIR_DOWN:	if (Config.torus)
        									{
        										int direct = (f.ringdest.y - ringCoord.y);
        										int indirect;
        										
        										if (direct < 0) {
        											indirect = (rows - ringCoord.y + f.ringdest.y);
        											return (Math.Abs(indirect) < Math.Abs(direct));
        										}
        										else {
        											indirect = (rows - f.ringdest.y + ringCoord.y);
        											return (Math.Abs(indirect) > Math.Abs(direct));
        										}
        											
        									}
        									else
        									return (ringCoord.y < f.ringdest.y);
        									
        		case Simulator.DIR_LEFT:	if (Config.torus)
        									{
        										int direct = (f.ringdest.x - ringCoord.x);
        										int indirect;
        										
        										if (direct < 0) {
        											indirect = (cols - ringCoord.x + f.ringdest.x);
        											return (Math.Abs(indirect) < Math.Abs(direct));
        										}
        										else {
        											indirect = (cols - f.ringdest.x + ringCoord.x);
        											return (Math.Abs(indirect) > Math.Abs(direct));
        										}
        											
        									}
        									else
        									return (ringCoord.x > f.ringdest.x);
        									
        		case Simulator.DIR_RIGHT:	if (Config.torus)
        									{
        										int direct = (ringCoord.x - f.ringdest.x);
        										int indirect;
        										
        										if (direct < 0) {
        											indirect = (cols - f.ringdest.x + ringCoord.x);
        											return (Math.Abs(indirect) > Math.Abs(direct));
        										}
        										else {
        											indirect = (cols - ringCoord.x + f.ringdest.x);
        											return (Math.Abs(indirect) > Math.Abs(direct));
        										}
        											
        									}
        									else
        									return (ringCoord.x < f.ringdest.x);
        									
        		case Simulator.DIR_CW:		return (ringCoord.y == f.ringdest.y && ringCoord.x == f.ringdest.x && ringCoord.z < f.ringdest.z);
        									
        		case Simulator.DIR_CCW:		return (ringCoord.y == f.ringdest.y && ringCoord.x == f.ringdest.x && ringCoord.z > f.ringdest.z);
        									
        		case Simulator.DIR_NONE:	return isDestRing(f);	
        		
        	    default: throw new Exception(" NOT A DIRECTION! ");
        	
        	}
        }
        
        bool isDestRing(Flit f)
        {
        	if(f.initPrio == -1)
        		return true;
        		
        	return (f.tempX == ringCoord.x && f.tempY == ringCoord.y);
        }

        void getNeighborXY(out int xcoord, out int ycoord)
        {
        	switch (connectionDirection)
        	{
        		case Simulator.DIR_UP: 
        			ycoord = ringCoord.y - 1;
        			xcoord = ringCoord.x;
        			break;
        		
        		case Simulator.DIR_DOWN: 
        			ycoord = ringCoord.y + 1;
        			xcoord = ringCoord.x;
        			break;
        		
        		case Simulator.DIR_LEFT: 
        			ycoord = ringCoord.y;
        			xcoord = ringCoord.x - 1;
        			break;
        		
        		case Simulator.DIR_RIGHT: 
        			ycoord = ringCoord.y;
        			xcoord = ringCoord.x + 1;
        			break;
        		
        		default: throw new Exception(" NOT A VALID DIRECTION ");
        	}
        }
                        
        void route()
        {
        	Flit[] temp = new Flit[6];
			for (int i = 0; i < 6; i++)
				temp[i] = input[i];
			
			int dir;
			if(clockwise)
				dir = Simulator.DIR_CCW;
			else
				dir = Simulator.DIR_CW;
			
			if(injectPlaceholder > 0)
			{
				injectPlaceholder--;
				if(temp[connectionDirection] != null)
					rBuf.addFlit(temp[connectionDirection]);
					
				temp[connectionDirection] = null;
				
				int tempX = 0;
				int tempY = 0;
				switch (injectPlaceholder % 4)
				{
					case 0:
						tempX = 4; tempY = 3; break;
					case 1:
						tempX = 0; tempY = 0; break;
					case 2: 
						tempX = 1; tempY = 2; break;
					case 3:
						tempX = 2; tempY = 4; break;
				}
				Coord tempCoord = new Coord(tempX, tempY);
				
				temp[connectionDirection] = new Flit(new Packet(null, 1337, 1337, tempCoord, tempCoord), 1337);
				temp[connectionDirection].initPrio = -1;
				#if INEJ
					Console.WriteLine("!!!!!!!!!! INJECTING PLACEHOLDER {0}.{1} at node {2} cyc {3} !!!!!!!!!!!", temp[connectionDirection].packet.ID, temp[connectionDirection].flitNr, ringCoord, Simulator.CurrentRound);
				#endif
				/*injectPlaceholder--;
				temp[dir] = new Flit(new Packet(null, 1337, 1337, new Coord(4,4), new Coord(4,4)), 1337);
				temp[dir].initPrio = -1;
				*/
			}
			
			if (connectionDirection == dir)
				throw new Exception("Connection direction should not be clockwise or counter clockwise");
			
			// If there is something coming in, try to pull it in.
			if (temp[connectionDirection] != null)
			{	
				if (isDestRing(temp[connectionDirection]))
				{ 
					if(temp[dir] == null)
					{
						#if INTERCONNECT
        					Console.WriteLine("|Moving Into Ring|   \t \tflit {0}.{1} at node {2} cyc {3} \t | dest: {4}", temp[connectionDirection].packet.ID, temp[connectionDirection].flitNr, ringCoord, Simulator.CurrentRound, temp[connectionDirection].packet.ringdest);
						#endif 
						temp[dir] = temp[connectionDirection];
						temp[connectionDirection] = null;
					}
					else
					{
						if (isRingProductive(temp[dir], connectionDirection))
						{
							Flit tempFlit;
							#if INTERCONNECT
        						Console.WriteLine("|Swapping outside-inside|\tflit {0}.{1} && flit {2}.{3} at node {4} cyc {5} \t \n| dest1: {6} dest2: {7}", temp[connectionDirection].packet.ID, temp[connectionDirection].flitNr, temp[dir].packet.ID, temp[dir].flitNr, ringCoord, Simulator.CurrentRound, temp[connectionDirection].packet.ringdest, temp[dir].packet.ringdest);
							#endif 				        
							tempFlit = temp[dir];
							temp[dir] = temp[connectionDirection];
							temp[connectionDirection] = tempFlit;
							getNeighborXY(out temp[connectionDirection].tempX, out temp[connectionDirection].tempY);
							
						}
						#if INTERCONNECT
						else
						{
							Console.WriteLine("!Deflected outside ring!\tflit {0}.{1} at node{2} cyc {3} \t | dest: {4}", temp[connectionDirection].packet.ID, temp[connectionDirection].flitNr, ringCoord, Simulator.CurrentRound, temp[connectionDirection].packet.ringdest);
							Console.WriteLine("!!!! DEFLECTED another outside ring!!!\t flit{0}.{1} at node{2} cyc {3} \t | dest: {4}\t Flit {5}.{6} dest:{7}", temp[dir].packet.ID, temp[dir].flitNr, ringCoord, Simulator.CurrentRound, temp[dir].packet.ringdest, temp[connectionDirection].packet.ID, temp[connectionDirection].flitNr, temp[connectionDirection].ringdest);
						}
						#endif
				    }
			    }
			}
			// Otherwise, try to push something into the connection.
			else
			{
				if(temp[dir] != null && isRingProductive(temp[dir], connectionDirection))
				{
					#if INTERCONNECT
        				Console.WriteLine("|Moving Out of Ring|\t \tflit {0}.{1} at node {2} cyc {3} \t | dest: {4}", temp[dir].packet.ID, temp[dir].flitNr, ringCoord, Simulator.CurrentRound, temp[dir].packet.ringdest);
					#endif 
					temp[connectionDirection] = temp[dir];
					temp[dir] = null;
					getNeighborXY(out temp[connectionDirection].tempX, out temp[connectionDirection].tempY);
				}
				#if INTERCONNECT
				else if (temp[dir] != null)
					Console.WriteLine("!Deflected inside ring!\tflit {0}.{1} at node{2} cyc {3}\t | dest: {4}", temp[dir].packet.ID, temp[dir].flitNr, ringCoord, Simulator.CurrentRound, temp[dir].packet.ringdest);
				#endif
			}	        	
        	input[connectionDirection] = temp[connectionDirection];
        	
        	if (clockwise){
        		input[Simulator.DIR_CW]  = temp[Simulator.DIR_CCW];
        		input[Simulator.DIR_CCW] = null;	
        	}
        	else {
        		input[Simulator.DIR_CCW] = temp[Simulator.DIR_CW];
        		input[Simulator.DIR_CW] = null;
        	}
        }
        
        Flit[] input = new Flit[6]; // keep this as a member var so we don't
                                    // have to allocate on every step (why can't
                                     // we have arrays on the stack like in C?)
                                     // --- because we have a garbage collector instead and want to dynamically allocate everything

        protected override void _doStep()
        {
        	int tempCount = 0;
        	
            /* Setup the inputs */
            for (int dir = 0; dir < 6; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                	tempCount++;
                    input[dir] = linkIn[dir].Out;
                    input[dir].inDir = dir;
                }
                else
                    input[dir] = null;
            }
            
            //To avoid the create / remove counter exception
            if (injectPlaceholder > 0)
            	tempCount++;
            
            /* Only used when forcing a placeholder into the system */
            int tempDir = (clockwise) ? Simulator.DIR_CCW : Simulator.DIR_CW;
            if (input[tempDir] == null && !rBuf.isEmpty())
            {
            	input[tempDir] = rBuf.removeFlit();
            }
            else if (input[connectionDirection] == null && !rBuf.isEmpty())
            {
            	input[connectionDirection] = rBuf.removeFlit();
            }
            
            route();
            
            int endCount = 0;
            
            /* Assign outputs */
            for (int dir = 0; dir < 6; dir++)
			{
                if (input[dir] != null)
                {
#if FLIT
        		Console.WriteLine("flit {0}.{1} at node {2} cyc {3} \t | dir:{4} | dest: {5} connectionDir {6} productive: {7} isDest {8}", input[dir].packet.ID, input[dir].flitNr, ringCoord, Simulator.CurrentRound, dir, input[dir].packet.ringdest, connectionDirection, isRingProductive(input[dir], connectionDirection),isDestRing(input[dir]));
				Console.WriteLine("\tflit {0}.{1} currentRing = {2} destRing = {3},{4}", input[dir].packet.ID, input[dir].flitNr, ringCoord, input[dir].tempX, input[dir].tempY);
#endif 			
					endCount++;
                    if (linkOut[dir] == null)
                        throw new Exception(String.Format("connector {0} does not have link in dir {1}",
                                    ringCoord, dir));
                    linkOut[dir].In = input[dir];
                }
            }
            if (endCount != tempCount)
            	throw new Exception(String.Format("connector {0} created or destroyed flits.  Before: {1} | After: {2}", ringCoord, tempCount, endCount));
        }

        public override bool canInjectFlit(Flit f)
        {
            return false;
        }

        public override void InjectFlit(Flit f)
        {
        	return;
        }

        public override void visitFlits(Flit.Visitor fv)
        {
        	return;
        }
    }	
}

