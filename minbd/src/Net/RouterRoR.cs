//#define DEBUG
using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public abstract class Router_RoR : Router
    {
        // injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;

        public Router_RoR(Coord myCoord)
            : base(myCoord)
        {
        	if (!Config.torus && !Config.edge_loop)
        		throw new Exception("Rings of Rings must be a torus or edge-loop.  Set Config.torus or Config.edge_loop to true");
        		
            m_injectSlot = null;
            m_injectSlot2 = null;
        }

        // accept one ejected flit into rxbuf
        void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }

        Flit[] m_ej = new Flit[4] { null, null, null, null };
        int m_ej_rr = 0;

        Flit ejectLocalNew()
        {
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                        linkIn[dir].Out.dest.ID == ID &&
                        m_ej[dir] == null)
                {
                    m_ej[dir] = linkIn[dir].Out;
                    linkIn[dir].Out = null;
                }

            m_ej_rr++; m_ej_rr %= 4;

            Flit ret = null;
            if (m_ej[m_ej_rr] != null)
            {
                ret = m_ej[m_ej_rr];
                m_ej[m_ej_rr] = null;
            }
#if DEBUG
            if (ret != null)
                Console.WriteLine("| ejecting flit  {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            return ret;
        }

        Flit ejectLocal()
        {
            // eject locally-destined flit (highest-ranked, if multiple)
            Flit ret = null;
            int bestDir = -1;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                    linkIn[dir].Out.state != Flit.State.Placeholder &&
                    linkIn[dir].Out.dest.ID == ID &&
                    (ret == null || rank(linkIn[dir].Out, ret) < 0))
                {
                    ret = linkIn[dir].Out;
                    bestDir = dir;
                }

            if (bestDir != -1) linkIn[bestDir].Out = null;
