using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    /**
     * This is a one-directional link with some delay (specified in
     * cycles). Will be pumped by the simulator once per cycle.
     *
     * Also includes a sideband (that should flow in the opposite
     * direction) that carries arbitrary objects. Used, for example,
     * for intelligent deflections to notify neighbors of congestion.
     */
    public class Link
    {
    	static int globalID = 0;
    	int ID;
        int m_delay;
        Flit[] m_fifo;
        object[] m_sideband_fifo;

        public Flit In, Out;
        public object SideBandIn, SideBandOut;

        public Coord outCoord;
        public int dir;

        int rdInd, wrInd;
        int rdBndInd, wrBndInd;

        public bool healthy;//models the health status of a link. If failed, In flits are not accepted.

        /**
         * Constructs a Link. Note that delay specifies _additional_ cycles; that is, if
         * delay == 0, then this.In will appear at this.Out after _one_ doStep() iteration.
         */
        public Link(int delay)
        {
            healthy = true;
            m_delay = delay;
            if (m_delay > 0)
                m_fifo = new Flit[m_delay];
            else
                m_fifo = null;

            SideBandOut = SideBandIn = null;
            if (m_delay > 0)
                m_sideband_fifo = new object[m_delay];
            else
                m_sideband_fifo = null;
                
            ID = globalID;
            globalID++;
        }

		public override string ToString()
		{
			return "{" + ID + "}";
		}
		
        public void doStep()
        {
            //In case the link is failed, the router should have not forwarded a packet to it.
            if (healthy == false && In != null)
                throw new Exception("A failed link should not be utilized!");
            else if (healthy == false)
                Out = null;
            else if (In != null && Out != null)
                throw new Exception("Overriding a non-consumed flit!!!");
            if (m_delay > 0)
            {
                Out = m_fifo[0];
                for (int i = 0; i < m_delay - 1; i++)
                    m_fifo[i] = m_fifo[i + 1];
                m_fifo[m_delay - 1] = In;

                SideBandOut = m_sideband_fifo[0];
                for (int i = 0; i < m_delay - 1; i++)
                    m_sideband_fifo[i] = m_sideband_fifo[i + 1];
                m_sideband_fifo[m_delay - 1] = SideBandIn;
            }
            else
            {
                Out = In;
                SideBandOut = SideBandIn;
            }

            if (In != null)
                In.hopCnt++;

            In = null;
            SideBandIn = null;
        }

        public void flush()
        {
            for (int i = 0; i < m_delay; i++)
            {
                m_fifo[i] = null;
                m_sideband_fifo[i] = null;
            }
            Out = null;
            In = null;
            SideBandOut = null;
            SideBandIn = null;
        }

        public void visitFlits(Flit.Visitor fv)
        {
            for (int i = 0; i < m_delay; i++)
                if (m_fifo[i] != null)
                    fv(m_fifo[i]);

            if (Out != null)
                fv(Out);
            if (In != null)
                fv(In);
        }
		public void setDelay(int delay)
		{
			m_delay = delay;
		}

        public void goFaulty()
        {
            healthy = false;
            Console.WriteLine("{0}:{1} -> failed", outCoord, dir);
            if(dir < 2)
            Simulator.newFault += string.Format("{0}<->{1}:failed ", outCoord, Simulator.network.routers[outCoord.ID].neigh[dir].coord);
        }
    }
}
