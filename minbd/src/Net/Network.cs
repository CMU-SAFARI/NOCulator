//#define TEST
//#define DEBUG

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class Network
    {
        // dimensions
        public int X, Y;

        // every node has a router and a processor; not every processor is an application proc
        public Router[] routers;
        public Node[] nodes;
        public List<Link> links;
        public Workload workload;
        public Golden golden;
        public NodeMapping mapping;
        public CmpCache cache;
        
       	public RingRouter[] ringRouter;
        public RingRouter[] connector;

        // finish mode
        public enum FinishMode { app, insn, synth, cycle, barrier };
        FinishMode m_finish;
        ulong m_finishCount;

        public FinishMode finishMode { get { return m_finish; } }

        public double _cycle_netutil; // HACK -- controllers can use this.
        public ulong  _cycle_insns;   // HACK -- controllers can use this.

        public Network(int dimX, int dimY)
        {
            X = dimX;
            Y = dimY;
        }

        public void setup()
        {
            routers = new Router[Config.N * (1 + ((Config.RingRouter) ? Config.nrConnections : 0))];
            nodes = new Node[Config.N];
            links = new List<Link>();
            cache = new CmpCache();
            
            ringRouter = new RingRouter[Config.N * (1 + Config.nrConnections)];

            /*if(Config.disjointRings)
            {
            	bool ignoreCase = true;
            	if (String.Compare(Config.disjointConnection, "mesh", ignoreCase) == 0)
            		connector = new RingRouter[Config.N]; // Fix later
            	else if (String.Compare(Config.disjointConnection, "torus", ignoreCase) == 0)
            		connector = new RingRouter[Config.N];
            	else if (String.Compare(Config.disjointConnection, "ring", ignoreCase) == 0)
            		connector = new RingRouter[Config.N]; // Fix later
            }*/

            ParseFinish(Config.finish);

            workload = new Workload(Config.traceFilenames);

            mapping = new NodeMapping_AllCPU_SharedCache();

            // create routers and nodes
            for (int n = 0; n < Config.N; n++)
            {
                Coord c = new Coord(n);
                nodes[n] = new Node(mapping, c);
                
                if (!Config.RingRouter)
                {
                	routers[n] = MakeRouter(c);
                	nodes[n].setRouter(routers[n]);
                	routers[n].setNode(nodes[n]);
                }
            }
            /*if (Config.disjointRings)
	            for (int n = 0; n < 8; n++)
	            {
	              	connectors[n] = MakeConnector(n);
	            }
			*/
            // create the Golden manager
            golden = new Golden();

            if (Config.RouterEvaluation)
                return;
			
            /*
             * Ring width = node width of each ring
             * Ring height = node height of each ring
             * NrConnections = number of connections routers per node
             * 
             */
            if (Config.RingRouter)
            {
            	int nrPlaceholdersPerNetwork = Config.nrPlaceholders / 2;
                int nrNodesPerRing = (Config.ringWidth * 2 + Config.ringHeight * 2 - 4);
                int nrConnectPerRing = nrNodesPerRing * Config.nrConnections;
                int nrItemInRing = nrConnectPerRing + nrNodesPerRing;
                
                #if DEBUG
                	Console.WriteLine("NrNodesPerRing = {0}, NrConectPerRing = {1}, NrItemInRing = {2}", nrNodesPerRing, nrConnectPerRing, nrItemInRing);
                #endif
                int prevID = -1;
                
                // TODO: Check edge conditions of for loops.  Currently only using for 2x2 ring.
				bool clockwise = false;
                for (int y = 0; y < Config.network_nrY / Config.ringHeight; y++) {
                    for (int x = 0; x < Config.network_nrX / Config.ringWidth; x++) {
                    	if (Config.alternateDir)
                    	{
                    		clockwise = !clockwise;
                    	}
                    	else
                    	{
                    		clockwise = true;
                    	}
                    	prevID = -1;
                    	
                        for (int z = 0; z < nrItemInRing; z++)
                        {
                            int ringID = RingCoord.getIDfromXYZ(x, y, z);
                            int nodeID = RingCoord.getIDfromRingID(ringID);
                            RingCoord rc = new RingCoord(ringID);
                            
                            #if DEBUG
                            	Console.WriteLine("ringID = {0}, nodeID = {1}, prevID = {2}", ringID, nodeID, prevID);
                            #endif
                            
                            ringRouter[ringID] = makeNodeRouter(rc); 
                            
							ringRouter[ringID].clockwise = clockwise;
							
													                            
							routers[nodeID] = ringRouter[ringID];
							nodes[nodeID].setRouter(ringRouter[ringID]);
							ringRouter[ringID].setNode(nodes[nodeID]);

                            // Link them up if its not the first node in the loop
                            if (prevID != -1)
                            {
                            	Link prev = new Link(Config.router.linkLatency - 1);
                            	links.Add(prev);
                            	
                            	#if DEBUG
                            		Console.WriteLine("Connecting prev{0} & current{1} with link {2}", prevID, ringID, prev);
                            	#endif
                            	
                            	if (clockwise)
                            	{
                            		ringRouter[prevID].linkOut[Simulator.DIR_CW] = prev;
                            		ringRouter[ringID].linkIn[Simulator.DIR_CCW] = prev;
                            	}
                            	else
                            	{
                            		ringRouter[prevID].linkIn[Simulator.DIR_CW]   = prev;
                            		ringRouter[ringID].linkOut[Simulator.DIR_CCW] = prev;
                            	}
                            }
                            
                            prevID = ringID;
                            
                            // Connection routers
                            for (int i = 0; i < Config.nrConnections; i++)
                            {
                                z++;
                                int connectID = RingCoord.getIDfromXYZ(x, y, z);
                                int connect2ID = RingCoord.getIDfromRingID(connectID);
                                RingCoord connectRC = new RingCoord(connectID);
                                
                                ringRouter[connectID] = makeConnection(connectRC);
								ringRouter[connectID].clockwise = clockwise;
								
								
								routers[connect2ID] = ringRouter[connectID];
								
                                Link prev_link = new Link(Config.router.linkLatency - 1);
                                links.Add(prev_link);
                                
                                #if DEBUG
                            		Console.WriteLine("Connecting prev{0} & connect{1} with link {2}", prevID, connectID, prev_link);
                                #endif
                                
                                if (clockwise)
                            	{
                            		ringRouter[prevID].linkOut[Simulator.DIR_CW] = prev_link;
                            		ringRouter[connectID].linkIn[Simulator.DIR_CCW] = prev_link;
                            	}
                            	else
                            	{
                            		ringRouter[prevID].linkIn[Simulator.DIR_CW]   = prev_link;
                            		ringRouter[connectID].linkOut[Simulator.DIR_CCW] = prev_link;
                            	}
                                
                                prevID = connectID;
                            }
                        }
                        
                        // Finish ring
                        int startID = RingCoord.getIDfromXYZ(x, y, 0);
                       	Link finish_link = new Link(Config.router.linkLatency - 1); 
                       	links.Add(finish_link);
                        
                        #if DEBUG
                        Console.WriteLine("Finishing connecting prev{0} & start{1} with link {2}", prevID, startID, finish_link);
                        #endif
                        
                        if (clockwise)
                    	{
                    		ringRouter[prevID].linkOut[Simulator.DIR_CW]  = finish_link;
                    		ringRouter[startID].linkIn[Simulator.DIR_CCW] = finish_link;
                    	}
                    	else
                    	{
                    		ringRouter[prevID].linkIn[Simulator.DIR_CW]    = finish_link;
                    		ringRouter[startID].linkOut[Simulator.DIR_CCW] = finish_link;
                    	} 
                    }
                }
				
                
                if (Config.nrConnections > 1)
                	throw new Exception("Check the rest of the code before you continue making more connections");
				
				#if DEBUG
					Console.WriteLine("Starting connections....");
				#endif
				
                int connectionDir, oppDir;
                int oppX;
                int oppY;
                int oppZ;
               	int currentID, oppID;
                
                for (int y = 0; y < Config.network_nrY / Config.ringHeight; y++) {
					for (int x = 0; x < Config.network_nrX / Config.ringWidth; x++) {
                	 	for (int z = 0; z < nrItemInRing; z++) {
                	 		for (int i = 0; i < Config.nrConnections; i++) {
                	 			//TODO: reconfigure for more than a 4x4 network and/or more connections
                	 			
                	 			z++;
                	 			connectionDir = -1;
                	 			oppDir = -1;
                	 			currentID = -1;
                	 			oppID = -1;
                	 			oppX = x;
                	 			oppY = y;
                	 			oppZ = z + 4;
                	 			
                	 			if(oppZ > nrItemInRing)
                	 				oppZ -= nrItemInRing;
 								
                	 			currentID  = RingCoord.getIDfromXYZ(x, y, z);
	 							
                    			
                    			#if DEBUG
                	 				Console.WriteLine("Current Coord ({0},{1},{2})", x, y, z);
                	 			#endif
                	 			
                	 			int switchNum = (z - 1) / 2;
                	 			switch (switchNum)
                	 			{
                	 				case 0: connectionDir = Simulator.DIR_UP;
                	 						oppDir = Simulator.DIR_DOWN;
                	 						oppY = y - 1;
                	 						
                	 						#if DEBUG
                	 							Console.WriteLine("0");
                	 						#endif
                	 						
                	 						if (oppY < 0)
                	 						{
                	 							oppY = Config.network_nrY / Config.ringHeight - 1;
                	 							
                	 							// ensure no duplication by handling a link at the lexicographically
                    							// first router
                    							if (oppX < x || (oppX == x && oppY < y)) continue;
                	 							
                	 							if (!Config.torus)
                	 							{
                	 								//Lower the link latency because this router is not being used
                	 								oppID = RingCoord.getIDfromXYZ(oppX, oppY, oppZ);
													int nextZ = ((z + 1) > nrItemInRing - 1) ? 0 : z+1;
													int prevZ = ((z - 1) < 0) ? nrItemInRing - 1 : z-1;
													int nextID = RingCoord.getIDfromXYZ(x, y, nextZ);
													int previousID = RingCoord.getIDfromXYZ(x, y, prevZ);
													
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, currentID {4}, previousID {5}", oppX, oppY, oppZ, nextID, currentID, previousID);
													#endif
													
													if (ringRouter[nextID].clockwise) {
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[currentID].linkIn[Simulator.DIR_CCW] = ringRouter[currentID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[currentID].linkIn[Simulator.DIR_CW] = ringRouter[currentID].linkOut[Simulator.DIR_CCW];
													}
													
													nextZ = ((oppZ + 1) > nrItemInRing - 1) ? 0 : oppZ+1;
													prevZ = ((oppZ - 1) < 0) ? nrItemInRing - 1 : oppZ-1;
													nextID = RingCoord.getIDfromXYZ(oppX, oppY, nextZ);
													previousID = RingCoord.getIDfromXYZ(oppX, oppY, prevZ);
															
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, oppID {4}, previousID {5}", oppX, oppY, oppZ, nextID, oppID, previousID);
													#endif
													
													if (ringRouter[nextID].clockwise) {													
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[oppID].linkIn[Simulator.DIR_CCW] = ringRouter[oppID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[oppID].linkIn[Simulator.DIR_CW] = ringRouter[oppID].linkOut[Simulator.DIR_CCW];
													}
													ringRouter[currentID] = null;
													ringRouter[oppID] = null;
       	 								    		continue;
                	 							}
                	 						}
                	 							
                	 						break;
                	 						
                	 				case 1: connectionDir = Simulator.DIR_RIGHT;
                	 						oppDir = Simulator.DIR_LEFT;
                	 						oppX = x + 1;
                	 						
                	 						#if DEBUG
                	 							Console.WriteLine("1");
                	 						#endif
                	 						
                	 						if (oppX >= Config.network_nrX / Config.ringWidth)
                	 						{
                	 							oppX = 0;
                	 							
                	 							// ensure no duplication by handling a link at the lexicographically
                    							// first router
                    							if (oppX < x || (oppX == x && oppY < y)) continue;
                    							
                	 							if (!Config.torus)
                	 							{
                	 								//Lower the link latency because this router is not being used
                	 								oppID = RingCoord.getIDfromXYZ(oppX, oppY, oppZ);
													int nextZ = ((z + 1) > nrItemInRing - 1) ? 0 : z+1;
													int prevZ = ((z - 1) < 0) ? nrItemInRing - 1 : z-1;
													int nextID = RingCoord.getIDfromXYZ(x, y, nextZ);
													int previousID = RingCoord.getIDfromXYZ(x, y, prevZ);
													
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, currentID {4}, previousID {5}", oppX, oppY, oppZ, nextID, currentID, previousID);
													#endif
													
													if (ringRouter[nextID].clockwise) {
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[currentID].linkIn[Simulator.DIR_CCW] = ringRouter[currentID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[currentID].linkIn[Simulator.DIR_CW] = ringRouter[currentID].linkOut[Simulator.DIR_CCW];
													}
													
													nextZ = ((oppZ + 1) > nrItemInRing - 1) ? 0 : oppZ+1;
													prevZ = ((oppZ - 1) < 0) ? nrItemInRing - 1 : oppZ-1;
													nextID = RingCoord.getIDfromXYZ(oppX, oppY, nextZ);
													previousID = RingCoord.getIDfromXYZ(oppX, oppY, prevZ);
													
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, oppID {4}, previousID {5}", oppX, oppY, oppZ, nextID, oppID, previousID);
													#endif
													
													if (ringRouter[nextID].clockwise) {
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[oppID].linkIn[Simulator.DIR_CCW] = ringRouter[oppID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[oppID].linkIn[Simulator.DIR_CW] = ringRouter[oppID].linkOut[Simulator.DIR_CCW];
													}
													ringRouter[currentID] = null;
													ringRouter[oppID] = null;
       	 								    		continue;
                	 							}
                	 						}
                	 							
                	 						break;	

                	 				case 2: connectionDir = Simulator.DIR_DOWN;
                	 						oppDir = Simulator.DIR_UP;
                	 						oppY = y + 1;
                	 						
                	 						#if DEBUG
                	 							Console.WriteLine("2");
                	 						#endif
                	 						
                	 						if (oppY >= Config.network_nrY / Config.ringHeight)
                	 						{
                	 							oppY = 0;
                	 							
                	 							// ensure no duplication by handling a link at the lexicographically
                    							// first router
                    							if (oppX < x || (oppX == x && oppY < y)) continue;
                	 							
                	 							if (!Config.torus)
                	 							{
                	 								//Lower the link latency because this router is not being used
                	 								oppID = RingCoord.getIDfromXYZ(oppX, oppY, oppZ);
													int nextZ = ((z + 1) > nrItemInRing - 1) ? 0 : z+1;
													int prevZ = ((z - 1) < 0) ? nrItemInRing - 1 : z-1;
													int nextID = RingCoord.getIDfromXYZ(x, y, nextZ);
													int previousID = RingCoord.getIDfromXYZ(x, y, prevZ);
													
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, currentID {4}, previousID {5}", oppX, oppY, oppZ, nextID, currentID, previousID);
													#endif
													
													if (ringRouter[nextID].clockwise) {
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[currentID].linkIn[Simulator.DIR_CCW] = ringRouter[currentID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[currentID].linkIn[Simulator.DIR_CW] = ringRouter[currentID].linkOut[Simulator.DIR_CCW];
													}
													
													nextZ = ((oppZ + 1) > nrItemInRing - 1) ? 0 : oppZ+1;
													prevZ = ((oppZ - 1) < 0) ? nrItemInRing - 1 : oppZ-1;
													nextID = RingCoord.getIDfromXYZ(oppX, oppY, nextZ);
													previousID = RingCoord.getIDfromXYZ(oppX, oppY, prevZ);
													
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, oppID {4}, previousID {5}", oppX, oppY, oppZ, nextID, oppID, previousID);
													#endif
													
													if (ringRouter[nextID].clockwise) {
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[oppID].linkIn[Simulator.DIR_CCW] = ringRouter[oppID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[oppID].linkIn[Simulator.DIR_CW] = ringRouter[oppID].linkOut[Simulator.DIR_CCW];
													}
													ringRouter[currentID] = null;
													ringRouter[oppID] = null;
       	 								    		continue;
                	 							}
                	 						}
                	 							
                	 						break;

                	 				case 3: connectionDir = Simulator.DIR_LEFT;
                	 				
                	 						#if DEBUG
                	 							Console.WriteLine("3");
                	 						#endif
                	 						
                	 						oppDir = Simulator.DIR_RIGHT;
                	 						oppX = x - 1;
                	 				        if (oppX < 0)
                	 						{
                	 							oppX = Config.network_nrX / Config.ringWidth - 1;
										
        										// ensure no duplication by handling a link at the lexicographically
                    							// first router
                    							if (oppX < x || (oppX == x && oppY < y)) continue;
                    							
                    							if (!Config.torus)
                	 							{
                	 								//Lower the link latency because this router is not being used
                	 								oppID = RingCoord.getIDfromXYZ(oppX, oppY, oppZ);
													int nextZ = ((z + 1) > nrItemInRing - 1) ? 0 : z+1;
													int prevZ = ((z - 1) < 0) ? nrItemInRing - 1 : z-1;
													int nextID = RingCoord.getIDfromXYZ(x, y, nextZ);
													int previousID = RingCoord.getIDfromXYZ(x, y, prevZ);
													
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, currentID {4}, previousID {5}", oppX, oppY, oppZ, nextID, currentID, previousID);
													#endif
													
													if (ringRouter[nextID].clockwise) {
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[currentID].linkIn[Simulator.DIR_CCW] = ringRouter[currentID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[currentID].linkIn[Simulator.DIR_CW] = ringRouter[currentID].linkOut[Simulator.DIR_CCW];
													}
													
													nextZ = ((oppZ + 1) > nrItemInRing - 1) ? 0 : oppZ+1;
													prevZ = ((oppZ - 1) < 0) ? nrItemInRing - 1 : oppZ-1;
													nextID = RingCoord.getIDfromXYZ(oppX, oppY, nextZ);
													previousID = RingCoord.getIDfromXYZ(oppX, oppY, prevZ);
													
													#if DEBUG
														Console.WriteLine("opp({0},{1},{2})\tnextID {3}, oppID {4}, previousID {5}", oppX, oppY, oppZ, nextID, oppID, previousID);
													#endif	
																
													if (ringRouter[nextID].clockwise) {
														ringRouter[nextID].linkIn[Simulator.DIR_CCW] = ringRouter[previousID].linkOut[Simulator.DIR_CW];
														ringRouter[oppID].linkIn[Simulator.DIR_CCW] = ringRouter[oppID].linkOut[Simulator.DIR_CW];
													}
													else {
														ringRouter[previousID].linkIn[Simulator.DIR_CW] = ringRouter[nextID].linkOut[Simulator.DIR_CCW];
														ringRouter[oppID].linkIn[Simulator.DIR_CW] = ringRouter[oppID].linkOut[Simulator.DIR_CCW];
													}
													ringRouter[currentID] = null;
													ringRouter[oppID] = null;
       	 								    		continue;
                	 							}
                	 						}
                	 						break;	
                	 						
                	 				default: throw new Exception("Ring too big for current system");
                	 			}
                	 			
								oppID = RingCoord.getIDfromXYZ(oppX, oppY, oppZ);
								
								// ensure no duplication by handling a link at the lexicographically
                    			// first router
                    			if (oppX < x || (oppX == x && oppY < y)) continue;													
								#if DEBUG
									Console.WriteLine("Creating normal link\topp({0},{1},{2})\toppID {3} dir {4} : oppdir {5}", oppX, oppY, oppZ, oppID, connectionDir, oppDir);
								#endif	
								
								Link linkA = new Link(Config.router.linkLatency - 1);
								Link linkB = new Link(Config.router.linkLatency - 1);
								links.Add(linkA);
								links.Add(linkB);
								
								#if DEBUG
                	 				Console.WriteLine("-------------------------------------------current{0} opp{1}", currentID, oppID);
								#endif
								
								((Connector)ringRouter[currentID]).connectionDirection    = connectionDir;
								((Connector)ringRouter[oppID]).connectionDirection        = oppDir;
								
								ringRouter[currentID].linkOut[connectionDir] = linkA;
								ringRouter[oppID].linkIn[oppDir]             = linkA;
								 
								ringRouter[currentID].linkIn[connectionDir]  = linkB;
								ringRouter[oppID].linkOut[oppDir]            = linkB;		
								
								if (nrPlaceholdersPerNetwork > 0) {
									((Connector)ringRouter[currentID]).injectPlaceholder = 2;
									nrPlaceholdersPerNetwork--;
								}
                	 		}
                	 	}
                	 			
                	}
                }
                #if DEBUG
				//Verification
				for (int y = 0; y < Config.network_nrY / Config.ringHeight; y++) {
					for (int x = 0; x < Config.network_nrX / Config.ringWidth; x++) {
						Console.WriteLine("");
						for (int z = 0; z < nrItemInRing; z++) {
							int tempID = RingCoord.getIDfromXYZ(x, y, z);
							if (ringRouter[tempID] == null) continue;
							if(z % 2 == 0) Console.WriteLine("Node with clockwise = {0}", ringRouter[tempID].clockwise);
							if(z % 2 == 1) Console.WriteLine("Connector with direction {0}", ((Connector)ringRouter[tempID]).connectionDirection);
							
							if (ringRouter[tempID].clockwise) {
								for (int dir = 5; dir >= 0; dir--) {
									if(ringRouter[tempID].linkIn[dir] != null)
										Console.WriteLine("{0} | ID:{1},  \tLinkIn  Direction {2}, \tLink: {3}\t|", ringRouter[tempID], tempID, dir, ringRouter[tempID].linkIn[dir]);
								}	
								for (int dir = 0; dir < 6; dir++) {
									if(ringRouter[tempID].linkOut[dir] != null)
										Console.WriteLine("{0} | ID:{1},  \tLinkOut Direction {2}, \tLink: {3}\t|", ringRouter[tempID], tempID, dir, ringRouter[tempID].linkOut[dir]);
								}
							}
							else {
								for (int dir = 5; dir >= 0; dir--) {
									if(ringRouter[tempID].linkOut[dir] != null)
										Console.WriteLine("{0} | ID:{1},  \tLinkOut Direction {2}, \tLink: {3}\t|", ringRouter[tempID], tempID, dir, ringRouter[tempID].linkOut[dir]);
								}
								for (int dir = 0; dir < 6; dir++) {
									if(ringRouter[tempID].linkIn[dir] != null)
										Console.WriteLine("{0} | ID:{1},  \tLinkIn  Direction {2}, \tLink: {3}\t|", ringRouter[tempID], tempID, dir, ringRouter[tempID].linkIn[dir]);
								}	
							}
						}
					}
				}
				#endif
                return;
            }

            /*
			if (Config.DisjointRings)
			{
				if (Config.SeperateConnect)
				{
					if (Config.network_nrX != Config.network_nrY)
						throw new Exception("Only works with square networks");
					
					// Designed for ringSize of 2
					// And square network
					// TODO: work on different dimensions
					
					int ringLength = Config.ringSize;
					int netXLength = Config.network_nrX;
					int netYLength = Config.network_nrY;
					
					int numRings = (netYLength / ringLength) * (netXLength / ringLength);
					int numRingNodes = (ringLength * ringLength);
					
					int dirOut    = 1;
					int dirIn     = 0;
					int dirInject = 2;
					int dirEject  = 3;
					
					Link A_B   = new Link(Config.router.linkLatency - 1);
					Link B_Z1A = new Link(Config.router.linkLatency - 1);
					Link Z1A_C = new Link(Config.router.linkLatency - 1); 
					Link C_Z4B = new Link(Config.router.linkLatency - 1);
					Link Z4B_D = new Link(Config.router.linkLatency - 1);
					Link D_A   = new Link(Config.router.linkLatency - 1);
					
					Link E_F   = new Link(Config.router.linkLatency - 1);
					Link F_G   = new Link(Config.router.linkLatency - 1);
					Link G_Z2A = new Link(Config.router.linkLatency - 1);
					Link Z2A_H = new Link(Config.router.linkLatency - 1);
					Link H_Z1B = new Link(Config.router.linkLatency - 1);
					Link Z1B_E = new Link(Config.router.linkLatency - 1);
					
					Link M_Z2B = new Link(Config.router.linkLatency - 1);
					Link Z2B_N = new Link(Config.router.linkLatency - 1);
					Link N_O   = new Link(Config.router.linkLatency - 1);
					Link O_P   = new Link(Config.router.linkLatency - 1);
					Link P_Z3A = new Link(Config.router.linkLatency - 1);
					Link Z3A_M = new Link(Config.router.linkLatency - 1);
					
					Link I_Z4A = new Link(Config.router.linkLatency - 1);
					Link Z4A_J = new Link(Config.router.linkLatency - 1);
					Link J_Z3B = new Link(Config.router.linkLatency - 1);
					Link Z3B_K = new Link(Config.router.linkLatency - 1);
					Link K_L   = new Link(Config.router.linkLatency - 1);
					Link L_I   = new Link(Config.router.linkLatency - 1);
					
					Link A_B1 = new Link(Config.router.linkLatency - 1);
					Link B_A1 = new Link(Config.router.linkLatency - 1);
					
					Link A_B2 = new Link(Config.router.linkLatency - 1);
					Link B_A2 = new Link(Config.router.linkLatency - 1);
					
					Link A_B3 = new Link(Config.router.linkLatency - 1);
					Link B_A3 = new Link(Config.router.linkLatency - 1);
					
					Link A_B4 = new Link(Config.router.linkLatency - 1);
					Link B_A4 = new Link(Config.router.linkLatency - 1);
					
					links.Add(A_B);
					links.Add(B_Z1A);
					links.Add(Z1A_C);
					links.Add(C_Z4B);
					links.Add(Z4B_D);
					links.Add(D_A);
					
					links.Add(E_F);
					links.Add(F_G); 
					links.Add(G_Z2A); 
					links.Add(Z2A_H);
					links.Add(H_Z1B);
					links.Add(Z1B_E);
					
					links.Add(M_Z2B);
					links.Add(Z2B_N);
					links.Add(N_O);
					links.Add(O_P);   
					links.Add(P_Z3A);
					links.Add(Z3A_M);
					
					links.Add(I_Z4A);
					links.Add(Z4A_J);
					links.Add(J_Z3B);
					links.Add(Z3B_K);
					links.Add(K_L);
					links.Add(L_I); 
					
					links.Add(A_B1);
					links.Add(B_A1);
					
					links.Add(A_B2);
					links.Add(B_A2);
					
					links.Add(A_B3);
					links.Add(B_A3); 
					
					links.Add(A_B4);
					links.Add(B_A4);
					
					int A = Coord.getIDfromXY(0, 0);
					int B = Coord.getIDfromXY(1, 0); 
					int C = Coord.getIDfromXY(1, 1);
					int D = Coord.getIDfromXY(0, 1);
					int E = Coord.getIDfromXY(2, 0);
					int F = Coord.getIDfromXY(3, 0);
					int G = Coord.getIDfromXY(3, 1);
					int H = Coord.getIDfromXY(2, 1);
					int I = Coord.getIDfromXY(0, 2);
					int J = Coord.getIDfromXY(1, 2);
					int K = Coord.getIDfromXY(1, 3);
					int L = Coord.getIDfromXY(0, 3);
					int M = Coord.getIDfromXY(2, 2);
					int N = Coord.getIDfromXY(3, 2);
					int O = Coord.getIDfromXY(3, 3);
					int P = Coord.getIDfromXY(2, 3);
					
					int Z1A = 0;
					int Z1B = 1;
					int Z2A = 2;
					int Z2B = 3;
					int Z3A = 4;
					int Z3B = 5;
					int Z4A = 6;
					int Z4B = 7;
					
					routers[A].linkIn[dirIn]   = D_A;
					routers[A].linkOut[dirOut] = A_B;
					routers[A].neigh[dirIn]    = routers[D];
					routers[A].neigh[dirOut]   = routers[B];
					routers[A].neighbors = 2;

					routers[B].linkIn[dirIn]   = A_B;
					routers[B].linkOut[dirOut] = B_Z1A;
					routers[B].neigh[dirIn]    = routers[A];
					routers[B].neigh[dirOut]   = connectors[Z1A];
					routers[B].neighbors = 2;

					routers[C].linkIn[dirIn]   = Z1A_C;
					routers[C].linkOut[dirOut] = C_Z4B;
					routers[C].neigh[dirIn]    = connectors[Z1A];
					routers[C].neigh[dirOut]   = connectors[Z4B];
					routers[C].neighbors = 2;	
					
					routers[D].linkIn[dirIn]   = Z4B_D;
					routers[D].linkOut[dirOut] = D_A;	
					routers[D].neigh[dirIn]    = connectors[Z4B];
					routers[D].neigh[dirOut]   = routers[A];
					routers[D].neighbors = 2;
				    
				    if (Config.sameDir)
				    {
					    routers[E].linkIn[dirIn]   = Z1B_E;
						routers[E].linkOut[dirOut] = E_F;
						routers[E].neigh[dirIn]    = connectors[Z1B];
						routers[E].neigh[dirOut]   = routers[F];
						routers[E].neighbors = 2;	
						
						routers[F].linkIn[dirIn]   = E_F;
						routers[F].linkOut[dirOut] = F_G;
						routers[F].neigh[dirIn]    = routers[E];
						routers[F].neigh[dirOut]   = routers[G];
						routers[F].neighbors = 2;	
						
						routers[G].linkIn[dirIn]   = F_G;
						routers[G].linkOut[dirOut] = G_Z2A;
						routers[G].neigh[dirIn]    = routers[F];
						routers[G].neigh[dirOut]   = connectors[Z2A];
						routers[G].neighbors = 2;	
						
						routers[H].linkIn[dirIn]   = Z2A_H;
						routers[H].linkOut[dirOut] = H_Z1B;
						routers[H].neigh[dirIn]    = connectors[Z2A];
						routers[H].neigh[dirOut]   = connectors[Z1B];
						routers[H].neighbors = 2;	
						
						routers[I].linkIn[dirIn]   = L_I;
						routers[I].linkOut[dirOut] = I_Z4A;
						routers[I].neigh[dirIn]    = routers[L];
						routers[I].neigh[dirOut]   = connectors[Z4A];
						routers[I].neighbors = 2;	
						
						routers[J].linkIn[dirIn]   = Z4A_J;
						routers[J].linkOut[dirOut] = J_Z3B;
						routers[J].neigh[dirIn]    = connectors[Z4A];
						routers[J].neigh[dirOut]   = connectors[Z3B];
						routers[J].neighbors = 2;	

						routers[K].linkIn[dirIn]   = Z3B_K;
						routers[K].linkOut[dirOut] = K_L;
						routers[K].neigh[dirIn]    = connectors[Z3B];
						routers[K].neigh[dirOut]   = routers[L];
						routers[K].neighbors = 2;	
						
						routers[L].linkIn[dirIn]   = K_L;
						routers[L].linkOut[dirOut] = L_I;
						routers[L].neigh[dirIn]    = routers[K];
						routers[L].neigh[dirOut]   = routers[I];
						routers[L].neighbors = 2;
					}
					else
					{
						routers[E].linkIn[dirIn]   = E_F;
						routers[E].linkOut[dirOut] = Z1B_E;	
						routers[E].neigh[dirIn]    = routers[F]; 
						routers[E].neigh[dirOut]   = connectors[Z1B];
						routers[E].neighbors = 2;	
						
						routers[F].linkIn[dirIn]   = F_G;
						routers[F].linkOut[dirOut] = E_F;
						routers[F].neigh[dirIn]    = routers[G];
						routers[F].neigh[dirOut]   = routers[E];
						routers[F].neighbors = 2;	
						
						routers[G].linkIn[dirIn]   = G_Z2A;
						routers[G].linkOut[dirOut] = F_G;	
						routers[G].neigh[dirIn]    = connectors[Z2A];
						routers[G].neigh[dirOut]   = routers[F];
						routers[G].neighbors = 2;	
						
						routers[H].linkIn[dirIn]   = H_Z1B;
						routers[H].linkOut[dirOut] = Z2A_H;	
						routers[H].neigh[dirIn]    = connectors[Z1B];
						routers[H].neigh[dirOut]   = connectors[Z2A];
						routers[H].neighbors = 2;	
						
						routers[I].linkIn[dirIn]   = I_Z4A;
						routers[I].linkOut[dirOut] = L_I;	
						routers[I].neigh[dirIn]    = connectors[Z4A];
						routers[I].neigh[dirOut]   = routers[L];
						routers[I].neighbors = 2;
						
						routers[J].linkIn[dirIn]   = J_Z3B;
						routers[J].linkOut[dirOut] = Z4A_J;
						routers[J].neigh[dirIn]    = connectors[Z3B];
						routers[J].neigh[dirOut]   = connectors[Z4A];
						routers[J].neighbors = 2;		

						routers[K].linkIn[dirIn]   = K_L;
						routers[K].linkOut[dirOut] = Z3B_K;	
						routers[K].neigh[dirIn]    = routers[L];
						routers[K].neigh[dirOut]   = connectors[Z3B];
						routers[K].neighbors = 2;
						
						routers[L].linkIn[dirIn]   = L_I;
						routers[L].linkOut[dirOut] = K_L;	
						routers[L].neigh[dirIn]    = routers[I]; 
						routers[L].neigh[dirOut]   = routers[K];
						routers[L].neighbors = 2;
					}
					
					routers[M].linkIn[dirIn]   = Z3A_M;
					routers[M].linkOut[dirOut] = M_Z2B;	
					routers[M].neigh[dirIn]    = connectors[Z3A];
					routers[M].neigh[dirOut]   = connectors[Z2B];
					routers[M].neighbors = 2;
					
					
					routers[N].linkIn[dirIn]   = Z2B_N;
					routers[N].linkOut[dirOut] = N_O;	
					routers[N].neigh[dirIn]    = connectors[Z2B];
					routers[N].neigh[dirOut]   = routers[O];
					routers[N].neighbors = 2;
					
					routers[O].linkIn[dirIn]   = N_O;
					routers[O].linkOut[dirOut] = O_P;	
					routers[O].neigh[dirIn]    = routers[N];
					routers[O].neigh[dirOut]   = routers[P];
					routers[O].neighbors = 2;	
					
					routers[P].linkIn[dirIn]   = O_P;
					routers[P].linkOut[dirOut] = P_Z3A;	
					routers[P].neigh[dirIn]    = routers[O];
					routers[P].neigh[dirOut]   = connectors[Z3A];
					routers[P].neighbors = 2;	
					
					// Connectors
					connectors[Z1A].linkIn[dirIn]     = B_Z1A;
					connectors[Z1A].linkOut[dirOut]   = Z1A_C;
					connectors[Z1A].neigh[dirIn]      = routers[B];
					connectors[Z1A].neigh[dirOut]     = routers[C];
					connectors[Z1A].neighbors        += 2;
					
					connectors[Z2B].linkIn[dirIn]     = M_Z2B;
					connectors[Z2B].linkOut[dirOut]   = Z2B_N;
					connectors[Z2B].neigh[dirIn]      = routers[M];
					connectors[Z2B].neigh[dirOut]     = routers[N];
					connectors[Z2B].neighbors        += 2;
					
					connectors[Z3A].linkIn[dirIn]     = P_Z3A;
					connectors[Z3A].linkOut[dirOut]   = Z3A_M;
					connectors[Z3A].neigh[dirIn]      = routers[P];
					connectors[Z3A].neigh[dirOut]     = routers[M];
					connectors[Z3A].neighbors        += 2;	
						
					connectors[Z4B].linkIn[dirIn]     = C_Z4B;
					connectors[Z4B].linkOut[dirOut]   = Z4B_D;
					connectors[Z4B].neigh[dirIn]      = routers[C];
					connectors[Z4B].neigh[dirOut]     = routers[D];
					connectors[Z4B].neighbors        += 2;	
					
					if (Config.sameDir)
					{
						connectors[Z1B].linkIn[dirIn]     = H_Z1B;
						connectors[Z1B].linkOut[dirOut]   = Z1B_E;
						connectors[Z1B].neigh[dirIn]      = routers[H];
						connectors[Z1B].neigh[dirOut]     = routers[E];
						connectors[Z1B].neighbors        += 2;	
					
						connectors[Z2A].linkIn[dirIn]     = G_Z2A;
						connectors[Z2A].linkOut[dirOut]   = Z2A_H;
						connectors[Z2A].neigh[dirIn]      = routers[G];
						connectors[Z2A].neigh[dirOut]     = routers[H];
						connectors[Z2A].neighbors        += 2;	
						
						connectors[Z4A].linkIn[dirIn]     = I_Z4A;
						connectors[Z4A].linkOut[dirOut]   = Z4A_J;
						connectors[Z4A].neigh[dirIn]      = routers[I];
						connectors[Z4A].neigh[dirOut]     = routers[J];
						connectors[Z4A].neighbors        += 2;	
					
						connectors[Z3B].linkIn[dirIn]     = J_Z3B;
						connectors[Z3B].linkOut[dirOut]   = Z3B_K;	
						connectors[Z3B].neigh[dirIn]      = routers[J];
						connectors[Z3B].neigh[dirOut]     = routers[K];
						connectors[Z3B].neighbors        += 2;
					}
					else
					{
						connectors[Z1B].linkIn[dirIn]     = Z1B_E;
						connectors[Z1B].linkOut[dirOut]   = H_Z1B;
						connectors[Z1B].neigh[dirIn]      = routers[E];
						connectors[Z1B].neigh[dirOut]     = routers[H];
						connectors[Z1B].neighbors        += 2;		
					
						connectors[Z2A].linkIn[dirIn]     = Z2A_H;
						connectors[Z2A].linkOut[dirOut]   = G_Z2A;
						connectors[Z2A].neigh[dirIn]      = routers[H];
						connectors[Z2A].neigh[dirOut]     = routers[G];
						connectors[Z2A].neighbors        += 2;		
					
						connectors[Z4A].linkIn[dirIn]     = Z4A_J;
						connectors[Z4A].linkOut[dirOut]   = I_Z4A;
						connectors[Z4A].neigh[dirIn]      = routers[J];
						connectors[Z4A].neigh[dirOut]     = routers[I];
						connectors[Z4A].neighbors        += 2;		
					
						connectors[Z3B].linkIn[dirIn]     = Z3B_K;
						connectors[Z3B].linkOut[dirOut]   = J_Z3B;	
						connectors[Z3B].neigh[dirIn]      = routers[K];
						connectors[Z3B].neigh[dirOut]     = routers[J];
						connectors[Z3B].neighbors        += 2;	
					}
					
					connectors[Z1A].linkIn[dirInject] = B_A1;
					connectors[Z1A].linkOut[dirEject] = A_B1;
					connectors[Z1A].neigh[dirIn]      = connectors[Z1B];
					connectors[Z1A].neigh[dirOut]     = connectors[Z1B];
					connectors[Z1A].neighbors        += 2;	
						
					connectors[Z1B].linkIn[dirInject] = A_B1;
					connectors[Z1B].linkOut[dirEject] = B_A1;	
					connectors[Z1B].neigh[dirIn]      = connectors[Z1A];
					connectors[Z1B].neigh[dirOut]     = connectors[Z1A];
					connectors[Z1B].neighbors        += 2;	
					
					connectors[Z2A].linkIn[dirInject] = B_A2;
					connectors[Z2A].linkOut[dirEject] = A_B2;	
					connectors[Z2A].neigh[dirIn]      = connectors[Z2B];
					connectors[Z2A].neigh[dirOut]     = connectors[Z2B];
					connectors[Z2A].neighbors        += 2;	
					
					connectors[Z2B].linkIn[dirInject] = A_B2;
					connectors[Z2B].linkOut[dirEject] = B_A2;
					connectors[Z2B].neigh[dirIn]      = connectors[Z2A];
					connectors[Z2B].neigh[dirOut]     = connectors[Z2A];
					connectors[Z2B].neighbors        += 2;		
					
					connectors[Z3A].linkIn[dirInject] = B_A3;
					connectors[Z3A].linkOut[dirEject] = A_B3;
					connectors[Z3A].neigh[dirIn]      = connectors[Z3B];
					connectors[Z3A].neigh[dirOut]     = connectors[Z3B];
					connectors[Z3A].neighbors        += 2;	
						
					connectors[Z3B].linkIn[dirInject] = A_B3;
					connectors[Z3B].linkOut[dirEject] = B_A3;	
					connectors[Z3B].neigh[dirIn]      = connectors[Z3A];
					connectors[Z3B].neigh[dirOut]     = connectors[Z3A];
					connectors[Z3B].neighbors        += 2;		
					
					connectors[Z4A].linkIn[dirInject] = B_A4;
					connectors[Z4A].linkOut[dirEject] = A_B4;
					connectors[Z4A].neigh[dirIn]      = connectors[Z4B];
					connectors[Z4A].neigh[dirOut]     = connectors[Z4B];
					connectors[Z4A].neighbors        += 2;			
					
					connectors[Z4B].linkIn[dirInject] = A_B4;
					connectors[Z4B].linkOut[dirEject] = B_A4;
					connectors[Z4B].neigh[dirIn]      = connectors[Z4A];
					connectors[Z4B].neigh[dirOut]     = connectors[Z4A];
					connectors[Z4B].neighbors        += 2;		
						
					return;
				}
			}
			
			/*
			if (Config.StreetRings)
			{
				// connect the network with Links
	            for (int n = 0; n < Config.N; n++)
	            {
	                int x, y;
	                Coord.getXYfromID(n, out x, out y);
	
	                // inter-router links
	                for (int dir = 0; dir < 2; dir++)
	                {
	                    int oppDir = (dir + 2) % 4; // direction from neighbor's perspective
	
	                    // determine neighbor's coordinates
	                    int x_, y_;
	                    switch (x % 2)
	                    {
		                    case 0:
				            	switch (dir)
				                {
				                    case 0:    x_ = x;     y_ = y + 1; break;
				                    case 1:    x_ = x;     y_ = y - 1; break;
				                    default: continue;
				                }
				                break;
		                    
		                    case 1:
			                    switch (dir)
			                    {
			                        case 0:    x_ = x;     y_ = y - 1; break;
			                        case 1:    x_ = x;     y_ = y + 1; break;
			                        default: continue;
			                    }
		                    break;
						}
				
	
	                    // ensure no duplication by handling a link at the lexicographically
	                    // first router
	                    if (x_ < x || (x_ == x && y_ < y)) continue;
	
	                    // Link param is *extra* latency (over the standard 1 cycle)
	                    Link dirA = new Link(Config.router.linkLatency - 1);
	                    Link dirB = new Link(Config.router.linkLatency - 1);
	                    links.Add(dirA);
	                    links.Add(dirB);
	
	                    // link 'em up
	                    routers[Coord.getIDfromXY(x,  y)].linkOut[dir]    = dirA;
	                    routers[Coord.getIDfromXY(x_, y_)].linkIn[oppDir] = dirA;
	
	                    routers[Coord.getIDfromXY(x,  y)].linkIn[dir]      = dirB;
	                    routers[Coord.getIDfromXY(x_, y_)].linkOut[oppDir] = dirB;
	
	                    routers[Coord.getIDfromXY(x,  y)].neighbors++;
	                    routers[Coord.getIDfromXY(x_, y_)].neighbors++;
	
	                    routers[Coord.getIDfromXY(x,  y)].neigh[dir]     = routers[Coord.getIDfromXY(x_, y_)];
	                    routers[Coord.getIDfromXY(x_, y_)].neigh[oppDir] = routers[Coord.getIDfromXY(x,  y)];
	
	                }
	            }
			}
			*/

            // connect the network with Links
            for (int n = 0; n < Config.N; n++)
            {
                int x, y;
                Coord.getXYfromID(n, out x, out y);
				int ID = n;
                // inter-router links
                for (int dir = 0; dir < 4; dir++)
                {
                    int oppDir = (dir + 2) % 4; // direction from neighbor's perspective

                    // determine neighbor's coordinates
                    int x_, y_;
                    switch (dir)
                    {
                        case Simulator.DIR_UP:    x_ = x;     y_ = y + 1; break;
                        case Simulator.DIR_DOWN:  x_ = x;     y_ = y - 1; break;
                        case Simulator.DIR_RIGHT: x_ = x + 1; y_ = y;     break;
                        case Simulator.DIR_LEFT:  x_ = x - 1; y_ = y;     break;
                        default: continue;
                    }

                    // If we are a torus, we manipulate x_ and y_
                    if(Config.torus)
                    {
                      if(x_ < 0)
                        x_ += X;
                      else if(x_ >= X)
                        x_ -= X;

                      if(y_ < 0)
                        y_ += Y;
                      else if(y_ >= Y)
                        y_ -= Y;
                    }
                    // mesh, not torus: detect edge
                    else if (x_ < 0 || x_ >= X || y_ < 0 || y_ >= Y)
                    {
                        if (Config.edge_loop)
                        {
                            Link edgeL = new Link(Config.router.linkLatency - 1);
                            links.Add(edgeL);
                            routers[ID].linkOut[dir] = edgeL;
                            routers[ID].linkIn[dir]  = edgeL;
                            routers[ID].neighbors++;
                            routers[ID].neigh[dir] =
                                routers[ID];
                        }

                        continue;
                    }

                    // ensure no duplication by handling a link at the lexicographically
                    // first router
                    if (x_ < x || (x_ == x && y_ < y)) continue;

                    // Link param is *extra* latency (over the standard 1 cycle)
                    Link dirA = new Link(Config.router.linkLatency - 1);
                    Link dirB = new Link(Config.router.linkLatency - 1);
                    links.Add(dirA);
                    links.Add(dirB);

					int oppID = Coord.getIDfromXY(x_, y_);

                    // link 'em up
                    routers[ID].linkOut[dir]    = dirA;
                    routers[oppID].linkIn[oppDir] = dirA;

                    routers[ID].linkIn[dir]      = dirB;
                    routers[oppID].linkOut[oppDir] = dirB;

                    routers[ID].neighbors++;
                    routers[oppID].neighbors++;

                    routers[ID].neigh[dir]     = routers[oppID];
                    routers[oppID].neigh[oppDir] = routers[ID];


                    if (Config.router.algorithm == RouterAlgorithm.DR_SCARAB)
                    {
                        for (int wireNr = 0; wireNr < Config.nack_nr; wireNr++)
                        {
                            Link nackA = new Link(Config.nack_linkLatency - 1);
                            Link nackB = new Link(Config.nack_linkLatency - 1);
                            links.Add(nackA);
                            links.Add(nackB);
                            ((Router_SCARAB)routers[Coord.getIDfromXY(x,  y)]).nackOut[dir    * Config.nack_nr + wireNr] = nackA;
                            ((Router_SCARAB)routers[Coord.getIDfromXY(x_, y_)]).nackIn[oppDir * Config.nack_nr + wireNr] = nackA;

                            ((Router_SCARAB)routers[Coord.getIDfromXY(x,  y)]).nackIn[dir      * Config.nack_nr + wireNr] = nackB;
                            ((Router_SCARAB)routers[Coord.getIDfromXY(x_, y_)]).nackOut[oppDir * Config.nack_nr + wireNr] = nackB;
                        }
                    }
                }
            }

            if (Config.torus)
                for (int i = 0; i < Config.N; i++)
                    if (routers[i].neighbors < 4)
                        throw new Exception("torus construction not successful!");
        }

        public void doStep()
        {
            doStats();

            // step the golden controller
            golden.doStep();

            // step the nodes
            for (int n = 0; n < Config.N; n++)
                nodes[n].doStep();

			if (Config.RingRouter)
            {
            	for(int n = 0; n < Config.N * (1 + Config.nrConnections); n++)
            		if(ringRouter[n] != null) 
            			ringRouter[n].doStep();
            }
            else
            {
            	// step the network sim: first, routers
            	for (int n = 0; n < Config.N; n++)
                	routers[n].doStep();
			}
            // now, step each link
            foreach (Link l in links)
                l.doStep();
        }

        void doStats()
        {
            int used_links = 0;
            foreach (Link l in links)
                if (l.Out != null)
                    used_links++;

            this._cycle_netutil = (double)used_links / links.Count;

            Simulator.stats.netutil.Add((double)used_links / links.Count);

            this._cycle_insns = 0; // CPUs increment this each cycle -- we want a per-cycle sum
        }

        public bool isFinished()
        {
            switch (m_finish)
            {
                case FinishMode.app:
                    int count = 0;
                    for (int i = 0; i < Config.N; i++)
                        if (nodes[i].Finished) count++;

                    return count == Config.N;

                case FinishMode.cycle:
                    return Simulator.CurrentRound >= m_finishCount;

                case FinishMode.barrier:
                    return Simulator.CurrentBarrier >= (ulong)Config.barrier;
            }

            throw new Exception("unknown finish mode");
        }

        public bool isLivelocked()
        {
            for (int i = 0; i < Config.N; i++)
                if (nodes[i].Livelocked) return true;
            return false;
        }

        void ParseFinish(string finish)
        {
            // finish is "app", "insn <n>", "synth <n>", or "barrier <n>"

            string[] parts = finish.Split(' ', '=');
            if (parts[0] == "app")
                m_finish = FinishMode.app;
            else if (parts[0] == "cycle")
                m_finish = FinishMode.cycle;
            else if (parts[0] == "barrier")
                m_finish = FinishMode.barrier;
            else
                throw new Exception("unknown finish mode");

            if (m_finish == FinishMode.app || m_finish == FinishMode.barrier)
                m_finishCount = 0;
            else
                m_finishCount = UInt64.Parse(parts[1]);
        }


        public void close()
        {
            for (int n = 0; n < Config.N; n++)
                routers[n].close();
        }

        public void visitFlits(Flit.Visitor fv)
        {
            foreach (Link l in links)
                l.visitFlits(fv);
            foreach (Router r in routers)
                r.visitFlits(fv);
            foreach (Node n in nodes)
                n.visitFlits(fv);
        }

        public delegate Flit FlitInjector();

        public int injectFlits(int count, FlitInjector fi)
        {
            int i = 0;
            for (; i < count; i++)
            {
                bool found = false;
                foreach (Router r in routers)
                    if (r.canInjectFlit(null))
                    {
                        r.InjectFlit(fi());
                        found = true;
                        break;
                    }

                if (!found)
                    return i;
            }

            return i;
        }

		public static RingRouter makeConnection(RingCoord rc)
		{
			switch (Config.router.connectionalgorithm)
			{
				case ConnectorAlgorithm.DIRECT_LINK:
					return new Connector(rc, false);
                //case ConnectorAlgorithm.RING_LINK:
				//	return new Connector_Ring(rc);
                default: throw new Exception("Invalid connector");
			}
		}
		
		public static RingRouter makeNodeRouter(RingCoord rc)
		{
			return new RingRouter_Simple(rc, true);
		}
		
        public static Router MakeRouter(Coord c)
        {
            switch (Config.router.algorithm)
            {
                case RouterAlgorithm.DR_AFC:
                    return new Router_AFC(c);

                case RouterAlgorithm.DR_FLIT_SWITCHED_CTLR:
                    return new Router_Flit_Ctlr(c);

                case RouterAlgorithm.DR_FLIT_SWITCHED_OLDEST_FIRST:
                    return new Router_Flit_OldestFirst(c);

                case RouterAlgorithm.DR_SCARAB:
                    return new Router_SCARAB(c);

                case RouterAlgorithm.DR_FLIT_SWITCHED_GP:
                    return new Router_Flit_GP(c);

                case RouterAlgorithm.DR_FLIT_SWITCHED_CALF:
                    return new Router_SortNet_GP(c);

                case RouterAlgorithm.DR_FLIT_SWITCHED_CALF_OF:
                    return new Router_SortNet_OldestFirst(c);

                case RouterAlgorithm.DR_FLIT_SWITCHED_RANDOM:
                    return new Router_Flit_Random(c);

                case RouterAlgorithm.ROUTER_FLIT_EXHAUSTIVE:
                    return new Router_Flit_Exhaustive(c);

                case RouterAlgorithm.OLDEST_FIRST_DO_ROUTER:
                    return new OldestFirstDORouter(c);

                case RouterAlgorithm.ROUND_ROBIN_DO_ROUTER:
                    return new RoundRobinDORouter(c);

                case RouterAlgorithm.STC_DO_ROUTER:
                    return new STC_DORouter(c);
                
                case RouterAlgorithm.NEW_OF:
                    return new Router_New_OldestFirst(c);
                case RouterAlgorithm.NEW_CF:
                    return new Router_New_ClosestFirst(c);
                case RouterAlgorithm.NEW_GP:
                    return new Router_New_GP(c);

                case RouterAlgorithm.ROR_RANDOM:
                    return new Router_RoR_Random(c);

                case RouterAlgorithm.ROR_OLDEST_FIRST:
                    return new Router_RoR_OldestFirst(c);
                
                case RouterAlgorithm.ROR_CLOSEST_FIRST:
                	return new Router_RoR_ClosestFirst(c);
                	
               	case RouterAlgorithm.ROR_GP:
                	return new Router_RoR_GP(c);
                
                //case RouterAlgorithm.RINGROUTER_SIMPLE:
                //	return new RingRouter_Simple(ingCoord.getRingIDfromID(c.ID));
                
                default:
                    throw new Exception("invalid routing algorithm " + Config.router.algorithm);
            }
        }
    }
}
