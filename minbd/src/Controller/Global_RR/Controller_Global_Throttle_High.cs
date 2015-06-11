//#define DEBUG
//#define DEBUG2
//#define MPKI_D

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Global_Throttle_High : Controller_ClassicBLESS
    {
        bool[] m_isThrottled = new bool[Config.N];
        double[] m_throttleRates = new double[Config.N];

        public Controller_Global_Throttle_High()
        {
            Console.WriteLine("init: Global_RR");
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                numInject[i]=0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
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

        public override void resetStat()
        {
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                num_ins_last_epoch[i] = Simulator.stats.insns_persrc[i].Count;
                numInject[i]=0;
		        L1misses[i]=0;
            }            
        }

        public override void doStep()
        {
            if (Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.throttle_sampling_period) == 0)
            {
                setThrottling();
                //if being throttled is set,throttle it with static rate
                for (int i = 0; i < Config.N; i++)
                    if(m_isThrottled[i])
                        setThrottleRate(i,Config.sweep_th_rate);
                resetStat();
            }
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

        void setThrottling()
        {
#if DEBUG
            //Console.WriteLine("\n:: cycle {0} ::",
                    //Simulator.CurrentRound);
#endif
            //get the MPKI value
            for (int i = 0; i < Config.N; i++)
            {
                if(num_ins_last_epoch[i]==0)
                    //NumInject gets incremented in RouterFlit.cs
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
            recordStats();
#if DEBUG
            bool flag=false;
#endif
            ulong hcount=0;
            double mpki_sum=0;
            double highest=0;
            double lowest=500;
            //turn on throttling for those high nodes
            for (int i = 0; i < Config.N; i++)
            {
                mpki_sum+=MPKI[i];
                if(MPKI[i]>Config.MPKI_high_node)
                    hcount++;
                if(MPKI[i]>highest)
                    highest=MPKI[i];
                if(MPKI[i]<lowest)
                    lowest=MPKI[i];

                if (MPKI[i] > Config.MPKI_high_node)
                {
#if DEBUG
                    if(!flag)
                    {
                        Console.Write("\n#ON#: ");
                        flag=true;
                    }
#endif
                    m_isThrottled[i] = true;
#if DEBUG
                    Console.Write("(ID:{0},M:{1},I:{2}) ",i,MPKI[i],numInject[i]);
#endif
                }
                else
                {
                    m_isThrottled[i] = false;
#if DEBUG2
                    Console.WriteLine("Low inj:{0}",numInject[i]);
#endif
                }
            }
#if MPKI_D
            if(mpki_avg>15)
                Console.WriteLine("L1 MPKI misses avg {0}",mpki_avg);
            /*if(hcount>0)
                Console.WriteLine("{0} nodes have higher than {1} MPKI. Highest: {2}",hcount,Config.MPKI_high_node,highest);*/
            if(hcount<16)
                Console.WriteLine("{0} nodes have higher than {1} MPKI. Lowest: {2}",hcount,Config.MPKI_high_node,lowest);
#endif
        }
    }
}
