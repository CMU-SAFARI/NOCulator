/**
 The new controller will go through a basic round robin
free-inject&throttle test to obtain each application's sensitivity to
throttling. The number of this test can be specified through
config.TWOTESTPHASES. 

If the application is not sensitive(small ipc differnece),
it will be put into an always-throttled cluster. The old mechanism that
divides applications into low and high cluster is still intact. The
additional cluster is just the always throttled cluster. The
controller tests the application sensitivity every several sampling
periods(config.sampling_period_freq).
 **/ 

//#define DEBUG_NETUTIL
//#define DEBUG
#define DEBUG_CLUSTER
#define DEBUG_NET
#define DEBUG_CLUSTER2

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_NetGain_Three_Level_Cluster: Controller_Adaptive_Cluster
    {
        public static int num_throttle_periods;
        public static int num_test_phases;

        public static bool isTestPhase;
        //nodes in these clusters are always throttled without given it a free injection slot!
        public static Cluster throttled_cluster;
        public static Cluster low_intensity_cluster;

        /* Stats for applications' sensitivity to throttling test */
        public static double[] ipc_accum_diff=new double[Config.N];
        //total ipc retired during every free injection slot
        public static double[] ipc_free=new double[Config.N];
        public static double[] ipc_throttled=new double[Config.N];

        public static ulong[] ins_free=new ulong[Config.N];
        public static ulong[] ins_throttled=new ulong[Config.N];
        public static int[]   last_free_nodes=new int[Config.N];

        //override the cluster_pool defined in Batch Controller!
        public static new UniformBatchClusterPool cluster_pool;

        //Current app within test phase2 that measure how much an app
        //hurt others
        public static int hurt_app_idx;
        public static bool free_for_all;
        public static int HURT_MEASURE=2;
        public static ulong[] last_ins_count=new ulong[Config.N];
        public static double[,] ipc_hurt=new double[Config.N,Config.N];

        public static int TWOTESTPHASES=2;

        string getName(int ID)
        {
            return Simulator.network.workload.getName(ID);
        }
        void writeNode(int ID)
        {
            Console.Write("{0} ({1}) ", ID, getName(ID));
        }

        public Controller_NetGain_Three_Level_Cluster()
        {
            isThrottling = false;
            for (int i = 0; i < Config.N; i++)
            {
                //initialization on stats for the test
                ipc_accum_diff[i]=0.0;
                ipc_free[i]=0.0;
                ipc_throttled[i]=0.0;
                ins_free[i]=0;
                ins_throttled[i]=0;
                last_free_nodes[i]=0;
                //test phase timing 
                num_throttle_periods=0;
                num_test_phases=0;
                isTestPhase=false;

                //test phase2 
                hurt_app_idx=0;
                free_for_all=true;

                MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
                lastNetUtil = 0;
            }
            throttled_cluster=new Cluster();
            low_intensity_cluster=new Cluster();
            cluster_pool=new UniformBatchClusterPool(Config.cluster_MPKI_threshold);
        }
        //0:low, 1:RR, 2:always throttled
        //TODO: use ipc_hurt_sum? this is only for %
        public int netGainDecision(double ipc_hurt_sum,double app_gain,double net_gain)
        {
            if(app_gain>=0&&ipc_hurt_sum>=0)//gain & no hurt
                return 0;
            else if(app_gain<0&&ipc_hurt_sum<0)//no gain & hurt
                return 2;
            else if(app_gain>=0&&ipc_hurt_sum<0)//gain & hurt
            {
                if(net_gain>0)
                    return 1;
                else
                    return 2;
            }
            //no gain and no hurt
            return 0;
        }

        public override void doThrottling()
        {
            //All nodes in the low intensity can alway freely inject.
            int [] low_nodes=low_intensity_cluster.allNodes();
            if(low_nodes.Length>0 && isTestPhase==true)
            {
                low_intensity_cluster.printCluster();
                throw new Exception("ERROR: Low nodes exist during sensitivity test phase.");
            }
            if(low_nodes.Length>0)
            {
#if DEBUG_CLUSTER2
            Console.WriteLine("\n:: cycle {0} ::", Simulator.CurrentRound);
            Console.Write("\nLow nodes *NOT* throttled: ");
#endif
                foreach (int node in low_nodes)
                {
#if DEBUG_CLUSTER2
                    writeNode(node);
#endif
                    setThrottleRate(node,false);
                    m_nodeStates[node] = NodeState.Low;
                }
            }

            //Throttle all the high other nodes
            int [] high_nodes=cluster_pool.allNodes();
#if DEBUG_CLUSTER2
            Console.Write("\nAll high other nodes: ");
#endif
            foreach (int node in high_nodes)
            {
#if DEBUG_CLUSTER2
                writeNode(node);
#endif
                setThrottleRate(node,true);
                m_nodeStates[node] = NodeState.HighOther;
            }

            //Unthrottle all the nodes in the free-injecting cluster
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

                    setThrottleRate(node,false);
                    m_nodeStates[node] = NodeState.HighGolden;
                    Simulator.stats.throttle_time_bysrc[node].Add();
#if DEBUG_CLUSTER2
                    writeNode(node);
#endif
                }
            }

            /* Throttle nodes in always throttled mode. */
            int [] throttled_nodes=throttled_cluster.allNodes();
            if(throttled_nodes.Length>0 && isTestPhase==true)
            {
                throttled_cluster.printCluster();
                throw new Exception("ERROR: Throttled nodes exist during sensitivity test phase.");
            }
            if(throttled_nodes.Length>0)
            {
#if DEBUG_CLUSTER2
                Console.Write("\nAlways Throttled nodes: ");
#endif
                foreach (int node in throttled_nodes)
                {
                    setThrottleRate(node,true);
                    //TODO: need another state for throttled throttled_nodes
                    m_nodeStates[node] = NodeState.AlwaysThrottled;
                    Simulator.stats.always_throttle_time_bysrc[node].Add();
#if DEBUG_CLUSTER2
                    writeNode(node);
#endif
                }
            }

