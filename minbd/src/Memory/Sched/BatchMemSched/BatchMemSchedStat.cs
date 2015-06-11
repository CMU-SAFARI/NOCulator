using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    /**
     * Batch memory scheduler statistics
     */
    public class BatchMemSchedStat
    {
        //collect stats
        bool[] is_collect_stat;        //whether to collect statistics
        
        //owning scheduler
        public BatchMemSched sched;     //the scheduler to which this statistics belongs

        //serviced requests
        public ulong[] total_serviced;                  //total serviced memory requests
        public ulong[] total_serviced_marked;           //total serviced marked memory requests

        //rank recomputation
        public double[] avg_load_recomp;                //average total load per processor at each recomputation
        public double[] avg_max_load_recomp;            //average maximum load, at any single bank, per processor at each recomputation

        //marking (batch formulation)
        public double[] avg_marked_load_left_remark;    //average left-over marked load at the beginning of each mark
        public double[] avg_load_remark;                //average total load per processor at each remark
        public double[] avg_max_load_remark;            //average maximum load, at any single bank, per processor at each remark
        public double[] avg_max_marked_remark;          //average maximum marked load, at any single bank, per processor after each remark
        public double[] avg_marked_remark;              //average newly marked load per processor after each remark

        //batch duration
        public double avg_batch_duration;

        //finished batches 
        public ulong[] total_finished_batch_cnt;        //total finished batches, in which a processor had at least one marked request
        public ulong[] total_finished_batch_duration;   //total batch duration, for batches in which a processor had at least one marked request
      
        /**
         * Constructor
         */
        public BatchMemSchedStat(BatchMemSched sched)
        {
            //collect stats
            is_collect_stat = new bool[Config.N];
            for (int p = 0; p < Config.N; p++) {
                is_collect_stat[p] = true;
            }

            //owning scheduler
            this.sched = sched;

            //serviced requests
            total_serviced = new ulong[Config.N];
            total_serviced_marked = new ulong[Config.N];

            //rank recomputation
            avg_load_recomp = new double[Config.N];
            avg_max_load_recomp = new double[Config.N];

            //marking (batch formulation)
            avg_marked_load_left_remark = new double[Config.N];
            avg_load_remark = new double[Config.N];
            avg_max_load_remark = new double[Config.N];
            avg_max_marked_remark = new double[Config.N];
            avg_marked_remark = new double[Config.N];

            //finished batches
            total_finished_batch_cnt = new ulong[Config.N];
            total_finished_batch_duration = new ulong[Config.N];
        }

        /**
         * Let's say we want two processors to both execute for at least 1M instructions.
         * But a memory hogger may execute more than 1M instructions while the other executes 1M instructions.
         * For the memory hogger, we "freeze" its statistics when it reaches 1M instructions.
         * We do not want the statistics to be contaminated by the instructions beyond that point.
         */
        public void freeze_stat(int proc_id)
        {
            is_collect_stat[proc_id] = false;

            //rank recomputation
            avg_load_recomp[proc_id] /= (double)sched.recomp;
            avg_max_load_recomp[proc_id] /= (double)sched.recomp;

            //remark
            avg_marked_load_left_remark[proc_id] /= (double)sched.mark;
            avg_load_remark[proc_id] /= (double)sched.mark;
            avg_max_load_remark[proc_id] /= (double)sched.mark;
            avg_max_marked_remark[proc_id] /= (double)sched.mark;
            avg_marked_remark[proc_id] /= (double)sched.mark;
        }

        /**
         * (Overloaded) Decrement stat by one
         * @param stat the stat to decrement
         */
        public void dec(int proc_id, ref int stat)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat--;
        }

        /**
         * (Overloaded) Increment stat by one
         * @param stat the stat to increment
         */
        public void inc(int proc_id, ref int stat)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat++;
        }

        /**
         * (Overloaded) Increment stat by one
         * @param stat the stat to increment
         */
        public void inc(int proc_id, ref ulong stat)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat++;
        }

        /**
         * (Overloaded) Increment stat by one
         * @param stat the stat to increment
         */
        public void inc(int proc_id, ref double stat)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat++;
        }

        /**
         * (Overloaded) Increment stat by specific value
         * @param stat the stat to increment
         * @param delta how much to increment by
         */
        public void inc(int proc_id, ref double stat, int delta)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat += delta;
        }

        /**
         * (Overloaded) Increment stat by specific value
         * @param stat the stat to increment
         * @param delta how much to increment by
         */
        public void inc(int proc_id, ref ulong stat, ulong delta)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat += delta;
        }

        /**
         * (Overloaded) Increment stat by specific value
         * @param stat the stat to increment
         * @param delta how much to increment by
         */
        public void inc(int proc_id, ref double stat, ulong delta)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat += delta;
        }

        /**
         * (Overloaded) Increment stat by specific value
         * @param stat the stat to increment
         * @param delta how much to increment by
         */
        public void inc_force(ref double stat, ulong delta)
        {
            stat += delta;
        }

        /**
         * Set a stat by a specific value
         * @param stat the stat to set
         * @param val the value
         */
        public void set(int proc_id, ref ulong stat, ulong val)
        {
            if (!is_collect_stat[proc_id])
                return;
            stat = val;
        }

        /**
         * Report statistics
         */ 
        public void report(TextWriter writer, int proc_id)
        {
            writer.WriteLine("Batching-Proc Stats:");
            writer.WriteLine("   AvgTotPerRemark: " + avg_load_remark[proc_id]);
            writer.WriteLine("   AvgMaxPerRemark: " + avg_max_load_remark[proc_id]);
            writer.WriteLine("   AvgTotMarkedRequestsPerRemark: " + avg_marked_remark[proc_id]);
            writer.WriteLine("   AvgMaxMarkedRequestsPerRemark: " + avg_max_marked_remark[proc_id]);
            writer.WriteLine("   AvgTotPerRecomp: " + avg_load_recomp[proc_id]);
            writer.WriteLine("   AvgMaxPerRecomp: " + avg_max_load_recomp[proc_id]);
            writer.WriteLine("   AvgMarkedReq at Batch-End: " + avg_marked_load_left_remark[proc_id]);
            writer.WriteLine("   TotalNrOfActiveBatches: " + total_finished_batch_cnt[proc_id]);
            writer.WriteLine("   AvgBatchCompletionTime: " + (double)total_finished_batch_duration[proc_id] / (double)total_finished_batch_cnt[proc_id]);
            writer.WriteLine("   TotalNrOfRequestsServiced: " + total_serviced[proc_id]);
            writer.WriteLine("   TotalNrOfMarkedRequestsServiced: " + total_serviced_marked[proc_id]);
            writer.WriteLine("   PercentageOfMarkedRequests: " + 100 * (double)total_serviced_marked[proc_id] / (double)total_serviced[proc_id]);
        }

        /**
         * Report statistics
         */ 
        public void report_verbose(TextWriter writer)
        {
            writer.WriteLine("Batching-Statistics:");
            writer.WriteLine("   Number of Batches: " + sched.mark);
            writer.WriteLine("   AvgBatchDuration: " + (double)avg_batch_duration / (double)sched.mark);

            ulong final_finished_batch_cnt = 0;
            ulong final_finished_batch_duration = 0;
            ulong final_serviced = 0;
            ulong final_serviced_marked = 0;

            for (int p = 0; p < Config.N; p++) {
                final_finished_batch_cnt += total_finished_batch_cnt[p];
                final_finished_batch_duration += total_finished_batch_duration[p];
                final_serviced += total_serviced[p];
                final_serviced_marked += total_serviced_marked[p];
            }

            writer.WriteLine("   AvgCompletionTime: " + (double)final_finished_batch_duration / (double)final_finished_batch_cnt);
            writer.WriteLine("   PercentageOfMarkedRequestsServiced: " + 100 * (double)final_serviced_marked / (double)final_serviced);
        }

        /**
         * Report statistics
         */ 
        public void report_excel(TextWriter writer)
        {
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine(avg_load_remark[procID]);
            writer.WriteLine(); writer.WriteLine();
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine(avg_max_load_remark[procID]);
            writer.WriteLine(); writer.WriteLine();
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine(avg_marked_load_left_remark[procID]);
            writer.WriteLine();
            writer.WriteLine((double)avg_batch_duration / (double)sched.mark);
        }
    }//class
}//namespace