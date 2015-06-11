#define debug

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    /**
     * @brief Buffered Bridge Router 
     * 1. Make sure a flit can inject when there's no flit coming in. This should be fine.
     * TODO: the livelock guarantee doesn't work in buffered b/c flits stop traversing when and
     * wait for the slot to be opened.
     **/ 
    public class Router_Bridge_Buffer : Router
    {
        int bufferDepth_G2L = Config.G2LBufferDepth;
        int bufferDepth_L2G = Config.L2GBufferDepth;
        Queue<Flit>[] Q_G2L;
        Queue<Flit>[] Q_L2G;
        int Local = 0;
        int Global = 1;
        public int GWidth;
        public int LWidth;
        int localRR;
        int globalRR;
        Flit [] local_observer;
        Flit [] global_observer;
        int [] local_obCounter;
        int [] global_obCounter;
        int [] local_loopCounter;
        int [] global_loopCounter;
        Flit [] local_preserve;
        Flit [] global_preserve;
        int local_loopCycle = -1;
        int global_loopCycle = -1;

        int [] local_starveCounter;
        int [] global_starveCounter;

        public Router_Bridge_Buffer(int ID, int GlobalRingWidth, int LocalRingWidth = 1, int L2GBufferDepth = -1, int G2LBufferDepth = -1) : base()
        {
            if (GlobalRingWidth == 4)
                RouterType = 2;
            else if (GlobalRingWidth == 2)
                RouterType = 1;
            enable = true;
            m_n = null;
            coord.ID = ID;
            if (L2GBufferDepth != -1)
                bufferDepth_L2G = L2GBufferDepth;
            if (G2LBufferDepth != -1)
                bufferDepth_G2L = G2LBufferDepth;
            LLinkIn = new Link[LocalRingWidth * 2];
            LLinkOut = new Link[LocalRingWidth * 2];
            GLinkIn = new Link[2 * GlobalRingWidth];
            GLinkOut = new Link[2 * GlobalRingWidth];
            Q_G2L = new Queue<Flit>[2 * GlobalRingWidth];
            Q_L2G = new Queue<Flit>[LocalRingWidth * 2];
            this.GWidth = GlobalRingWidth;
            this.LWidth = LocalRingWidth;
            // for livelock freedom
            local_observer = new Flit[2 * LWidth];
            global_observer = new Flit[2 * GWidth];
            local_preserve = new Flit[2 * LWidth];
            global_preserve = new Flit[2 * GWidth];
            local_obCounter = new int[2 * LWidth];
            global_obCounter = new int[2 * GWidth];
            local_loopCounter = new int[2 * LWidth];
            global_loopCounter = new int [2 * GWidth];

            local_starveCounter = new int[2 * LWidth];
            global_starveCounter = new int[2 * GWidth];
            localRR = 0;
            globalRR = 0;
            for (int n = 0; n < 2 * LWidth; n++)
            {
                Q_L2G[n] = new Queue<Flit>();
                local_starveCounter[n] = 0;
            }
            for (int n = 0; n < 2 * GlobalRingWidth; n++)
            {
                Q_G2L[n] = new Queue<Flit>();
                global_starveCounter[n] = 0;
            }
            if (Config.simpleLivelock)
                switch (Config.topology)
                {
                    case Topology.HR_4drop : {local_loopCycle = 5; global_loopCycle = 8; break;}
                    case Topology.HR_8drop : {local_loopCycle = 6; global_loopCycle = 16; break;}
                    case Topology.HR_16drop : {local_loopCycle = 8; global_loopCycle = 16; break;}
                }
        }

        public override bool productive(Flit f, int level)
        {
            int RingID = -1;
            switch (Config.topology)
            {
                case Topology.HR_4drop : {RingID = ID; break;}
                case Topology.HR_8drop : {RingID = ID / 2; break;}
                case Topology.HR_16drop : {RingID = ID / 4; break;}
                case Topology.HR_8_16drop : {RingID = ID / 4; break;}
                case Topology.HR_8_8drop : {RingID = ID / 2; break;}
                case Topology.HR_16_8drop : {RingID = ID / 2; break;}
                case Topology.HR_32_8drop : {RingID = ID / 2; break;}
            }
            if (Config.N == 16 || GWidth == 2)
            {
                if (level == Local)
                    return f.packet.dest.ID / 4 != RingID;
                else if (level == Global)
                    return f.packet.dest.ID / 4 == RingID;
                else
                    throw new Exception("undefined level!");
            }
            else if (GWidth >= 4)
            {
                if (level == Local)
                    return f.packet.dest.ID / GWidth / GWidth != ID / 2;
                else
                    return f.packet.dest.ID / GWidth / GWidth == ID / 2;
            }
            else
                throw new Exception("Topology not supported!");
        }

        public override bool creditAvailable(Flit f)
        {
            int dir = f.packet.pktParity;
#if debug
            Console.WriteLine("BRIDGE check credit cyc {0} -> flit ID {1} nr {2} dir {3}.", Simulator.CurrentRound,
                              f.packet.ID, f.flitNr, dir);
#endif
            if (Q_L2G[dir].Count < bufferDepth_L2G && 
                (local_preserve[dir] == null || local_preserve[dir] == f || Q_L2G[dir].Count < bufferDepth_L2G - 1))
                return true;
            return false;
        }

        protected override void _doStep()
        {
            // TODO: this seems to be an index for going through each ring. 
            localRR = (localRR == LWidth * 2 - 1)? 0 : localRR + 1;
            globalRR = (globalRR == GWidth * 2 - 1)? 0 : globalRR + 1;
            /*			int RingID = -1;
                        switch (Config.topology)
                        {
                        case Topology.HR_4drop: {RingID = ID; break; }
                        case Topology.HR_8drop: {RingID = ID / 2; break;}
                        case Topology.HR_16drop : {RingID = ID / 4; break;}
                        case Topology.HR_8_16drop : {RingID = ID / 4; break;}
                        case Topology.HR_8_8drop : {RingID = ID / 2; break;}
                        }
                        for (int i = 0; i < LWidth * 2; i++)
                        {
                        if (LLinkIn[i].Out != null && LLinkIn[i].Out.packet.src.ID / 4 == RingID)
                        LLinkIn[i].Out.timeInTheSourceRing ++;
                        else if (LLinkIn[i].Out != null && LLinkIn[i].Out.packet.dest.ID != ID && Config.RingEjectTrial == 2 && Config.topology == Topology.HR_4drop)
                        throw new Exception("In the dest Ring, should not pass the bridgeRouter");
                        if (Config.SingleDirRing && LLinkIn[1].Out != null)
                        throw new Exception("CCW shouldn't be used in SingleDirRing");
                        }

                        for (int i = 0; i < 2 * GWidth; i++)
                        {
                        if (GLinkIn[i].Out != null)
                        GLinkIn[i].Out.timeInGR += 2;
                        if (Config.SingleDirRing && GLinkIn[2*  (i/2) + 1].Out != null)
                        throw new Exception("CCW shouldn't be used in SingleDirRing");
                        }
                        */

            // the observer, for livelock freedom
            if (Config.simpleLivelock)
            {
                for (int n = 0; n < 2 * LWidth; n++)
                {
                    if (Config.topology != Topology.HR_8drop)
                        throw new Exception("The observer algorithm only support 8 drop topology");
                    if (ID % 2 == 1) // the odd bridge routers dont observe traffic
                        break;

                    if (local_observer[n] != null && local_obCounter[n] != local_loopCycle)
                        local_obCounter[n] ++;
                    else if (local_observer[n] != null && local_obCounter[n] == local_loopCycle && local_observer[n] == LLinkIn[n].Out)
                    {
                        local_loopCounter[n] ++;
                        local_obCounter[n] = 1;
                        if (local_loopCounter[n] == Config.observerThreshold)
                        {
                            local_preserve[n] = local_observer[n];
                        }
                    }
                    else if (local_observer[n] != null && local_obCounter[n] == local_loopCycle && local_observer[n] != LLinkIn[n].Out)
                    {
                        local_observer[n] = null;
                        local_preserve[n] = null;
                    }
                    else if (local_observer[n] == null && LLinkIn[n].Out != null && productive(LLinkIn[n].Out, Local))
                    {
                        local_preserve[n] = null;
                        local_observer[n] = LLinkIn[n].Out;
                        local_obCounter[n] = 1;
                        local_loopCounter[n] = 0;
                    }
                }
                for (int n = 0; n < 2*GWidth; n++)
                {
                    if (Config.topology != Topology.HR_8drop)
                        throw new Exception("The observer algorithm only support 8 drop topology");
                    if (ID % 2 == 1)
                        break;
                    if (global_observer[n] != null && global_obCounter[n] != global_loopCycle)
                        global_obCounter[n] ++;
                    else if (global_observer[n] != null && global_obCounter[n] == global_loopCycle && global_observer[n] == GLinkIn[n].Out)
                    {
                        global_loopCounter[n] ++;
                        global_obCounter[n]  = 1;
                        if (global_loopCounter[n] == Config.observerThreshold)
                        {
                            global_preserve[n] = global_observer[n];
                        }
                    }
                    else if (global_observer[n] != null && global_obCounter[n] == global_loopCycle && global_observer[n] != GLinkIn[n].Out)
                    {
                        global_observer[n] = null;
                        global_preserve[n] = null;
                    }
                    else if (global_observer[n] == null && GLinkIn[n].Out != null && productive(GLinkIn[n].Out, Global))
                    {
                        global_preserve[n] = null;
                        global_observer[n] = GLinkIn[n].Out;
                        global_obCounter[n] = 1;
                        global_loopCounter[n] = 0;
                    }
                }
            }


            ulong Ldeflected = 0;
            ulong Gdeflected = 0;
            ulong G2Lcrossed = 0;
            ulong L2Gcrossed = 0;
            for (int n = 0; n < 2 * LWidth; n++)  // load local traffic into buffer if productive
            {
                Flit f = LLinkIn[n].Out;

                /* 
                 * Try to enq local traffic into the global ring
                 * productive(flit, level); level = 0 (local) or 1 (global)
                 * */
                if (f != null && productive(f, Local) && Q_L2G[n].Count < bufferDepth_L2G && (local_preserve[n] == null || local_preserve[n] == LLinkIn[n].Out || Q_L2G[n].Count < bufferDepth_L2G - 1))
                {
                    Q_L2G[n].Enqueue(f);
                    L2Gcrossed ++;

                    // local preserve is set in the simple live lock code
                    if (local_preserve[n] == LLinkIn[n].Out)
                    {
                        local_preserve[n] = null;
                        local_observer[n] = null;
                    }
                    LLinkIn[n].Out = null;
                }
                else
                {
                    // TODO: no buffer space and some other condition, deflect. Double check that this case does
                    // not happen to buffered local ring.
                    if (f != null && productive(f, Local))
                        throw new Exception("Flit is deflected from local to global");
                        //Ldeflected ++;
                    LLinkOut[n].In = f;
                    LLinkIn[n].Out = null;
                }
            }
            
            // TODO: double check if downstream router_node has available space. Similar to injection check in 
            // router_node.
            for (int n = 0; n < GWidth * 2; n++) // load global traffic into buffer if productive
            {
                Flit f = GLinkIn[n].Out;
                if (f != null && productive(f, Global) && Q_G2L[n].Count < bufferDepth_G2L && (global_preserve[n] == null || global_preserve[n] == GLinkIn[n].Out || Q_G2L[n].Count < bufferDepth_G2L - 1))
                {
                    G2Lcrossed ++;
                    Q_G2L[n].Enqueue(f);
                    if (global_preserve[n] == GLinkIn[n].Out)
                    {
                        global_preserve[n] = null;
                        global_observer[n] = null;
                    }
                    GLinkIn[n].Out = null;
                }
                else
                {
                    if (f != null && productive(f, Global))
                        throw new Exception("Flit is deflected from local to global");
                        //Gdeflected ++;
                    GLinkOut[n].In = f;
                    GLinkIn[n].Out = null;
                }
            }

            // If 2 flits want to enter each others' ring, put both into the buffer and swap the flits in the buffer
            for (int n = 0; n < 2 * LWidth; n++)
            {
                int Qnum = (n + localRR) % (LWidth * 2);
                Flit f = LLinkOut[Qnum].In;
                if (f == null || !productive(f, Local))
                    continue;
                //int preference = L2G_preference(f);
                bool flag = false;
                int iter = 2;
                for (int i = 0; i < iter; i++)
                {
                    for (int j = 0; j < GWidth; j++)
                    {
                        int k =  j;//(j + globalRR) % GWidth;
                        int gport = k*2 + i;
                        Flit fg = GLinkOut[gport].In;
                        if (fg != null && flag == false && productive(fg, Global) && Q_L2G[Qnum].Count > 0 && Q_G2L[gport].Count > 0)
                        {
                            flag = true;
                            GLinkOut[gport].In = Q_L2G[Qnum].Dequeue();
                            Q_L2G[Qnum].Enqueue(f);
                            LLinkOut[Qnum].In = Q_G2L[gport].Dequeue();
                            Q_G2L[gport].Enqueue(fg);
                            local_starveCounter[Qnum] = 0;
                            global_starveCounter[gport] = 0;
                            Gdeflected --;
                            Ldeflected --;
                            G2Lcrossed ++;
                            L2Gcrossed ++;
                        }
                    }
                }
            }


            //	Simulator.stats.GdeflectedFlit.Add(Gdeflected);
            //	Simulator.stats.LdeflectedFlit.Add(Ldeflected);
            //	Simulator.stats.G2LcrossFlit.Add(G2Lcrossed);
            //	Simulator.stats.L2GcrossFlit.Add(L2Gcrossed);

            // flits from local buffer to global ring
            for (int n = 0; n < 2 * LWidth; n++)
            {
                int Qnum = (localRR + n) % (LWidth * 2);
                if (Q_L2G[Qnum].Count == 0)
                    continue;
                // The shorter distance is always preffered
                Flit f = Q_L2G[Qnum].Peek();
                int preference = L2G_preference(f);
                bool flag = false;
                int iter = (Config.SingleDirRing || Config.forcePreference)? 1 : 2;
                for (int i = 0; i < iter; i++)
                {
                    for (int j = 0; j < GWidth; j++)
                    {
                        int k = (j + globalRR) % GWidth;
                        int gport = Config.SingleDirRing? k*2 : k*2+(preference+i)%2;
                        Flit fg = GLinkOut[gport].In;
                        if (fg == null && flag == false)
                        {
                            GLinkOut[gport].In = Q_L2G[Qnum].Dequeue();
                            flag = true;
                            local_starveCounter[Qnum] = 0;
                            //			if (Q_L2G[Qnum].Count == 0)
                            //				Simulator.stats.L2GBufferBypass.Add(1);
                        }
                    }
                }
            }

            //flits from global buffer to local ring
            for (int n = 0; n < GWidth * 2; n ++)
            {
                int Qnum = (n + globalRR) % (GWidth * 2);
                if (Q_G2L[Qnum].Count == 0)
                    continue;
                Flit f = Q_G2L[Qnum].Peek();
                int preference = G2L_preference(f);
                bool flag = false;
                // Default no force on pref and not single dirRing
                int iter = (Config.SingleDirRing || Config.forcePreference)? 1:2;
                if (Config.SingleDirRing)
                    preference = 0;

                for (int i = 0; i < iter; i ++)
                {
                    for (int j = 0; j < LWidth; j++)
                    {
                        int k = (j + localRR) % LWidth;
                        int r = Config.SingleDirRing? k*2 : k * 2 + (preference+i) % 2;
                        //Console.WriteLine("LW{0}, GW{1}, Qnum{2},QCount:{3}, r:{4}", LWidth, GWidth, Qnum,Q_G2L[Qnum].Count, r);
                        if (LLinkOut[r].In == null && flag == false)
                        {
                            LLinkOut[r].In = Q_G2L[Qnum].Dequeue();
                            flag = true;
                            global_starveCounter[Qnum] = 0;
                            //							if (Q_G2L[Qnum].Count == 0)
                            //								Simulator.stats.G2LBufferBypass.Add(1);
                        }
                    }
                }
            }


            if (Config.simpleLivelock)
            {
                bool portStarve = false;
                for (int n = 0; n < 2 * LWidth; n++)
                {
                    if (Q_L2G[n].Count > 0)
                        local_starveCounter[n] ++;
                    if (local_starveCounter[n] > Config.starveThreshold)
                        portStarve = true;
                }
                for (int n = 0; n < 2 * GWidth; n++)
                {
                    if (Q_G2L[n].Count > 0)
                        global_starveCounter[n] ++;
                    if (global_starveCounter[n] > Config.starveThreshold)
                        portStarve = true;
                }
                if (portStarve)
                    starved[ID + Config.N] = true;
                else
                    starved[ID + Config.N] = false;
            }
        }

        //TODO: need to make sure all flits within a packet take the same ring
        private int G2L_preference(Flit f)
        {
            if (Config.NoPreference)
                return Simulator.rand.Next(2);
            int preference = -1;
            int dest = f.packet.dest.ID;
            if (Config.topology == Topology.HR_4drop)
            {
                if (dest == 0 || dest == 3 || dest == 4 || dest == 7 || dest == 9 || dest == 10 || dest == 13 || dest == 14)
                    preference = 0;
                else
                    preference = 1;
            }
            else if (Config.topology == Topology.HR_8drop)
            {
                if (dest == 3 || dest == 0 || dest == 5 || dest == 6 || dest == 9 || dest == 10 || dest == 12 || dest == 15)
                    preference = ID % 2;
                else
                    preference = (1 + ID) % 2;
            }
            else if (Config.topology == Topology.HR_16drop)
            {
                if (dest == ID || dest == ID + 1 || dest == ID - 3)
                    preference = 0;
                else
                    preference = 1;
            }
            else if (Config.topology == Topology.HR_8_16drop)
            {
                int start = ID * 2;
                int end = (ID * 2 + 3) % 8 + ID / 4 * 8;
                if (end > start)
                {
                    if (dest >= start && dest <= end)
                        preference = 0;
                    else
                        preference = 1;
                }
                else
                {
                    if (dest > end && dest < start)
                        preference = 1;
                    else
                        preference = 0;
                }
            }
            else if (Config.topology == Topology.HR_8_8drop || Config.topology == Topology.HR_16_8drop ||
                    Config.topology == Topology.HR_32_8drop)
            {
                if (dest / LWidth / LWidth % 4 == 0 || dest / LWidth / LWidth % 4 == 3)
                    preference = ((ID % 8 == 0) || (ID % 8 == 3) || (ID % 8 == 5) || (ID % 8 == 6)) ? 0 : 1;
                else
                    preference = ((ID % 8 == 0) || (ID % 8 == 3) || (ID % 8 == 5) || (ID % 8 == 6)) ? 1 : 0;
            }
            else
                throw new Exception("topology not suported!!!");
            return preference;
        }
        
        //TODO: need to make sure all flits within a packet take the same ring
        private int L2G_preference(Flit f)
        {
            if (Config.NoPreference)
                return Simulator.rand.Next(2);
            int preference = -1;
            int src = f.packet.src.ID;
            //	int dest = f.packet.dest.ID;
            if (f.packet.dest.ID / GWidth / GWidth / 4 == f.packet.src.ID / GWidth / GWidth / 4)
            {  // src and dest are now on the same same level of rings
                if ((f.packet.dest.ID/GWidth/GWidth - f.packet.src.ID/GWidth/GWidth + 4) % 4 < 2)
                    preference = 0;
                else if ((f.packet.dest.ID/GWidth/GWidth - f.packet.src.ID/GWidth/GWidth + 4) % 4 > 2)
                    preference = 1;
            }
            else
            {
                if ((src/GWidth/GWidth) % 2 == 0)
                    preference = 0;
                else
                    preference = 1;
            }
            if (preference == -1)
                preference = Simulator.rand.Next(2);
            return preference;
        }

        public override bool canInjectFlit(Flit f)
        {
            return false;
        }
        public override void InjectFlit(Flit f)
        {
            return;
        }

    }
}

