using System;
using System.Collections.Generic;

namespace ICSimulator
{
    /* STC */
    public class PktPool_STC : IPrioPktPool
    {
        STC stc;
        Queue<Packet>[] queues;
        int nqueues;
        int flitCount = 0;

        public PktPool_STC(STC _stc)
        {
            stc = _stc;
            nqueues = Config.STC_binCount;
            queues = new Queue<Packet>[nqueues];
            for (int i = 0; i < nqueues; i++)
                queues[i] = new Queue<Packet>();
        }

        public void addPacket(Packet pkt)
        {
            flitCount += pkt.nrOfFlits;
            if (pkt.requesterID == -1)
                queues[0].Enqueue(pkt);
            else
                queues[stc.priorities[pkt.requesterID]].Enqueue(pkt);
        }

        public Packet next()
        {
            for (int i = nqueues - 1; i >= 0; i--)
                if (queues[i].Count > 0)
                {
                    Packet p = queues[i].Dequeue();
                    flitCount -= p.nrOfFlits;
                    return p;
                }
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
                for (int i = 0; i < nqueues; i++) sum += queues[i].Count;
                return sum;
            }
        }

        public int FlitCount { get { return flitCount; } }
    }

    public class Controller_STC : Controller_ClassicBLESS
    {
        STC stc = new STC();

        public override IPrioPktPool newPrioPktPool(int node)
        {
            return new PktPool_STC(stc);
        }

        int cmp(int a, int b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        int cmp(ulong a, ulong b)
        {
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public override int rankFlits(Flit f1, Flit f2)
        {
            // rule 1: older batch (lower batchID) is greater
            int cmp1 = 0;
            if (f1.packet.batchID != f2.packet.batchID)
                cmp1 = - cmp(f1.packet.batchID, f2.packet.batchID);

            // rule 2: higher STC priority is greater
            int cmp2 = 0;
            if (f1.packet.requesterID != -1 &&
                    f2.packet.requesterID != -1 &&
                    f1.packet.requesterID != f2.packet.requesterID)
                cmp2 = cmp(stc.priorities[f1.packet.requesterID], stc.priorities[f2.packet.requesterID]);

            // rule 3: older packet is greater
            int cmp3 = cmp(f1.packet.creationTime, f2.packet.creationTime);

            // rule 4: packet ID
            int cmp4 = cmp(f1.packet.ID, f2.packet.ID);

            // rule 5: lower-sequence flit is greater
            int cmp5 = cmp(f1.flitNr, f2.flitNr);

            if (cmp1 != 0) return cmp1;
            if (cmp2 != 0) return cmp2;
            if (cmp3 != 0) return cmp3;
            if (cmp4 != 0) return cmp4;
            return cmp5;
        }

        public override void doStep()
        {
            if (Simulator.CurrentRound > 0 && Simulator.CurrentRound % Config.STC_period == 0)
                stc.coordinate();

            base.doStep();
        }
    }
}
