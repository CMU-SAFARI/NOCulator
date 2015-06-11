using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public class Router_TorusRingNode : Router
    {
    	Flit m_injectSlot;
    	public Router_TorusRingNode(Coord myCoord)
            : base(myCoord)
        {
        	// the node Router is just a Rong node. A Flit gets ejected or moves straight forward
        	m_injectSlot = null;
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
            if (linkIn[0].Out != null && linkIn[0].Out.dest.ID == ID)
			{
				ret = linkIn[0].Out;
				linkIn[0].Out = null;
			}
#if DEBUG
            if (ret != null)
                Console.WriteLine("ejecting flit {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.currentRound);
#endif
			
            return ret;
        }
        
        // Only one inject Slot now. 
        //TODO: Create 2 injuct Slots, one for clockwise network, one for counter-clockwise network
        public override bool canInjectFlit(Flit f)   
        {
        	return m_injectSlot == null;
        }
        
        public override void InjectFlit(Flit f)
        {
            //if (m_injectSlot_CW != null)
            //    throw new Exception("Trying to inject twice in one cycle");
      		m_injectSlot = f;
        }
        
        protected override void _doStep()
        {
        	Flit eject = ejectLocal();
        	if (eject != null)
        		acceptFlit(eject);
       		if (linkIn[0].Out != null)
        	{
        		linkOut[0].In = linkIn[0].Out;
        		linkIn[0].Out = null;
        	}
        	if (m_injectSlot != null && linkOut[0].In == null)
        	{
        		linkOut[0].In = m_injectSlot;
        		m_injectSlot = null;
        	}
        }
    }
    public class Router_TorusRingConnect : Router
    {
    	public int ConnectID;
    	public Router_TorusRingConnect(int ID) : base()
        {
        	ConnectID = ID;
        	coord.ID = ID;
        	linkIn = new Link[2];
        	linkOut = new Link[2];
            m_n = null;
        }
        
        private bool productiveRouter(Flit f, int port)
        {   
        	int currentX=-1, currentY=-1;
        	if (ID == 0 && port == 0 ||
        		ID == 1 && port == 1 ||
        		ID == 7 && port == 1 ||
        		ID == 6 && port == 0)	
        	{ currentX = 0; currentY = 0;}
        	if (ID == 0 && port == 1 ||
        		ID == 3 && port == 1 ||
        		ID == 1 && port == 0 ||
        		ID == 2 && port == 0)	
        	{ currentX = 0; currentY = 1;}
        	if (ID == 3 && port == 0 ||
        		ID == 4 && port == 1 ||
        		ID == 2 && port == 1 ||
        		ID == 5 && port == 0)	
        	{ currentX = 1; currentY = 1;}
        	if (ID == 7 && port == 0 ||
        		ID == 5 && port == 1 ||
        		ID == 6 && port == 1 ||
        		ID == 4 && port == 0)	
        	{ currentX = 1; currentY = 0;}
        		
        	int destCluster = f.packet.dest.ID/4;
        	int destCX = destCluster / 2;
        	int destCY = (destCluster == 0 || destCluster == 3)? 0:1;
        	if (ID == 0 || ID == 1 || ID == 4 || ID == 5)
        	{
				return destCY != currentY;
        	}
        	else
        	{
        		return destCX != currentX;
        	}        	
        }
        
        protected override void _doStep()
        {
        	if (ID == 6 || ID == 7) // to guarantee livelock freedom
        	{
        		linkOut[0].In = linkIn[0].Out;
        		linkOut[1].In = linkIn[1].Out;
        		linkIn[0].Out = null;
        		linkIn[1].Out = null;
        	}
        	
        	// consider 4 input ports seperately. If not productive, keep circling
        	for (int dir = 0; dir < 2; dir ++)  
        		if (linkIn[dir].Out != null && !productiveRouter(linkIn[dir].Out, dir))
        		{
        			linkOut[dir].In = linkIn[dir].Out;
        			linkIn[dir].Out = null;
        		}
        	//If changing rings is productive
        	for (int dir = 0; dir < 2; dir ++)
        	{        		
        		if (linkIn[dir].Out == null) continue;
        		if (linkOut[1-dir].In != null)
        		{
        			linkOut[dir].In = linkIn[dir].Out;
        			linkIn[dir].Out = null;
        			//Simulator.stats.connectRouter_reflected[ID,dir].Add();
        		} // if not, inject
        		else if (linkOut[1-dir].In == null)
        		{
        			linkOut[1-dir].In = linkIn[dir].Out;
        			linkIn[dir].Out = null;
        			//Simulator.stats.connectRouter_traversed[ID,dir].Add();
        		}
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
