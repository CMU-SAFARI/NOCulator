/**
 * @brief Same throttleing mechanism as the baseline global_round_robin scheme. However
 * this controller puts high apps into a cluster with different mechanims.
 **/ 
//#define DEBUG_NETUTIL
//#define EXTRA_DEBUG

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Global_round_robin_netutil: Controller_Global_round_robin
    {
        public static double lastNetUtil;
        public static int total_cluster;

        public Controller_Global_round_robin_netutil()
        {
            isThrottling = false;
            Console.WriteLine("init: Global_RR");
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
                lastNetUtil = 0;
                total_cluster=Config.num_epoch;
            }
        }
        public override void doThrottling()
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
            if(currentAllow > total_cluster)
                currentAllow=1;
        }
        /**
         * @brief Add cluster to be short live to reduce netutil
         * 
         * 1.determine netutil 
         * 2.add cluster that has short life
         * 3.Pick the highest MPKI node from each cluster and add it to
         * the new cluster
         * TODO: shufftle the apps to be picked
         **/ 
        public void addShortLifeCluster()
        {
            int short_life_id=total_cluster+1;
            int empty_count=0;
            //id=0 means not throttled. Pick apps from the original clusters
            for(int id=1;id<=Config.num_epoch;id++)
            {
                int max_id=-1;
                double maxMPKI=0.0;
                for(int i=0;i<Config.N;i++)
                {
                    if(throttleTable[i]==id && MPKI[i]>maxMPKI)
                    {
                        maxMPKI=MPKI[i];
                        max_id=i;
                    }
                }
                if(max_id>-1)
                {
#if EXTRA_DEBUG
                    Console.WriteLine("Node {0} added to Scluster {1}",max_id,short_life_id);
#endif
                    throttleTable[max_id]=short_life_id;
                }
                else
                    empty_count++;
            }
            if(empty_count<Config.num_epoch)
                total_cluster++;
        }
        
        /**
         * @brief Removes all the short life clusters and 
         * distribute apps in the cluster to other clusters randomly.
         **/ 
        public void removeShortLifeCluster()
        {
#if EXTRA_DEBUG
            Console.WriteLine("\n****Removing the short life cluster");
#endif
            //reset it
            total_cluster=Config.num_epoch;
            for(int i=0;i<Config.N;i++)
            {
                if(throttleTable[i]>Config.num_epoch)
                {
                    //randomly distribute it to another cluster
                    int randseed=Simulator.rand.Next(1,Config.num_epoch+1);
                    if(randseed==0 || randseed>Config.num_epoch)
                        throw new Exception("rand seed errors: out of range!");
                    throttleTable[i]=randseed;
                }
            }
        }

        public override void doStep()
        {
            //throttle stats
            for(int i=0;i<Config.N;i++)
            {
                if(throttleTable[i]!=0 && throttleTable[i]!=currentAllow && isThrottling)
                    Simulator.stats.throttle_time_bysrc[i].Add();
            }
            
            //sampling period: Examine the network state and determine whether to throttle or not
            if (Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.throttle_sampling_period) == 0)
            {
                //In case the removecluster never happends at small intervals. Deallocate
                //this cluster to ensure reshuffling of apps in the short life cluster.
                if(total_cluster>Config.num_epoch)
                    removeShortLifeCluster();            
                setThrottling();
                lastNetUtil = Simulator.stats.netutil.Total;
                resetStat();
            }
            //once throttle mode is turned on. Let each cluster run for a certain time interval
            //to ensure fairness. Otherwise it's throttled most of the time.
            if (isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.interval_length) == 0)
            {
                ulong cycles_elapsed=Simulator.CurrentRound%(ulong)Config.throttle_sampling_period;
                double netutil=(cycles_elapsed==0)?(double)Simulator.stats.netutil.Total:
                    ((double)(Simulator.stats.netutil.Total -lastNetUtil)/cycles_elapsed);
                        
#if EXTRA_DEBUG
                //Console.WriteLine("Interval: netutil {0} should be a double",netutil);
#endif
                doThrottling();
                if(netutil>Config.netutil_throttling_threshold)
                    addShortLifeCluster();
                if(netutil<Config.netutil_throttling_threshold && total_cluster>Config.num_epoch)
                    removeShortLifeCluster();            
            }
            else if(isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&    
                    //more than 1 short life cluster
                    total_cluster>Config.num_epoch && 
                    //After a short life cluster has run by looking at next currentAllow
                    //b/c currentAllow will only be back to the first or within the extra id region.
                    (currentAllow==1||currentAllow>(Config.num_epoch+1)) &&
                    (Simulator.CurrentRound % (ulong)Config.short_life_interval)==0)
            {
                //after a short life for the cluster, yield its left over interval to other clusters
                doThrottling();
            }
            if(total_cluster<Config.num_epoch)
            {
                Console.WriteLine("Incorrect total cluster {0}",total_cluster);
                throw new Exception("Incorrect total cluster");
            }
        }
        public override bool thresholdTrigger()
        {
            double netutil=((double)(Simulator.stats.netutil.Total -lastNetUtil)/ (double)Config.throttle_sampling_period);
#if DEBUG_NETUTIL
            Console.WriteLine("avg netUtil = {0} thres at {1}",netutil,Config.netutil_throttling_threshold);
#endif
            return (netutil > (double)Config.netutil_throttling_threshold)?true:false;
        }

        public override void setThrottling()
        {
#if DEBUG_NETUTIL
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
            recordStats();

            if(isThrottling)
            {
                //see if we can un-throttle the netowork
#if DEBUG_NETUTIL
                Console.WriteLine("In throttle mode: avg netUtil = {0} thres at {1}",(double)((Simulator.stats.netutil.Total-lastNetUtil)/(double)Config.throttle_sampling_period), Config.netutil_throttling_threshold);
#endif
                if(((double)(Simulator.stats.netutil.Total -lastNetUtil)/ (double)Config.throttle_sampling_period) < (double)Config.netutil_throttling_threshold)
                {
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
                    //determine if the node is high intensive by using MPKI
                    int total_high = 0;
                    for (int i = 0; i < Config.N; i++)
                    {
                        if (MPKI[i] > Config.MPKI_high_node)
                        {
                            total_high++;
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
    }
}
