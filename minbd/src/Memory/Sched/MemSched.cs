using System;
using System.Collections.Generic;
using System.Text;
using lpsolve55;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{
    public interface MemSched
    {
        void tick();

        MemoryRequest get_next_req(MemCtlr mem);
        bool issue_req(MemoryRequest req);
        void remove_req(MemoryRequest req);

        bool is_empty();
        bool is_full(int threadID);
       
        void flush_reads();
    }

    public abstract class AbstractMemSched : MemSched
    {
        /****************************************************************
         *  ------ Scheduler parameters -----
         ***************************************************************/
        public int bank_max;        //total number of banks
        public int buf_size;        //sum of size of all bank-specific buffers and remaining shared buffer
        public int shared_size;     //size of shared buffer that is shared among all banks when their own buffers overflow

         /****************************************************************
         *  ------ Statistics and reporting -----
         ***************************************************************/
        public MemSchedStat stat;

        /****************************************************************
         *  ------ Components -----
         ***************************************************************/
        public Bank[] bank;         //array of banks
        public MemoryRequest[,] buf;          //buffer for memory requests; [bank_index, buffer_index]
        public int[] buf_load;      //for each buffer, the number of memory requests in it
        
        /****************************************************************
         *  ------ Scheduler status -----
         ***************************************************************/
        //load
        public int cur_load;            //number of current requests
        public int cur_nonwb_load;      //number of curent non-writeback requests
        public int cur_shared_load;     //number of current requests in shared buffer

        //load per proc
        public int[] cur_load_per_proc;         //number of current requests per processor
        public int[,] cur_load_per_procbank;    //number of current requests per processor for each bank [proc, bank]

        public int[] cur_nonwb_per_proc;        //number of current non-writeback requests per processor
        public int[,] cur_nonwb_per_procbank;   //number of current non-writeback requests per processor for each bank [proc, bank]

        //max-load per proc
        public int[] cur_max_load_per_proc;        //maximum load across banks per processor

        /****************************************************************
         *  ------ Scheduling algorithm helper -----
         ***************************************************************/
        public MemSchedHelper helper;

        /****************************************************************
         *  ------ Etc -----
         ***************************************************************/
        public const ulong EMPTY_SLOT = ulong.MaxValue;
          public int[] bankRank;
          public int[,] bankRankPerThread;

        /**
         * Constructor
         */
        protected AbstractMemSched(int buf_size, Bank[] bank)
        {
            //parameters
            bank_max = bank.Length; //shared MC see global, non-shared
            this.buf_size = buf_size;
            shared_size = buf_size - Config.N * Config.memory.buf_size_per_proc;
            Debug.Assert(shared_size >= 0);

            //components
            this.bank = bank;
            buf = new MemoryRequest[bank_max, buf_size];
            buf_load = new int[bank_max];

            //load
            cur_load = 0;
            cur_nonwb_load = 0;
            cur_shared_load = 0;

            //load per proc
            cur_load_per_proc = new int[Config.N];
            cur_load_per_procbank = new int[Config.N, bank_max];

            cur_nonwb_per_proc = new int[Config.N];
            cur_nonwb_per_procbank = new int[Config.N, bank_max];

              bankRank = new int[Config.N];
              bankRankPerThread = new int[Config.memory.mem_max * Config.memory.bank_max_per_mem,Config.N];

            //max-load per proc
            cur_max_load_per_proc = new int[Config.N];

            //helper
            helper = new MemSchedHelper(bank, bank_max, cur_max_load_per_proc);

            //stat
            stat = new MemSchedStat(Config.N, bank, bank_max);
        }

        /**
         * This method returns the next request to be scheduled (sent over the bus). 
         * Each memory request scheduler needs to implement its own function
         */
        abstract public MemoryRequest get_next_req(MemCtlr mem);

        /**
         * This function can be implemented if the scheduler needs to make updates in every cycle
         */
        virtual public void tick()
        {
            //----- STATS START -----
            for (int p = 0; p < Config.N; p++) {

                stat.inc(p, ref stat.tick_load_per_proc[p], (ulong)cur_load_per_proc[p]);

                if (BankStat.req_cnt[p] > 0) {
                    stat.inc(p, ref stat.tick_req[p]);
                    stat.inc(p, ref stat.tick_req_cnt[p], (ulong)BankStat.req_cnt[p]);
                }

                if (BankStat.marked_req_cnt[p] > 0) {
                    stat.inc(p, ref stat.tick_marked[p]);
                    stat.inc(p, ref stat.tick_marked_req[p], (ulong)BankStat.marked_req_cnt[p]);
                }

                if (BankStat.unmarked_req_cnt[p] > 0) {
                    stat.inc(p, ref stat.tick_unmarked[p]);
                    stat.inc(p, ref stat.tick_unmarked_req[p], (ulong)BankStat.unmarked_req_cnt[p]);
                }
            }
            //----- STATS END -----
        }

        /**
         * Removes a request from the memory request buffer when it is finished. The implementation
         * removes this request from the memory request buffer and updates all statistic 
         * variables. If some data structure needs to be updated for the scheduler, this
         * method should be overriden
         */
        virtual public void remove_req(MemoryRequest req)
        {
            int bank_index = req.glob_b_index;
            int slot = req.buf_index;

            Debug.Assert(buf[bank_index, slot] == req);

            //overwrite last req to the req to be removed
            int buf_last_index = buf_load[bank_index] - 1;
            buf[bank_index, buf_last_index].buf_index = slot;
            buf[bank_index, slot] = buf[bank_index, buf_last_index];

            //clear the last buffer entry
            buf[bank_index, buf_last_index] = null;
            buf_load[bank_index]--;
            Debug.Assert(buf_load[bank_index] >= 0);

            //----- STATS START -----
            //shared load needs to be updated before cur_load_per_proc
            if (cur_load_per_proc[req.request.requesterID] > Config.memory.buf_size_per_proc) {
                cur_shared_load--;
            }

            cur_load--;
            cur_load_per_proc[req.request.requesterID]--;
            cur_load_per_procbank[req.request.requesterID, req.glob_b_index]--;

            Debug.Assert(cur_load >= 0);
            Debug.Assert(cur_load_per_proc[req.request.requesterID] >= 0);
            Debug.Assert(cur_load_per_procbank[req.request.requesterID, req.glob_b_index] >= 0);

            if (req.type != MemoryRequestType.WB) {
                cur_nonwb_load--;
                cur_nonwb_per_proc[req.request.requesterID]--;
                cur_nonwb_per_procbank[req.request.requesterID, req.glob_b_index]--;
            }
            //----- STATS END ------

            //find maximum load for any bank for this request
            cur_max_load_per_proc[req.request.requesterID] = 0;
            for (int b = 0; b < bank_max; b++)
                if (cur_max_load_per_proc[req.request.requesterID] < cur_load_per_procbank[req.request.requesterID, b])
                    cur_max_load_per_proc[req.request.requesterID] = cur_load_per_procbank[req.request.requesterID, b];
        }

        /**
        * This method is called when a new request is inserted into the memory request buffer. 
        * If the buffer is full, an exception is thrown. If the scheduler needs to maintain 
        * some extra data structures, it should override this method
        */
        virtual public bool issue_req(MemoryRequest req)
        {
            Debug.Assert(cur_load != buf_size);

            //time of arrival
            req.timeOfArrival = MemCtlr.cycle;

            //----- STATS START -----
            stat.inc(req.request.requesterID, ref stat.total_req_per_procbank[req.request.requesterID, req.glob_b_index]);

            cur_load++;
            cur_load_per_proc[req.request.requesterID]++;
            cur_load_per_procbank[req.request.requesterID, req.glob_b_index]++;

            //shared load needs to be updated after cur_load_per_proc
            if (cur_load_per_proc[req.request.requesterID] > Config.memory.buf_size_per_proc)
                cur_shared_load++;

            if (req.type != MemoryRequestType.WB) {
                cur_nonwb_load++;
                cur_nonwb_per_proc[req.request.requesterID]++;
                cur_nonwb_per_procbank[req.request.requesterID, req.glob_b_index]++;
            }
            //----- STATS END ------

            //set maximum load for any bank for this request
            if (cur_max_load_per_proc[req.request.requesterID] < cur_load_per_procbank[req.request.requesterID, req.glob_b_index])
                cur_max_load_per_proc[req.request.requesterID] = cur_load_per_procbank[req.request.requesterID, req.glob_b_index];


            //add request to end of the request buffer
            buf[req.glob_b_index, buf_load[req.glob_b_index]] = req;
            req.buf_index = buf_load[req.glob_b_index];
            buf_load[req.glob_b_index]++;

            return true;
        }

        virtual public void flush_reads()
        {
            for (int i = 0; i < bank_max; i++)
                for (int j = 0; j < buf_size; j++)
                    if (buf[i, j] != null && buf[i, j].type != MemoryRequestType.WB)
                        remove_req(buf[i, j]);

            for (int i = 0; i < bank_max; i++)
                if (bank[i].cur_req != null && bank[i].cur_req.type != MemoryRequestType.WB)
                    bank[i].cur_req = null;
        }

        /**
         * Returns true if the buffer is empty
         */
        virtual public bool is_empty()
        {
            return cur_load == 0;
        }

        /**
         * Returns true if the buffer is full
         */
        virtual public bool is_full(int threadID)
        {
            return (cur_shared_load >= buf_size - Config.N * Config.memory.buf_size_per_proc)
                    && (cur_load_per_proc[threadID] >= Config.memory.buf_size_per_proc);
        }

        /**
         * Returns the fraction of Writebacks in the request buffer
         */
        protected double get_wb_fraction()
        {
            return (double)(cur_load - cur_nonwb_load) / buf_size;
        }

        /**
        * Collects statistics from thread procID
        */
        public virtual void freeze_stat(int threadID)
        {
            stat.freeze_stat(threadID);
        }

        /**
        * Collects statistics from thread procID
        */
        public virtual void report(TextWriter writer, int threadID)
        {
            stat.report(writer, threadID);
        }

        public virtual void report_verbose(TextWriter writer, int procID, string trace_name)
        {
            stat.report_verbose(writer, procID, trace_name);
        }

        public virtual void report_excel(TextWriter writer)
        {
            stat.report_excel(writer);
        }

        /**
        * Write the output for Memory Schedulers here!
        */
        public virtual void report(TextWriter writer)
        {
            stat.report(writer);
        }
    }
}
