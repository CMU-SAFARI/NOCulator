using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace ICSimulator
{
    /**
     * The memory
     */
    public class MemCtlr
    {
        public Node node; //containing node

        //mem index
        static int index = 0;       ///total number of memories
        public int mem_id;          ///index of this memory

        //time
        public static ulong cycle = 0;      ///time; number of cycles since start.
                                            ///this shouldn't be static, but a lot of the code was written assuming it was.
                                            ///so we keep it as static, but introduce a little hack to increment it only once.

        //size
        public int bank_max;        ///total number of banks

        //components
        Bank[] bank;                ///array of banks consisting the memory
        public MemSched sched;      ///memory scheduler
        
        static Bank[] bank_global;
        static MemSched sched_global;

        //etc
        //public static ReqRecycle req_recycler = new ReqRecycle();   ///reduce simulation overhead of constantly allocating memory request objects

        public Bank getBank(int index)
        {
            return bank[index];
        }

        /**
         * Constructor
         */
        public MemCtlr(Node node)
        {
            this.node = node;

            /*
            //locally visible bank
            bank = new Bank[Config.memory.bank_max_per_mem];

            //allocate scheduler (sees local banks)
            sched = MemCtlr.alloc_sched(bank, Config.memory.buf_size_per_bank, Config.memory.mem_sched_algo, Config.memory.wb_special_sched);

            //allocate individual banks
            for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                bank[b] = new Bank(sched, this);
            
            //memory id
            mem_id = index++;

            //size
            this.bank_max = bank.Length;
            */
            
            //Yoongu: giant hack to support shared MCs
            if(Config.memory.is_shared_MC == false)
            {
                bank = new Bank[Config.memory.bank_max_per_mem];

                //allocate scheduler (sees local banks)
                sched = MemCtlr.alloc_sched(bank, Config.memory.buf_size_per_mem, Config.memory.mem_sched_algo, Config.memory.wb_special_sched);
    
                //allocate individual banks
                for (int b = 0; b < Config.memory.bank_max_per_mem; b++)
                    bank[b] = new Bank(sched, this);
                
                //memory id
                mem_id = index++;
    
                //size
                this.bank_max = bank.Length;                
            }
            else
            {
                //memory id
                mem_id = index++;
                            
                if(mem_id == 0){
                    //only the first memory allocates the global banks
                    bank_global = new Bank[Config.memory.bank_max_per_mem * Config.memory.mem_max];
                    
                    //allocate scheduler (sees local banks)
                    sched_global = MemCtlr.alloc_sched(bank_global, Config.memory.buf_size_per_mem * Config.memory.mem_max, 
                                                       Config.memory.mem_sched_algo, Config.memory.wb_special_sched);
        
                    //allocate individual banks
                    for (int b = 0; b < bank_global.Length; b++)
                        bank_global[b] = new Bank(sched_global, this);
                }
                            
                sched = sched_global;
                bank = new Bank[Config.memory.bank_max_per_mem];
                for(int b = 0; b < bank.Length; b++)
                {
                    bank[b] = bank_global[mem_id * Config.memory.bank_max_per_mem + b];
                }
                    
                //size
                this.bank_max = bank.Length;                
            }
        }

        /**
        * Progresses time for the memory system (scheduler, banks, bus)
        */
        public void doStep()
        {
            //progress time
            for (int b = 0; b < bank_max; b++) {
                bank[b].tick();
            }
            
            if (Config.memory.is_shared_MC)
            {
                if (mem_id == 0)
                {
                    sched.tick();
                }
            }
            else
            {
                sched.tick();
            }   
            
            if(mem_id == 0)
                cycle++;

            //get next request
            MemoryRequest next_req = null;
            next_req = sched.get_next_req(this);

            //no next request; can't do anything
            if (next_req == null)
                return;

            //serve next request
            bank[next_req.b_index].add_req(next_req);
        }
        
        /*
        public void receivePacket(MemoryPacket p)
        {
            Simulator.Ready cb;
            
            //receive WB or request from memory        
            if(p.type == MemoryRequestType.RD)
            {
                cb = delegate()
                    {
                        MemoryPacket mp = new MemoryPacket(
                            p.request, p.block,
                            MemoryRequestType.DAT, p.dest, p.src);

                        node.queuePacket(mp);
                    };                
            }
            else
            {
                // WB don't need a callback
                cb = delegate(){};
            }
                        
            access(p.request, cb);
        }
        */

        public void access(Request req, Simulator.Ready cb)
        {
            MemoryRequest mreq = new MemoryRequest(req, cb);
            sched.issue_req(mreq);
            bank[mreq.b_index].outstandingReqs_perapp[req.requesterID]++;
            bank[mreq.b_index].outstandingReqs++;
        }

        public void flush()
        {
            sched.flush_reads();
        }

        /**
         * Get scheduler
         */
        public MemSched get_sched()
        {
            return sched;
        }

        /**
         * Report statistics
         */
        public void report(TextWriter writer)
        {
            if (sched is AbstractMemSched)
                ((AbstractMemSched)sched).report(writer);
        }

        /**
         * A
         */
        public static MemSched alloc_sched(Bank[] bank, int buf_size, MemSchedAlgo mem_sched_algo, bool wb_special_sched)
        {
            if (wb_special_sched)
                switch (mem_sched_algo) {
                    case MemSchedAlgo.FairMemScheduler:
                        return new Ideal_MM_MemoryScheduler_WB(buf_size, bank);
                    case MemSchedAlgo.FRFCFS:
                        return new FR_FCFS_WB(buf_size, bank);
                    case MemSchedAlgo.FCFS:
                        return new FCFS_MemoryScheduler_WB(buf_size, bank);
                    case MemSchedAlgo.NesbitBasic:
                        return new Nesbit_Basic_MemoryScheduler_WB(buf_size, bank);
                    case MemSchedAlgo.NesbitFull:
                        return new Nesbit_Full_MemoryScheduler_WB(buf_size, bank);
                    case MemSchedAlgo.FairMemMicro:
                        return new Ideal_MICRO_MemoryScheduler_WB(buf_size, bank);
                    case MemSchedAlgo.FR_FCFS_Cap:
                        return new FR_FCFS_Cap_WB(buf_size, bank);
                    default:
                        Debug.Assert(false);
                        return null;
                }

            switch (mem_sched_algo) {
                
                //LAS_BA --- start
                case MemSchedAlgo.LAS_BA_FR:
                    return new LAS_BA_FR(buf_size, bank);

                case MemSchedAlgo.FR_LAS_BA:
                    return new FR_LAS_BA(buf_size, bank);

                case MemSchedAlgo.LAS_BA2_FR:
                    return new LAS_BA2_FR(buf_size, bank);

                //LAS_BA --- end

                case MemSchedAlgo.FairMemScheduler:
                    return new Ideal_MM_MemoryScheduler(buf_size, bank);
                case MemSchedAlgo.LAS_FCFS_F1:
                    return new LAS_FCFS_F1(buf_size, bank);
                case MemSchedAlgo.LAS_FCFS_F2:
                    return new LAS_FCFS_F2(buf_size, bank);

                case MemSchedAlgo.LAS_FCFS:
                    return new LAS_FCFS(buf_size, bank);

                case MemSchedAlgo.LAS_FR_FCFS:
                    return new LAS_FR_FCFS(buf_size, bank);

                case MemSchedAlgo.LAS_QT_FCFS:
                    return new LAS_QT_FCFS(buf_size, bank);
                    
                case MemSchedAlgo.LAS_QT_FR_FCFS:
                    return new LAS_QT_FR_FCFS(buf_size, bank);

                case MemSchedAlgo.FR_LAS_QT_FCFS:
                    return new FR_LAS_QT_FCFS(buf_size, bank);

                case MemSchedAlgo.FR_SLACK_LAS_QT_FCFS:
                    return new FR_SLACK_LAS_QT_FCFS(buf_size, bank);

                case MemSchedAlgo.OUR:
                    return new OUR(buf_size, bank);

                case MemSchedAlgo.LAS_FR_FCFS_FAIR_TS:
                    return new LAS_FR_FCFS_FAIR_TS(buf_size, bank);
                case MemSchedAlgo.LAS_FCFS_FAIR_TS:
                    return new LAS_FCFS_FAIR_TS(buf_size, bank);


                case MemSchedAlgo.FR_LAS_FCFS:
                    return new FR_LAS_FCFS(buf_size, bank);
                case MemSchedAlgo.FRFCFS:
                    Console.WriteLine("FRFCFS initiated");
                    return new FR_FCFS(buf_size, bank);
                case MemSchedAlgo.FCFS:
                    Console.WriteLine("FCFS initiated");
                    return new FCFS(buf_size, bank);
                case MemSchedAlgo.PLL:
                    Console.WriteLine("PLL initiated");
                    return new PLL_MemoryScheduler(buf_size, bank);
                case MemSchedAlgo.PURE_PRIORITY_SCHEME:
                    return new PURE_PRIORITY_SCHEME_MemoryScheduler(buf_size, bank);
                case MemSchedAlgo.STATIC_BATCH:
                    return new STATIC_BATCH(buf_size, bank, Config.memory.ranking_algo, Config.memory.batch_sched_algo);
                case MemSchedAlgo.FULL_BATCH:
                    return new FULL_BATCH(buf_size, bank, Config.memory.ranking_algo, Config.memory.batch_sched_algo);
                case MemSchedAlgo.FULL_BATCH_RV:
                    return new FULL_BATCH_RV(buf_size, bank, Config.memory.ranking_algo, Config.memory.batch_sched_algo);
                case MemSchedAlgo.PERBANK_FULL_BATCH:
                    return new PERBANK_FULL_BATCH_MemoryScheduler(buf_size, bank, Config.memory.ranking_algo, Config.memory.batch_sched_algo);
                case MemSchedAlgo.EMPTY_SLOT_FULL_BATCH:
                    return new EMPTY_SLOT_FULL_BATCH_MemoryScheduler(buf_size, bank, Config.memory.ranking_algo, Config.memory.batch_sched_algo);
                case MemSchedAlgo.STATIC_BATCH_WITH_PRIORITIES:
                    return new STATIC_BATCH_WITH_PRIORITIES_MemoryScheduler(buf_size, bank, Config.memory.ranking_algo);
                case MemSchedAlgo.FULL_BATCH_WITH_PRIORITIES:
                    return new FULL_BATCH_WITH_PRIORITIES_MemoryScheduler(buf_size, bank, Config.memory.ranking_algo);
                case MemSchedAlgo.NesbitBasic:
                    return new Nesbit_Basic_MemoryScheduler(buf_size, bank);
                case MemSchedAlgo.NesbitFull:
                    return new Nesbit_Full_MemoryScheduler(buf_size, bank);
                case MemSchedAlgo.FairMemMicro:
                    return new Ideal_MICRO_MemoryScheduler(buf_size, bank);
                case MemSchedAlgo.FR_FCFS_Cap:
                    return new FR_FCFS_Cap(buf_size, bank);
                default:
                    Debug.Assert(false);
                    return null;
                    
            }//switch

        }//alloc_sched

    }//class Mem
}
