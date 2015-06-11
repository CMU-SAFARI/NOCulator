using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ICSimulator
{
    // yanked from ccraik's STC impl in Net/Buffered/
    public class STC
    {
        public int[] priorities { get { return _priorities; } }
        private int[] _priorities = new int[Config.N];

        private ulong getAndClearCounterFromApp(int appID)
        {
            // simplify a bit
            //ulong numerator = Config.router.counterNumerator[appID].EndPeriod();
            //ulong denominator = Config.router.counterDenominator[appID].EndPeriod();

            ulong numerator = Simulator.stats.L1_misses_persrc_period[appID].EndPeriod();
            ulong denominator = Simulator.stats.insns_persrc_period[appID].EndPeriod();
            return (ulong)(10000.0 * numerator / denominator);
        }
        private ulong[] historyWeightedCounters = new ulong[Config.N];

        public void coordinate()
        {
            ulong[] counters = new ulong[Config.N];
            double alpha = Config.STC_history; //Config.router.priorityHistoryWeight;

            Console.Write("STC coordinate: counters ");
            for (int n = 0; n < Config.N; n++)
            {
                counters[n] = getAndClearCounterFromApp(n);
                if (alpha != 0)
                {
                    //TODO: turn to double
                    historyWeightedCounters[n] = (ulong)(Math.Ceiling(alpha * historyWeightedCounters[n])
                                                       + Math.Ceiling((1 - alpha) * counters[n]));
                    counters[n] = (ulong)historyWeightedCounters[n];
                }
                Console.Write("{0} ", (float)counters[n]);
            }
            Console.WriteLine();

            _priorities = bin(counters, Config.STC_binCount, Config.STC_binMethod); //Config.router.priorityRouteBinCount, Config.router.priorityRouteBinMethod);

            Console.Write("STC coordinate: prios    ");
            for (int i = 0; i < Config.N; i++)
                Console.Write("{0} ", _priorities[i]);
            Console.WriteLine();
        }

        public static int[] bin(ulong[] priority_data, int binCount, BinningMethod method)
        {
            if (binCount == priority_data.Length)
            {
                int[] indices = new int[binCount];
                List<ulong> rawPriorities = new List<ulong>();
                for (int n = 0; n < Config.N; n++)
                    rawPriorities.Add(priority_data[n]);
                rawPriorities.Sort();
                for (int n = 0; n < Config.N; n++)
                    indices[n] = (rawPriorities.Count - rawPriorities.IndexOf(priority_data[n]));
            }
            switch (method)
            {
                case BinningMethod.KMEANS:
                    return kmeans(priority_data, 8, binCount);
                default:
                    throw new Exception("Unhandled binning method type!");
            }
        }

        public static int[] kmeans(ulong[] tobin, int num_of_iterations, int num_of_clusters)
        {
            List<ulong>[] clusters = new List<ulong>[num_of_clusters];
            for (int i = 0; i < num_of_clusters; i++)
                clusters[i] = new List<ulong>();
            double[] cluster_sums = new double[num_of_clusters];
            int[] tobin_cluster_indices = new int[tobin.Length];

            //initialize each value to a cluster
            for (int i = 0; i < tobin.Length; i++)
            {
                int initialCluster = i % num_of_clusters;
                cluster_sums[initialCluster] += tobin[i];
                clusters[initialCluster].Add(tobin[i]);
                tobin_cluster_indices[i] = initialCluster;
            }

            //in each iteration, move each value to the nearest cluster
            for (int iter = 0; iter < num_of_iterations; iter++)
                for (int i = 0; i < tobin.Length; i++)
                {
                    int min_cluster = tobin_cluster_indices[i];
                    double minDistance = cluster_sums[min_cluster] / clusters[min_cluster].Count;

                    for (int j = 0; j < num_of_clusters; j++)
                    {
                        double distance = Math.Abs(tobin[i] - (cluster_sums[j] / clusters[j].Count));
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            min_cluster = j;
                        }
                    }
                    if (min_cluster != tobin_cluster_indices[i] && clusters[tobin_cluster_indices[i]].Count > 1)
                    {
                        //swap the item to the better cluster!
                        clusters[tobin_cluster_indices[i]].Remove(tobin[i]);
                        cluster_sums[tobin_cluster_indices[i]] -= tobin[i];

                        tobin_cluster_indices[i] = min_cluster;

                        clusters[tobin_cluster_indices[i]].Add(tobin[i]);
                        cluster_sums[tobin_cluster_indices[i]] += tobin[i];
                    }
                }

            //sort the clusters in ascending order (lower index means larger priority)
            for (int j = num_of_clusters - 1; j > 0; j--)
                for (int i = 0; i < j; i++)
                    if ((cluster_sums[i] / clusters[i].Count) > (cluster_sums[i + 1] / clusters[i + 1].Count))
                    {
                        double temp = cluster_sums[i];
                        cluster_sums[i] = cluster_sums[i + 1];
                        cluster_sums[i + 1] = temp;

                        List<ulong> temp_cluster = clusters[i];
                        clusters[i] = clusters[i + 1];
                        clusters[i + 1] = temp_cluster;
                    }

            //rebuild cluster indices (since sorting destroyed them)
            for (int i = 0; i < tobin.Length; i++)
                for (int j = 0; i < num_of_clusters; j++)
                    if (clusters[j].Contains(tobin[i]))
                    {
                        tobin_cluster_indices[i] = j;
                        break;
                    }
            return tobin_cluster_indices;
        }
    }
}
