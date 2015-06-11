using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class Controller_Mix : Controller
    {
        List<Controller> m_feedback;
        List<Controller> m_throt, m_prio;
        Controller m_map;

        public Controller_Mix()
        {
            m_feedback = new List<Controller>();
            m_throt = new List<Controller>();
            m_prio = new List<Controller>();
            m_map = null;

            parseConfig();
        }

        Dictionary<string, Controller> m_controllers = new Dictionary<string, Controller>();
        Controller getController(string ctlr)
        {
            if (m_controllers.ContainsKey(ctlr))
                return m_controllers[ctlr];
            else
            {
                Controller c = null;
                if (ctlr == "CLASSIC") c = new Controller_ClassicBLESS();
                if (ctlr == "THROTTLE") c = new Controller_Throttle();
                if (ctlr == "STC") c = new Controller_STC();
                if (ctlr == "MIX") c = new Controller_Mix(); // no practical use for this, but for completeness...
                if (ctlr == "SIMPLEMAP") c = new Controller_SimpleMap();
                m_controllers[ctlr] = c;
                return c;
            }
        }

        void parseConfig()
        {
            if (Config.controller_mix != "")
            {
                // format like: prio:STC,map:CLASSIC,throttle:THROTTLE
                string[] parts = Config.controller_mix.Split(',');
                foreach (string part in parts)
                {
                    string[] parts2 = part.Split(':');
                    if (parts2.Length != 2) continue;
                    string type = parts2[0], ctlr = parts2[1];

                    Controller c = getController(ctlr);

                    Console.WriteLine("adding ctlr {0} as type {1}", ctlr, type);
                    
                    if (type == "prio") addPrioritizer(c);
                    if (type == "map") addMapper(c);
                    if (type == "throttle") addThrottler(c);
                }

                foreach (Controller c in m_controllers.Values)
                    addFeedback(c);
            }
            else
            {
                Controller_ClassicBLESS cb = new Controller_ClassicBLESS();
                addFeedback(cb);
                addThrottler(cb);
                addPrioritizer(cb);
                addMapper(cb);
            }
        }

        public void addFeedback(Controller c)
        {
            m_feedback.Add(c);
        }

        public void addThrottler(Controller c)
        {
            m_throt.Add(c);
        }

        public void addPrioritizer(Controller c)
        {
            m_prio.Add(c);
        }

        public void addMapper(Controller c)
        {
            m_map = c;
        }

        // feedback methods
        public override void reportStarve(int node)
        {
            foreach (Controller c in m_feedback)
                c.reportStarve(node);
        }

        public override void doStep()
        {
            foreach (Controller c in m_feedback)
                c.doStep();
        }

        // throttling
        public override bool tryInject(int node)
        {
            bool allow = true;
            foreach (Controller c in m_throt)
                allow = c.tryInject(node) && allow;
            return allow;
        }

        // mapping
        public override int mapApp(int appID)
        {
            if (m_map != null)
                return m_map.mapApp(appID);
            else
                return appID;
        }

        public override int mapCache(int appID, ulong block)
        {
            if (m_map != null)
                return m_map.mapCache(appID, block);
            else
                return (int)(block % (ulong)Config.N);
        }

        public override int mapMC(int appID, ulong block)
        {
            if (m_map != null)
                return m_map.mapMC(appID, block);
            else
                return (int)(block % (ulong)Config.N);
        }

        public override int memMiss(int appID, ulong block)
        {
            if (m_map != null)
                return m_map.memMiss(appID, block);
            else
                return -1;
        }

        // prioritization
        public override int rankFlits(Flit f1, Flit f2)
        {
            foreach (Controller c in m_prio)
            {
                int cmp = c.rankFlits(f1, f2);
                if (cmp != 0) return cmp;
            }

            return 0;
        }

        public override IPrioPktPool newPrioPktPool(int node)
        {
            if (m_prio.Count > 0)
                return m_prio[0].newPrioPktPool(node);
            else
                return new FIFOPrioPktPool();
        }

        public override void setInjPool(int node, IPrioPktPool pool)
        {
            foreach (Controller c in m_feedback)
                c.setInjPool(node, pool);
        }
    }
}
