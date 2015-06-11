//#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace ICSimulator
{
    public class Workload
    {
        // MP trace, group specified: trace.mpt.<thd>.gz.new(<group>)
        static Regex r_mpt_group = new Regex(
            @"^(.*)\.mpt\.(\d+).gz.new\((\d+)\)$");

        // MP trace, no group specified: trace.mpt.<thd>
        static Regex r_mpt = new Regex(
            @"^(.*)\.mpt\.(\d+).gz.new$");

        // standard (non-MP) trace
        static Regex r_sp = new Regex( 
            @"^(.*)\.gz$");
        
        static Regex r_sp_uncomp = new Regex( 
            @"^(.*)\.bin$");

        int[] m_groups; // CPU id -> group ID
        int[] m_thds;   // CPU id -> thread ID (within group)
        string[] m_files; // CPU id -> trace filename

        int[,] m_mappings; // (group ID, thread ID) -> CPU id
        int[] m_counts; // group ID -> thread count

        int m_count;

        public int Count { get { return m_count; } }
        public int GroupCount { get { return m_nextGroup; } }

        public Workload(string[] parts)
        {
            m_groupMappings = new Dictionary<string, int>();
            m_nextGroup = 0;

            m_groups = new int[parts.Length];
            m_thds = new int[parts.Length];
            m_files = new string[parts.Length];
            m_mappings = new int[parts.Length, parts.Length];
            m_counts = new int[parts.Length];

            int idx = 0;
            foreach (string file in parts)
            {
                parseFilename(file, idx++);
            }

            m_count = idx;
        }

        // dictionary: "file_group" or "file" -> real group ID
        Dictionary<string, int> m_groupMappings;
        
        // next available real group number
        int m_nextGroup = 0;

        void parseFilename(string file, int cpu)
        {
            Match m,mu;

            Console.WriteLine("cpu {0} filename '{1}'", cpu, file);
                        
            m = r_mpt_group.Match(file);
            if (m.Success)
            {
                string basename = m.Groups[1].Value;
                string thd = m.Groups[2].Value;
                string group = m.Groups[3].Value;
                
                assignGrouped(cpu, basename, int.Parse(thd),
                              String.Format("{0}_{1}", basename, group));
                return;
            }

            m = r_mpt.Match(file);
            if (m.Success)
            {
                string basename = m.Groups[1].Value;
                string thd = m.Groups[2].Value;

                assignGrouped(cpu, basename, int.Parse(thd), basename);
                return;
            }

            m = r_sp.Match(file);
            mu = r_sp_uncomp.Match(file);
            if (m.Success)
            {
                string basename = m.Groups[1].Value;
                assignSingle(cpu, basename + ".gz");
                return;
            }

            if(mu.Success)
            {
                string basename = mu.Groups[1].Value;
                assignSingle(cpu, basename + ".bin");
                return;
            }

            if (file == "null" || file == "synth")
            {
                assignSingle(cpu, file);
                return;
            }

            throw new Exception("Could not understand trace filename " + file);
        }

        void assignGrouped(int cpu, string basename, int thd, string groupKey)
        {
            m_files[cpu] = String.Format("{0}.mpt.{1}.gz.new", basename, thd);
            m_thds[cpu] = thd;

            if (!m_groupMappings.ContainsKey(groupKey))
                m_groupMappings[groupKey] = m_nextGroup++;
                
            int groupID = m_groupMappings[groupKey];

            m_groups[cpu] = groupID;
            m_mappings[groupID, thd] = cpu;
            m_counts[groupID]++;
        }

        void assignSingle(int cpu, string basename)
        {
            m_files[cpu] = basename;
            m_thds[cpu] = 0;
            m_groups[cpu] = m_nextGroup++;
            m_mappings[m_groups[cpu], 0] = cpu;
            m_counts[m_groups[cpu]]++;
        }

        // CPU id -> trace file
        public string getFile(int ID)
        {
            string basename = m_files[ID];

            if (basename == "null") return basename;
            if (basename == "synth") return basename;

            // find in trace file path
            foreach (string dir in Config.TraceDirs.Split(',', ' '))
                if (File.Exists(dir + "/" + basename))
                    return dir + "/" + basename;
            
            throw new Exception("Could not find trace file " + basename);
        }

        public string getName(int ID)
        {
            string s = m_files[ID];
            if (s.EndsWith(".bin.gz"))
                s = s.Substring(0, s.Length - 7);
            return s;
        }

        // CPU id -> group ID
        public int getGroup(int ID)
        {
            return m_groups[ID];
        }

        // CPU id -> thread ID (within group)
        public int getThread(int ID)
        {
            return m_thds[ID];
        }

        // (group ID, thread ID) -> CPU id
        public int mapThd(int group, int thdID)
        {
            return m_mappings[group, thdID];
        }

        public int getGroupSize(int group)
        {
            return m_counts[group];
        }

        public string getLogFile(int id, string prefix)
        {
            string trace = getFile(id);
            FileInfo fi = new FileInfo(trace);
            string dir = fi.Directory.FullName + "/anno/" + (!Config.sh_cache_perfect).ToString() + "/" + prefix + "/";
            string tracename = fi.Name;
            if (tracename[0] == '4') tracename = tracename.Substring(4); // remove 4xx.* on SPEC -- HACK
            string name = tracename.Replace(".bin.gz", ".log.gz");
            return dir + name;
        }
    }

#if TEST
    public class test
    {
        public static void Main(string[] args)
        {
            Workload w = new Workload(new string[] {
                    "a.gz", "b.mpt.0", "b.mpt.1", "b.mpt.0(1)", "b.mpt.1(1)", "a.gz"
                });

            Console.WriteLine("--- {0} CPUs: ---", w.Count);
            for (int i = 0; i < w.Count; i++)
            {
                Console.WriteLine("CPU {0}: file {1}, group {2}, thd {3}",
                                  i, w.getFile(i), w.getGroup(i), w.getThread(i));
            }

            Console.WriteLine("--- {0} Groups: ---", w.GroupCount);
            for (int i = 0; i < w.GroupCount; i++)
            {
                Console.Write("Group {0}: ", i);
                for (int j = 0; j < w.getGroupSize(i); j++)
                    Console.Write("{0} ", w.mapThd(i, j));
                Console.WriteLine();
            }
        }

        /* should display:

           --- 6 CPUs: ---
           CPU 0: file a.gz, group 0, thd 0
           CPU 1: file b.mpt.0, group 1, thd 0
           CPU 2: file b.mpt.1, group 1, thd 1
           CPU 3: file b.mpt.0, group 2, thd 0
           CPU 4: file b.mpt.1, group 2, thd 1
           CPU 5: file a.gz, group 3, thd 0
           --- 4 Groups: ---
           Group 0: 0 
           Group 1: 1 2 
           Group 2: 3 4 
           Group 3: 5 
        */
    }
#endif
}
