using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public class Router_Connect : Router
    {
    	public int ConnectID;
    	int bufferDepth = 4;
    	public static int[,] bufferCurD = new int[Config.N / 2,4];
    	Flit[,] buffers;// = new Flit[4,32];
    	public Router_Connect(int ID) : base()
        {
        	ConnectID = ID;
        	coord.ID = ID;
        	enable = true;
            m_n = null;
   			buffers = new Flit[4,32]; // actual buffer depth is decided by bufferDepth
            for (int i = 0; i < 4; i++)
    			for (int n = 0; n < bufferDepth; n++)
    				buffers[i, n] = null;
        }
       
        
        private bool productiveRouter(Flit f, int port)
        {
		    if (ID % Config.network_nrX < Config.network_nrX / 2) // connectRouters bridging along the y direction
		    {
		    	int y = (port == 1 || port == 2) ? ID / Config.network_nrX: (ID / Config.network_nrX + Config.network_nrY / 2 - 1) % (Config.network_nrY / 2);
		    	int dest_y = f.packet.dest.ID / 4 % (Config.network_nrY / 2);
		    	int distance;
		    	if (Config.RC_mesh == true || Config.RC_x_Torus)
				{
					if (port == 1 || port == 2)
						return dest_y < y;
					else
						return dest_y > y;
				}
				else if (Config.RC_Torus)
				{
					if (dest_y == y) return false;
					if (port == 0 || port == 3) 
		    			distance = (dest_y - y < 0) ? dest_y - y + Config.network_nrY / 2 : dest_y - y;
		    		else 	
	    				distance = (y - dest_y < 0) ? y - dest_y + Config.network_nrY / 2 : y - dest_y;
	    			return distance <= Config.network_nrY / 4;		    		
				}
				else 
					throw new Exception("Topology not defined!");
		    }
		    else  // connectRouters bridging along the x direction
		    {
		    	int x = (port == 0 || port == 1) ? (ID % Config.network_nrX - 1) % (Config.network_nrX / 2) : 
		    		ID % Config.network_nrX - Config.network_nrX / 2;
		    	int dest_x = f.packet.dest.ID / 4 / (Config.network_nrY / 2);
				int distance;
				if (Config.RC_mesh)
				{			
		 	 		if (port == 0 || port == 1)
						return dest_x > x;
		    		else 
						return dest_x < x;
				}
				else if (Config.RC_Torus || Config.RC_x_Torus) 
				{
					if (Config.N == 16)
						return dest_x != x;
					else if (Config.N == 64)
					{
						if (Config.AllBiDirLink == false)
						{
							if (dest_x == x) return false;
							if ((ID % 8 == 4 || ID % 8 == 7) && (port == 1 || port == 2))
								return false;
							if ((ID % 8 == 5 || ID % 8 == 6) && (port == 0 || port == 3))
								return false;
						}
						if (port == 1 || port == 0)
							distance = (dest_x - x < 0) ? dest_x - x + Config.network_nrX / 2 : dest_x - x;
						else
							distance = (x - dest_x < 0) ? x - dest_x + Config.network_nrX / 2 : x - dest_x;
						return distance <= Config.network_nrX / 4;						
					}
					else 
						throw new Exception("For Torus or xTorus, only support 4x4 or 8x8 network");
				}
				else 
					throw new Exception("Topology not defined!");
			}
	    }
        
        protected override void _doStep()
        {
        	// consider 4 input ports seperately. If not productive, keep circling	
        	if (!enable) return;
/*        	if (ID == 3)
			{	
				if (linkIn[0].Out != null && linkIn[0].Out.packet.dest.ID == 11)
					Console.WriteLine("parity : {0}, src:{1}", linkIn[0].Out.parity, linkIn[0].Out.packet.src.ID);
			}*/
        	for (int dir = 0; dir < 4; dir ++)
        	{
        		int i;
        		for (i = 0; i < bufferDepth; i++)
        			if (buffers[dir, i] == null)
        				break;
				bool productive = (linkIn[dir].Out != null)? productiveRouter(linkIn[dir].Out, dir) : false;
				if (linkIn[dir].Out != null && !productive || i == bufferDepth) // nonproductive or the buffer is full : bypass the router
        		{
        			int bypassdir = (ID % Config.network_nrX < Config.network_nrX / 2) ? 3- dir : ((dir < 2) ? 1 - dir : 5 - dir);
					Simulator.stats.bypassConnect.Add(1);
        			linkOut[bypassdir].In = linkIn[dir].Out;
        			linkIn[dir].Out = null;
        		}
        		else if (linkIn[dir].Out != null)    //productive direction and the buffer has empty space, add into buffer
        		{
					if (ID % Config.network_nrX >= Config.network_nrX / 2) // connecting along the x direction
						Simulator.stats.crossXConnect.Add(1);
					else
						Simulator.stats.crossYConnect.Add(1);
					// for 8x8 case, guarantee functinality
					if (Config.N == 64 && (Config.RC_Torus || Config.RC_x_Torus) && !Config.AllBiDirLink && linkIn[dir].Out != null)
					{
						if ((ID % 8 == 4 || ID % 8 == 7) && (dir == 1 || dir == 2) ||
							(ID % 8 == 5 || ID % 8 == 6) && (dir == 0 || dir == 3))
							throw new Exception("Injection port forbidden!");						
					}
        			int k;
        			for (k = 0; k < bufferDepth; k++)
	        		{
	        			if (buffers[dir, k] == null)
	        			{
	        				buffers[dir, k] = linkIn[dir].Out;
							buffers[dir, k].timeIntoTheBuffer = Simulator.CurrentRound;
	        				linkIn[dir].Out = null;
	        				break;
	        			}
	        		}
		        	if (k == bufferDepth)
		        		throw new Exception("The buffer is full!!");
			//		Console.WriteLine("Add to buffer : CR:{0}, dir:{1}, depth:{2}", ID, dir, k);
        		}
        	}
        	//Console.WriteLine("TEST");
        	// if there're extra slots in the same direction network, inject from the buffer
        	for (int dir = 0; dir < 4; dir ++)
        	{
        		if (linkOut[dir].In == null)    // extra slot available
        		{	
        			int posdir = (ID % Config.network_nrX < Config.network_nrX / 2) ? ((dir >= 2)? 5-dir : 1-dir) : (3 - dir);
        			if (buffers[posdir, 0] != null)
        			{
						if (Config.N == 64 && (Config.RC_Torus || Config.RC_x_Torus) && !Config.AllBiDirLink && ID % Config.network_nrX >= Config.network_nrX / 2)
						{	
							int dest_x = buffers[posdir, 0].packet.dest.ID / 16;
							int x = (posdir == 0 || posdir == 1) ? (ID % Config.network_nrX - 1) % (Config.network_nrX / 2) : 
		    					ID % Config.network_nrX - Config.network_nrX / 2;
		    				int x_next = (posdir == 0 || posdir == 1) ? (x + 1) % (Config.network_nrX / 2) : (x + Config.network_nrX / 2 - 1) % (Config.network_nrX / 2);
							//Console.WriteLine("TTTTtest");
							if (dest_x != x_next && (ID % 8 == 4 && posdir == 0 || ID % 8 == 5 && posdir == 2 || ID % 8 == 6 && posdir == 1 || ID % 8 == 7 && posdir == 3))
							{
								Console.WriteLine("Can't inject to the same dir : CR:{0}, posdir:{1}, dest_x:{2}", ID, posdir, dest_x);
								continue;
							}
						}
        				linkOut[dir].In = buffers[posdir, 0];
        				buffers[posdir, 0] = null;
					}
        		}
        	}
        	// if the productive direction with the same parity is not available. The direction with the other parity is also fine
        	for (int dir = 0; dir < 4; dir ++)
        	{				
				int crossdir = (dir + 2) % 4;
        		if (linkOut[dir].In == null || linkOut[dir].In.state == Flit.State.Placeholder)    // extra slot available
        		{
        			if (buffers[crossdir, 0] != null)
        			{
        				if (ID % Config.network_nrX >= Config.network_nrX / 2) // for routers connecting rings along the x direction
        				{
        					int dest_x = buffers[crossdir, 0].packet.dest.ID / 4 / (Config.network_nrY / 2);
							int x = (crossdir == 0 || crossdir == 1) ? (ID % Config.network_nrX - 1) % (Config.network_nrX / 2) : 
		    					ID % Config.network_nrX - Config.network_nrX / 2;
		    				int x_next = (crossdir == 0 || crossdir == 1) ? (x + 1) % (Config.network_nrX / 2) : (x + Config.network_nrX / 2 - 1) % (Config.network_nrX / 2);
		    				//if the next ring's x is not the destination x, can't inject
							if (!Config.AllBiDirLink && Config.N == 16)
			    				if (x_next != dest_x) 
		    						throw new Exception("The next x must be the dest_x");
							if (Config.N == 64 && (Config.RC_Torus || Config.RC_x_Torus) && !Config.AllBiDirLink)
								if (dest_x != x_next && (ID % 8 == 4 && crossdir == 3 || ID % 8 == 5 && crossdir == 1 || ID % 8 == 6 && crossdir == 2 || ID % 8 == 7 && crossdir == 0))
								{
									continue;
								}
		    			}
		    			else if (ID % Config.network_nrX < Config.network_nrX / 2) // routers connecting along y direction
		    			{
		    				int x = ID % Config.network_nrX;
		    				int dest_x = buffers[crossdir, 0].packet.dest.ID / 4 / (Config.network_nrY / 2);
		    				if (!Config.AllBiDirLink)
								if (x != dest_x) 
			    					continue;
		    			}
        				linkOut[dir].In = buffers[crossdir, 0];
        				buffers[crossdir, 0] = null;
					}
        		}
        	}
        	for (int dir = 0; dir < 4; dir ++)
	        {	
	        	if (buffers[dir, 0] == null)
	        	{
	        		for (int i = 0; i < bufferDepth - 1; i++)	
    					buffers[dir, i] = buffers[dir, i + 1];
        			buffers[dir, bufferDepth-1] = null;
        		}
        	}
        	for (int dir = 0; dir < 4; dir ++)
        	{
        		int i;
        		for (i = 0; i < bufferDepth; i++)
        			if (buffers[dir, i] == null)
        				break;
        		bufferCurD[ID,dir] = i;
        	}
        }
		public override bool canInjectFlit(Flit f)
        {
        	return false;
        }
        public override void InjectFlit(Flit f)
        {
        	return;
        }
    }
}
