using System;
using System.Collections.Generic;
using System.Text;
using lpsolve55;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    abstract class BATCH_WITH_PRIORITIES_MemoryScheduler : AbstractMemSched
    {
        public int[] procRank;
        public int[] Rank;
        public int[] markedReqThisBatchPerPriority;
        public ulong[] markCountPerPriority;
        public ulong markCount = 0;
        public ulong recompCount = 0;
        protected ulong batchStartPulse;
        public ulong[] batchStart;   // memory time of batch start for each priority. 


        // this is a priority from 0 to 9. 0 is the highest priority, 9 the lowest. 
        public int[] threadPriority;
        public int minimumPriorityLevel;

        public RankAlgo rankingScheme;

        // variables used for remarking
        protected int[] curMarkedPerProc;
        protected int[,] curMarkedPerProcBank; // counter that maintains how many requests per proc x bank  
        protected bool[,] exceedsBatchingCap; // flag is true if counter exceeds threshold. 


        public int currentPriorityThread = 0;


        //Statistics for SIB
        public double[] avgNumReqPerProcRemark;
        public double[] avgMaxReqPerProcRemark;
        public ulong[] avgNumReqPerProcRecomp;
        public ulong[] avgMaxReqPerProcRecomp;
        public ulong[] avgMarkedReqAtBatchEnd;
        public double[] avgNumMarkedReqPerProcRemark;
        public double[] avgMaxMarkedReqPerProcRemark;

        public ulong[] avgFullBatchingDuration;

        // Final Stats
        protected double[] finalavgNumReqPerProcRemark;
        protected double[] finalavgMaxReqPerProcRemark;
        protected double[] finalavgNumReqPerProcRecomp;
        protected double[] finalavgMaxReqPerProcRecomp;
        protected double[] finalavgMarkedReqAtBatchEnd;
        protected double[] finalavgNumMarkedReqPerProcRemark;
        protected double[] finalavgMaxMarkedReqPerProcRemark;
        public ulong[] finalmarkCountPerPriority;

        public double[] finalavgFullBatchingDuration;



        public BATCH_WITH_PRIORITIES_MemoryScheduler(int totalSize, Bank[] bank, RankAlgo rankingScheme)
            : base(totalSize, bank)
        {
            this.rankingScheme = rankingScheme;
            procRank = new int[Config.N];
            Rank = new int[Config.N];
            markedReqThisBatchPerPriority = new int[Config.memory.prio_max];

            curMarkedPerProc = new int[Config.N];
            curMarkedPerProcBank = new int[Config.N, Config.memory.bank_max_per_mem];
            exceedsBatchingCap = new bool[Config.N, Config.memory.bank_max_per_mem];

            threadPriority = new int[Config.N];
            minimumPriorityLevel = Config.memory.prio_max + 1;
            for (int p = 0; p < Config.N; p++) {
                threadPriority[p] = (int)Config.memory.weight[p];  // TODO: Properly rescale between weights and priorities!
                Console.WriteLine("Processor " + p + " has priority-level: " + threadPriority[p]);
                if (threadPriority[p] < minimumPriorityLevel) minimumPriorityLevel = threadPriority[p];
            }

            avgNumReqPerProcRemark = new double[Config.N];
            avgMaxReqPerProcRemark = new double[Config.N];
            avgNumReqPerProcRecomp = new ulong[Config.N];
            avgMaxReqPerProcRecomp = new ulong[Config.N];
            avgMarkedReqAtBatchEnd = new ulong[Config.N];
            avgNumMarkedReqPerProcRemark = new double[Config.N];
            avgMaxMarkedReqPerProcRemark = new double[Config.N];
            finalavgNumMarkedReqPerProcRemark = new double[Config.N];
            finalavgMaxMarkedReqPerProcRemark = new double[Config.N];
            markCountPerPriority = new ulong[Config.memory.prio_max];
            finalmarkCountPerPriority = new ulong[Config.N];
            finalavgNumReqPerProcRemark = new double[Config.N];
            finalavgMaxReqPerProcRemark = new double[Config.N];
            finalavgNumReqPerProcRecomp = new double[Config.N];
            finalavgMaxReqPerProcRecomp = new double[Config.N];
            finalavgMarkedReqAtBatchEnd = new double[Config.N];
            avgFullBatchingDuration = new ulong[Config.N];
            finalavgFullBatchingDuration = new double[Config.N];
            batchStart = new ulong[Config.memory.prio_max];
            for (int i = 0; i < Config.N; i++) {
                procRank[i] = i;
                Rank[i] = Config.N - 1;
            }
            Console.WriteLine("Initialized BATCH_WITH_PRIORITIES_MemoryScheduler");
            Console.WriteLine("Ranking Scheme: " + rankingScheme.ToString());
        }

        protected virtual void reMark(int firstUnbatchedPriority)
        {
            batchStartPulse = MemCtlr.cycle;
            markCount++;
            // reset batchingCapCounters
            for (int proc = 0; proc < Config.N; proc++) {
                curMarkedPerProc[proc] = 0;
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++) {
                    curMarkedPerProcBank[proc, b] = 0;
                    exceedsBatchingCap[proc, b] = false;
                }
            }

            //  Console.WriteLine("Starting new batch up to priority-level: " + firstUnbatchedPriority);
            for (int proc = 0; proc < Config.N; proc++) {
                if (threadPriority[proc] < firstUnbatchedPriority)
                    avgFullBatchingDuration[proc] += (MemCtlr.cycle - batchStart[threadPriority[proc]]);
            }
            for (int p = 0; p < firstUnbatchedPriority; p++) {
                batchStart[p] = MemCtlr.cycle;
                markedReqThisBatchPerPriority[p] = 0;
                markCountPerPriority[p]++;
            }
            // remark all remarks of threads with priority less than firstUnbatchedPriority
            for (int b = 0; b < Config.memory.bank_max_per_mem; b++)  // for each bank
            {
                for (int row = 0; row < buf_load[b]; row++) {
                    int procID = buf[b, row].request.requesterID;
                    int reqPriority = threadPriority[procID];
                    if (reqPriority < firstUnbatchedPriority) {
                        if (buf[b, row].isMarked == true) {   // should never be the case for full batching!
                            if (this is FULL_BATCH_WITH_PRIORITIES_MemoryScheduler)
                                throw new Exception("There is a marked entry remaining in the batch! This is not allowed in priority full-batching");
                            avgMarkedReqAtBatchEnd[procID]++;
                        }
                        if (curMarkedPerProcBank[procID, b] < Config.memory.batch_cap) {
                            // mark this request and increase the counter!
                            curMarkedPerProcBank[procID, b]++;
                            curMarkedPerProc[procID]++;
                            markedReqThisBatchPerPriority[reqPriority]++;
                            buf[b, row].isMarked = true;
                            avgNumMarkedReqPerProcRemark[procID]++;  // stats
                        }
                        else if (curMarkedPerProcBank[procID, b] == Config.memory.batch_cap) {
                            exceedsBatchingCap[procID, b] = true;
                        }
                    }
                }
            }

            // now for all proc x bank pairs that exceeded the cap, find the N oldest requests. 
            for (int proc = 0; proc < Config.N; proc++)
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                    if (exceedsBatchingCap[proc, b]) {
                        // first, unmark all requests from this thread. 
                        for (int row = 0; row < buf_load[b]; row++) {
                            if (buf[b, row].request.requesterID == proc) {
                                buf[b, row].isMarked = false;
                            }
                        }
                        // remark the requests so that the N oldest requests are marked!
                        // TODO: The whole thing can be implemented more efficiently!
                        for (int n = 0; n < Config.memory.batch_cap; n++) {
                            MemoryRequest curOldest = null;
                            for (int row = 0; row < buf_load[b]; row++) {
                                if (buf[b, row].request.requesterID == proc && !buf[b, row].isMarked &&
                                    (curOldest == null ||
                                    buf[b, row].timeOfArrival < curOldest.timeOfArrival)) {
                                    curOldest = buf[b, row];
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

            for (int i = 0; i < Config.N; i++) {
                if (threadPriority[i] < firstUnbatchedPriority) {
                    avgNumReqPerProcRemark[i] += cur_load_per_proc[i];
                    avgMaxReqPerProcRemark[i] += cur_max_load_per_proc[i];
                    avgMaxMarkedReqPerProcRemark[i] += Math.Min(cur_max_load_per_proc[i], Config.memory.batch_cap);
                }
            }

        }

        protected virtual void recomputeRank()
        {
            for (int i = 0; i < Config.N; i++) {
                avgNumReqPerProcRecomp[i] += (ulong)cur_load_per_proc[i];
                avgMaxReqPerProcRecomp[i] += (ulong)cur_max_load_per_proc[i];
            }

            switch (rankingScheme) {
                case RankAlgo.MAX_TOT_RANKING:
                    computeMaxTotRanking();
                    break;
                case RankAlgo.TOT_MAX_RANKING:
                    computeTotMaxRanking();
                    break;
                case RankAlgo.ROUND_ROBIN_RANKING:
                    computeRoundRobinRanking();
                    break;
                case RankAlgo.NO_RANKING:  // leads to FR-FCFS only within a batch. 
                    // everybody has equal rank!
                    for (int i = 0; i < Config.N; i++) {
                        Rank[i] = 0;
                    }
                    break;
            }
        }

        public override void remove_req(MemoryRequest request)
        {
            if (request.isMarked) {
                markedReqThisBatchPerPriority[threadPriority[request.request.requesterID]]--;
            }
            base.remove_req(request);
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
                if (bank[b].is_ready() && buf_load[b] > 0) {
                    // Find the highest priority request for this bank
                    if (nextRequest == null) nextRequest = buf[b, 0];
                    for (int j = 0; j < buf_load[b]; j++) {
                        if (!helper.is_MARK_PRIO_FR_RANK_FCFS(nextRequest, buf[b, j],
                            threadPriority[nextRequest.request.requesterID], threadPriority[buf[b, j].request.requesterID],
                            Rank[nextRequest.request.requesterID], Rank[buf[b, j].request.requesterID])) {
                            nextRequest = buf[b, j];
                        }
                    }
                }
            }

            return nextRequest;
        }

        // returns the first level that does not have to be checked or marked. 
        protected int findFirstUnbatchedPriority(ulong markCount)
        {
            int p = 0;
            ulong pInterval = 1;
            while (markCount % pInterval == 0 && p < Config.memory.prio_max - 1) {
                p++;
                pInterval *= 2;
            }
            return p;
        }



        public override void freeze_stat(int procID)
        {
            base.freeze_stat(procID);
            finalmarkCountPerPriority[procID] = markCountPerPriority[threadPriority[procID]];
            finalavgNumReqPerProcRemark[procID] = (double)avgNumReqPerProcRemark[procID] / (double)markCountPerPriority[threadPriority[procID]];
            finalavgMaxReqPerProcRemark[procID] = (double)avgMaxReqPerProcRemark[procID] / (double)markCountPerPriority[threadPriority[procID]];
            finalavgNumReqPerProcRecomp[procID] = (double)avgNumReqPerProcRecomp[procID] / (double)recompCount;
            finalavgMaxReqPerProcRecomp[procID] = (double)avgMaxReqPerProcRecomp[procID] / (double)recompCount;
            finalavgMarkedReqAtBatchEnd[procID] = (double)avgMarkedReqAtBatchEnd[procID] / (double)markCountPerPriority[threadPriority[procID]];
            finalavgFullBatchingDuration[procID] = (double)avgFullBatchingDuration[procID] / (double)markCountPerPriority[threadPriority[procID]];
            finalavgMaxMarkedReqPerProcRemark[procID] = (double)avgMaxMarkedReqPerProcRemark[procID] / (double)markCountPerPriority[threadPriority[procID]];
            finalavgNumMarkedReqPerProcRemark[procID] = (double)avgNumMarkedReqPerProcRemark[procID] / (double)markCountPerPriority[threadPriority[procID]];
        }

        public override void report(TextWriter writer, int procID)
        {
            base.report(writer, procID);
            writer.WriteLine("Batching-Proc Stats:");
            writer.WriteLine("   NumberOfRemarks: " + finalmarkCountPerPriority[procID]);
            writer.WriteLine("   AvgTotPerRemark: " + finalavgNumReqPerProcRemark[procID]);
            writer.WriteLine("   AvgMaxPerRemark: " + finalavgMaxReqPerProcRemark[procID]);
            writer.WriteLine("   AvgTotMarkedRequestsPerRemark: " + finalavgNumMarkedReqPerProcRemark[procID]);
            writer.WriteLine("   AvgMaxMarkedRequestsPerRemark: " + finalavgMaxMarkedReqPerProcRemark[procID]);
            writer.WriteLine("   AvgTotPerRecomp: " + finalavgNumReqPerProcRecomp[procID]);
            writer.WriteLine("   AvgMaxPerRecomp: " + finalavgMaxReqPerProcRecomp[procID]);
            writer.WriteLine("   AvgMarkedReq at Batch-End: " + finalavgMarkedReqAtBatchEnd[procID]);
            writer.WriteLine("   AvgBatchDuration: " + finalavgFullBatchingDuration[procID]);
        }

        public override void report(TextWriter writer)
        {
            //    Simulator.writer.WriteLine("Batching-Statistics:");
            //    Simulator.writer.WriteLine("   AvgBatchDuration: " + (double)avgFullBatchingDuration / (double)markCount);
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
            writer.WriteLine("0");
            //    Simulator.writer.WriteLine((double)avgFullBatchingDuration / (double)markCount);
        }

        protected virtual void computeRoundRobinRanking()
        {
            int temp = procRank[0];
            for (int i = 0; i < Config.N - 1; i++) {
                procRank[i] = procRank[i + 1];
            }
            procRank[Config.N - 1] = temp;
            for (int i = 0; i < Config.N - 1; i++) {
                Rank[procRank[i]] = i;
            }
        }

        protected virtual void computeTotMaxRanking()
        {
            int temp;
            for (int i = 0; i < Config.N - 1; i++) {
                for (int j = i + 1; j < Config.N; j++) {
                    if (cur_load_per_proc[procRank[i]] > cur_load_per_proc[procRank[j]]) {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else
                        if (cur_load_per_proc[procRank[i]] < cur_load_per_proc[procRank[j]]) { }
                        else {
                            if (cur_max_load_per_proc[procRank[i]] > cur_max_load_per_proc[procRank[j]]) {
                                temp = procRank[i];
                                procRank[i] = procRank[j];
                                procRank[j] = temp;
                            }
                        }
                }
            }
            Rank[procRank[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++) {
                if ((cur_max_load_per_proc[procRank[i]] == cur_max_load_per_proc[procRank[i - 1]])
                    && (cur_load_per_proc[procRank[i]] == cur_load_per_proc[procRank[i - 1]])) {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }
        }

        protected virtual void computeMaxTotRanking()
        {
            int temp;
            for (int i = 0; i < Config.N - 1; i++) {
                for (int j = i + 1; j < Config.N; j++) {
                    if (cur_max_load_per_proc[procRank[i]] > cur_max_load_per_proc[procRank[j]]) {
                        temp = procRank[i];
                        procRank[i] = procRank[j];
                        procRank[j] = temp;
                    }
                    else
                        if (cur_max_load_per_proc[procRank[i]] < cur_max_load_per_proc[procRank[j]]) { }
                        else {
                            if (cur_load_per_proc[procRank[i]] > cur_load_per_proc[procRank[j]]) {
                                temp = procRank[i];
                                procRank[i] = procRank[j];
                                procRank[j] = temp;
                            }
                        }
                }
            }
            Rank[procRank[0]] = Config.N - 1;
            for (int i = 1; i < Config.N; i++) {
                if ((cur_max_load_per_proc[procRank[i]] == cur_max_load_per_proc[procRank[i - 1]])
                    && (cur_load_per_proc[procRank[i]] == cur_load_per_proc[procRank[i - 1]])) {
                    Rank[procRank[i]] = Rank[procRank[i - 1]];
                }
                else {
                    Rank[procRank[i]] = Rank[procRank[i - 1]] - 1;
                }
            }
        }
    }


}