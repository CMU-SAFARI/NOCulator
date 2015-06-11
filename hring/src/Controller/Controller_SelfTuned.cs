//#define DEBUG
//#define THROTTLE_ALL

using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class Controller_SelfTuned : Controller_ClassicBLESS
    {
        IPrioPktPool[] m_injPools = new IPrioPktPool[Config.N];
        AveragingWindow avg_netutil, avg_ipc;
        double m_lastIPC = 0.0;
        double m_target;
        double m_rate;

        public Controller_SelfTuned()
        {
            avg_netutil = new AveragingWindow(Config.selftuned_netutil_window);
            avg_ipc = new AveragingWindow(Config.selftuned_ipc_window);

            m_target = Config.selftuned_init_netutil_target;
            m_rate = 0.0;
        }

        public override IPrioPktPool newPrioPktPool(int node)
        {
#if THROTTLE_ALL
            return new MultiQPrioPktPool();
#else
            return MultiQThrottlePktPool.construct();
#endif
        }

        public override void setInjPool(int node, IPrioPktPool pool)
        {
            m_injPools[node] = pool;
            pool.setNodeId(node);
        }

        public override bool ThrottleAtRouter
        { get
            {
#if THROTTLE_ALL
                return true;
#else
                return false;
#endif
            }
        }

        // true to allow injection, false to block (throttle)
        public override bool tryInject(int node)
        {
            if (m_rate > 0.0)
                return Simulator.rand.NextDouble() > m_rate;
            else
                return true;
        }

        public override void doStep()
        {
            avg_netutil.accumulate(Simulator.network._cycle_netutil);
            avg_ipc.accumulate((double)Simulator.network._cycle_insns / Config.N);

            if (Simulator.CurrentRound % (ulong)Config.selftuned_quantum == 0)
                doUpdate();

            if (Config.selftuned_seek_higher_ground)
                if (Simulator.CurrentRound % (ulong)Config.selftuned_ipc_quantum == 0)
                    GoClimbAMountain();
        }

        void doUpdate()
        {
            double netu = avg_netutil.average();

            if (Config.selftuned_bangbang)
            {
                m_rate = (netu > m_target) ? 1.00 : 0.00;
            }
            else
            {
                if (netu > m_target + Config.selftuned_netutil_tolerance)
                    if (m_rate < 1.00) m_rate += Config.selftuned_rate_delta;
                if (netu < m_target - Config.selftuned_netutil_tolerance)
                    if (m_rate > 0.00) m_rate -= Config.selftuned_rate_delta;

#if DEBUG
                Console.WriteLine("cycle {0}: netu {1}, target {2}, rate {3}",
                        Simulator.CurrentRound, netu, m_target, m_rate);
#endif
            }
        }

        void GoClimbAMountain()
        {
            double ipc = avg_ipc.average();
            double last_ipc = m_lastIPC;
            m_lastIPC = ipc;

            Console.WriteLine("cycle {0}: last IPC {1}, cur IPC {2}, cur target {3}",
                    Simulator.CurrentRound, last_ipc, ipc, m_target);

            // Self-Tuned Congestion Networks, Table 1 (p. 6)
            if (last_ipc > 0 && ipc / last_ipc < Config.selftuned_drop_threshold) // drop > 25% from last period?
            {
                m_target -= Config.selftuned_target_decrease;
                Console.WriteLine("--> decrease to {0}", m_target);
            }
            else
            {
                // no significant drop. increase if target is < 1.0.
                if (m_target < 1.0)
                    m_target += Config.selftuned_target_increase;
                Console.WriteLine("--> increase to {0}", m_target);
            }
        }
    }
}
