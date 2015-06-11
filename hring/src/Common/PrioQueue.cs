using System;

namespace ICSimulator
{
    public class PrioQueue<T>
    {
        struct Node
        {
            public T data;
            public ulong prio;
        }

        Node[] data;
        int count;
        int capacity;

        public int Count { get { return count - 1; } }
        public bool Empty { get { return count == 1; } }

        public PrioQueue()
        {
            count = 1; // the root is at 1
            capacity = 16;
            data = new Node[capacity];
        }

        void grow()
        {
            Node[] old_data = data;
            capacity *= 2;
            data = new Node[capacity];
            Array.Copy(old_data, data, old_data.Length);
        }

        int parent(int idx)
        {
            return idx / 2;
        }

        int child0(int idx)
        {
            return idx*2;
        }

        int child1(int idx)
        {
            return idx*2 + 1;
        }

        void bubble_up(int idx)
        {
            if (idx == 1) return;

            if (data[idx].prio < data[parent(idx)].prio)
            {
                Node temp;
                temp = data[idx];
                data[idx] = data[parent(idx)];
                data[parent(idx)] = temp;

                bubble_up(parent(idx));
            }
        }

        void bubble_down(int idx)
        {
            int c0 = child0(idx), c1 = child1(idx);
            if ((c0 < count) &&
                (data[idx].prio > data[c0].prio))
            {
                Node temp;
                temp = data[c0];
                data[c0] = data[idx];
                data[idx] = temp;
                bubble_down(c0);
            }
            if ((c1 < count) &&
                (data[idx].prio > data[c1].prio))
            {
                Node temp;
                temp = data[c1];
                data[c1] = data[idx];
                data[idx] = temp;
                bubble_down(c1);
            }
        }

        public void Enqueue(T dat, ulong prio)
        {
            if (count == capacity) grow();
            
            data[count].data = dat;
            data[count].prio = prio;

            bubble_up(count);

            count++;
        }

        public ulong MinPrio
        {
            get
            {
                if (count == 1) return 0;
                else return data[1].prio;
            }
        }

        public T Peek()
        {
            if (count == 1) return default(T);
            return data[1].data;
        }

        public T Dequeue()
        {
            if (count == 1) return default(T);

            count--;
            Node temp;
            temp = data[1];
            data[1] = data[count];
            bubble_down(1);

            return temp.data;
        }
    }

/*
    public class Test
    {
        static void Assert(bool x)
        {
            if (!x) throw new Exception("bad assert!");
        }
        
        public static void Main()
        {
            PrioQueue<string> q = new PrioQueue<string>();

            Assert(q.Empty);
            Assert(q.Count == 0);
            Assert(q.MinPrio == 0);

            q.Enqueue("hi", 4);
            Assert(q.MinPrio == 4);
            Assert(q.Count == 1);
            Assert(!q.Empty);

            q.Enqueue("hi2", 2);
            Assert(q.MinPrio == 2);
            Assert(q.Count == 2);
            Assert(!q.Empty);

            q = new PrioQueue<string>();

            for (int i = 100; i > 0; i--)
                q.Enqueue(String.Format("string {0}", i), (ulong)i);

            for (int i = 1; i <= 100; i++)
            {
                Assert(!q.Empty);
                Assert(q.Count == 100-i+1);
                Assert(q.MinPrio == (ulong)i);
                Assert(q.Dequeue() == String.Format("string {0}", i));
            }

            Assert(q.Empty);
        }
    }
*/
}
