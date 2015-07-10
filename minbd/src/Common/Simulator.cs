using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;
using System.Threading;

namespace ICSimulator
{
    public class Simulator
    {
        public static Rand rand;

        // the network (this owns the Routers, the Nodes and the Links)
        public static Network network;
        public static Controller controller;

        public static Stats stats;

        public const int DIR_UP = 0;
        public const int DIR_RIGHT = 1;
        public const int DIR_DOWN = 2;
        public const int DIR_LEFT = 3;
        public const int DIR_LOCAL = 4;
        public const int DIR_BLOCKED = -1;
        public const int DIR_NONE = -99;

        public const int DIR_CW  = 4;
        public const int DIR_CCW = 5;

        public static string[] DEF_args = new string[] {"-config", "..\\..\\..\\config.txt",
                                                        "-workload", "..\\..\\..\\workload_synth", "1"};

        // simulator state
        public static ulong CurrentRound = 0;
        public static bool Warming = false;

        public static ulong CurrentBarrier = 0; // MT workloads


        public static StreamWriter rtStWriter;
        // ready callback and deferred-callback queue
        public delegate void Ready();

        private static PrioQueue<Simulator.Ready> m_deferQueue = new PrioQueue<Simulator.Ready>();

        public static void Main(string[] args)
        {
            System.Diagnostics.Process.Start("hostname");
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            Console.Write( Directory.GetCurrentDirectory());

            Init(args);

            RunSimulationRun();

            Finish();
        }

        public static void Init(string[] args)
        {
            Config config = new Config();

            if (args.Length == 0)
                args = DEF_args;

            config.read(args);

            rand = new Rand(Config.rand_seed);
            CurrentRound = 0;

            controller = Controller.construct();

            network = new Network(Config.network_nrX, Config.network_nrY);

            network.setup();

            Warming = true;
        }

        public static void Finish()
        {
            if (network.isLivelocked())
                Simulator.stats.livelock.Add();

            if (!Config.ignore_livelock && network.isLivelocked())
                Console.WriteLine("STOPPED DUE TO LIVELOCK.");

            Simulator.stats.Finish();
            using (TextWriter tw = new StreamWriter(Config.output, true))
            {
                Simulator.stats.DumpJSON(tw);
                //Simulator.stats.Report(tw);
            }
            if (Config.matlab != "")
                using (TextWriter tw = new StreamWriter(Config.matlab))
                {
                    Simulator.stats.DumpMATLAB(tw);
                }

            Simulator.network.close();
        }
        public static string newFault = "";
        public static void rtReport(bool initReport = false)
        {
            if (initReport)
            {
                rtStWriter.WriteLine("\nRouting algorithm: {0}", Config.router.algorithm);
                rtStWriter.WriteLine("Selection function: {0}", Config.maze.selection);
                rtStWriter.WriteLine("Mesh size: ({0},{1})", Config.network_nrX, Config.network_nrY);
                rtStWriter.WriteLine("Synthetic traffic: {0}, rate: {1} ", Config.synthGen, Config.synthRate);
                rtStWriter.WriteLine("Synthetic traffic increase step: {0}, interval: {1} ", Config.synthRateIncStep, Config.synthRateIncInterval);
                rtStWriter.WriteLine("Initial # of faults: {0}, New fault interval: {1}", Config.fault_initialCount, Config.fault_injectionInterval);
                if (newFault != "")
                    rtStWriter.WriteLine(newFault);
                rtStWriter.WriteLine(
                    "Cycle\t" +
                    "Inj. Rate\t"+
                    "tot. # of injected flits\t" +
                    "tot. # of arrived flits\t" +
                    "av. overall lat.\t" +
                    "av. after change lat.\t"+
                    "avg. interval lat.\t" +
                    "avg. interval hopCnt.\t" +
                    "new fault?\t" +
                    "time stamp");
            }
            rtStWriter.WriteLine("{0}\t{1:0.###}\t{2:#.###}\t{3:#.###}\t{4:#.###}\t{5:#.###}\t{6:#.###}\t{7:#.###}\t{8}\t{9}",
                CurrentRound,
                Config.synthRate,
                Simulator.stats.inject_flit.Count,
                Simulator.stats.eject_flit.Count,
                Simulator.stats.total_latency.Avg,
                Simulator.stats.total_after_change_latency.Avg,
                Simulator.stats.total_interval_latency.Avg,
                Simulator.stats.total_interval_hopCnt.Avg,
                newFault,
                DateTime.Now);

            Simulator.stats.total_interval_latency.Reset();
            Simulator.stats.total_interval_hopCnt.Reset();
            newFault = "";
            
        }

