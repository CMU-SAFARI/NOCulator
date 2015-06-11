//#define DEBUG
//#define RETX_DEBUG

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public class AFCBufferSlot : IComparable
    {
        Flit m_f;
        public ulong inTimeStamp;

        public Flit flit { get { return m_f; } set { m_f = value; } }

        public AFCBufferSlot(Flit f)
        {
            m_f = f;
            inTimeStamp = Simulator.CurrentRound;
        }

        public int CompareTo(object o)
        {
            if (o is AFCBufferSlot)
                return Router_Flit_OldestFirst._rank(m_f, (o as AFCBufferSlot).m_f);
            else
                throw new ArgumentException("bad comparison");
        }

        public void getNewTimeStamp()
        {
            inTimeStamp = Simulator.CurrentRound;
        }
    }

    public class AFCUtilAvg
    {
        double m_avg;
        double m_window_sum;
        double[] m_window;
        int m_window_ptr;

        public AFCUtilAvg()
        {
            m_window = new double[Config.afc_avg_window];
            m_window_ptr = 0;
            m_window_sum = 0;
            m_avg = 0;
        }

        public void Add(double util)
        {
            // add new sample to window and update sum
            m_window_sum -= m_window[m_window_ptr];
            m_window[m_window_ptr] = util;
            m_window_sum += util;
            m_window_ptr = (m_window_ptr + 1) % Config.afc_avg_window;

            // mix window-average into EWMA
            m_avg = Config.afc_ewma_history * m_avg +
                (1 - Config.afc_ewma_history) * (m_window_sum / Config.afc_avg_window);
        }

        public double Avg { get { return m_avg; } }
    }

    public class Router_AFC : Router
    {
        // injectSlot is from Node
        protected Flit m_injectSlot;
        
        // buffers, indexed by physical channel and virtual network
        protected MinHeap<AFCBufferSlot>[,] m_buf;
        int m_buf_occupancy;

        // buffers active?
        protected bool m_buffered;

        public Router_AFC(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot = null;

            m_buf = new MinHeap<AFCBufferSlot>[5, Config.afc_vnets];
            for (int pc = 0; pc < 5; pc++)
                for (int i = 0; i < Config.afc_vnets; i++)
                    m_buf[pc, i] = new MinHeap<AFCBufferSlot>();

            m_buffered = false;
            m_buf_occupancy = 0;
        }

        protected Router_AFC getNeigh(int dir)
        {
            return neigh[dir] as Router_AFC;
        }

        // accept one ejected flit into rxbuf
        protected void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }

        Flit ejectLocal()
        {
            // eject locally-destined flit (highest-ranked, if multiple)
            Flit ret = null;
            int bestDir = -1;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                        linkIn[dir].Out.state != Flit.State.Placeholder &&
                        linkIn[dir].Out.dest.ID == ID &&
                        (ret == null || rank(linkIn[dir].Out, ret) < 0))
                {
                    ret = linkIn[dir].Out;
                    bestDir = dir;
                }

            if (bestDir != -1) linkIn[bestDir].Out = null;
