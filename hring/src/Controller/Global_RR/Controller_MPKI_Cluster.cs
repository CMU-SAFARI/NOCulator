/**
 * @brief Same throttleing mechanism as the baseline global_round_robin scheme. However
 * this controller puts high apps into a cluster with different mechanims.
 **/ 
//#define DEBUG

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_MPKI_cluster: Controller_Global_round_robin
    {
        public Controller_MPKI_cluster()
        {
            isThrottling = false;
            Console.WriteLine("init: Global_RR");
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
            }
        }

        /**
         * @brief cluster assignment-according to their mpki values
         **/ 
		public override void setThrottleTable()
		{
			double[] mpki_node = new double[Config.N];
			int[] ranking = new int[Config.N];
            int throttle_count=0;
			for(int i = 0; i < Config.N; i++)
			{
				mpki_node[i] = MPKI[i];
				ranking[i]=i;
                if(m_isThrottled[i])
                    throttle_count++;
			}
			//sort the nodes using MPKI
			double temp1;
			int temp2;
			for(int i = 0; i < Config.N; i++)
			{
				for(int j = i+1; j < Config.N; j++)
				{
					if(mpki_node[i]>mpki_node[j])
					{
						temp1=mpki_node[i];
						mpki_node[i]=mpki_node[j];
						mpki_node[j]=temp1;
						temp2=ranking[i];
						ranking[i]=ranking[j];
						ranking[j]=temp2;
					}
				}
			}

            //int cluster_size=throttle_count/Config.num_epoch;
            //int extra_size=throttle_count%Config.num_epoch;
            int idx=Config.N-throttle_count;
            for(int i=idx;i<throttle_count+idx;i++)
            {
                if(!m_isThrottled[i])
                    throw new Exception("ERROR cluster grouping: trying to put a "+
                            "non throttled node into a cluster");
                /* problem: should group based on pure mpki range. Not limiting the cluster size */
            }

		}

    }
}
