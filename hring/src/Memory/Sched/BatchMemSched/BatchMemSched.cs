using System;
using System.Collections.Generic;
using System.Text;
using lpsolve55;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    /**
     * Base class for all batch schedulers
     */
    public abstract class BatchMemSched : AbstractMemSched
    {
        /****************************************************************
        *  ------ Statistics and reporting -----
        ***************************************************************/
        public BatchMemSchedStat bstat;//the statistics for the batch memory scheduler
        public ulong mark;              //total markings (batch formulations)
        public ulong recomp;            //total rank recomputations

        /****************************************************************
         *  ------ Components -----
         ***************************************************************/
        public RankAlgo rank_algo;                  //ranking algorithm
        public BatchSchedAlgo batch_sched_algo;     //batch scheduling algorithm

        public int[] rank_to_proc;                  //rank to processor id mapping (higher rank value is greater precedence)
        public int[] proc_to_rank;                  //processor id to rank mapping (higher rank value is greater precedence)

        public ulong batch_start_time;              //batch start (formulation) time

        /****************************************************************
         *  ------ Scheduler status -----
         ***************************************************************/
        //batch formulation; marking
        MemoryRequest[][][] markable;     //requests that should be newly marked; [processor][bank][request]
        int[][] markable_cnt;   //number of requests that should be newly marked; [processor][bank]
        int[][] markable_cnt_unbound;

        static int markable_len = 2 * Config.memory.batch_cap;

        //current marked requests
        public int cur_marked_req;                      //number of currently remaining marked requests across all processors

        //current marked requests per processor
        public int[] cur_marked_per_proc;               //number of currently remaining marked requests per processor
        public int[] cur_period_marked_per_proc;        //number of requests per processor marked in the current batch formation

        //current marked requests per processor, bank
        public int[,] cur_marked_per_procbank;          //number of currently remaining marked requests per processor for each bank [proc, bank] 
        public int[,] cur_period_marked_per_procbank;   //number of requests per processor for each bank [proc, bank] marked in the current batch formation

        /****************************************************************
         *  ------ Scheduling algorithm helper -----
         ***************************************************************/
        public BatchMemSchedHelper bhelper;         //ranking algorithm helper

        /**
         * Constructor
         */
        public BatchMemSched(int buf_size, Bank[] bank, RankAlgo rank_algo, BatchSchedAlgo batch_sched_algo)
            : base(buf_size, bank)
        {
            //stat
            bstat = new BatchMemSchedStat(this);

            //components
            this.rank_algo = rank_algo;
            this.batch_sched_algo = batch_sched_algo;

            rank_to_proc = new int[Config.N];
            proc_to_rank = new int[Config.N];
            for (int p = 0; p < Config.N; p++) {
                rank_to_proc[p] = p;
                proc_to_rank[p] = Config.N - 1;
            }

            //batch formulation; marking
            markable = new MemoryRequest[Config.N][][];
            for (int p = 0; p < Config.N; p++) {
                markable[p] = new MemoryRequest[bank_max][];
                for (int b = 0; b < bank_max; b++) {
                    markable[p][b] = new MemoryRequest[markable_len];
                }
            }

            markable_cnt = new int[Config.N][];
            for (int p = 0; p < Config.N; p++) {
                markable_cnt[p] = new int[bank_max];
            }

            markable_cnt_unbound = new int[Config.N][];
            for (int p = 0; p < Config.N; p++) {
                markable_cnt_unbound[p] = new int[bank_max];
            }

            //marked requests per processor
            cur_marked_per_proc = new int[Config.N];
            cur_period_marked_per_proc = new int[Config.N];

            //marked requests per processor, bank
            cur_marked_per_procbank = new int[Config.N, bank_max];
            cur_period_marked_per_procbank = new int[Config.N, bank_max];

            //bhelper
            bhelper = new BatchMemSchedHelper(this);

            Console.WriteLine("Initialized BATCH_MemoryScheduler");
            Console.WriteLine("Ranking Scheme: " + rank_algo.ToString());
            Console.WriteLine("WithinBatch Priority: " + batch_sched_algo.ToString());
            Console.WriteLine("BatchingCap: " + Config.memory.batch_cap);
        }

        private void req_insert(MemoryRequest[] markable, MemoryRequest req)
        {
            int insert = -1;

            for (int i = 0; i < markable.Length; i++) {

                MemoryRequest cur = markable[i];

                if (cur == null) {
                    insert = i;
                    continue;
                }

                if (cur.timeOfArrival <= req.timeOfArrival)
                    break;

                insert = i;
            }

            if (insert == -1)
                return;

            if (markable[insert] != null) {
                //shift younger requests to left
                for (int i = 0; i < insert; i++) {
                    markable[i] = markable[i + 1];
                }
            }
            markable[insert] = req;
        }

        private void load_balance0(){

            //batch balancing
            int[] batch_per_bank = new int[bank_max];
            int max_batch = 0;
            int min_batch = Int32.MaxValue;

            int max_bank = -1;
            int min_bank = -1;

            for (int b = 0; b < bank_max; b++) {
                for (int p = 0; p < Config.N; p++) {
                    batch_per_bank[b] += markable_cnt[p][b];
                }

                if (batch_per_bank[b] > max_batch) {
                    max_batch = batch_per_bank[b];
                    max_bank = b;
                }

                if (batch_per_bank[b] < min_batch) {
                    min_batch = batch_per_bank[b];
                    min_bank = b;
                }
            }

            Debug.Assert(max_bank != -1 && min_bank != -1);

            for (int b = 0; b < bank_max; b++) {

                if (b == max_bank)
                    continue;

                int cur_batch = batch_per_bank[b];

                int hog_size = 0;
                int hog_p = -1;
                for (int p = 0; p < Config.N; p++) {

                    if (markable_cnt_unbound[p][b] <= Config.memory.batch_cap)
                        continue;

                    if (markable_cnt_unbound[p][b] > hog_size) {
                        hog_size = markable_cnt_unbound[p][b];
                        hog_p = p;
                    }
                }

                if (hog_p != -1) {
                    int batch_bal = Math.Min(max_batch - cur_batch, hog_size - Config.memory.batch_cap);
                    markable_cnt[hog_p][b] += batch_bal;
                }
            }

        }

        protected virtual void remark()
        {
            mark++;
            batch_start_time = MemCtlr.cycle;

            //reset marking
            for (int p = 0; p < Config.N; p++) {
                for (int b = 0; b < bank_max; b++) {
                    Array.Clear(markable[p][b], 0, markable_len);
                }
            }

            for (int p = 0; p < Config.N; p++) {
                Array.Clear(markable_cnt_unbound[p], 0, bank_max);
                Array.Clear(markable_cnt[p], 0, bank_max);
            }

            cur_marked_req = 0;

            Array.Clear(cur_marked_per_proc, 0, Config.N);
            Array.Clear(cur_period_marked_per_proc, 0, Config.N);

            Array.Clear(cur_marked_per_procbank, 0, Config.N * bank_max);
            Array.Clear(cur_period_marked_per_procbank, 0, Config.N * bank_max);

            //search for markable requests (at most N oldest requests for each (processor, bank) pair)
            for (int b = 0; b < bank_max; b++) {
                for (int j = 0; j < buf_load[b]; j++) {

                    MemoryRequest req = buf[b, j];
                    int p = req.request.requesterID;

                    //check for any left-over marked requests
                    if (req.isMarked == true) {
                        //no request should be left marked for full batch scheduler
                        if (this is FULL_BATCH)
                            Debug.Assert(false);

                        bstat.inc(p, ref bstat.avg_marked_load_left_remark[p]);
                    }

                    if (markable_cnt_unbound[p][b] < markable_len) {

                        markable_cnt_unbound[p][b]++;

                        if (markable_cnt[p][b] < Config.memory.batch_cap)
                            markable_cnt[p][b]++;
                    }

                    req_insert(markable[p][b], req);
                }
            }

            //load balance0
            if (Config.memory.load_balance0)
                load_balance0();

            //mark
            for (int p = 0; p < Config.N; p++) {
                for (int b = 0; b < bank_max; b++) {
                    for (int j = 0; j < markable_cnt[p][b]; j++) {
                        markable[p][b][markable_len - 1 - j].isMarked = true;
                    }
                }
            }

            //status
            int sum_over_proc = 0;
            for (int p = 0; p < Config.N; p++) {

                int sum_over_bank = 0;
                for (int b = 0; b < bank_max; b++) {
                    int cnt = markable_cnt[p][b];

                    cur_marked_per_procbank[p, b] = cnt;
                    cur_period_marked_per_procbank[p, b] = cnt;

                    sum_over_bank += cnt;
                }

                cur_marked_per_proc[p] = sum_over_bank;
                cur_period_marked_per_proc[p] = sum_over_bank;

                sum_over_proc += sum_over_bank;
            }
            cur_marked_req = sum_over_proc;

            //----- STATS START -----
            //for the request that the bank is currently chewing on, update its marking status; for stat keeping 
            for (int b = 0; b < bank_max; b++)
                bank[b].update_marking();

            for (int p = 0; p < Config.N; p++) {
                bstat.inc(p, ref bstat.avg_marked_remark[p], cur_period_marked_per_proc[p]);

                bstat.inc(p, ref bstat.avg_load_remark[p], cur_load_per_proc[p]);
                bstat.inc(p, ref bstat.avg_max_load_remark[p], cur_max_load_per_proc[p]);
                bstat.inc(p, ref bstat.avg_max_marked_remark[p], Math.Min(cur_max_load_per_proc[p], Config.memory.batch_cap));
            }
            //----- STATS END -----
        }

        /**
         * Rank the processors
         */
        protected virtual void rerank()
        {
            //----- STATS START -----
            for (int p = 0; p < Config.N; p++) {
                bstat.inc(p, ref bstat.avg_load_recomp[p], (ulong)cur_load_per_proc[p]);
                bstat.inc(p, ref bstat.avg_max_load_recomp[p], (ulong)cur_max_load_per_proc[p]);
            }
            //----- STATS END -----

            //ranking algorithm specific calculation
            switch (rank_algo) {
                case RankAlgo.MAX_TOT_REVERSE_RANKING:
                    bhelper.MAX_TOT_REVERSE();
                    break;
                case RankAlgo.MAX_TOT_RANKING:
                    bhelper.MAX_TOT();
                    break;
                case RankAlgo.ROW_BUFFER_AWARE_MAX_RANKING:
                    bhelper.RBA_MAX();
                    break;
                case RankAlgo.TOT_MAX_RANKING:
                    bhelper.TOT_MAX();
                    break;
                case RankAlgo.ROUND_ROBIN_RANKING:
                    bhelper.RR();
                    break;
                case RankAlgo.RANDOM_RANKING:
                    bhelper.RANDOM();
                    break;
                case RankAlgo.PERBANK_LP_RANKING:
                    bhelper.LP();
                    break;
                case RankAlgo.ECT_RANKING:
                    bhelper.ECT3();
                    break;
                case RankAlgo.ECT_RV_RANKING:
                    bhelper.ECT_RV();
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        /**
         * Remove a memory request from the buffer (that holds requests from each processor to banks)
         * 
         * @param req the memory request to remove
         */
        public override void remove_req(MemoryRequest req)
        {
            int threadID = req.request.requesterID;

            //stats
            bstat.inc(threadID, ref bstat.total_serviced[threadID]);

            if (req.isMarked) {

                cur_marked_req--;
                cur_marked_per_proc[threadID]--;
                cur_marked_per_procbank[threadID, req.glob_b_index]--;

                //----- STATS START -----
                bstat.inc(threadID, ref bstat.total_serviced_marked[threadID]);

                if (cur_marked_req == 0)
                    Config.memory.tavgNumReqPBPerProcRemark += MemCtlr.cycle % Config.memory.mark_interval;

                if (cur_marked_per_proc[threadID] == 0 && cur_period_marked_per_proc[threadID] > 0) {
                    bstat.inc(threadID, ref bstat.total_finished_batch_duration[threadID], MemCtlr.cycle - batch_start_time);

                    bstat.inc(threadID, ref bstat.total_finished_batch_cnt[threadID]);
                }
                //----- STATS END -----
            }

            base.remove_req(req);
        }

        /**
         * 
         */
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
            return next_req;
        }

        public override void freeze_stat(int threadID)
        {
            base.freeze_stat(threadID);
            bstat.freeze_stat(threadID);
        }

        public override void report(TextWriter writer, int threadID)
        {
            base.report(writer, threadID);
            bstat.report(writer, threadID);
        }

        public override void report(TextWriter writer)
        {
            bstat.report_verbose(writer);
            base.report(writer);
        }

        public override void report_excel(TextWriter writer)
        {
            base.report_excel(writer);
            bstat.report_excel(writer);
        }
    }//class
}//namespace