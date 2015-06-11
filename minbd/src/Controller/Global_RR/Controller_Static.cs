using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class Controller_Static : Controller_ClassicBLESS
    {
        double[] m_throttleRates = new double[Config.N];
        IPrioPktPool[] m_injPools = new IPrioPktPool[Config.N];
        private static double TH_OFF = 0.0;

        public override IPrioPktPool newPrioPktPool(int node)
        {
            return MultiQThrottlePktPool.construct();
        }

        public override void setInjPool(int node, IPrioPktPool pool)
        {
            m_injPools[node] = pool;
            pool.setNodeId(node);
        }

        public override void resetStat()
        {
            for (int i = 0; i < Config.N; i++)
            {
                num_ins_last_epoch[i] = Simulator.stats.insns_persrc[i].Count;
                L1misses[i]=0;
            }            
        }

        public Controller_Static()
        {
            Console.WriteLine("Static throttler with rate {0}",Config.sweep_th_rate);
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                prev_MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                L1misses[i]=0;
            }
            //Throttle every single cycle,so only configure the throttle rate once.
            string [] throttle_node=Config.throttle_node.Split(',');
            if(throttle_node.Length!=Config.N)
            {
                Console.WriteLine("Specified string {0} for throttling nodes do not match "+
                        "with the number of nodes {1}",Config.throttle_node,Config.N);
                throw new Exception("Unmatched length");
            }
            for (int i = 0; i < Config.N; i++)
            {
                if(String.Compare(throttle_node[i],"1")==0)
                    setThrottleRate(i, Config.sweep_th_rate);
                else
                    setThrottleRate(i, TH_OFF);
            }
        }

        void setThrottleRate(int node, double rate)
        {
            m_throttleRates[node] = rate;
        }

        // true to allow injection, false to block (throttle)
        public override bool tryInject(int node)
        {
            if (m_throttleRates[node] > 0.0)
                return Simulator.rand.NextDouble() > m_throttleRates[node];
            else
                return true;
        }

        public virtual void recordStats()
        {
            double mpki_sum=0;
            double mpki=0;
            for(int i=0;i<Config.N;i++)
            {
                mpki=MPKI[i];
                mpki_sum+=mpki;
                Simulator.stats.mpki_bysrc[i].Add(mpki);
            }
            Simulator.stats.total_sum_mpki.Add(mpki_sum);
        }

        private void getMPKI()
        {
#if DEBUG
            Console.Write("\n:: cycle {0} ::",
                    Simulator.CurrentRound);
#endif
            //get the MPKI value
            for (int i = 0; i < Config.N; i++)
            {
                prev_MPKI[i]=MPKI[i];
                if(num_ins_last_epoch[i]==0)
                    MPKI[i]=((double)(L1misses[i]*1000))/(Simulator.stats.insns_persrc[i].Count);
                else
                {
                    if(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]>0)
                        MPKI[i]=((double)(L1misses[i]*1000))/(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]);
                    else if(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]==0)
                        MPKI[i]=0;
                    else
                        throw new Exception("MPKI error!");
                }
            }
        }
        public override void doStep()
        {
            //record stats every sampling period
            if (Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.throttle_sampling_period) == 0)
            {
                getMPKI();
                recordStats();
                resetStat();
            }
        }
    }
}
