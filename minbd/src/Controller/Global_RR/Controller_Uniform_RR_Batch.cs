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
#define DEBUG_CLUSTER
//#define DEBUG_CLUSTER2
#define DEBUG_CLUSTER_RATE

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Uniform_Batch: Controller_Global_Batch
    {
        public static double[] ipc_diff=new double[Config.N];
        //total ipc retired during every free injection slot
        public static double[] ipc_free=new double[Config.N];
        public static double[] ipc_throttled=new double[Config.N];

        public static ulong[] ins_free=new ulong[Config.N];
        public static ulong[] ins_throttled=new ulong[Config.N];
        public static int[]   last_free_nodes=new int[Config.N];

        public Controller_Uniform_Batch()
        {
            isThrottling = false;
            for (int i = 0; i < Config.N; i++)
            {
                ipc_diff[i]=0.0;
                ipc_free[i]=0.0;
                ipc_throttled[i]=0.0;

                ins_free[i]=0;
                ins_throttled[i]=0;
                last_free_nodes[i]=0;

                MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
                lastNetUtil = 0;
            }
            cluster_pool=new UniformBatchClusterPool(Config.cluster_MPKI_threshold);
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
            Console.Write("\n:: cycle {0} ::", Simulator.CurrentRound);
            foreach (int node in high_nodes)
            {
                setThrottleRate(node,true);
                m_nodeStates[node] = NodeState.HighOther;
            }

#if DEBUG_CLUSTER2
            Console.Write("\nLow nodes *NOT* throttled: ");
            for(int i=0;i<Config.N;i++)
                if(!m_isThrottled[i])
                {
                    Console.Write("{0} ",i);
                    throw new Exception("All nodes should be in high cluster in uniform cluster controller");
                }
#endif

            //Unthrottle all the nodes in the cluster
            int [] nodes=cluster_pool.nodesInNextCluster();
#if DEBUG_CLUSTER2
            Console.Write("\nUnthrottling cluster nodes: ");
#endif
            if(nodes.Length>0)
            {
                //Clear the vector for last free nodes
                Array.Clear(last_free_nodes,0,last_free_nodes.Length);
                foreach (int node in nodes)
                {
                    ins_free[node]=Simulator.stats.insns_persrc[node].Count;
                    last_free_nodes[node]=1;
                    Console.Write("\nFree Turn: Node {0} w/ ins {1} ",node,ins_free[node]);

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
            if (isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)Config.interval_length) == 0)
            {
                for(int node=0;node<Config.N;node++)
                {
                    /* Calculate IPC during the last free-inject and throtttled interval */
                    if(ins_throttled[node]==0)
                        ins_throttled[node]=Simulator.stats.insns_persrc[node].Count;
                    if(last_free_nodes[node]==1)
                    {
                        ulong ins_retired=Simulator.stats.insns_persrc[node].Count-ins_free[node];
                        ipc_free[node]+=(double)ins_retired/Config.interval_length;
                        Console.Write("\nFree calc: Node {0} w/ ins {1}->{3} retired {2}::IPC accum:{4}",
                                node,ins_free[node],ins_retired,Simulator.stats.insns_persrc[node].Count,
                                ipc_free[node]);
                    }
                    else
                    {
                        ulong ins_retired=Simulator.stats.insns_persrc[node].Count-ins_throttled[node];
                        ipc_throttled[node]+=(double)ins_retired/Config.interval_length;
                    }
                    ins_throttled[node]=Simulator.stats.insns_persrc[node].Count;
                }
            }

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
                doThrottling();
        }

        public override void setThrottling()
        {
            if(isThrottling)
            {
                Console.WriteLine("\n:: cycle {0} ::",
                        Simulator.CurrentRound);
                int free_inj_count=Config.throttle_sampling_period/Config.interval_length/Config.N;
                int throttled_inj_count=(Config.throttle_sampling_period/Config.interval_length)-free_inj_count;
                for(int i=0;i<Config.N;i++)
                {
                    ipc_free[i]/=free_inj_count;
                    ipc_throttled[i]/=throttled_inj_count;
                    if(ipc_free[i]==0)
                        ipc_diff[i]=0;
                    else
                        ipc_diff[i]=(ipc_free[i]==0)?0:(ipc_free[i]-ipc_throttled[i])/ipc_throttled[i];
                    //record the % difference
                    Simulator.stats.ipc_diff_bysrc[i].Add(ipc_diff[i]);
                    Console.WriteLine("id:{0} ipc_free:{1} ipc_th:{2} ipc free gain:{3}%",
                            i,ipc_free[i],ipc_throttled[i],(int)(ipc_diff[i]*100));
                    
                    //Reset
                    ipc_diff[i]=0.0;
                    ipc_free[i]=0.0;
                    ipc_throttled[i]=0.0;

                    ins_free[i]=0;
                    ins_throttled[i]=0; 
                    last_free_nodes[i]=0;
                }
            }

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
#if DEBUG_NETUTIL
                double netutil=((double)(Simulator.stats.netutil.Total-lastNetUtil)/(double)Config.throttle_sampling_period);
                Console.WriteLine("In throttle mode: avg netUtil = {0} thres at {1}",
                        netutil,Config.netutil_throttling_threshold);
#endif
                isThrottling=false;
                //un-throttle the network
                for(int i=0;i<Config.N;i++)
                    setThrottleRate(i,false);
                cluster_pool.removeAllClusters();
                Simulator.stats.total_th_rate.Add(Config.RR_throttle_rate);
            }


            if (thresholdTrigger()) // an abstract fn() that trigger whether to start throttling or not
            {
                //Add every node to high cluster
                int total_high = 0;
                double total_mpki=0.0;
                for (int i = 0; i < Config.N; i++)
                {
                    total_mpki+=MPKI[i];
                    cluster_pool.addNewNode(i,MPKI[i]);
                    total_high++;
#if DEBUG
                    Console.Write("#ON#:Node {0} with MPKI {1} ",i,MPKI[i]);
#endif
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

    public class UniformBatchClusterPool: BatchClusterPool
    {
        public UniformBatchClusterPool(double mpki_threshold)
        :base(mpki_threshold)
        {
            _mpki_threshold=mpki_threshold;
            q=new List<Cluster>();
            nodes_pool=new List<int>();
            _cluster_id=0;
        }
 
        public void addNewNodeUniform(int id, double mpki)
        {
            nodes_pool.Add(id);
            q.Add(new Cluster());
            Cluster cur_cluster=q[q.Count-1];
            cur_cluster.addNode(id,mpki);
            return;
        }
    }
}
