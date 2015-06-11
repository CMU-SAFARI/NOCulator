using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class FULL_BATCH_RV : BatchMemSched
    {
        ulong batch_begin_time;
        bool is_batch; //is a batch currently active

        int cur_rank;

        public FULL_BATCH_RV(int total_size, Bank[] bank, RankAlgo rank_algo, BatchSchedAlgo batch_sched_algo)
            : base(total_size, bank, rank_algo, batch_sched_algo)
        {
            Console.WriteLine("Initialized FULL_BATCH_RV_MemoryScheduler");
        }

        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest next_req = null;

            // Get the highest priority request.
            int bindex_low;
            if (Config.memory.is_shared_MC)
                bindex_low = mem.mem_id * Config.memory.bank_max_per_mem;
            else
                bindex_low = 0;

            for (int b = bindex_low; b < bindex_low + mem.bank_max; b++)  // for each bank
            {
                if (!bank[b].is_ready() || buf_load[b] == 0)
                    continue;

                if (next_req == null)
                    next_req = buf[b, 0];

                for (int j = 0; j < buf_load[b]; j++) {

                    MemoryRequest req = buf[b, j];

                    switch (Config.memory.batch_sched_algo) {

                        case BatchSchedAlgo.MARKED_FR_RANK_FCFS:
                            if (!helper.is_MARK_FR_RANK_FCFS(next_req, req, proc_to_rank[next_req.request.requesterID], proc_to_rank[req.request.requesterID]))
                                next_req = req;
                            break;

                        case BatchSchedAlgo.MARKED_RANK_FR_FCFS:
                            if (!helper.is_MARK_RANK_FR_FCFS(next_req, req, proc_to_rank[next_req.request.requesterID], proc_to_rank[req.request.requesterID]))
                                next_req = req;
                            break;

                        case BatchSchedAlgo.MARKED_RANK_FCFS:
                            if (!helper.is_MARK_RANK_FCFS(next_req, req, proc_to_rank[next_req.request.requesterID], proc_to_rank[req.request.requesterID]))
                                next_req = req;
                            break;

                        case BatchSchedAlgo.MARKED_FR_FCFS:
                            if (!helper.is_MARK_FR_FCFS(next_req, req))
                                next_req = req;
                            break;

                        case BatchSchedAlgo.MARKED_FCFS:
                            if (!helper.is_MARK_FCFS(next_req, req))
                                next_req = req;
                            break;

                        default:
                            Debug.Assert(false);
                            break;

                    }//switch

                }//for over buffer

            }//for over banks

            //Debug.Assert(next_req == null);

            if (next_req == null)
                return null;

            if (is_batch == false)
                return next_req;

            if (next_req.isMarked)
                return next_req;

            //which round are we in (within a batch)?
            int cur_proc = rank_to_proc[cur_rank];
            int cur_ct = bhelper.completion_time[cur_proc];
            if ((MemCtlr.cycle - batch_begin_time) >= (ulong)cur_ct)
                cur_rank--;

            //test for completion time
            int test_ct = bhelper.completion_time[next_req.request.requesterID];

            if (test_ct > cur_ct && cur_rank == Config.N - 1)
                return null;

            return next_req;

            /*
            //which round are we in (within a batch)?
            int cur_proc = rank_to_proc[cur_rank];
            int cur_ct = bhelper.completion_time[cur_proc];
            if ((Mem.cycle - batch_begin_time) >= (ulong) cur_ct)
                cur_rank--;

            //test for completion time
            int test_ct = bhelper.completion_time[next_req.threadID];

            if (test_ct > cur_ct)
                return null;

            return next_req;
             */
        }

        public override void tick()
        {
            //progress time
            base.tick();

            //constant reranking
            if (Config.memory.constant_rerank)
                rerank();

            if (cur_marked_req != 0) {
                //current batch is not complete
                return;
            }
            else {
                is_batch = false;
                cur_rank = Config.N - 1;
            }

            //not enough requests to form a new batch;
            if (cur_load < 3)
                return;

            //stats
            bstat.inc_force(ref bstat.avg_batch_duration, (MemCtlr.cycle - batch_begin_time));
            batch_begin_time = MemCtlr.cycle;

            //form a new batch
            remark();
            is_batch = true;

            //rank processors (threads)
            recomp++;
            rerank();
        }
    }
}