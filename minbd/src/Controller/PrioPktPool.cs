using System;
using System.Collections.Generic;

namespace ICSimulator
{
    /* a Priority Packet Pool is an abstract packet container that
     * implements a priority/reordering/scheduling. Intended for use
     * with injection queues, and possibly in-network buffers, that are
     * nominally FIFO but could potentially reorder.
     */
    public interface IPrioPktPool
    {
        void addPacket(Packet pkt);
        void setNodeId(int id);
        Packet next();
        int Count { get; }
        int FlitCount { get; }
    }

    /* simple single-FIFO packet pool. */
    public class FIFOPrioPktPool : IPrioPktPool
    {
        Queue<Packet> queue;
        int flitCount = 0;

        public FIFOPrioPktPool()
        {
            queue = new Queue<Packet>();
        }

        public void addPacket(Packet pkt)
        {
            flitCount += pkt.nrOfFlits;
            queue.Enqueue(pkt);
        }

        public Packet next()
        {
            if (queue.Count > 0)
            {
                Packet p = queue.Dequeue();
                flitCount -= p.nrOfFlits;
                return p;
            }
            else
                return null;
        }

        public void setNodeId(int id)
        {
        }

        public int Count { get { return queue.Count; } }
        public int FlitCount { get { return flitCount; } }
    }

    /* multi-queue priority packet pool, based on Packet's notion of
     * queues (Packet.numQueues and Packet.getQueue() ): currently
     * Packet implements these based on the cache-coherence protocol,
     * so that control, data, and writeback packets have separate queues.
     */
    public class MultiQPrioPktPool : IPrioPktPool
    {
        Queue<Packet>[] queues;
        int nqueues;
        int queue_next;
        int flitCount = 0;

        public MultiQPrioPktPool()
        {
            nqueues = Packet.numQueues;
            queues = new Queue<Packet>[nqueues];
            for (int i = 0; i < nqueues; i++)
                queues[i] = new Queue<Packet>();
            queue_next = nqueues - 1;
        }

        public void addPacket(Packet pkt)
        {
            flitCount += pkt.nrOfFlits;
            queues[pkt.getQueue()].Enqueue(pkt);
        }

        void advanceRR()
        {
            int tries = nqueues;

            do
                queue_next = (queue_next + 1) % nqueues;
            while (tries-- > 0 && queues[queue_next].Count == 0);
        }

        public Packet next()
        {
            advanceRR();
            if (queues[queue_next].Count > 0)
            {
                Packet p = queues[queue_next].Dequeue();
                flitCount -= p.nrOfFlits;
                return p;
            }
            else
                return null;
        }

        public void setNodeId(int id)
        {
        }

        public int Count
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < nqueues; i++)
                    sum += queues[i].Count;
                return sum;
            }
        }

        public int FlitCount { get { return flitCount; } }
    }
    /**
     * @brief Same as the multiqpriopktpool with throttling on request packet
     * enabled. 
     **/ 
    public class MultiQThrottlePktPool : IPrioPktPool
    {
        Queue<Packet>[] queues;
        int nqueues;
        int queue_next;
        int node_id=-1;
        int flitCount = 0;

        public static IPrioPktPool construct()
        {
            if (Config.cluster_prios_injq)
                return new ReallyNiftyPrioritizingPacketPool();
            else
                return new MultiQThrottlePktPool();
        }

        private MultiQThrottlePktPool()
        {
            nqueues = Packet.numQueues;
            queues = new Queue<Packet>[nqueues];
            for (int i = 0; i < nqueues; i++)
                queues[i] = new Queue<Packet>();
            queue_next = nqueues - 1;
        }

        public void addPacket(Packet pkt)
        {
            flitCount += pkt.nrOfFlits;
            queues[pkt.getQueue()].Enqueue(pkt);
        }

        void advanceRR()
        {
            int tries = nqueues;

            do
                queue_next = (queue_next + 1) % nqueues;
            while (tries-- > 0 && queues[queue_next].Count == 0);
        }

        public Packet next()
        {
            if(node_id==-1)
                throw new Exception("Haven't configured the packet pool");
            advanceRR();
            
            //only queues for non-control packets go proceed without any 
            //throttling constraint
            if (queues[queue_next].Count > 0 &&
                    (queue_next!=0 || Simulator.controller.tryInject(node_id)) )
            {
                Packet p = queues[queue_next].Dequeue();
                flitCount -= p.nrOfFlits;
                return p; 
            }
            else
                return null;
        }

        public void setNodeId(int id)
        {
            node_id=id;
        }

        public int Count
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < nqueues; i++)
                    sum += queues[i].Count;
                return sum;
            }
        }

        public int FlitCount { get { return flitCount; } }
    }


    /** @brief the Really Nifty Prioritizing Packet Pool respects full
     * priorities (i.e., by using a min-heap) and also optionally respects
     * throttling.
     */
    public class ReallyNiftyPrioritizingPacketPool : IPrioPktPool
    {
        class HeapNode : IComparable
        {
            public Packet pkt;
            public HeapNode(Packet p) { pkt = p; }
            public int CompareTo(object o)
            {
                if (o is HeapNode)
                    return Simulator.controller.rankFlits(pkt.flits[0], (o as HeapNode).pkt.flits[0]);
                else
                    throw new ArgumentException("bad comparison");
            }
        }

        MinHeap<HeapNode>[] heaps;
        int nheaps;
        int heap_next;
        int flitCount = 0;
        int node_id = -1;

        public ReallyNiftyPrioritizingPacketPool()
        {
            nheaps = Packet.numQueues;
            heaps = new MinHeap<HeapNode>[nheaps];
            for (int i = 0; i < nheaps; i++)
                heaps[i] = new MinHeap<HeapNode>();
            heap_next = nheaps - 1;
        }

        public void addPacket(Packet pkt)
        {
            flitCount += pkt.nrOfFlits;
            HeapNode h = new HeapNode(pkt);
            heaps[pkt.getQueue()].Enqueue(h);
        }

        void advanceRR()
        {
            int tries = nheaps;

            do
                heap_next = (heap_next + 1) % nheaps;
            // keep incrementing while we're at an empty queue or at req-queue and throttling
            while (tries-- > 0 &&
                    ((heaps[heap_next].Count == 0) ||
                     (heap_next == 0 && !Simulator.controller.tryInject(node_id))));
        }

        public Packet next()
        {
            advanceRR();
            
            //only queues for non-control packets go proceed without any 
            //throttling constraint
            if (heaps[heap_next].Count > 0 &&
                    (heap_next!=0 || Simulator.controller.tryInject(node_id)) )
            {
                HeapNode h = heaps[heap_next].Dequeue();
                Packet p = h.pkt;
                flitCount -= p.nrOfFlits;
                return p; 
            }
            else
                return null;
        }

        public void setNodeId(int id)
        {
            node_id = id;
        }

        public int Count
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < nheaps; i++)
                    sum += heaps[i].Count;
                return sum;
            }
        }

        public int FlitCount { get { return flitCount; } }
    }

}
