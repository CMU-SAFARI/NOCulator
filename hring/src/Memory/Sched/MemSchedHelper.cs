using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace ICSimulator
{
    public class MemSchedHelper
    {
        public Bank[] bank;
        public int bank_max;
        public int[] cur_max_load_per_proc;

        public MemSchedHelper(Bank[] bank, int bank_max, int[] cur_max_load_per_proc)
        {
            this.bank = bank;
            this.bank_max = bank_max;
            this.cur_max_load_per_proc = cur_max_load_per_proc;
        }

        private bool BATCH_A(MemoryRequest r1, MemoryRequest r2, ref bool det)
        {
            det = true;

            if (((MemCtlr.cycle - r1.timeOfArrival) > 500) && ((MemCtlr.cycle - r2.timeOfArrival) < 500))
                return true;

            if (((MemCtlr.cycle - r1.timeOfArrival) < 500) && ((MemCtlr.cycle - r2.timeOfArrival) > 500))
                return false;

            det = false;
            return false;
        }

        private bool BATCH_B(MemoryRequest r1, MemoryRequest r2, ref bool det)
        {
            det = true;

            if ((cur_max_load_per_proc[r1.request.requesterID] < 5) && (cur_max_load_per_proc[r2.request.requesterID] >= 5))
                return true;

            if ((cur_max_load_per_proc[r1.request.requesterID] >= 5) && (cur_max_load_per_proc[r2.request.requesterID] < 5))
                return false;

            det = false;
            return false;
        }
        private bool X(double x1, double x2, ref bool det)
        {
            det = true;

            if (x1 > x2)
                return true;

            if (x1 < x2)
                return false;

            det = false;
            return false;
        }

        private bool PLL(MemoryRequest r1, MemoryRequest r2, ref bool det)
        {
            det = true;

            int bank_cnt1 = 0, bank_cnt2 = 0;
            for (int b = 0; b < bank_max; b++) {

                if (!bank[b].is_ready() && (bank[b].get_cur_req().request.requesterID == r1.request.requesterID))
                    bank_cnt1++;

                if (!bank[b].is_ready() && (bank[b].get_cur_req().request.requesterID == r2.request.requesterID))
                    bank_cnt2++;
            }

            if (bank_cnt1 > bank_cnt2)
                return true;

            if (bank_cnt1 < bank_cnt2)
                return false;

            det = false;
            return false;
        }

        private bool PRIO(int prio1, int prio2, ref bool det)
        {
            det = true;

            //lower priority value is greater precedence
            if (prio1 < prio2)
                return true;

            if (prio1 > prio2)
                return false;

            det = false;
            return false;
        }

        private bool TOTAU(float totAU1, float totAU2, ref bool det)
        {
            det = true;

            //higher rank value is greater precedence
            if (totAU1 > totAU2)
                return true;

            if (totAU1 < totAU2)
                return false;

            det = false;
            return false;
        }

        private bool BRANK(int brank1, int brank2, ref bool det)
        {
            det = true;

            //higher rank value is greater precedence
            if (brank1 > brank2)
                return true;

            if (brank1 < brank2)
                return false;

            det = false;
            return false;
        }

        private bool BRT(int brankt1, int brankt2, ref bool det)
        {
            det = true;

            //higher rank value is greater precedence
            if (brankt1 > brankt2)
                return true;

            if (brankt1 < brankt2)
                return false;

            det = false;
            return false;
        }


        private bool MARK(MemoryRequest r1, MemoryRequest r2, ref bool det)
        {
            det = true;

            if (r1.isMarked && !r2.isMarked)
                return true;

            if (!r1.isMarked && r2.isMarked)
                return false;

            det = false;
            return false;
        }

        private bool NONWB(MemoryRequest r1, MemoryRequest r2, ref bool det)
        {
            det = true;

            bool is_nonwb1 = (r1.type != MemoryRequestType.WB);
            bool is_nonwb2 = (r2.type != MemoryRequestType.WB);

            if (is_nonwb1 && !is_nonwb2)
                return true;

            if (!is_nonwb1 && is_nonwb2)
                return false;

            det = false;
            return false;
        }

        private bool RANK(int rank1, int rank2, ref bool det)
        {
            det = true;

            //higher rank value is greater precedence
            if (rank1 > rank2)
                return true;

            if (rank1 < rank2)
                return false;

            det = false;
            return false;
        }

        private bool FR(MemoryRequest r1, MemoryRequest r2, ref bool det)
        {
            det = true;

            bool is_row_hit1 = (r1.r_index == bank[r1.glob_b_index].get_cur_row());
            bool is_row_hit2 = (r2.r_index == bank[r2.glob_b_index].get_cur_row());

            if (is_row_hit1 && !is_row_hit2)
                return true;

            if (!is_row_hit1 && is_row_hit2)
                return false;

            det = false;
            return false;
        }

        //private bool RANK_FR_SR(MemoryRequest r1, Req r2, int rank1, int rank2, ref bool det)
        //{
        //    det = true;

        //    bool FR_det = new bool();
        //    bool FR_res = FR(r1, r2, ref FR_det);

        //    if (!FR_det) {
        //        bool RANK_det = new bool();
        //        bool RANK_res = RANK(rank1, rank2, ref RANK_det);
 
        //        det = RANK_det;
        //        return RANK_res;
        //    }

        //    if (FR_res) {
        //        //r1 is FR and r2 is FR
        //        if (rank1 >= Simulator.NumberOfApplication_Processors / 2)
        //            return true;
        //        else
        //            return false;
        //    }

        //    //r2 is FR and r1 is non-FR
        //    if (rank2 >= Simulator.NumberOfApplication_Processors / 2)
        //        return false;
        //    else
        //        return true;
        //}

        //private bool RANK_FR_SAS(MemoryRequest r1, Req r2, int as1, int as2, ref bool det)
        //{
        //    det = true;

        //    bool FR_det = new bool();
        //    bool FR_res = FR(r1, r2, ref FR_det);

        //    if (!FR_det) {
        //        bool RANK_det = new bool();
        //        bool RANK_res = RANK(rank1, rank2, ref RANK_det);

        //        det = RANK_det;
        //        return RANK_res;
        //    }

        //    if (FR_res) {
        //        //r1 is FR and r2 is FR
        //        if (rank1 >= Simulator.NumberOfApplication_Processors / 2)
        //            return true;
        //        else
        //            return false;
        //    }

        //    //r2 is FR and r1 is non-FR
        //    if (rank2 >= Simulator.NumberOfApplication_Processors / 2)
        //        return false;
        //    else
        //        return true;
        //}

        private bool FCFS(MemoryRequest r1, MemoryRequest r2, ref bool det)
        {
            det = true;

            if (r1.timeOfArrival < r2.timeOfArrival)
                return true;

            if (r1.timeOfArrival > r2.timeOfArrival)
                return false;

            det = false;
            return false;
        }

        public bool is_FCFS(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_NONWB_FCFS(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = NONWB(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_FR_FCFS(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_NONWB_FR_FCFS(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = NONWB(r1, r2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_MARK_RANK_FCFS(MemoryRequest r1, MemoryRequest r2, int rank1, int rank2)
        {
            bool det = false;
            bool result;

            result = MARK(r1, r2, ref det);
            if (det)
                return result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        //public bool is_MARK_RANK_FR_SR_FCFS(MemoryRequest r1, Req r2, int rank1, int rank2)
        //{
        //    bool det = false;
        //    bool result;

        //    result = MARK(r1, r2, ref det);
        //    if (det)
        //        return result;

        //    result = RANK_FR_SR(r1, r2, rank1, rank2, ref det);
        //    if (det)
        //        return result;

        //    result = FCFS(r1, r2, ref det);
        //    return result;
        //}

        public bool is_MARK_RANK_FR_FCFS(MemoryRequest r1, MemoryRequest r2, int rank1, int rank2)
        {
            bool det = false;
            bool result;

            result = MARK(r1, r2, ref det);
            if (det)
                return result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_RANK_FR_FCFS(MemoryRequest r1, MemoryRequest r2, int rank1, int rank2)
        {
            bool det = false;
            bool result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_RANK_FCFS(MemoryRequest r1, MemoryRequest r2, int rank1, int rank2)
        {
            bool det = false;
            bool result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_FR_RANK_FCFS(MemoryRequest r1, MemoryRequest r2, int rank1, int rank2)
        {
            bool det = false;
            bool result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_MARK_FR_RANK_FCFS(MemoryRequest r1, MemoryRequest r2, int rank1, int rank2)
        {
            bool det = false;
            bool result;

            result = MARK(r1, r2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_MARK_PRIO_FR_RANK_TOTAU_BR_BRT_FCFS(MemoryRequest r1, MemoryRequest r2, int rank1, int rank2, int prio1, int prio2, int brank1, int brank2, int brankt1, int brankt2, float totAU1, float totAU2)
        {
            bool det = false;
            bool result;

            result = MARK(r1, r2, ref det);
            if (det)
                return result;

            result = PRIO(prio1, prio2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;
        
                result = TOTAU(totAU1, totAU2, ref det);
                if (det)
                    return result;

            result = BRANK(brank1, brank2, ref det);
            if (det)
                return result;
        
            result = BRT(brankt1, brankt2, ref det);
            if (det)
                return result;
        
            result = FCFS(r1, r2, ref det);
            return result;
        }


        public bool is_MARK_FR_FCFS(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = MARK(r1, r2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_MARK_FCFS(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = MARK(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_MARK_PRIO_FR_RANK_FCFS(MemoryRequest r1, MemoryRequest r2, int prio1, int prio2, int rank1, int rank2)
        {
            bool det = false;
            bool result;

            result = MARK(r1, r2, ref det);
            if (det)
                return result;

            result = PRIO(prio1, prio2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = RANK(rank1, rank2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_PLL(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = PLL(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_FR_X_FCFS(MemoryRequest r1, MemoryRequest r2, double x1, double x2)
        {
            bool det = false;
            bool result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = X(x1, x2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_NONWB_FR_X_FCFS(MemoryRequest r1, MemoryRequest r2, double x1, double x2)
        {
            bool det = false;
            bool result;

            result = NONWB(r1, r2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = X(x1, x2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_X_FCFS(MemoryRequest r1, MemoryRequest r2, double x1, double x2)
        {
            bool det = false;
            bool result;

            result = X(x1, x2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_NONWB_X_FCFS(MemoryRequest r1, MemoryRequest r2, double x1, double x2)
        {
            bool det = false;
            bool result;

            result = NONWB(r1, r2, ref det);
            if (det)
                return result;

            result = X(x1, x2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

        public bool is_BATCH(MemoryRequest r1, MemoryRequest r2)
        {
            bool det = false;
            bool result;

            result = BATCH_A(r1, r2, ref det);
            if (det)
                return result;

            result = BATCH_B(r1, r2, ref det);
            if (det)
                return result;

            result = FR(r1, r2, ref det);
            if (det)
                return result;

            result = FCFS(r1, r2, ref det);
            return result;
        }

    }

}
