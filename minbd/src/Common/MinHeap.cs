
using System;

namespace ICSimulator
{
    public class MinHeap<T> where T : IComparable
    {
        T[] data;
        int count;
        int capacity;

        public int Count { get { return count - 1; } }
        public bool Empty { get { return count == 1; } }

        public MinHeap()
        {
            count = 1; // the root is at 1
            capacity = 16;
            data = new T[capacity];
        }

        void grow()
        {
            T[] old_data = data;
            capacity *= 2;
            data = new T[capacity];
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

            if (data[idx].CompareTo(data[parent(idx)]) < 0)
            {
                T temp;
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
                (data[idx].CompareTo(data[c0]) > 0))
            {
                T temp;
                temp = data[c0];
                data[c0] = data[idx];
                data[idx] = temp;
                bubble_down(c0);
            }
            if ((c1 < count) &&
                (data[idx].CompareTo(data[c1]) > 0))
            {
                T temp;
                temp = data[c1];
                data[c1] = data[idx];
                data[idx] = temp;
                bubble_down(c1);
            }
        }

        public void Enqueue(T dat)
        {
            if (count == capacity) grow();
            
            data[count] = dat;

            bubble_up(count);

            count++;
        }

        public T Peek()
        {
            if (count == 1) return default(T);
            return data[1];
        }

        public T Dequeue()
        {
            if (count == 1) return default(T);

            count--;
            T temp;
            temp = data[1];
            data[1] = data[count];
            bubble_down(1);

            return temp;
        }
    }
}
