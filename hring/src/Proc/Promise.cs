//#define DEBUG

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.IO;

namespace ICSimulator
{
    
    public class Promise
    {
        public int slot;
        public bool active;
        public int id;
        public PromisedRsrc owner;

        static int _id = 0;

        public Promise(PromisedRsrc owner, int s)
        {
            this.owner = owner;
            slot = s; active = false; id = _id++;
        }

        public override string ToString()
        {
            return String.Format("promise(slot {0}, owner {1}, id {2})", slot, owner, id);
        }
    }

    public class PromisedRsrc
    {
        public int N;
        public Queue<Promise> promises;

        class Slot
        {
            public bool used; // slot currently alloc'd?
            public bool promise; // alloc used promise to grab it?
            public EvictHandler handler; // evict handler if no promise used
        }
        Slot[] slots;

        protected delegate void EvictHandler();

        public PromisedRsrc(int N)
        {
            this.N = N;

            promises = new Queue<Promise>();
            slots = new Slot[N];
            for (int i = 0; i < N; i++)
            {
                promises.Enqueue(new Promise(this, i));
                slots[i] = new Slot();
                slots[i].used = false;
                slots[i].promise = false;
                slots[i].handler = null;
            }
        }

        public Promise getPromise()
        {
#if DEBUG
            Console.WriteLine("allocing promise, count was {0}", promises.Count);
#endif
            if (promises.Count > 0)
            {
                Promise ret = promises.Dequeue();
                ret.active = true;
#if DEBUG
                Console.WriteLine("promise {0} allocated", ret);
#endif
                return ret;
            }
            else
            {
#if DEBUG
                Console.WriteLine("out of promises at {0}", this);
#endif
                return null;
            }
        }

        public void putPromise(Promise p)
        {
#if DEBUG
            Console.WriteLine("putting promise {0} back, count was {1}", p, promises.Count);
#endif
            if (!p.active)
                throw new Exception("returning inactive promise!");

            p.active = false;
            promises.Enqueue(p);
        }

        // allocates a slot; p can be null; returns slot index
        protected int alloc(Promise p, EvictHandler h)
        {
            // using promise to alloc?
            if (p != null)
            {
                // yes: clear out contents if there are any
                if (slots[p.slot].used)
                {
                    // shouldn't already have promised this slot
                    if (slots[p.slot].promise)
                        throw new Exception("attempting to use promise on already-promised slot!");

                    // evict speculative contents...
                    slots[p.slot].handler();
                    slots[p.slot].used = false;
                }

                slots[p.slot].used = true;
                slots[p.slot].promise = true;
                slots[p.slot].handler = null;

                return p.slot;
            }
            else
            {
                // no: attempt to grab a free slot if there is one
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].used)
                    {
                        slots[i].used = true;
                        slots[i].promise = false;
                        slots[i].handler = h;
                        return i;
                    }
                }

                // no free slots
                return -1;
            }
        }

        protected void free(int slot)
        {
            slots[slot].used = false;
            slots[slot].promise = false;
            slots[slot].handler = null;
        }
    }
}
