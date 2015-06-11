using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace ICSimulator
{
    /**
     * Memory scheduling algorithm.
     */
    public enum MemSchedAlgo
    {
        //???
        FairMemScheduler,

        //STFM scheduling algorithm
        FairMemMicro,          //Stall time fair memory scheduler (STFM)

        //Naive scheduling algorithm
        FCFS,                   //FCFS
        FRFCFS,                //FR-FCFS
        FR_FCFS_Cap,            //FR-FCFS with maximum cap on consecutive row hits

        //batch scheduling algorithms
        STATIC_BATCH,
        FULL_BATCH,                     //PAR-BS
        FULL_BATCH_RV,
        
        PERBANK_FULL_BATCH,
        EMPTY_SLOT_FULL_BATCH,
        STATIC_BATCH_WITH_PRIORITIES,
        FULL_BATCH_WITH_PRIORITIES,

        //Nesbit (NFQ) scheduling algorithms
        NesbitBasic,            //Nesbit (NFQ) without priority inversion threshold
        NesbitFull,             //Nesbit (NFQ) with priority inversion threshold

        //Etc
        PLL,
        PURE_PRIORITY_SCHEME,

        LAS_FCFS,
        LAS_FCFS_F1,
        LAS_FCFS_F2,
        FR_LAS_FCFS,
        LAS_FR_FCFS,

        LAS_FCFS_FAIR_TS, 
        LAS_FR_FCFS_FAIR_TS,

        LAS_QT_FCFS,
        LAS_QT_FR_FCFS,

        FR_LAS_QT_FCFS,
        FR_SLACK_LAS_QT_FCFS,

              OUR,

        //LAS_BA
        LAS_BA_FR,
        FR_LAS_BA,
        LAS_BA2_FR

    }

    /**
     * For batch-based memory scheduling algorithms, the within batch scheduling algorithm.
     */
    public enum BatchSchedAlgo
    {
        MARKED_RANK_FR_FCFS,    //Marked, then higher rank, then row hit, then FCFS
        MARKED_FR_RANK_FCFS,    //Marked, then row hit, then higher rank, then FCFS; PAR-BS
        
        MARKED_RANK_FCFS,       //Marked, then higher rank, then FCFS

        MARKED_FR_FCFS,         //Marked, then FR-FCFS
        MARKED_FCFS             //Marked, then FCFS
    }

    /**
     * For batch-based memory scheduling algorithms, the thread-ranking algorithm.
     */
    public enum RankAlgo
    {
        ROW_BUFFER_AWARE_MAX_RANKING,
        MAX_TOT_RANKING,        //PAR-BS
        TOT_MAX_RANKING,
        ROUND_ROBIN_RANKING,
        NO_RANKING,
        RANDOM_RANKING,
        PERBANK_LP_RANKING,
        PERBANK_SJF_RANKING,
        ECT_RANKING,            //Earliest completion time first
        ECT_RV_RANKING,          //Earliest completion time first, with ranking
        MAX_TOT_REVERSE_RANKING
    }
}
