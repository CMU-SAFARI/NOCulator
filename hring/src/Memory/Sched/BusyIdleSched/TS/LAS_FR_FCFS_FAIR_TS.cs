using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class LAS_FR_FCFS_FAIR_TS : LAS
    {

        public LAS_FR_FCFS_FAIR_TS(int total_size, Bank[] bank)
            : base(total_size, bank)
        {
            Console.WriteLine("LAS_FCFS_FAIR_TS");
        }

        public override void tick()
        {

            base.tick();

            for (int b = 0; b < bank_max; b++) {
                for (int j = 0; j < buf_load[b]; j++) {

                    MemoryRequest req = buf[b, j];
                    //int p = req.threadID;

                    if (Simulator.CurrentRound - req.timeOfArrival > (ulong)Config.memory.max_wait_thresh) {
                        req.isMarked = true;
                    }
                }
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
                    if (!helper.is_MARK_RANK_FR_FCFS(cur_req, buf[b, j], service_rank[cur_proc], service_rank[buf[b, j].request.requesterID]))
                        cur_req = buf[b, j];
                }

                if (next_req == null || !helper.is_MARK_RANK_FR_FCFS(next_req, cur_req, service_rank[next_req.request.requesterID], service_rank[cur_req.request.requesterID])) {
                    next_req = cur_req;
                }
            }
            return next_req;
        }
    }//class
}