#if DEBUG
            if (ret != null)
                Console.WriteLine("| ejecting flit  {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            return ret;
        }

  		/*
  		 * Injects flits
  		 */      
        void inject(out bool injected)
        {
        	injected = false;
        	
        	if (input[4] == null)
        		return;
        	
        	if (Config.injectOnlyX)
        	{
	        		int i = (Simulator.rand.Next(2) == 1) ? Simulator.DIR_RIGHT : Simulator.DIR_LEFT;
	    			if (input[i] == null)
	    			{
	    				injected = true;
	    				input[i] = input[4];
	    				input[4] = null;
	    				return;
	    			}
	        		if (i == Simulator.DIR_RIGHT)
	        			i = Simulator.DIR_LEFT;
	        		else
	        			i = Simulator.DIR_RIGHT;
	
	        		if (input[i] == null)
	    			{
	    				injected = true;
	    				input[i] = input[4];
	    				input[4] = null;
	    			}	
	        }
	        else
	        {
	        	for (int i = 0; i < 4; i++)
	        	{
	        		if (input[i] == null)
	        		{
	        			injected = true;
	        			input[i] = input[4];
	        			input[4] = null;
	        			break;
	        		}
	        	}
	        }
        }
        
        /* Tells if a flit wants to turn */
        bool wantToTurn(Flit f, int dir)
        {
        	bool wantToTurn = false;
        	switch(dir)
    		{
    			case Simulator.DIR_UP:
    				wantToTurn = (coord.y == f.packet.dest.y);
    				break;
    				
    			case Simulator.DIR_DOWN:
    				wantToTurn = (coord.y == f.packet.dest.y);	
    				break;
    			
    			case Simulator.DIR_LEFT:
    				wantToTurn = (coord.x == f.packet.dest.x);	
    				break;
    			
    			case Simulator.DIR_RIGHT:
    				wantToTurn = (coord.x == f.packet.dest.x);
    				break;
    			default:
    				throw new Exception("Not a direction");
    		}
    		return wantToTurn;
        }
        
        int turn_priority(int dir1, int dir2)
        {
        	Flit f1 = (dir1 == -1) ? null : input[dir1];
        	Flit f2 = (dir2 == -1) ? null : input[dir2];
        	int ret = rank(f1, f2);
        	switch (ret)
        	{
        		case 0:
        			return (Simulator.rand.Next(2) == 1) ? dir1 : dir2;
        			//throw new Exception("Tiebraker wasn't decided");
        		case -1:
        			return dir1;
        		case 1:
        			return dir2;
        		default:
        			throw new Exception("Should I allow for larger values than 1 and -1");
        	}
        	//return (1 == Simulator.rand.Next(2)) ? dir1 : dir2;
        }
        
        int turnX_priority()
        {
        	Flit left  = input[Simulator.DIR_LEFT];
        	Flit right = input[Simulator.DIR_RIGHT];
        	bool turn_left  = false;
        	bool turn_right = false;
        	
        	if (left != null)
        		turn_left = wantToTurn(left, Simulator.DIR_LEFT);
        	
        	if (right != null)
        		turn_right = wantToTurn(right, Simulator.DIR_RIGHT);
        	
        	if (turn_left ^ turn_right)
        	{
        		if(turn_left)
        			return Simulator.DIR_LEFT;
        		else
        			return Simulator.DIR_RIGHT;
        	}
        	else if (turn_left && turn_right)
        	{
        		int ret = turn_priority(Simulator.DIR_LEFT, Simulator.DIR_RIGHT);
        		if (ret == Simulator.DIR_LEFT)
        			right.missedTurns++;
        		else
        			left.missedTurns++;
        		return ret;
        	}
        	else
        		return -1;
        }
        
        int turnY_priority()
        {
        	Flit up   = input[Simulator.DIR_UP];
        	Flit down = input[Simulator.DIR_DOWN];
        	bool turn_up   = false;
        	bool turn_down = false;
        	
        	if (up != null)
        		turn_up = wantToTurn(up, Simulator.DIR_UP);
        	
        	if (down != null)
        		turn_down = wantToTurn(down, Simulator.DIR_DOWN);
        	
        	if (turn_up ^ turn_down)
        	{
        		if(turn_down)
        			return Simulator.DIR_DOWN;
        		else
        			return Simulator.DIR_UP;
        	}
        	else if (turn_up && turn_down)
        	{	
        		int ret = turn_priority(Simulator.DIR_DOWN, Simulator.DIR_UP); 
        		if (ret == Simulator.DIR_UP)
        			down.missedTurns++;
        		else
        			up.missedTurns++;
        		return ret;
        	}
        	else
        		return -1;	
        }
        
        //int[] swapArray = new int[5];
        void swap(int dir1, int dir2)
        {
        	Flit tempFlit = input[dir1];
        	input[dir1] = input[dir2];
        	input[dir2] = tempFlit;
        	//swapArray[dir2] = dir1;
        	//swapArray[dir1] = dir2;
        }
        
        
        int getFreeFlit(int dir1, int dir2)
        {
        	Flit f1 = (dir1 == -1) ? null : input[dir1];
        	Flit f2 = (dir2 == -1) ? null : input[dir2];
        	int ret = rank(f1, f2);
        	switch (ret)
        	{
        		case 0:
        			return (Simulator.rand.Next(2) == 1) ? dir1 : dir2;
        			//throw new Exception("Tiebraker wasn't decided");
        		case -1:
        			return dir2;
        		case 1:
        			return dir1;
        		default:
        			throw new Exception("Should I allow for larger values than 1 and -1");
        	}
        }
        
        void route()
        {
        	int nullCount = 0;
        	for(int i = 0; i < 4;  i++)
        	{
        		//swapArray[i] = i;
        		if (input[i] == null)
        			nullCount++;	
        	}
        	int turnY = turnY_priority();
        	int turnX = turnX_priority();
        	Flit flitX = (turnX != -1) ? input[turnX] : null;
        	Flit flitY = (turnY != -1) ? input[turnY] : null;
        	
        	bool isFreeX = (input[Simulator.DIR_LEFT] == null || input[Simulator.DIR_RIGHT] == null);
        	bool isFreeY = (input[Simulator.DIR_UP]   == null || input[Simulator.DIR_DOWN]  == null);
        	
        	// NOTE: BIAS maybe need to be randomized which one comes first
        	
        	int freeX = getFreeFlit(Simulator.DIR_LEFT, Simulator.DIR_RIGHT);
      		int freeY = getFreeFlit(Simulator.DIR_DOWN, Simulator.DIR_UP);
        	
        	bool swapX = isFreeX || (turnY == turn_priority(turnY, freeX));
        	bool swapY = isFreeY || (turnX == turn_priority(turnX, freeY));
        	
        	if ((turnY != -1) ^ (turnX != -1))
        	{
        		if (turnY != -1)
        		{
        			if(swapX)
        			{
#if DEBUG
               			Console.WriteLine("\tturning flitY {0}.{1} at node {2} cyc {3}", flitY.packet.ID, flitY.flitNr, coord, Simulator.CurrentRound);
#endif	
        				
						if (turnY == turn_priority(freeX, turnY))
        					swap(freeX, turnY);
        					if (input[freeX] == input[turnY])
		   			throw new Exception("Two flits are the same");
        				//if(input[turnY] != null)
        				//	throw new Exception("Swapping didn't work turnY");
        			}
        			else
        				flitY.missedTurns++;
        		}
        		else if (turnX != -1)
        		{
        			if (swapY)
        			{
#if DEBUG
               			Console.WriteLine("\tturning flitX {0}.{1} at node {2} cyc {3}", flitX.packet.ID, flitX.flitNr, coord, Simulator.CurrentRound);
#endif
						if (turnX == turn_priority(freeY, turnX))
    	    				swap(freeY, turnX);
    	    				if (input[freeY] == input[turnX])
		   			throw new Exception("Two flits are the same");
        				//if(input[turnX] != null)
        				//	throw new Exception("Swapping didn't work turnX");
        			}
        			else
        				flitX.missedTurns++;	
        		}
        	}
        	else if(turnY != -1 && turnX != -1)
        	{
#if DEBUG
        		Console.WriteLine("\tturning flitX {0}.{1} and flitY {2}.{3} at node {4} cyc {5}", flitX.packet.ID, flitX.flitNr, flitY.packet.ID, flitY.flitNr, coord, Simulator.CurrentRound);
#endif	
		   		swap(turnX, turnY);
		   		if (input[turnX] == input[turnY])
		   			throw new Exception("Two flits are the same");
		   		if(input[turnX] == null || input[turnY] == null)
		   			throw new Exception("Flits are turning null");
        		
        		/* //Allow only one to win
        		int winner = turn_priority(flitX, flitY, turnX, turnY);
        		if (winner == turnX)
        		{
        			swap(freeY, turnX);
        			flitY.missedTurns++;
        		}
        		else
        		{
        			swap(freeX, turnY);
        			flitX.missedTurns++;
        		}
        	    */
        	}
        	int finalNullCount = 0;
        	for(int i = 0; i < 4; i++)
        		if(input[i] == null)
        			finalNullCount++;
        	
        	if (nullCount < finalNullCount)
        		throw new Exception("Flits are disappearing");
        	else if(nullCount > finalNullCount)
        		throw new Exception("Flits are duplicating");
        	
        	Flit[] temp = new Flit[5];	
        	for(int i = 0; i < 4; i++)
        	{
        		temp[i] = input[i];
        	}
        		
        	input[Simulator.DIR_UP]    = temp[Simulator.DIR_DOWN];
        	input[Simulator.DIR_DOWN]  = temp[Simulator.DIR_UP];
        	input[Simulator.DIR_LEFT]  = temp[Simulator.DIR_RIGHT];
        	input[Simulator.DIR_RIGHT] = temp[Simulator.DIR_LEFT];
        }
        
        Flit[] input = new Flit[5]; // keep this as a member var so we don't
                                    // have to allocate on every step (why can't
                                     // we have arrays on the stack like in C?)

        protected override void _doStep()
        {
            /* Ejection selection and ejection */
            Flit eject = null;
            eject =  ejectLocalNew();
			
            /* Setup the inputs */
            for (int dir = 0; dir < 4; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                    input[dir] = linkIn[dir].Out;
                    input[dir].inDir = dir;
                }
                else
                    input[dir] = null;
            }
            
            /* Injection */
            Flit inj = null;
            bool injected = false;
			
			/* Pick slot to inject */
            if (m_injectSlot2 != null)
            {
                inj = m_injectSlot2;
                m_injectSlot2 = null;
            }
            else if (m_injectSlot != null)
            {
                inj = m_injectSlot;
                m_injectSlot = null;
            }

            /* Port 4 becomes the injected line */
            input[4] = inj;

            /* If there is data, set the injection direction */
            if (inj != null)
                inj.inDir = -1;

            /* Go thorugh inputs, find their preferred directions */
            for (int i = 0; i < 5; i++)
            {
                if (input[i] != null)
                {
                    PreferredDirection pd = determineDirection(input[i]);
                    if (pd.xDir != Simulator.DIR_NONE)
                        input[i].prefDir = pd.xDir;
                    else
                        input[i].prefDir = pd.yDir;
                }
			}
			
            /* Inject and route flits in correct directions */
            inject(out injected);

