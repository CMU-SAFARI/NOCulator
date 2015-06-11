using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace ICSimulator
{
    public class Cluster
    {
        public double mpki; 
        private List<int> nodes_q; 

        public Cluster()
        {
            nodes_q=new List<int>();
            mpki=0.0;
        }
        /**
         * @brief Add a node to the cluster.
         *
         * There's no need to create a remove function because they should never
         * be dynamically removed during a throttling period. They will only be
         * completely removed. 
         **/ 
        public void addNode(int node_id, double mpki)
        {
            this.mpki+=mpki;
            nodes_q.Add(node_id);
        }
        public int count()
        {
            return nodes_q.Count;
        }
        public double avgClusterMPKI()
        {
            return (nodes_q.Count==0)?(double)0:(double)(mpki/nodes_q.Count);
        }
        public double totalClusterMPKI()
        {
            return mpki;
        }
        public void removeAllNodes()
        {
            mpki=0.0;
            nodes_q.Clear();
        }
        public int[] allNodes()
        {
            return nodes_q.ToArray();
        }
        public void printCluster()
        {
            Console.WriteLine("\n----Cluster Nodes----");
            for(int i=0;i<nodes_q.Count;i++)
                    Console.Write("{0} ",nodes_q[i]);
        }
    }
    /**
     * @brief Pool to store all the clusters for throttling.
     *
     * This Pool sets limit on the average mpki for each cluster. 
     **/ 
    public class BatchClusterPool
    {
        public List<Cluster> q;
        //Used for keeping track of all the high intensive nodes.
        public List<int> nodes_pool;
        public double _mpki_threshold;
        public int _cluster_id;
        
        public BatchClusterPool(double mpki_threshold)
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
        public virtual void addNewNode(int id,double mpki)
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
                //try to find a cluster to fit the current node
                for(int i=0;i<q.Count;i++)
                {
                    cur_cluster=q[i];
                    double sum_MPKI=cur_cluster.totalClusterMPKI()+mpki;
                    if(sum_MPKI<=_mpki_threshold)
                    {
                        cur_cluster.addNode(id,mpki);
                        return;
                    }
                    if(i==q.Count-1 && sum_MPKI>_mpki_threshold)
                    {
                        q.Add(new Cluster());
                        //count is incremented
                        cur_cluster=q[q.Count-1];
                        cur_cluster.addNode(id,mpki);
                        return;
                    }
                }
            }
            return;
        }
        //return all the nodes that have been added to the pool
        //so far. This can help the controller to keep track of
        //high apps, so low apps id can be found.
        public int[] allNodes()
        {
            return nodes_pool.ToArray();
        }
        public bool isInHighCluster(int id)
        {
            return nodes_pool.Contains(id);
        }
        public void addEmptyCluster()
        {
            q.Add(new Cluster());
        }
        public double clusterThreshold()
        {
            return _mpki_threshold;
        }
        public int count()
        {
            return q.Count;
        }
        public void changeThresh(double mpki_threshold)
        {
            _mpki_threshold=mpki_threshold;
        }
        public int[] nodesInCluster(int cluster_id)
        {
            return q[cluster_id].allNodes();
        }
        /* Round-Robin */
        public int[] nodesInNextCluster()
        {
            if(q.Count==0)
                return new int[0];
            int [] arr=q[_cluster_id].allNodes();
            _cluster_id++;
            _cluster_id%=q.Count;
            return arr;
        }

        public void randClusterId()
        {
            if(_cluster_id!=0)
                throw new Exception("Should only randomized starting cluster ID when it's 0.");
            _cluster_id=Simulator.rand.Next(0,q.Count);
        } 

        public int clusterId()
        {
            return _cluster_id;
        }
        public void removeAllClusters()
        {
            nodes_pool.Clear();
            q.Clear();
            _cluster_id=0;
        }
        public void removeAllClusters(double mpki_threshold)
        {
            _mpki_threshold=mpki_threshold;
            nodes_pool.Clear();
            q.Clear();
            _cluster_id=0;
        }
        public void printClusterPool()
        {
            Console.Write("\n----Cluster Pool Nodes----");
            for(int i=0;i<q.Count;i++)
            {
                int []arr=q[i].allNodes();
                Console.Write("\ncluster id:{0} Nodes:",i);
                for(int j=0;j<arr.Length;j++)
                    Console.Write("{0} ",arr[j]);
            }
        }
    }
}
