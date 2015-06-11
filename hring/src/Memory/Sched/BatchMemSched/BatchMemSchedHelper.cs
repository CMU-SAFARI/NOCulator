using System;
using System.Collections.Generic;
using System.Text;
using lpsolve55;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    /**
     * Ranking algorithm helper for batch memory scheduler
     */
    public class BatchMemSchedHelper
    {
        /****************************************************************
         *  ------ Parameters for Row-Buffer-Aware scheduling -----
         ***************************************************************/
        int[] RBA_max;
        int[] RBA_total;
        int[] cur_bankproc;
        int[] block_req_per_bank;

        /****************************************************************
         *  ------ Pertaining to Class AbstractMemSched -----
         ***************************************************************/
        int bank_max;

        Bank[] bank;
        MemoryRequest[,] buf;
        int[] buf_load;

        int[] cur_load_per_proc;
        int[,] cur_load_per_procbank;

        int[] cur_max_load_per_proc;

        /****************************************************************
         *  ------ Pertaining to Class BatchMemSched -----
         ***************************************************************/
        int[] rank_to_proc;
        int[] proc_to_rank;

        int[] cur_marked_per_proc;
        //int[] cur_period_marked_per_proc;

        int[,] cur_marked_per_procbank;
        //int[,] cur_period_marked_per_procbank;

        /****************************************************************
         *  ------ Misc. -----
         ***************************************************************/
        int[,] virtual_marked_per_procbank;
        public int[] completion_time;

        /**
         * Constructor
         */
        public BatchMemSchedHelper(BatchMemSched sched)
        {
            //pertaining to AbstractMemSched
            bank_max = sched.bank_max;

            bank = sched.bank;
            buf = sched.buf;
            buf_load = sched.buf_load;

            cur_load_per_proc = sched.cur_load_per_proc;
            cur_load_per_procbank = sched.cur_load_per_procbank;

            cur_max_load_per_proc = sched.cur_max_load_per_proc;

            //pertaining to BatchMemSched
            rank_to_proc = sched.rank_to_proc;
            proc_to_rank = sched.proc_to_rank;

            cur_marked_per_proc = sched.cur_marked_per_proc;
            //cur_period_marked_per_proc = sched.cur_period_marked_per_proc;

            cur_marked_per_procbank = sched.cur_marked_per_procbank;
            //cur_period_marked_per_procbank = sched.cur_period_marked_per_procbank;

            RBA_max = new int[Config.N];
            RBA_total = new int[Config.N];
            cur_bankproc = new int[bank_max];
            block_req_per_bank = new int[bank_max];

            //misc.
            virtual_marked_per_procbank = new int[Config.N, bank_max];
            completion_time = new int[Config.N];
        }

        private void fill_array(out double[] array, double[] val)
        {
            array = new double[val.Length + 1];
            for (int i = 0; i < val.Length; i++)
                array[i + 1] = val[i];
        }

        public void ECT_RV()
        {
            for (int p = 0; p < Config.N; p++)
                for (int b = 0; b < bank_max; b++)
                    virtual_marked_per_procbank[p, b] = cur_marked_per_procbank[p, b];

            int cur_rank = Config.N - 1;
            bool[] ranked = new bool[Config.N];

            int virtual_marked_sum = 0;

            //rank all processors
            for (int ranked_cnt = 0; ranked_cnt < Config.N; ranked_cnt++) {

                int global_min = Int32.MaxValue;
                int cur_proc = -1;

                //find the next processor to rank
                for (int p = 0; p < Config.N; p++) {

                    if (ranked[p])
                        continue;

                    //find max-marked for this processor
                    int local_max = -1;
                    for (int b = 0; b < bank_max; b++) {
                        if (virtual_marked_per_procbank[p, b] > local_max) {
                            local_max = virtual_marked_per_procbank[p, b];
                        }
                    }

                    if (local_max < global_min) {
                        global_min = local_max;
                        cur_proc = p;
                        continue;
                    }

                    //tie breaker; compare total-marked
                    if (local_max == global_min) {
                        if (cur_marked_per_proc[p] >= cur_marked_per_proc[cur_proc])
                            continue;

                        cur_proc = p;
                    }
                }

                //rank the found processor
                Debug.Assert(cur_proc != -1 && global_min != Int32.MaxValue);
                ranked[cur_proc] = true;
                rank_to_proc[cur_rank] = cur_proc;
                proc_to_rank[cur_proc] = cur_rank;
                cur_rank--;

                //completion time for the processor
                virtual_marked_sum += global_min;
                completion_time[cur_proc] = virtual_marked_sum * (Config.memory.row_hit_latency + Config.memory.bus_busy_time);

                //update virtual marked
                for (int b = 0; b < bank_max; b++) {

                    int virtual_marked = cur_marked_per_procbank[cur_proc, b];

                    for (int p = 0; p < Config.N; p++) {

                        if (ranked[p])
                            continue;

                        if (virtual_marked_per_procbank[p, b] == 0)
                            continue;

                        virtual_marked_per_procbank[p, b] += virtual_marked;
                    }
                }

            }//for: rank all processors

            //rank all processors with no outstanding requests
            for (int p = 0; p < Config.N; p++) {

                if (ranked[p])
                    continue;

                //rank
                ranked[p] = true;
                rank_to_proc[cur_rank] = p;
                proc_to_rank[p] = cur_rank;
                cur_rank--;

                //completion time
                completion_time[p] = 0;
            }
        }

        public void ECT3()
        {
            for (int p = 0; p < Config.N; p++)
                for (int b = 0; b < bank_max; b++)
                    virtual_marked_per_procbank[p, b] = cur_marked_per_procbank[p, b];

            int cur_rank = Config.N - 1;
            bool[] ranked = new bool[Config.N];

            //rank all processors
            for (int ranked_cnt = 0; ranked_cnt < Config.N; ranked_cnt++) {

                int global_min = Int32.MaxValue;
                int cur_proc = -1;

                //find the next processor to rank
                for (int p = 0; p < Config.N; p++) {

                    if (ranked[p])
                        continue;

                    //find max-marked for this processor
                    int local_max = -1;
                    for (int b = 0; b < bank_max; b++) {
                        if (virtual_marked_per_procbank[p, b] > local_max) {
                            local_max = virtual_marked_per_procbank[p, b];
                        }
                    }

                    if (local_max < global_min) {
                        global_min = local_max;
                        cur_proc = p;
                        continue;
                    }

                    //tie breaker; compare total-marked
                    if (local_max == global_min) {
                        if (cur_marked_per_proc[p] >= cur_marked_per_proc[cur_proc])
                            continue;

                        cur_proc = p;
                    }
                }

                //rank the found processor
                Debug.Assert(cur_proc != -1 && global_min != Int32.MaxValue);
                ranked[cur_proc] = true;
                proc_to_rank[cur_proc] = cur_rank--;

                //update virtual marked
                for (int b = 0; b < bank_max; b++) {

                    int virtual_marked = cur_marked_per_procbank[cur_proc, b];

                    for (int p = 0; p < Config.N; p++) {

                        if (ranked[p])
                            continue;

                        virtual_marked_per_procbank[p, b] += virtual_marked;
                    }
                }

            }//for: rank all processors
        }

        public void ECT()
        {
            for (int p = 0; p < Config.N; p++)
                for (int b = 0; b < bank_max; b++)
                    virtual_marked_per_procbank[p, b] = cur_marked_per_procbank[p, b];

            int cur_rank = Config.N - 1;
            bool[] ranked = new bool[Config.N];

            //rank all processors
            for (int ranked_cnt = 0; ranked_cnt < Config.N; ranked_cnt++) {

                int global_min = Int32.MaxValue;
                int cur_proc = -1;

                //find the next processor to rank
                for (int p = 0; p < Config.N; p++) {

                    if (ranked[p])
                        continue;

                    //find max_for this processor
                    int local_max = -1;
                    for (int b = 0; b < bank_max; b++) {
                        if (virtual_marked_per_procbank[p, b] > local_max) {
                            local_max = virtual_marked_per_procbank[p, b];
                        }
                    }

                    if (local_max < global_min) {
                        global_min = local_max;
                        cur_proc = p;
                    }
                }

                //rank the found processor
                Debug.Assert(cur_proc != -1 && global_min != Int32.MaxValue);
                ranked[cur_proc] = true;
                proc_to_rank[cur_proc] = cur_rank--;

                //update virtual marked
                for (int b = 0; b < bank_max; b++) {

                    int virtual_marked = cur_marked_per_procbank[cur_proc, b];

                    for (int p = 0; p < Config.N; p++) {

                        if (ranked[p])
                            continue;
                        
                        virtual_marked_per_procbank[p, b] += virtual_marked;
                    }
                }

            }//for: rank all processors
        }

        public void MAX_TOT()
        {
            for (int i = 0; i < Config.N - 1; i++) {
                for (int j = i + 1; j < Config.N; j++) {

                    //maintain ranks
                    if (cur_max_load_per_proc[rank_to_proc[i]] < cur_max_load_per_proc[rank_to_proc[j]]) {
                        continue;
                    }

                    int temp;

                    //switch ranks
                    if (cur_max_load_per_proc[rank_to_proc[i]] > cur_max_load_per_proc[rank_to_proc[j]]) {
                        temp = rank_to_proc[i];
                        rank_to_proc[i] = rank_to_proc[j];
                        rank_to_proc[j] = temp;
                        continue;
                    }

                    //tie-breaker
                    if (cur_load_per_proc[rank_to_proc[i]] > cur_load_per_proc[rank_to_proc[j]]) {
                        temp = rank_to_proc[i];
                        rank_to_proc[i] = rank_to_proc[j];
                        rank_to_proc[j] = temp;
                    }
                }
            }

            proc_to_rank[rank_to_proc[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++) {
                if ((cur_max_load_per_proc[rank_to_proc[i]] == cur_max_load_per_proc[rank_to_proc[i - 1]]) && (cur_load_per_proc[rank_to_proc[i]] == cur_load_per_proc[rank_to_proc[i - 1]])) {
                    proc_to_rank[rank_to_proc[i]] = proc_to_rank[rank_to_proc[i - 1]];
                }
                else {
                    proc_to_rank[rank_to_proc[i]] = proc_to_rank[rank_to_proc[i - 1]] - 1;
                }
            }
        }
        public void MAX_TOT_REVERSE()
        {
            MAX_TOT();
            for (int i = 0; i < Config.N; i++)
            {
                proc_to_rank[i]= Config.N- proc_to_rank[i]-1;
            }
            for (int i = 0; i < Config.N; i++)
            {
                rank_to_proc[proc_to_rank[i]] = i;
            }
        }
        public void TOT_MAX()
        {
            int temp;
            for (int i = 0; i < Config.N - 1; i++) {
                for (int j = i + 1; j < Config.N; j++) {
                    if (cur_load_per_proc[rank_to_proc[i]] > cur_load_per_proc[rank_to_proc[j]]) {
                        temp = rank_to_proc[i];
                        rank_to_proc[i] = rank_to_proc[j];
                        rank_to_proc[j] = temp;
                    }
                    else
                        if (cur_load_per_proc[rank_to_proc[i]] < cur_load_per_proc[rank_to_proc[j]]) { }
                        else {
                            if (cur_max_load_per_proc[rank_to_proc[i]] > cur_max_load_per_proc[rank_to_proc[j]]) {
                                temp = rank_to_proc[i];
                                rank_to_proc[i] = rank_to_proc[j];
                                rank_to_proc[j] = temp;
                            }
                        }
                }
            }
            proc_to_rank[rank_to_proc[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++) {
                if ((cur_max_load_per_proc[rank_to_proc[i]] == cur_max_load_per_proc[rank_to_proc[i - 1]]) && (cur_load_per_proc[rank_to_proc[i]] == cur_load_per_proc[rank_to_proc[i - 1]])) {
                    proc_to_rank[rank_to_proc[i]] = proc_to_rank[rank_to_proc[i - 1]];
                }
                else {
                    proc_to_rank[rank_to_proc[i]] = proc_to_rank[rank_to_proc[i - 1]] - 1;
                }
            }
        }

        public void RANDOM()
        {
            // assign random values
            int temp;
            for (int i = 0; i < Config.N - 1; i++) {
                rank_to_proc[i] = Simulator.rand.Next(int.MaxValue);
                proc_to_rank[i] = i;
            }
            // sort
            for (int i = 0; i < Config.N - 1; i++) {
                for (int j = i + 1; j < Config.N; j++) {
                    if (rank_to_proc[i] > rank_to_proc[j]) {
                        temp = rank_to_proc[i];
                        rank_to_proc[i] = rank_to_proc[j];
                        rank_to_proc[j] = temp;
                        temp = proc_to_rank[i];
                        proc_to_rank[i] = proc_to_rank[j];
                        proc_to_rank[j] = temp;
                    }
                }
            }
        }

        public void RR()
        {
            int temp = rank_to_proc[0];
            for (int i = 0; i < Config.N - 1; i++) {
                rank_to_proc[i] = rank_to_proc[i + 1];
            }
            rank_to_proc[Config.N - 1] = temp;
            for (int i = 0; i < Config.N - 1; i++) {
                proc_to_rank[rank_to_proc[i]] = i;
            }
        }

        // This ranking takes into account the currently opened row and ranks different
        // threads according to the earliest time they would be finished if all currently 
        // marked requests in the buffer would be executed according to fr-rank and the
        // thread had first rank. (shortest-job first)
        public void RBA_MAX()
        {
            // find the number of outstanding, marked requests that go to the open row per bank 
            int marked_req_to_opened_row;
            for (int b = 0; b < bank_max; b++) {
                ulong opened_row = bank[b].get_cur_row();

                marked_req_to_opened_row = 0;

                cur_bankproc[b] = -1;
                for (int row = 0; row < buf_load[b]; row++) {
                    if (buf[b, row].isMarked && buf[b, row].r_index == opened_row) {
                        cur_bankproc[b] = buf[b, row].request.requesterID;
                        marked_req_to_opened_row++;
                    }
                }
                block_req_per_bank[b] = marked_req_to_opened_row;
            }

            // now compute the max of each thread. 
            for (int p = 0; p < Config.N; p++) {
                RBA_max[p] = 0;
                RBA_total[p] = 0;
                for (int b = 0; b < bank_max; b++) {
                    // in a sense, this is inaccurate, because it assumes that all requests by proc are row conflicts
                    // threadRBAMax[proc] += Simulator.RowBufferHit * blockingRequestsPerBank[b]
                    //                       + Simulator.BankConflictTime * curMarkedPerProcBank[proc, b];
                    // an alternative, too positive solution:
                    if (cur_marked_per_procbank[p, b] > 0) {
                        if (p != cur_bankproc[b]) {
                            RBA_total[p] += block_req_per_bank[b] + cur_marked_per_procbank[p, b];
                            if (RBA_max[p] < block_req_per_bank[b] + cur_marked_per_procbank[p, b])
                                RBA_max[p] = block_req_per_bank[b] + cur_marked_per_procbank[p, b];
                        }
                        else {
                            RBA_total[p] += cur_marked_per_procbank[p, b];
                            if (RBA_max[p] < cur_marked_per_procbank[p, b])
                                RBA_max[p] = cur_marked_per_procbank[p, b];
                        }
                    }

                }
            }
            // now sort according to threadRBAMax!
            int temp;
            for (int i = 0; i < Config.N - 1; i++) {
                for (int j = i + 1; j < Config.N; j++) {
                    if (RBA_max[rank_to_proc[i]] > RBA_max[rank_to_proc[j]]) {
                        temp = rank_to_proc[i];
                        rank_to_proc[i] = rank_to_proc[j];
                        rank_to_proc[j] = temp;
                    }
                    else if (RBA_max[rank_to_proc[i]] == RBA_max[rank_to_proc[j]]
                        && RBA_total[rank_to_proc[i]] > RBA_total[rank_to_proc[j]]) {
                        temp = rank_to_proc[i];
                        rank_to_proc[i] = rank_to_proc[j];
                        rank_to_proc[j] = temp;
                    }
                }
            }
            proc_to_rank[rank_to_proc[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++) {
                if ((RBA_max[rank_to_proc[i]] == RBA_max[rank_to_proc[i - 1]]) && (RBA_total[rank_to_proc[i]] == RBA_total[rank_to_proc[i - 1]])) {
                    proc_to_rank[rank_to_proc[i]] = proc_to_rank[rank_to_proc[i - 1]];
                }
                else {
                    proc_to_rank[rank_to_proc[i]] = proc_to_rank[rank_to_proc[i - 1]] - 1;
                }
            }
        }

        public void LP()
        {
            // Call lp_solve to get the ranking

            const string NewLine = "\n";
            int lp;
            double[] Row;
            //double[] Col;
            //double[] Lower;
            //double[] Upper;
            //double[] Arry;
            double[] ProcArray = new double[Config.N];
            double[,] p = new double[Config.N, bank_max];
            double[] Solution;

            // Make a liner program with x columns and y rows. Columns are the variables.
            // lp = lpsolve.make_lp(x, y);
            // Rows will include the function to minimize or maximize, and the constraints to abide by

            lp = lpsolve.make_lp(0, Config.N); // <--- Constructs the matrix to express the linear program

            lpsolve.set_outputfile(lp, "lpsolve-out.txt");
            // lpsolve.set_maxim(lp); <--- Use this if we want to maximize the objective function

            for (int tt = 0; tt < Config.N; tt++) {
                ProcArray[tt] = 1;
            }
            fill_array(out Row, ProcArray);

            lpsolve.print_str(lp, "Set the objective function" + NewLine);
            lpsolve.print_str(lp, "lpsolve.set_obj_fn(lp, ref Row[0]);" + NewLine);
            lpsolve.set_obj_fn(lp, ref Row[0]); // <--- sets our objective function: Minimize sigma Ci
            lpsolve.print_lp(lp);

            // Now add constraints
            // Ci >= 0
            for (int kk = 0; kk < Config.N; kk++) {
                for (int tt = 0; tt < Config.N; tt++) {
                    // zero out procarray first    
                    ProcArray[tt] = 0;
                }
                ProcArray[kk] = 1;
                fill_array(out Row, ProcArray);
                lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, 0);
                lpsolve.print_lp(lp);
            }

            // Find p values (pij) based on available information ("job length" of thread i in bank j) 
            // --- currently this is the same as # of requests from thread i in bank j
            // if we have limited information about a bank, we will need to change the body of this loop
            for (int tt = 0; tt < Config.N; tt++) {
                for (int bb = 0; bb < bank_max; bb++) {
                    p[tt, bb] = cur_load_per_procbank[tt, bb];
                    //p[tt, bb] = curMarkedPerProcBank[tt, bb];
                }
            }

            // Now add more constraints for each bank
            for (int bb = 0; bb < bank_max; bb++) {
                lpsolve.print_str(lp, "Adding constraints for Bank " + bb + NewLine);
                // for each thread combination subset
                for (int tcomb = 1; tcomb < (Math.Pow(2, Config.N)); tcomb++) {
                    int examined_tcomb = tcomb;
                    int ii;
                    int[] one_hot_threadarray = new int[Config.N];

                    for (int tt = 0; tt < Config.N; tt++) {
                        // zero out procarray first    
                        one_hot_threadarray[tt] = 0;
                        ProcArray[tt] = 0;
                    }

                    ii = 0;
                    while (examined_tcomb > 0) {
                        if ((examined_tcomb & 1) > 0) {
                            one_hot_threadarray[ii] = 1;
                        }
                        examined_tcomb = (examined_tcomb >> 1);
                        ii++;
                    }

                    // compute [sigma pij]^2
                    double squared_sigma_pij = 0;
                    double sigma_pij_squared = 0;
                    double inequality_RHS = 0; // right hand side of >= inequality
                    for (int tt = 0; tt < Config.N; tt++) {
                        if (one_hot_threadarray[tt] > 0) {
                            squared_sigma_pij += p[tt, bb];
                            sigma_pij_squared += p[tt, bb] * p[tt, bb];
                            ProcArray[tt] = p[tt, bb];
                        }
                    }
                    squared_sigma_pij = squared_sigma_pij * squared_sigma_pij;
                    inequality_RHS = 0.5 * (squared_sigma_pij + sigma_pij_squared);

                    fill_array(out Row, ProcArray);
                    lpsolve.print_str(lp, "Adding constraints for Bank " + bb + " tcomb " + tcomb + NewLine);
                    lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, inequality_RHS);
                    lpsolve.print_lp(lp);
                }
            }


            /* int errorcode = (int) */ lpsolve.solve(lp); // <--- Solves the LP
            double solution = lpsolve.get_objective(lp); // <--- Get the value of the solved objective function

            lpsolve.print_str(lp, "PRINTING SOLUTION: " + lpsolve.get_objective(lp) + " " + solution + "\n");

            Solution = new double[lpsolve.get_Ncolumns(lp)];
            lpsolve.get_variables(lp, ref Solution[0]); // <--- These are the values for the variables placed into the Col array

            lpsolve.print_str(lp, "PRINTING VARIABLE VALUES...\n");
            //Console.WriteLine("PRINTING VARIABLE VALUES...\n");
            for (int i = 0; i < Solution.Length; i++) {
                lpsolve.print_str(lp, Solution[i] + " ");
                //  Console.Write(Solution[i] + " ");
            }
            lpsolve.print_str(lp, "\n");
            //Console.Write("\n");

            //currentLoadPerProcBank = new int[Simulator.NumberOfProcessors, Simulator.NumberOfBanks];

            // Sort the solution to get the final thread ranking
            {
                // assign rank values
                double[] CompletionTimes = new double[Config.N];
                int[] CorrespondingThreads = new int[Config.N];
                double temp;
                int itemp;
                for (int i = 0; i < Config.N; i++) {
                    CompletionTimes[i] = Solution[i];
                    CorrespondingThreads[i] = i;
                }

                bool swapped = false;
                do {
                    swapped = false;
                    for (int i = 0; i < (Config.N - 1); i++) {
                        if (CompletionTimes[i] < CompletionTimes[i + 1]) {
                            temp = CompletionTimes[i];
                            CompletionTimes[i] = CompletionTimes[i + 1];
                            CompletionTimes[i + 1] = temp;
                            itemp = CorrespondingThreads[i];
                            CorrespondingThreads[i] = CorrespondingThreads[i + 1];
                            CorrespondingThreads[i + 1] = itemp;
                            swapped = true;
                        }
                    }
                } while (swapped);

                /* sort
                for (int i = 0; i < (Simulator.NumberOfProcessors - 1); i++)
                {
                    for (int j = 0; j < (Simulator.NumberOfProcessors - 1 - i); j++)
                    {
                        if (CompletionTimes[j+1] > CompletionTimes[j])
                        {
                            temp = CompletionTimes[j];
                            CompletionTimes[j] = CompletionTimes[j+1];
                            CompletionTimes[j+1] = temp;
                            temp = CorrespondingThreads[j];
                            CorrespondingThreads[j] = CorrespondingThreads[j+1];
                            CorrespondingThreads[j+1] = temp;
                        }
                    }
                }
                */

                for (int i = 0; i < Config.N; i++) {
                    proc_to_rank[CorrespondingThreads[i]] = i;
                }

            }

            // print the ranking
            lpsolve.print_str(lp, "RANKING: ");
            //Console.WriteLine("RANKING: ");
            for (int i = 0; i < Config.N; i++) {
                lpsolve.print_str(lp, proc_to_rank[i] + " ");
                //  Console.Write(Rank[i] + " ");
            }
            lpsolve.print_str(lp, "\n");
            //Console.Write("\n");


            /*
            lpsolve.print_str(lp, "Now we add some constraints" + NewLine);
            lpsolve.print_str(lp, "lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.LE, 4);" + NewLine);
            FillArray(out Row, new double[] { 3, 2, 2, 1 });
            lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.LE, 4); // <--- Adds a constraint using the numbers in Row as coefficients
            lpsolve.print_lp(lp);

            lpsolve.print_str(lp, "lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, 3);" + NewLine);
            FillArray(out Row, new double[] { 0, 4, 3, 1 });
            lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, 3); // <--- Adds a constraint using the numbers in Row as coefficients
            lpsolve.print_lp(lp);
            */

            /*int errorcode = (int) lpsolve.solve(lp); // <--- Solves the LP
            double solution = lpsolve.get_objective(lp); // <--- Get the value of the solved objective function

            lpsolve.print_str(lp, "PRINTING SOLUTION: " + lpsolve.get_objective(lp) + " " + solution + "\n");

            Col = new double[lpsolve.get_Ncolumns(lp)];
            lpsolve.get_variables(lp, ref Col[0]); // <--- These are the values for the variables placed into the Col array

            lpsolve.print_str(lp, "PRINTING VARIABLE VALUES...\n");
            for (int i = 0; i < Col.Length; i++)
            {
                lpsolve.print_str(lp, Col[i] + " ");
            }
            lpsolve.print_str(lp, "\n");
            */

            Row = new double[lpsolve.get_Nrows(lp)];
            lpsolve.get_constraints(lp, ref Row[0]); // <--- These are the values for the constraints placed into the Row array

            lpsolve.print_str(lp, "PRINTING CONSTRAINT VALUES...\n");
            for (int i = 0; i < Row.Length; i++) {
                lpsolve.print_str(lp, Row[i] + " ");
            }
            lpsolve.print_str(lp, "\n");

            lpsolve.set_outputfile(lp, null);

            lpsolve.delete_lp(lp); // <-- delete lp so that we do not leak

            if ((cur_load_per_proc[0] > 9) && (cur_load_per_proc[1] > 2)) {
                //System.Environment.Exit(-1);
            }

            /*
            for (int bank = 0; bank < Simulator.NumberOfBanks; bank++)
            {
                int temp = perBankprocRank[0, bank];
                for (int i = 0; i < Simulator.NumberOfProcessors - 1; i++)
                {
                    perBankprocRank[i, bank] = perBankprocRank[i + 1, bank];
                }
                perBankprocRank[Simulator.NumberOfProcessors - 1, bank] = temp;
                for (int i = 0; i < Simulator.NumberOfProcessors - 1; i++)
                {
                    perBankRank[perBankprocRank[i, bank], bank] = i;
                }
            }
            */
        }
    }
}
