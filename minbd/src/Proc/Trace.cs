using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip;

namespace ICSimulator
{
    public abstract class Trace
    {
        public enum Type
        { Rd, Wr, NonMem, Lock, Unlock, Barrier, Label, Sync, Pause }

        public abstract bool getNext(); // false if end-of-sequence
        public abstract void rewind();
        public abstract void seek(ulong insns);

        public Type type;        
        public ulong address; // addr for read/write; count for nonmem
        public int from;      // for sync

        public abstract bool EOF { get; }
    }

    public class TraceFile_Old : Trace
    {
        string m_filename;
        BinaryReader m_reader;
        bool m_eof;
        int m_group;

        private bool m_nextValid;
        private Type m_nextType;
        private ulong m_nextAddr;
        
        public TraceFile_Old(string filename, int group)
        {
            m_filename = filename;
            openFile();

            m_group = group;
            m_nextValid = false;
        }

        void openFile()
        {
            if (m_reader != null) m_reader.Close();

            m_reader = new BinaryReader(new GZipInputStream(File.OpenRead(m_filename)));
            m_eof = false;
        }

        public override bool getNext()
        {
            if (m_eof) return false;

            if (m_nextValid)
            {
                address = m_nextAddr;
                type = m_nextType;
                from = 0;
                m_nextValid = false;
                return true;
            }

            try
            {
                ulong t_addr = m_reader.ReadUInt64();
                int t_preceding = m_reader.ReadInt32();
                Trace.Type t_type = (t_addr >> 63) != 0 ?
                    Trace.Type.Rd : Trace.Type.Wr;
                t_addr &= 0x7FFFFFFFFFFFFFFF;

                t_addr |= ((ulong)m_group) << 48;

                if (t_preceding > 0)
                {
                    m_nextAddr = t_addr;
                    m_nextType = t_type;
                    m_nextValid = true;

                    address = (ulong)t_preceding;
                    from = 0;
                    type = Trace.Type.NonMem;
                }
                else
                {
                    address = t_addr;
                    from = 0;
                    type = t_type;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                m_eof = true;
                return false;
            }
        }

        public override void rewind()
        {
            openFile();
        }

        public override void seek(ulong insns)
        {
            long count = (long)insns; // might go negative

            while (count > 0)
            {
                if (!getNext())
                    return;
                switch (type)
                {
                    case Trace.Type.NonMem: count -= (long)address; break;
                    case Trace.Type.Rd: count--; break;
                    case Trace.Type.Wr: count--; break;
                }
            }
        }

        public override bool EOF { get { return m_eof; } }
    }



    public class TraceFile_Old_Scalable : Trace
    {
        string m_filename;
        bool m_eof;
        int m_group;

        private bool m_nextValid;
        private Type m_nextType;
        private ulong m_nextAddr;

        static Dictionary<string,BinaryReader> m_traces;
        static Dictionary<string,Object> m_tlocks;
        int m_trace_pos;
        
        public TraceFile_Old_Scalable(string filename, int group)
        {

            if(m_traces == null)
              m_traces = new Dictionary<string,BinaryReader>();
            if(m_tlocks == null)
              m_tlocks = new Dictionary<string,Object>();
            
            m_filename = filename;

            if(!m_traces.ContainsKey(m_filename))
              openFile();

            m_tlocks[m_filename] = new Object();

            m_trace_pos = 0;    // the local position is set to 0

            m_group = group;
            m_nextValid = false;
        }

        void openFile()
        {
            m_traces[m_filename] = new BinaryReader(File.OpenRead(m_filename));
            m_eof = false;
        }

        public override bool getNext()
        {
            if (m_eof) return false;

            if (m_nextValid)
            {
                address = m_nextAddr;
                type = m_nextType;
                from = 0;
                m_nextValid = false;
                return true;
            }

            try
            {
                ulong t_addr;
                int t_preceding;
                lock(m_tlocks[m_filename]) {
                  m_traces[m_filename].BaseStream.Seek(m_trace_pos, SeekOrigin.Begin);
                  t_addr = m_traces[m_filename].ReadUInt64();
                  t_preceding = m_traces[m_filename].ReadInt32();
                  m_trace_pos += 12;
                }
                Trace.Type t_type = (t_addr >> 63) != 0 ?
                    Trace.Type.Rd : Trace.Type.Wr;
                t_addr &= 0x7FFFFFFFFFFFFFFF;

                t_addr |= ((ulong)m_group) << 48;

                if (t_preceding > 0)
                {
                    m_nextAddr = t_addr;
                    m_nextType = t_type;
                    m_nextValid = true;

                    address = (ulong)t_preceding;
                    from = 0;
                    type = Trace.Type.NonMem;
                }
                else
                {
                    address = t_addr;
                    from = 0;
                    type = t_type;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                m_eof = true;
                return false;
            }
        }

        public override void rewind()
        {
            openFile();
        }

        public override void seek(ulong insns)
        {
            long count = (long)insns; // might go negative

            while (count > 0)
            {
                if (!getNext())
                    return;
                switch (type)
                {
                    case Trace.Type.NonMem: count -= (long)address; break;
                    case Trace.Type.Rd: count--; break;
                    case Trace.Type.Wr: count--; break;
                }
            }
        }

        public override bool EOF { get { return m_eof; } }
    }