        public static void RunSimulationRun()
        {
           if (File.Exists(Config.output))
            {
                Console.WriteLine("Output file {0} exists; exiting.", Config.output);
                //Environment.Exit(0);
            }
            
            rtStWriter = new StreamWriter(Config.rt_output, true);
            rtReport(true);

            if (Config.RouterEvaluation)
                RouterEval.evaluate();
            else
                RunSimulation();
            rtStWriter.WriteLine("---------------------------------------------\n\n");
            rtStWriter.Close();

            Console.WriteLine("simulation finished");
        }

        public static bool DoStep()
        {
            // handle pending deferred-callbacks first
            while (!m_deferQueue.Empty && m_deferQueue.MinPrio <= Simulator.CurrentRound)
            {
                m_deferQueue.Dequeue() (); // dequeue and call the callback
            }
            if (CurrentRound == (ulong)Config.warmup_cyc)
            {
                Console.WriteLine("done warming");
                //Console.WriteLine("warmup_cyc {0}",Config.warmup_cyc);
                //throw new Exception("done warming");
                Simulator.stats.Reset();
                controller.resetStat();
                WarmingStats();
                Warming = false;
            }
            if (!Warming)
                Simulator.stats.cycle.Add();

            if (CurrentRound % Config.rt_interval == 0)
            {
                rtReport();
            }
			
            if (CurrentRound % 100000 == 0)
			{
                ProgressUpdate();
                if (network.LiveLockedRouters())
                    Console.WriteLine("A livelock exists!!");

                if (Config.fault_injectionInterval > 0 && Warming == false &&
                    CurrentRound % (ulong)Config.fault_injectionInterval == 0)
                {
                    rtStWriter.Flush();
                    if (Config.fault_maxCount == 0 || network.faultCount < Config.fault_maxCount)
                    {
                        network.injectNewFault();
                        stats.total_after_change_latency.Reset();
                    }
                }
                if (Config.synthRateIncStep > 0.0 && Warming == false)
                    if (CurrentRound % (ulong)Config.synthRateIncInterval == 0)
                    {
                        Config.synthRate += Config.synthRateIncStep;
                        stats.total_after_change_latency.Reset();
                        rtStWriter.Flush();
                    }
			}
            CurrentRound++;

            network.doStep();
            controller.doStep();

            return !network.isFinished() && (Config.ignore_livelock || !network.isLivelocked());
        }

        public static void RunSimulation()
        {
            while (DoStep()) ;
        }

        //static bool isLivelock = false;

        static void ProgressUpdate()
        {
            if (!Config.progress) return;

            Console.Out.WriteLine("cycle {0}: {1} flits injected, {2} flits arrived, avg total latency {3}",
                                  CurrentRound,
                                  Simulator.stats.inject_flit.Count,
                                  Simulator.stats.eject_flit.Count,
                                  Simulator.stats.total_latency.Avg);
            Console.WriteLine("TimeStamp = {0}",DateTime.Now);
        }

        static void WarmingStats()
        {
            // TODO: update this for new caches
            /*
            int l1_warmblocks = 0, l1_totblocks = 0;
            int l2_warmblocks = 0, l2_totblocks = 0;

            foreach (Node n in network.nodes)
            {
                l1_warmblocks += n.cpu.Sets.WarmBlocks;
                l1_totblocks += n.cpu.Sets.TotalBlocks;
                l2_warmblocks += n.SharedCache.Sets.WarmBlocks;
                l2_totblocks += n.SharedCache.Sets.TotalBlocks;
            }

            Simulator.stats.l1_warmblocks.Add((ulong)l1_warmblocks);
            Simulator.stats.l1_totblocks.Add((ulong)l1_totblocks);

            Simulator.stats.l2_warmblocks.Add((ulong)l2_warmblocks);
            Simulator.stats.l2_totblocks.Add((ulong)l2_totblocks);
            */
       }

        public static void Defer(Simulator.Ready cb, ulong cyc)
        {
            m_deferQueue.Enqueue(cb, cyc);
        }

        public static ulong distance(Coord c1, Coord c2)
        {
            return (ulong)(Math.Abs(c1.x - c2.x) + Math.Abs(c1.y - c2.y));
        }

        public static ulong distance(Coord c1, int x, int y)
        {
            return (ulong)(Math.Abs(c1.x - x) + Math.Abs(c1.y - y));
        }

        // helpers

        public static bool hasNeighbor(int dir, Router router)
        {
            int x, y;
            x = router.coord.x;
            y = router.coord.y;
            switch (dir)
            {
                case DIR_DOWN: y--; break;
                case DIR_UP: y++; break;
                case DIR_LEFT: x--; break;
                case DIR_RIGHT: x++; break;
            }

            return x >= 0 && x < Config.network_nrX && y >= 0 && y < Config.network_nrY;
        }
    }


}
