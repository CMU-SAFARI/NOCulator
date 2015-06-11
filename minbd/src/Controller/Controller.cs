using System;
using System.Collections.Generic;

namespace ICSimulator
{
    /*
     *   * prioritization
     *       * rankFlits(flit1, flit2) -- used for in-router/in-network arbitration
     *       * newPrioPktPool() -- returns a new ``priority pool'' (for inj queue, possibly buffers later)
     *       * addPacket(pkt) -- adds packet to pool (controller chooses impl)
     *       * next() -- returns "next" packet
     *
     *   * throttling
     *       * tryInject(nodeID) -- returns true to allow injection
     *
     *   * mapping
     *       * mapApp(appID) -- need to think about how to do this. split out CPU and thread-context?
     *       * mapCache(appID, block)
     *       * mapMC(appID, block)
     *
     *   * clock
     *       * doStep
     */

    // when adding a new controller, modify Controller.construct() as well as Controller_Mix.getController()
    public enum ControllerType
    {
        CLASSIC,
        THROTTLE,
        STC,
        MIX,
        SIMPLEMAP,
        GLOBAL_RR,
        GLOBAL_RR_BIN,
        STATIC,
        GLOBAL_HIGH,
        RATE,
        MPKI_CLUSTER,
        GLOBAL_RR_DYN,
        GLOBAL_RR_NETUTIL,
        GLOBAL_BATCH,
        GLOBAL_BATCH_FRAC,
        GLOBAL_ADAPTIVE,
        GLOBAL_ADAPTIVE_DIST,
        GLOBAL_ADAPTIVE_SHUFFLE,
        UNIFORM_BATCH,
        SELFTUNED,
        THREE_LEVEL,
        NETGAIN,
        NONE
    }

    /* base class. implements default behavior
       (random ranking, FIFO queues, striped mapping) */
    public class Controller
    {

        public static Controller construct()
        {
            switch (Config.controller)
            {
                case ControllerType.CLASSIC:
                    return new Controller_ClassicBLESS();
                case ControllerType.THROTTLE:
                    return new Controller_Throttle();
                case ControllerType.STC:
                    return new Controller_STC();
                case ControllerType.MIX:
                    return new Controller_Mix();
                case ControllerType.SIMPLEMAP:
                    return new Controller_SimpleMap();
                case ControllerType.GLOBAL_RR:
                    return new Controller_Global_round_robin();
                case ControllerType.GLOBAL_RR_BIN:
                    return new Controller_Global_round_robin_group();
                case ControllerType.GLOBAL_HIGH:
                    return new Controller_Global_Throttle_High();
                //Config file to change the static throttle rate: Config.sweep_th_rate
                case ControllerType.STATIC:
                    return new Controller_Static();
                case ControllerType.MPKI_CLUSTER:
                    return new Controller_MPKI_cluster();
                case ControllerType.RATE:
                    return new Controller_Rate();
                case ControllerType.GLOBAL_RR_DYN:
                    return new Controller_Global_round_robin_deflection();
                case ControllerType.NONE:
                    return new Controller();
                case ControllerType.GLOBAL_RR_NETUTIL:
                    return new Controller_Global_round_robin_netutil();
                case ControllerType.GLOBAL_BATCH:
                    return new Controller_Global_Batch();
                case ControllerType.GLOBAL_BATCH_FRAC:
                    return new Controller_Global_Batch_fraction();
                case ControllerType.GLOBAL_ADAPTIVE:
                    return new Controller_Adaptive_Cluster();
                case ControllerType.GLOBAL_ADAPTIVE_SHUFFLE:
                    return new Controller_Adaptive_Cluster_shuffle();
                case ControllerType.UNIFORM_BATCH:
                    return new Controller_Uniform_Batch();
                case ControllerType.GLOBAL_ADAPTIVE_DIST:
                    return new Controller_Adaptive_Cluster_dist();
                case ControllerType.SELFTUNED:
                    return new Controller_SelfTuned();
                case ControllerType.THREE_LEVEL:
                    return new Controller_Three_Level_Cluster();
                case ControllerType.NETGAIN:
                    return new Controller_NetGain_Three_Level_Cluster();
                default:
                    return new Controller();
            }
        }
        // for RR-throttling used in Controller_Global_RR.cs
        // this is miss-per-kilo-inst-per-epoch, reset every epoch
        public double[] MPKI = new double[Config.N];
        public double[] prev_MPKI = new double[Config.N];
        //TODO: numinject is obsolete now since this will not be needed.
        //If num of flits being injected is needed, use inject_flit_bysrc.
        public ulong[] numInject = new ulong[Config.N];
        public ulong[] L1misses = new ulong[Config.N];
        // record the number of instructions of the last epoch, use for MPKI
        public ulong[] num_ins_last_epoch = new ulong[Config.N];

