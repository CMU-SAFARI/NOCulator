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
        public const int DIR_BLOCKED = -1;
        public const int DIR_NONE = -99;

        // simulator state
        public static ulong CurrentRound = 0;
        public static bool Warming = false;

        public static ulong CurrentBarrier = 0; // MT workloads

        // ready callback and deferred-callback queue
        public delegate void Ready();

        private static PrioQueue<Simulator.Ready> m_deferQueue = new PrioQueue<Simulator.Ready>();

        public static void Main(string[] args)
        {
            System.Diagnostics.Process.Start("hostname");
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            Init(args);

            RunSimulationRun();

            Finish();
        }

        public static void Init(string[] args)
        {
            Config config = new Config();

            config.read(args);

            rand = new Rand(Config.rand_seed);
            CurrentRound = 0;

            controller = Controller.construct();
	   		if (Config.ScalableRingClustered)
				network = new RC_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.HR_16drop)
				network = new HR_16drop_Network(Config.network_nrX, Config.network_nrY);				
			else if (Config.topology == Topology.HR_4drop)
				network = new HR_4drop_Network(Config.network_nrX, Config.network_nrY);	
			else if (Config.topology == Topology.HR_8drop)
				network = new HR_8drop_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.HR_8_16drop)
				network = new HR_8_16drop_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.HR_8_8drop)
				network = new HR_8_8drop_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.HR_16_8drop)				
				network = new HR_16_8drop_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.HR_32_8drop)				
				network = new HR_32_8drop_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.HR_buffered)
				network = new HR_buffered_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.SingleRing)
				network = new SingleRing_Network(Config.network_nrX, Config.network_nrY);
			else if (Config.topology == Topology.MeshOfRings)
				network = new MeshOfRings_Network(Config.network_nrX, Config.network_nrY);
                        else if (Config.topology == Topology.BufRingNetwork)
                            network = new BufRingNetwork(Config.network_nrX, Config.network_nrY);
                        else if (Config.topology == Topology.BufRingNetworkMulti)
                            network = new BufRingMultiNetwork(Config.network_nrX, Config.network_nrY);
	    	else
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
            using (TextWriter tw = new StreamWriter(Config.output))
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

        public static void RunSimulationRun()
        {
           if (File.Exists(Config.output))
            {
                Console.WriteLine("Output file {0} exists; exiting.", Config.output);
                Environment.Exit(0);
            }

            if (Config.RouterEvaluation)
                RouterEval.evaluate();
            else
                RunSimulation();

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

            if (CurrentRound % 100000 == 0)
                ProgressUpdate();

            CurrentRound++;

            network.doStep();
			if (Config.simpleLivelock)
				Router.livelockFreedom();
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
