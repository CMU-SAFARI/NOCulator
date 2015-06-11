using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace ICSimulator
{
    /**
     * Bank statistics
     */
    public class BankStat
    {
        //collect stats
        private Boolean is_collect_stat;

        //owning processor
        public Bank bank;      //the bank to which this statistics belongs
        private int bank_id;

        //memory requests (for all banks)
        public static int[] req_cnt = new int[Config.N];                //current outstanding memory requests, by originating processor
        public static int[] marked_req_cnt = new int[Config.N];         //current outstanding marked memory requests, by originating processor
        public static int[] unmarked_req_cnt = new int[Config.N];       //current of outstanding unmarked memory requests, by originating processor

        //hit or miss
        public ulong row_hit;   //total row hits
        public ulong row_miss;  //total row misses (conflicts)

        //hit or miss per originating processor
        public ulong[] row_hit_per_proc;    //total row hits, by originating processor
        public ulong[] row_miss_per_proc;   //total row misses (conflicts), by originating processor

        /**
         * Constructor
         */
        public BankStat(Bank bank)
        {
            //collecting stats
            is_collect_stat = true;

            //owning processor
            this.bank = bank;
            bank_id = bank.bank_id;

            //hit or miss per originating processor
            row_hit_per_proc = new ulong[Config.N];
            row_miss_per_proc = new ulong[Config.N];
        }

        /**
         * (Overloaded) Decrement stat by one
         * @param stat the stat to decrement
         */
        public void dec(ref int stat)
        {
            if (!is_collect_stat)
                return;
            stat--;
        }

        /**
         * (Overloaded) Increment stat by one
         * @param stat the stat to increment
         */
        public void inc(ref int stat)
        {
            if (!is_collect_stat)
                return;
            stat++;
        }

        /**
         * (Overloaded) Increment stat by one
         * @param stat the stat to increment
         */
        public void inc(ref ulong stat)
        {
            if (!is_collect_stat)
                return;
            stat++;
        }

        /**
         * (Overloaded) Increment stat by specific value
         * @param stat the stat to increment
         * @param delta how much to increment by
         */
        public void inc(ref ulong stat, ulong delta)
        {
            if (!is_collect_stat)
                return;
            stat += delta;
        }

        /**
         * Set a stat by a specific value
         * @param stat the stat to set
         * @param val the value
         */
        public void set(ref ulong stat, ulong val)
        {
            if (!is_collect_stat)
                return;
            stat = val;
        }

        /**
         * Report bank statistics
         */
        public void report(TextWriter writer)
        {
            writer.WriteLine();
            writer.WriteLine(
                "Bank " + bank_id.ToString() + ":  " +
                "Hits: " + row_hit + "  " +
                "Conflicts: " + row_miss + "  " +
                "Hit-Ratio: {0:f}%", (double)row_hit * 100 / (row_hit + row_miss));

            for (int p = 0; p < Config.N; p++) {
                writer.WriteLine(
                    "    P" + p + ":  " +
                    "Hits: " + row_hit_per_proc[p] + "  " +
                    "Conflicts: " + row_miss_per_proc[p] + "  " +
                    "Hit-Ratio: {0:f}%", (double)row_hit_per_proc[p] * 100 / (row_hit_per_proc[p] + row_miss_per_proc[p]));
            }
        }
    }//class
}//namespace