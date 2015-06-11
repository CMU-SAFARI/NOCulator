using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace ICSimulator
{
    /**
     * For batch-based memory scheduling algorithms, the within batch scheduling algorithm.
     */

    /*
    public enum BatchSchedAlgo
    {
        MARKED_FCFS,           //Marked, then FCFS
        MARKED_FR_FCFS,         //Marked, then FR-FCFS
        MARKED_FR_RANK_FCFS,    //Marked, then row hit, then higher rank, then FCFS; PAR-BS
        MARKED_RANK_FR_FCFS,    //Marked, then higher rank, then row hit, then FCFS
    }

    public class BatchSched
    {
        public SchedAlgo sched_algo;

        public Bank[] bank;
        public int bank_max;
        public int[] cur_max_load_per_proc;

        public BatchSched(Bank[] bank, int bank_max, int[] cur_max_load_per_proc)
        {
            this.bank = bank;
            this.bank_max = bank_max;
            this.cur_max_load_per_proc = cur_max_load_per_proc;
        }

  
    }

*/
}