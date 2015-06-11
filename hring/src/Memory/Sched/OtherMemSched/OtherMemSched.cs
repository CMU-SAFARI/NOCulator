using System;
using System.Collections.Generic;
using System.Text;
using lpsolve55;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    abstract class PERBANK_BATCH_MemoryScheduler : AbstractMemSched
    {
        public int[] procRank;
        public int[] Rank;
        public int[,] perBankprocRank;
        public int[,] perBankRank;
        public ulong currentBatchStartTime;
        public int markedReqThisBatch;
        public ulong markCount = 0;
        public ulong recompCount = 0;
        public ulong ACTSamplingCount = 0;
        public ulong ACTSampleTaken = 0;
        public bool samplingRankingPolicy = false;
        public bool activeBatch = true;

        public RankAlgo rankingScheme;
        public BatchSchedAlgo withinBatchPriority;

        public int currentPriorityThread = 0;

        // variables used for remarking
        protected int[] curMarkedPerProc;
        protected int[] thisPeriodMarkedPerProc;
        protected int[] thisPeriodMarkedMaxPerProc;
        protected int[] thisPeriodMarkedTotalPerProc;
        protected int[,] thisPeriodMarkedPerProcBank; // counter that maintains how many requests per proc x bank was marked within the batch (not decremented) 
        protected int[,] curMarkedPerProcBank; // counter that maintains how many requests per proc x bank  
        protected bool[,] exceedsBatchingCap; // flag is true if counter exceeds threshold. 

        // variables for the row-buffer-aware ranking scheme. 
        int[] blockingRequestsPerBank = new int[Config.memory.bank_max_per_mem];
        int[] threadRBAMax = new int[Config.N];
        int[] threadRBATot = new int[Config.N];
        int[] currentlyUsingBankThread = new int[Config.memory.bank_max_per_mem];


        //Statistics for SIB
        public double[] avgNumReqPerProcRemark;  // avg nr of requests of a thread at the time of a remarking
        public double[] avgMaxReqPerProcRemark;  // max nr of requests of a thread at the time of a remarking
        public double[] avgNumMarkedReqPerProcRemark;  // avg max nr of requests of a thread that become marked at a remarking
        public double[] avgMaxMarkedReqPerProcRemark;  // max nr of requests of a thread that become marked at a remarking
        public ulong[] avgNumReqPerProcRecomp;
        public ulong[] avgMaxReqPerProcRecomp;
        public ulong[] avgMarkedReqAtBatchEnd;
        protected ulong[] nrTotalMarkedRequests;
        protected ulong[] nrTotalRequests;
        public ulong avgFullBatchingDuration;

        // stats counter variables for computer average completion time. 
        protected ulong[] totalBatchCompletionTime;
        protected ulong[] numberOfActiveBatches;
        protected ulong[] sample_totalBatchCompletionTime;
        protected ulong[] sample_numberOfActiveBatches;
        protected ulong[] LPoptimal_totalBatchCompletionTime;
        protected ulong[] LPoptimal_numberOfActiveBatches;
        protected double sample_TotalACT = 0;
        protected ulong sample_TotalACTSamples = 0;
        protected ulong thisBatchCompletionTime = 0;
        protected ulong thisBatchCompletedThreadCount = 0;
        protected double LPoptimal_TotalACT = 0;
        protected ulong LPoptimal_TotalACTSamples = 0;

        // Final Stats
        protected double[] finalavgNumReqPerProcRemark;
        protected double[] finalavgMaxReqPerProcRemark;
        protected double[] finalavgNumMarkedReqPerProcRemark;
        protected double[] finalavgMaxMarkedReqPerProcRemark;
        protected double[] finalavgNumReqPerProcRecomp;
        protected double[] finalavgMaxReqPerProcRecomp;
        protected double[] finalavgMarkedReqAtBatchEnd;
        protected ulong[] finaltotalBatchCompletionTime;
        protected ulong[] final_sample_numberOfActiveBatches;
        protected ulong[] final_sample_totalBatchCompletionTime;
        protected ulong[] final_LPoptimal_numberOfActiveBatches;
        protected ulong[] final_LPoptimal_totalBatchCompletionTime;
        protected ulong[] finalnumberOfActiveBatches;
        protected ulong[] finalnrTotalMarkedRequests;
        protected ulong[] finalnrTotalRequests;




        public PERBANK_BATCH_MemoryScheduler(int totalSize, Bank[] bank,
            RankAlgo rankingScheme, BatchSchedAlgo withinBatchPriority)
            : base(totalSize, bank)
        {
            this.rankingScheme = rankingScheme;
            this.withinBatchPriority = withinBatchPriority;
            procRank = new int[Config.N];
            Rank = new int[Config.N];
            perBankprocRank = new int[Config.N, Config.memory.bank_max_per_mem];
            perBankRank = new int[Config.N, Config.memory.bank_max_per_mem];
            curMarkedPerProc = new int[Config.N];
            thisPeriodMarkedPerProc = new int[Config.N];
            avgNumReqPerProcRemark = new double[Config.N];
            avgMaxReqPerProcRemark = new double[Config.N];
            avgNumMarkedReqPerProcRemark = new double[Config.N];
            avgMaxMarkedReqPerProcRemark = new double[Config.N];
            avgNumReqPerProcRecomp = new ulong[Config.N];
            avgMaxReqPerProcRecomp = new ulong[Config.N];
            avgMarkedReqAtBatchEnd = new ulong[Config.N];
            totalBatchCompletionTime = new ulong[Config.N];
            numberOfActiveBatches = new ulong[Config.N];
            sample_totalBatchCompletionTime = new ulong[Config.N];
            sample_numberOfActiveBatches = new ulong[Config.N];
            LPoptimal_totalBatchCompletionTime = new ulong[Config.N];
            LPoptimal_numberOfActiveBatches = new ulong[Config.N];
            nrTotalMarkedRequests = new ulong[Config.N];
            nrTotalRequests = new ulong[Config.N];
            finalavgNumReqPerProcRemark = new double[Config.N];
            finalavgMaxReqPerProcRemark = new double[Config.N];
            finalavgNumMarkedReqPerProcRemark = new double[Config.N];
            finalavgMaxMarkedReqPerProcRemark = new double[Config.N];
            finalavgNumReqPerProcRecomp = new double[Config.N];
            finalavgMaxReqPerProcRecomp = new double[Config.N];
            finalavgMarkedReqAtBatchEnd = new double[Config.N];
            finaltotalBatchCompletionTime = new ulong[Config.N];
            finalnumberOfActiveBatches = new ulong[Config.N];
            final_sample_totalBatchCompletionTime = new ulong[Config.N];
            final_sample_numberOfActiveBatches = new ulong[Config.N];
            final_LPoptimal_totalBatchCompletionTime = new ulong[Config.N];
            final_LPoptimal_numberOfActiveBatches = new ulong[Config.N];
            finalnrTotalMarkedRequests = new ulong[Config.N];
            finalnrTotalRequests = new ulong[Config.N];
            curMarkedPerProcBank = new int[Config.N, Config.memory.bank_max_per_mem];
            thisPeriodMarkedPerProcBank = new int[Config.N, Config.memory.bank_max_per_mem];
            exceedsBatchingCap = new bool[Config.N, Config.memory.bank_max_per_mem];
            thisPeriodMarkedMaxPerProc = new int[Config.N];
            thisPeriodMarkedTotalPerProc = new int[Config.N];

            for (int i = 0; i < Config.N; i++)
            {
                procRank[i] = i;
                Rank[i] = Config.N - 1;

                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                {
                    perBankprocRank[i, b] = i;
                    perBankRank[i, b] = Config.N - 1;
                }
            }
            Console.WriteLine("Initialized PERBANK_BATCH_MemoryScheduler");
            Console.WriteLine("Ranking Scheme: " + rankingScheme.ToString() + " Sampling: " + Config.memory.ACTSamplingBatchInterval);
            Console.WriteLine("WithinBatch Priority: " + withinBatchPriority.ToString());
            Console.WriteLine("BatchingCap: " + Config.memory.batch_cap);
        }



        protected virtual void reMark()
        {
            // Implement BatchingCap: a per-thread per-bank threshold for marking. 
            // This limits the number of marked requests and avoids short-term starvation 
            // of non-intensive threads
            markCount++;
            markedReqThisBatch = 0;
            currentBatchStartTime = MemCtlr.cycle;
            // reset batchingCapCounters
            for (int proc = 0; proc < Config.N; proc++)
            {
                curMarkedPerProc[proc] = 0;
                thisPeriodMarkedPerProc[proc] = 0;
                for (int bank = 0; bank < Config.memory.bank_max_per_mem; bank++)
                {
                    curMarkedPerProcBank[proc, bank] = 0;
                    thisPeriodMarkedPerProcBank[proc, bank] = 0;
                    exceedsBatchingCap[proc, bank] = false;
                }
            }

            if (samplingRankingPolicy)
            {
                sample_TotalACT += (thisBatchCompletionTime / thisBatchCompletedThreadCount);
                sample_TotalACTSamples++;
                thisBatchCompletionTime = 0;
                thisBatchCompletedThreadCount = 0;
            }


            // do the marking!
            for (int b = 0; b < Config.memory.bank_max_per_mem; b++)  // for each bank
            {
                for (int row = 0; row < buf_load[b]; row++)
                {
                    int procID = buf[b, row].request.requesterID;
                    if (buf[b, row].isMarked == true)
                    {   // should never be the case for full batching!
                        if (this is PERBANK_FULL_BATCH_MemoryScheduler) throw new Exception("There is a marked entry remaining in the batch! This is not allowed in full-batching");
                        avgMarkedReqAtBatchEnd[procID]++;
                    }

                    if (curMarkedPerProcBank[procID, b] < Config.memory.batch_cap)
                    {
                        // mark this request and increase the counter!
                        curMarkedPerProcBank[procID, b]++;
                        curMarkedPerProc[procID]++;
                        markedReqThisBatch++;
                        thisPeriodMarkedPerProcBank[procID, b]++;
                        thisPeriodMarkedPerProc[procID]++;
                        buf[b, row].isMarked = true;
                        avgNumMarkedReqPerProcRemark[procID]++;  // stats
                    }
                    else if (curMarkedPerProcBank[procID, b] == Config.memory.batch_cap)
                    {
                        exceedsBatchingCap[procID, b] = true;
                        // TODO: no new requests should be marked, but requests that are older should be marked!
                    }
                }
            }

            // now for all proc x bank pairs that exceeded the cap, find the N oldest requests. 
            for (int proc = 0; proc < Config.N; proc++)
                for (int bank = 0; bank < Config.memory.bank_max_per_mem; bank++)
                    if (exceedsBatchingCap[proc, bank])
                    {
                        // first, unmark all requests from this thread. 
                        for (int row = 0; row < buf_load[bank]; row++)
                        {
                            if (buf[bank, row].request.requesterID == proc)
                            {
                                buf[bank, row].isMarked = false;
                            }
                        }
                        // remark the requests so that the N oldest requests are marked!
                        // TODO: The whole thing can be implemented more efficiently!
                        for (int n = 0; n < Config.memory.batch_cap; n++)
                        {
                            MemoryRequest curOldest = null;
                            for (int row = 0; row < buf_load[bank]; row++)
                            {
                                if (buf[bank, row].request.requesterID == proc && !buf[bank, row].isMarked &&
                                    (curOldest == null ||
                                    buf[bank, row].timeOfArrival < curOldest.timeOfArrival))
                                {
                                    curOldest = buf[bank, row];
                                }
                            }
                            if (curOldest.isMarked) throw new Exception("Error in Marking procedure!");
                            curOldest.isMarked = true;
                        }
                    }

            // check whether marking stats is still ok. 
            // TODO: This whole thing could be implemented better!
            for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                bank[b].update_marking();

            for (int proc = 0; proc < Config.N; proc++)
            {
                avgNumReqPerProcRemark[proc] += cur_load_per_proc[proc];
                avgMaxReqPerProcRemark[proc] += cur_max_load_per_proc[proc];
                avgMaxMarkedReqPerProcRemark[proc] += Math.Min(cur_max_load_per_proc[proc], Config.memory.batch_cap);
            }


            for (int proc = 0; proc < Config.N; proc++)
            {
                thisPeriodMarkedMaxPerProc[proc] = 0; // compute max over marked requests
                for (int nob1 = 0; nob1 < Config.memory.bank_max_per_mem; nob1++)
                    if (thisPeriodMarkedMaxPerProc[proc] < thisPeriodMarkedPerProcBank[proc, nob1])
                        thisPeriodMarkedMaxPerProc[proc] = thisPeriodMarkedPerProcBank[proc, nob1];
                thisPeriodMarkedTotalPerProc[proc] = 0; // compute max over marked requests
                for (int nob1 = 0; nob1 < Config.memory.bank_max_per_mem; nob1++)
                    thisPeriodMarkedTotalPerProc[proc] += thisPeriodMarkedPerProcBank[proc, nob1];
            }

        }

        protected virtual void recomputeRank()
        {

            for (int i = 0; i < Config.N; i++)
            {
                avgNumReqPerProcRecomp[i] += (ulong)cur_load_per_proc[i];
                avgMaxReqPerProcRecomp[i] += (ulong)cur_max_load_per_proc[i];
            }

            if ((Config.memory.ACTSamplingBatchInterval > 0) && (ACTSamplingCount > 0) && ((ACTSamplingCount % (ulong)Config.memory.ACTSamplingBatchInterval) == 0))
            {
                // try a different ranking policy here to get an estimate of average completion times through sampling

                samplingRankingPolicy = true;
                ACTSampleTaken++;
                Console.WriteLine("Sampling Batch " + ACTSamplingCount);

                switch (rankingScheme)
                {
                    //case Simulator.RankingSchemeType.ROW_BUFFER_AWARE_MAX_RANKING:
                    //    computeRBAMaxRanking();
                    //    break;
                    case RankAlgo.MAX_TOT_RANKING:
                        //computePerBankMaxTotRanking();
                        //computeMaxTotRanking();
                        computeMarkedMaxTotRanking();
                        break;
                    case RankAlgo.PERBANK_SJF_RANKING:
                        //computePerBankMaxTotRanking(); 
                        computeMarkedPerBankMaxTotRanking();
                        break;
                    //case Simulator.RankingSchemeType.TOT_MAX_RANKING:
                    //    computeTotMaxRanking();
                    //    break;
                    case RankAlgo.ROUND_ROBIN_RANKING:
                        computePerBankRoundRobinRanking();
                        break;
                    case RankAlgo.RANDOM_RANKING:
                        computePerBankRandomRanking();
                        break;
                    case RankAlgo.PERBANK_LP_RANKING:
                        //computePerBankLPRanking();
                        computeMarkedPerBankLPRanking();
                        break;
                    default:
                        // writer.WriteLine("No such ranking scheme\n");
                        Debug.Assert(false);
                        System.Environment.Exit(-1);
                        break;
                    //case Simulator.RankingSchemeType.PERBANK_RANDOM_RANKING:
                    //        computePerBankRandomRanking();
                    //    break;
                    //case Simulator.RankingSchemeType.PERBANK_MAX_TOT_RANKING:
                    //    computePerBankMaxTotRanking();
                    //    break;
                }

            }
            else if (Config.memory.ACTSamplingBatchInterval > 0)
            {
                // use the fixed baseline policy (PARBS) to do the ranking

                samplingRankingPolicy = false;

                //computeMaxTotRanking();
                computeMarkedMaxTotRanking();
            }

            if (Config.memory.ACTSamplingBatchInterval > 0)
            {
                ACTSamplingCount++;
                return;
            }


            switch (rankingScheme)
            {
                //case Simulator.RankingSchemeType.ROW_BUFFER_AWARE_MAX_RANKING:
                //    computeRBAMaxRanking();
                //    break;
                case RankAlgo.MAX_TOT_RANKING:
                    //computePerBankMaxTotRanking();
                    //computeMaxTotRanking();
                    computeMarkedMaxTotRanking();
                    break;
                case RankAlgo.PERBANK_SJF_RANKING:
                    //computePerBankMaxTotRanking(); 
                    computeMarkedPerBankMaxTotRanking();
                    break;
                //case Simulator.RankingSchemeType.TOT_MAX_RANKING:
                //    computeTotMaxRanking();
                //    break;
                case RankAlgo.ROUND_ROBIN_RANKING:
                    computePerBankRoundRobinRanking();
                    break;
                case RankAlgo.RANDOM_RANKING:
                    computePerBankRandomRanking();
                    break;
                case RankAlgo.PERBANK_LP_RANKING:
                    //computePerBankLPRanking();
                    computeMarkedPerBankLPRanking();
                    break;
                default:
                    Debug.Assert(false);
                    //writer.WriteLine("No such ranking scheme\n");
                    System.Environment.Exit(-1);
                    break;
                //case Simulator.RankingSchemeType.PERBANK_RANDOM_RANKING:
                //    computePerBankRandomRanking();
                //    break;
                //case Simulator.RankingSchemeType.PERBANK_MAX_TOT_RANKING:
                //    computePerBankMaxTotRanking();
                //    break;
            }


        }

        public override void remove_req(MemoryRequest request)
        {
            int threadID = request.request.requesterID;
            nrTotalRequests[threadID]++;
            if (request.isMarked)
            {
                nrTotalMarkedRequests[threadID]++;
                curMarkedPerProc[threadID]--;
                curMarkedPerProcBank[threadID, request.glob_b_index]--;
                // do not touch thisPeriodMarkedPerProcBank[procID, bank]

                // If there had been requests marked, but they are all done now, 
                // increase the stats variables!
                if (curMarkedPerProc[threadID] == 0 && thisPeriodMarkedPerProc[threadID] > 0)
                {
                    totalBatchCompletionTime[threadID] += MemCtlr.cycle - currentBatchStartTime;
                    numberOfActiveBatches[threadID]++;

                    if (samplingRankingPolicy)
                    {
                        sample_totalBatchCompletionTime[threadID] += MemCtlr.cycle - currentBatchStartTime;
                        sample_numberOfActiveBatches[threadID]++;

                        thisBatchCompletionTime += MemCtlr.cycle - currentBatchStartTime;
                        thisBatchCompletedThreadCount++;

                    }

                }


                markedReqThisBatch--;
                if (markedReqThisBatch == 0)
                    Config.memory.tavgNumReqPBPerProcRemark += MemCtlr.cycle % Config.memory.mark_interval;
            }
            base.remove_req(request);
        }

        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;

            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC)
            {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else
            {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0)
                {
                    // Find the highest priority request for this bank
                    if (nextRequest == null) nextRequest = buf[b, 0];
                    if (Config.memory.batch_sched_algo == BatchSchedAlgo.MARKED_FR_RANK_FCFS)
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (!helper.is_MARK_FR_RANK_FCFS(nextRequest, buf[b, j], perBankRank[nextRequest.request.requesterID, b], perBankRank[buf[b, j].request.requesterID, b]))
                            {
                                nextRequest = buf[b, j];
                            }
                            /* global rank across all banks
                            if (!higher_MARK_FR_RANK_FCFS_Priority(nextRequest, buffer[i, j], Rank[nextRequest.threadID], Rank[buffer[i, j].threadID]))
                            {
                                nextRequest = buffer[i, j];
                            }
                            */
                        }
                    else if (Config.memory.batch_sched_algo == BatchSchedAlgo.MARKED_RANK_FR_FCFS)
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (!helper.is_MARK_RANK_FR_FCFS(nextRequest, buf[b, j], perBankRank[nextRequest.request.requesterID, b], perBankRank[buf[b, j].request.requesterID, b]))
                            {
                                nextRequest = buf[b, j];
                            }

                            /*
                            if (!higher_MARK_RANK_FR_FCFS_Priority(nextRequest, buffer[i, j], Rank[nextRequest.threadID], Rank[buffer[i, j].threadID]))
                            {
                                nextRequest = buffer[i, j];
                            }
                            */
                        }

                    else if (Config.memory.batch_sched_algo == BatchSchedAlgo.MARKED_FR_FCFS)
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (!helper.is_MARK_FR_FCFS(nextRequest, buf[b, j]))
                            {
                                nextRequest = buf[b, j];
                            }
                        }
                    else if (Config.memory.batch_sched_algo == BatchSchedAlgo.MARKED_FCFS)
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (!helper.is_MARK_FCFS(nextRequest, buf[b, j]))
                            {
                                nextRequest = buf[b, j];
                            }
                        }

                    else
                        throw new Exception("Unknown WithinBatchPriority");
                }
            }

            return nextRequest;
        }

        public override void freeze_stat(int procID)
        {
            base.freeze_stat(procID);
            finalavgNumReqPerProcRemark[procID] = (double)avgNumReqPerProcRemark[procID] / (double)markCount;
            finalavgMaxReqPerProcRemark[procID] = (double)avgMaxReqPerProcRemark[procID] / (double)markCount;
            finalavgNumMarkedReqPerProcRemark[procID] = (double)avgNumMarkedReqPerProcRemark[procID] / (double)markCount;
            finalavgMaxMarkedReqPerProcRemark[procID] = (double)avgMaxMarkedReqPerProcRemark[procID] / (double)markCount;
            finalavgNumReqPerProcRecomp[procID] = (double)avgNumReqPerProcRecomp[procID] / (double)recompCount;
            finalavgMaxReqPerProcRecomp[procID] = (double)avgMaxReqPerProcRecomp[procID] / (double)recompCount;
            finalavgMarkedReqAtBatchEnd[procID] = (double)avgMarkedReqAtBatchEnd[procID] / (double)markCount;
            finalnumberOfActiveBatches[procID] = numberOfActiveBatches[procID];
            finaltotalBatchCompletionTime[procID] = totalBatchCompletionTime[procID];

            final_sample_numberOfActiveBatches[procID] = sample_numberOfActiveBatches[procID];
            final_sample_totalBatchCompletionTime[procID] = sample_totalBatchCompletionTime[procID];

            final_LPoptimal_numberOfActiveBatches[procID] = LPoptimal_numberOfActiveBatches[procID];
            final_LPoptimal_totalBatchCompletionTime[procID] = LPoptimal_totalBatchCompletionTime[procID];

            finalnrTotalMarkedRequests[procID] = nrTotalMarkedRequests[procID];
            finalnrTotalRequests[procID] = nrTotalRequests[procID];
        }

        public override void report(TextWriter writer, int procID)
        {
            base.report(writer, procID);
            writer.WriteLine("Batching-Proc Stats:");
            writer.WriteLine("   AvgTotPerRemark: " + finalavgNumReqPerProcRemark[procID]);
            writer.WriteLine("   AvgMaxPerRemark: " + finalavgMaxReqPerProcRemark[procID]);
            writer.WriteLine("   AvgTotMarkedRequestsPerRemark: " + finalavgNumMarkedReqPerProcRemark[procID]);
            writer.WriteLine("   AvgMaxMarkedRequestsPerRemark: " + finalavgMaxMarkedReqPerProcRemark[procID]);
            writer.WriteLine("   AvgTotPerRecomp: " + finalavgNumReqPerProcRecomp[procID]);
            writer.WriteLine("   AvgMaxPerRecomp: " + finalavgMaxReqPerProcRecomp[procID]);
            writer.WriteLine("   AvgMarkedReq at Batch-End: " + finalavgMarkedReqAtBatchEnd[procID]);
            writer.WriteLine("   TotalNrOfActiveBatches: " + finalnumberOfActiveBatches[procID]);
            writer.WriteLine("   AvgBatchCompletionTime: " + (double)finaltotalBatchCompletionTime[procID] / (double)finalnumberOfActiveBatches[procID]);
            writer.WriteLine("   TotalNrOfRequestsServiced: " + finalnrTotalRequests[procID]);
            writer.WriteLine("   TotalNrOfMarkedRequestsServiced: " + finalnrTotalMarkedRequests[procID]);
            writer.WriteLine("   PercentageOfMarkedRequests: " + 100 * (double)finalnrTotalMarkedRequests[procID] / (double)finalnrTotalRequests[procID]);
            writer.WriteLine("   SAMPLE-AvgBatchCompletionTime: " + (double)final_sample_totalBatchCompletionTime[procID] / (double)final_sample_numberOfActiveBatches[procID]);
            if (rankingScheme == RankAlgo.PERBANK_LP_RANKING)
            {
                writer.WriteLine("   SAMPLE-AvgBatchCompletionTime: " + (double)final_LPoptimal_totalBatchCompletionTime[procID] / (double)final_LPoptimal_numberOfActiveBatches[procID]);
            }
        }

        public override void report(TextWriter writer)
        {
            writer.WriteLine("Batching-Statistics:");
            writer.WriteLine("   Number of Batches: " + markCount);
            writer.WriteLine("   AvgBatchDuration: " + (double)avgFullBatchingDuration / (double)markCount);

            ulong totalActiveBatches = 0;
            ulong totalCompletionTime = 0;
            ulong totalRequests = 0;
            ulong totalMarkedRequests = 0;
            ulong sample_totalActiveBatches = 0;
            ulong sample_totalCompletionTime = 0;
            ulong LPoptimal_totalActiveBatches = 0;
            ulong LPoptimal_totalCompletionTime = 0;
            for (int i = 0; i < Config.N; i++)
            {
                totalActiveBatches += finalnumberOfActiveBatches[i];
                totalCompletionTime += finaltotalBatchCompletionTime[i];
                totalRequests += finalnrTotalRequests[i];
                totalMarkedRequests += finalnrTotalMarkedRequests[i];
                sample_totalActiveBatches += final_sample_numberOfActiveBatches[i];
                sample_totalCompletionTime += final_sample_totalBatchCompletionTime[i];
                LPoptimal_totalActiveBatches += final_LPoptimal_numberOfActiveBatches[i];
                LPoptimal_totalCompletionTime += final_LPoptimal_totalBatchCompletionTime[i];
            }
            writer.WriteLine("   AvgCompletionTime: " + (double)totalCompletionTime / (double)totalActiveBatches);
            writer.WriteLine("   SAMPLE-AvgCompletionTime: " + (double)sample_totalCompletionTime / (double)sample_totalActiveBatches);
            writer.WriteLine("   SAMPLE-NumberOfBatches: " + ACTSampleTaken);
            writer.WriteLine("   PercentageOfMarkedRequestsServiced: " + 100 * (double)totalMarkedRequests / (double)totalRequests);
            writer.WriteLine("   NEW-SAMPLE-ACT: " + sample_TotalACT / sample_TotalACTSamples);
            if (rankingScheme == RankAlgo.PERBANK_LP_RANKING)
            {
                writer.WriteLine("   LPOPTIMAL-AvgCompletionTime: " + (double)LPoptimal_totalCompletionTime / (double)LPoptimal_totalActiveBatches);
                writer.WriteLine("   NEW-LPOPTIMAL-AvgCompletionTime: " + (double)LPoptimal_TotalACT / (double)LPoptimal_TotalACTSamples);
            }

            base.report(writer);
        }

        public override void report_excel(TextWriter writer)
        {
            base.report_excel(writer);
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine(finalavgNumReqPerProcRemark[procID]);
            writer.WriteLine(); writer.WriteLine();
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine(finalavgMaxReqPerProcRemark[procID]);
            writer.WriteLine(); writer.WriteLine();
            for (int procID = 0; procID < Config.N; procID++)
                writer.WriteLine(finalavgMarkedReqAtBatchEnd[procID]);
            writer.WriteLine();
            writer.WriteLine((double)avgFullBatchingDuration / (double)markCount);
        }

        // This ranking takes into account the currently opened row and ranks different
        // threads according to the earliest time they would be finished if all currently 
        // marked requests in the buffer would be executed according to fr-rank and the
        // thread had first rank. (shortest-job first)
        protected virtual void computeRBAMaxRanking()
        {

            // find the number of outstanding, marked requests that go to the open row per bank 
            int markedRequestsToOpenRow;
            for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
            {
                ulong openedRowIndex = bank[b].get_cur_row();
                markedRequestsToOpenRow = 0;
                currentlyUsingBankThread[b] = -1;
                for (int row = 0; row < buf_load[b]; row++)
                {
                    if (buf[b, row].isMarked && buf[b, row].r_index == openedRowIndex)
                    {
                        currentlyUsingBankThread[b] = buf[b, row].request.requesterID;
                        markedRequestsToOpenRow++;
                    }
                }
                blockingRequestsPerBank[b] = markedRequestsToOpenRow;
            }
            // now compute the max of each thread. 
            for (int proc = 0; proc < Config.N; proc++)
            {
                threadRBAMax[proc] = 0;
                threadRBATot[proc] = 0;
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                {
                    // in a sense, this is inaccurate, because it assumes that all requests by proc are row conflicts
                    // threadRBAMax[proc] += Simulator.RowBufferHit * blockingRequestsPerBank[b]
                    //                       + Simulator.BankConflictTime * curMarkedPerProcBank[proc, b];
                    // an alternative, too positive solution:
                    if (curMarkedPerProcBank[proc, b] > 0)
                    {
                        if (proc != currentlyUsingBankThread[b])
                        {
                            threadRBATot[proc] += blockingRequestsPerBank[b] + curMarkedPerProcBank[proc, b];
                            if (threadRBAMax[proc] < blockingRequestsPerBank[b] + curMarkedPerProcBank[proc, b])
                                threadRBAMax[proc] = blockingRequestsPerBank[b] + curMarkedPerProcBank[proc, b];
                        }
                        else
                        {
                            threadRBATot[proc] += curMarkedPerProcBank[proc, b];
                            if (threadRBAMax[proc] < curMarkedPerProcBank[proc, b])
                                threadRBAMax[proc] = curMarkedPerProcBank[proc, b];
                        }
                    }

                }
            }
            // now sort according to threadRBAMax!
            int temp;
            for (int i = 0; i < Config.N - 1; i++)
            {
                for (int j = i + 1; j < Config.N; j++)
                {
                    if (threadRBAMax[procRank[i]] > threadRBAMax[procRank[j]])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else if (threadRBAMax[procRank[i]] == threadRBAMax[procRank[j]]
                        && threadRBATot[procRank[i]] > threadRBATot[procRank[j]])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                }
            }
            Rank[procRank[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++)
            {
                if ((threadRBAMax[procRank[i]] == threadRBAMax[procRank[i - 1]])
                    && (threadRBATot[procRank[i]] == threadRBATot[procRank[i - 1]]))
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }
        }

        protected virtual void computeMaxTotRanking()
        {
            int temp;
            for (int i = 0; i < Config.N - 1; i++)
            {
                for (int j = i + 1; j < Config.N; j++)
                {
                    if (cur_max_load_per_proc[procRank[i]] > cur_max_load_per_proc[procRank[j]])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else
                        if (cur_max_load_per_proc[procRank[i]] < cur_max_load_per_proc[procRank[j]]) { }
                        else
                        {
                            if (cur_load_per_proc[procRank[i]] > cur_load_per_proc[procRank[j]])
                            {
                                temp = procRank[i];
                                procRank[i] = procRank[j];
                                procRank[j] = temp;
                            }
                        }
                }
            }
            Rank[procRank[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++)
            {
                if ((cur_max_load_per_proc[procRank[i]] == cur_max_load_per_proc[procRank[i - 1]])
                    && (cur_load_per_proc[procRank[i]] == cur_load_per_proc[procRank[i - 1]]))
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }

            for (int i = 0; i < Config.N; i++)
            {
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                {
                    perBankRank[i, b] = Rank[i];
                }
            }
        }

        protected virtual void computeMarkedMaxTotRanking()
        {
            int temp;
            for (int i = 0; i < Config.N - 1; i++)
            {
                for (int j = i + 1; j < Config.N; j++)
                {
                    if (thisPeriodMarkedMaxPerProc[procRank[i]] > thisPeriodMarkedMaxPerProc[procRank[j]])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else
                        if (thisPeriodMarkedMaxPerProc[procRank[i]] < thisPeriodMarkedMaxPerProc[procRank[j]]) { }
                        else
                        {
                            if (thisPeriodMarkedTotalPerProc[procRank[i]] > thisPeriodMarkedTotalPerProc[procRank[j]])
                            {
                                temp = procRank[i];
                                procRank[i] = procRank[j];
                                procRank[j] = temp;
                            }
                        }
                }
            }
            Rank[procRank[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++)
            {
                if ((thisPeriodMarkedMaxPerProc[procRank[i]] == thisPeriodMarkedMaxPerProc[procRank[i - 1]])
                    && (thisPeriodMarkedTotalPerProc[procRank[i]] == thisPeriodMarkedTotalPerProc[procRank[i - 1]]))
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }

            for (int i = 0; i < Config.N; i++)
            {
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                {
                    perBankRank[i, b] = Rank[i];
                }
            }
        }

        /* This is essentially shortest-job-first per bank */
        protected virtual void computePerBankMaxTotRanking()
        {
            int temp;
            /*
            for (int i = 0; i < Simulator.NumberOfProcessors - 1; i++)
            {
                for (int j = i + 1; j < Simulator.NumberOfProcessors; j++)
                {
                    if (currentMaxPerProc[procRank[i]] > currentMaxPerProc[procRank[j]])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else
                        if (currentMaxPerProc[procRank[i]] < currentMaxPerProc[procRank[j]]) { }
                        else
                        {
                            if (currentTotalLoadPerProc[procRank[i]] > currentTotalLoadPerProc[procRank[j]])
                            {
                                temp = procRank[i];
                                procRank[i] = procRank[j];
                                procRank[j] = temp;
                            }
                        }
                }
            }
            Rank[procRank[0]] = Simulator.NumberOfProcessors - 1;
            for (int i = 1; i < Simulator.NumberOfProcessors; i++)
            {
                if ((currentMaxPerProc[procRank[i]] == currentMaxPerProc[procRank[i - 1]])
                    && (currentTotalLoadPerProc[procRank[i]] == currentTotalLoadPerProc[procRank[i - 1]]))
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }
            */

            // per-bank rank computation using per bank max/total values
            for (int bank = 0; bank < Config.memory.bank_max_per_mem; bank++)
            {
                for (int i = 0; i < Config.N - 1; i++)
                {
                    for (int j = i + 1; j < Config.N; j++)
                    {
                        if (cur_load_per_procbank[perBankprocRank[i, bank], bank] > cur_load_per_procbank[perBankprocRank[j, bank], bank])
                        {
                            temp = perBankprocRank[i, bank];
                            perBankprocRank[i, bank] = perBankprocRank[j, bank];
                            perBankprocRank[j, bank] = temp;
                        }
                        else /* this should not be executed with local info */
                            if (cur_load_per_procbank[perBankprocRank[i, bank], bank] < cur_load_per_procbank[perBankprocRank[j, bank], bank]) { }
                            else
                            {
                                if (cur_load_per_procbank[perBankprocRank[i, bank], bank] > cur_load_per_procbank[perBankprocRank[j, bank], bank])
                                {
                                    temp = perBankprocRank[i, bank];
                                    perBankprocRank[i, bank] = perBankprocRank[j, bank];
                                    perBankprocRank[j, bank] = temp;
                                }
                            }
                    }
                }
                perBankRank[perBankprocRank[0, bank], bank] = Config.N - 1;
                for (int i = 1; i < Config.N; i++)
                {
                    if ((cur_load_per_procbank[perBankprocRank[i, bank], bank] == cur_load_per_procbank[perBankprocRank[i - 1, bank], bank])
                        && (cur_load_per_procbank[perBankprocRank[i, bank], bank] == cur_load_per_procbank[perBankprocRank[i - 1, bank], bank]))
                    {
                        perBankRank[perBankprocRank[i, bank], bank] = perBankRank[perBankprocRank[i - 1, bank], bank];
                    }
                    else
                    {
                        perBankRank[perBankprocRank[i, bank], bank] = perBankRank[perBankprocRank[i - 1, bank], bank] - 1;
                    }
                }
            }
        }


        protected virtual void computeMarkedPerBankMaxTotRanking()
        {
            int temp;
            /*
            for (int i = 0; i < Simulator.NumberOfProcessors - 1; i++)
            {
                for (int j = i + 1; j < Simulator.NumberOfProcessors; j++)
                {
                    if (currentMaxPerProc[procRank[i]] > currentMaxPerProc[procRank[j]])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else
                        if (currentMaxPerProc[procRank[i]] < currentMaxPerProc[procRank[j]]) { }
                        else
                        {
                            if (currentTotalLoadPerProc[procRank[i]] > currentTotalLoadPerProc[procRank[j]])
                            {
                                temp = procRank[i];
                                procRank[i] = procRank[j];
                                procRank[j] = temp;
                            }
                        }
                }
            }
            Rank[procRank[0]] = Simulator.NumberOfProcessors - 1;
            for (int i = 1; i < Simulator.NumberOfProcessors; i++)
            {
                if ((currentMaxPerProc[procRank[i]] == currentMaxPerProc[procRank[i - 1]])
                    && (currentTotalLoadPerProc[procRank[i]] == currentTotalLoadPerProc[procRank[i - 1]]))
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }
            */

            // per-bank rank computation using per bank max/total values
            for (int bank = 0; bank < Config.memory.bank_max_per_mem; bank++)
            {
                for (int i = 0; i < Config.N - 1; i++)
                {
                    for (int j = i + 1; j < Config.N; j++)
                    {
                        if (thisPeriodMarkedPerProcBank[perBankprocRank[i, bank], bank] > thisPeriodMarkedPerProcBank[perBankprocRank[j, bank], bank])
                        {
                            temp = perBankprocRank[i, bank];
                            perBankprocRank[i, bank] = perBankprocRank[j, bank];
                            perBankprocRank[j, bank] = temp;
                        }
                        else /* this should not be executed with local info */
                            if (thisPeriodMarkedPerProcBank[perBankprocRank[i, bank], bank] < thisPeriodMarkedPerProcBank[perBankprocRank[j, bank], bank]) { }
                            else
                            {
                                if (thisPeriodMarkedPerProcBank[perBankprocRank[i, bank], bank] > thisPeriodMarkedPerProcBank[perBankprocRank[j, bank], bank])
                                {
                                    temp = perBankprocRank[i, bank];
                                    perBankprocRank[i, bank] = perBankprocRank[j, bank];
                                    perBankprocRank[j, bank] = temp;
                                }
                            }
                    }
                }
                perBankRank[perBankprocRank[0, bank], bank] = Config.N - 1;
                for (int i = 1; i < Config.N; i++)
                {
                    if ((thisPeriodMarkedPerProcBank[perBankprocRank[i, bank], bank] == thisPeriodMarkedPerProcBank[perBankprocRank[i - 1, bank], bank])
                        && (thisPeriodMarkedPerProcBank[perBankprocRank[i, bank], bank] == thisPeriodMarkedPerProcBank[perBankprocRank[i - 1, bank], bank]))
                    {
                        perBankRank[perBankprocRank[i, bank], bank] = perBankRank[perBankprocRank[i - 1, bank], bank];
                    }
                    else
                    {
                        perBankRank[perBankprocRank[i, bank], bank] = perBankRank[perBankprocRank[i - 1, bank], bank] - 1;
                    }
                }
            }
        }


        protected virtual void computeTotMaxRanking()
        {
            /* per-bank version not yet implemented */
            int temp;
            for (int i = 0; i < Config.N - 1; i++)
            {
                for (int j = i + 1; j < Config.N; j++)
                {
                    if (cur_load_per_proc[procRank[i]] > cur_load_per_proc[procRank[j]])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else
                        if (cur_load_per_proc[procRank[i]] < cur_load_per_proc[procRank[j]]) { }
                        else
                        {
                            if (cur_max_load_per_proc[procRank[i]] > cur_max_load_per_proc[procRank[j]])
                            {
                                temp = procRank[i];
                                procRank[i] = procRank[j];
                                procRank[j] = temp;
                            }
                        }
                }
            }
            Rank[procRank[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++)
            {
                if ((cur_max_load_per_proc[procRank[i]] == cur_max_load_per_proc[procRank[i - 1]])
                    && (cur_load_per_proc[procRank[i]] == cur_load_per_proc[procRank[i - 1]]))
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else
                {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }
        }

        protected virtual void computeRandomRanking()
        {
            // assign random values
            int temp;
            for (int i = 0; i < Config.N - 1; i++)
            {
                procRank[i] = Simulator.rand.Next(int.MaxValue);
                Rank[i] = i;
            }
            // sort
            for (int i = 0; i < Config.N - 1; i++)
            {
                for (int j = i + 1; j < Config.N; j++)
                {
                    if (procRank[i] > procRank[j])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                        temp = Rank[i];
                        Rank[i] = Rank[j];
                        Rank[j] = temp;
                    }
                }
            }
        }

        // per-bank version of random ranking
        protected virtual void computePerBankRandomRanking()
        {
            // assign random values
            //int temp;
            for (int i = 0; i < Config.N - 1; i++)
            {
                //procRank[i] = Simulator.rand.Next(int.MaxValue);
                //Rank[i] = i;
                for (int bank = 0; bank < Config.memory.bank_max_per_mem; bank++)
                {
                    perBankprocRank[i, bank] = Simulator.rand.Next(int.MaxValue);
                    perBankRank[i, bank] = Simulator.rand.Next(int.MaxValue);
                }
            }
            // sort
            /*
            for (int i = 0; i < Simulator.NumberOfProcessors - 1; i++)
            {
                for (int j = i + 1; j < Simulator.NumberOfProcessors; j++)
                {
                    if (procRank[i] > procRank[j])
                    {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                        temp = Rank[i];
                        Rank[i] = Rank[j];
                        Rank[j] = temp;
                    }
                }
            } 
            */

            // per-bank sort -- sort for each bank
            /*
            for (int bank = 0; bank < Simulator.NumberOfBanks; bank++)
            {
                for (int i = 0; i < Simulator.NumberOfProcessors - 1; i++)
                {
                    for (int j = i + 1; j < Simulator.NumberOfProcessors; j++)
                    {
                        if (perBankprocRank[i, bank] > perBankprocRank[j, bank])
                        {
                            temp = perBankprocRank[i, bank];
                            perBankprocRank[i, bank] = perBankprocRank[j, bank];
                            perBankprocRank[j, bank] = temp;
                            temp = perBankRank[i, bank];
                            perBankRank[i, bank] = perBankRank[j, bank];
                            perBankRank[j, bank] = temp;
                        }
                    }
                }
            }
            */
        }

        protected virtual void computeRoundRobinRanking()
        {
            int temp = procRank[0];
            for (int i = 0; i < Config.N - 1; i++)
            {
                procRank[i] = procRank[i + 1];
            }
            procRank[Config.N - 1] = temp;
            for (int i = 0; i < Config.N - 1; i++)
            {
                Rank[procRank[i]] = i;
            }
        }

        protected virtual void computePerBankRoundRobinRanking()
        {
            for (int bank = 0; bank < Config.memory.bank_max_per_mem; bank++)
            {
                int temp = perBankprocRank[0, bank];
                for (int i = 0; i < Config.N - 1; i++)
                {
                    perBankprocRank[i, bank] = perBankprocRank[i + 1, bank];
                }
                perBankprocRank[Config.N - 1, bank] = temp;
                for (int i = 0; i < Config.N - 1; i++)
                {
                    perBankRank[perBankprocRank[i, bank], bank] = i;
                }
            }
        }

        protected virtual void computePerBankLPRanking()
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
            double[,] p = new double[Config.N, Config.memory.bank_max_per_mem];
            double[] Solution;

            // Make a liner program with x columns and y rows. Columns are the variables.
            // lp = lpsolve.make_lp(x, y);
            // Rows will include the function to minimize or maximize, and the constraints to abide by

            lp = lpsolve.make_lp(0, Config.N); // <--- Constructs the matrix to express the linear program

            lpsolve.set_outputfile(lp, "lpsolve-out.txt");
            // lpsolve.set_maxim(lp); <--- Use this if we want to maximize the objective function

            for (int tt = 0; tt < Config.N; tt++)
            {
                ProcArray[tt] = 1;
            }
            FillArray(out Row, ProcArray);

            lpsolve.print_str(lp, "Set the objective function" + NewLine);
            lpsolve.print_str(lp, "lpsolve.set_obj_fn(lp, ref Row[0]);" + NewLine);
            lpsolve.set_obj_fn(lp, ref Row[0]); // <--- sets our objective function: Minimize sigma Ci
            lpsolve.print_lp(lp);

            // Now add constraints
            // Ci >= 0
            for (int kk = 0; kk < Config.N; kk++)
            {
                for (int tt = 0; tt < Config.N; tt++)
                {
                    // zero out procarray first    
                    ProcArray[tt] = 0;
                }
                ProcArray[kk] = 1;
                FillArray(out Row, ProcArray);
                lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, 0);
                lpsolve.print_lp(lp);
            }

            // Find p values (pij) based on available information ("job length" of thread i in bank j) 
            // --- currently this is the same as # of requests from thread i in bank j
            // if we have limited information about a bank, we will need to change the body of this loop
            for (int tt = 0; tt < Config.N; tt++)
            {
                for (int bb = 0; bb < Config.memory.bank_max_per_mem; bb++)
                {
                    p[tt, bb] = cur_load_per_procbank[tt, bb];
                    //p[tt, bb] = curMarkedPerProcBank[tt, bb];
                }
            }

            // Now add more constraints for each bank
            for (int bb = 0; bb < Config.memory.bank_max_per_mem; bb++)
            {
                lpsolve.print_str(lp, "Adding constraints for Bank " + bb + NewLine);
                // for each thread combination subset
                for (int tcomb = 1; tcomb < (Math.Pow(2, Config.N)); tcomb++)
                {
                    int examined_tcomb = tcomb;
                    int ii;
                    int[] one_hot_threadarray = new int[Config.N];

                    for (int tt = 0; tt < Config.N; tt++)
                    {
                        // zero out procarray first    
                        one_hot_threadarray[tt] = 0;
                        ProcArray[tt] = 0;
                    }

                    ii = 0;
                    while (examined_tcomb > 0)
                    {
                        if ((examined_tcomb & 1) > 0)
                        {
                            one_hot_threadarray[ii] = 1;
                        }
                        examined_tcomb = (examined_tcomb >> 1);
                        ii++;
                    }

                    // compute [sigma pij]^2
                    double squared_sigma_pij = 0;
                    double sigma_pij_squared = 0;
                    double inequality_RHS = 0; // right hand side of >= inequality
                    for (int tt = 0; tt < Config.N; tt++)
                    {
                        if (one_hot_threadarray[tt] > 0)
                        {
                            squared_sigma_pij += p[tt, bb];
                            sigma_pij_squared += p[tt, bb] * p[tt, bb];
                            ProcArray[tt] = p[tt, bb];
                        }
                    }
                    squared_sigma_pij = squared_sigma_pij * squared_sigma_pij;
                    inequality_RHS = 0.5 * (squared_sigma_pij + sigma_pij_squared);

                    FillArray(out Row, ProcArray);
                    lpsolve.print_str(lp, "Adding constraints for Bank " + bb + " tcomb " + tcomb + NewLine);
                    lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, inequality_RHS);
                    lpsolve.print_lp(lp);
                }
            }


            /*int errorcode = (int)*/lpsolve.solve(lp); // <--- Solves the LP
            double solution = lpsolve.get_objective(lp); // <--- Get the value of the solved objective function

            lpsolve.print_str(lp, "PRINTING SOLUTION: " + lpsolve.get_objective(lp) + " " + solution + "\n");

            Solution = new double[lpsolve.get_Ncolumns(lp)];
            lpsolve.get_variables(lp, ref Solution[0]); // <--- These are the values for the variables placed into the Col array



            lpsolve.print_str(lp, "PRINTING VARIABLE VALUES...\n");
            for (int i = 0; i < Solution.Length; i++)
            {
                lpsolve.print_str(lp, Solution[i] + " ");
            }
            lpsolve.print_str(lp, "\n");

            //currentLoadPerProcBank = new int[Simulator.NumberOfProcessors, Simulator.NumberOfBanks];



            ulong LPoptimal_thisBatchCompletionTime = 0;
            ulong LPoptimal_thisBatchNumThreads = 0;

            // Sort the solution to get the final thread ranking
            {
                // assign rank values
                double[] CompletionTimes = new double[Config.N];
                int[] CorrespondingThreads = new int[Config.N];
                double temp;
                int itemp;
                for (int i = 0; i < Config.N; i++)
                {
                    CompletionTimes[i] = Solution[i];
                    CorrespondingThreads[i] = i;

                    if (curMarkedPerProc[i] > 0)
                    {
                        LPoptimal_totalBatchCompletionTime[i] += (ulong)(Solution[i] * (double)Config.memory.row_hit_latency);
                        LPoptimal_numberOfActiveBatches[i]++;

                        LPoptimal_thisBatchCompletionTime += (ulong)(Solution[i] * (double)Config.memory.row_hit_latency);
                        LPoptimal_thisBatchNumThreads++;

                    }
                }

                LPoptimal_TotalACT += (LPoptimal_thisBatchCompletionTime / LPoptimal_thisBatchNumThreads);
                LPoptimal_TotalACTSamples++;


                bool swapped = false;
                do
                {
                    swapped = false;
                    for (int i = 0; i < (Config.N - 1); i++)
                    {
                        if (CompletionTimes[i] < CompletionTimes[i + 1])
                        {
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

                for (int i = 0; i < Config.N; i++)
                {
                    Rank[CorrespondingThreads[i]] = i;
                }

            }


            for (int i = 0; i < Config.N; i++)
            {
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                {
                    perBankRank[i, b] = Rank[i];
                }
            }

            // print the ranking
            lpsolve.print_str(lp, "RANKING: ");
            for (int i = 0; i < Config.N; i++)
            {
                lpsolve.print_str(lp, Rank[i] + " ");
            }
            lpsolve.print_str(lp, "\n");



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
            for (int i = 0; i < Row.Length; i++)
            {
                lpsolve.print_str(lp, Row[i] + " ");
            }
            lpsolve.print_str(lp, "\n");

            lpsolve.set_outputfile(lp, null);

            lpsolve.delete_lp(lp); // <-- delete lp so that we do not leak

            if ((cur_load_per_proc[0] > 5) && (cur_load_per_proc[1] > 2))
            {
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

        protected virtual void computeMarkedPerBankLPRanking()
        {
            // Call lp_solve to get the ranking

            int kValue = Config.memory.k_value;
            const string NewLine = "\n";
            int lp;
            double[] Row;
            //double[] Col;
            //double[] Lower;
            //double[] Upper;
            //double[] Arry;
            double[] ProcArray = new double[Config.N];
            double[,] p = new double[Config.N, Config.memory.bank_max_per_mem];
            double[] Solution;

            // Make a liner program with x columns and y rows. Columns are the variables.
            // lp = lpsolve.make_lp(x, y);
            // Rows will include the function to minimize or maximize, and the constraints to abide by

            lp = lpsolve.make_lp(0, Config.N); // <--- Constructs the matrix to express the linear program

            lpsolve.set_outputfile(lp, "lpsolve-out.txt");
            // lpsolve.set_maxim(lp); <--- Use this if we want to maximize the objective function

            for (int tt = 0; tt < Config.N; tt++)
            {
                ProcArray[tt] = 1;
            }
            FillArray(out Row, ProcArray);

            lpsolve.print_str(lp, "Set the objective function" + NewLine);
            lpsolve.print_str(lp, "lpsolve.set_obj_fn(lp, ref Row[0]);" + NewLine);
            lpsolve.set_obj_fn(lp, ref Row[0]); // <--- sets our objective function: Minimize sigma Ci
            lpsolve.print_lp(lp);

            // Now add constraints
            // Ci >= 0
            for (int kk = 0; kk < Config.N; kk++)
            {
                for (int tt = 0; tt < Config.N; tt++)
                {
                    // zero out procarray first    
                    ProcArray[tt] = 0;
                }
                ProcArray[kk] = 1;
                FillArray(out Row, ProcArray);
                lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, 0);
                lpsolve.print_lp(lp);
            }

            // Find p values (pij) based on available information ("job length" of thread i in bank j) 
            // --- currently this is the same as # of requests from thread i in bank j
            // if we have limited information about a bank, we will need to change the body of this loop

            for (int bb = 0; bb < Config.memory.bank_max_per_mem; bb++)
            {
                for (int tt = 0; tt < Config.N; tt++)
                {
                    p[tt, bb] = thisPeriodMarkedPerProcBank[tt, bb];
                    //             Console.WriteLine(bb + "  -  " + tt + ":  " + p[tt, bb]);
                }
            }

            // in each bank
            for (int bb = 0; bb < Config.memory.bank_max_per_mem; bb++)
            {

                // determine largest k threads
                bool[] amongTopK = new bool[Config.N];
                // TODO: Better implementation: Sort first and then take largest k
                for (int k = 0; k < Config.N - kValue; k++)
                {
                    int maxThread = -1;
                    double maxP = -1;
                    for (int proc = 0; proc < Config.N; proc++)
                    {
                        if (!amongTopK[proc] && p[proc, bb] > maxP)
                        {
                            maxThread = proc;
                            maxP = p[proc, bb];
                        }
                    }
                    amongTopK[maxThread] = true;
                }

                // get average P value for all non-top k threads
                double avgP = 0;
                int nrNonTopKThreads = 0;
                for (int proc = 0; proc < Config.N; proc++)
                {
                    if (!amongTopK[proc])
                    {
                        nrNonTopKThreads++;
                        avgP += p[proc, bb];
                    }
                }
                avgP = avgP / nrNonTopKThreads;

                // redefine all p values. 
                for (int proc = 0; proc < Config.N; proc++)
                {
                    if (!amongTopK[proc])
                    {
                        p[proc, bb] = avgP;
                    }
                    //             Console.WriteLine("XX " + bb + "  -  " + proc + ":  " + p[proc, bb]);
                }
            }

            // Now add more constraints for each bank
            for (int bb = 0; bb < Config.memory.bank_max_per_mem; bb++)
            {
                lpsolve.print_str(lp, "Adding constraints for Bank " + bb + NewLine);
                // for each thread combination subset
                for (int tcomb = 1; tcomb < (Math.Pow(2, Config.N)); tcomb++)
                {
                    int examined_tcomb = tcomb;
                    int ii;
                    int[] one_hot_threadarray = new int[Config.N];

                    for (int tt = 0; tt < Config.N; tt++)
                    {
                        // zero out procarray first    
                        one_hot_threadarray[tt] = 0;
                        ProcArray[tt] = 0;
                    }

                    ii = 0;
                    while (examined_tcomb > 0)
                    {
                        if ((examined_tcomb & 1) > 0)
                        {
                            one_hot_threadarray[ii] = 1;
                        }
                        examined_tcomb = (examined_tcomb >> 1);
                        ii++;
                    }

                    // compute [sigma pij]^2
                    double squared_sigma_pij = 0;
                    double sigma_pij_squared = 0;
                    double inequality_RHS = 0; // right hand side of >= inequality
                    for (int tt = 0; tt < Config.N; tt++)
                    {
                        if (one_hot_threadarray[tt] > 0)
                        {
                            squared_sigma_pij += p[tt, bb];
                            sigma_pij_squared += p[tt, bb] * p[tt, bb];
                            ProcArray[tt] = p[tt, bb];
                        }
                    }
                    squared_sigma_pij = squared_sigma_pij * squared_sigma_pij;
                    inequality_RHS = 0.5 * (squared_sigma_pij + sigma_pij_squared);

                    FillArray(out Row, ProcArray);
                    lpsolve.print_str(lp, "Adding constraints for Bank " + bb + " tcomb " + tcomb + NewLine);
                    lpsolve.add_constraint(lp, ref Row[0], lpsolve.lpsolve_constr_types.GE, inequality_RHS);
                    lpsolve.print_lp(lp);
                }
            }


            /*int errorcode = (int)*/lpsolve.solve(lp); // <--- Solves the LP
            double solution = lpsolve.get_objective(lp); // <--- Get the value of the solved objective function

            lpsolve.print_str(lp, "PRINTING SOLUTION: " + lpsolve.get_objective(lp) + " " + solution + "\n");

            Solution = new double[lpsolve.get_Ncolumns(lp)];
            lpsolve.get_variables(lp, ref Solution[0]); // <--- These are the values for the variables placed into the Col array



            lpsolve.print_str(lp, "PRINTING VARIABLE VALUES...\n");
            for (int i = 0; i < Solution.Length; i++)
            {
                lpsolve.print_str(lp, Solution[i] + " ");
            }
            lpsolve.print_str(lp, "\n");

            //currentLoadPerProcBank = new int[Simulator.NumberOfProcessors, Simulator.NumberOfBanks];



            ulong LPoptimal_thisBatchCompletionTime = 0;
            ulong LPoptimal_thisBatchNumThreads = 0;

            // Sort the solution to get the final thread ranking
            {
                // assign rank values
                double[] CompletionTimes = new double[Config.N];
                int[] CorrespondingThreads = new int[Config.N];
                double temp;
                int itemp;
                for (int i = 0; i < Config.N; i++)
                {
                    CompletionTimes[i] = Solution[i];
                    CorrespondingThreads[i] = i;

                    if (curMarkedPerProc[i] > 0)
                    {
                        LPoptimal_totalBatchCompletionTime[i] += (ulong)(Solution[i] * (double)Config.memory.row_hit_latency);
                        LPoptimal_numberOfActiveBatches[i]++;

                        LPoptimal_thisBatchCompletionTime += (ulong)(Solution[i] * (double)Config.memory.row_hit_latency);
                        LPoptimal_thisBatchNumThreads++;

                    }
                }

                LPoptimal_TotalACT += (LPoptimal_thisBatchCompletionTime / LPoptimal_thisBatchNumThreads);
                LPoptimal_TotalACTSamples++;


                bool swapped = false;
                do
                {
                    swapped = false;
                    for (int i = 0; i < (Config.N - 1); i++)
                    {
                        if (CompletionTimes[i] < CompletionTimes[i + 1])
                        {
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

                for (int i = 0; i < Config.N; i++)
                {
                    Rank[CorrespondingThreads[i]] = i;
                }

            }


            for (int i = 0; i < Config.N; i++)
            {
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                {
                    perBankRank[i, b] = Rank[i];
                }
            }

            // print the ranking
            lpsolve.print_str(lp, "RANKING: ");
            for (int i = 0; i < Config.N; i++)
            {
                lpsolve.print_str(lp, Rank[i] + " ");
            }
            lpsolve.print_str(lp, "\n");



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
            for (int i = 0; i < Row.Length; i++)
            {
                lpsolve.print_str(lp, Row[i] + " ");
            }
            lpsolve.print_str(lp, "\n");

            lpsolve.set_outputfile(lp, null);

            lpsolve.delete_lp(lp); // <-- delete lp so that we do not leak

            if ((cur_load_per_proc[0] > 5) && (cur_load_per_proc[1] > 2))
            {
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
        private static void FillArray(out double[] Arry, double[] v)
        {
            Arry = new double[v.Length + 1];
            for (int i = 0; i < v.Length; i++)
            {
                Arry[i + 1] = v[i];
            }
        } //FillArray
    }

    class PERBANK_FULL_BATCH_MemoryScheduler : PERBANK_BATCH_MemoryScheduler
    {

        protected ulong batchBeginning;

        public PERBANK_FULL_BATCH_MemoryScheduler(int totalSize, Bank[] bank,
            RankAlgo rankingScheme, BatchSchedAlgo withinBatchPriority)
            : base(totalSize, bank, rankingScheme, withinBatchPriority)
        {
            Console.WriteLine("Initialized PERBANK_FULL_BATCH_MemoryScheduler");
        }


        public override void tick()
        {
            base.tick();

            // start a new batch if there are no marked requests left and there is at least 2 requests
            if (markedReqThisBatch == 0 && cur_load > 2)
            {
                avgFullBatchingDuration += (MemCtlr.cycle - batchBeginning);
                batchBeginning = MemCtlr.cycle;
                reMark();
                // recompute ranks.
                recompCount++;
                recomputeRank();
            }
        }

    }


    class EMPTY_SLOT_FULL_BATCH_MemoryScheduler : FULL_BATCH
    {
        public EMPTY_SLOT_FULL_BATCH_MemoryScheduler(int totalSize, Bank[] bank,
            RankAlgo rankingScheme, BatchSchedAlgo withinBatchPriority)
            : base(totalSize, bank, rankingScheme, withinBatchPriority)
        {
            Console.WriteLine("Initialized EMPTY_SLOT_FULL_BATCH_MemoryScheduler");
        }

        public override bool issue_req(MemoryRequest request)
        {
            //if (curMarkedPerProcBank[request.threadID, request.bankIndex] < Simulator.BatchingCap)
            if (cur_period_marked_per_procbank[request.request.requesterID, request.glob_b_index] < Config.memory.batch_cap)
            {
                request.isMarked = true;
                cur_marked_per_procbank[request.request.requesterID, request.glob_b_index]++;
                cur_period_marked_per_procbank[request.request.requesterID, request.glob_b_index]++;
                cur_marked_req++;
                bank[request.glob_b_index].update_marking();
            }
            return base.issue_req(request);
        }
    }



    class STATIC_BATCH_WITH_PRIORITIES_MemoryScheduler : BATCH_WITH_PRIORITIES_MemoryScheduler
    {

        public STATIC_BATCH_WITH_PRIORITIES_MemoryScheduler(int totalSize, Bank[] bank, RankAlgo rankingScheme)
            : base(totalSize, bank, rankingScheme)
        {
            Console.WriteLine("Initialized STATIC_WITH_PRIORITIES_BATCH_MemoryScheduler");
        }


        public override void tick()
        {
            base.tick();

            if (((MemCtlr.cycle % Config.memory.mark_interval) == 0) && (MemCtlr.cycle != 0))
            {
                reMark(findFirstUnbatchedPriority(markCount));
            }


            if (((MemCtlr.cycle % Config.memory.recomp_interval) == 0) && (MemCtlr.cycle != 0))
            {
                recompCount++;
                recomputeRank();
            }
        }
    }

    class PLL_MemoryScheduler : AbstractMemSched
    {

        public PLL_MemoryScheduler(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            Console.WriteLine("Initialized FCFS_MemoryScheduler");
        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;

            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC)
            {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else
            {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0)
                {
                    // Find the highest priority request for this bank
                    if (nextRequest == null) nextRequest = buf[b, 0];
                    for (int j = 0; j < buf_load[b]; j++)
                    {
                        if (!helper.is_PLL(nextRequest, buf[b, j]))
                        {
                            nextRequest = buf[b, j];
                        }
                    }
                }
            }
            //if( nextRequest != null)
            //Console.WriteLine(currentMaxPerProc[nextRequest.threadID]);
            return nextRequest;
        }


    }

    class PURE_PRIORITY_SCHEME_MemoryScheduler : AbstractMemSched
    {

        public PURE_PRIORITY_SCHEME_MemoryScheduler(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            Console.WriteLine("Initialized PURE_PRIORITY_SCHEME_MemoryScheduler");
        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;

            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC)
            {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else
            {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0)
                {
                    // Find the highest priority request for this bank
                    if (nextRequest == null) nextRequest = buf[b, 0];
                    for (int j = 0; j < buf_load[b]; j++)
                    {
                        if (!helper.is_BATCH(nextRequest, buf[b, j]))
                        {
                            nextRequest = buf[b, j];
                        }
                    }
                }
            }
            //if( nextRequest != null)
            //Console.WriteLine(currentMaxPerProc[nextRequest.threadID]);
            return nextRequest;
        }


    }



    /**
     * This model implements the FairMem algorithm 
     */
    class Ideal_MM_MemoryScheduler : AbstractMemSched
    {
        // The following variables are used to implement our algorithm
        protected int[] nrOfSamples;   // how many requests were served of this thread. 
        protected ulong[] realLatency;
        protected ulong[] idealLatency;
        protected ulong[,] currentRowBuffer; // [thread, bank]
        protected ulong timeCounter;   // when this reaches zero, decrease cumulated latencies!

        // variables for statistics
        protected ulong samplePeriods = 0;
        protected double[] chi = new double[Config.N];
        protected double[] chiTotal = new double[Config.N];
        protected ulong[] totalNumberOfSamples = new ulong[Config.N];
        protected ulong frfcfsRule = 0;
        protected ulong fairnessRule = 0;
        protected ulong frfcfsRule_unsampled = 0;
        protected double maxChiEver = 0, minChiEver = double.MaxValue;
        protected int maxChiEverProc = -1, minChiEverProc = -1;


        public Ideal_MM_MemoryScheduler(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            // Initialization of variables. 
            timeCounter = 0;
            currentRowBuffer = new ulong[Config.N, Config.memory.bank_max_per_mem];
            realLatency = new ulong[Config.N];
            idealLatency = new ulong[Config.N];
            nrOfSamples = new int[Config.N];
            for (int i = 0; i < Config.N; i++)
            {
                realLatency[i] = 0;
                idealLatency[i] = 0;
                nrOfSamples[i] = 0;
                for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
                {
                    currentRowBuffer[i, j] = EMPTY_SLOT;
                }
            }
            Console.WriteLine("Initialized Ideal_MM_MemoryScheduler");

        }

        protected ulong[] temp = new ulong[Config.N];

        // MAYBE FIXME: I am sure this can be implemented in a more efficient way!

        public override void tick()
        {
            base.tick();
            timeCounter++;
            if (timeCounter == Config.memory.beta)   // after beta time steps, decrease latencies
            {
                samplePeriods++;
                for (int i = 0; i < Config.N; i++)
                {
                    if (idealLatency[i] > 0) chiTotal[i] += (double)realLatency[i] / (double)idealLatency[i];
                    idealLatency[i] = 0;
                    realLatency[i] = 0;
                    totalNumberOfSamples[i] += (ulong)nrOfSamples[i];
                    nrOfSamples[i] = 0;
                }

                // IMPORTANT: We cannot simply reset the realLatency to 0, because the ideal latencies will come later and that would cause x to be less than 1. 
                // Therefore, we now add the reallatencies of all banks in which there are still outstanding requests. 
                recomputeRealLatenciesOfRequestsInBuffer();
                timeCounter = 0;
            }
            // increase latencies of all threads x bank for which there is at least one request in the buffer!
            for (int i = 0; i < Config.N; i++)
                for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
                {
                    if (getRelevantLoadPerProcBank(i, j) > 0) realLatency[i]++;
                }
        }

        public override void remove_req(MemoryRequest request)
        {
            nrOfSamples[request.request.requesterID]++;
            updateIdealLatencies(request);
            // Remove the request from the buffer. 
            base.remove_req(request);
        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;

            // Compute current values of chi_i and get largest and smallest!
            bool applyFairnessRule = false;
            double minchi = double.MaxValue;
            double maxchi = double.MinValue;
            int maxProc = -1;
            int minProc = -2;
            for (int i = 0; i < Config.N; i++)
            {
                // MENTAL NOTE: Can this be very inaccurate at the beginning, 
                // when idealLatency and realLatency are very small???

                chi[i] = (double)realLatency[i] / (double)idealLatency[i];

                if (chi[i] < 1)
                {
                    //throw new Exception("X is less than 1!");
                    //Console.WriteLine("X is less than 1!!!!!!");
                    chi[i] = 1;
                }

                // Priorities
                if (Config.memory.use_weight > 0)
                {
                    chi[i] = 1 + ((chi[i] - 1) * Config.memory.weight[i]);
                }

                // check whether processor i has at least one outstanding ready request
                bool considerProc = false;
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_load_per_procbank[i, b] > 0)
                    {
                        considerProc = true; break;
                    }
                }

                if (considerProc && nrOfSamples[i] >= Config.memory.sample_min)
                {
                    if (chi[i] < minchi)
                    {
                        minchi = chi[i];
                        minProc = i;
                        if (chi[i] < minChiEver)
                        {
                            minChiEver = chi[i];
                            minChiEverProc = i;
                        }
                    }
                    if (chi[i] > maxchi)
                    {
                        maxchi = chi[i];
                        maxProc = i;
                        if (chi[i] > maxChiEver)
                        {
                            maxChiEver = chi[i];
                            maxChiEverProc = i;
                        }
                    }
                }
            }
            if (minchi != double.MaxValue && maxProc != -1 && maxchi / minchi > Config.memory.alpha)
                applyFairnessRule = true;

            // TODO BUGFIX: 
            // 1) Use different tie-breaker rule  (within and especially across bank)
            // 2) Check whether MaxProc has at least one outstanding request. 
            //    --> Check also MICRO implementation!!!
            // --> DONE! It should work now!


            if (applyFairnessRule)
            {
                // Get the highest priority request according to fairness index! 
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_load_per_procbank[maxProc, b] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = null;
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (buf[b, j].request.requesterID == maxProc)
                            {
                                if (nextBankRequest == null ||
                                    !helper.is_FR_FCFS(nextBankRequest, buf[b, j]))
                                {
                                    nextBankRequest = buf[b, j];
                                }
                            }
                        }
                        if (nextBankRequest == null) throw new Exception("Bank Load is 0");
                        // Compare between highest priority between different banks
                        if (nextRequest == null ||
                            !helper.is_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID]))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
                if (nextRequest == null) throw new Exception("No Request from MaxProc");
            }
            else
            {
                // Get the highest priority request according to FR-FCFS. 
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && buf_load[b] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = buf[b, 0];
                        for (int j = 1; j < buf_load[b]; j++)
                        {
                            if (!helper.is_FR_X_FCFS(nextBankRequest, buf[b, j], chi[nextBankRequest.request.requesterID], chi[buf[b, j].request.requesterID]))
                            {
                                nextBankRequest = buf[b, j];
                            }
                        }
                        // Compare between highest priority between different banks
                        if (nextRequest == null || !helper.is_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID]))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
            }
            // for stats!
            if (nextRequest != null)
            {
                if (applyFairnessRule)
                    fairnessRule++;
                else if (minchi == double.MaxValue || maxProc == -1 || minProc == maxProc)
                    frfcfsRule_unsampled++;
                else
                    frfcfsRule++;
            }

            return nextRequest;
        }


        // IMPORTANT: We cannot simply reset the realLatency to 0, because the ideal latencies will come later and that would cause x to be less than 1. 
        // Therefore, we now add the reallatencies of all banks in which there are still outstanding requests. 
        protected virtual void recomputeRealLatenciesOfRequestsInBuffer()
        {
            for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
            {
                for (int k = 0; k < Config.N; k++) temp[k] = 0;
                for (int k = 0; k < buf_load[j]; k++)
                {
                    if ((MemCtlr.cycle - buf[j, k].timeOfArrival) > temp[buf[j, k].request.requesterID])
                        temp[buf[j, k].request.requesterID] = MemCtlr.cycle - buf[j, k].timeOfArrival;
                    // this is how long the oldest oustanding request has been in the buffer.  
                }
                for (int k = 0; k < Config.N; k++) realLatency[k] += temp[k];
            }
        }

        protected virtual void updateIdealLatencies(MemoryRequest request)
        {
            // Here, update latencies and currentRowBuffers!
            if (currentRowBuffer[request.request.requesterID, request.glob_b_index] == EMPTY_SLOT)
            {   // Row closed. 
                idealLatency[request.request.requesterID] += (ulong)(Config.memory.bus_busy_time + Config.memory.row_closed_latency);
                currentRowBuffer[request.request.requesterID, request.glob_b_index] = request.r_index;
            }
            else if (request.r_index == currentRowBuffer[request.request.requesterID, request.glob_b_index])
            {   // Row hit. 
                idealLatency[request.request.requesterID] += (ulong)(Config.memory.bus_busy_time + Config.memory.row_hit_latency);
            }
            else
            {   // Row conflict. 
                idealLatency[request.request.requesterID] += (ulong)(Config.memory.bus_busy_time + Config.memory.row_conflict_latency);
                currentRowBuffer[request.request.requesterID, request.glob_b_index] = request.r_index;
            }
        }

        protected virtual int getRelevantLoadPerProcBank(int proc, int bank)
        {
            return cur_load_per_procbank[proc, bank];
        }



        public override void report(TextWriter writer)
        {
            base.report(writer);
            writer.WriteLine();
            writer.WriteLine("OUTPUT FOR OUR SCHEDULING ALGORITHM:");
            writer.WriteLine("Mininum X ever: " + minChiEver);
            writer.WriteLine("Processor with minimum X ever: " + minChiEverProc);
            writer.WriteLine("Maximum X ever: " + maxChiEver);
            writer.WriteLine("Processor with maximum X ever: " + maxChiEverProc);
            writer.WriteLine("Requests by Fairness Rule : " + fairnessRule + "    Ratio: " + (double)fairnessRule / (double)(frfcfsRule + frfcfsRule_unsampled + fairnessRule));
            writer.WriteLine("Requests by FR-FCFS Rule : " + frfcfsRule + "    Ratio: " + (double)frfcfsRule / (double)(frfcfsRule + frfcfsRule_unsampled + fairnessRule));
            writer.WriteLine("Requests by FR-FCFS Rule (unsampled) : " + frfcfsRule_unsampled + "    Ratio: " + (double)frfcfsRule_unsampled / (double)(frfcfsRule + frfcfsRule_unsampled + fairnessRule));
            writer.WriteLine("Average X of processors:");
            for (int i = 0; i < Config.N; i++)
            {
                writer.WriteLine("Processor " + i + ":  Samples: " + totalNumberOfSamples[i] + "     AvgX: " + (double)chiTotal[i] / (double)samplePeriods);
            }
            writer.WriteLine("Total Number of Sample Windows: " + samplePeriods);
            writer.WriteLine();
        }

    }

    class Ideal_MM_MemoryScheduler_WB : Ideal_MM_MemoryScheduler
    {
        public Ideal_MM_MemoryScheduler_WB(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            Console.WriteLine("Initialized Ideal_MM_WB_MemoryScheduler");
        }

        // IMPORTANT: We cannot simply reset the realLatency to 0, because the ideal latencies will come later and that would cause x to be less than 1. 
        // Therefore, we now add the reallatencies of all banks in which there are still outstanding requests. 
        protected override void recomputeRealLatenciesOfRequestsInBuffer()
        {
            for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
            {
                for (int k = 0; k < Config.N; k++) temp[k] = 0;
                for (int k = 0; k < buf_load[j]; k++)
                {
                    if (buf[j, k].type != MemoryRequestType.WB &&
                        (MemCtlr.cycle - buf[j, k].timeOfArrival) > temp[buf[j, k].request.requesterID])
                        temp[buf[j, k].request.requesterID] = MemCtlr.cycle - buf[j, k].timeOfArrival;
                    // this is how long the oldest oustanding request has been in the buffer.  
                }
                for (int k = 0; k < Config.N; k++) realLatency[k] += temp[k];
            }
        }

        protected override void updateIdealLatencies(MemoryRequest request)
        {
            if (request.type != MemoryRequestType.WB)
            {
                base.updateIdealLatencies(request);
            }
        }

        /**
      * This is the main function of interest. Another memory scheduler needs to 
      * implement this function in a different way!
      */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;
            bool ScheduleWB = (this.get_wb_fraction() > Config.memory.wb_full_ratio);

            // Compute current values of chi_i and get largest and smallest!
            bool applyFairnessRule = false;
            double minchi = double.MaxValue;
            double maxchi = double.MinValue;
            int maxProc = -1;
            int minProc = -2;
            for (int i = 0; i < Config.N; i++)
            {
                chi[i] = (double)realLatency[i] / (double)idealLatency[i];

                if (chi[i] < 1)
                {
                    //throw new Exception("X is less than 1!");
                    //Console.WriteLine("X is less than 1!!!!!!");
                    chi[i] = 1;
                }

                // Priorities
                if (Config.memory.use_weight > 0)
                {
                    chi[i] = 1 + ((chi[i] - 1) * Config.memory.weight[i]);
                }

                // check whether processor i has at least one outstanding ready request
                bool considerProc = false;
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_nonwb_per_procbank[i, b] > 0)
                    {
                        considerProc = true; break;
                    }
                }

                if (considerProc && nrOfSamples[i] >= Config.memory.sample_min)
                {
                    if (chi[i] < minchi)
                    {
                        minchi = chi[i];
                        minProc = i;
                        if (chi[i] < minChiEver)
                        {
                            minChiEver = chi[i];
                            minChiEverProc = i;
                        }
                    }
                    if (chi[i] > maxchi)
                    {
                        maxchi = chi[i];
                        maxProc = i;
                        if (chi[i] > maxChiEver)
                        {
                            maxChiEver = chi[i];
                            maxChiEverProc = i;
                        }
                    }
                }
            }
            if (minchi != double.MaxValue && maxProc != -1 && maxchi / minchi > Config.memory.alpha)
                applyFairnessRule = true;

            if (applyFairnessRule)
            {
                // Get the highest priority request according to fairness index! 
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_load_per_procbank[maxProc, b] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = null;
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (buf[b, j].request.requesterID == maxProc)
                            {
                                if (nextBankRequest == null ||
                                    (ScheduleWB && !helper.is_FR_FCFS(nextBankRequest, buf[b, j])) ||
                                    (!ScheduleWB && !helper.is_NONWB_FR_FCFS(nextBankRequest, buf[b, j])))
                                {
                                    nextBankRequest = buf[b, j];
                                }
                            }
                        }
                        if (nextBankRequest == null) throw new Exception("Bank Load is 0");
                        // Compare between highest priority between different banks
                        if (nextRequest == null ||
                            (ScheduleWB && !helper.is_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID])) ||
                            (!ScheduleWB && !helper.is_NONWB_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID])))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
                if (nextRequest == null) throw new Exception("No Request from MaxProc");
            }
            else
            {
                // Get the highest priority request according to FR-FCFS. 
                for (int i = 0; i < Config.memory.bank_max_per_mem; i++)  // for each bank
                {
                    if (bank[i].is_ready() && buf_load[i] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = buf[i, 0];
                        for (int j = 1; j < buf_load[i]; j++)
                        {
                            if ((ScheduleWB && !helper.is_FR_FCFS(nextBankRequest, buf[i, j])) ||
                                (!ScheduleWB && !helper.is_NONWB_FR_FCFS(nextBankRequest, buf[i, j])))

                            //         if ((ScheduleWB && !higherFRFCFSPriority(nextBankRequest, buffer[i, j], chi[nextBankRequest.threadID], chi[buffer[i, j].threadID])) ||
                            //             (!ScheduleWB && !higherFRFCFSPriorityWB(nextBankRequest, buffer[i, j], chi[nextBankRequest.threadID], chi[buffer[i, j].threadID])))
                            {
                                nextBankRequest = buf[i, j];
                            }
                        }
                        // Compare between highest priority between different banks
                        if (nextRequest == null ||
                            (ScheduleWB && !helper.is_FCFS(nextRequest, nextBankRequest)) ||
                            (!ScheduleWB && !helper.is_NONWB_FCFS(nextRequest, nextBankRequest)))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
            }

            // for stats!
            if (nextRequest != null)
            {
                //bool isRowHit = (nextRequest.r_index == bank[nextRequest.glob_b_index].get_cur_row());
                if (applyFairnessRule)
                    fairnessRule++;
                else if (minchi == double.MaxValue || maxProc == -1 || minProc == maxProc)
                    frfcfsRule_unsampled++;
                else
                    frfcfsRule++;
            }

            // assert!
            if (nextRequest == null)
            {
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)  // for each bank
                {
                    if (bank[b].is_ready() && buf_load[b] > 0)
                        throw new Exception("No Request from MaxProc");
                }
            }

            return nextRequest;
        }


        protected override int getRelevantLoadPerProcBank(int proc, int bank)
        {
            return cur_nonwb_per_procbank[proc, bank];
        }

    }

    class Ideal_MICRO_MemoryScheduler : AbstractMemSched
    {
        // The following variables are used to implement our algorithm
        protected ulong[] stallShared;
        protected ulong[] stallAlone;
        protected ulong[] stallDelta;
        protected ulong[,] currentRowBuffer; // [thread, bank]

        // DO I NEED THIS?
        protected ulong timeCounter;   // when this reaches zero, decrease cumulated latencies!

        // variables for statistics
        protected ulong samplePeriods = 0;
        protected double[] chi = new double[Config.N];
        protected double[] chiTotal = new double[Config.N];
        protected ulong[] totalNumberOfSamples = new ulong[Config.N];
        protected ulong frfcfsRule = 0;
        protected ulong fairnessRule = 0;
        protected ulong frfcfsRule_unsampled = 0;
        protected double maxChiEver = 0, minChiEver = double.MaxValue;
        protected int maxChiEverProc = -1, minChiEverProc = -1;

        private ulong[] totParallelism;
        private ulong[] totParallelismSamples;
        private int[] maxParallelism;
        protected ulong[] totExecParallelism;
        protected ulong[] totExecParallelismSamples;
        protected int[] maxExecParallelism;


        public Ideal_MICRO_MemoryScheduler(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            // Initialization of variables. 
            timeCounter = 0;
            currentRowBuffer = new ulong[Config.N, Config.memory.bank_max_per_mem];
            stallShared = new ulong[Config.N];
            stallAlone = new ulong[Config.N];
            stallDelta = new ulong[Config.N];
            totParallelism = new ulong[Config.N];
            totParallelismSamples = new ulong[Config.N];
            maxParallelism = new int[Config.N];
            totExecParallelism = new ulong[Config.N];
            totExecParallelismSamples = new ulong[Config.N];
            maxExecParallelism = new int[Config.N];
            for (int i = 0; i < Config.N; i++)
            {
                stallShared[i] = 5 * (ulong)Config.memory.row_conflict_latency;
                for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
                {
                    currentRowBuffer[i, j] = EMPTY_SLOT;
                }
            }
            Console.WriteLine("Initialized Ideal_MICRO_MemoryScheduler");
        }

        // MAYBE FIXME: I am sure this can be implemented in a more efficient way!
        public override void tick()
        {
            base.tick();
            timeCounter++;
            if (timeCounter == Config.memory.beta)   // after beta time steps, decrease latencies
            {
                samplePeriods++;
                for (int n = 0; n < Config.N; n++)
                {
                    stallAlone[n] = stallShared[n] - stallDelta[n];
                    chiTotal[n] += (double)stallShared[n] / (double)stallAlone[n];
                    // TODO: How to incorporate the beta policy?!
                    stallShared[n] = 5 * (ulong)Config.memory.row_conflict_latency;
                    stallAlone[n] = 0;
                    stallDelta[n] = 0;
                }
                timeCounter = 0;
            }
        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest nextRequest = selectHighestPriorityRequest(mem);

            // Update all stalledDelta if nextRequest != null! 
            if (nextRequest != null)
            {
                // 1. add bus latency to all threads that aren't scheduled
                updateOthersBusStallDelta(nextRequest.request.requesterID);
                // 2. add bank latency to all threads in the bank that aren't scheduled
                updateOthersStallDelta(nextRequest.request.requesterID, nextRequest.glob_b_index, nextRequest.r_index);
                // 3. add latency to the scheduled thread IF it's a conflict, but it would have been a hit
                // 3. Now, update own stallDelta if necessary!
                // Also, update currentRowBuffers. 
                updateOwnStallDelta(nextRequest.request.requesterID, nextRequest.glob_b_index, nextRequest.r_index);

            }
            return nextRequest;
        }


        /**
         * Returns the highest priority request, but does not do any updates. 
         */
        protected virtual MemoryRequest selectHighestPriorityRequest(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;
            for (int n = 0; n < Config.N; n++)
                stallShared[n] += Simulator.network.nodes[n].cpu.get_stalledSharedDelta();

            // Compute current values of chi_i and get largest and smallest!
            bool applyFairnessRule = false;
            double minchi = double.MaxValue;
            double maxchi = double.MinValue;
            int maxProc = -1;
            int minProc = -2;
            for (int i = 0; i < Config.N; i++)
            {
                stallAlone[i] = stallShared[i] - stallDelta[i];
                chi[i] = (double)stallShared[i] / (double)stallAlone[i];
                if (chi[i] < 1)
                {
                    //throw new Exception("X is less than 1!");
                    //Console.WriteLine("X is less than 1!!!!!!");
                    chi[i] = 1;
                }
                // Priorities
                if (Config.memory.use_weight > 0)
                {
                    chi[i] = 1 + ((chi[i] - 1) * Config.memory.weight[i]);
                }

                // check whether processor i has at least one outstanding ready request
                bool considerProc = false;
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_load_per_procbank[i, b] > 0)
                    {
                        considerProc = true; break;
                    }
                }

                if (considerProc)
                {
                    if (chi[i] < minchi)
                    {
                        minchi = chi[i];
                        minProc = i;
                        if (chi[i] < minChiEver)
                        {
                            minChiEver = chi[i];
                            minChiEverProc = i;
                        }
                    }
                    if (chi[i] > maxchi)
                    {
                        maxchi = chi[i];
                        maxProc = i;
                        if (chi[i] > maxChiEver)
                        {
                            maxChiEver = chi[i];
                            maxChiEverProc = i;
                        }
                    }
                }
            }
            if (minProc != -2 && maxProc != -1 && maxchi / minchi > Config.memory.alpha)
                applyFairnessRule = true;

            if (applyFairnessRule)
            {
                // Get the highest priority request according to fairness index! 
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_load_per_procbank[maxProc, b] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = null;
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (buf[b, j].request.requesterID == maxProc)
                            {
                                if (nextBankRequest == null ||
                                    !helper.is_FR_FCFS(nextBankRequest, buf[b, j]))
                                {
                                    nextBankRequest = buf[b, j];
                                }
                            }
                        }
                        if (nextBankRequest == null) throw new Exception("Bank Load is 0");
                        // Compare between highest priority between different banks
                        if (nextRequest == null || !helper.is_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID]))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
                if (nextRequest == null) throw new Exception("No Request from MaxProc");
            }
            else
            {
                // Get the highest priority request according to FR-FCFS. 
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && buf_load[b] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = buf[b, 0];
                        for (int j = 1; j < buf_load[b]; j++)
                        {
                            if (!helper.is_FR_X_FCFS(nextBankRequest, buf[b, j], chi[nextBankRequest.request.requesterID], chi[buf[b, j].request.requesterID]))
                            {
                                nextBankRequest = buf[b, j];
                            }
                        }
                        // Compare between highest priority between different banks
                        if (nextRequest == null || !helper.is_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID]))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
            }

            // for stats!
            if (nextRequest != null)
            {
                if (applyFairnessRule)
                    fairnessRule++;
                else if (minchi == double.MaxValue || maxProc == -1 || minProc == maxProc)
                    frfcfsRule_unsampled++;
                else
                    frfcfsRule++;
            }
            return nextRequest;
        }

        /**
         * Updates the sequential Bus Stall Delta of all threads other than id, when a request of thread id
         * is scheduled. 
         */
        protected virtual void updateOthersBusStallDelta(int id)
        {
            for (int i = 0; i < Config.N; i++)
            {
                if (i != id)
                {
                    for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                    {
                        if (bank[b].is_ready() && getRelevantLoadPerProcBank(i, b) > 0)
                            stallDelta[i] += (ulong)Config.memory.bus_busy_time;
                    }
                }
            }
        }

        /**
         * Updates the Stall Delta of all threads other than id, when a request of thread id
         * is scheduled. 
         */
        protected virtual void updateOthersStallDelta(int id, int bankIndex, ulong rowIndex)
        {
            ulong stallTime;
            if (rowIndex == bank[bankIndex].get_cur_row())
                stallTime = (ulong)Config.memory.row_hit_latency;
            else
                stallTime = (ulong)Config.memory.row_conflict_latency;

            for (int i = 0; i < Config.N; i++)
            {
                if (i != id && getRelevantLoadPerProcBank(i, bankIndex) > 0)
                {
                    int nrOfDiffBanks = 0;
                    // add stall time divided by the number of parallel threads in the buffer
                    if (Config.memory.ignore_paral > 0)
                        stallDelta[i] += (ulong)stallTime;
                    else
                    {
                        for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
                            if (getRelevantLoadPerProcBank(i, j) > 0)
                                nrOfDiffBanks++;

                        if (nrOfDiffBanks >= 1)
                            stallDelta[i] += (ulong)(stallTime / (1 + (Config.memory.paral_factor * ((ulong)nrOfDiffBanks) - 1)));
                        else
                            stallDelta[i] += (ulong)stallTime;
                    }
                    totParallelism[i] += (ulong)nrOfDiffBanks;
                    totParallelismSamples[i]++;
                    if (nrOfDiffBanks > maxParallelism[i]) maxParallelism[i] = nrOfDiffBanks;
                }
            }

        }

        /**
         * Updates the Stall time of the thread whose request is currently scheduled
         */
        protected virtual void updateOwnStallDelta(int id, int bankIndex, ulong rowIndex)
        {

            bool isRowHit = (rowIndex == bank[bankIndex].get_cur_row());

            if (currentRowBuffer[id, bankIndex] == EMPTY_SLOT)
            {   // Would have been a row closed if the thread had run alone. 
                currentRowBuffer[id, bankIndex] = rowIndex;
                if (isRowHit)
                    stallDelta[id] -= (ulong)(Config.memory.row_closed_latency - Config.memory.row_hit_latency);
                else
                    stallDelta[id] += (ulong)(Config.memory.row_conflict_latency - Config.memory.row_closed_latency);
            }

            // NOTE: 
            // The stallDelta[id] should also be divided by the parallelism!!! 
            // Check whether this is a too optimistic approximation or not. 

            else if (currentRowBuffer[id, bankIndex] == rowIndex)
            {   // Would have been a row hit if the thread had run alone. 
                if (!isRowHit)
                {
                    // check how many requests of this thread are currently executing. 
                    int exectionParallelism = 1;

                    if (Config.memory.ignore_paral > 1)
                        stallDelta[id] += (ulong)(Config.memory.row_conflict_latency - Config.memory.row_hit_latency);
                    else
                    {
                        for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                        {
                            if (!bank[b].is_ready() && bank[b].get_cur_req().request.requesterID == id) exectionParallelism++;
                        }
                        stallDelta[id] += (ulong)((Config.memory.row_conflict_latency - Config.memory.row_hit_latency) / exectionParallelism);
                    }
                    // for stats
                    totExecParallelismSamples[id]++;
                    totExecParallelism[id] += (ulong)exectionParallelism;
                    if (exectionParallelism > maxExecParallelism[id]) maxExecParallelism[id] = exectionParallelism;
                }
            }
            else
            {   // Would have been a row conflict if the thread had run alone. 
                currentRowBuffer[id, bankIndex] = rowIndex;
                if (isRowHit)
                    stallDelta[id] -= (ulong)(Config.memory.row_conflict_latency - Config.memory.row_hit_latency);
            }
        }

        /**
         * These functions can be overridden to return the appropriate counter
         */
        protected virtual int getRelevantLoadPerProc(int proc)
        {
            return cur_load_per_proc[proc];
        }

        protected virtual int getRelevantLoadPerProcBank(int proc, int bank)
        {
            return cur_load_per_procbank[proc, bank];
        }

        public override void report(TextWriter writer)
        {
            base.report(writer);
            writer.WriteLine();
            writer.WriteLine("OUTPUT FOR MICRO SCHEDULING ALGORITHM:");
            writer.WriteLine("Mininum X ever: " + minChiEver);
            writer.WriteLine("Processor with minimum X ever: " + minChiEverProc);
            writer.WriteLine("Maximum X ever: " + maxChiEver);
            writer.WriteLine("Processor with maximum X ever: " + maxChiEverProc);
            writer.WriteLine("Requests by Fairness Rule : " + fairnessRule + "    Ratio: " + (double)fairnessRule / (double)(frfcfsRule + frfcfsRule_unsampled + fairnessRule));
            writer.WriteLine("Requests by FR-FCFS Rule : " + frfcfsRule + "    Ratio: " + (double)frfcfsRule / (double)(frfcfsRule + frfcfsRule_unsampled + fairnessRule));
            writer.WriteLine("Requests by FR-FCFS Rule (unsampled) : " + frfcfsRule_unsampled + "    Ratio: " + (double)frfcfsRule_unsampled / (double)(frfcfsRule + frfcfsRule_unsampled + fairnessRule));
            writer.WriteLine("Average X of processors:");
            for (int i = 0; i < Config.N; i++)
            {
                writer.WriteLine("Processor " + i + ":  Samples: " + totalNumberOfSamples[i] + "     AvgX: " + (double)chiTotal[i] / (double)samplePeriods);
            }
            writer.WriteLine("Total Number of Sample Windows: " + samplePeriods);
            writer.WriteLine("Average Parallelism:");
            for (int i = 0; i < Config.N; i++)
            {
                writer.WriteLine("Processor " + i + ":  Samples: " + totParallelismSamples[i] + "     Avg: " + (double)totParallelism[i] / (double)totParallelismSamples[i] + "  Maximum: " + maxParallelism[i]);
            }
            writer.WriteLine("Average Current Execution Parallelism:");
            for (int i = 0; i < Config.N; i++)
            {
                writer.WriteLine("Processor " + i + ":  Samples: " + totExecParallelismSamples[i] + "     Avg: " + (double)totExecParallelism[i] / (double)totExecParallelismSamples[i] + "  Maximum: " + maxExecParallelism[i]);
            }
            writer.WriteLine();
        }

    }

    class Ideal_MICRO_MemoryScheduler_WB : Ideal_MICRO_MemoryScheduler
    {
        public Ideal_MICRO_MemoryScheduler_WB(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            // Initialization of variables. 
            Console.WriteLine("Initialized Ideal_MICRO_WB_MemoryScheduler");
        }

        /**
         * Updates the Stall time of the thread whose request is currently scheduled
         */
        protected override void updateOwnStallDelta(int id, int bankIndex, ulong rowIndex)
        {
            bool isRowHit = (rowIndex == bank[bankIndex].get_cur_row());
            bool update = (cur_nonwb_per_procbank[id, bankIndex] > 0);

            if (currentRowBuffer[id, bankIndex] == EMPTY_SLOT)
            {   // Would have been a row closed if the thread had run alone. 
                currentRowBuffer[id, bankIndex] = rowIndex;
                if (isRowHit)
                    if (update) stallDelta[id] -= (ulong)(Config.memory.row_closed_latency - Config.memory.row_hit_latency);
                    else
                        if (update) stallDelta[id] += (ulong)(Config.memory.row_conflict_latency - Config.memory.row_closed_latency);
            }
            else if (currentRowBuffer[id, bankIndex] == rowIndex)
            {   // Would have been a row hit if the thread had run alone. 
                if (!isRowHit && update)
                {
                    // check how many requests of this thread are currently executing. 
                    int exectionParallelism = 1;

                    if (Config.memory.ignore_paral > 1)
                    {
                        stallDelta[id] += (ulong)(Config.memory.row_conflict_latency - Config.memory.row_hit_latency);
                    }
                    else
                    {
                        for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                        {
                            if (!bank[b].is_ready() && bank[b].get_cur_req().request.requesterID == id)
                            {
                                exectionParallelism++;
                            }
                        }
                        stallDelta[id] += (ulong)((Config.memory.row_conflict_latency - Config.memory.row_hit_latency) / exectionParallelism);
                    }
                    totExecParallelismSamples[id]++;
                    totExecParallelism[id] += (ulong)exectionParallelism;
                    if (exectionParallelism > maxExecParallelism[id]) maxExecParallelism[id] = exectionParallelism;

                }
            }
            else
            {   // Would have been a row conflict if the thread had run alone. 
                currentRowBuffer[id, bankIndex] = rowIndex;
                if (isRowHit)
                    if (update) stallDelta[id] -= (ulong)(Config.memory.row_conflict_latency - Config.memory.row_hit_latency);
            }

        }

        /**
        * Returns the highest priority request, but does not do any updates. 
        */
        protected override MemoryRequest selectHighestPriorityRequest(MemCtlr mem)
        {
            MemoryRequest nextRequest = null;
            bool ScheduleWB = (this.get_wb_fraction() > Config.memory.wb_full_ratio);

            for (int n = 0; n < Config.N; n++)
            {
                stallShared[n] += Simulator.network.nodes[n].cpu.get_stalledSharedDelta();
            }

            // Compute current values of chi_i and get largest and smallest!
            bool applyFairnessRule = false;
            double minchi = double.MaxValue;
            double maxchi = double.MinValue;
            int maxProc = -1;
            int minProc = -2;
            for (int i = 0; i < Config.N; i++)
            {
                stallAlone[i] = stallShared[i] - stallDelta[i];
                chi[i] = (double)stallShared[i] / (double)stallAlone[i];
                if (chi[i] < 1)
                {
                    //throw new Exception("X is less than 1!");
                    //Console.WriteLine("X is less than 1!!!!!!");
                    chi[i] = 1;
                }

                // Priorities
                if (Config.memory.use_weight > 0)
                {
                    chi[i] = 1 + ((chi[i] - 1) * Config.memory.weight[i]);
                }

                // just checking whether currentLoadPerProc > 0 is not enough, 
                // because all the banks may be busy!!!!
                bool considerProc = false;
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_nonwb_per_procbank[i, b] > 0)
                    {
                        considerProc = true; break;
                    }
                }

                if (considerProc)
                {
                    if (chi[i] < minchi)
                    {
                        minchi = chi[i];
                        minProc = i;
                        if (chi[i] < minChiEver)
                        {
                            minChiEver = chi[i];
                            minChiEverProc = i;
                        }
                    }
                    if (chi[i] > maxchi)
                    {
                        maxchi = chi[i];
                        maxProc = i;
                        if (chi[i] > maxChiEver)
                        {
                            maxChiEver = chi[i];
                            maxChiEverProc = i;
                        }
                    }
                }
            }
            if (minProc != -2 && maxProc != -1 && maxchi / minchi > Config.memory.alpha)
                applyFairnessRule = true;

            if (applyFairnessRule)
            {
                // Get the highest priority request according to fairness index! 
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && cur_load_per_procbank[maxProc, b] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = null;
                        for (int j = 0; j < buf_load[b]; j++)
                        {
                            if (buf[b, j].request.requesterID == maxProc)
                            {
                                if (nextBankRequest == null ||
                                 (ScheduleWB && !helper.is_FR_FCFS(nextBankRequest, buf[b, j])) ||
                                 (!ScheduleWB && !helper.is_NONWB_FR_FCFS(nextBankRequest, buf[b, j])))
                                {
                                    nextBankRequest = buf[b, j];
                                }
                            }
                        }
                        if (nextBankRequest == null) throw new Exception("Bank Load is 0");
                        // Compare between highest priority between different banks
                        if (nextRequest == null ||
                          (ScheduleWB && !helper.is_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID])) ||
                          (!ScheduleWB && !helper.is_NONWB_X_FCFS(nextRequest, nextBankRequest, chi[nextRequest.request.requesterID], chi[nextBankRequest.request.requesterID])))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
                if (nextRequest == null) throw new Exception("No Request from MaxProc");
            }
            else
            {
                // Get the highest priority request according to FR-FCFS. 
                // Get the highest priority request.
                int bank_index;
                if (Config.memory.is_shared_MC)
                {
                    bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
                }
                else
                {
                    bank_index = 0;
                }
                for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    if (bank[b].is_ready() && buf_load[b] > 0) // now I have do check nonetheless!
                    {
                        // Find the highest priority request for this bank
                        MemoryRequest nextBankRequest = buf[b, 0];
                        for (int j = 1; j < buf_load[b]; j++)
                        {
                            if ((ScheduleWB && !helper.is_FR_FCFS(nextBankRequest, buf[b, j])) ||
                              (!ScheduleWB && !helper.is_NONWB_FR_FCFS(nextBankRequest, buf[b, j])))
                            {
                                nextBankRequest = buf[b, j];
                            }
                        }
                        // Compare between highest priority between different banks
                        if (nextRequest == null ||
                         (ScheduleWB && !helper.is_FCFS(nextRequest, nextBankRequest)) ||
                         (!ScheduleWB && !helper.is_NONWB_FCFS(nextRequest, nextBankRequest)))
                        {
                            nextRequest = nextBankRequest;
                        }
                    }
                }
            }
            // for stats!
            if (nextRequest != null)
            {
                if (applyFairnessRule)
                    fairnessRule++;
                else if (minchi == double.MaxValue || maxProc == -1 || minProc == maxProc)
                    frfcfsRule_unsampled++;
                else
                    frfcfsRule++;
            }

            // assert!
            if (nextRequest == null)
            {
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)  // for each bank
                {
                    if (bank[b].is_ready() && buf_load[b] > 0)
                        throw new Exception("No Request from MaxProc");
                }
            }

            return nextRequest;
        }

        /**
        * These functions can be overridden to return the appropriate counter
        */
        protected override int getRelevantLoadPerProc(int proc)
        {
            return cur_nonwb_per_proc[proc];
        }

        protected override int getRelevantLoadPerProcBank(int proc, int bank)
        {
            return cur_nonwb_per_procbank[proc, bank];
        }

    }

    /**
     * Basic Nesbit without priority inversion scheme!
     */
    class Nesbit_Basic_MemoryScheduler : AbstractMemSched
    {
        protected ulong[,] vtmsBankFinishTime;   // per thread x bank
        protected ulong[] vtmsBusFinishTime; // per thread
        protected ulong[] oldestArrivalTime; // per thread

        private ulong[,] toaQueue;   // a queue storing the arrival times of all current threads 
        private int[] oldest, youngest; // pointer per thread to the oldest and youngest entry in toaQueue
        int queueSize;


        public Nesbit_Basic_MemoryScheduler(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            queueSize = 10 * totalSize;
            // Nesbit data structures
            vtmsBankFinishTime = new ulong[Config.N, Config.memory.bank_max_per_mem];
            vtmsBusFinishTime = new ulong[Config.N];
            oldestArrivalTime = new ulong[Config.N];
            toaQueue = new ulong[Config.N, queueSize];
            for (int i = 0; i < Config.N; i++)
            {
                oldestArrivalTime[i] = EMPTY_SLOT;
                for (int j = 0; j < queueSize; j++)
                {
                    toaQueue[i, j] = EMPTY_SLOT;
                }
            }
            oldest = new int[Config.N];
            youngest = new int[Config.N];
            Console.WriteLine("Initialized Nesbit_Basic_MemoryScheduler");
        }

        /**
         * Updates the Time-of-arrival queue and calls the base function
         */
        public override bool issue_req(MemoryRequest request)
        {
            bool success = base.issue_req(request);

            if (success)
            {
                // if it is the first request in the buffer issued by this thread, adjust arrivalTime
                if (oldestArrivalTime[request.request.requesterID] == EMPTY_SLOT)
                    oldestArrivalTime[request.request.requesterID] = request.timeOfArrival;

                // update queue
                toaQueue[request.request.requesterID, youngest[request.request.requesterID]] = request.timeOfArrival;
                youngest[request.request.requesterID]++;
                //     Console.WriteLine(Memory.memoryTime + "   Issue: " + youngest[request.threadID] + "   " + oldest[request.threadID] + "  " + request);
                if (youngest[request.request.requesterID] == queueSize) youngest[request.request.requesterID] = 0;
            }
            return success;
        }


        /**
         * Selection procedure
         */
        protected void updateVMTSRegisters(MemoryRequest nextRequest)
        {
            int thread = nextRequest.request.requesterID;
            int bk = nextRequest.glob_b_index;

            // first update the oldest virtual arrival time!
            int cur = oldest[thread];
            int sentinel = oldest[thread] - 1;
            if (sentinel == -1) sentinel = queueSize - 1;
            while (nextRequest.timeOfArrival != toaQueue[thread, cur] && cur != sentinel)
            {
                cur++;
                if (cur == queueSize) cur = 0;
            }
            if (nextRequest.timeOfArrival != toaQueue[thread, cur])
                throw new Exception("Time-of-Arrival Queue has been corrupted. Entry is missing.");

            oldestArrivalTime[thread] = toaQueue[thread, oldest[thread]];

            /////// Reorder this after the recording of oldestArrivalTime[thread] = toaQueue[thread, oldest[thread]];
            toaQueue[thread, cur] = EMPTY_SLOT;
            //      Console.WriteLine(Memory.memoryTime + "   Remove1: " + youngest[thread] + "   " + oldest[thread] + "  " + nextRequest);

            while (toaQueue[thread, oldest[thread]] == EMPTY_SLOT && oldest[thread] != youngest[thread])
            {
                oldest[thread]++;
                if (oldest[thread] == queueSize) oldest[thread] = 0;
            }
            //       Console.WriteLine(Memory.memoryTime + "   Remove2: " + youngest[thread] + "   " + oldest[thread]);
            /////// End of reordering

            // now update the vtms registers
            int serviceTime = Config.memory.row_conflict_latency;
            if (nextRequest.r_index == bank[bk].get_cur_row()) serviceTime = Config.memory.row_hit_latency;

            // TODO: THIS IS ONLY TRUE IF PRIORITIES ARE EQUAL!!!
            if (Config.memory.use_weight > 0)
            {
                vtmsBankFinishTime[thread, bk] =
                    Math.Max(oldestArrivalTime[thread], vtmsBankFinishTime[thread, bk]) + (ulong)((double)serviceTime * (double)(1.0 / Config.memory.weight[thread]));
                vtmsBusFinishTime[thread] =
                    Math.Max(vtmsBankFinishTime[thread, bk], vtmsBusFinishTime[thread]) + (ulong)((double)Config.memory.bus_busy_time * (double)(1.0 / Config.memory.weight[thread]));
            }
            else
            {
                vtmsBankFinishTime[thread, bk] =
                    Math.Max(oldestArrivalTime[thread], vtmsBankFinishTime[thread, bk]) + (ulong)(serviceTime * Config.N);
                vtmsBusFinishTime[thread] =
                    Math.Max(vtmsBankFinishTime[thread, bk], vtmsBusFinishTime[thread]) + (ulong)(Config.memory.bus_busy_time * Config.N);
            }
            // ONUR
            //System.Console.WriteLine("T: " + thread + " B: " + bk + " Bank: " + vtmsBankFinishTime[thread, bk] + " Bus: " + vtmsBusFinishTime[thread] + " R0:" + currentNonWBPerProcBank[0,0] + " R1:" + currentNonWBPerProcBank[1,0] + " A0: " + currentLoadPerProcBank[0, 0] + " A1: " + currentLoadPerProcBank[1, 0]);

        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way! 
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            // In the Nesbit scheme, this function has to do three things: 
            // 1) Get the next request to be scheduled
            // 2) If there is a next request, update the data structures
            // 3) Return the next request
            MemoryRequest nextRequest = null;
            ulong nextVFT = 0;

            // 1. Get the highest priority request, if there is one. 
            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC)
            {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else
            {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0)
                {
                    // Find the highest priority request for this bank
                    MemoryRequest nextBankRequest = buf[b, 0];
                    ulong nextBankVFT = computeVirtualFinishTime(nextBankRequest);
                    for (int j = 1; j < buf_load[b]; j++)
                    {
                        ulong curBankVFT = computeVirtualFinishTime(buf[b, j]);
                        if (!higherNesbitPriority(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT))
                        {
                            nextBankRequest = buf[b, j];
                            nextBankVFT = curBankVFT;
                        }
                    }
                    // Compare between highest priority between different banks
                    if (nextRequest == null || !higherNesbitPriority(nextRequest, nextBankRequest, nextVFT, nextBankVFT))
                    {
                        nextRequest = nextBankRequest;
                        nextVFT = nextBankVFT;
                    }
                }
            }

            //2. Update the data structures
            if (nextRequest != null) updateVMTSRegisters(nextRequest);

            // 3. Return the request. 
            return nextRequest;
        }

        protected virtual ulong computeVirtualFinishTime(MemoryRequest r)
        {
            int serviceTime = Config.memory.row_conflict_latency;
            if (r.r_index == bank[r.glob_b_index].get_cur_row()) serviceTime = Config.memory.row_hit_latency;

            if (Config.memory.use_weight > 0)
            {
                return Math.Max(
                        Math.Max(
                            oldestArrivalTime[r.request.requesterID],
                            vtmsBankFinishTime[r.request.requesterID, r.glob_b_index]
                        ) + (ulong)((double)serviceTime * (double)(1.0 / Config.memory.weight[r.request.requesterID])),
                        vtmsBusFinishTime[r.request.requesterID]
                    ) + (ulong)((double)Config.memory.bus_busy_time * (double)(1.0 / Config.memory.weight[r.request.requesterID]));
            }
            else
            {
                return Math.Max(
                           Math.Max(
                               oldestArrivalTime[r.request.requesterID],
                               vtmsBankFinishTime[r.request.requesterID, r.glob_b_index]
                           ) + (ulong)(serviceTime * Config.N),
                           vtmsBusFinishTime[r.request.requesterID]
                       ) + (ulong)(Config.memory.bus_busy_time * Config.N);
            }
        }

        /**
       * Returns true if r1 has higher priority than r2, if both requests are in the same bank!
       */
        protected bool higherNesbitPriority(MemoryRequest r1, MemoryRequest r2, ulong VFT1, ulong VFT2)
        {
            // 1. Priority: Requests with the open row hit
            // 2. Priority: Thread with smaller virtual finish time
            // 3. Priority: Requests with earlier arrival time
            bool isRowHit1 = (r1.r_index == bank[r1.glob_b_index].get_cur_row());
            bool isRowHit2 = (r2.r_index == bank[r2.glob_b_index].get_cur_row());

            // TODO: Can be implemented faster!
            if (!isRowHit1 && !isRowHit2)
            {
                if (r1.request.requesterID != r2.request.requesterID) // which thread should be prioritized?
                    return (VFT1 < VFT2);
                else  // if two requests from the same thread 
                    return (r1.timeOfArrival < r2.timeOfArrival);
            }
            else if (!isRowHit1 && isRowHit2)
                return false;
            else if (isRowHit1 && !isRowHit2)
                return true;
            // (r1.rowIndex != currentRow1 && r2.rowIndex != currentRow2)
            else
                if (r1.request.requesterID != r2.request.requesterID)
                    return (VFT1 < VFT2);
                else  // if two requests from the same thread 
                    return (r1.timeOfArrival < r2.timeOfArrival);

        }

        /**
      * Returns true if r1 has higher priority than r2, if both requests are in the same bank!
         * Writebacks are deprioritized
      */
        protected bool higherNesbitPriorityWB(MemoryRequest r1, MemoryRequest r2, ulong VFT1, ulong VFT2)
        {
            // 1. Priority: Requests with the open row hit
            // 2. Priority: Read request before writeback request
            // 3. Priority: Thread with smaller virtual finish time
            // 4. Priority: Requests with earlier arrival time
            bool isRowHit1 = (r1.r_index == bank[r1.glob_b_index].get_cur_row());
            bool isRowHit2 = (r2.r_index == bank[r2.glob_b_index].get_cur_row());
            bool isWriteback1 = (r1.type == MemoryRequestType.WB);
            bool isWriteback2 = (r2.type == MemoryRequestType.WB);

            if (!isRowHit1 && !isRowHit2)
            {
                if (isWriteback1 && !isWriteback2)
                    return false;
                else if (!isWriteback1 && isWriteback2)
                    return true;
                else if (r1.request.requesterID != r2.request.requesterID) // which thread should be prioritized?
                    return (VFT1 < VFT2);
                else  // if two requests from the same thread 
                    return (r1.timeOfArrival < r2.timeOfArrival);
            }
            else if (!isRowHit1 && isRowHit2)
                return false;
            else if (isRowHit1 && !isRowHit2)
                return true;
            // (r1.rowIndex != currentRow1 && r2.rowIndex != currentRow2)
            else
            {
                if (isWriteback1 && !isWriteback2)
                    return false;
                else if (!isWriteback1 && isWriteback2)
                    return true;
                else if (r1.request.requesterID != r2.request.requesterID)
                    return (VFT1 < VFT2);
                else  // if two requests from the same thread 
                    return (r1.timeOfArrival < r2.timeOfArrival);
            }

        }

    }

    class Nesbit_Basic_MemoryScheduler_WB : Nesbit_Basic_MemoryScheduler
    {

        public Nesbit_Basic_MemoryScheduler_WB(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            Console.WriteLine("Initialized Nesbit_Basic_MemoryScheduler_WB");
        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way! 
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            // In the Nesbit scheme, this function has to do three things: 
            // 1) Get the next request to be scheduled
            // 2) If there is a next request, update the data structures
            // 3) Return the next request
            MemoryRequest nextRequest = null;
            bool ScheduleWB = (this.get_wb_fraction() > Config.memory.wb_full_ratio);
            ulong nextVFT = 0;

            // 1. Get the highest priority request, if there is one. 
            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC)
            {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else
            {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0)
                {
                    // Find the highest priority request for this bank
                    MemoryRequest nextBankRequest = buf[b, 0];
                    ulong nextBankVFT = computeVirtualFinishTime(nextBankRequest);
                    for (int j = 1; j < buf_load[b]; j++)
                    {
                        ulong curBankVFT = computeVirtualFinishTime(buf[b, j]);
                        if (ScheduleWB && !higherNesbitPriority(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT) ||
                        (!ScheduleWB && !higherNesbitPriorityWB(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT)))
                        {
                            nextBankRequest = buf[b, j];
                            nextBankVFT = curBankVFT;
                        }
                    }
                    // Compare between highest priority between different banks
                    if (nextRequest == null ||
                     (ScheduleWB && !higherNesbitPriority(nextRequest, nextBankRequest, nextVFT, nextBankVFT)) ||
                     (!ScheduleWB && !higherNesbitPriorityWB(nextRequest, nextBankRequest, nextVFT, nextBankVFT)))
                    {
                        nextRequest = nextBankRequest;
                        nextVFT = nextBankVFT;
                    }
                }
            }

            //2. Update the data structures
            if (nextRequest != null) updateVMTSRegisters(nextRequest);

            // 3. Return the request. 
            return nextRequest;
        }

    }

    class Nesbit_Full_MemoryScheduler : Nesbit_Basic_MemoryScheduler
    {

        // Use the Nesbit_Basic scheduler. Each bank has a timer that is reset when a new
        // row is used. When the timer exceeds a threshold T, then first-virtual-deadline first
        // is used. Otherwise, the Nesbit_rule. 

        protected ulong[,] totalScheduled;  // total number of requests per thread
        protected ulong[,] wonDueToFR;        // requests scheduled due to row-hit first (per thread/bank)
        protected ulong[,] lostDueToFR;        // requests scheduled due to row-hit first (per thread/bank)
        protected ulong[] bankTimers;

        public Nesbit_Full_MemoryScheduler(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {
            totalScheduled = new ulong[Config.N, Config.memory.bank_max_per_mem];
            wonDueToFR = new ulong[Config.N, Config.memory.bank_max_per_mem];
            lostDueToFR = new ulong[Config.N, Config.memory.bank_max_per_mem];
            bankTimers = new ulong[Config.memory.bank_max_per_mem];
            Console.WriteLine("Initialized Nesbit_Full_MemoryScheduler");
        }

        protected ulong[,] VFTHitCache = new ulong[Config.N, Config.memory.bank_max_per_mem];
        protected ulong[,] VFTMissCache = new ulong[Config.N, Config.memory.bank_max_per_mem];

        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            // In the Nesbit scheme, this function has to do three things: 
            // 1) Get the next request to be scheduled
            // 2) If there is a next request, update the data structures
            // 3) Return the next request
            MemoryRequest nextRequest = null;
            MemoryRequest statsNextRequest = null; // for statistics, also keep the VFT highest request
            ulong nextVFT = 0;
            ulong statsNextVFT = 0; // for statistics

            //     Simulator.writer.WriteLine(Memory.memoryTime + "   " + vtmsBankFinishTime[0, 0] + "   " + vtmsBankFinishTime[1, 0]);


            // 1. Get the highest priority request, if there is one. 
            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC)
            {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else
            {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0)
                {
                    // check whether the timer for this bank has already exceeded
                    bool cap = ((MemCtlr.cycle - bankTimers[b]) >= Config.memory.prio_inv_thresh);

                    if (Config.memory.prio_inv_thresh == 0)
                    {
                        if (cap == false)
                        {
                            Environment.Exit(1);
                        }
                    }

                    // Find the highest priority request for this bank
                    MemoryRequest nextBankRequest = buf[b, 0];
                    MemoryRequest statsNextBankRequest = buf[b, 0];   // for statistics
                    ulong nextBankVFT = computeVirtualFinishTime(nextBankRequest);
                    ulong statsNextBankVFT = nextBankVFT;  // for statistics
                    for (int j = 1; j < buf_load[b]; j++)
                    {
                        ulong curBankVFT = computeVirtualFinishTime(buf[b, j]);
                        // for stats
                        if (!higherFVDPriority(statsNextBankRequest, buf[b, j], statsNextBankVFT, curBankVFT))
                        {
                            statsNextBankRequest = buf[b, j];
                            statsNextBankVFT = curBankVFT;
                        }
                        // for real!
                        if (cap)
                        {
                            if (!higherFVDPriority(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT))
                            {
                                nextBankRequest = buf[b, j];
                                nextBankVFT = curBankVFT;
                            }
                        }
                        else
                        {
                            if (!higherNesbitPriority(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT))
                            {
                                nextBankRequest = buf[b, j];
                                nextBankVFT = curBankVFT;
                            }

                        }
                    }

                    // for stats
                    if (statsNextRequest == null || !higherNesbitPriority(statsNextRequest, statsNextBankRequest, statsNextVFT, statsNextBankVFT))
                    {
                        statsNextRequest = statsNextBankRequest;
                        statsNextVFT = statsNextBankVFT;
                    }


                    // Compare between highest priority between different banks
                    if (nextRequest == null || !higherNesbitPriority(nextRequest, nextBankRequest, nextVFT, nextBankVFT))
                    //   if (nextRequest == null || !higherFCFSPriority(nextRequest, nextBankRequest))
                    {
                        nextRequest = nextBankRequest;
                        nextVFT = nextBankVFT;
                    }
                }
            }

            // 2. Update the data structures
            if (nextRequest != null)
            {
                // update statistics  // TODO!!!
                totalScheduled[nextRequest.request.requesterID, nextRequest.glob_b_index]++;
                if (nextRequest.request.requesterID != statsNextRequest.request.requesterID)
                {
                    wonDueToFR[nextRequest.request.requesterID, nextRequest.glob_b_index]++;
                    lostDueToFR[statsNextRequest.request.requesterID, statsNextRequest.glob_b_index]++;
                }

                // update VTMSRegisters
                updateVMTSRegisters(nextRequest);

                // Update the bank timers if the row has changed!
                if (bank[nextRequest.glob_b_index].get_cur_row() != nextRequest.r_index)
                    bankTimers[nextRequest.glob_b_index] = MemCtlr.cycle;

                // delete VTF cache
            }
            for (int i = 0; i < Config.N; i++)
                for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
                {
                    VFTHitCache[i, j] = 0;
                    VFTMissCache[i, j] = 0;
                }

            // 3. Return the request. 
            return nextRequest;
        }


        /**
         * First checks whether the value has already been computed!
         * If so, don't compute it again!
         */
        protected override ulong computeVirtualFinishTime(MemoryRequest r)
        {
            if (bank[r.glob_b_index].get_cur_row() == r.r_index)
                if (VFTHitCache[r.request.requesterID, r.glob_b_index] != 0)
                {
                    return VFTHitCache[r.request.requesterID, r.glob_b_index];
                }
                else
                {
                    VFTHitCache[r.request.requesterID, r.glob_b_index] = base.computeVirtualFinishTime(r);
                    return VFTHitCache[r.request.requesterID, r.glob_b_index];
                }
            else  // if bank[r.bankIndex].getCurrentRowIndex() != r.rowIndex
            {
                if (VFTMissCache[r.request.requesterID, r.glob_b_index] != 0)
                {
                    return VFTMissCache[r.request.requesterID, r.glob_b_index];
                }
                else
                {
                    VFTMissCache[r.request.requesterID, r.glob_b_index] = base.computeVirtualFinishTime(r);
                    return VFTMissCache[r.request.requesterID, r.glob_b_index];
                }
            }

        }

        /**
       * Returns true if r1 has higher priority than r2, if both requests are in the same bank!
       */
        protected bool higherFVDPriority(MemoryRequest r1, MemoryRequest r2, ulong VFT1, ulong VFT2)
        {
            // 1. Priority: First-Virtual Deadline first (regardless of row-buffer)
            // 2. Priority: If from same thread -> use open row buffer first!

            if (r1.request.requesterID != r2.request.requesterID)
                return (VFT1 < VFT2);

            // NOTE: Nesbit does not actually write it this way. It should always have a cap!!!
            else  // if two requests from the same thread - probably prioritize open row hit first ?!
            {
                bool isRowHit1 = (r1.r_index == bank[r1.glob_b_index].get_cur_row());
                bool isRowHit2 = (r2.r_index == bank[r2.glob_b_index].get_cur_row());
                if (!isRowHit1 && isRowHit2)
                    return false;
                else if (isRowHit1 && !isRowHit2)
                    return true;
                else // either both the same row or both a different row!
                    return (r1.timeOfArrival < r2.timeOfArrival);

            }
        }


        /**
      * Returns true if r1 has higher priority than r2, if both requests are in the same bank!
      */
        protected bool higherFVDPriorityWB(MemoryRequest r1, MemoryRequest r2, ulong VFT1, ulong VFT2)
        {
            // 1. Priority: Read operation before Writeback 
            // 2. Priority: First-Virtual Deadline first (regardless of row-buffer)
            // 3. Priority: If from same thread -> use open row buffer first!
            bool isWriteback1 = (r1.type == MemoryRequestType.WB);
            bool isWriteback2 = (r2.type == MemoryRequestType.WB);

            if (isWriteback1 && !isWriteback2)
                return false;
            else if (!isWriteback1 && isWriteback2)
                return true;
            else if (r1.request.requesterID != r2.request.requesterID)
                return (VFT1 < VFT2);
            else  // if two requests from the same thread - probably prioritize open row hit first ?!
            {
                bool isRowHit1 = (r1.r_index == bank[r1.glob_b_index].get_cur_row());
                bool isRowHit2 = (r2.r_index == bank[r2.glob_b_index].get_cur_row());
                if (!isRowHit1 && isRowHit2)
                    return false;
                else if (isRowHit1 && !isRowHit2)
                    return true;
                else // either both the same row or both a different row!
                    return (r1.timeOfArrival < r2.timeOfArrival);

            }
        }

        public override void report(TextWriter writer)
        {
            base.report(writer);
            ulong[] threadScheduled = new ulong[Config.N];
            ulong[] threadDueToFVD = new ulong[Config.N];
            ulong[] threadLostDueToFVD = new ulong[Config.N];
            for (int i = 0; i < Config.N; i++)
                for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
                {
                    threadScheduled[i] += totalScheduled[i, j];
                    threadDueToFVD[i] += wonDueToFR[i, j];
                    threadLostDueToFVD[i] += lostDueToFR[i, j];
                }



            writer.WriteLine();
            writer.WriteLine("OUTPUT FOR NESBIT_FULL ALGORITHM:");
            writer.WriteLine("Priority-Inversion Threshold: " + Config.memory.prio_inv_thresh);
            for (int i = 0; i < Config.N; i++)
            {
                writer.WriteLine("Processor " + i + ": ");
                writer.WriteLine("      Total Number of Requests: " + threadScheduled[i]);
                writer.WriteLine("      Requests scheduled due to Priority Inversion: " + threadDueToFVD[i]);
                writer.WriteLine("      Requests lost due to Priority Inversion: " + threadLostDueToFVD[i]);
            }

        }
    }

    class Nesbit_Full_MemoryScheduler_WB : Nesbit_Full_MemoryScheduler
    {


        public Nesbit_Full_MemoryScheduler_WB(int totalSize, Bank[] bank)
            : base(totalSize, bank)
        {

            Console.WriteLine("Initialized Nesbit_Full_MemoryScheduler_WB");
        }

        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            // In the Nesbit scheme, this function has to do three things: 
            // 1) Get the next request to be scheduled
            // 2) If there is a next request, update the data structures
            // 3) Return the next request
            bool ScheduleWB = (this.get_wb_fraction() > Config.memory.wb_full_ratio);

            MemoryRequest nextRequest = null;
            MemoryRequest statsNextRequest = null; // for statistics, also keep the VFT highest request
            ulong nextVFT = 0;
            ulong statsNextVFT = 0; // for statistics

            //     Simulator.writer.WriteLine(Memory.memoryTime + "   " + vtmsBankFinishTime[0, 0] + "   " + vtmsBankFinishTime[1, 0]);


            // 1. Get the highest priority request, if there is one. 
            // Get the highest priority request.
            int bank_index;
            if (Config.memory.is_shared_MC)
            {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else
            {
                bank_index = 0;
            }
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
            {
                if (bank[b].is_ready() && buf_load[b] > 0)
                {
                    // check whether the timer for this bank has already exceeded
                    bool cap = ((MemCtlr.cycle - bankTimers[b]) >= Config.memory.prio_inv_thresh);

                    // Find the highest priority request for this bank
                    MemoryRequest nextBankRequest = buf[b, 0];
                    MemoryRequest statsNextBankRequest = buf[b, 0];   // for statistics
                    ulong nextBankVFT = computeVirtualFinishTime(nextBankRequest);
                    ulong statsNextBankVFT = nextBankVFT;  // for statistics
                    for (int j = 1; j < buf_load[b]; j++)
                    {
                        ulong curBankVFT = computeVirtualFinishTime(buf[b, j]);
                        // for stats
                        if (ScheduleWB && !higherFVDPriority(statsNextBankRequest, buf[b, j], statsNextBankVFT, curBankVFT) ||
                            (!ScheduleWB && !higherFVDPriorityWB(statsNextBankRequest, buf[b, j], statsNextBankVFT, curBankVFT)))
                        {
                            statsNextBankRequest = buf[b, j];
                            statsNextBankVFT = curBankVFT;
                        }
                        // for real!
                        if (cap)
                        {
                            if (ScheduleWB && !higherFVDPriority(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT) ||
                                (!ScheduleWB && !higherFVDPriorityWB(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT)))
                            {
                                nextBankRequest = buf[b, j];
                                nextBankVFT = curBankVFT;
                            }
                        }
                        else
                        {
                            if (ScheduleWB && !higherNesbitPriority(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT) ||
                                 (!ScheduleWB && !higherNesbitPriorityWB(nextBankRequest, buf[b, j], nextBankVFT, curBankVFT)))
                            {
                                nextBankRequest = buf[b, j];
                                nextBankVFT = curBankVFT;
                            }

                        }
                    }

                    // TODO: Correct this here for stats
                    if (statsNextRequest == null || !higherFVDPriority(statsNextRequest, statsNextBankRequest, statsNextVFT, statsNextBankVFT))
                    {
                        statsNextRequest = statsNextBankRequest;
                        statsNextVFT = statsNextBankVFT;
                    }


                    // Compare between highest priority between different banks
                    if (nextRequest == null ||
                   (ScheduleWB && !higherNesbitPriority(nextRequest, nextBankRequest, nextVFT, nextBankVFT)) ||
                   (!ScheduleWB && !higherNesbitPriorityWB(nextRequest, nextBankRequest, nextVFT, nextBankVFT)))

                    //   if (nextRequest == null || !higherFCFSPriority(nextRequest, nextBankRequest))
                    {
                        nextRequest = nextBankRequest;
                        nextVFT = nextBankVFT;
                    }
                }
            }

            // 2. Update the data structures
            if (nextRequest != null)
            {
                // update statistics  // TODO!!!
                totalScheduled[nextRequest.request.requesterID, nextRequest.glob_b_index]++;
                if (nextRequest.request.requesterID != statsNextRequest.request.requesterID)
                {
                    wonDueToFR[nextRequest.request.requesterID, nextRequest.glob_b_index]++;
                    lostDueToFR[statsNextRequest.request.requesterID, statsNextRequest.glob_b_index]++;
                }

                // update VTMSRegisters
                updateVMTSRegisters(nextRequest);

                // Update the bank timers if the row has changed!
                if (bank[nextRequest.glob_b_index].get_cur_row() != nextRequest.r_index)
                    bankTimers[nextRequest.glob_b_index] = MemCtlr.cycle;

            }
            // delete VTF cache
            for (int i = 0; i < Config.N; i++)
                for (int j = 0; j < Config.memory.bank_max_per_mem; j++)
                {
                    VFTHitCache[i, j] = 0;
                    VFTMissCache[i, j] = 0;
                }
            // 3. Return the request. 
            return nextRequest;
        }

    }
}
