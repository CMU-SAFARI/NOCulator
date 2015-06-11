using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class FCFS : AbstractMemSched
    {
        public FCFS(int total_size, Bank[] bank)
            : base(total_size, bank)
        {
            Console.WriteLine("Initialized FCFS_MemoryScheduler");
        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest next_req = null;

            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC) {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (!bank[b].is_ready() || buf_load[b] == 0)
                    continue;

                if (next_req == null)
                    next_req = buf[b, 0];

                for (int j = 0; j < buf_load[b]; j++) {
                    if (!helper.is_FCFS(next_req, buf[b, j]))
                        next_req = buf[b, j];
                }
            }
            return next_req;
        }
    }//class

    class FCFS_MemoryScheduler_WB : AbstractMemSched
    {

        public FCFS_MemoryScheduler_WB(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            Console.WriteLine("Initialized FCFS_MemoryScheduler_WB");
        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;
            bool ScheduleWB = (this.get_wb_fraction() > Config.memory.wb_full_ratio);

            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC) {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0) {
                    // Find the highest priority request for this bank
                    if (nextRequest == null) nextRequest = buf[b, 0];
                    for (int j = 0; j < buf_load[b]; j++) {
                        if (ScheduleWB && !helper.is_FCFS(nextRequest, buf[b, j]) ||
                           (!ScheduleWB && !helper.is_NONWB_FCFS(nextRequest, buf[b, j]))) {
                            nextRequest = buf[b, j];
                        }
                    }
                }
            }
            return nextRequest;
        }
    }//class
}