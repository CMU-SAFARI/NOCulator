using System;
using System.Collections.Generic;
using System.Text;
/*
using System.IO;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
*/
using lpsolve55;

//namespace ICSimulator
//{
//    static class MemConfig
//    {
//        /****************************************************************
//         *  ------ Components -----
//         ***************************************************************/
//        //public static MemCtlr[] mem;       //memory
//        //public static Bank[][] bank;    //bank
//
//        /****************************************************************
//         *  ------ Statistics and reporting -----
//         ***************************************************************/
//        //public static SimStat stat;             //statistics for the simulation
//        //public static string report_name;       //serves as a basis for multiple report file names
//        //public static TextWriter writer;      //for console and file I/O
//
//
//        /****************************************************************
//         *  ------ Simulation parameters -----
//         ***************************************************************/
//        //simulation parameters
//        /*
//        // processor
//        public static int second_chance = 0;
//        public static ulong sim_cycle_max = 0;
//
//        //architecture parameters
//        public static int Simulator.NumberOfApplication_Processors = 1;                 //number of processors
//        public static int inst_wnd_size = 128;          //size of instruction window
//        public static bool is_shared_cache = false;     //whether all processors share the same cache
//
//        //cache
//        public static int assoc = 1 << 4;               //associativity
//        public static int cache_size = 1 << 20;         //size of cache in bytes
//        public static int block_size = 1 << block_bit;  //size of cache block in bytes
//        public static int block_bit = 5;                //log2(number of bytes in a cache block in bytes)
//        */
//        //memory
//        public static int mem_bit = 0;                          //log2(number of MCs)
//        public static int mem_max = 1 << mem_bit;               //number of memory
//        public static int row_bit = 6;                          //log2(number of cache blocks in a row)
//        public static int bank_bit = 3;                         //log2(number of banks)
//        public static int bank_max_per_mem = 1 << bank_bit;     //number of banks per memory
//
//        //memory scheduler buffer
//        public static int buf_size_per_proc = 0;
//        public static int buf_size_per_bank = 128 * 8;
//
//        //timing
//        public static int cycle_ratio = 2;              //processor to memory clock frequency ratio
//        private static int cycle_count = cycle_ratio;   //counting down
//
//        //latency
//        public static int bus_busy_time = 4 / cycle_ratio;          //memory bus transfer time in processor cycles
//        public static int row_closed_latency = 300 / cycle_ratio;   //closed row, access time in processor cycles
//        public static int row_hit_latency = 200 / cycle_ratio;      //opened row, row hit, access time in processor cycles
//        public static int row_conflict_latency = 400 / cycle_ratio; //opened row, row conflict, access time in processor cycles
//
//        /****************************************************************
//         *  ------ General memory scheduler parameters -----
//         ***************************************************************/
//        //shared memory controller (central arbiter)
//        public static bool is_shared_MC = false;        //whether all memory share the same controller
//
//        //memory scheduling algorithm
//        public static MemSchedAlgo mem_sched_algo = MemSchedAlgo.FULL_BATCH;    //memory scheduling algorithm
//
//        //memory address mapping
//        public static AddressMap address_mapping = AddressMap.MRB;
//
//        //writeback
//        public static int ignore_wb = 1;                //do not generate writeback requests for evicted cache blocks
//        public static bool wb_special_sched = false;    //special scheduling consideration for writeback requests
//        public static double wb_full_ratio = 0.7;       //threshold until non-writeback requests are prioritized
//
//        //row access latency
//        public static bool row_same_latency = false;    //same row access latencies for both row hits and misses
//
//        //idleness
//        public static int[] idle_start_shift;
//        public static int[] idle_freq;        // After this many memory instructions, insert an idleness period
//        public static int[] idle_duration;    // In each idle period, add this many non-memory instructions!
//
//        /****************************************************************
//         *  ------ Parameters for for LAS scheduling algorithms -----
//         ***************************************************************/
//        public static int max_wait_thresh = 20000;
//        public static int quantum_size = 10000;
//        public static bool las_reset = true;
//        public static ulong las_periodic_reset = 0;
//        public static bool service_overlap = false;
//        public static int time_decay = 0;
//
//        //LAS BATCH
//        public static int LAS_BA_batch_cycles = 1000;
//        public static int LAS_BA_threshold_cycles = 1000;
//        public static double LAS_BA_history_weight = 0;
//
//        /****************************************************************
//         *  ------ Parameters for for batch scheduling algorithms -----
//         ***************************************************************/
//        public static BatchSchedAlgo batch_sched_algo = BatchSchedAlgo.MARKED_RANK_FR_FCFS;
//        public static RankAlgo ranking_algo = RankAlgo.MAX_TOT_RANKING;
//
//        //scheduler: all batching based schedulers
//        public static int batch_cap = 5;                        //maximum requests from a single thread to a single thread that is included in batch.
//
//        //scheduler: FULL_BATCH_WITH_PRIORITIES
//        public static bool load_balance0 = false;
//        public static bool load_balance1 = false;
//
//        public static bool constant_rerank = false;
//
//        //scheduler: FULL_BATCH_WITH_PRIORITIES
//        public static ulong full_batch_min_batch_length = 800;  //parameter for FULL_BATCH_WITH_PRIORITIES scheduler; in processor cycles
//
//        //schedulers: {FULL, STATIC}_BATCH_WITH_PRIORITIES
//        public static int prio_max = 11;                        //parameter for {FULL, STATIC}_BATCH_WITH_PRIORITIES scheduler; 0~9 are real priorities, 10 is no-priority
//
//        //schedulers: STATIC_BATCH{, _WITH_PRIORITIES}
//        public static ulong recomp_interval = 800;              //parameter for STATIC_BATCH{, _WITH_PRIORITIES scheduler}; rank recomputation interval in memory cycles
//
//        //ranking: PERBANK_LP_RANKING
//        public static int k_value = 0;                          //parameter for PERBANK_LP_RANKING ranking algorithm; how many processors to exclude for linear programming
//
//        //?????
//        public static ulong mark_interval = 800;                //?????; input in processor clock cycles
//        public static int ACTSamplingBatchInterval = 0;         //?????
//        public static ulong tavgNumReqPBPerProcRemark;          //?????
//
//        /****************************************************************
//         *  ------ Parameters for for other scheduling algorithms -----
//         ***************************************************************/
//        //scheduler: FR_FCFS_Cap
//        public static int row_hit_cap = 4;              //parameter for FR_FCFS_Cap scheduler; the maximum cap consecutive hits allowed 
//
//        //scheduler: Ideal_MM scheduler
//        public static double alpha = 1.1;                 //parameter for Ideal_MM scheduler
//        public static ulong beta = (ulong)1 << 24;               //parameter for Ideal_MM scheduler
//        public static int sample_min = 25;              //parameter for Ideal_MM scheduler
//
//        //scheduler: Ideal_MICRO (STFM) scheduler
//        public static double paral_factor = 0.5;        //parameter for Ideal_MICRO (STFM) scheduler
//        public static int ignore_paral = 0;             //parameter for Ideal_MICRO (STFM) scheduler
//
//        //schedulers: FR_FCFS_Cap, NesbitFull
//        public static ulong prio_inv_thresh = 0;        //parameter for FR_FCFS_Cap, NesbitFull schedulers; in memory cycles
//
//        //schedulers: Ideal_MM, Ideal_MICRO (STFM), Nesbit{Basic, Full}
//        public static int use_weight = 0;                           //parameter for IIdeal_MM, Ideal_MICRO (STFM), Nesbit{Basic, Full} schedulers
//        public static double[] weight; //parameter for IIdeal_MM, Ideal_MICRO (STFM), Nesbit{Basic, Full} schedulers
//
//        /****************************************************************
//         *  ------ Etc -----
//         ***************************************************************/
//        public static Random rand = new Random(0);
//
//
//        public static ulong[] accessDelayConflictHistogram = new ulong[2000];
//        public static ulong[] accessDelaySameHistogram = new ulong[2000];
//        /**
//         * The simulator
//         */
//        /*
//        static void Main(string[] args)
//        {
//            //load simulation setting from config file
//            Config.read(config_file);
//
//            //load simulation setting from arguments
//            Argument.read(args);
//
//            //simulation thread's priority; we don't want it to hang the computer
//            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
//
//            //init system parameters
//            init_parameters();
//
//            //init scheduling algorithm parameters
//            init_sched_algo();
//
//            //initialize
//            init_components();
//
//            //run the simulation
//            run_simulation();
//
//            //report results
//            stat.report();
//        }*/
//
//        /*
//        static void init_parameters()
//        {
//            mem_max = 1 << mem_bit;
//            bank_max_per_mem = 1 << bank_bit;
//            //block_size = 1 << block_bit;
//
//
//            idle_start_shift = new int[Config.N];
//            idle_freq = new int[Config.N];        // After this many memory instructions, insert an idleness period
//            idle_duration = new int[Config.N];    // In each idle period, add this many non-memory instructions!
//            weight = new double[Config.N];
//        }
//
//        static void init_sched_algo()
//        {
//            //Nesbit{Basic, Full} schedulers: weights must be normalized
//            if (use_weight != 0)
//            {
//                if ((mem_sched_algo == MemSchedAlgo.NesbitBasic) || (mem_sched_algo == MemSchedAlgo.NesbitFull))
//                {
//                    //calculate total weight
//                    double total_weight = 0;
//                    for (int i = 0; i < Config.N; i++)
//                        total_weight += weight[i];
//
//                    //normalize weights
//                    for (int i = 0; i < Config.N; i++)
//                    {
//                        weight[i] = weight[i] / total_weight;
//                        Console.WriteLine("Processor " + i + " weight: " + weight[i]);
//                    }
//                }
//            }
//
//            //PERBANK_FULL_BATCH scheduler && PERBANK_LP_RANKING ranking algorithm: linear-programming is used
//            if ((mem_sched_algo == MemSchedAlgo.PERBANK_FULL_BATCH) && (ranking_algo == RankAlgo.PERBANK_LP_RANKING))
//                lpsolve.Init(".");
//        }
//        */
//
//        /*public static void init_separate_MC()
//        {
//            List<Node> proc = new List<Node>();
//
//            Coord[] locations = Config.MCLocations.ToArray();
//            mem_bit = (int)Math.Log(locations.Length, 2);
//            if ((int)Math.Ceiling(Math.Log(locations.Length, 2)) != (int)Math.Floor(Math.Log(locations.Length, 2)))
//                throw new Exception("Number of Memory Controllers is not a power of two");
//
//            mem_max = 1 << mem_bit;
//            for (int i = 0; i < locations.Length; i++)
//            {
//                proc.Add(Simulator.Node[locations[i].x, locations[i].y]);
//                //Console.WriteLine(locations[i].ToString());
//            }
//
//            bank = new Bank[mem_max][];
//            for (int m = 0; m < mem_max; m++)
//            {
//                bank[m] = new Bank[bank_max_per_mem];
//            }
//
//            //allocate memory
//            mem = new MemCtlr[mem_max];
//            for (int m = 0; m < mem_max; m++)
//            {
//                //locally visible bank
//                bank[m] = new Bank[bank_max_per_mem];
//
//                //allocate scheduler (sees local banks)
//                Console.WriteLine("");
//                MemSched sched = MemCtlr.alloc_sched(bank[m], buf_size_per_bank, mem_sched_algo, wb_special_sched);
//
//                //allocate individual memory
//                mem[m] = new MemCtlr(sched, bank[m], proc[m]);
//
//                //allocate individual banks
//                for (int b = 0; b < bank_max_per_mem; b++)
//                {
//                    bank[m][b] = new Bank(sched, mem[m]);
//                }
//
//                //assign MC to a Processor
//                proc[m].addMC(mem[m]);
//            }
//
//        }*/
//        /*
//        static void init_components()
//        {
//            //init stat
//            //stat = new SimStat(report_name);
//
//            //allocate banks, memory, scheduler
//        }
//
//        static void cycle_memory()
//        {
//            //progress time for memory
//            if (cycle_count == 0)
//            {
//                for (int i = 0; i < mem_max; i++)
//                {
//                    mem[i].tick();
//                }
//                cycle_count = cycle_ratio;
//            }
//        }*/
//        /*
//        static void collect_all_stats()
//        {
//            for (int p = 0; p < Simulator.NumberOfApplication_Processors; p++) {
//                Proc cur_proc = proc[p];
//                proc[p].freeze_stat();
//            }
//        }
//
//
//        static void collect_stats(int threadID)
//        {
//            proc[threadID].freeze_stat();
//        }
//
//        public static Proc get_proc(int threadID)
//        {
//            return proc[threadID];
//        }
//        */
//        public static MemCtlr get_mem(int mem_index)
//        {
//            //return mem[mem_index];
//            throw new NotImplementedException();
//        }
//    }//class
//}//namespace
