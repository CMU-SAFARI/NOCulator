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
//#define DEBUG_CLUSTER2
//#define DEBUG_CLUSTER_RATE

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Global_Batch: Controller_Global_round_robin
    {
        public static double lastNetUtil;
        //a pool of clusters for throttle mode
        public static BatchClusterPool cluster_pool;

        public Controller_Global_Batch()
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
            cluster_pool=new BatchClusterPool(Config.cluster_MPKI_threshold);
        }

        public override void doThrottling()
        {
            //Unthrottle all nodes b/c could have nodes being throttled
            //in last sample period, but not supposed to be throttle in
            //this period.
            for(int i=0;i<Config.N;i++)
            {
                setThrottleRate(i,false);
                m_nodeStates[i] = NodeState.Low;
            }

            //Throttle all the high nodes
            int [] high_nodes=cluster_pool.allNodes();
            foreach (int node in high_nodes)
            {
                setThrottleRate(node,true);
                m_nodeStates[node] = NodeState.HighOther;
            }

#if DEBUG_CLUSTER2
            Console.Write("\nLow nodes *NOT* throttled: ");
            for(int i=0;i<Config.N;i++)
                if(!m_isThrottled[i])
                    Console.Write("{0} ",i);
#endif

            //Unthrottle all the nodes in the cluster
            int [] nodes=cluster_pool.nodesInNextCluster();
#if DEBUG_CLUSTER2
            Console.Write("\nUnthrottling cluster nodes: ");
#endif
            if(nodes.Length>0)
            {
                foreach (int node in nodes)
                {
                    setThrottleRate(node,false);
                    m_nodeStates[node] = NodeState.HighGolden;
                    Simulator.stats.throttle_time_bysrc[node].Add();
#if DEBUG_CLUSTER2
                    Console.Write("{0} ",node);
#endif
                }
            }

#if DEBUG_CLUSTER2
            Console.Write("\nThrottled nodes: ");
            for(int i=0;i<Config.N;i++)
                if(m_isThrottled[i])
                    Console.Write("{0} ",i);
            Console.Write("\n*NOT* Throttled nodes: ");
            for(int i=0;i<Config.N;i++)
                if(!m_isThrottled[i])
                    Console.Write("{0} ",i);
            Console.Write("\n");
#endif
        }
        
        public override void doStep()
        {
            //sampling period: Examine the network state and determine whether to throttle or not
            if (Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.throttle_sampling_period) == 0)
            {
                setThrottling();
                lastNetUtil = Simulator.stats.netutil.Total;
                resetStat();
            }
            //once throttle mode is turned on. Let each cluster run for a certain time interval
            //to ensure fairness. Otherwise it's throttled most of the time.
            if (isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.interval_length) == 0)
            {
                doThrottling();
            }
        }

        /**
         * @brief Determines whether the network is congested or not 
         **/ 
        public override bool thresholdTrigger()
        {
            double netutil=((double)(Simulator.stats.netutil.Total -lastNetUtil)/ (double)Config.throttle_sampling_period);
#if DEBUG_NETUTIL
            Console.WriteLine("avg netUtil = {0} thres at {1}",netutil,Config.netutil_throttling_threshold);
#endif
            return (netutil > Config.netutil_throttling_threshold)?true:false;
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
                        new_MPKI_thresh+=10;
                    else if(diff>0.2)
                        new_MPKI_thresh-=20;
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
                //determine if the node is high intensive by using MPKI
                int total_high = 0;
                double total_mpki=0.0;
                for (int i = 0; i < Config.N; i++)
                {
                    total_mpki+=MPKI[i];
                    if (MPKI[i] > Config.MPKI_high_node)
                    {
                        cluster_pool.addNewNode(i,MPKI[i]);
                        total_high++;
                        //TODO: add stats?
#if DEBUG
                        Console.Write("#ON#:Node {0} with MPKI {1} ",i,MPKI[i]);
#endif
                    }
                    else
                    {
#if DEBUG
                        Console.Write("@OFF@:Node {0} with MPKI {1} ",i,MPKI[i]);
#endif
                    }
                }
                Simulator.stats.total_sum_mpki.Add(total_mpki);
#if DEBUG
                Console.WriteLine(")");
#endif
                //if no node needs to be throttled, set throttling to false
                isThrottling = (total_high>0)?true:false;
#if DEBUG_CLUSTER
                cluster_pool.printClusterPool();
#endif
            }
        }
    }
}
