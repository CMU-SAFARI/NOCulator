//#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class HR_Network : Network
    {
        public HR_Network(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }

        public override void setup()
        {
            nodeRouters = new Router_Node[Config.N];
            switchRouters = new Router_Switch[Config.N]; 
            nodes = new Node[Config.N];
            links = new List<Link>();
            cache = new CmpCache();

            ParseFinish(Config.finish);

            workload = new Workload(Config.traceFilenames);

            mapping = new NodeMapping_AllCPU_SharedCache();

            // create routers and nodes
            for (int n = 0; n < Config.N; n++)
           {
                Coord c = new Coord(n);
                RC_Coord RC_c = new RC_Coord(n);

                nodes[n] = new Node(mapping, c);
                nodeRouters[n] = new Router_Node(RC_c, c);
                nodes[n].setRouter(nodeRouters[n]);
                nodeRouters[n].setNode(nodes[n]);
            }
            for (int n = 0; n < Config.N; n++)
            	switchRouters[n] = new Router_Switch(n);

            if (Config.RouterEvaluation)
                return;

            // connect the network with Links
            for (int y = 0; y < Y / 2; y++)
            {
            	for (int x = 0; x < X / 2; x++)
            	{
            		for (int z = 0; z < 4; z++)
            		{
            			Link dirA = new Link(Config.router.switchLinkLatency - 1);
						Link dirB = new Link(Config.router.switchLinkLatency - 1);
						Link dirC = new Link(Config.router.switchLinkLatency - 1);
						Link dirD = new Link(Config.router.switchLinkLatency - 1);
						links.Add(dirA);
						links.Add(dirB);
						links.Add(dirC);
						links.Add(dirD);
						int next = (RC_Coord.getIDfromXYZ(x, y, z)+1) % 4 + RC_Coord.getIDfromXYZ(x, y, z) / 4 * 4;
						//Console.WriteLine("Net/HR_Network. ID:{0}", RC_Coord.getIDfromXYZ(x, y, z));
						nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkOut[CW] = dirA;
						switchRouters[next].linkIn[Local_CW] = dirA;
						nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkIn[CCW] = dirB;
						switchRouters[next].linkOut[Local_CCW] = dirB;

						nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkOut[CCW] = dirC;
						switchRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkIn[Local_CCW] = dirC;
						nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkIn[CW] = dirD;
						switchRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkOut[Local_CW] = dirD;
            		}
            	}
            }
			int [] xRingIndex = {1, 3, 9, 11, 15, 13, 7, 5};
			int [] yRingIndex = {0, 8, 10, 12, 14, 6, 4, 2};
			for (int n = 0; n < 8; n++)
			{
				Link dirA = new Link(Config.router.level1RingLinkLatency- 1);				
				Link dirB = new Link(Config.router.level1RingLinkLatency- 1);
				switchRouters[xRingIndex[n]].linkOut[GL_CCW] = dirA;
				switchRouters[xRingIndex[(n+1) % 8]].linkIn[GL_CCW] = dirA;
				switchRouters[xRingIndex[n]].linkIn[GL_CW] = dirB;
				switchRouters[xRingIndex[(n+1) % 8]].linkOut[GL_CW] = dirB;
			
				Link dirC = new Link(Config.router.level1RingLinkLatency- 1);				
				Link dirD = new Link(Config.router.level1RingLinkLatency- 1);
				switchRouters[yRingIndex[n]].linkOut[GL_CCW] = dirC;
				switchRouters[yRingIndex[(n+1) % 8]].linkIn[GL_CCW] = dirC;
				switchRouters[yRingIndex[n]].linkIn[GL_CW] = dirD;
				switchRouters[yRingIndex[(n+1) % 8]].linkOut[GL_CW] = dirD;
				links.Add(dirA);
				links.Add(dirB);
				links.Add(dirC);
				links.Add(dirD);
			}
        }

       	
        public override void doStep()
        {
        
            doStats();
		//	printFlits();
		//	Console.ReadKey(true);

            for (int n = 0; n < Config.N; n++)
                nodes[n].doStep();
            // step the network sim: first, routers

           	for (int n = 0; n < Config.N; n++)
           		nodeRouters[n].doStep();
           	for (int n = 0; n < Config.N; n++)
           		switchRouters[n].doStep();
            // now, step each link
            foreach (Link l in links)
                l.doStep();
        }
		public override void close()
		{
			for (int n = 0; n < Config.N; n++)
				nodeRouters[n].close();
			for (int n = 0; n < Config.N; n++)
				switchRouters[n].close();
		}

		void printFlits()
		{
			for (int i = 0; i < Config.N; i++)
			{
				int from , to;
				Flit f = nodeRouters[i].linkIn[CW].Out;
				if (f == null)  {from = -1; to = -1;}
				else {from = f.packet.src.ID; to = f.packet.dest.ID;}
				Console.WriteLine("nodeID:{0} from {1} to {2}", i, from, to);
			}
			for (int i = 0; i < Config.N; i++)
			{
				for (int dir = 2; dir <= 3; dir++)
				{
					int from, to;
					Flit f = switchRouters[i].linkIn[dir].Out;
					if (f==null) {from = -1; to = -1;}
					else {from = f.packet.src.ID; to = f.packet.dest.ID;}
					Console.WriteLine("SwitchID:{0}, from{1}, to{2}", i, from, to);
				}
			}
		}
    }
}