#if DEBUG			
			for (int dir = 0; dir < 4; dir++)
				if(input[dir] != null)
        			Console.WriteLine("flit {0}.{1} at node {2} cyc {3} AFTER_INJ", input[dir].packet.ID, input[dir].flitNr, coord, Simulator.CurrentRound);
#endif 	
			
            if (injected && input[4] != null)
            	throw new Exception("Not removing injected flit from slot");
            
            route();
#if DEBUG			
			for (int dir = 0; dir < 4; dir++)
				if(input[dir] != null)
        			Console.WriteLine("flit {0}.{1} at node {2} cyc {3} AFTER_ROUTE", input[dir].packet.ID, input[dir].flitNr, coord, Simulator.CurrentRound);
#endif 	
            //for (int i = 0; i < 4; i++)
            //{
            //    if (input[i] != null)
            //    {
            //        input[i].Deflected = input[i].prefDir != i;
            //    }
            //}
            
            /* If something wasn't injected, move the flit into the injection slots *
             *   If it was injected, take stats                                     */
            if (!injected)
            {
                if (m_injectSlot == null)
                    m_injectSlot = inj;
                else
                    m_injectSlot2 = inj;
            }
            else
                statsInjectFlit(inj);

            /* Put ejected flit in reassembly buffer */
            if (eject != null)
                acceptFlit(eject);
            
            
            /* Assign outputs */
            for (int dir = 0; dir < 4; dir++)
			{
                if (input[dir] != null)
                {
#if DEBUG
        		Console.WriteLine("flit {0}.{1} at node {2} cyc {3} END", input[dir].packet.ID, input[dir].flitNr, coord, Simulator.CurrentRound);
#endif 
                    if (linkOut[dir] == null)
                        throw new Exception(String.Format("router {0} does not have link in dir {1}",
                                    coord, dir));
                    linkOut[dir].In = input[dir];
                }
#if DEBUG
                //else if(dir == Simulator.DIR_LEFT || dir == Simulator.DIR_RIGHT)
      			//	Console.WriteLine("no flit at node {0} cyc {1}", coord, Simulator.CurrentRound);
#endif 
            }
        }

        public override bool canInjectFlit(Flit f)
        {
            return m_injectSlot == null;
        }

        public override void InjectFlit(Flit f)
        {
            if (m_injectSlot != null)
                throw new Exception("Trying to inject twice in one cycle");

            m_injectSlot = f;
        }

        public override void visitFlits(Flit.Visitor fv)
        {
            if (m_injectSlot != null)
                fv(m_injectSlot);
            if (m_injectSlot2 != null)
                fv(m_injectSlot2);
        }

        //protected abstract int rank(Flit f1, Flit f2);
    }

    public class Router_RoR_Random : Router_RoR
    {
        public Router_RoR_Random(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_Random._rank(f1, f2);
        }
    }

    public class Router_RoR_OldestFirst : Router_RoR
    {
        public Router_RoR_OldestFirst(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_OldestFirst._rank(f1, f2);
        }
    }
    
    public class Router_RoR_ClosestFirst : Router_RoR
    {
        public Router_RoR_ClosestFirst(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_ClosestFirst._rank(f1, f2);
        }
    }
    
    public class Router_RoR_GP : Router_RoR
    {
        public Router_RoR_GP(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_GP._rank(f1, f2);
        }
    }
}
