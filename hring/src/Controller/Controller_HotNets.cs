//#define DEBUG
//#define DEBUG2
//#define THROTTLE_ALL
//#define ADJUST_RATE_FEEDBACK

using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class HotNetsThrottlePool : IPrioPktPool
    {
        Queue<Flit>[] queues;
        int nqueues;
        int next_queue;
        int last_peek_queue;
        int node_id=-1;
        int flitCount = 0;

        public static IPrioPktPool construct()
        {
            return new HotNetsThrottlePool();
        }

        private HotNetsThrottlePool()
        {
            nqueues = Packet.numQueues;
            queues = new Queue<Flit>[nqueues];
            for (int i = 0; i < nqueues; i++)
                queues[i] = new Queue<Flit>();
            next_queue = nqueues - 1;
            last_peek_queue = 0;
        }

        public bool FlitInterface { get { return true; } }

        public void addPacket(Packet pkt)
        {
            flitCount += pkt.nrOfFlits;
            Queue<Flit> q = queues[pkt.getQueue()];
            foreach (Flit f in pkt.flits)
                q.Enqueue(f);
        }

        int chooseQueue()
        {
            if (!Simulator.controller.tryInject(node_id))
                return 1;
            else if (queues[next_queue].Count > 0 && queues[next_queue].Peek().flitNr != 0)
                return next_queue;
            else
            {
                int tries = nqueues;
                while (tries-- > 0)
                {
                    next_queue = (next_queue + 1) % nqueues;
                    if (queues[next_queue].Count > 0) return next_queue;
                }
                return next_queue;
            }
        }

        public Flit peekFlit()
        {
            last_peek_queue = chooseQueue();
            if (queues[last_peek_queue].Count == 0) return null;
            else return queues[last_peek_queue].Peek();
        }

        public void takeFlit()
        {
            if (queues[last_peek_queue].Count == 0) throw new Exception("don't take unless you peek!");
            queues[last_peek_queue].Dequeue();
        }

        public Packet next() { return null; }

        public void setNodeId(int id)
        {
            node_id=id;
        }

        public int Count { get { return flitCount; } }
        public int FlitCount { get { return flitCount; } }

        public int ReqCount { get { return queues[0].Count; } }
    }

    public class Controller_HotNets : Controller_ClassicBLESS
    {
        HotNetsThrottlePool[] m_injPools = new HotNetsThrottlePool[Config.N];
        AveragingWindow avg_netutil, avg_ipc;
        AveragingWindow[] avg_qlen;
        double m_lastIPC = 0.0;
        double m_target;
        double m_rate;
        bool[] m_throttled = new bool[Config.N];
        ulong[] last_ins = new ulong[Config.N];
        ulong[] last_inj = new ulong[Config.N];
        double[] m_ipf = new double[Config.N];
        double m_avg_ipf = 0.0;
        bool m_starved = false;

        public Controller_HotNets()
        {
            avg_netutil = new AveragingWindow(Config.selftuned_netutil_window);
            avg_ipc = new AveragingWindow(Config.selftuned_ipc_window);
            avg_qlen = new AveragingWindow[Config.N];
            for (int i = 0; i < Config.N; i++)
                avg_qlen[i] = new AveragingWindow(Config.hotnets_qlen_window);

            m_target = Config.selftuned_init_netutil_target;
            m_rate = 0.0;
        }

        public override IPrioPktPool newPrioPktPool(int node)
        {
            return HotNetsThrottlePool.construct();
        }

        public override void setInjPool(int node, IPrioPktPool pool)
        {
            m_injPools[node] = pool as HotNetsThrottlePool;
            pool.setNodeId(node);
        }

        public override bool ThrottleAtRouter
        { get { return false; } }

        // true to allow injection, false to block (throttle)
        public override bool tryInject(int node)
        {
            if (!m_throttled[node])
                return true;

            if (Config.hotnets_closedloop_rate)
            {
                if (m_rate > 0.0)
                    return Simulator.rand.NextDouble() > m_rate;
                else
                    return true;
            }
            else
            {
                double rate = Math.Min(Config.hotnets_max_throttle, Config.hotnets_min_throttle + Config.hotnets_scale / avg_qlen[node].average());
#if DEBUG2
                Console.WriteLine("node {0} qlen {1} rate {2}", node, avg_qlen[node].average(), rate);
#endif
                if (rate > 0)
                    return Simulator.rand.NextDouble() > rate;
                else
                    return true;
            }
        }

        public override void doStep()
        {
            avg_netutil.accumulate(Simulator.network._cycle_netutil);
            avg_ipc.accumulate((double)Simulator.network._cycle_insns / Config.N);

            for (int i = 0; i < Config.N; i++)
                avg_qlen[i].accumulate(m_injPools[i].ReqCount);

            if (Simulator.CurrentRound % (ulong)Config.hotnets_ipc_quantum == 0)
                doUpdateIPF();
            if (Simulator.CurrentRound % (ulong)Config.selftuned_quantum == 0)
                doUpdate();

            stepStarve();
        }

        int m_starve_idx = 0;
        bool[,] m_starve_window = new bool[Config.N, Config.hotnets_starve_window];
        int[] m_starve_total = new int[Config.N];

        void stepStarve()
        {
            m_starve_idx = (m_starve_idx + 1) % Config.hotnets_starve_window;
            for (int node = 0; node < Config.N; node++)
            {
                if (m_starve_window[node, m_starve_idx]) m_starve_total[node]--;
                m_starve_window[node, m_starve_idx] = false;
            }
        }

        public override void reportStarve(int node)
        {
            m_starve_window[node, m_starve_idx] = true;
            m_starve_total[node]++;
        }

        void doUpdateIPF()
        {
            m_avg_ipf = 0.0;
            m_starved = false;
            for (int i = 0; i < Config.N; i++)
            {
                ulong ins = Simulator.network.nodes[i].cpu.ICount - last_ins[i];
                ulong inj = Simulator.network.routers[i].Inject - last_inj[i];
                last_ins[i] = Simulator.network.nodes[i].cpu.ICount;
                last_inj[i] = Simulator.network.routers[i].Inject;
                m_ipf[i] = (inj > 0) ? (double)ins / (double)inj : 0.0;
                m_avg_ipf += m_ipf[i];

            }
            m_avg_ipf /= (double)Config.N;
        }

        void doUpdate()
        {
            double netu = avg_netutil.average();

            m_starved = false;
            double avg_Q = 0;
            for (int i = 0; i < Config.N; i++)
            {
                double qlen = avg_qlen[i].average();
                avg_Q += qlen;

                double srate = (double)m_starve_total[i] / Config.hotnets_starve_window;

                double starve_thresh = Math.Min(Config.hotnets_starve_max,
                        Config.hotnets_starve_min + Config.hotnets_starve_scale / m_ipf[i]);

#if DEBUG2
                Console.WriteLine("router {0} starve_thresh {1} starvation rate {2} starved {3}",
                        i, starve_thresh, srate, srate > starve_thresh);
#endif

                if (srate > starve_thresh)
                    m_starved = true;
            }
            avg_Q /= Config.N;

            for (int i = 0; i < Config.N; i++)
            {
                if (Simulator.CurrentRound > 0)
                    //m_throttled[i] = m_starved && (m_ipf[i] < m_avg_ipf);
                    m_throttled[i] = m_starved && (avg_qlen[i].average() > avg_Q);
                else
                    m_throttled[i] = false;

#if DEBUG
                Console.WriteLine("cycle {0} node {1} ipf {2} (avg {3}) throttle {4}",
                        Simulator.CurrentRound, i, m_ipf[i], avg_ipf, m_throttled[i]);
#endif
            }

            if (netu > m_target + Config.selftuned_netutil_tolerance)
                if (m_rate < 1.00) m_rate += Config.selftuned_rate_delta;
            if (netu < m_target - Config.selftuned_netutil_tolerance)
                if (m_rate > 0.00) m_rate -= Config.selftuned_rate_delta;

#if DEBUG
            Console.WriteLine("cycle {0}: netu {1}, target {2}, rate {3}",
                    Simulator.CurrentRound, netu, m_target, m_rate);
#endif
        }
    }
}
