using System;
using System.Collections.Generic;
using System.Text;
using lpsolve55;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    public class MemSchedStat
    {
        //collect stats
        bool[] is_collect_stat;//whether to collect statistics
        
        //components
        Bank[] bank;
        int bank_max;           //total number of banks for this memory
        //int proc_max;           //total number of processors
    
        //stats
        public ulong[] tick_load_per_proc;          //sum of load at each cycle, by originating processor
        protected double[] time_avg_load_per_proc;  //time average of load, by originating processor

        public ulong[] tick_req;            //number of cycles where there are outstanding requests, by originating processor
        public ulong[] tick_req_cnt;        //sum of outstanding requests at each cycle, by originating processor

        public ulong[] tick_marked;         //number of cycles where there are outstanding marked requests, by originating processor
        public ulong[] tick_marked_req;     //sum of outstanding marked requests at each cycle, by originating processor

        public ulong[] tick_unmarked;       //number of cycles where there are outstanding unmarked requests, by originating processor
        public ulong[] tick_unmarked_req;   //sum of outstanding unmarked requests at each cycle, by originating processor

        public ulong[,] total_req_per_procbank; //total number of requests per processor for each bank [proc, bank]

        public MemSchedStat(int proc_max, Bank[] bank, int bank_max)
        {
            //collect stats
            is_collect_stat = new bool[Config.N];
            for (int p = 0; p < Config.N; p++) {
                is_collect_stat[p] = true;
            }

            //components
            this.bank = bank;
            this.bank_max = bank_max;
            //this.proc_max = proc_max;

            //other stats
            tick_load_per_proc = new ulong[proc_max];
            time_avg_load_per_proc = new double[proc_max];

            tick_req = new ulong[proc_max];
            tick_req_cnt = new ulong[proc_max];

            tick_marked = new ulong[proc_max];
            tick_marked_req = new ulong[proc_max];

            tick_unmarked = new ulong[proc_max];
            tick_unmarked_req = new ulong[proc_max];

            total_req_per_procbank = new ulong[proc_max, bank_max];
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
        
        public void freeze_stat(int proc_id)
        {
            is_collect_stat[proc_id] = false;

            time_avg_load_per_proc[proc_id] = (double)((double)tick_load_per_proc[proc_id] / (double)(MemCtlr.cycle));
        }
        

        /**
 * Collects statistics from thread procID
 */
        public virtual void report(TextWriter writer, int proc_id)
        {
            writer.WriteLine("   AvgRequestsPerProcCycle: " + time_avg_load_per_proc[proc_id]);
            writer.WriteLine("   Parallelism-Factor: " + (double)((double)tick_req_cnt[proc_id] / (double)(tick_req[proc_id])));
            writer.WriteLine("   MarkedParallelism-Factor: " + (double)((double)tick_marked_req[proc_id] / (double)(tick_marked[proc_id])));
            writer.WriteLine("   UnmarkedParallelism-Factor: " + (double)((double)tick_unmarked_req[proc_id] / (double)(tick_unmarked[proc_id])));
            writer.WriteLine("   par: " + (double)tick_req_cnt[proc_id] + " bb: " + (double)(tick_req[proc_id]));
            writer.WriteLine("   par: " + (double)tick_marked_req[proc_id] + " bb: " + (double)(tick_marked[proc_id]));
            writer.WriteLine("   par: " + (double)tick_unmarked_req[proc_id] + " bb: " + (double)(tick_unmarked[proc_id]));

            writer.WriteLine("   MemRequests Per Bank: ");
            for (int b = 0; b < bank_max; b++)
                writer.WriteLine("       Bank:- " + b + " Requests:- " + total_req_per_procbank[proc_id, b]);
        }

        public virtual void report_verbose(TextWriter writer, int proc_id, string trace_name)
        {
            writer.WriteLine(trace_name + "\tAvgRequestsPerProcCycle\t{0:0.0000}", (double)time_avg_load_per_proc[proc_id]);
            writer.WriteLine(trace_name + "\tParallelismFactor\t{0:0.0000}", (double)((double)tick_req_cnt[proc_id] / (double)(tick_req[proc_id])));
            writer.WriteLine(trace_name + "\tMarkedParallelismFactor\t{0:0.0000}", (double)((double)tick_marked_req[proc_id] / (double)(tick_marked[proc_id])));
            writer.WriteLine(trace_name + "\tUnmarkedParallelismFactor\t{0:0.0000}", (double)((double)tick_unmarked_req[proc_id] / (double)(tick_unmarked[proc_id])));
            for (int b = 0; b < bank_max; b++)
                writer.WriteLine(trace_name + "\tBank-" + b + "Requests\t{0:0.0000}", (double)total_req_per_procbank[proc_id, b]);

            ulong totalBankHits = 0;
            ulong totalBankConflicts = 0;
            double[] bankAccessRates = new double[bank_max];

            for (int b = 0; b < bank_max; b++) {
                totalBankHits += bank[b].stat.row_hit_per_proc[proc_id];
                totalBankConflicts += bank[b].stat.row_miss_per_proc[proc_id];
            }

            for (int i = 0; i < bank_max; i++) {
                bankAccessRates[i] = (double)(100 * (double)(bank[i].stat.row_hit_per_proc[proc_id] + bank[i].stat.row_miss_per_proc[proc_id]) / (double)(totalBankHits + totalBankConflicts));
            }

            writer.WriteLine(trace_name + "\tRBHitRate\t{0:0.0000}", (double)((double)totalBankHits * 100 / (double)(totalBankHits + totalBankConflicts)));
            //writer.WriteLine(trace_name + "\tBankVariance:\t{0:0.0000}", (double)Config.memory.stat.get_var(bankAccessRates));
            //writer.WriteLine(trace_name + "\tBankSTDev\t{0:0.0000}", (double)Config.memory.stat.get_std(bankAccessRates));
        }

        public virtual void report_excel(TextWriter writer)
        {
            ulong[] totalBankHits = new ulong[Config.N];
            ulong[] totalBankConflicts = new ulong[Config.N];
            for (int p = 0; p < Config.N; p++) {
                for (int i = 0; i < bank.Length; i++) {
                    totalBankHits[p] += bank[i].stat.row_hit_per_proc[p];
                    totalBankConflicts[p] += bank[i].stat.row_miss_per_proc[p];
                }
            }


            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine(time_avg_load_per_proc[procID]);
            writer.WriteLine(); writer.WriteLine();
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine((double)((double)tick_req_cnt[procID] / (double)(tick_req[procID])));
            writer.WriteLine(); writer.WriteLine();
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine((double)((double)totalBankHits[procID] * 100 / (double)(totalBankHits[procID] + totalBankConflicts[procID])));
            writer.WriteLine(); writer.WriteLine();
        }

        /**
        * Write the output for Memory Schedulers here!
        */
        public virtual void report(TextWriter writer)
        {

            writer.WriteLine();
            writer.WriteLine("Memory Buffer Statistics");
            for (int i = 0; i < Config.N; i++) {
                writer.WriteLine("Processor " + i + " --> AvgRequestsPerProcCycle: " + time_avg_load_per_proc[i]);
            }

            ulong[] totalBankHits = new ulong[Config.N];
            ulong[] totalBankConflicts = new ulong[Config.N];
            double[] bankAccessRates = new double[bank_max];
            for (int p = 0; p < Config.N; p++) {
                for (int i = 0; i < bank.Length; i++) {
                    totalBankHits[p] += bank[i].stat.row_hit_per_proc[p];
                    totalBankConflicts[p] += bank[i].stat.row_miss_per_proc[p];
                }
                for (int i = 0; i < bank.Length; i++) {
                    bankAccessRates[i] = (double)(100 * (double)(bank[i].stat.row_hit_per_proc[p] + bank[i].stat.row_miss_per_proc[p]) / (double)(totalBankHits[p] + totalBankConflicts[p]));
                }
                writer.WriteLine("Processor " + p + " RB hit rate %: " + (double)((double)totalBankHits[p] * 100 / (double)(totalBankHits[p] + totalBankConflicts[p])));
                //writer.WriteLine("            Bank variance: " + Config.memory.stat.get_var(bankAccessRates));
                //writer.WriteLine("            Bank STDev: " + Config.memory.stat.get_std(bankAccessRates));
            }
            for (int i = 0; i < bank.Length; i++) {
                bank[i].stat.report(writer);
            }


        }
    }
}
