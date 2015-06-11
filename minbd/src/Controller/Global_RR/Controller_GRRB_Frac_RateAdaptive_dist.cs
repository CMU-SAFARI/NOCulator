/**
 * @brief Same throttleing mechanism as the baseline global_round_robin scheme. However
 * this controller puts high apps into a cluster with batching. The number of clusters is
 * no longer static. Each cluster is batched with an average MPKI value. Therefore the number
 * of clusters depends on the number of high apps and their average MPKI value.
 *
 * TODO: can go into throttle mode with only 2 nodes, and none of them has high enough MPKI to throttle.
 *
 * Trigger metric: netutil.
 * High app metric: MPKI.
 * Batch metric: MPKI.
 **/ 

//#define DEBUG_NETUTIL
//#define DEBUG
//#define DEBUG_CLUSTER
//#define DEBUG_CLUSTER_RATE
//#define DEBUG_AD

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Adaptive_Cluster_dist: Controller_Global_Batch
    {
        public Controller_Adaptive_Cluster_dist()
        {
            isThrottling = false;
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
                lastNetUtil = 0;
            }
            cluster_pool=new BatchClusterPool_distance(Config.cluster_MPKI_threshold);
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
                double netutil=((double)(Simulator.stats.netutil.Total-lastNetUtil)/(double)Config.throttle_sampling_period);
#if DEBUG_NETUTIL
                Console.WriteLine("In throttle mode: avg netUtil = {0} thres at {1}",
                        netutil,Config.netutil_throttling_threshold);
#endif
                /* TODO:
                 * 1.If the netutil remains high, lower the threshold value for each cluster
                 * to reduce the netutil further more and create a new pool/schedule for 
                 * all the clusters. How to raise it back?
                 * Worst case: 1 app per cluster.
                 *
                 * 2.Find the difference b/w the current netutil and the threshold.
                 * Then increase the throttle rate for each cluster based on that difference.
                 *
                 * 3.maybe add stalling clusters?
                 * */
                isThrottling=false;
                //un-throttle the network
                for(int i=0;i<Config.N;i++)
                    setThrottleRate(i,false);
                cluster_pool.removeAllClusters();
                double diff=netutil-Config.netutil_throttling_threshold;

                //Option1: adjust the mpki thresh for each cluster
                //if netutil within range of 10% increase MPKI boundary for each cluster
                if(Config.adaptive_cluster_mpki)
                {
                    double new_MPKI_thresh=cluster_pool.clusterThreshold();
                    if(diff<0.10)
                        new_MPKI_thresh*=1.05;
                    else if(diff>0.2)
                        new_MPKI_thresh*=0.95;
                    cluster_pool.changeThresh(new_MPKI_thresh);
                } 
                //Option2: adjust the throttle rate
                //
                //
                //Use alpha*total_MPKI+base to find the optimal netutil for performance
                //0.5 is the baseline netutil threshold
                //0.03 is calculated empricically using some base cases to find this number b/w total_mpki and target netutil
                double total_mpki=0.0;
                for(int i=0;i<Config.N;i++)
                    total_mpki+=MPKI[i];
                
                double target_netutil=(0.03*total_mpki+50)/100;
                //50% baseline
                if(Config.alpha)
                {
                    //within 2% range
                    if(netutil<(0.98*target_netutil))
                        Config.RR_throttle_rate-=0.02;
                    else if(netutil>(1.02*target_netutil))
                        if(Config.RR_throttle_rate<0.95)//max is 95%
                            Config.RR_throttle_rate+=0.01;
                    
                    //if target is too high, only throttle max to 95% inj rate
                    if(target_netutil>0.9)
                        Config.RR_throttle_rate=0.95;
                }
                //Trying to force 60-70% netutil
                if(Config.adaptive_rate)
                {
                    if(diff<0.1)
                        Config.RR_throttle_rate-=0.02;
                    else if(diff>0.2)
                        if(Config.RR_throttle_rate<0.95)
                            Config.RR_throttle_rate+=0.01;
                }
#if DEBUG_CLUSTER_RATE
                 Console.WriteLine("Netutil diff: {2}-{3}={1} New th rate:{0} New MPKI thresh: {6} Target netutil:{4} Total MPKI:{5}",
                         Config.RR_throttle_rate,diff,
                         netutil,Config.netutil_throttling_threshold,target_netutil,total_mpki,cluster_pool.clusterThreshold());
#endif
                Simulator.stats.total_th_rate.Add(Config.RR_throttle_rate);
            }


            if (thresholdTrigger()) // an abstract fn() that trigger whether to start throttling or not
            {
                List<int> sortedList = new List<int>();
                double total_mpki=0.0;
                double small_mpki=0.0;
                double current_allow=0.0;
                int total_high=0;

                for(int i=0;i<Config.N;i++)
                {
                    sortedList.Add(i);
                    total_mpki+=MPKI[i];
                    //stats recording-see what's the total mpki composed by low/med apps
                    if(MPKI[i]<=30)
                        small_mpki+=MPKI[i];
                }
                //sort by mpki
                sortedList.Sort(CompareByMpki);
#if DEBUG_AD
                for(int i=0;i<Config.N;i++)
                    Console.WriteLine("ID:{0} MPKI:{1}",sortedList[i],MPKI[sortedList[i]]);
                Console.WriteLine("*****total MPKI: {0}",total_mpki);
                Console.WriteLine("*****small MPKI: {0}\n",small_mpki);
#endif
                //find the first few apps that will be allowed to run freely without being throttled
                for(int i=0;i<Config.N;i++)
                {
                    int node_id=sortedList[i];
                    if(withInRange(current_allow+MPKI[node_id],Config.free_total_MPKI))
                    {
                        current_allow+=MPKI[node_id];
                        continue;
                    }
                    else
                    {
#if DEBUG_AD
                        Console.WriteLine("high node: {0}",node_id);
#endif
                        cluster_pool.addNewNode(node_id,MPKI[node_id]);
                        total_high++;
                    }
                } 
#if DEBUG_AD
                Console.WriteLine("total high: {0}\n",total_high);
#endif
                //STATS
                Simulator.stats.allowed_sum_mpki.Add(current_allow);
                Simulator.stats.total_sum_mpki.Add(total_mpki);

                sortedList.Clear();
                isThrottling = (total_high>0)?true:false;
            }
        }
        //within 10% of range
        private bool withInRange(double mpki,double target)
        {
            if(mpki<=(target*1.1))
                return true;
            return false;
        }
        private int CompareByMpki(int x,int y)
        {
            if(MPKI[x]-MPKI[y]>0.0) return 1;
            else if(MPKI[x]-MPKI[y]<0.0) return -1;
            return 0;
        }
    }
}
