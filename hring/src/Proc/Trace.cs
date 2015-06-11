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

        public Random rand = new Random();

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
            int t_preceding = (int)(-1.0 / m_rate * Math.Log(m_r.NextDouble()));
	//		Console.WriteLine("t_preceding : {0}", t_preceding);
            Trace.Type t_type = (m_reads_fraction > m_r.NextDouble()) ?
                Trace.Type.Rd : Trace.Type.Wr;

            t_addr &= 0x7FFFFFFFFFFFFFFF;   // Clear MSB bit
            t_addr |= ((ulong)m_group) << 48;   // embed group number in address

			//ulong t_addr = (ulong)m_r.Next(0,Int32.MaxValue);
            // assumes 32-byte cache block, 4x4 (16-node) network
            if (Config.network_nrX != 4 && Config.network_nrY != 4 &&
				Config.network_nrX != 8 && Config.network_nrY != 8 && 
				Config.network_nrX != 16 && Config.network_nrY != 16 && 
                (Config.bSynthBitComplement || Config.bSynthTranspose))
                throw new Exception("bit complement and transpose are only for 4x4 and 8x8 and 16x16.");
            ulong mask = 0;
			if (Config.network_nrX == 4 && Config.network_nrY == 4)
				mask = 0x1e0;
			else if (Config.network_nrX == 8 && Config.network_nrY == 8)
				mask = 0x7e0;
			else if (Config.network_nrX == 16 && Config.network_nrY == 16)
				mask = 0x01fe0;
            if (Config.bSynthBitComplement)
			{
				if (Config.topology == Topology.Mesh || Config.topology == Topology.SingleRing)
				{
					if (Config.network_nrX == 4 && Config.network_nrY == 4)
		                t_addr = (t_addr & ~mask) | (ulong)((~m_group & 0x0f)<< 5);
					else if (Config.network_nrX == 8 && Config.network_nrY == 8)
		                t_addr = (t_addr & ~mask) | (ulong)((~m_group & 0x3f)<< 5);
					else if (Config.network_nrX == 16 && Config.network_nrY == 16)
		                t_addr = (t_addr & ~mask) | (ulong)((~m_group & 0xff)<< 5);
				}		
				else
				{
					ulong mapping = 0;
					if (Config.network_nrX == 4 && Config.network_nrY == 4)
					{
						switch(m_group)
						{
							case 0 : {mapping = 10; break;}
							case 1 : {mapping = 11; break;}
							case 2 : {mapping = 8; break;}
							case 3 : {mapping = 9; break;}
							case 4 : {mapping = 14; break;}
							case 5 : {mapping = 15; break;}
							case 6 : {mapping = 12; break;}
							case 7 : {mapping = 13; break;}
							case 8 : {mapping = 2; break;}
							case 9 : {mapping = 3; break;}
							case 10 : {mapping = 0; break;}
							case 11 : {mapping = 1; break;}
							case 12 : {mapping = 6; break;}
							case 13 : {mapping = 7; break;}
							case 14 : {mapping = 4; break;}
							case 15 : {mapping = 5; break;}
							default : {Console.WriteLine("Error");break;}
						}
						t_addr = (t_addr & ~mask) | (ulong)((mapping)<< 5);
					}
					else
					{
						int g3row = (m_group / 64 == 0 || m_group / 64 == 3)? 0 : 1;
						int g3col = (m_group / 64 == 0 || m_group / 64 == 1)? 0 : 1;
						int g2row = (m_group / 16 % 16 == 0 || m_group / 16 % 16 == 3)? 0 : 1;
						int g2col = (m_group / 16 % 16 == 1 || m_group / 16 % 16 == 0)? 0 : 1;
						int g1row = (m_group / 4 % 4 == 0 || m_group / 4 % 4 == 3)? 0 : 1;
						int g1col = (m_group / 4 % 4 == 0 || m_group / 4 % 4 == 1)? 0 : 1;
						int lrow = (m_group % 4 == 0 || m_group % 4 == 3)? 0 : 1;
						int lcol = (m_group % 4 == 0 || m_group % 4 == 1)? 0 : 1;
						int row = g3row * 8 + g2row * 4 + g1row * 2 + lrow;
						int col = g3col * 8 + g2col * 4 + g1col * 2 + lcol;
						ulong index = (ulong)row * (ulong)Config.network_nrX + (ulong)col;
						if (Config.network_nrX == 8)
							t_addr = (t_addr & ~mask) | (ulong)((~index & 0x3f)<< 5);
						else if (Config.network_nrX == 16)
							t_addr = (t_addr & ~mask) | (ulong)((~index & 0xff)<< 5);
					}
				}
			}
            else if (Config.bSynthTranspose)
            {
				if (Config.topology == Topology.Mesh || Config.topology == Topology.SingleRing)
	            {
					if (Config.network_nrX == 4 && Config.network_nrY == 4)
					{
						int dest = ((m_group << 2) & 0x0c) | ((m_group >> 2) & 0x03);
	                	t_addr = (t_addr & ~mask) | (ulong)(dest << 5);
					}
					else if (Config.network_nrX == 8 && Config.network_nrY == 8)
					{
						int dest = ((m_group << 3) & 0x38) | ((m_group >> 3) & 0x07);
	                	t_addr = (t_addr & ~mask) | (ulong)(dest << 5);
					}
					else if (Config.network_nrX == 16 && Config.network_nrY == 16)
					{
						int dest = ((m_group << 4) & 0xf0) | ((m_group >> 4) & 0x0f);
	                	t_addr = (t_addr & ~mask) | (ulong)(dest << 5);
					}
				}
				else
				{	
					if (Config.network_nrX == 4 && Config.network_nrY == 4)
					{
						ulong mapping = 0;
						switch(m_group)
						{
							case 0 : {mapping = 10; break;}
							case 1 : {mapping = 9; break;}
							case 2 : {mapping = 8; break;}
							case 3 : {mapping = 11; break;}
							case 4 : {mapping = 6; break;}
							case 5 : {mapping = 5; break;}
							case 6 : {mapping = 4; break;}
							case 7 : {mapping = 7; break;}
							case 8 : {mapping = 2; break;}
							case 9 : {mapping = 1; break;}
							case 10 : {mapping = 0; break;}
							case 11 : {mapping = 3; break;}
							case 12 : {mapping = 14; break;}
							case 13 : {mapping = 13; break;}
							case 14 : {mapping = 12; break;}
							case 15 : {mapping = 15; break;}
						}
						t_addr = (t_addr & ~mask) | (ulong)((mapping)<< 5);
					}
					else
					{
						int g3row = (m_group / 64 == 0 || m_group / 64 == 3)? 0 : 1;
						int g3col = (m_group / 64 == 0 || m_group / 64 == 1)? 0 : 1;
						int g2row = (m_group / 16 % 16 == 0 || m_group / 16 % 16 == 3)? 0 : 1;
						int g2col = (m_group / 16 % 16 == 1 || m_group / 16 % 16 == 0)? 0 : 1;
						int g1row = (m_group / 4 % 4 == 0 || m_group / 4 % 4 == 3)? 0 : 1;
						int g1col = (m_group / 4 % 4 == 0 || m_group / 4 % 4 == 1)? 0 : 1;
						int lrow = (m_group % 4 == 0 || m_group % 4 == 3)? 0 : 1;
						int lcol = (m_group % 4 == 0 || m_group % 4 == 1)? 0 : 1;
						int row = g3row * 8 + g2row * 4 + g1row * 2 + lrow;
						int col = g3col * 8 + g2col * 4 + g1col * 2 + lcol;

						ulong index = (ulong)row * (ulong)Config.network_nrX + (ulong)col;
						if (Config.network_nrX == 8 && Config.network_nrY == 8)
						{
							ulong dest = ((index << 3) & 0x38) | ((index >> 3) & 0x07);
	                		t_addr = (t_addr & ~mask) | (ulong)(dest << 5);
						}
						else if (Config.network_nrX == 16 && Config.network_nrY == 16)
						{
							ulong dest = ((index << 4) & 0xf0) | ((index >> 4) & 0x0f);
	                		t_addr = (t_addr & ~mask) | (ulong)(dest << 5);
						}
					}
				}
            }

            if (Config.bSynthHotspot)
            {
//                if (Config.network_nrX != 4 && Config.network_nrY != 4)
//                    throw new Exception("hotspot for 4x4 only.");

                // Send to 1 destination only
                ulong _mask = 0x1e0;
                int dest = 10;
                t_addr = (t_addr & ~_mask) | (ulong)(dest << 5); 
            }

            if (Config.randomHotspot)
            {
                if (Config.network_nrX != 4 && Config.network_nrY != 4)
                    throw new Exception("hotspot for 4x4 only.");

                // Send to 1 destination only
                ulong _mask = 0x1e0;
                
                int mapping = 10;
                // ----- generate an address that traverse out of the ring
                if(rand.NextDouble()> Config.non_local_chance) //generate a non local traffic
                {
                    int loc = rand.Next()%12;
                    if(m_group == 0 || m_group == 1 || m_group == 4 || m_group ==5)
                    {
                        switch(loc)
                        {
                            case 0:{mapping = 2; break;}
                            case 1:{mapping = 3; break;}
                            case 2:{mapping = 6; break;}
                            case 3:{mapping = 7; break;}
                            case 4:{mapping = 8; break;}
                            case 5:{mapping = 9; break;}
                            case 6:{mapping = 12; break;}
                            case 7:{mapping = 13; break;}
                            case 8:{mapping = 10; break;}
                            case 9:{mapping = 11; break;}
                            case 10:{mapping = 14; break;}
                            case 11:{mapping = 15; break;}
                        }
                    }
                    else if(m_group == 2 || m_group == 3 || m_group == 6 || m_group ==7)
                    {
                        switch(loc)
                        {
                            case 0:{mapping = 0; break;}
                            case 1:{mapping = 1; break;}
                            case 2:{mapping = 4; break;}
                            case 3:{mapping = 5; break;}
                            case 4:{mapping = 8; break;}
                            case 5:{mapping = 9; break;}
                            case 6:{mapping = 12; break;}
                            case 7:{mapping = 13; break;}
                            case 8:{mapping = 10; break;}
                            case 9:{mapping = 11; break;}
                            case 10:{mapping = 14; break;}
                            case 11:{mapping = 15; break;}
                        }
                    }
                    else if(m_group == 8 || m_group == 9 || m_group == 12 || m_group ==13)
                    {
                        switch(loc)
                        {
                            case 0:{mapping = 0; break;}
                            case 1:{mapping = 1; break;}
                            case 2:{mapping = 4; break;}
                            case 3:{mapping = 5; break;}
                            case 4:{mapping = 2; break;}
                            case 5:{mapping = 3; break;}
                            case 6:{mapping = 6; break;}
                            case 7:{mapping = 7; break;}
                            case 8:{mapping = 10; break;}
                            case 9:{mapping = 11; break;}
                            case 10:{mapping = 14; break;}
                            case 11:{mapping = 15; break;}
                        }
                    }
                    else if(m_group == 10 || m_group == 11 || m_group == 14 || m_group ==15)
                    {
                        switch(loc)
                        {
                            case 0:{mapping = 0; break;}
                            case 1:{mapping = 1; break;}
                            case 2:{mapping = 4; break;}
                            case 3:{mapping = 5; break;}
                            case 4:{mapping = 8; break;}
                            case 5:{mapping = 9; break;}
                            case 6:{mapping = 12; break;}
                            case 7:{mapping = 13; break;}
                            case 8:{mapping = 2; break;}
                            case 9:{mapping = 3; break;}
                            case 10:{mapping = 6; break;}
                            case 11:{mapping = 7; break;}
                        }
                    }
                }
                else // generate a local traffic
                {
                    int loc = rand.Next()%4;
                    if(m_group == 0 || m_group == 1 || m_group == 4 || m_group ==5)
                    {
                        switch(loc)
                        {
                    	case 0:{mapping = 0; break;}
                    	case 1:{mapping = 1; break;}
                    	case 2:{mapping = 4; break;}
                    	case 3:{mapping = 5; break;}
                        }
                    }
                    else if(m_group == 2 || m_group == 3 || m_group == 6 || m_group ==7)
                    {
                        switch(loc)
                        {
                    	case 0:{mapping = 2; break;}
                    	case 1:{mapping = 3; break;}
                    	case 2:{mapping = 6; break;}
                    	case 3:{mapping = 7; break;}
                        }
                    }
                    else if(m_group == 8 || m_group == 9 || m_group == 12 || m_group ==13)
                    {
                        switch(loc)
                        {
                    	case 0:{mapping = 8; break;}
                    	case 1:{mapping = 9; break;}
                    	case 2:{mapping = 12; break;}
                    	case 3:{mapping = 13; break;}
                        }
                    }
                    else if(m_group == 10 || m_group == 11 || m_group == 14 || m_group ==15)
                    {
                        switch(loc)
                        {
                    	case 0:{mapping = 10; break;}
                    	case 1:{mapping = 11; break;}
                    	case 2:{mapping = 14; break;}
                    	case 3:{mapping = 15; break;}
                        }
                    }
                }
                t_addr = (t_addr & ~_mask) | (ulong)(mapping << 5); 
            }



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
