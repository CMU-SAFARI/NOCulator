/**
 * @brief Same throttleing mechanism as the baseline global_round_robin scheme. However
 * this controller puts high apps into a cluster with different mechanims.
 **/ 
//#define DEBUG_DYN

using System;
using System.Collections.Generic;

namespace ICSimulator
{

    public class Controller_Global_round_robin_deflection: Controller_Global_round_robin
    {
	//weight on deflection
	public static double beta = 1.0;
	public static ulong[] last_def_flit = new ulong[Config.N];
        public Controller_Global_round_robin_deflection()
        {
            isThrottling = false;
            Console.WriteLine("init: Global_RR");
            for (int i = 0; i < Config.N; i++)
            {
                MPKI[i]=0.0;
		last_def_flit[i] = 0;
                num_ins_last_epoch[i]=0;
                m_isThrottled[i]=false;
                L1misses[i]=0;
            }
        }
	public override bool thresholdTrigger()
	{
	    //TODO: factor in the deflection and MPKI for nowa
	    double avg = 0.0;
	    for (int i = 0 ; i < Config.N ; i++)
	    {
		avg = avg + (beta * (double)(Simulator.stats.deflect_flit_bysrc[i].Count-last_def_flit[i]) );
	        last_def_flit[i] = (Simulator.stats.deflect_flit_bysrc[i].Count);
            }
	    avg = avg/(Config.N * Config.throttle_sampling_period);
#if DEBUG_DYN
            Console.WriteLine("avg deflection = {0} thres at {1}",avg, Config.throttling_threshold);
#endif
	    if(avg > Config.throttling_threshold)
		return true;
	    return false; 
	}
    }
}
