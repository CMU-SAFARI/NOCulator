//#define DEBUG
#define DEBUG_BIN


using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Global_round_robin_group : Controller_Global_round_robin
    {
        public Controller_Global_round_robin_group()
        {
            isThrottling = false;
            Console.WriteLine("init: Global_RR");
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                numInject[i]=0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
            }
        }

        public override void setThrottleTable()
        {
            double[] mpki_node = new double[Config.N];
            int[] ranking = new int[Config.N];
            for(int i = 0; i < Config.N; i++)
            {
                mpki_node[i] = MPKI[i];
                ranking[i]=i;
                if(MPKI[i] > Config.MPKI_high_node)
                    throttleTable[i] = 1;
            }
            //sort the MPKI
            double temp1;
            int temp2;
            for(int i = 0; i < Config.N; i++)
            {
                for(int j = i+1; j < Config.N; j++)
                {
                    if(mpki_node[i]<mpki_node[j])
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
#if DEBUG_BIN
            Console.Write("ranked Table");
            for(int i=0;i<Config.N;i++)
            {
                Console.Write("{0} ",throttleTable[ranking[i]]);
            }
            Console.WriteLine("");

#endif
            temp2 = 1;
            for(int i=0;throttleTable[ranking[i]]==1 && i< Config.N;i++)
            {
            throttleTable[ranking[i]] = temp2; 
            Console.WriteLine("setting {0} to {1}", ranking[i], temp2);
                if(temp2 == Config.num_epoch)
                    temp2 = 1;
                else
                    temp2++;
            }
#if DEBUG_BIN
            Console.Write("Throttle Table");
            for(int i=0;i<Config.N;i++)
            {
                Console.Write("{0} ",throttleTable[i]);
            }
            Console.WriteLine("");
#endif
        }
    }
}
