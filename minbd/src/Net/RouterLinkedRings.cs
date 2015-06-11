//#define DEBUG
using System;
using System.Collections.Generic;

namespace ICSimulator
{
	public class SwapNode
    {
        public delegate int  Rank(Flit f1, Flit f2);

        Rank   m_r;
		public bool changeRing;
		public bool swapped;
		
		public Flit in_port, out_port, inject_port, eject_port;
		
        public SwapNode(Rank r)
        {
            m_r = r;
        }
        
        public void doStep()
        {
        	if (eject_port != null)
        		throw new Exception("Ejection port not already empty");
        	if (out_port != null)
        		throw new Exception("Out port not already empty");
        		
        	if(changeRing || (inject_port != null && m_r(in_port, inject_port) > 1))
        	{
        		out_port   = inject_port;
        		eject_port = in_port;
        	}
        	else
        	{
        		out_port   = in_port;
        		eject_port = inject_port;
        	}
        }
    }

	// Linked Rings
    public abstract class Router_LinkedRings : Router
    {
        // injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;

        public Router_LinkedRings(Coord myCoord)
            : base(myCoord)
        {
        		
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
            return ret;
		}

  		/*
  		 * Injects flits
  		 */      
        void inject(out bool injected)
        {
        	injected = false;
        	
        	if (input[4] != null)
        	{
        		int i = (Simulator.rand.Next(2) == 1) ? Simulator.DIR_RIGHT : Simulator.DIR_LEFT;
    			if (input[i] == null)
    			{
    				injected = true;
    				input[i] = input[4];
    				input[4] = null;
    				return;
    			}
        	}
        }
        
        void swap(int dir1, int dir2)
        {
        	Flit tempFlit = input[dir1];
        	input[dir1] = input[dir2];
        	input[dir2] = tempFlit;
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
        	Flit[] temp = new Flit[5];	
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

    public class Router_LinkedRings_Random : Router_LinkedRings
    {
        public Router_LinkedRings_Random(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_Random._rank(f1, f2);
        }
    }

    public class Router_LinkedRings_OldestFirst : Router_LinkedRings
    {
        public Router_LinkedRings_OldestFirst(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_OldestFirst._rank(f1, f2);
        }
    }
}
