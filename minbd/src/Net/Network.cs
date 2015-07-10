//#define TEST
//#define DEBUG

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

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

        public int faultCount = 0;
        public FinishMode finishMode { get { return m_finish; } }

        public double _cycle_netutil; // HACK -- controllers can use this.
        public ulong  _cycle_insns;   // HACK -- controllers can use this.

        Random faultRandomizer;
        public Network(int dimX, int dimY)
        {
            X = dimX;
            Y = dimY;
        }

        private void ringSetup()
        {
            	int nrPlaceholdersPerNetwork = Config.nrPlaceholders / 2;
                int nrNodesPerRing = (Config.ringWidth * 2 + Config.ringHeight * 2 - 4);
                int nrConnectPerRing = nrNodesPerRing * Config.nrConnections;
                int nrItemInRing = nrConnectPerRing + nrNodesPerRing;
                
                #if DEBUG_
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
                            
                            #if DEBUG_
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
                            	
                            	#if DEBUG_
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
                                
                                #if DEBUG_
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
                        
                        #if DEBUG_
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
				
				#if DEBUG_
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
	 							
                    			
                    			#if DEBUG_
                	 				Console.WriteLine("Current Coord ({0},{1},{2})", x, y, z);
                	 			#endif
                	 			
                	 			int switchNum = (z - 1) / 2;
                	 			switch (switchNum)
                	 			{
                	 				case 0: connectionDir = Simulator.DIR_UP;
                	 						oppDir = Simulator.DIR_DOWN;
                	 						oppY = y - 1;
                	 						
                	 						#if DEBUG_
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
													
													#if DEBUG_
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
															
													#if DEBUG_
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
                	 						
                	 						#if DEBUG_
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
													
													#if DEBUG_
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
													
													#if DEBUG_
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
                	 						
                	 						#if DEBUG_
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
													
													#if DEBUG_
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
													
													#if DEBUG_
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
                	 				
                	 						#if DEBUG_
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
													
													#if DEBUG_
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
													
													#if DEBUG_
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
								#if DEBUG_
									Console.WriteLine("Creating normal link\topp({0},{1},{2})\toppID {3} dir {4} : oppdir {5}", oppX, oppY, oppZ, oppID, connectionDir, oppDir);
								#endif	
								
								Link linkA = new Link(Config.router.linkLatency - 1);
								Link linkB = new Link(Config.router.linkLatency - 1);
								links.Add(linkA);
								links.Add(linkB);
								
								#if DEBUG_
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
                #if DEBUG_
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

            if (Config.synthGen)
                mapping = new SynthNodeMapping();
            else
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
            	ringSetup();
                return;
            }
			
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

                    dirA.outCoord = routers[ID].coord;
                    dirA.dir = dir;
                    dirB.outCoord = routers[oppID].coord;
                    dirB.dir = oppDir;


                    routers[ID].neighbors++;
                    routers[oppID].neighbors++;
                    routers[ID].healthyNeighbors = routers[ID].neighbors;
                    routers[oppID].healthyNeighbors = routers[ID].neighbors;

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

            //Initializing the fault configuration of the system
            faultRandomizer = new Random(Config.rand_seed);
            if (Config.fault_initialCount != 0)
            {
                while (faultCount < Config.fault_initialCount)
                    injectNewFault();
            }
        }

        public void checkReachables()
        {
            for (int n = 0; n < Config.N; n++)
            {
                Router node = routers[n];
                node.visited = false;
                for (int k = 0; k < Config.N; k++)
                {
                    node.canReach[k] = false;
                }
            }

            Router root = routers[0]; //This chooses root
            while (root != null)
            {
                BFS(root);
                root = selectRoot();
            }

            for (int n = 0; n < Config.N; n++)
                routers[n].visited = false;
        }

        Router selectRoot()
        {
            for (int i = 0; i < Config.N; i++)
                if (routers[i].visited == false)
                    return routers[i];
            return null;
        }

        void BFS(Router startNode)
        {
            List<Router> visitedQ = new List<Router>();
            List<Router> queue = new List<Router>();

            startNode.visited = true;
            queue.Add(startNode);
            visitedQ.Add(startNode);

            while (queue.Count != 0)
            {
                Router s = queue.First();
                queue.RemoveAt(0);

                for (int i = 0; i < 4; i++)
                {
                    if (s.linkOut[i] == null || !s.linkOut[i].healthy)
                        continue;

                    Router child = s.neigh[i];

                    if (!child.visited)
                    {
                        child.visited = true;
                        queue.Add(child);
                        visitedQ.Add(child);
                    }
                }
            }

            foreach(Router src in visitedQ)
                foreach (Router dst in visitedQ)
                {
                    src.canReach[dst.ID] = true;
                }

        }

        public void breakNode(int n)
        {
            for (int i = 0; i < 4; i++)
                if (routers[n].linkOut[i] != null)
                    breakLink(n, i);
            nodes[n].healthy = false;
        }

        public void breakLink(int n, int dir)
        {
            routers[n].linkOut[dir].goFaulty();
            routers[n].linkIn[dir].goFaulty();
            routers[n].healthyNeighbors--;
            routers[n].neighbor(dir).healthyNeighbors--;
        }
        

        public void injectNewFault()
        {
            int n, dir;
            if (Config.fault_type == Config.faultType.node)
            {
                do
                {
                    n = faultRandomizer.Next(Config.N);
                } while (nodes[n].healthy == false);

                breakNode(n);
            }
            else
            {
                do
                {
                    n = faultRandomizer.Next(Config.N);
                    dir = faultRandomizer.Next(2);
                } while (routers[n].linkOut[dir] == null || routers[n].linkOut[dir].healthy == false);

                breakLink(n, dir);
            }
            faultCount++;
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

        public bool LiveLockedRouters()
        {
            for (int i = 0; i < Config.N; i++)
                if (nodes[i].router.hasLiveLock())
                    return true;
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
                case RouterAlgorithm.Maze_Router:
                    return new Router_Maze(c);

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
