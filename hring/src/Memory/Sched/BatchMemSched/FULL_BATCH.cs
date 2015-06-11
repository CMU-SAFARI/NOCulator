using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class FULL_BATCH : BatchMemSched
    {
        protected ulong batch_begin_time;

        public FULL_BATCH(int total_size, Bank[] bank, RankAlgo rank_algo, BatchSchedAlgo batch_sched_algo)
            : base(total_size, bank, rank_algo, batch_sched_algo)
        {
            Console.WriteLine("Initialized FULL_BATCH_MemoryScheduler");
        }

        public override void tick()
        {
            //progress time
            base.tick();

            //constant reranking
            if (Config.memory.constant_rerank)
                rerank();

            //current batch is not complete or not enough requests to form a new batch; can't do anything
            if (cur_marked_req != 0 || cur_load < 3)
                return;

            //stats
            bstat.inc_force(ref bstat.avg_batch_duration, (MemCtlr.cycle - batch_begin_time));
            batch_begin_time = MemCtlr.cycle;

            //form a new batch
            remark();

            //rank processors (threads)
            recomp++;
            rerank();
        }
    }
}