#if DEBUG
            if (ret != null)
                Console.WriteLine("ejecting flit {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            return ret;
        }

        // keep these as member vars so we don't have to allocate on every step
        // (why can't we have arrays on the stack like in C?)
        Flit[] input = new Flit[4]; 
        AFCBufferSlot[] requesters = new AFCBufferSlot[5];
        int[] requester_dir = new int[5];

        Queue<AFCBufferSlot> m_freeAFCSlots = new Queue<AFCBufferSlot>();

        AFCBufferSlot getFreeBufferSlot(Flit f)
        {
            if (m_freeAFCSlots.Count > 0)
            {
                AFCBufferSlot s = m_freeAFCSlots.Dequeue();
                s.flit = f;
                s.getNewTimeStamp();
                return s;
            }
            else
                return new AFCBufferSlot(f);
        }
        void returnFreeBufferSlot(AFCBufferSlot s)
        {
            m_freeAFCSlots.Enqueue(s);
        }

        void switchBufferless()
        {
            m_buffered = false;
        }

        void switchBuffered()
        {
            m_buffered = true;
            if (m_injectSlot != null)
            {
                InjectFlit(m_injectSlot);
                m_injectSlot = null;
            }
        }

        AFCUtilAvg m_util_avg = new AFCUtilAvg();

        protected override void _doStep()
        {
            int flit_count = 0;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                    flit_count++;

            m_util_avg.Add((double)flit_count / neighbors);

            Simulator.stats.afc_avg.Add(m_util_avg.Avg);
            Simulator.stats.afc_avg_bysrc[ID].Add(m_util_avg.Avg);

            bool old_status = m_buffered;
            bool new_status = old_status;
            bool gossip_induced = false;

            if (Config.afc_force)
            {
                new_status = Config.afc_force_buffered;
            }
            else
            {
                if (!m_buffered && 
                        (m_util_avg.Avg > Config.afc_buf_threshold))
                    new_status = true;

                if (m_buffered &&
                        (m_util_avg.Avg < Config.afc_bless_threshold) &&
                        m_buf_occupancy == 0)
                    new_status = false;

                // check at least one free slot in downstream routers; if not, gossip-induced switch
                for (int n = 0; n < 4; n++)
                {
                    Router_AFC nr = getNeigh(n);
                    if (nr == null) continue;
                    int oppDir = (n + 2) % 4;
                    for (int vnet = 0; vnet < Config.afc_vnets; vnet++)
                    {
                        int occupancy = nr.m_buf[oppDir, vnet].Count;
                        if ((capacity(vnet) - occupancy) < 2)
                        {
                            gossip_induced = true;
                            break;
                        }
                    }
                }
                if (gossip_induced) new_status = true;
            }

            // perform switching and stats accumulation
            if (old_status && !new_status)
            {
                switchBufferless();
                Simulator.stats.afc_switch.Add();
                Simulator.stats.afc_switch_bless.Add();
                Simulator.stats.afc_switch_bysrc[ID].Add();
                Simulator.stats.afc_switch_bless_bysrc[ID].Add();
            }
            if (!old_status && new_status)
            {
                switchBuffered();
                Simulator.stats.afc_switch.Add();
                Simulator.stats.afc_switch_buf.Add();
                Simulator.stats.afc_switch_bysrc[ID].Add();
                Simulator.stats.afc_switch_buf_bysrc[ID].Add();
            }

            if (m_buffered)
            {
                Simulator.stats.afc_buffered.Add();
                Simulator.stats.afc_buffered_bysrc[ID].Add();
                if (gossip_induced)
                {
                    Simulator.stats.afc_gossip.Add();
                    Simulator.stats.afc_gossip_bysrc[ID].Add();
                }
            }
            else
            {
                Simulator.stats.afc_bless.Add();
                Simulator.stats.afc_bless_bysrc[ID].Add();
            }

            if (m_buffered)
            {
                Simulator.stats.afc_buf_enabled.Add();
                Simulator.stats.afc_buf_enabled_bysrc[ID].Add();

                Simulator.stats.afc_buf_occupancy.Add(m_buf_occupancy);
                Simulator.stats.afc_buf_occupancy_bysrc[ID].Add(m_buf_occupancy);

                // grab inputs into buffers
                for (int dir = 0; dir < 4; dir++)
                {
                    if (linkIn[dir] != null && linkIn[dir].Out != null)
                    {
                        Flit f = linkIn[dir].Out;
                        linkIn[dir].Out = null;
                        f.bufferInTime = Simulator.CurrentRound;
                        AFCBufferSlot slot = getFreeBufferSlot(f);
                        m_buf[dir, f.packet.getClass()].Enqueue(slot);
                        m_buf_occupancy++;

                        Simulator.stats.afc_buf_write.Add();
                        Simulator.stats.afc_buf_write_bysrc[ID].Add();
                    }
                }

                // perform arbitration: (i) collect heads of each virtual-net
                // heap (which represents many VCs) to obtain a single requester
                // per physical channel; (ii)  request outputs among these
                // requesters based on DOR; (iii) select a single winner
                // per output

                for (int i = 0; i < 5; i++)
                {
                    requesters[i] = null;
                    requester_dir[i] = -1;
                }
                
                // find the highest-priority vnet head for each input PC
                for (int pc = 0; pc < 5; pc++)
                    for (int vnet = 0; vnet < Config.afc_vnets; vnet++)
                        if (m_buf[pc, vnet].Count > 0)
                        {
                            AFCBufferSlot top = m_buf[pc, vnet].Peek();
                            PreferredDirection pd = determineDirection(top.flit, coord);
                            int outdir = (pd.xDir != Simulator.DIR_NONE) ?
                                pd.xDir : pd.yDir;
                            if (outdir == Simulator.DIR_NONE)
                                outdir = 4; // local ejection

                            // skip if (i) not local ejection and (ii)
                            // destination router is buffered and (iii)
                            // no credits left to destination router
                            if (outdir != 4)
                            {
                                Router_AFC nrouter = (Router_AFC)neigh[outdir];
                                int ndir = (outdir + 2) % 4;
                                if (nrouter.m_buf[ndir, vnet].Count >= capacity(vnet) &&
                                        nrouter.m_buffered)
                                    continue;
                            }

                            // otherwise, contend for top requester from this
                            // physical channel
                            if (requesters[pc] == null ||
                                    top.CompareTo(requesters[pc]) < 0)
                            {
                                requesters[pc] = top;
                                requester_dir[pc] = outdir;
                            }
                        }

                // find the highest-priority requester for each output, and pop
                // it from its heap
                for (int outdir = 0; outdir < 5; outdir++)
                {
                    AFCBufferSlot top  = null;
                    AFCBufferSlot top2 = null;
                    int top_indir  = -1;
                    int top2_indir = -1;
                    int nrWantToEject = 0;

                    for (int req = 0; req < 5; req++)
                        if (requesters[req] != null &&
                                requester_dir[req] == outdir)
                        {
                            if (top == null ||
                                    requesters[req].CompareTo(top) < 0)
                            {
                                top2 = top;
                                top2_indir = top_indir;
                                top = requesters[req];
                                top_indir = req;
                                nrWantToEject++;
                            }
                        }

                    if (outdir == 4)
                        switch (nrWantToEject)
                        {
                            case 0: Simulator.stats.eject_0.Add(); break;
                            case 1: Simulator.stats.eject_1.Add(); break;
                            case 2: Simulator.stats.eject_2.Add(); break;
                            case 3: Simulator.stats.eject_3.Add(); break;
                            case 4: Simulator.stats.eject_4.Add(); break;
                            default: throw new Exception("Ejection problem");
                        }    
                                        
                    if (top_indir != -1)
                    {
                        m_buf[top_indir, top.flit.packet.getClass()].Dequeue();
                        if (top.inTimeStamp == Simulator.CurrentRound)
                            Simulator.stats.buffer_bypasses.Add();
                        Simulator.stats.buffer_comparisons.Add();
                        Simulator.stats.afc_buf_read.Add();
                        Simulator.stats.afc_buf_read_bysrc[ID].Add();
                        Simulator.stats.afc_xbar.Add();
                        Simulator.stats.afc_xbar_bysrc[ID].Add();

                        if (top_indir == 4)
                            statsInjectFlit(top.flit);

                        // propagate to next router (or eject)
                        if (outdir == 4)
                            acceptFlit(top.flit);
                        else
                            linkOut[outdir].In = top.flit;

                        returnFreeBufferSlot(top);
                        m_buf_occupancy--;
                    }
                    
                    if (Config.ejectCount == 2 && outdir == 4 && top2_indir != -1)
                    {
                        m_buf[top2_indir, top2.flit.packet.getClass()].Dequeue();    
                        if (top2 != null)
                            acceptFlit(top2.flit);
                        
                        returnFreeBufferSlot(top2);
                        m_buf_occupancy--;
                    }
                }

            }
            else
            {
                Flit[] eject = new Flit[4];
                eject[0] = eject[1] = eject[2] = eject[3] = null;
                int ejCount = 0;
                for (int i = 0; i < Config.ejectCount; i++)
                {
                    eject[ejCount] = ejectLocal();
                    ejCount++;
                }

                for (int i = 0; i < 4; i++) input[i] = null;

                // grab inputs into a local array so we can sort
                int c = 0;
                for (int dir = 0; dir < 4; dir++)
                    if (linkIn[dir] != null && linkIn[dir].Out != null)
                    {
                        input[c++] = linkIn[dir].Out;
                        linkIn[dir].Out.inDir = dir;
                        linkIn[dir].Out = null;
                    }

                // sometimes network-meddling such as flit-injection can put unexpected
                // things in outlinks...
                int outCount = 0;
                for (int dir = 0; dir < 4; dir++)
                    if (linkOut[dir] != null && linkOut[dir].In != null)
                        outCount++;

                bool wantToInject = m_injectSlot != null;
                bool canInject = (c + outCount) < neighbors;
                bool starved = wantToInject && !canInject;

                if (starved)
                {
                    Flit starvedFlit = m_injectSlot;
                    Simulator.controller.reportStarve(coord.ID);
                    statsStarve(starvedFlit);
                }
                if (canInject && wantToInject)
                {
                    Flit inj = null;
                    if (m_injectSlot != null)
                    {
                        inj = m_injectSlot;
                        m_injectSlot = null;
                    }
                    else
                        throw new Exception("trying to inject a null flit");

                    input[c++] = inj;
#if DEBUG
                    Console.WriteLine("injecting flit {0}.{1} at node {2} cyc {3}",
                            m_injectSlot.packet.ID, m_injectSlot.flitNr, coord, Simulator.CurrentRound);
#endif
                    statsInjectFlit(inj);
                }
                
                for (int i = 0; i < Config.ejectCount; i++)
                {
                    if (eject[i] != null)
                        acceptFlit(eject[i]);
                }
                // inline bubble sort is faster for this size than Array.Sort()
                // sort input[] by descending priority. rank(a,b) < 0 iff a has higher priority.
                for (int i = 0; i < 4; i++)
                    for (int j = i + 1; j < 4; j++)
                        if (input[j] != null &&
                                (input[i] == null ||
                                 rank(input[j], input[i]) < 0))
                        {
                            Flit t = input[i];
                            input[i] = input[j];
                            input[j] = t;
                        }

                // assign outputs
                for (int i = 0; i < 4 && input[i] != null; i++)
                {
                    PreferredDirection pd = determineDirection(input[i], coord);
                    int outDir = -1;

                    if (pd.xDir != Simulator.DIR_NONE && linkOut[pd.xDir].In == null)
                    {
                        linkOut[pd.xDir].In = input[i];
                        outDir = pd.xDir;
                    }

                    else if (pd.yDir != Simulator.DIR_NONE && linkOut[pd.yDir].In == null)
                    {
                        linkOut[pd.yDir].In = input[i];
                        outDir = pd.yDir;
                    }

                    // deflect!
                    else
                    {
                        input[i].Deflected = true;
                        int dir = 0;
                        if (Config.randomize_defl) dir = Simulator.rand.Next(4); // randomize deflection dir (so no bias)
                        for (int count = 0; count < 4; count++, dir = (dir + 1) % 4)
                            if (linkOut[dir] != null && linkOut[dir].In == null)
                            {
                                linkOut[dir].In = input[i];
                                outDir = dir;
                                break;
                            }

                        if (outDir == -1) throw new Exception(
                                String.Format("Ran out of outlinks in arbitration at node {0} on input {1} cycle {2} flit {3} c {4} neighbors {5} outcount {6}", coord, i, Simulator.CurrentRound, input[i], c, neighbors, outCount));
                    }
                }
            }
        }

        public override bool canInjectFlit(Flit f)
        {
            int cl = f.packet.getClass();

            if (m_buffered)
                return m_buf[4, cl].Count < capacity(cl);
            else
                return m_injectSlot == null;
        }

        public override void InjectFlit(Flit f)
        {
            Simulator.stats.afc_vnet[f.packet.getClass()].Add();

            if (m_buffered)
            {
                //AFCBufferSlot slot = new AFCBufferSlot(f);
                AFCBufferSlot slot = getFreeBufferSlot(f);
                m_buf[4, f.packet.getClass()].Enqueue(slot);
                m_buf_occupancy++;

                Simulator.stats.afc_buf_write.Add();
                Simulator.stats.afc_buf_write_bysrc[ID].Add();
            }
            else
            {
                if (m_injectSlot != null)
                    throw new Exception("Trying to inject twice in one cycle");

                m_injectSlot = f;
            }
        }

        int capacity(int cl)
        {
            // in the future, we might size each virtual network differently; for now,
            // we use just one virtual network (since there is no receiver backpressure)
            return Config.afc_buf_per_vnet;
        }

        public override void flush()
        {
            m_injectSlot = null;
        }

        protected virtual bool needFlush(Flit f) { return false; }
    }
}
