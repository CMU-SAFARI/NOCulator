using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace ICSimulator
{
    public class MemoryConfig : ConfigGroup
    {
        //memory
        //public BitValuePair memoryBV = new BitValuePair(0);
        public int mem_bit = 0;    //log2(number of MCs)
        public int mem_max { get { return (int)1 << mem_bit; } } //number of memory

        //public BitValuePair rowBV = new BitValuePair(6);
        public int row_bit = 6;

        //public BitValuePair bankBV = new BitValuePair(3);
        public int bank_bit = 2; //log2(number of banks)
        public int bank_max_per_mem { get { return (int)1 << bank_bit; } }     //number of banks per memory

        //memory scheduler buffer
        public int buf_size_per_proc = 0;
        public int buf_size_per_mem = 128 * 8;

        //latency
        public int bus_busy_time = 4;          //memory bus transfer time in processor cycles
        public int row_closed_latency = 140;   //closed row, access time in processor cycles
        public int row_hit_latency = 100;      //opened row, row hit, access time in processor cycles
        public int row_conflict_latency = 170; //opened row, row conflict, access time in processor cycles

        /****************************************************************
         *  ------ General memory scheduler parameters -----
         ***************************************************************/
        //shared memory controller (central arbiter)
        public bool is_shared_MC = false;        //whether all memory share the same controller

        //memory scheduling algorithm
        public MemSchedAlgo mem_sched_algo = MemSchedAlgo.FULL_BATCH;    //memory scheduling algorithm

        //memory address mapping
        public AddressMap address_mapping = AddressMap.BMR;

        //writeback
        public int ignore_wb = 1;                //do not generate writeback requests for evicted cache blocks
        public bool wb_special_sched = false;    //special scheduling consideration for writeback requests
        public double wb_full_ratio = 0.7;       //threshold until non-writeback requests are prioritized

        //row access latency
        public bool row_same_latency = false;    //same row access latencies for both row hits and misses

        //idleness
        public int[] idle_start_shift;
        public int[] idle_freq;        // After this many memory instructions, insert an idleness period
        public int[] idle_duration;    // In each idle period, add this many non-memory instructions!

        /****************************************************************
         *  ------ Parameters for for LAS scheduling algorithms -----
         ***************************************************************/
        public int max_wait_thresh = 20000;
        public int quantum_size = 10000;
        public bool las_reset = true;
        public ulong las_periodic_reset = 0;
        public bool service_overlap = false;
        public int time_decay = 0;

        //LAS BATCH
        public int LAS_BA_batch_cycles = 1000;
        public int LAS_BA_threshold_cycles = 1000;
        public double LAS_BA_history_weight = 0;

          //OUR
            public int OUR_threshold_cycles = 10000;
            public int OUR_miniframe_cycles = 1000;
            public int OUR_frame_cycles = 10000;

        /****************************************************************
         *  ------ Parameters for for batch scheduling algorithms -----
         ***************************************************************/
        public BatchSchedAlgo batch_sched_algo = BatchSchedAlgo.MARKED_RANK_FR_FCFS;
        public RankAlgo ranking_algo = RankAlgo.MAX_TOT_RANKING;

        //scheduler: all batching based schedulers
        public int batch_cap = 5;                        //maximum requests from a single thread to a single thread that is included in batch.

        //scheduler: FULL_BATCH_WITH_PRIORITIES
        public bool load_balance0 = false;
        public bool load_balance1 = false;

        public bool constant_rerank = false;

        //scheduler: FULL_BATCH_WITH_PRIORITIES
        public ulong full_batch_min_batch_length = 800;  //parameter for FULL_BATCH_WITH_PRIORITIES scheduler; in processor cycles

        //schedulers: {FULL, }_BATCH_WITH_PRIORITIES
        public int prio_max = 11;                        //parameter for {FULL, }_BATCH_WITH_PRIORITIES scheduler; 0~9 are real priorities, 10 is no-priority

        //schedulers: _BATCH{, _WITH_PRIORITIES}
        public ulong recomp_interval = 800;              //parameter for _BATCH{, _WITH_PRIORITIES scheduler}; rank recomputation interval in memory cycles

        //ranking: PERBANK_LP_RANKING
        public int k_value = 0;                          //parameter for PERBANK_LP_RANKING ranking algorithm; how many processors to exclude for linear programming

        //?????
        public ulong mark_interval = 800;                //?????; input in processor clock cycles
        public int ACTSamplingBatchInterval = 0;         //?????
        public ulong tavgNumReqPBPerProcRemark;          //?????

        /****************************************************************
         *  ------ Parameters for for other scheduling algorithms -----
         ***************************************************************/
        //scheduler: FR_FCFS_Cap
        public int row_hit_cap = 4;              //parameter for FR_FCFS_Cap scheduler; the maximum cap consecutive hits allowed 

        //scheduler: Ideal_MM scheduler
        public double alpha = 1.1;                 //parameter for Ideal_MM scheduler
        public ulong beta = (ulong)1 << 24;               //parameter for Ideal_MM scheduler
        public int sample_min = 25;              //parameter for Ideal_MM scheduler

        //scheduler: Ideal_MICRO (STFM) scheduler
        public double paral_factor = 0.5;        //parameter for Ideal_MICRO (STFM) scheduler
        public int ignore_paral = 0;             //parameter for Ideal_MICRO (STFM) scheduler

        //schedulers: FR_FCFS_Cap, NesbitFull
        public ulong prio_inv_thresh = 0;        //parameter for FR_FCFS_Cap, NesbitFull schedulers; in memory cycles

        //schedulers: Ideal_MM, Ideal_MICRO (STFM), Nesbit{Basic, Full}
        public int use_weight = 0;               //parameter for IIdeal_MM, Ideal_MICRO (STFM), Nesbit{Basic, Full} schedulers
        public double[] weight;                  //parameter for IIdeal_MM, Ideal_MICRO (STFM), Nesbit{Basic, Full} schedulers

        /*
        public  ulong[] accessDelayConflictHistogram = new ulong[2000];
        public  ulong[] accessDelaySameHistogram = new ulong[2000];
        */
        public List<Coord> MCLocations = new List<Coord>(); //summary> List of node coordinates with an MC </summary>

        protected override bool setSpecialParameter(string flag_type, string flag_val)
        {
            string[] values = new string[Config.N];
            char[] splitter = { ',' };

            switch (flag_type)
            {
                //LAS_BA --- start
                case "batch_cycles":
                    LAS_BA_batch_cycles = int.Parse(flag_val); break;

                case "threshold_cycles":
                    LAS_BA_threshold_cycles = int.Parse(flag_val); break;

                case "OUR_threshold_cycles":
                    OUR_threshold_cycles = int.Parse(flag_val); break;

                case "miniframe_cycles":
                    OUR_miniframe_cycles = int.Parse(flag_val); break;

                case "frame_cycles":
                    OUR_frame_cycles = int.Parse(flag_val); break;

                case "history_weight":
                    LAS_BA_history_weight = double.Parse(flag_val); break;
                //LAS_BA --- end

                case "TimeDecay":
                    time_decay = int.Parse(flag_val); break;
                
                case "AddressMap":
                    switch (flag_val) {
                        case "BMR":
                            address_mapping = AddressMap.BMR; break;
                        case "BRM":
                            address_mapping = AddressMap.BRM; break;
                        case "MBR":
                            address_mapping = AddressMap.MBR; break;
                        case "MRB":
                            address_mapping = AddressMap.MRB; break;
                        default:
                            Console.WriteLine("AddressMap " + flag_val + " not found");
                            Environment.Exit(-1);
                            break;
                    }
                    break;
                
                case "MaxWait":
                    max_wait_thresh = int.Parse(flag_val); break;
                    /*
                case "NrOfMemBits":
                    mem_bit = int.Parse(flag_val); break;
                case "NrOfCacheLineBits":
                    row_bit = int.Parse(flag_val); break;
                case "NrOfBankIndexBits":
                    bank_bit = int.Parse(flag_val); break;
                case "NumberOfMemory":
                    mem_max = int.Parse(flag_val); break;
                    */
                case "IsSharedMC":
                    is_shared_MC = bool.Parse(flag_val); break;
                case "BufferSizePerThread":
                    buf_size_per_proc = int.Parse(flag_val); break;
                case "TotalBufferSize":
                    buf_size_per_mem = int.Parse(flag_val); break;
                case "ClosedRowBufferTime":
                    row_closed_latency = (int)(int.Parse(flag_val)); break;
                case "RowBufferHit":
                    row_hit_latency = (int)(int.Parse(flag_val)); break;
                case "RowCap": // old CapThreshold parameter except enforces a cap based on number of row hits serviced before a conflict
                    row_hit_cap = (int)(int.Parse(flag_val)); break;
                case "BankConflictTime":
                    row_conflict_latency = (int)(int.Parse(flag_val)); break;
                case "RowBufferDisabled":
                    row_same_latency = (bool)(bool.Parse(flag_val)); Console.WriteLine("Row buffer " + row_same_latency); break;
                case "Alpha":
                    alpha = double.Parse(flag_val, NumberStyles.Float); break;
                case "Beta":
                    beta = ulong.Parse(flag_val); break;
                case "MinNrOfSamples":
                    sample_min = int.Parse(flag_val); break;
                case "ParallelismFactor":
                    paral_factor = double.Parse(flag_val, NumberStyles.Float); break;
                case "MinimumBatchLength":
                    full_batch_min_batch_length = ulong.Parse(flag_val); break;
                case "IgnoreParallelism":
                    ignore_paral = int.Parse(flag_val); break;
                case "IgnoreWritebacks":
                    ignore_wb = int.Parse(flag_val); break;
                case "UseWeights":
                    use_weight = int.Parse(flag_val); break;
                case "RecomputationPeriod":
                    recomp_interval = (ulong.Parse(flag_val)); Console.WriteLine(recomp_interval); break;
                case "MarkingPeriod":
                    mark_interval = (ulong.Parse(flag_val)); Console.WriteLine(mark_interval); break;
                case "CapThreshold":
                    prio_inv_thresh = (ulong.Parse(flag_val)); break;
                case "WBFillFraction":
                    wb_full_ratio = double.Parse(flag_val, NumberStyles.Float); break;
                case "WBSpecialScheduling":
                    int temp = int.Parse(flag_val);
                    if (temp == 0)
                        wb_special_sched = false;
                    else if (temp == 1)
                        wb_special_sched = true;
                    break;
                case "IdlenessFrequency":
                    values = flag_val.Split(splitter);
                    for (int p = 0; p < values.Length; p++)
                    {
                        idle_freq[p] = int.Parse(values[p]);
                    }
                    for (int p = values.Length; p < Config.N; p++)
                    {
                        idle_freq[p] = 0;
                    }
                    break;
                case "IdlenessDuration":
                    values = flag_val.Split(splitter);
                    for (int p = 0; p < values.Length; p++)
                    {
                        idle_duration[p] = int.Parse(values[p]);
                    }
                    for (int p = values.Length; p < Config.N; p++)
                    {
                        idle_duration[p] = 0;
                    }
                    break;
                case "Weights":
                    values = flag_val.Split(splitter);
                    for (int p = 0; p < values.Length; p++)
                    {
                        weight[p] = double.Parse(values[p], NumberStyles.Float);
                    }
                    break;
                case "IdlenessStartShift":
                    values = flag_val.Split(splitter);
                    for (int p = 0; p < values.Length; p++)
                    {
                        idle_start_shift[p] = int.Parse(values[p]);
                    }
                    for (int p = values.Length; p < Config.N; p++)
                    {
                        idle_start_shift[p] = 0;
                    }
                    break;
                case "BatchingCap":
                    batch_cap = int.Parse(flag_val); break;
                case "LoadBalance0":
                    load_balance0 = bool.Parse(flag_val); break;
                case "LoadBalance1":
                    load_balance1 = bool.Parse(flag_val); break;
                case "ConstantRerank":
                    constant_rerank = bool.Parse(flag_val); break;

                
                //Ranking Algorithm
                case "RankingScheme":
                    switch (flag_val) {
                        case "ect":
                            ranking_algo = RankAlgo.ECT_RANKING;
                            break;
                        case "ect-rv":
                            ranking_algo = RankAlgo.ECT_RV_RANKING;
                            break;
                        case "max-tot":
                            ranking_algo = RankAlgo.MAX_TOT_RANKING;
                            break;
                        case "tot-max":
                            ranking_algo = RankAlgo.TOT_MAX_RANKING;
                            break;
                        case "max-tot-reverse":
                            ranking_algo = RankAlgo.MAX_TOT_REVERSE_RANKING;
                            break;
                        case "round-robin":
                            ranking_algo = RankAlgo.ROUND_ROBIN_RANKING;
                            break;
                        case "random":
                            ranking_algo = RankAlgo.RANDOM_RANKING;
                            break;
                        case "no-ranking":
                            ranking_algo = RankAlgo.NO_RANKING;
                            break;
                        case "rba-max-tot":
                            ranking_algo = RankAlgo.ROW_BUFFER_AWARE_MAX_RANKING;
                            break;
                        case "lp":
                            ranking_algo = RankAlgo.PERBANK_LP_RANKING;
                            break;
                        case "sjf":
                            ranking_algo = RankAlgo.PERBANK_SJF_RANKING;
                            break;
                        default:
                            Console.WriteLine("Ranking Scheme " + flag_val + " not found");
                            Environment.Exit(-1);
                            break;
                    }
                    break;

                //Within Batch Priority
                case "WithinBatchPriority":
                    switch (flag_val) {
                        case "rank-fr-fcfs":
                            batch_sched_algo = BatchSchedAlgo.MARKED_RANK_FR_FCFS;
                            break;
                        case "fr-rank-fcfs":
                            batch_sched_algo = BatchSchedAlgo.MARKED_FR_RANK_FCFS;
                            break;
                        case "rank-fcfs":
                            batch_sched_algo = BatchSchedAlgo.MARKED_RANK_FCFS;
                            break;
                        case "fr-fcfs":
                            batch_sched_algo = BatchSchedAlgo.MARKED_FR_FCFS;
                            break;
                        case "fcfs":
                            batch_sched_algo = BatchSchedAlgo.MARKED_FCFS;
                            break;
                        default:
                            Console.WriteLine("WithinBatchPriority " + flag_val + " not found");
                            Environment.Exit(-1);
                            break;
                    }
                    break;
                

                case "ACTSampleInterval":
                    ACTSamplingBatchInterval = int.Parse(flag_val); break;
                case "kValue":
                    k_value = int.Parse(flag_val); break;

                /*********************
                 * Scheduling Algorithm
                 ********************/

                case "LasReset":
                    las_reset = bool.Parse(flag_val); break;

                case "ServiceOverlap":
                    service_overlap = bool.Parse(flag_val); break;

                case "LasPeriodicReset":
                    las_periodic_reset = ulong.Parse(flag_val); break;

                case "RamAlgorithm":
                    mem_sched_algo = (MemSchedAlgo)Enum.Parse(typeof(MemSchedAlgo), flag_val); break;

                case "MCLocations":
                    parseMCLocations(flag_val); break;
                default:
                    return false;
            }
            return true;
        }

        public override void finalize()
        {
            /*
            // Verify the MCLocations list
            if (MCLocations.Count == 0)
            {
                MCLocations.Add(new Coord(0, 0));
                MCLocations.Add(new Coord(0, Config.network_nrY - 1));
                MCLocations.Add(new Coord(Config.network_nrX - 1, 0));
                MCLocations.Add(new Coord(Config.network_nrX - 1, Config.network_nrY - 1));
                            
                memoryBV.bits = 2;
            }
            */

			if ((Config.ScalableRingClustered || Config.topology != Topology.Mesh) && Config.sh_cache_perfect == false)
			{
				if (mem_max != 4) 
					throw new Exception("Only 4 MCs are supported in ScalableRC or Hierarchical Ring");				
				MCLocations.Add(new Coord(0));
				MCLocations.Add(new Coord(5));
				MCLocations.Add(new Coord(11));
				MCLocations.Add(new Coord(14));
				return;
			}
            
            MCLocations.Add(new Coord(0, 0));
            if(mem_max == 1) return;
            
            MCLocations.Add(new Coord(0, Config.network_nrY - 1));
            if(mem_max == 2) return;
            
            MCLocations.Add(new Coord(Config.network_nrX - 1, 0));
            if(mem_max == 3) return;
            
            MCLocations.Add(new Coord(Config.network_nrX - 1, Config.network_nrY - 1));
            if(mem_max == 4) return;
            
        }

        public void parseMCLocations(string mcLocationsToBeConverted)
        {
            Char[] delims = new Char[] { '(', ',', ' ', ')' };
            string[] split = mcLocationsToBeConverted.Split(delims);
            for (int i = 1; i < split.Length - 1; i += 4)
            {
                MCLocations.Add(new Coord(Int32.Parse(split[i]), Int32.Parse(split[i + 1])));
            }
            //Yoongu
            //memoryBV.val = (ulong)MCLocations.Count;
        }
    }
}
