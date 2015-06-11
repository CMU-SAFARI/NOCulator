//#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class HR_buffered_Network : Network
    {
        public HR_buffered_Network(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }

        public override void setup()
        {
			if (Config.network_nrX != 4 && Config.network_nrY != 4)
				throw new Exception("buffered hierarchical ring only support 4x4 network");
			nics = new Router_NIC[Config.N];
			iris = new Router_IRI[4];
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
                nodes[n] = new Node(mapping, c);
				nics[n] = new Router_NIC(c);
                nodes[n].setRouter(nics[n]);
                nics[n].setNode(nodes[n]);
            }
			for (int n = 0; n < 4; n++)
			{
				iris[n] = new Router_IRI(n);
			}

			for (int group = 0; group < 4; group ++)
			{
				for (int i = 0; i < 4; i++)
				{
					if (i == 3) continue;
					int node = group * 4 + i;
					Link dirA = new Link(Config.router.switchLinkLatency - 1);
					links.Add(dirA);
					nics[node].linkOut[0] = dirA;
					nics[node+1].linkIn[0] = dirA;
				}

				Link dirD = new Link(Config.router.switchLinkLatency - 1);
				Link dirB = new Link(Config.router.switchLinkLatency - 1);
				links.Add(dirB);
				links.Add(dirD);
				nics[group*4 + 3].linkOut[0] = dirD;
				iris[group].LLinkIn[0] = dirD;	
				nics[group*4].linkIn[0] = dirB;
				iris[group].LLinkOut[0] = dirB;
				int next = (group + 1) % 4;
				Link dirC = new Link(Config.router.level1RingLinkLatency - 1);
				links.Add(dirC);
				iris[group].GLinkOut[0] = dirC;
				iris[next].GLinkIn[0] = dirC;
			}
       	} 
        // Pins specification for nodeRouters
       	
        public override void doStep()
        {
            doStats();
            for (int n = 0; n < Config.N; n++)
                nodes[n].doStep();
            // step the network sim: first, routers
           	for (int n = 0; n < Config.N; n++)
           		nics[n].doStep();
           	for (int n = 0; n < 4; n++)
           		iris[n].doStep();            	
            // now, step each link
            foreach (Link l in links)
                l.doStep();
        }


        public override void close()
        {
	      	for (int n = 0; n < Config.N; n++)
    	    	nics[n].close();
			for (int n = 0; n < 4; n++)
				iris[n].close();
        }
	}
}
