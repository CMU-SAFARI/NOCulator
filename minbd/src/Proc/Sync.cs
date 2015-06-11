using System;
using System.Collections.Generic;

namespace ICSimulator
{
    class Syncer
    {
        ulong[] m_labels; // synchronization labels (dependence arc points)

        Dictionary<ulong, bool> m_locks; // mapping: lock addr -> lock held

        class BarrierRec
        {
            public int count;      // barrier count (counts up)
            public bool[] awaken;  // signal from waker to wakee
            public bool[] waiting; // wakee's state

            public BarrierRec()
            {
                awaken = new bool[Config.N];
                waiting = new bool[Config.N];
            }
        }

        Dictionary<ulong, BarrierRec> m_barriers; // barriers (by barrier-var addr)

        public Syncer()
        {
            m_labels = new ulong[Config.N];
            m_locks = new Dictionary<ulong, bool>();
            m_barriers = new Dictionary<ulong, BarrierRec>();
        }

        // each of these routines corresponds one-to-one to a trace record
        // type, and returns True to consume record and continue or False
        // to block.

        public bool Label(int core, int from, ulong addr)
        {
            // update the latest label reached on this core
            m_labels[core] = addr;
            return true;
        }

        public bool Sync(int core, int from, ulong addr)
        {
            // can continue only if other core has passed the
            // given label point
            return m_labels[from] >= addr;
        }

        public bool Lock(int core, int from, ulong addr)
        {
            // dynamically expand the set of known locks
            if (!m_locks.ContainsKey(addr))
                m_locks[addr] = false;

            // if lock is taken, block; otherwise, take it and continue
            if (m_locks[addr]){
                return false;                
            }
            else
            {
                m_locks[addr] = true;
                Console.WriteLine("Lock" + '\t' + core.ToString() + '\t' + addr.ToString());
                return true;
            }
        }

        public bool Unlock(int core, int from, ulong addr)
        {
            // mark lock as free and return
            m_locks[addr] = false;
            Console.WriteLine("Unlock" + '\t' + core.ToString() + '\t' + addr.ToString());
            return true;
        }

        public bool Barrier(int core, int from, ulong addr)
        {
            BarrierRec b;
            if (m_barriers.ContainsKey(addr))
                b = m_barriers[addr];
            else
            {
                b = new BarrierRec();
                m_barriers[addr] = b;
            }

            if (b.waiting[core])
            {
                if (b.awaken[core])
                {
                    b.waiting[core] = false;
                    b.awaken[core] = false;
                    //Console.WriteLine("Progress: " + core.ToString());
                }
            }
            else
            {
                b.waiting[core] = true;
                b.count++;
                Console.WriteLine("Barrier: " + core.ToString() + ' ' + addr.ToString() + ' ' + b.count.ToString());
                
                /*
                string waiting = "";
                string awaken = "";

                for (int i = 0; i < Config.N; i++){
                    waiting += (b.waiting[i]).ToString() + " ";
                    awaken += (b.awaken[i]).ToString() + " ";
                }
                Console.WriteLine("Barrier.waiting " + waiting);
                Console.WriteLine("Barrier.awaken  " + awaken);
                */
                if (b.count == from)
                {
                    Console.WriteLine("Waking");
                    b.count = 0;
                    b.waiting[core] = false;
                    for (int i = 0; i < b.awaken.Length; i++)
                        b.awaken[i] = true;
                    b.awaken[core] = false;

                    Simulator.CurrentBarrier++;
                }
                
                /*
                waiting = "";
                awaken = "";
                for (int i = 0; i < Config.N; i++){
                    waiting += (b.waiting[i]).ToString() + " ";
                    awaken += (b.awaken[i]).ToString() + " ";
                }
                Console.WriteLine("Barrier.waiting " + waiting);
                Console.WriteLine("Barrier.awaken  " + awaken);
                Console.WriteLine("=============");
                */
            }

            return ! b.waiting[core];
        }
    }
}
