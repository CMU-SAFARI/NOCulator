using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public class Router_Node : Router
    {
    	Flit m_injectSlot_CW;
    	Flit m_injectSlot_CCW;
    	RC_Coord rc_coord;
		Queue<Flit>[] ejectBuffer;
		int starveCounter;
    	public Router_Node(Coord myCoord)
            : base(myCoord)
        {
        	// the node Router is just a Rong node. A Flit gets ejected or moves straight forward
        	linkOut = new Link[2];
        	linkIn = new Link[2];
        	m_injectSlot_CW = null;
        	m_injectSlot_CCW = null;
			throttle[ID] = false;
			starved[ID] = false;
			starveCounter = 0;
        }
        public Router_Node(RC_Coord RC_c, Coord c) : base(c)
        {
        	linkOut = new Link[2];
        	linkIn = new Link[2];
        	m_injectSlot_CW = null;
        	m_injectSlot_CCW = null;
        	rc_coord = RC_c;
			ejectBuffer = new Queue<Flit> [2];
			for (int i = 0; i < 2; i++)
				ejectBuffer[i] = new Queue<Flit>();
			throttle[ID] = false;
			starved[ID] = false;
			starveCounter = 0;
        }
        
        protected void acceptFlit(Flit f)
        {
           	statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);
            m_n.receiveFlit(f);
        }
        
        Flit ejectLocal()
        {
            // eject locally-destined flit (highest-ranked, if multiple)
            Flit ret = null;
			int flitsTryToEject = 0;
			int bestDir = -1;
            for (int dir = 0; dir < 2; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                    linkIn[dir].Out.state != Flit.State.Placeholder &&
                    linkIn[dir].Out.dest.ID == ID)
                {
                    ret = linkIn[dir].Out;
                    bestDir = dir;
					flitsTryToEject ++;
                }
            if (bestDir != -1) linkIn[bestDir].Out = null;
			Simulator.stats.flitsTryToEject[flitsTryToEject].Add();
			
#if DEBUG
            if (ret != null)
                Console.WriteLine("ejecting flit {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            return ret;
        }
        
        // Only one inject Slot now. 
        //TODO: Create 2 injuct Slots, one for clockwise network, one for counter-clockwise network		
        public override bool canInjectFlit(Flit f)   
        {
			if (throttle[ID])
				return false;
			bool can;
        	if (f.parity == 0)
			{
				if (m_injectSlot_CW != null) 
					f.timeWaitToInject ++;
        		can = m_injectSlot_CW == null;
			}
        	else if (f.parity == 1)
			{
				if (m_injectSlot_CCW != null)
					f.timeWaitToInject ++;
        		can = m_injectSlot_CCW == null;
			}
        	else if (f.parity == -1)
			{
				if (m_injectSlot_CW != null && m_injectSlot_CCW != null)
					f.timeWaitToInject ++;
				can = (m_injectSlot_CW == null || m_injectSlot_CCW == null);
			}
        	else throw new Exception("Unkown parity value!");
			if (!can)
			{
				starveCounter ++;
				Simulator.stats.injStarvation.Add();
			}
			if (starveCounter == Config.starveThreshold)
				starved[ID] = true;

			return can;
        }
        
        public override void InjectFlit(Flit f)
        {						
			if (Config.topology != Topology.Mesh && Config.topology != Topology.MeshOfRings && f.parity != -1)
                throw new Exception("In Ring based topologies, the parity must be -1");
			starveCounter = 0;
			starved[ID] = false;

			if (f.parity == 0)
        	{
        		if (m_injectSlot_CW == null)
	        		m_injectSlot_CW = f;        	
				else
        			throw new Exception("The slot is empty!");
        	}
        	else if (f.parity == 1)
        	{
        		if (m_injectSlot_CCW == null)
	        		m_injectSlot_CCW = f;        
				else
        			throw new Exception("The slot is empty!");
        	}
        	else
        	{				
				int preference = -1;
				int dest = f.packet.dest.ID;
				int src = f.packet.src.ID;
				if (Config.topology == Topology.SingleRing)
				{	
					if ((dest - src + Config.N) % Config.N < Config.N / 2)
						preference = 0;
					else 
						preference = 1;
				}
				else if (dest / 4 == src / 4)
				{
					if ((dest - src + 4) % 4 < 2)
						preference = 0;
					else if ((dest - src + 4) % 4 > 2)
						preference = 1;
				}
				else if (Config.topology == Topology.HR_4drop)
				{
					if (ID == 1 || ID == 2 || ID == 5 || ID == 6 || ID == 8 || ID == 11 || ID == 12 || ID == 15 )
						preference = 0;
					else 
						preference = 1;
				}
				else if (Config.topology == Topology.HR_8drop || Config.topology == Topology.HR_8_8drop)
				{
					if (ID % 2 == 0)
						preference = 0;
					else 
						preference = 1;
				}
				else if (Config.topology == Topology.HR_8_16drop)
				{
					if (dest / 8 == src / 8)
					{
						if ((dest - src + 8) % 8 < 4)
							preference = 0;
						else 
							preference = 1;
					}
					else 
					{
						if (ID % 2 == 0)
							preference = 1;
						else 
							preference = 0;
					}
				}
				if (Config.NoPreference)
					preference = -1;
				if (preference == -1)
					preference = Simulator.rand.Next(2);
				if (preference == 0)
				{
					if (m_injectSlot_CW == null) 
						m_injectSlot_CW = f;
		        	else if (m_injectSlot_CCW == null)
						m_injectSlot_CCW = f;
				}
				else if (preference == 1)
				{
					if (m_injectSlot_CCW == null) 
						m_injectSlot_CCW = f;
		        	else if (m_injectSlot_CW == null)
						m_injectSlot_CW = f;
				}
				else 
					throw new Exception("Unknown preference!");
        	}
        }
        
       	protected override void _doStep()
        {
			for (int i = 0; i < 2; i++)
			{
				if (linkIn[i].Out != null && Config.N == 16)
				{
					Flit f = linkIn[i].Out;
					if (f.packet.src.ID / 4 == ID / 4)
						f.timeInTheSourceRing+=1;
					else if (f.packet.dest.ID / 4 == ID / 4)
						f.timeInTheDestRing +=1;
					else 
						f.timeInTheTransitionRing += 1;
				}
			}

		    Flit f1 = null,f2 = null;
			if (Config.EjectBufferSize != -1 && Config.RingEjectTrial == -1)
			{								
				for (int dir =0; dir < 2; dir ++)
				{
					if (linkIn[dir].Out != null && linkIn[dir].Out.packet.dest.ID == ID && ejectBuffer[dir].Count < Config.EjectBufferSize)
					{
						ejectBuffer[dir].Enqueue(linkIn[dir].Out);
						linkIn[dir].Out = null;
					}
				}
				int bestdir = -1;			
				for (int dir = 0; dir < 2; dir ++)
//					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || ejectBuffer[dir].Peek().injectionTime < ejectBuffer[bestdir].Peek().injectionTime))
					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || ejectBuffer[dir].Count > ejectBuffer[bestdir].Count))
//					if (ejectBuffer[dir].Count > 0 && (bestdir == -1 || Simulator.rand.Next(2) == 1))
						bestdir = dir;
				if (bestdir != -1)
					acceptFlit(ejectBuffer[bestdir].Dequeue());
			}
			else 
            {
				for (int i = 0; i < Config.RingEjectTrial; i++)
	            {
		        	Flit eject = ejectLocal();
			        if (i == 0) f1 = eject; 
				    else if (i == 1) f2 = eject;
			    	if (eject != null)             
			    		acceptFlit(eject);              
				}
				if (f1 != null && f2 != null && f1.packet == f2.packet)
					Simulator.stats.ejectsFromSamePacket.Add(1);
				else if (f1 != null && f2 != null)
              		Simulator.stats.ejectsFromSamePacket.Add(0);
			}
        
        	for (int dir = 0; dir < 2; dir++)
        	{
        		if (linkIn[dir].Out != null)
        		{
					Simulator.stats.flitsPassBy.Add(1);
        			linkOut[dir].In = linkIn[dir].Out;
        			linkIn[dir].Out = null;
        		}
        	}
        	if (m_injectSlot_CW != null && linkOut[0].In == null)  //inject a flit into network
        	{  
				linkOut[0].In = m_injectSlot_CW;
		       	statsInjectFlit(m_injectSlot_CW);
    		   	m_injectSlot_CW = null;
        	}
        	if (m_injectSlot_CCW != null && linkOut[1].In == null)
        	{
        		linkOut[1].In = m_injectSlot_CCW;
	        	statsInjectFlit(m_injectSlot_CCW);
    	    	m_injectSlot_CCW = null;
        	}
        }
    }
    
	public class Router_Switch : Router
    {
    	int bufferDepth = 1;
    	public static int[,] bufferCurD = new int[Config.N,4];
    	Flit[,] buffers;// = new Flit[4,32];
    	public Router_Switch(int ID) : base()
        {
        	coord.ID = ID;
        	enable = true;
            m_n = null;
   			buffers = new Flit[4,32]; // actual buffer depth is decided by bufferDepth
            for (int i = 0; i < 4; i++)
    			for (int n = 0; n < bufferDepth; n++)
    				buffers[i, n] = null;
        }
        
		int Local_CW = 0;
		int Local_CCW = 1;
		int GL_CW = 2;
		int GL_CCW = 3;

		// different routing algorithms can be implemented by changing this function
        private bool productiveRouter(Flit f, int port)
        {
//			Console.WriteLine("Net/RouterRing.cs");
			int RingID = ID / 4;
			if (Config.HR_NoBias)
        	{
				if ((port == Local_CW || port == Local_CCW) && RingID == f.packet.dest.ID / 4)
					return false;
				else if ((port == Local_CW || port == Local_CCW) && RingID != f.packet.dest.ID / 4)
					return true;
				else if ((port == GL_CW || port == GL_CCW) && RingID == f.packet.dest.ID / 4)
					return true;
				else if ((port == GL_CW || port == GL_CCW) && RingID != f.packet.dest.ID / 4)
					return false;
				else 
					throw new Exception("Unknown port of switchRouter");
			}
			else if (Config.HR_SimpleBias)
			{
				if ((port == Local_CW || port == Local_CCW) && RingID == f.packet.dest.ID / 4)
					return false;
				else if ((port == Local_CW || port == Local_CCW) && RingID != f.packet.dest.ID / 4)
			//		if (RingID + f.packet.dest.ID / 4 == 3)	 //diagonal. can always inject
						return true;
			/*		else if (RingID == 0 && destID == 1) return ID == 2 || ID == 1;
					else if (RingID == 0 && destID == 2) return ID == 3 || ID == 0;
					else if (RingID == 1 && destID == 0) return ID == 4 || ID == 5;
					else if (RingID == 1 && destID == 3) return ID == 6 || ID == 7;
					else if (RingID == 2 && destID == 0) return ID == 8 || ID == 9;
					else if (RingID == 2 && destID == 3) return ID == 10 || ID == 11;
					else if (RingID == 3 && destID == 1) return ID == 13 || ID == 14;
					else if (RingID == 3 && destID == 2) return ID == 12 || ID == 15;
					else 
						throw new Exception("Unknown src and dest in Hierarchical Ring");*/
				else if ((port == GL_CW || port == GL_CCW) && RingID == f.packet.dest.ID / 4)
					return true;
				else if ((port == GL_CW || port == GL_CCW) && RingID != f.packet.dest.ID / 4)
					return false;
				else 
					throw new Exception("Unknown port of switchRouter");

			}
			else 
				throw new Exception("Unknow Routing Algorithm for Hierarchical Ring");
        }
        
        protected override void _doStep()
        {
			switchRouterStats();
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
				//Console.WriteLine("linkIn[dir] == null:{0},ID:{1}, dir:{2}", linkIn[dir] == null, ID, dir);
				bool productive = (linkIn[dir].Out != null)? productiveRouter(linkIn[dir].Out, dir) : false;
				//Console.WriteLine("productive: {0}", productive);
				if (linkIn[dir].Out != null && !productive || i == bufferDepth) // nonproductive or the buffer is full : bypass the router
        		{										
        			linkOut[dir].In = linkIn[dir].Out;
        			linkIn[dir].Out = null;
        		}
        		else if (linkIn[dir].Out != null)    //productive direction and the buffer has empty space, add into buffer
        		{									
        			int k;
        			for (k = 0; k < bufferDepth; k++)
	        		{
	        			if (buffers[dir, k] == null)
	        			{
	        				buffers[dir, k] = linkIn[dir].Out;
	        				linkIn[dir].Out = null;
	        				break;
	        			}
						//Console.WriteLine("{0}", k);
	        		}
		        	if (k == bufferDepth)
		        		throw new Exception("The buffer is full!!");
        		}
        	}
        	// if there're extra slots in the same direction network, inject from the buffer
        	for (int dir = 0; dir < 4; dir ++)
        	{
        		if (linkOut[dir].In == null)    // extra slot available
        		{	
        			int posdir = (dir+2) % 4;
        			if (buffers[posdir, 0] != null)
        			{
        				linkOut[dir].In = buffers[posdir, 0];
        				buffers[posdir, 0] = null;
        			}
        		}
        	}
        	// if the productive direction with the same parity is not available. The direction with the other parity is also fine
        	for (int dir = 0; dir < 4; dir ++)
        	{				
				int crossdir = 3 - dir;
        		if (linkOut[dir].In == null)    // extra slot available
        		{
        			if (buffers[crossdir, 0] != null)
        			{	
						// for HR_SimpleBias is the dir is not a local ring, can't change rotating direction
						if (Config.HR_SimpleBias && (dir == GL_CW || dir == GL_CCW))
							continue;					
        				linkOut[dir].In = buffers[crossdir, 0];
        				buffers[crossdir, 0] = null;
        			}
        		}        	
			}
			if (Config.HR_NoBuffer)
			{
				for (int dir = 0; dir < 4; dir ++)
				{
					if (buffers[dir, 0] != null)
					{						
						if (linkOut[dir].In != null)
							throw new Exception("The outlet of the buffer is blocked");
						linkOut[dir].In = buffers[dir, 0];
						buffers[dir, 0] = null;
					}
				}
			}
			// move all the flits in the buffer if the head flit is null 
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

		void switchRouterStats()
		{
			for (int dir = 0; dir < 4; dir ++)
			{
				Flit f = linkIn[dir].Out;
				if (f != null && (dir == Local_CW || dir == Local_CCW))
				{
					if (f.packet.src.ID / 4 == ID / 4)
						f.timeInTheSourceRing ++;
					else if (f.packet.dest.ID / 4 == ID / 4)
						f.timeInTheDestRing ++;
				}
				else if (f != null && (dir == GL_CW || dir == GL_CCW))
					f.timeInGR += 2;						
			}
			return;
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
