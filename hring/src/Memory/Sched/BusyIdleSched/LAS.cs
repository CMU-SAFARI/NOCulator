using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{

    public abstract class LAS : AbstractMemSched
    {
        public int[] service_bank = new int[Config.N];
        public double[] service_time = new double[Config.N];
        public int[] service_rank = new int[Config.N];

        public int time_decay = Config.memory.time_decay;

        protected LAS(int total_size, Bank[] bank)
            : base(total_size, bank)
        {
        }

        public override void tick(){

            base.tick();

            //update servicing banks
            for (int p = 0; p < Config.N; p++) {
                service_bank[p] = 0;
            }

            for (int b = 0; b < bank_max; b++)  // for each bank
            {
                MemoryRequest cur_req = bank[b].get_cur_req();
                if (cur_req == null)
                    continue;

                service_bank[cur_req.request.requesterID]++;
            }

            //update serviced time
            for (int p = 0; p < Config.N; p++) {

                if (Config.memory.use_weight != 0) {

                    if (Config.memory.service_overlap == true) {

                        if (service_bank[p] > 0)
                            service_time[p] += 1 / Config.memory.weight[p];

                    }
                    else {
                        service_time[p] += service_bank[p] / Config.memory.weight[p];
                    }
                }
                else {
                    if (Config.memory.service_overlap == true) {

                        if (service_bank[p] > 0)
                            service_time[p] += 1;

                    }
                    else {
                        service_time[p] += service_bank[p];
                    }
                }
            }

            //time decay
            if (time_decay > 0) {

                time_decay--;

                if (time_decay == 0) {

                    time_decay = Config.memory.time_decay;

                    for (int p = 0; p < Config.N; p++) {

                        service_time[p] /= 2;
                    }
                }
            }
            
            //periodic reset
            if (Config.memory.las_reset && Config.memory.las_periodic_reset > 0) {

                if (Simulator.CurrentRound % Config.memory.las_periodic_reset == 0) {

                    for (int p = 0; p < Config.N; p++) {
                        service_time[p] = 0;
                    }
                }
            }

            //rank
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc++) {
                int cur_rank = 0;
                for (int p = 0; p < Config.N; p++) {
                    if (service_time[p] > service_time[cur_proc])
                        cur_rank++;
                }
                service_rank[cur_proc] = cur_rank;
            }
        }
    
    }
}