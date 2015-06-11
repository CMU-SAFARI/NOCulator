//#define DEBUG
//#define DEBUG2

using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class Controller_Global_round_robin : Controller_ClassicBLESS
    {
        public bool[] m_isThrottled = new bool[Config.N];
        //This represent the round-robin turns
        public int[] throttleTable = new int[Config.N];
        public bool isThrottling;
        //This tell which group are allowed to run in a certain epoch
        public int currentAllow = 1;
        //Packet Pools
        IPrioPktPool[] m_injPools = new IPrioPktPool[Config.N];

        //thresholdTrigger will return true if the evaluation of the throttled related metric
        //exceed the threshold. This threshold can be made dynamic
        public virtual bool thresholdTrigger()
        {
#if DEBUG2
            Console.WriteLine("throttle OFF!");
#endif
            Simulator.stats.total_th_off.Add(1);
            double avg = 0.0;
            for (int i = 0; i < Config.N; i++)
                avg = avg + MPKI[i];
            avg = avg/Config.N;
#if DEBUG
            Console.WriteLine("NOT THROTTLING->Estimating MPKI, max_thresh {1}: avg MPKI {0}",avg,Config.MPKI_max_thresh);
#endif
            //greater than the max threshold
            return avg > Config.MPKI_max_thresh;

        }

        public Controller_Global_round_robin()
        {
            isThrottling = false;
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                prev_MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
            }
        }

        //Default controller uses one single queue. However, we are trying 
        //completely shut off injection of control packets only. Therefore
        //we need to use separate queues for each control, data, & writeback.
        public override IPrioPktPool newPrioPktPool(int node)
        {
            return MultiQThrottlePktPool.construct();
        }

        //configure the packet pool for each node. Makes it aware of
        //the node position.
        public override void setInjPool(int node, IPrioPktPool pool)
        {
            m_injPools[node] = pool;
            pool.setNodeId(node);
        }

        public override void resetStat()
        {
            for (int i = 0; i < Config.N; i++)
            {
                //prev_MPKI=MPKI[i];
                //MPKI[i]=0.0;
                num_ins_last_epoch[i] = Simulator.stats.insns_persrc[i].Count;
                L1misses[i]=0;
            }            
        }

        //On or off
        public virtual void setThrottleRate(int node, bool cond)
        {
            m_isThrottled[node] = cond;
        }

        // true to allow injection, false to block (throttle)
        // RouterFlit uses this function to determine whether it can inject or not
        public override bool tryInject(int node)
        {
            if(m_isThrottled[node]&&Simulator.rand.NextDouble()<Config.RR_throttle_rate)
            {
                Simulator.stats.throttled_counts_persrc[node].Add();
                return false;
            }
            else
            {
                Simulator.stats.not_throttled_counts_persrc[node].Add();
                return true;
            }
        }

        public virtual void doThrottling()
        {
            for(int i=0;i<Config.N;i++)
            {
                if((throttleTable[i]==currentAllow)||(throttleTable[i]==0))
                    setThrottleRate(i,false);
                else
                    setThrottleRate(i, true);
            }
            currentAllow++;
            //interval based 
            if(currentAllow > Config.num_epoch)
                currentAllow=1;
        }

        public override void doStep()
        {
            // for (int i = 0; i < Config.N; i++)
            // {
            // this is not needed. Can be uncommented if we want to consider the starvation
            // avg_MPKI[i].accumulate(m_starved[i]); 
            // avg_qlen[i].accumulate(m_injPools[i].Count);
            // m_starved[i] = false;
            // }

            //throttle stats
            for(int i=0;i<Config.N;i++)
            {
                if(throttleTable[i]!=0 && throttleTable[i]!=currentAllow && isThrottling)
                    Simulator.stats.throttle_time_bysrc[i].Add();
            }

            if (Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.throttle_sampling_period) == 0)
            {
                setThrottling();
                resetStat();
            }
            if (isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.interval_length) == 0)
            {
                doThrottling();
            }
        }
        /**
         *** SetThrottleTable will specify which group the workload belongs to, this will be used
         *** when we are throttling the network. The current setting is it will rank the node base on
         *** the injection (MPKI is actually the injection) and try to weight out among the workloads
         *** TODO: location-aware binning mechanism, congestion-aware binning mechanism
         ***/

        /**
         * @brief Random cluster assignment 
         **/ 
        public virtual void setThrottleTable()
        {
            //if the node is throttled, randomly assign a cluster group to it.
            for(int i = 0; i < Config.N; i++)
            {
                if(m_isThrottled[i])
                {
                    int randseed=Simulator.rand.Next(1,Config.num_epoch+1);
                    if(randseed==0 || randseed>Config.num_epoch)
                        throw new Exception("rand seed errors: out of range!");
                    throttleTable[i] = randseed;
                }
            }
#if DEBUG2
            Console.WriteLine("\nThrottle Table:");
            for(int i = 0; i < Config.N; i++)
            {
                Console.Write("(id:{0},th? {1},cluster:{2}) ",i,m_isThrottled[i],throttleTable[i]);
            }
#endif
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
        public virtual void setThrottling()
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
#if DEBUG
                if(MPKI[i]>1000)
                {
                    Console.WriteLine("node:{2} MPKI:{0} warmup cycles:{1}",MPKI[i],Config.warmup_cyc,i);
                    Console.WriteLine("insns:{0} insns_last_epoch:{1} l1misses:{2}",Simulator.stats.insns_persrc[i].Count,num_ins_last_epoch[i],L1misses[i]);
                    throw new Exception("abnormally high mpki");
                }
#endif
            }       
            recordStats();

            if(isThrottling)
            {
#if DEBUG2
                Console.WriteLine("throttle on!");
#endif
                //see if we can un-throttle the netowork
                double avg = 0.0;
                //check if we can go back to FFA
                for (int i = 0; i < Config.N; i++)
                {
#if DEBUG
                    Console.Write("[{1}] {0} |",(int)MPKI[i],i); 
#endif
                    avg = avg + MPKI[i];
                }
                avg = avg/Config.N;            
#if DEBUG
                Console.WriteLine("THROTTLING->Estimating MPKI, min_thresh {1}: avg MPKI {0}",avg,Config.MPKI_min_thresh);
#endif
                if(avg < Config.MPKI_min_thresh)
                {
#if DEBUG
                    Console.WriteLine("\n****OFF****Transition from Throttle mode to FFA! with avg MPKI {0}\n",avg);
#endif
                    isThrottling = false;
                    //un-throttle the network
                    for(int i=0;i<Config.N;i++)
                        setThrottleRate(i,false);
                }
            }
            else
            {

                if (thresholdTrigger()) // an abstract fn() that trigger whether to start throttling or not
                {
#if DEBUG
                    Console.Write("Throttle mode turned on: cycle {0} (",
                            Simulator.CurrentRound);
#endif
                    //determine if the node is congested
                    int total_high = 0;
                    for (int i = 0; i < Config.N; i++)
                    {
                        //Also uses previous sampled MPKI as a metric to avoid throttling low network
                        //intensive apps
                        if (MPKI[i] > Config.MPKI_high_node /*&& prev_MPKI[i] > Config.MPKI_high_node*/)
                        {
                            total_high++;
                            //throttleTable[i] = Simulator.rand.Next(Config.num_epoch);
                            setThrottleRate(i, true);
#if DEBUG
                            Console.Write("#ON#:Node {0} with MPKI {1} ",i,MPKI[i]);
#endif
                        }
                        else
                        {
                            throttleTable[i]=0;
                            setThrottleRate(i, false);
#if DEBUG
                            Console.Write("@OFF@:Node {0} with MPKI {1} prev_MPKI {2} ",i,MPKI[i],prev_MPKI[i]);
#endif
                        }
                    }
                    setThrottleTable();
#if DEBUG
                    Console.WriteLine(")");
#endif
                    isThrottling = true;
                    currentAllow = 1;
                }
            }
        }

        protected enum NodeState { Low = 0, HighGolden = 1, HighOther = 2, AlwaysThrottled = 3 }
        protected NodeState[] m_nodeStates = new NodeState[Config.N];

        public override int rankFlits(Flit f1, Flit f2)
        {
            if (Config.cluster_prios)
            {
                if (f1.packet.requesterID != -1 && f2.packet.requesterID != -1)
                {
                    if ((int)m_nodeStates[f1.packet.requesterID] < (int)m_nodeStates[f2.packet.requesterID])
                        return -1;
                    if ((int)m_nodeStates[f1.packet.requesterID] > (int)m_nodeStates[f2.packet.requesterID])
                        return 1;
                }
            }

            return Router_Flit_OldestFirst._rank(f1, f2);
        }
    }
}
