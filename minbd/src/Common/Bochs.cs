using System;

using ICSimulator;

namespace Bochs
{
    public class Backend
    {
        static ulong m = 0, ins = 0;

        public static void init(string param)
        {
            string[] args = (param + " -bochs_fe true").Split(' ');

            Simulator.Init(args);
        }

        public static void inst(ulong inst_id, int cpu, int src1, int src2, int dest, int op_type)
        {
            ins++;
            TraceBochs.putInst(cpu);
        }

        public static void mem(int cpu, ulong addr, bool write)
        {
            m++;
            TraceBochs.putMem(cpu, addr, write);
        }

        public static int window(int cpu)
        {
            return TraceBochs.freeSlots(cpu);
        }

        static bool sim_finished = false;

        public static bool cycle(bool finish)
        {
            if (sim_finished) return false;

            bool cont = false;
            try
            {
                cont = Simulator.DoStep();

                if (!cont || finish)
                {
                    Simulator.Finish();
                    sim_finished = true;
                    Console.WriteLine("m = {0}, i = {1}", m, ins);
                }

            }
            catch(Exception e)
            {
                Console.WriteLine("Exception in backend simulator: {0}", e.Message);
                Console.WriteLine("Stack trace:\n{0}", e.StackTrace);
                cont = false;
            }

            return cont;
        }
    }

}

namespace ICSimulator
{
    public class TraceBochs : Trace
    {
        int m_ID;

        static int[] m_nonmem;
        static bool[] m_mem;
        static ulong[] m_addr;
        static bool[] m_write;
        static bool m_inited = false;

        public TraceBochs(int ID)
        {
            m_ID = ID;

            if (!m_inited)
            {
                m_nonmem = new int[Config.N];
                m_mem = new bool[Config.N];
                m_addr = new ulong[Config.N];
                m_write = new bool[Config.N];
                m_inited = true;
            }
        }

        public override bool getNext()
        {
            if (m_nonmem[m_ID] > 0)
            {
                type = Type.NonMem;
                address = (ulong)m_nonmem[m_ID];
                m_nonmem[m_ID] = 0;
                return true;
            }
            else if (m_mem[m_ID])
            {
                address = m_addr[m_ID];
                type = m_write[m_ID] ? Type.Wr : Type.Rd;
                m_mem[m_ID] = false;
                return true;
            }
            else
            {
                type = Type.Pause; // bubble in stream
                return true;
            }
        }

        public static void putInst(int cpu)
        {
            m_nonmem[cpu]++;
        }

        public static void putMem(int cpu, ulong addr, bool write)
        {
            m_nonmem[cpu]--;
            m_mem[cpu] = true;
            m_addr[cpu] = addr;
            m_write[cpu] = write;
        }

        public static int freeSlots(int cpu)
        {
            return m_mem[cpu] ? 0 : 1;
        }

        public override bool EOF { get { return false; } }

        public override void rewind() { }
        public override void seek(ulong insns) { }
    }
}