#if DEBUG_CLUSTER2
            Console.Write("\n*NOT* Throttled nodes: ");
            for(int i=0;i<Config.N;i++)
                if(!m_isThrottled[i])
                    writeNode(i);
            Console.Write("\n");
#endif
        }


        public void collectIPCDiff()
        {
#if DEBUG_CLUSTER
            Console.WriteLine("\n***COLLECT IPC DIFF*** CYCLE{0}",Simulator.CurrentRound);
#endif
            int free_inj_count=Config.throttle_sampling_period/Config.interval_length/Config.N;
            int throttled_inj_count=(Config.throttle_sampling_period/Config.interval_length)-free_inj_count;
            for(int i=0;i<Config.N;i++)
            {
                ipc_free[i]/=free_inj_count;
                ipc_throttled[i]/=throttled_inj_count;
                double temp_ipc_diff;
                if(ipc_free[i]==0)
                    temp_ipc_diff=0;
                else
                {
                    if(Config.use_absolute)
                        temp_ipc_diff=ipc_free[i]-ipc_throttled[i];
                    else
                        temp_ipc_diff=(ipc_free[i]-ipc_throttled[i])/ipc_throttled[i];
                }
                //record the % difference
                ipc_accum_diff[i]+=temp_ipc_diff;
                Simulator.stats.ipc_diff_bysrc[i].Add(temp_ipc_diff);
#if DEBUG_CLUSTER
                Console.Write("id:");
                writeNode(i);
                if(Config.use_absolute)
                    Console.WriteLine("ipc_free:{0} ipc_th:{1} ipc free gain:{2}",ipc_free[i],ipc_throttled[i],temp_ipc_diff);
                else
                    Console.WriteLine("ipc_free:{0} ipc_th:{1} ipc free gain:{2}%",ipc_free[i],ipc_throttled[i],(int)(temp_ipc_diff*100));
#endif
                
                //Reset
                //ipc_accum_diff[i]=0.0;
                ipc_free[i]=0.0;
                ipc_throttled[i]=0.0;

                ins_free[i]=0;
                ins_throttled[i]=0; 
                last_free_nodes[i]=0;
            }
        }
        public void updateInsCount()
        {
#if DEBUG_CLUSTER
            Console.WriteLine("***Update instructions count*** CYCLE{0}",Simulator.CurrentRound);
#endif
            for(int node=0;node<Config.N;node++)
            {
                /* Calculate IPC during the last free-inject and throtttled interval */
                if(ins_throttled[node]==0)
                    ins_throttled[node]=Simulator.stats.insns_persrc[node].Count;
                if(last_free_nodes[node]==1)
                {
                    ulong ins_retired=Simulator.stats.insns_persrc[node].Count-ins_free[node];
                    ipc_free[node]+=(double)ins_retired/Config.interval_length;
#if DEBUG_CLUSTER
                    Console.Write("Free calc: Node ");
                    writeNode(node);
                    Console.WriteLine(" w/ ins {0}->{2} retired {1}::IPC accum:{3}",
                            ins_free[node],ins_retired,Simulator.stats.insns_persrc[node].Count,
                            ipc_free[node]);
#endif
                }
                else
                {
                    ulong ins_retired=Simulator.stats.insns_persrc[node].Count-ins_throttled[node];
                    ipc_throttled[node]+=(double)ins_retired/Config.interval_length;
                }
                ins_throttled[node]=Simulator.stats.insns_persrc[node].Count;
            }
        }

        public void unthrottleAll()
        {
            for(int i=0;i<Config.N;i++)
                setThrottleRate(i,false);
        }
 
        //Calculate the IPC when all the applications are freely running
        public void collectFreeIPC()
        {
#if DEBUG_NET
            Console.WriteLine("\n::cycle {0} ::", Simulator.CurrentRound);
            Console.Write("--Control Node: ");
            writeNode(hurt_app_idx);
            Console.WriteLine("--");
#endif
            for(int i=0;i<Config.N;i++)
            {
                if(i==hurt_app_idx) continue;
                double ins_retired=Simulator.stats.insns_persrc[i].Count-last_ins_count[i];
                //temporarily store the free for all ipc in ipc_hurt array
                ipc_hurt[hurt_app_idx,i]=ins_retired/(Config.interval_length/2);
#if DEBUG_NET
                writeNode(i);
                Console.WriteLine("->Free-For-All ipc {0} ins {1}",ipc_hurt[hurt_app_idx,i],ins_retired);
#endif
            }

        }
        public void collectIPCHurt()
        {
#if DEBUG_NET
            Console.WriteLine("\n::cycle {0} ::", Simulator.CurrentRound);
            Console.Write("--Control Node: ");
            writeNode(hurt_app_idx);
            Console.WriteLine("--");
#endif
            for(int i=0;i<Config.N;i++)
            {
                if(i==hurt_app_idx) continue;
                double ins_retired=Simulator.stats.insns_persrc[i].Count-last_ins_count[i];
                double ipc=ins_retired/(Config.interval_length/2);
                //collect the actual ipc difference when the app becomes unthrottled
                if(Config.use_absolute)
                    ipc_hurt[hurt_app_idx,i]-=ipc;
                else //calculate the percentage of hurt w.r.t one app being throttled
                    ipc_hurt[hurt_app_idx,i]=(ipc_hurt[hurt_app_idx,i]-ipc)/ipc;
#if DEBUG_NET
                writeNode(i);
                if(Config.use_absolute)
                    Console.WriteLine("->ipc hurt {0} ins {1}",ipc_hurt[hurt_app_idx,i],ins_retired);
                else
                    Console.WriteLine("->ipc hurt {0}% ins {1}",ipc_hurt[hurt_app_idx,i]*100,ins_retired);
#endif
            }
        }
        public override void doStep()
        {
            //Gather stats at the beginning of next sampling period.
            if(isTestPhase==true)
            {
                if(num_test_phases!=HURT_MEASURE)
                {
                    if (isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                            (Simulator.CurrentRound % (ulong)Config.interval_length) == 0)
                        updateInsCount();
                    //Obtain the ipc difference between free injection and throttled.
                    if (isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                            (Simulator.CurrentRound % (ulong)Config.throttle_sampling_period) == 0)
                        collectIPCDiff();
                }
                else
                {
                    if (isThrottling && Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                            (Simulator.CurrentRound % (ulong)(Config.interval_length/2)) == 0)
                    {
                        if(free_for_all)//the beginning of the next ffa
                        {
                            collectIPCHurt();
                            hurt_app_idx++;
                            hurt_app_idx%=Config.N;
                        }
                        else
                            collectFreeIPC();
                    }
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
            {
                Console.WriteLine("\n:: do throttle cycle {0} ::", Simulator.CurrentRound);
                doThrottling();
            }
            //Second test phase: trying to measure how much an app hurt others
            if (isTestPhase==true && isThrottling &&
                    Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    num_test_phases==HURT_MEASURE && 
                    (Simulator.CurrentRound % (ulong)(Config.interval_length/2)) == 0)
            {
                //update Instructions Count
                for(int i=0;i<Config.N;i++)
                    last_ins_count[i]=Simulator.stats.insns_persrc[i].Count;
                //TODO: change the throttle rate to 100%?
                if(free_for_all)
                    unthrottleAll();
                else
                    setThrottleRate(hurt_app_idx,true);
#if DEBUG_NET
                Console.WriteLine("\n:: test phase 2 do throttle cycle {0} ::", Simulator.CurrentRound);
                for(int i=0;i<Config.N;i++)
                {
                    writeNode(i);
                    Console.WriteLine("throttled:{0}",m_isThrottled[i]);
                }
#endif
                free_for_all=!free_for_all;
            }
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
                //Clear all the clusters
#if DEBUG_CLUSTER
                Console.WriteLine("cycle {0} ___Clear clusters___",Simulator.CurrentRound);
#endif
                cluster_pool.removeAllClusters();
                throttled_cluster.removeAllNodes();
                low_intensity_cluster.removeAllNodes();

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
#if DEBUG_NETUTIL
                 Console.WriteLine("Netutil diff: {2}-{3}={1} New th rate:{0} New MPKI thresh: {6} Target netutil:{4} Total MPKI:{5}",
                         Config.RR_throttle_rate,diff,
                         netutil,Config.netutil_throttling_threshold,target_netutil,total_mpki,cluster_pool.clusterThreshold());
#endif
                Simulator.stats.total_th_rate.Add(Config.RR_throttle_rate);
            }





#if DEBUG_CLUSTER
            Console.WriteLine("***SET THROTTLING Thresh trigger point*** CYCLE{0}",Simulator.CurrentRound);
#endif
            //TODO: test phase can also reduce the mpki,so...might not have consecutive test phases
            if (thresholdTrigger()) // an abstract fn() that trigger whether to start throttling or not
            {
#if DEBUG_CLUSTER
                Console.WriteLine("Congested!! Trigger throttling! isTest {0}, num th {1}, num samp {2}",
                        isTestPhase,num_test_phases,num_throttle_periods);
#endif
#if DEBUG_CLUSTER
                Console.WriteLine("cycle {0} ___Clear clusters___",Simulator.CurrentRound);
#endif
                cluster_pool.removeAllClusters();
                throttled_cluster.removeAllNodes();
                low_intensity_cluster.removeAllNodes();

                ///////State Trasition
                //Initial state
                if(num_throttle_periods==0&&num_test_phases==0)
                    isTestPhase=true;
                //From test->cluster throttling
                if(isTestPhase && num_test_phases==TWOTESTPHASES)
                {
                    isTestPhase=false;
                    num_test_phases=0;
                }
                //From cluster throttling->test
                if(num_throttle_periods==Config.sampling_period_freq)
                {
                    //reset ipc_accum_diff
                    for(int node_id=0;node_id<Config.N;node_id++)
                        ipc_accum_diff[node_id]=0.0;
                    isTestPhase=true;
                    num_throttle_periods=0;
                }

                ///////Cluster Distribution
                if(isTestPhase==true)
                {
                    num_test_phases++;
                    //Add every node to high cluster
                    int total_high = 0;
                    double total_mpki=0.0;
                    for (int i = 0; i < Config.N; i++)
                    {
                        total_mpki+=MPKI[i];
                        cluster_pool.addNewNodeUniform(i,MPKI[i]);
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
                else
                {
                    //Increment the number of throttle periods so that the test phase can kick back
                    //in after certain number of throttle periods.
                    num_throttle_periods++;
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
#if DEBUG_CLUSTER
                    for(int i=0;i<Config.N;i++)
                        Console.WriteLine("ID:{0} MPKI:{1}",sortedList[i],MPKI[sortedList[i]]);
                    Console.WriteLine("*****total MPKI: {0}",total_mpki);
                    Console.WriteLine("*****total MPKIs of apps with MPKI<30: {0}\n",small_mpki);
#endif
                    //find the first few apps that will be allowed to run freely without being throttled
                    for(int list_index=0;list_index<Config.N;list_index++)
                    {
                        int node_id=sortedList[list_index];
                        //add the node that has no performance gain from free injection slot to 
                        //always throttled cluster.
                        double ipc_avg_diff=ipc_accum_diff[node_id];
                        //Net gain = coefficient*sum(ipc_hurt)+app_gain
                        double sum=0.0;
                        for(int i=0;i<Config.N;i++)
                        {
                            if(i==node_id && ipc_hurt[node_id,i]!=0)
                                throw new Exception("Non-zero ipc hurt for own node.");
                            sum+=ipc_hurt[node_id,i];
                        }

                        double net_gain=Config.ipc_hurt_co*sum+ipc_avg_diff;

#if DEBUG_CLUSTER
                        writeNode(node_id);
                        if(Config.use_absolute)
                            Console.WriteLine("with ipc diff {1} netgain {2} mpki {3}",node_id,ipc_avg_diff,net_gain,MPKI[node_id]);
                        else
                            Console.WriteLine("with ipc diff {1}% netgain {2}% mpki {3}",node_id,ipc_avg_diff*100,net_gain*100,MPKI[node_id]);
#endif
                        //If an application doesn't fit into one cluster, it will always be throttled
                        /*if(ipc_avg_diff<Config.sensitivity_threshold || (Config.use_cluster_threshold && MPKI[node_id]>Config.cluster_MPKI_threshold))
                        {
#if DEBUG_CLUSTER
                            Console.WriteLine("->Throttled node: {0}",node_id);
#endif
                            throttled_cluster.addNode(node_id,MPKI[node_id]);
                        }
                        else
                        {
                            if(withInRange(current_allow+MPKI[node_id],Config.free_total_MPKI))
                            {
#if DEBUG_CLUSTER
                                Console.WriteLine("->Low node: {0}",node_id);
#endif
                                low_intensity_cluster.addNode(node_id,MPKI[node_id]);
                                current_allow+=MPKI[node_id];
                                continue;
                            }
                            else
                            {
#if DEBUG_CLUSTER
                                Console.WriteLine("->High node: {0}",node_id);
#endif
                                cluster_pool.addNewNode(node_id,MPKI[node_id]);
                                total_high++;
                            }
                        }*/
                        //0:low, 1:RR, 2:always throttled
                        int decision=netGainDecision(sum,ipc_avg_diff,net_gain);
                        if(decision==0)
                        {
#if DEBUG_CLUSTER
                            Console.WriteLine("->Low node: {0}",node_id);
#endif
                            low_intensity_cluster.addNode(node_id,MPKI[node_id]);
                        }
                        else if(decision==1)
                        {
#if DEBUG_CLUSTER
                            Console.WriteLine("->High node: {0}",node_id);
#endif
                            cluster_pool.addNewNode(node_id,MPKI[node_id]);
                            total_high++;
                        }
                        else if(decision==2)
                        {
#if DEBUG_CLUSTER
                            Console.WriteLine("->Throttled node: {0}",node_id);
#endif
                            throttled_cluster.addNode(node_id,MPKI[node_id]);
                            total_high++;
                        }
                        else
                            throw new Exception("Unknown decision!");
                    } 
#if DEBUG_CLUSTER
                    Console.WriteLine("total high: {0}\n",total_high);
#endif
                    //STATS
                    Simulator.stats.allowed_sum_mpki.Add(current_allow);
                    Simulator.stats.total_sum_mpki.Add(total_mpki);

                    sortedList.Clear();
                    isThrottling = (total_high>0)?true:false;
#if DEBUG_CLUSTER
                    cluster_pool.printClusterPool();
#endif
                }
            }
        }
    }
}
