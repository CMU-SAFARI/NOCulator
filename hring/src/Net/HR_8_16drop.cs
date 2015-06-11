#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class HR_8_16drop_Network : Network
    {
        public HR_8_16drop_Network(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }
      	public override void setup()
       	{
			if (Config.N != 64)
				throw new Exception("HR_8_16drop only suport 8x8 network");
            nodeRouters = new Router_Node[Config.N];
           	bridgeRouters = new Router_Bridge[16]; 
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
            for (int n = 0; n < 16; n++)
            	bridgeRouters[n] = new Router_Bridge(n, Config.GlobalRingWidth);
            // connect the network with Links           
			for (int n = 0; n < 4; n++)
            {
				for (int i = 0; i < 8; i++)
				{
					int ID = n * 8 + i;
					if (ID % 2 == 1)
						continue;
					int next = ID + 1;
					Link dirA = new Link(Config.router.switchLinkLatency - 1);
					Link dirB = new Link(Config.router.switchLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					nodeRouters[ID].linkOut[CW] = dirA;
					nodeRouters[next].linkIn[CW] = dirA;
					nodeRouters[ID].linkIn[CCW] = dirB;
					nodeRouters[next].linkOut[CCW] = dirB;
				}
			}
			int [] Gnext = {2, 5, 4, 1, 6, 7, 10, 9, 14, 11, 8, 15, 0, 3, 12, 13};
			for (int n = 0; n < 16; n++)
			{
				int next = n * 2;
				int pre = (next - 1 + 8) % 8 + n / 4 * 8;
				Link dirA = new Link(Config.router.switchLinkLatency - 1);
				Link dirB = new Link(Config.router.switchLinkLatency - 1);
				Link dirC = new Link(Config.router.switchLinkLatency - 1);
				Link dirD = new Link(Config.router.switchLinkLatency - 1);
				links.Add(dirA);
				links.Add(dirB);
				links.Add(dirC);
				links.Add(dirD);
				bridgeRouters[n].LLinkOut[CW] = dirA;
				nodeRouters[next].linkIn[CW] = dirA;
				bridgeRouters[n].LLinkIn[CCW] = dirB;
				nodeRouters[next].linkOut[CCW] = dirB;

				bridgeRouters[n].LLinkOut[CCW] = dirC;
				nodeRouters[pre].linkIn[CCW] = dirC;
				bridgeRouters[n].LLinkIn[CW] = dirD;
				nodeRouters[pre].linkOut[CW] = dirD;
				
				for (int i = 0 ; i < Config.GlobalRingWidth; i++)
				{
					dirA = new Link(2 - 1);
					dirB = new Link(2 - 1);
					links.Add(dirA);
					links.Add(dirB);
					bridgeRouters[n].GLinkIn[i*2+1] = dirA;
					bridgeRouters[Gnext[n]].GLinkOut[i*2+1] = dirA;
					bridgeRouters[n].GLinkOut[i*2] = dirB;
					bridgeRouters[Gnext[n]].GLinkIn[i*2] = dirB;
				}
			}
       	}

        public override void doStep()
        {
        
            doStats();
			for (int n = 0; n < Config.N; n++)
				nodes[n].doStep();
            // step the network sim: first, routers
           	for (int n = 0; n < Config.N; n++)
           		nodeRouters[n].doStep();
           	for (int n = 0; n < 16; n++)
           		bridgeRouters[n].doStep();
            // now, step each link
            foreach (Link l in links)
                l.doStep();
        }
		public override void close()
		{
			for (int n = 0; n < Config.N; n++)
				nodeRouters[n].close();
			for (int n = 0; n < 16; n++)
				bridgeRouters[n].close();
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
