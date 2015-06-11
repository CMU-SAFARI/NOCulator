using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class CachePacket : Packet
    {
        public Simulator.Ready cb;
        int m_class;
        int m_VCclass;

        public override int getQueue()
        {
            if (!Config.split_queues) return 0;
            else return m_class;
        }

        public override int getClass()
        {
            return m_VCclass;
        }

        public CachePacket(int reqNode, int from, int to, int flits, int _class, int _vcclass, Simulator.Ready _cb)
            : base(null, 0, flits, new Coord(from), new Coord(to))
        {
            cb = _cb;
            m_class = _class;
            m_VCclass = mapClass(_vcclass);
            requesterID = reqNode;
        }

        public override string ToString()
        {
            return String.Format("pkt from {0} to {1} size {2}", src, dest, nrOfFlits);
        }

        protected int mapClass(int vn)
        {
            if (Config.afc_real_classes)
            {
                // second virtual network for a given class if the destination is
                // lexicographically smaller than the source. This breaks deadlock
                // cycles (see [Dally86]).
                int flip = ((dest.x < src.x || (dest.x == src.x && dest.y < src.y))) ? 1 : 0;

                switch (vn)
                {
                    case 0: vn = 0 + flip; break;
                    case 1: vn =  2 + flip; break;
                    case 4: vn =  4 + flip; break;
                    case 5: vn =  6 + flip; break;
                    default: vn =  0 + flip; break;
                }

                return vn;
            }
            else
                return Simulator.rand.Next(Config.afc_vnets);
        }
    }

    public class RetxPacket : Packet
    {
        public Packet pkt;

        public RetxPacket(Coord src, Coord dest, Packet p)
            : base(null, 0, 1, src, dest)
        {
            pkt = p;
        }
    }

    public class SynthPacket : Packet
    {
        public int m_class;

        public SynthPacket(Coord src, Coord dest)
            : base(null, 0, 1, src, dest)
        {
            requesterID = src.ID;
            m_class = computeClass();
        }

        protected int computeClass()
        {
            int flip = ((dest.x < src.x || (dest.x == src.x && dest.y < src.y))) ? 1 : 0;
            if (Config.afc_vnets == 1) return 0;
            else
            {
                int msgclass = Simulator.rand.Next(Config.afc_vnets / 2);
                int vc = msgclass*2 + flip;
                if (vc >= Config.afc_vnets) vc = Config.afc_vnets - 1; // odd case
                return vc;
            }
        }

        public override int getClass()
        {
            return m_class;
        }
    }
}
