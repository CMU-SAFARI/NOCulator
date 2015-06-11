//#define DEBUG_DIST

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;


namespace ICSimulator
{
    public class BatchClusterPool_distance: BatchClusterPool
    {

        //List<Cluster> q;
        //Used for keeping track of all the high intensive nodes.
        //List<int> nodes_pool;
        //private double _mpki_threshold;
        //private int _cluster_id;

        public BatchClusterPool_distance(double mpki_threshold):base(mpki_threshold)
        {
            _mpki_threshold=mpki_threshold;
            q=new List<Cluster>();
            nodes_pool=new List<int>();
            _cluster_id=0;
        }

        /**
         * @brief Add a new node to the cluster. 
         *
         * If the new node doesn't exceed the total mpki of the most recent added cluster,
         * the node is inserted to that cluster. Otherwise, it's added to a new cluster.
         **/ 
        public override void addNewNode(int id,double mpki)
        {
            nodes_pool.Add(id);
            Cluster cur_cluster;
            if(q.Count==0)
            {
                q.Add(new Cluster());
                cur_cluster=q[0];
                cur_cluster.addNode(id,mpki);
                return;
            }
            else
            {
                List<int> availCluster = new List<int>();
                bool needNewCluster = true;
                double maxDist = 0;
                Cluster selectedCluster = q[0];
                for(int i=0;i<q.Count;i++)
                {
                    cur_cluster=q[i];
                    double sum_MPKI=cur_cluster.totalClusterMPKI()+mpki;
#if DEBUG_DIST
                    Console.WriteLine("sum_MPKI = {0}, mpki_threshold = {1}", sum_MPKI, _mpki_threshold);
#endif
                    if(sum_MPKI<=_mpki_threshold)
                    {
                        //cur_cluster.addNode(id,mpki);
                        availCluster.Add(i);
                        needNewCluster = false;
                        //return;
                    }
                }   
                for(int i=0;i<availCluster.Count;i++)
                {
                    double avgDist = 0.0;
                    cur_cluster = q[i];
                    int [] nodes = cur_cluster.allNodes();
                    for(int j=0;j<nodes.Length;j++)
                        avgDist+= computeDist(nodes[j],id);
                    avgDist = avgDist/nodes.Length;
                    if(avgDist>maxDist)
                    {
                        selectedCluster = cur_cluster;
                        maxDist = avgDist;
                    }
                }
                if(needNewCluster)
                {
#if DEBUG_DIST
                Console.WriteLine("Adding a new cluster");
#endif
                    q.Add(new Cluster());
                    //count is incremented
                    cur_cluster=q[q.Count-1];
                    cur_cluster.addNode(id,mpki);
                    return;
                }
                else
                {
#if DEBUG_DIST
                int [] allNodes = selectedCluster.allNodes();
                Console.Write("Adding {0} to cluster with nodes: ",id);
                for(int i=0;i<allNodes.Length;i++)
                    Console.Write("{0} ",allNodes[i]);
                Console.WriteLine("");
#endif
                    selectedCluster.addNode(id,mpki);
                }
            }
            return;
        }
        
        //a helpter function that compute the distance between node i and j
        private int computeDist(int i, int j)
        {
            return (Math.Abs((i%4)-(j%4)))+(Math.Abs((i/4)-(j/4)));
        }


    }
}
