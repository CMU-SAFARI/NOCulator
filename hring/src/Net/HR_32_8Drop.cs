#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class HR_32_8drop_Network : Network
    {
        public HR_32_8drop_Network(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }
      	public override void setup()
       	{
			if (Config.N != 1024)
				throw new Exception("HR_16_8drop only suport 8x8 network");
            nodeRouters = new Router_Node[Config.N];
           	L1bridgeRouters = new Router_Bridge[512];
			L2bridgeRouters = new Router_Bridge[128];
			L3bridgeRouters = new Router_Bridge[32];
			L4bridgeRouters = new Router_Bridge[8];
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
            for (int n = 0; n < 512; n++)
            	L1bridgeRouters[n] = new Router_Bridge(n, 2, 1);
			for (int n = 0; n < 128; n++)
				L2bridgeRouters[n] = new Router_Bridge(n, 4, 2, 2, 8);
			for (int n = 0; n < 32; n++)
				L3bridgeRouters[n] = new Router_Bridge(n, 8, 4, 4, 16);
			for (int n = 0; n < 8; n++)
				L4bridgeRouters[n] = new Router_Bridge(n, 16, 8, 8, 32);
            // connect the network with Links
            for (int n = 0; n < 256; n++)
            {
				for (int i = 0; i < 4; i++)
				{
					int ID = n * 4 + i;
					if (ID % 2 == 0) 
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

			// connect L1 bridge Routers
			for (int n = 0; n < 512; n++)
			{
				int ID = n;
				int next, pre;
				if (ID % 8 == 0 || ID % 8 == 6) { next = ID * 2 + 3; pre = ID * 2 + 2; }
				else if (ID % 8 == 1 || ID % 8 == 7) {next = ID * 2 - 1; pre = ID * 2 - 2; }
				else { next = ID * 2 + 1; pre = ID * 2;}

				Link dirA = new Link(Config.router.switchLinkLatency - 1);
				Link dirB = new Link(Config.router.switchLinkLatency - 1);
				Link dirC = new Link(Config.router.switchLinkLatency - 1);
				Link dirD = new Link(Config.router.switchLinkLatency - 1);
				links.Add(dirA);
				links.Add(dirB);
				links.Add(dirC);
				links.Add(dirD);
				L1bridgeRouters[n].LLinkOut[CW] = dirA;
				nodeRouters[next].linkIn[CW] = dirA;
				L1bridgeRouters[n].LLinkIn[CCW] = dirB;
				nodeRouters[next].linkOut[CCW] = dirB;

				L1bridgeRouters[n].LLinkOut[CCW] = dirC;
				nodeRouters[pre].linkIn[CCW] = dirC;
				L1bridgeRouters[n].LLinkIn[CW] = dirD;
				nodeRouters[pre].linkOut[CW] = dirD;
				
				int nextL1 = (n + 1) % 8 + n / 8 * 8;
				if (ID % 8 == 1 || ID % 8 == 5)
					continue;
				for (int i = 0 ; i < 2; i++)
				{
					dirA = new Link(Config.router.level1RingLinkLatency - 1);
					dirB = new Link(Config.router.level1RingLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					L1bridgeRouters[n].GLinkIn[i*2+1] = dirA;
					L1bridgeRouters[nextL1].GLinkOut[i*2+1] = dirA;
					L1bridgeRouters[n].GLinkOut[i*2] = dirB;
					L1bridgeRouters[nextL1].GLinkIn[i*2] = dirB;
				}
			}			
			// connect L2 bridge Routers
			for (int n = 0; n < 128; n++)
			{
				int next, pre;
				if (n % 8 == 0 || n % 8 == 6) {next = n * 4 + 6; pre = next - 1; }
				else if (n % 8 == 1 || n % 8 == 7) {next = n * 4 - 2; pre = next - 1;}
				else {next = n * 4 + 2; pre = next - 1;}
				
				for (int i = 0; i < 2; i++)
				{
					Link dirA = new Link(Config.router.level1RingLinkLatency - 1);
					Link dirB = new Link(Config.router.level1RingLinkLatency - 1);
					Link dirC = new Link(Config.router.level1RingLinkLatency - 1);
					Link dirD = new Link(Config.router.level1RingLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					links.Add(dirC);
					links.Add(dirD);
	
					L2bridgeRouters[n].LLinkOut[i * 2] = dirA;
					L1bridgeRouters[next].GLinkIn[i * 2] = dirA;
					L2bridgeRouters[n].LLinkIn[i * 2 + 1] = dirB;
					L1bridgeRouters[next].GLinkOut[i * 2 + 1] = dirB;

					L2bridgeRouters[n].LLinkOut[i * 2 + 1] = dirC;
					L1bridgeRouters[pre].GLinkIn[i * 2 + 1] = dirC;
					L2bridgeRouters[n].LLinkIn[i * 2] = dirD;
					L1bridgeRouters[pre].GLinkOut[i * 2] = dirD;
				}
				int nextL2 = (n + 1) % 8 + n / 8 * 8;
				if (n % 8 == 1 || n % 8 == 5)
					continue;
				for (int i = 0; i < 4; i++)
				{
					Link dirA = new Link(Config.router.level2RingLinkLatency - 1);
					Link dirB = new Link(Config.router.level2RingLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					L2bridgeRouters[n].GLinkIn[i*2+1] = dirA;
					L2bridgeRouters[nextL2].GLinkOut[i*2+1] = dirA;
					L2bridgeRouters[n].GLinkOut[i*2] = dirB;
					L2bridgeRouters[nextL2].GLinkIn[i*2] = dirB;
				}
			}			
			//connect L3 bridge Routers
			for (int n = 0; n < 32; n++)
			{
				int next, pre;
				if (n % 8 == 0 || n % 8 == 6) {next = n * 4 + 6; pre = next - 1;}
				else if (n % 8 == 1 || n % 8 == 7) {next = n * 4 - 2; pre = next - 1;}
				else {next = n * 4 + 2; pre = next - 1;}

				for (int i = 0; i < 4; i++)
				{	
					Link dirA = new Link(Config.router.level2RingLinkLatency - 1);
					Link dirB = new Link(Config.router.level2RingLinkLatency - 1);
					Link dirC = new Link(Config.router.level2RingLinkLatency - 1);
					Link dirD = new Link(Config.router.level2RingLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					links.Add(dirC);
					links.Add(dirD);

					L3bridgeRouters[n].LLinkOut[i*2] = dirA;
					L2bridgeRouters[next].GLinkIn[i*2] = dirA;
					L3bridgeRouters[n].LLinkIn[i*2+1] = dirB;
					L2bridgeRouters[next].GLinkOut[i*2+1] = dirB;

					L3bridgeRouters[n].LLinkOut[i * 2 + 1] = dirC;
					L2bridgeRouters[pre].GLinkIn[i * 2 + 1] = dirC;
					L3bridgeRouters[n].LLinkIn[i * 2] = dirD;
					L2bridgeRouters[pre].GLinkOut[i * 2] = dirD;
				}				
				int nextL3 = (n+1) % 8 + n / 8 * 8;
				if (n % 8 == 1 || n % 8 == 5)
					continue;
				for (int i = 0; i < 8; i++)
				{
					Link dirA = new Link(Config.router.level3RingLinkLatency - 1);
					Link dirB = new Link(Config.router.level3RingLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					L3bridgeRouters[n].GLinkIn[i*2+1] = dirA;
					L3bridgeRouters[nextL3].GLinkOut[i*2+1] = dirA;
					L3bridgeRouters[n].GLinkOut[i*2] = dirB;
					L3bridgeRouters[nextL3].GLinkIn[i*2] = dirB;
				}
			}
			//connect L4 bridge Routers
			for (int n = 0; n < 8; n++)
			{
				int next, pre;
				if (n % 8 == 0 || n % 8 == 6) {next = n * 4 + 6; pre = next - 1;}
				else if (n % 8 == 1 || n % 8 == 7) {next = n * 4 - 2; pre = next - 1;}
				else {next = n * 4 + 2; pre = next - 1;}

				for (int i = 0; i < 8; i++)
				{	
					Link dirA = new Link(Config.router.level3RingLinkLatency - 1);
					Link dirB = new Link(Config.router.level3RingLinkLatency - 1);
					Link dirC = new Link(Config.router.level3RingLinkLatency - 1);
					Link dirD = new Link(Config.router.level3RingLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					links.Add(dirC);
					links.Add(dirD);

					L4bridgeRouters[n].LLinkOut[i*2] = dirA;
					L3bridgeRouters[next].GLinkIn[i*2] = dirA;
					L4bridgeRouters[n].LLinkIn[i*2+1] = dirB;
					L3bridgeRouters[next].GLinkOut[i*2+1] = dirB;

					L4bridgeRouters[n].LLinkOut[i * 2 + 1] = dirC;
					L3bridgeRouters[pre].GLinkIn[i * 2 + 1] = dirC;
					L4bridgeRouters[n].LLinkIn[i * 2] = dirD;
					L3bridgeRouters[pre].GLinkOut[i * 2] = dirD;
				}				
				int nextL4 = (n+1) % 8 + n / 8 * 8;
				for (int i = 0; i < 16; i++)
				{
					Link dirA = new Link(Config.router.level4RingLinkLatency - 1);
					Link dirB = new Link(Config.router.level4RingLinkLatency - 1);
					links.Add(dirA);
					links.Add(dirB);
					L4bridgeRouters[n].GLinkIn[i*2+1] = dirA;
					L4bridgeRouters[nextL4].GLinkOut[i*2+1] = dirA;
					L4bridgeRouters[n].GLinkOut[i*2] = dirB;
					L4bridgeRouters[nextL4].GLinkIn[i*2] = dirB;
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
           	for (int n = 0; n < 512; n++)
           		L1bridgeRouters[n].doStep();
			for (int n = 0; n < 128; n++)
				L2bridgeRouters[n].doStep();
			for (int n = 0; n < 32; n++)
				L3bridgeRouters[n].doStep();
			for (int n = 0; n < 8; n++)
				L4bridgeRouters[n].doStep();
			
            // now, step each link
            foreach (Link l in links)
                l.doStep();
        }
		public override void close()
		{
			for (int n = 0; n < Config.N; n++)
				nodeRouters[n].close();
			for (int n = 0; n < 512; n++)
				L1bridgeRouters[n].close();
			for (int n = 0; n < 128; n++)
				L2bridgeRouters[n].close();
			for (int n = 0; n < 32; n++)
				L3bridgeRouters[n].close();
			for (int n = 0; n < 8; n++)
				L4bridgeRouters[n].close();
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
