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
        int m_delay;
        Flit[] m_fifo;
        int[][] m_sideband_fifo;

        public Flit In, Out;
        public int[] SideBandIn = new int [2];
		public int[] SideBandOut = new int [2];

        /**
         * Constructs a Link. Note that delay specifies _additional_ cycles; that is, if
         * delay == 0, then this.In will appear at this.Out after _one_ doStep() iteration.
         */
        public Link(int delay)
        {
            m_delay = delay;
            if (m_delay > 0)
                m_fifo = new Flit[m_delay];
            else
                m_fifo = null;

			for (int n = 0; n < 2; n++)
			{			
				SideBandOut[n] = -1;
				SideBandIn[n] = -1;
			}
            if (m_delay > 0)
            {
				m_sideband_fifo = new int[m_delay][];
				for (int n = 0; n < m_delay; n++)
				{
					m_sideband_fifo[n] = new int [2];
					m_sideband_fifo[n][0] = -1;
					m_sideband_fifo[n][1] = -1;
				}
			}
            else
                m_sideband_fifo = null;
        }

        public void doStep()
        {
            if (m_delay > 0)
            {
                Out = m_fifo[0];
                for (int i = 0; i < m_delay - 1; i++)
                    m_fifo[i] = m_fifo[i + 1];
                m_fifo[m_delay - 1] = In;
                SideBandOut[0] = m_sideband_fifo[0][0];
                SideBandOut[1] = m_sideband_fifo[0][1];
                for (int i = 0; i < m_delay - 1; i++)
                {
					m_sideband_fifo[i][0] = m_sideband_fifo[i + 1][0];
					m_sideband_fifo[i][1] = m_sideband_fifo[i + 1][1];
				}
                m_sideband_fifo[m_delay - 1][0] = SideBandIn[0];
                m_sideband_fifo[m_delay - 1][1] = SideBandIn[1];
			}
            else
            {
                Out = In;
                SideBandOut[1] = SideBandIn[1];
                SideBandOut[0] = SideBandIn[0];
            }
			SideBandIn[0] = -1;
			SideBandIn[1] = -1;
            In = null;
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
    }
}
