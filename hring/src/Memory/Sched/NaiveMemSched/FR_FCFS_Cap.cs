using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class FR_FCFS_Cap : AbstractMemSched
    {

        protected ulong[] bankTimers;
        protected int[] bankRowHitCount;

        public FR_FCFS_Cap(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            bankTimers = new ulong[Config.memory.bank_max_per_mem];
            bankRowHitCount = new int[Config.memory.bank_max_per_mem];
            Console.WriteLine("Initialized FR_FCFS_Cap_MemoryScheduler");
        }

        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;

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
                /*bool cap = ((Memory.memoryTime - bankTimers[i]) >= Simulator.PriorityInversionThreshold);
                if (Simulator.PriorityInversionThreshold == 0)
                {
                    if (cap == false)
                    {
                        Environment.Exit(1);
                    }
                }*/

                if (bank[b].is_ready() && buf_load[b] > 0) {
                    // Find the highest priority request for this bank
                    bool cap = (bankRowHitCount[b] > Config.memory.row_hit_cap);

                    MemoryRequest nextBankRequest = buf[b, 0];
                    for (int j = 1; j < buf_load[b]; j++) {

                        if (cap) {
                            if (!helper.is_FCFS(nextBankRequest, buf[b, j])) {
                                nextBankRequest = buf[b, j];
                            }
                        }
                        else {
                            if (!helper.is_FR_FCFS(nextBankRequest, buf[b, j])) {
                                nextBankRequest = buf[b, j];
                            }

                        }
                    }
                    // Compare between highest priority between different banks
                    if (nextRequest == null || !helper.is_FCFS(nextRequest, nextBankRequest)) {
                        nextRequest = nextBankRequest;
                    }
                }
            }

            if (nextRequest != null) {
                // Update the bank timers if the row has changed!
                if (bank[nextRequest.glob_b_index].get_cur_row() != nextRequest.r_index)
                    //bankTimers[nextRequest.bankIndex] = Memory.memoryTime;
                    bankRowHitCount[nextRequest.glob_b_index] = 0;
                else
                    bankRowHitCount[nextRequest.glob_b_index]++;

            }

            return nextRequest;
        }
    }//class

    class FR_FCFS_Cap_WB : AbstractMemSched
    {
        protected ulong[] bankTimers;

        public FR_FCFS_Cap_WB(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            bankTimers = new ulong[Config.memory.bank_max_per_mem];
            Console.WriteLine("Initialized FR_FCFS_Cap_MemoryScheduler_WB");
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
                bool cap = ((MemCtlr.cycle - bankTimers[b]) >= Config.memory.prio_inv_thresh);
                if (Config.memory.prio_inv_thresh == 0) {
                    if (cap == false) {
                        Environment.Exit(1);
                    }
                }

                if (bank[b].is_ready() && buf_load[b] > 0) {
                    // Find the highest priority request for this bank
                    MemoryRequest nextBankRequest = buf[b, 0];
                    for (int j = 1; j < buf_load[b]; j++) {

                        if (cap) {
                            if (ScheduleWB && !helper.is_FCFS(nextBankRequest, buf[b, j]) ||
                               (!ScheduleWB && !helper.is_NONWB_FCFS(nextBankRequest, buf[b, j]))) {
                                nextBankRequest = buf[b, j];
                            }
                        }
                        else {
                            if (ScheduleWB && !helper.is_FR_FCFS(nextBankRequest, buf[b, j]) ||
                               (!ScheduleWB && !helper.is_NONWB_FR_FCFS(nextBankRequest, buf[b, j]))) {
                                nextBankRequest = buf[b, j];
                            }

                        }
                    }
                    // Compare between highest priority between different banks
                    if (nextRequest == null || !helper.is_FCFS(nextRequest, nextBankRequest)) {
                        nextRequest = nextBankRequest;
                    }
                }
            }

            if (nextRequest != null) {
                // Update the bank timers if the row has changed!
                if (bank[nextRequest.glob_b_index].get_cur_row() != nextRequest.r_index)
                    bankTimers[nextRequest.glob_b_index] = MemCtlr.cycle;
            }

            return nextRequest;
        }
    }//class
}//namespace