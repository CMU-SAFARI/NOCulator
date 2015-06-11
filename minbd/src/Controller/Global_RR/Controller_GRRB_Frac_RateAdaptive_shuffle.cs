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
//#define DEBUG_SHUFFLE

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Adaptive_Cluster_shuffle: Controller_Global_Batch
    {
        private double MPKI_close_thresh;
        private Random rand = new Random();
        public Controller_Adaptive_Cluster_shuffle()
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
            MPKI_close_thresh = 10.0;
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

                //Option1: adjust the mpki thresh for each cluster
                double diff=netutil-Config.netutil_throttling_threshold;
                //if netutil within range of 10% increase MPKI boundary for each cluster
                /*
                   double new_MPKI_thresh=cluster_pool.clusterThreshold();
                   if(diff<0.10)
                   new_MPKI_thresh*=1.05;
                   else if(diff>0.2)
                   new_MPKI_thresh*=0.95;
                   cluster_pool.changeThresh(new_MPKI_thresh);
#if DEBUG_CLUSTER
Console.WriteLine("Netutil diff: {2}-{3}={1} New MPKI thresh: {0}",new_MPKI_thresh,diff,
netutil,Config.netutil_throttling_threshold);
#endif
*/
                //Option2: adjust the throttle rate
                if(diff<0.10)
                    Config.RR_throttle_rate-=0.02;
                else if(diff>0.2)
                    if(Config.RR_throttle_rate<1.00)
                        Config.RR_throttle_rate+=0.01;
#if DEBUG_CLUSTER_RATE
                Console.WriteLine("Netutil diff: {2}-{3}={1} New th rate thresh: {0}",Config.RR_throttle_rate,diff,
                        netutil,Config.netutil_throttling_threshold);
#endif

            }


            if (thresholdTrigger()) // an abstract fn() that trigger whether to start throttling or not
            {
                List<int> sortedList = new List<int>();
                List<int> shuffleList = new List<int>();
                double total_mpki=0.0;
                double small_mpki=0.0;
                double current_allow=0.0;
                int total_high=0;
                int borderline=-1;
                int apps_to_shuffle = 0;

                for(int i=0;i<Config.N;i++)
                {
                    sortedList.Add(i);
                    total_mpki+=MPKI[i];
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
                apps_to_shuffle = 0;
                //find the first few apps that will be allowed to run freely without being throttled
                //TODO: Reshuffling the borderline cases
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
                        //borderline will be the first app that is throttled
                        if(borderline == -1)
                        {
                            borderline = node_id;
#if DEBUG_SHUFFLE
                            Console.WriteLine("borderline = {0}, MPKI = {1}",node_id,MPKI[node_id]);
#endif
                        }
#if DEBUG_AD
                        Console.WriteLine("high node: {0}",node_id);
#endif
                        if(close_by_MPKI(borderline,node_id))
                        {
                            cluster_pool.addNewNode(node_id,MPKI[node_id]);
                            total_high++;
                        }
                        else
                        {
#if DEBUG_SHUFFLE
                            Console.WriteLine("Adding {0} with {1} to shuffle list", node_id, MPKI[node_id]);
#endif
                            apps_to_shuffle++;
                            shuffleList.Add(node_id);
                        }
                    }
                } 
#if DEBUG_SHUFFLE
                Console.WriteLine("Apps to shuffle = {0}",apps_to_shuffle);
#endif
                //add all apps in the free pool that is close to the borderline case to the shuffle list
                for(int i=0;i<borderline;i++)
                {
                    if(close_by_MPKI(borderline,sortedList[i]))
                    {
#if DEBUG_SHUFFLE
                        Console.WriteLine("Adding {0} with {1} from the free pool to the shuffle list", sortedList[i],MPKI[sortedList[i]]);
                        shuffleList.Add(sortedList[i]);
#endif
                    }
                }
                //shuffle borderline apps to the throttled pool
                for(int i=0;i<apps_to_shuffle;i++)
                {
                    int rand_id = rand.Next(shuffleList.Count);
                    int node_id = shuffleList[rand_id];
#if DEBUG_SHUFFLE
                    Console.WriteLine("Adding node {0} with MPKI {1} (borderline MPKI = {2})",node_id, MPKI[node_id], MPKI[borderline]);
#endif
                    cluster_pool.addNewNode(node_id,MPKI[node_id]);
                    total_high++;
                    shuffleList.RemoveAt(rand_id);
                }
#if DEBUG_AD
                Console.WriteLine("total high: {0}\n",total_high);
#endif
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
        private bool close_by_MPKI(int node1, int node2)
        {
            if(Math.Abs(MPKI[node1]-MPKI[node2]) < MPKI_close_thresh)
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
