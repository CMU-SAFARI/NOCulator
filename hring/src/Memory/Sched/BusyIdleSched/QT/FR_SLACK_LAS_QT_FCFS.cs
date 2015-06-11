using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class FR_SLACK_LAS_QT_FCFS : LAS
    {

        public FR_SLACK_LAS_QT_FCFS(int total_size, Bank[] bank)
            : base(total_size, bank)
        {
            Console.WriteLine("FR_SLACK_LAS_QT_FCFS");
        }

        public override void tick()
        {

            base.tick();

            if (Simulator.CurrentRound % (ulong)Config.memory.quantum_size != 0)
                return;

            for (int b = 0; b < bank_max; b++) {

                for (int j = 0; j < buf_load[b]; j++) {

                    MemoryRequest req = buf[b, j];
                    //int p = req.threadID;
                    ulong wait_time = Simulator.CurrentRound - req.timeOfArrival;
                    if ((wait_time > (ulong)Config.memory.max_wait_thresh) && (wait_time <= (ulong)Config.memory.max_wait_thresh + (ulong)Config.memory.quantum_size)) {
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
            MemoryRequest fr_req = null;

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

                MemoryRequest buf_req = buf[b, 0];
                int buf_proc = buf_req.request.requesterID;
                
                for (int j = 1; j < buf_load[b]; j++) {

                    MemoryRequest cur_req = buf[b, j];

                    if (!helper.is_MARK_RANK_FR_FCFS(buf_req, cur_req, service_rank[buf_proc], service_rank[cur_req.request.requesterID]))
                        buf_req = cur_req;

                    if (cur_req.r_index != bank[b].get_cur_row())
                        continue;

                    //row-hit (fr)
                    if (fr_req != null && service_rank[fr_req.request.requesterID] < service_rank[cur_req.request.requesterID])
                        fr_req = cur_req;

                }

                if (next_req == null || !helper.is_MARK_RANK_FR_FCFS(next_req, buf_req, service_rank[next_req.request.requesterID], service_rank[buf_req.request.requesterID])) {
                    next_req = buf_req;
                }
            }

            //row-hit; return it
            if (fr_req == null || next_req.r_index == bank[next_req.glob_b_index].get_cur_row())
                return next_req;

            //no row-hit; see if we can do better.
            if (service_rank[fr_req.request.requesterID] >= Config.N / 2)
                return fr_req;
            else
                return next_req;
            
        }
    }//class
}