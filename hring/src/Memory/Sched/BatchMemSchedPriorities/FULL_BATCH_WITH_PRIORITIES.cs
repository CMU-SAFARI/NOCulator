using System;
using System.Collections.Generic;
using System.Text;
using lpsolve55;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    class FULL_BATCH_WITH_PRIORITIES_MemoryScheduler : BATCH_WITH_PRIORITIES_MemoryScheduler
    {

        public FULL_BATCH_WITH_PRIORITIES_MemoryScheduler(int totalSize, Bank[] bank, RankAlgo rankingScheme)
            : base(totalSize, bank, rankingScheme)
        {
            Console.WriteLine("Initialized FULL_WITH_PRIORITIES_BATCH_MemoryScheduler");
        }
        
        public override void tick()
        {
            base.tick();

            // check whether a new batch starts. 

            if (markedReqThisBatchPerPriority[minimumPriorityLevel] == 0
               && MemCtlr.cycle - batchStartPulse >= Config.memory.full_batch_min_batch_length) {
                // all priority levels below this priority have to be marked!
                int firstUnbatchedPriority = findFirstUnbatchedPriority(markCount);
                //     Console.WriteLine("Checking to start new batch up to priority-level: " + firstUnbatchedPriority);

                // check whether all threads are finished with their batch
                bool startNewBatch = true;
                for (int p = 0; p < firstUnbatchedPriority; p++) {
                    if (markedReqThisBatchPerPriority[p] > 0) {
                        startNewBatch = false;
                        break;
                    }
                }
                if (startNewBatch) {
                    reMark(firstUnbatchedPriority);
                    recompCount++;
                    // recompute ranks. 
                    recomputeRank();
                }
            }
        }
    }
    
}