//#define DEBUG

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;

namespace ICSimulator
{
    /*
    public class RxBuf : PromisedRsrc
    {
        private Dictionary<Packet, int> mapping;

        public delegate void DeliveryGuy(Packet p); // called when a packet is complete
        public delegate void GarbageMan(Packet p); // called when an incomplete packet is killed

        private DeliveryGuy m_dg;
        private GarbageMan m_gm;

        public string name;

        public int Count { get { return promises.Count; } }

        public RxBuf(int nSlots, DeliveryGuy dg, GarbageMan gm, string name)
            : base(nSlots)
        {
            m_dg = dg;
            m_gm = gm;
            mapping = new Dictionary<Packet, int>();

            this.name = name;
        }

       public void deliverFlit(Flit f)
       {
            // short path: single-flit (control) packets
            if (f.packet.nrOfFlits == 1)
            {
                if (f.packet._promise != null)
                {
                    putPromise(f.packet._promise);
                    f.packet._promise = null;
                }

                m_dg(f.packet);
                return;
            }

            int slot;
            if (!mapping.TryGetValue(f.packet, out slot))
                slot = alloc(f.packet._promise, delegate()
                        {
                            m_gm(f.packet);
                        });

            if (slot == -1)
            {
                m_gm(f.packet);
                return;
            }

            mapping[f.packet] = slot;

            f.packet.nrOfArrivedFlits++;
            if (f.packet.nrOfArrivedFlits == f.packet.nrOfFlits)
            {
                free(slot);
                mapping.Remove(f.packet);
                putPromise(f.packet._promise);

#if DEBUG
                Console.WriteLine("reclaimed promise {0} from packet {1}, count now {2}", f.packet._promise, f.packet, promises.Count);
#endif

                f.packet._promise = null;

                m_dg(f.packet);
            }
        }

       public override string ToString()
       {
           return name;
       }
    }
*/
} 
