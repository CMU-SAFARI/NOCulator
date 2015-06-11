//#define DEBUG

using System;
using System.Collections.Generic;

namespace ICSimulator
{
    class AveragingWindow
    {
        protected int len, ptr;
        protected double[] window;
        protected double sum;

        public AveragingWindow(int N)
        {
            len = N;
            window = new double[N];
            ptr = 0;
            sum = 0.0;
        }

        public virtual void accumulate(double t)
        {
            sum -= window[ptr];
            window[ptr] = t;
            sum += window[ptr];
            ptr = (ptr + 1) % len;
        }

        public double average()
        {
            return sum / (double)len;
        }
    }

    class AveragingWindow_Binary : AveragingWindow
    {
        public AveragingWindow_Binary(int N)
            : base(N)
        {
        }

        public void accumulate(bool t)
        {
            base.accumulate(t ? 1 : 0);
        }
    }

    public class Controller_Throttle : Controller_ClassicBLESS
    {
        double[] m_throttleRates = new double[Config.N];
        IPrioPktPool[] m_injPools = new IPrioPktPool[Config.N];
        bool[] m_starved = new bool[Config.N];

        AveragingWindow_Binary[] avg_starve =
            new AveragingWindow_Binary[Config.N];
        AveragingWindow[] avg_qlen =
            new AveragingWindow[Config.N];

        public Controller_Throttle()
        {
            Console.WriteLine("init");
            for (int i = 0; i < Config.N; i++)
            {
                avg_starve[i] = new AveragingWindow_Binary(Config.throttle_averaging_window);
                avg_qlen[i] = new AveragingWindow(Config.throttle_averaging_window);
            }
        }

        void setThrottleRate(int node, double rate)
        {
            m_throttleRates[node] = rate;
        }

        public override IPrioPktPool newPrioPktPool(int node)
        {
            return MultiQThrottlePktPool.construct();
        }

        // true to allow injection, false to block (throttle)
        public override bool tryInject(int node)
        {
            if (m_throttleRates[node] > 0.0)
                return Simulator.rand.NextDouble() > m_throttleRates[node];
            else
                return true;
        }

        public override void setInjPool(int node, IPrioPktPool pool)
        {
            m_injPools[node] = pool;
            pool.setNodeId(node);
        }


        public override void reportStarve(int node)
        {
            m_starved[node] = true;
        }

        public override void doStep()
        {
            for (int i = 0; i < Config.N; i++)
            {
                int qlen = Simulator.network.nodes[i].RequestQueueLen;
                avg_starve[i].accumulate(m_starved[i]);
                avg_qlen[i].accumulate(qlen);
                m_starved[i] = false;
            }

            if (Simulator.CurrentRound > 0 &&
                    (Simulator.CurrentRound % (ulong)Config.throttle_epoch) == 0)
                setThrottling();
        }

        void setThrottling()
        {
            // find the average IPF
            double avg = 0.0;
            for (int i = 0; i < Config.N; i++)
                avg += avg_qlen[i].average();
            avg /= Config.N;

            // determine whether any node is congested
            bool congested = false;
            for (int i = 0; i < Config.N; i++)
                if (avg_starve[i].average() > Config.srate_thresh)
                {
                    congested = true;
                    break;
                }

#if DEBUG
            Console.WriteLine("throttle: cycle {0} congested {1}\n---",
                   Simulator.CurrentRound, congested ? 1 : 0);

            Console.WriteLine("avg qlen is {0}", avg);
#endif

            for (int i = 0; i < Config.N; i++)
                if (congested && avg_qlen[i].average() > avg)
                {
                    setThrottleRate(i, Math.Min(Config.throt_max, Config.throt_min+Config.throt_scale * avg_qlen[i].average()));
#if DEBUG
                    Console.WriteLine("node {0} qlen {1} rate {2}", i,
                           avg_qlen[i].average(),
                           Math.Min(Config.throt_max, Config.throt_min+Config.throt_scale * avg_qlen[i].average()));
#endif
                }
                else
                {
                    setThrottleRate(i, 0.0);
#if DEBUG
                    Console.WriteLine("node {0} qlen {1} rate 0.0", i, avg_qlen[i].average());
#endif
                }
#if DEBUG
            Console.WriteLine("---\n");
#endif
        }
    }
}
