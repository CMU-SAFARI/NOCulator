using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class IdealRouter : Router
    {
        Queue<Flit>[] m_queues;
        int m_width, m_length;
        Flit m_injectSlot;

        public IdealRouter(Coord myCoord)
            : base(myCoord)
        {
            m_width = Config.ideal_router_width;
            m_length = Config.ideal_router_length;
            m_queues = new Queue<Flit>[m_width];
            for (int i = 0; i < m_width; i++)
                m_queues[i] = new Queue<Flit>();
        }

        public override bool canInjectFlit(Flit f)
        {
            return m_injectSlot == null;
        }

        public override void InjectFlit(Flit f)
        {
            if (m_injectSlot == null)
                m_injectSlot = f;
            else
                throw new Exception("Could not inject flit!");
        }

        int freeSpace()
        {
            int c = 0;
            for (int i = 0; i < m_width; i++)
                if (m_queues[i].Count < m_length)
                    c += m_length - m_queues[i].Count;
            return c;
        }

        protected override void _doStep()
        {
            int slots = freeSpace();
            Flit[] input = new Flit[slots];
            int idx = 0;

            for (int i = 0; i < 4; i++)
                if (linkIn[i] != null && linkIn[i].Out != null)
                {
                    if (idx == slots) throw new Exception("got too many flits");
                    input[idx++] = linkIn[i].Out;
                    linkIn[i].Out = null;
                }
            if (idx < slots && m_injectSlot != null)
            {
                input[idx++] = m_injectSlot;
                m_injectSlot = null;
            }
        }
    }
}
