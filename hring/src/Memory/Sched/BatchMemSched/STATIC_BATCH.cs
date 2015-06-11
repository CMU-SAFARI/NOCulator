using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class STATIC_BATCH : BatchMemSched
    {
        public bool is_batch_active = true;

        public STATIC_BATCH(int buf_size, Bank[] bank,
            RankAlgo rank_algo, BatchSchedAlgo batch_sched_algo)
            : base(buf_size, bank, rank_algo, batch_sched_algo)
        {
            Console.WriteLine("Initialized STATIC_BATCH_MemoryScheduler");
        }

        public override void tick()
        {
            base.tick();

            if (((MemCtlr.cycle % Config.memory.mark_interval) == 0) && (MemCtlr.cycle != 0)) {
                if (is_batch_active)
                    bstat.inc_force(ref bstat.avg_batch_duration, Config.memory.mark_interval);

                is_batch_active = true;
                if (cur_marked_req != 0) {
                    cur_marked_req = 0;
                    //            Simulator.tavgNumReqPBPerProcRemark += Simulator.MarkingPeriod;
                }
                remark();
            }
            else if (is_batch_active && cur_marked_req == 0) {
                // here the batch would have been full!
                is_batch_active = false;
                bstat.inc_force(ref bstat.avg_batch_duration, (MemCtlr.cycle % Config.memory.mark_interval));
            }



            if (((MemCtlr.cycle % Config.memory.recomp_interval) == 0) && (MemCtlr.cycle != 0)) {
                recomp++;
                rerank();
            }
        }
    }//class
}//namespace