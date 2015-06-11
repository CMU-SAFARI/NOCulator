//#define DEBUG

using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class Flow
    {
        Node m_n;
        int m_slots;
        Queue<Packet> m_retx;

        public Flow(Node n, int slots)
        {
            m_n = n;
            m_slots = slots;
            m_retx = new Queue<Packet>();
        }
       
        public Packet process(Packet p)
        {
            if (p.flow_close)
            {
                Simulator.stats.flow_close.Add();
                m_slots++;
#if DEBUG
                Console.WriteLine("FLOW {0}: flow close on {1} (now {2} slots free)", m_n.coord, p, m_slots);
#endif
                checkQueue();
                return p;
            }
            else if (p.flow_open)
            {
                Simulator.stats.flow_open.Add();
#if DEBUG
                Console.WriteLine("FLOW {0}: flow open on {1}", m_n.coord, p);
#endif
                if (m_slots > 0)
                {
#if DEBUG
                    Console.WriteLine("FLOW {0}: granted", m_n.coord);
#endif
                    m_slots--;
                    return p;
                }
                else
                {
                    Simulator.stats.flow_retx.Add();

                    m_retx.Enqueue(p);
#if DEBUG
                    Console.WriteLine("FLOW {0}: queued: now {1} entries on queue", m_n.coord, m_retx.Count);
#endif
                    return null;
                }
            }
            else
            {
#if DEBUG
                Console.WriteLine("FLOW {0}: passthrough {1}", m_n.coord, p);
#endif
                return p;
            }
        }

        void checkQueue()
        {
            while (m_retx.Count > 0 && m_slots > 0)
            {
                Packet p = m_retx.Dequeue();
                p.flow_open = false; // implicitly gets the alloc'd slot now
#if DEBUG
                Console.WriteLine("FLOW {0}: sending packet {1} for retx", m_n.coord, p);
#endif
                m_n.sendRetx(p);
                m_slots--;
            }
        }
    }
} 
