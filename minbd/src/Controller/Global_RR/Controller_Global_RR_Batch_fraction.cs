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

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Global_Batch_fraction: Controller_Global_Batch
    {
        public static double MPKI_desired_target;

        public Controller_Global_Batch_fraction()
        {
            isThrottling = false;
            MPKI_desired_target = 500;
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
                lastNetUtil = 0;
            }
            cluster_pool=new BatchClusterPool(Config.cluster_MPKI_threshold);
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
                /* 
                 * If the netutil remains high, lower the threshold value for each cluster
                 * to reduce the netutil further more and create a new pool/schedule for 
                 * all the clusters.
                 *
                 * Worst case: 1 app per cluster.
                 * TODO: maybe add stalling clusters?
                 * */
                isThrottling=false;
                //un-throttle the network
                for(int i=0;i<Config.N;i++)
                    setThrottleRate(i,false);
                cluster_pool.removeAllClusters();

                double new_MPKI_thresh=cluster_pool.clusterThreshold();
                //Sets a lower bound on the threshold value.
                double lower_bound=0.45;
                if(netutil > Config.netutil_throttling_threshold)
                {
                    new_MPKI_thresh=(new_MPKI_thresh*0.9<lower_bound)?new_MPKI_thresh*(1+Config.thresholdWeight):new_MPKI_thresh*(1-Config.thresholdWeight);//increase or decrease it by 10% depending on how off are we from the threshold
                    MPKI_desired_target = (netutil<lower_bound)?(MPKI_desired_target*(1-Config.thresholdWeight)):(MPKI_desired_target*(1+Config.thresholdWeight));
                    cluster_pool.changeThresh(new_MPKI_thresh);
                    //TODO: perhaps add multiple?
                    //cluster_pool.addEmptyCluster();
#if DEBUG_CLUSTER
                    Console.WriteLine("New MPKI thresh: {0}",new_MPKI_thresh);
#endif

                }
            }


            if (thresholdTrigger()) // an abstract fn() that trigger whether to start throttling or not
            {
                List<int> tempHigh = new List<int>();
                //determine if the node is high intensive by using MPKI
                int total_high = 0;
                double current_MPKI_cluster = 0.0;
                for (int i = 0; i < Config.N; i++)
                {
                    if (MPKI[i] > Config.MPKI_high_node)
                    {
                        tempHigh.Add(i);
                        //cluster_pool.addNewNode(i,MPKI[i]);
                        total_high++;
                    }
                }
                // if there is no need to throttle. This should not be the case because if netutil > some threshold, there should be some high nodes exist in the network
                isThrottling = (total_high>0)?true:false;
#if DEBUG
                Console.WriteLine("total High = {0}",total_high);
#endif
                Random randGen = new Random();
                while(tempHigh.Count>0 && current_MPKI_cluster < MPKI_desired_target)
                {
                    int index = randGen.Next(total_high--);
                    cluster_pool.addNewNode(tempHigh[index],MPKI[tempHigh[index]]);
#if DEBUG
                    Console.WriteLine("Adding {0} to the cluster", tempHigh[index]);
#endif
                    current_MPKI_cluster += MPKI[tempHigh[index]];
                    tempHigh.RemoveAt(index);
                }
#if DEBUG
                for(int i=0;i<Config.N;i++)
                {
                    if(cluster_pool.isInHighCluster(i))
                        Console.Write("#ON#:Node {0} with MPKI {1} ",i,MPKI[i]);
                    else
                        Console.Write("@OFF@:Node {0} with MPKI {1} ",i,MPKI[i]);
                }
                Console.WriteLine(")");
#endif
            }
        }
    }
}
