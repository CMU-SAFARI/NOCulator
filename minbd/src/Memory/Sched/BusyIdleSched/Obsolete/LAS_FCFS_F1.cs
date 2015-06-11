using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class LAS_FCFS_F1 : LAS
    {

        public LAS_FCFS_F1(int total_size, Bank[] bank)
            : base(total_size, bank)
        {
            Console.WriteLine("LAS_FCFS_F1");
        }

        public override void tick()
        {

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

                service_time[p] += service_bank[p];

                //wrap-around
                if (service_time[p] >= 4000)
                    service_time[p] = 0;

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

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest next_req = null;

            //search in global/local banks
            int bank_index;
            if (Config.memory.is_shared_MC)
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            else
                bank_index = 0;

            //search for next request
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (!bank[b].is_ready() || buf_load[b] == 0)
                    continue;

                MemoryRequest cur_req = buf[b, 0];
                int cur_proc = cur_req.request.requesterID;
                for (int j = 1; j < buf_load[b]; j++) {
                    if (!helper.is_RANK_FCFS(cur_req, buf[b, j], service_rank[cur_proc], service_rank[buf[b, j].request.requesterID]))
                        cur_req = buf[b, j];
                }

                if (next_req == null || !helper.is_RANK_FCFS(next_req, cur_req, service_rank[next_req.request.requesterID], service_rank[cur_req.request.requesterID])) {
                    next_req = cur_req;
                }
            }
            return next_req;
        }
    }//class
}