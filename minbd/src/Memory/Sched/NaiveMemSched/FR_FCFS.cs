using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class FR_FCFS : AbstractMemSched
    {

        public FR_FCFS(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            Console.WriteLine("Initialized FR_FCFS_MemoryScheduler");
        }

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
                if (bank[b].is_ready() && buf_load[b] > 0) {
                    // Find the highest priority request for this bank
                    MemoryRequest nextBankRequest = buf[b, 0];
                    for (int j = 1; j < buf_load[b]; j++) {
                        if (!helper.is_FR_FCFS(nextBankRequest, buf[b, j])) {
                            nextBankRequest = buf[b, j];
                        }
                    }
                    // Compare between highest priority between different banks
                    if (next_req == null || !helper.is_FCFS(next_req, nextBankRequest)) {
                        next_req = nextBankRequest;
                    }
                }
            }
            return next_req;
        }
    }//class

    class FR_FCFS_WB : AbstractMemSched
    {

        public FR_FCFS_WB(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            Console.WriteLine("Initialized FR_FCFS_MemoryScheduler_WB");
        }

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
                    MemoryRequest nextBankRequest = buf[b, 0];
                    for (int j = 1; j < buf_load[b]; j++) {
                        if (ScheduleWB && !helper.is_FR_FCFS(nextBankRequest, buf[b, j]) ||
                            (!ScheduleWB && !helper.is_NONWB_FR_FCFS(nextBankRequest, buf[b, j]))) {
                            nextBankRequest = buf[b, j];
                        }
                    }
                    // Compare between highest priority between different banks
                    if (nextRequest == null ||
                        (ScheduleWB && !helper.is_FCFS(nextRequest, nextBankRequest)) ||
                        (!ScheduleWB && !helper.is_NONWB_FCFS(nextRequest, nextBankRequest))) {
                        nextRequest = nextBankRequest;
                    }
                }
            }
            return nextRequest;
        }
    }//class
}