    public class TraceFile_New : Trace
    {
        string m_filename;
        BinaryReader m_reader;
        bool m_eof;
        int m_group;

        public TraceFile_New(string filename, int group)
        {
            m_filename = filename;
            openFile();

            m_group = group;
        }

        void openFile()
        {
            if (m_reader != null) m_reader.Close();            
            
            m_reader = new BinaryReader(new GZipInputStream(File.OpenRead(m_filename)));
            //m_reader = new BinaryReader(File.OpenRead(m_filename));
        }

        public override bool getNext()
        {
            if (m_eof) return false;

            try
            {
                address = m_reader.ReadUInt64();
                from = m_reader.ReadInt32();
                int t = m_reader.ReadInt32();
                switch (t)
                {
                case 0:
                    address |= ((ulong)m_group) << 48;
                    type = Trace.Type.Rd; break;
                case 1:
                    address |= ((ulong)m_group) << 48;
                    type = Trace.Type.Wr; break;
                case 2:
                    type = Trace.Type.NonMem; break;
                case 3:
                    address |= ((ulong)m_group) << 48;
                    type = Trace.Type.Lock; break;
                case 4:
                    address |= ((ulong)m_group) << 48;
                    type = Trace.Type.Unlock; break;
                case 5:
                    address |= ((ulong)m_group) << 48;
                    type = Trace.Type.Barrier; break;
                case 6:
                    type = Trace.Type.Label; break;
                case 7:
                    type = Trace.Type.Sync; break;
                default:
                    type = Trace.Type.NonMem; address = 0; break;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                m_eof = true;
                return false;
            }
        }

        public override void rewind()
        {
            openFile();
        }

        public override void seek(ulong insns)
        {
        }

        public override bool EOF { get { return m_eof; } }
    }

    public class TraceSynth : Trace
    {
        int m_group;

        private bool m_nextValid;
        private Type m_nextType;
        private ulong m_nextAddr;
        private Random m_r;

        private double m_rate;
        private double m_reads_fraction;

        public TraceSynth(int group)
        {
            m_group = group;
            m_nextValid = false;
            m_r = new Random();
            m_rate = Config.synth_rate;
            m_reads_fraction = Config.synth_reads_fraction;
        }

        public override bool getNext()
        {
            if (m_nextValid)
            {
                address = m_nextAddr;
                type = m_nextType;
                from = 0;
                m_nextValid = false;
                return true;
            }

            if (m_rate == 0)
            {
                address = 1;
                type = Trace.Type.NonMem;
                from = 0;
                return true;
            }

            // Generate new trace record (mem address + preceding insts)
            ulong t_addr = (ulong)m_r.Next(0,Int32.MaxValue);
            ulong mask = 0x1e0;

            // assumes 32-byte cache block, 4x4 (16-node) network
            switch(Config.router.pattern)
            {
                case TrafficPattern.bitComplement:
                    if (Config.network_nrX != 4 && Config.network_nrY != 4)
                        throw new Exception("Synth only for 4x4");
                    t_addr = (t_addr & ~mask) | (ulong)((~m_group & 0x0f) << 5);
                    break;

                case TrafficPattern.transpose:
                    if (Config.network_nrX != 4 && Config.network_nrY != 4)
                        throw new Exception("Synth only for 4x4");
                    int dest = ((m_group << 2) & 0x0c) | ((m_group >> 2) & 0x03);
                    t_addr = (t_addr & ~mask) | (ulong)(dest << 5);
                    break;

                case TrafficPattern.uniformRandom:
                    break;

                default: throw new Exception("Synth pattern not implemented yet");
            }

            int t_preceding = (int)(-1.0 / m_rate * Math.Log(m_r.NextDouble()));
            Trace.Type t_type = (m_reads_fraction > m_r.NextDouble()) ?
                Trace.Type.Rd : Trace.Type.Wr;

            t_addr &= 0x7FFFFFFFFFFFFFFF;   // Clear MSB bit
            t_addr |= ((ulong)m_group) << 48;   // embed group number in address

            if (t_preceding > 0)
            {
                m_nextAddr = t_addr;
                m_nextType = t_type;
                m_nextValid = true;

                address = (ulong)t_preceding;
                from = 0;
                type = Trace.Type.NonMem;
            }
            else
            {
                address = t_addr;
                from = 0;
                type = t_type;
            }

            return true;

        }

        public override void rewind()
        {
        }

        public override void seek(ulong insns)
        {
            // Poisson process is memoryless, so seek is a no-op
        }

        public override bool EOF { get { return false; } }
    }

}
