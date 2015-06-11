//#define DEBUG

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Rate : Controller_ClassicBLESS
    {
        double[] m_isThrottled = new double[Config.N];
        IPrioPktPool[] m_injPools = new IPrioPktPool[Config.N];
        bool[] m_starved = new bool[Config.N];
        //This represent the round-robin turns
        int[] throttleTable = new int[Config.N];
        bool isThrottling;
        //This tell which group are allowed to run in a certain epoch
        int currentAllow = 0;
	
	int injLimit = 150;

        public Controller_Rate()
        {
            isThrottling = false;
            Console.WriteLine("init: Global_RR");
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                numInject[i]=0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=0.0;
		L1misses[i]=0;
            }
        }

        public override void resetStat()
        {
#if DEBUG
            Console.WriteLine("Reset MPKIs and num_ins after throttling");
#endif
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
                num_ins_last_epoch[i] = Simulator.stats.insns_persrc[i].Count;
                numInject[i]=0;
		L1misses[i]=0;
            }            
        }

        void setThrottleRate(int node, double cond)
        {
            m_isThrottled[node] = cond;
        }

        // true to allow injection, false to block (throttle)
        // RouterFlit uses this function to determine whether it can inject or not
        // TODO: put this in a node?
        public override bool tryInject(int node)
        {
            if(Simulator.rand.NextDouble()>m_isThrottled[node])
	    {
		    Simulator.stats.throttled_counts_persrc[node].Add();
		    return true;
	    }
	    else
		    return false;
        }

        public override void setInjPool(int node, IPrioPktPool pool)
        {
            m_injPools[node] = pool;
        }


        public override void reportStarve(int node)
        {
            m_starved[node] = true;
        }

        void doThrottling()
        {
            for(int i=0;i<Config.N;i++)
            {
		//if we want to enable interval based throttling
		//if(throttleTable[i]>0)
		//if we always throttle
		if(numInject[i]>30)
		{
			//maximum throttling rate
			if(((double)numInject[i]/(double)injLimit)>0.6)
				setThrottleRate(i,0.6);
			else
				setThrottleRate(i,((double)numInject[i]/(double)injLimit));
		}
		else
			setThrottleRate(i,0);
                //if((throttleTable[i]==currentAllow)||(throttleTable[i]==0))
                //{
                //    setThrottleRate(i,false);
                //}
                //else
                //{
                //    setThrottleRate(i, true);
                    //TODO: hack to test the common ground
                    //setThrottleRate(i,false);
                //}
            }
            currentAllow++;
            //interval based 
            if(currentAllow > Config.num_epoch)
            {
                //wrap around here
                currentAllow=1;
            }
        }

        public override void doStep()
        {
            // for (int i = 0; i < Config.N; i++)
            // {
            // this is not needed. Can be uncommented if we want to consider the starvation
            // avg_MPKI[i].accumulate(m_starved[i]); 
            // avg_qlen[i].accumulate(m_injPools[i].Count);
            // m_starved[i] = false;
            // }

            if (Simulator.CurrentRound > 20000 &&
                    (Simulator.CurrentRound % (ulong)Config.throttle_sampling_period) == 0)
            {
                setThrottling();
                resetStat();
            }
            if (isThrottling && Simulator.CurrentRound > 20000 &&
                    (Simulator.CurrentRound % (ulong)Config.interval_length) == 0)
            {
                doThrottling();
            }
        }

        void setThrottling()
        {
#if DEBUG
            Console.Write("\n:: cycle {0} ::",
                    Simulator.CurrentRound);
#endif
            //get the MPKI value
            for (int i = 0; i < Config.N; i++)
            {
                if(num_ins_last_epoch[i]==0)
                    //NumInject gets incremented in RouterFlit.cs
                    MPKI[i]=((double)(numInject[i]*1000))/(Simulator.stats.insns_persrc[i].Count);
                else
                {
                    if(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]!=0)
                        MPKI[i]=((double)(numInject[i]*1000))/(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]);
                    else
                        MPKI[i]=0;
                }
            }        

            if(isThrottling)
            {
                //see if we can un-throttle the netowork
                double avg = 0.0;
                //check if we can go back to FFA
                for (int i = 0; i < Config.N; i++)
                {
#if DEBUG
                    Console.Write("[{1}] {0} |",(int)MPKI[i],i); 
#endif
                    avg = avg + MPKI[i];
                }
                avg = avg/Config.N;            
#if DEBUG
                Console.WriteLine("Estimating MPKI, min_thresh {1}: avg MPKI {0}",avg,Config.MPKI_max_thresh);
#endif
                if(avg < Config.MPKI_min_thresh)
                {
#if DEBUG
                    Console.WriteLine("\n****OFF****Transition from Throttle mode to FFA! with avg MPKI {0}\n",avg);
#endif
                    isThrottling = false;
                    //un-throttle the network
                    for(int i=0;i<Config.N;i++)
                        setThrottleRate(i,0.0);
                }
            }
            else
            {
                double avg = 0.0;
                // determine whether any node is congested
                int total_high = 0;
                for (int i = 0; i < Config.N; i++)
                    avg = avg + MPKI[i];
                avg = avg/Config.N;
#if DEBUG
                Console.WriteLine("Estimating MPKI, max_thresh {1}: avg MPKI {0}",avg,Config.MPKI_max_thresh);
#endif
                //greater than the max threshold

                if (avg > Config.MPKI_max_thresh) // TODO: Change this to a dynamic scheme
                {
#if DEBUG
                    Console.Write("Throttle mode turned on: cycle {0} (",
                            Simulator.CurrentRound);
#endif
                    for (int i = 0; i < Config.N; i++)
                        if (MPKI[i] > Config.MPKI_high_node)
                        {
                            total_high++;
                            //right now we randomly pick one epoch to run
                            //TODO: make this more intelligent
                            throttleTable[i] = Simulator.rand.Next(Config.num_epoch);
                            //TODO: why set it here?
                            setThrottleRate(i, 0.6);
                            //TODO: hack to test the common ground
                            //setThrottleRate(i,false);
#if DEBUG
                            Console.Write("#ON#:Node {0} with MPKI {1} ",i,MPKI[i]);
#endif
                        }
                        else
                        {
                            throttleTable[i]=0;
                            setThrottleRate(i, 0.0);
#if DEBUG
                            Console.Write("@OFF@:Node {0} with MPKI {1} ",i,MPKI[i]);
#endif
                        }
#if DEBUG
                    Console.WriteLine(")");
#endif
                    isThrottling = true;
                    currentAllow = 1;
                }
            }
        }
    }
}
