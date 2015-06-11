#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class HR_4drop_Network : Network
    {
        int [] CWnext = {3, 7, 9, 13};
        int [] CCWnext = {2, 6, 8, 12};

        public override int [] GetCWnext {get {return CWnext;}}
        public override int [] GetCCWnext {get {return CCWnext;}}

        public HR_4drop_Network(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }
      	public override void setup()
       	{
			if (Config.N != 16)
				throw new Exception("HR_Simple only suport 4x4 network");
            nodeRouters = new Router_Node[Config.N];
           	bridgeRouters = new Router_Bridge[4]; 
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
            for (int n = 0; n < 4; n++)
            	bridgeRouters[n] = new Router_Bridge(n, Config.GlobalRingWidth);
            // connect the network with Links
            for (int n = 0; n < 4; n++)
            {
				for (int i = 0; i < 4; i++)
				{
					int ID = n * 4 + i;
					if (ID == 2 || ID == 6 || ID == 8 || ID == 12)
						continue;
					int next = (i + 1) % 4 + n * 4;
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
			for (int n = 0; n < 4; n++)
			{
				Link dirA = new Link(Config.router.switchLinkLatency - 1);
				Link dirB = new Link(Config.router.switchLinkLatency - 1);
				Link dirC = new Link(Config.router.switchLinkLatency - 1);
				Link dirD = new Link(Config.router.switchLinkLatency - 1);
				links.Add(dirA);
				links.Add(dirB);
				links.Add(dirC);
				links.Add(dirD);
				bridgeRouters[n].LLinkOut[CW] = dirA;
				nodeRouters[CWnext[n]].linkIn[CW] = dirA;
				bridgeRouters[n].LLinkIn[CCW] = dirB;
				nodeRouters[CWnext[n]].linkOut[CCW] = dirB;

				bridgeRouters[n].LLinkOut[CCW] = dirC;
				nodeRouters[CCWnext[n]].linkIn[CCW] = dirC;
				bridgeRouters[n].LLinkIn[CW] = dirD;
				nodeRouters[CCWnext[n]].linkOut[CW] = dirD;
				
				int next = (n + 1) % 4;

				for (int i = 0 ; i < Config.GlobalRingWidth; i++)
				{
					dirA = new Link(2 - 1);
					dirB = new Link(2 - 1);
					links.Add(dirA);
					links.Add(dirB);
					bridgeRouters[n].GLinkIn[i*2+1] = dirA;
					bridgeRouters[next].GLinkOut[i*2+1] = dirA;
					bridgeRouters[n].GLinkOut[i*2] = dirB;
					bridgeRouters[next].GLinkIn[i*2] = dirB;
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
           	for (int n = 0; n < 4; n++)
           		bridgeRouters[n].doStep();
            // now, step each link
            foreach (Link l in links)
                l.doStep();
        }
		public override void close()
		{
			for (int n = 0; n < Config.N; n++)
				nodeRouters[n].close();
			for (int n = 0; n < 4; n++)
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