        IPrioPktPool[] m_injPools = new IPrioPktPool[Config.N];
        
        public static int smallest_mpki = 0;
        public static int largest_mpki  = 0;

        public Controller()
        {
        }

        /* ------------- PRIORITIZATION ---------------- */

        // rank flits at router arbitration
        public virtual int rankFlits(Flit f1, Flit f2)
        {
            return Simulator.rand.Next(3) - 1; // one of {-1, 0, 1}
        }

        public virtual IPrioPktPool newPrioPktPool(int node) // used to alloc inj pools
        {
            //return new FIFOPrioPktPool();
            return MultiQThrottlePktPool.construct();
        }

        public virtual void setInjPool(int node, IPrioPktPool pool)
        {
            m_injPools[node] = pool;
            pool.setNodeId(node);
        }

        /* ------------- THROTTLING ---------------- */

        // called by router to report a starvation cycle
        public virtual void reportStarve(int node)
        {
        }

        public virtual bool ThrottleAtRouter { get { return false; } }

        // true to allow injection, false to block (throttle)
        public virtual bool tryInject(int node)
        {
            return true;
        }

        public virtual void resetStat()
        {
            for (int i = 0; i < Config.N; i++)
            {
                numInject[i]=0;
		        L1misses[i]=0;
            }
        }

        /* ------------ MAPPING --------------- */

        public virtual int mapApp(int appID)
        {
            return appID;
        }

        public virtual int mapCache(int appID, ulong block)
        {
            return (int)(block % (ulong)Config.N);
        }

        public virtual int mapMC(int appID, ulong block)
        {
            return Config.memory.MCLocations[MemoryRequest.mapMC(block)].ID;
        }

        // returns -1 for hit-in-L2, or nonzero latency for miss
        public virtual int memMiss(int appID, ulong block)
        {
            return -1;
        }

        /* ---------- PERIODIC UPDATES ----------- */
        public virtual void doStep()
        {
            //record mpki vals every 20k cycles
            if (Simulator.CurrentRound > (ulong)Config.warmup_cyc &&
                    (Simulator.CurrentRound % (ulong)20000) == 0)
            {
                for (int i = 0; i < Config.N; i++)
                {
                    prev_MPKI[i]=MPKI[i];
                    if(num_ins_last_epoch[i]==0)
                        MPKI[i]=((double)(L1misses[i]*1000))/(Simulator.stats.insns_persrc[i].Count);
                    else
                    {
                        if(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]>0)
                            MPKI[i]=((double)(L1misses[i]*1000))/(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]);
                        else if(Simulator.stats.insns_persrc[i].Count-num_ins_last_epoch[i]==0)
                            MPKI[i]=0;
                        else
                            throw new Exception("MPKI error!");
                    }
                }

                double mpki_sum=0;
                double mpki=0;
                //int smallest_mpki = 0;
                //int largest_mpki  = 0;
                for(int i=0;i<Config.N;i++)
                {
                    mpki=MPKI[i];
                    mpki_sum+=mpki;
                    Simulator.stats.mpki_bysrc[i].Add(mpki);
                    if(mpki < MPKI[smallest_mpki])
                        smallest_mpki = i;
                    if(mpki > MPKI[largest_mpki])
                        largest_mpki  = i;
                }
                Simulator.stats.total_sum_mpki.Add(mpki_sum);
                

            }
        }
    }

    /* "classic BLESS" controller: OF ranking, multiple injection queues */
    public class Controller_ClassicBLESS : Controller
    {
        public override int rankFlits(Flit f1, Flit f2)
        {
            return Router_Flit_OldestFirst._rank(f1, f2);
        }

        public override IPrioPktPool newPrioPktPool(int node)
        {
            return new MultiQPrioPktPool();
        }
    }
}
