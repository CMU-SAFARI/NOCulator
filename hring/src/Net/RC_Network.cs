//#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public class RC_Network : Network
    {
        public RC_Network(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }

        public override void setup()
        {
            nodeRouters = new Router_Node[Config.N];
            connectRouters = new Router_Connect[Config.N / 2]; // some of the connectRouters may be disabled
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
            // connectRouters are numbered by sweeping x, with y increase in steps
            // disable the routers not needed here. Different topologies can be realised. (mesh, torus)
            for (int n = 0; n < Config.N / 2 ; n++)
            {
            	connectRouters[n] = new Router_Connect(n);
				/*if (n == 0 || n == 1 || n == 2 || n == 3 || 
            		n == 32 || n == 33 || n == 34 || n == 35)
            		connectRouters[n].enable = false;
            	if (n == 4 || n == 12 || n == 20 || n == 28)
            		connectRouters[n].enable = false;*/
            }
			if(Config.RC_mesh)
			{
				for (int i = 0; i < Config.network_nrX / 2; i++)
					connectRouters[i].enable = false;
				for (int i = 0; i < Config.network_nrY / 2; i++)
					connectRouters[Config.network_nrX * i + Config.network_nrX / 2].enable = false;
			}
			if(Config.RC_Torus)
			{
				// all connection Routers are enabled. Both horizontally and vertically. 
				; 
			}
			if (Config.RC_x_Torus)
			{
				for (int i = 0; i < Config.network_nrX / 2; i++)
					connectRouters[i].enable = false;
			}

//			Console.WriteLine("test");
            if (Config.RouterEvaluation)
                return;

            // connect the network with Links
            for (int y = 0; y < Y / 2; y++)
            {
            	for (int x = 0; x < X / 2; x++)
            	{
            		for (int z = 0; z < 4; z++)
            		{
            			int nextCRouter = -1;
						int preCRouter = -1;
            			switch (z)
            			{
            				case 0 : {nextCRouter = x + X*y + X/2; preCRouter = x + X*y; break;}
            				case 1 : {nextCRouter = (x + X*(y+1)) % (Config.N/2); preCRouter = x + X*y + X/2; break;}
            				case 2 : {nextCRouter = (x+1) % (X/2) + X*y + X/2; preCRouter = (x + X*(y+1)) % (Config.N/2); break;}
            				case 3 : {nextCRouter = x + X*y; preCRouter = (x+1) % (X/2) + X*y + X/2; break;}
            			}
						

		        		if (connectRouters[nextCRouter].enable) // clockwise: the connectRouter next to z = 0
		        		{	
		        			Link dirA = new Link(Config.router.linkLatency_N2R - 1);
		        			Link dirB = new Link(Config.router.linkLatency_N2R - 1);
		        			links.Add(dirA);
		        			links.Add(dirB);
							int port = ((z-1) < 0) ? z + 3 : z - 1;
						//	Console.WriteLine("x:{0},y:{1},z:{2}, ID:{3}", x, y, z, RC_Coord.getIDfromXYZ(x, y, z));
						//	Console.WriteLine("node:{0}, next_id:{1}, port{2}", RC_Coord.getIDfromXYZ(x, y, z), nextCRouter, port);
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkOut[CW] = dirA;							
		        			connectRouters[nextCRouter].linkIn[port] = dirA;
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkIn[CCW] = dirB;
		        			connectRouters[nextCRouter].linkOut[port] = dirB;
		        		}
		        		else  // the router is not enabled : does not exist
		        		{
		        			Link dirA = new Link(Config.router.linkLatency_N2N - 1);
		        			Link dirB = new Link(Config.router.linkLatency_N2N - 1);
		        			links.Add(dirA);
		        			links.Add(dirB);
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkOut[CW] = dirA;
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, (z+1) % 4)].linkIn[CW] = dirA;
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkIn[CCW] = dirB;
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, (z+1) % 4)].linkOut[CCW] = dirB;
							//Console.WriteLine("node:{0}, wire to node:{1}", RC_Coord.getIDfromXYZ(x, y, z), RC_Coord.getIDfromXYZ(x, y, (z+1) % 4));
		        		}		        		
		        		if (connectRouters[preCRouter].enable)
		        		{
		        			Link dirA = new Link(Config.router.linkLatency_N2R - 1);
		        			Link dirB = new Link(Config.router.linkLatency_N2R - 1);
		        			links.Add(dirA);
		        			links.Add(dirB);
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkOut[CCW] = dirA;
		        			connectRouters[preCRouter].linkIn[(z+1) % 4] = dirA;
		        			nodeRouters[RC_Coord.getIDfromXYZ(x, y, z)].linkIn[CW] = dirB;
		        			connectRouters[preCRouter].linkOut[(z+1) % 4] = dirB;
							//Console.WriteLine("node:{0}, pre_id:{1}, port{2}", RC_Coord.getIDfromXYZ(x, y, z), preCRouter, port);
		        		}
            		}
            	}
            }
			int PHnumber = Config.PlaceHolderNum;
			while (PHnumber > 0)
			{
				PHnumber --;
				Flit f1 = new Flit(null , 0); // generate a placeholder flit
				Flit f2 = new Flit(null , 0);
				f1.state = Flit.State.Placeholder;
				f2.state = Flit.State.Placeholder;
				nodeRouters[PHnumber].linkOut[0].In = f1;
				nodeRouters[PHnumber].linkOut[1].In = f2;				
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
           	for (int n = 0; n < Config.N / 2; n++)
           		connectRouters[n].Scalable_doStep();
            // now, step each link
            foreach (Link l in links)
                l.doStep();
        }
		public override void close()
		{
			for (int n = 0; n < Config.N; n++)
				nodeRouters[n].close();
			for (int n = 0; n < Config.N / 2; n++)
				connectRouters[n].close();
		}
    }
}
