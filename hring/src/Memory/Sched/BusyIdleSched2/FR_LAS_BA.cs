using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{

    public class FR_LAS_BA : AbstractMemSched
    {
        public int[] service_bank = new int[Config.N];
        public long[] service_time = new long[Config.N];
        public double[] service_history = new double[Config.N];
        public int[] service_rank = new int[Config.N];

        public int batch_cycles_left = Config.memory.LAS_BA_batch_cycles;
        public int threshold_cycles = Config.memory.LAS_BA_threshold_cycles;
        public double history_weight = Config.memory.LAS_BA_history_weight;

        public FR_LAS_BA(int total_size, Bank[] bank)
            : base(total_size, bank)
        {
        }

        public override void tick()
        {
            base.tick();

            //count how many banks are servicing a processor
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

                if (Config.memory.service_overlap == true) {

                    if (service_bank[p] > 0)
                        service_time[p] += 1;

                }
                else {
                    service_time[p] += service_bank[p];
                }
            }

            //mark really old requests
            for (int b = 0; b < bank_max; b++) {

                for (int j = 0; j < buf_load[b]; j++) {

                    MemoryRequest req = buf[b, j];

                    if (MemCtlr.cycle - req.timeOfArrival > (ulong)threshold_cycles) {
                        req.isMarked = true;
                    }
                }
            }

            //check if this batch has terminated
            if (batch_cycles_left > 0) {
                batch_cycles_left--;
                return;
            }

            //new batch
            batch_cycles_left = Config.memory.LAS_BA_batch_cycles;

            //update service history
            for (int p = 0; p < Config.N; p++) {

                service_history[p] = history_weight * service_history[p]
                                   + (1 - history_weight) * service_time[p];

                service_time[p] = 0;
            }

            //new rank for the batch
            for (int cur_proc = 0; cur_proc < Config.N; cur_proc++) {

                int cur_rank = 0;

                for (int p = 0; p < Config.N; p++) {

                    if (service_history[p] > service_history[cur_proc])
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
            if (Config.memory.is_shared_MC) {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else {
                bank_index = 0;
            }

            //search for next request
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (!bank[b].is_ready() || buf_load[b] == 0)
                    continue;

                MemoryRequest cur_req = buf[b, 0];
                int cur_proc = cur_req.request.requesterID;
                for (int j = 1; j < buf_load[b]; j++) {
                    if (!helper.is_MARK_FR_RANK_FCFS(cur_req, buf[b, j], service_rank[cur_proc], service_rank[buf[b, j].request.requesterID]))
                        cur_req = buf[b, j];
                }

                if (next_req == null || !helper.is_MARK_FR_RANK_FCFS(next_req, cur_req, service_rank[next_req.request.requesterID], service_rank[cur_req.request.requesterID])) {
                    next_req = cur_req;
                }
            }
            return next_req;
        }

    }
}