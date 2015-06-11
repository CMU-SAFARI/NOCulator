using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{

    public enum MemoryRequestType
    {
        RD,
        DAT,
        WB,
    }

    public class MemoryRequest
    {
        public Request request;
        public MemoryRequestType type;
        public int memoryRequesterID; //summary>LLC node</summary>

        public ulong timeOfArrival;
        public bool isMarked;

        public ulong creationTime;

        public int m_index;         //summary>memory index pertaining to the block address</summary>
        public int b_index;         //summary>bank index pertaining to the block address</summary>
        public ulong r_index;       //summary>row index pertaining to the block address</summary>
        public int glob_b_index;    //summary>global bank index (for central arbiter)</summary>

        //scheduling related
        public MemSched sched;      //summary>the memory scheduler this request is destined to; determined by memory index</summary>
        public int buf_index;       //summary>within the memory scheduler, this request's index in the buffer; saves the effort of searching through entire buffer</summary>

        //public int bufferSlot;      // that is just an optimization that I don't need to search over the buffer anymore!

        public Simulator.Ready cb; // completion callback

        public static void mapAddr(ulong block,
                out int m_index, out int b_index, out ulong r_index, out int glob_b_index)
        {
            ulong shift_row;
            ulong shift_mem;
            ulong shift_bank;
            int groupID = (int)(block >> (48 - Config.cache_block));

            switch (Config.memory.address_mapping)
            {

                case AddressMap.BMR:
                    /**
                     * row-level striping (inter-mem): default
                     * RMS (BMR; really original)
                     */
                    shift_row = block >> Config.memory.row_bit;

                    m_index = (int)((shift_row ^ (ulong)groupID) % (ulong)Config.memory.mem_max);
					//Console.WriteLine("Common/Request.cs : m_index:{0}, groupID:{1}, mem_max:{2}", m_index, groupID, Config.memory.mem_max);
                    shift_mem = (ulong)(shift_row >> Config.memory.mem_bit);

                    b_index = (int)((shift_mem ^ (ulong)groupID) % (ulong)Config.memory.bank_max_per_mem);
                    r_index = (ulong)(shift_mem >> Config.memory.bank_bit);
                    break;

                case AddressMap.BRM:
                    /**
                     * block-level striping (inter-mem)
                     * BMS (BRM; original)
                     */

                    m_index = (int)(block % (ulong)Config.memory.mem_max);
                    shift_mem = block >> Config.memory.mem_bit;

                    shift_row = shift_mem >> Config.memory.row_bit;

                    b_index = (int)(shift_row % (ulong)Config.memory.bank_max_per_mem);
                    r_index = (ulong)(shift_row >> Config.memory.bank_bit);

                    break;

                case AddressMap.MBR:
                    /**
                     * row-level striping (inter-bank)
                     * RBS (MBR; new)
                     */

                    shift_row = block >> Config.memory.row_bit;

                    b_index = (int)(shift_row % (ulong)Config.memory.bank_max_per_mem);
                    shift_bank = (ulong)(shift_row >> Config.memory.bank_bit);

                    m_index = (int)(shift_bank % (ulong)Config.memory.mem_max);
                    r_index = (ulong)(shift_bank >> Config.memory.mem_bit);
                    break;

                case AddressMap.MRB:
                    /**
                     * block-level striping (inter-bank)
                     * BBS
                     */
                    //Console.WriteLine(block.ToString("x"));
                    b_index = (int)(block % (ulong)Config.memory.bank_max_per_mem);
                    shift_bank = block >> Config.memory.bank_bit;

                    shift_row = shift_bank >> Config.memory.row_bit;

                    m_index = (int)(shift_row % (ulong)Config.memory.mem_max);
                    r_index = shift_row >> Config.memory.mem_bit;
                    //Console.WriteLine("bmpm:{0} bb:{1} b:{2} m:{3} r:{4}", Config.memory.bank_max_per_mem, Config.memory.bank_bit, b_index.ToString("x"), m_index.ToString("x"), r_index.ToString("x"));
                    break;

                default:
                    throw new Exception("Unknown address map!");
            }
            
            //central arbiter related
            if (Config.memory.is_shared_MC) {
                glob_b_index = m_index * Config.memory.bank_max_per_mem + b_index;
            }
            else {
                glob_b_index = b_index;                
            }
        }

        public static int mapMC(ulong block)
        {
            int m, b, glob_b;
            ulong r;
            mapAddr(block, out m, out b, out r, out glob_b);
            return m;
        }

        public MemoryRequest(Request req, Simulator.Ready cb)
        {
            this.cb = cb;
            request = req;
            req.beenToMemory = true;

            mapAddr(req.blockAddress, out m_index, out b_index, out r_index, out glob_b_index);

            //scheduling related
            //sched = Config.memory.mem[m_index].sched;
            sched = null;
            isMarked = false;
        }
    }

    public class Request
    {
        /// <summary> Reasons for a request to be delayed, such as addr packet transmission, data packet injection, memory queueing, etc. </summary>
        public enum DelaySources
        {
            //TODO: this (with coherency awareness)
            UNKNOWN,
            COHERENCE,
            MEMORY,
            LACK_OF_MSHRS,
            ADDR_PACKET,
            DATA_PACKET,
            MC_ADDR_PACKET,
            MC_DATA_PACKET,
            INJ_ADDR_PACKET,
            INJ_DATA_PACKET,
            INJ_MC_ADDR_PACKET,
            INJ_MC_DATA_PACKET
        }

        public bool write { get { return _write; } }
        private bool _write;

        public ulong blockAddress { get { return _address >> Config.cache_block; } }
        public ulong address { get { return _address; } }
        private ulong _address;

        public int requesterID { get { return _requesterID; } }
        private int _requesterID;

        public ulong creationTime { get { return _creationTime; } }
        private ulong _creationTime;

        public int mshr;

        /// <summary> Packet/MemoryRequest/CoherentDir.Entry on the critical path of the serving of this request. </summary>
        // e.g. the address packet, then data pack on the way back
        // or addr, mc_addr, mc_request, mc_data and then data
        // or upgrade(to dir), release(to owner), release_data(to dir), data_exclusive(to requestor)
        //object _carrier;
        public void setCarrier(object carrier)
        {
        //  _carrier = carrier;
        }

        // Statistics gathering
        /// <summary> Records cycles spent in each portion of its path (see Request.TimeSources) </summary>
        //private ulong[] cyclesPerLocation;
        /// <summary> Record number of stalls caused by this request while it's the oldest in the inst window </summary>
        public double backStallsCaused;

        public Request(int requesterID, ulong address, bool write)
        {
            this._requesterID = requesterID;
            this._address = address;
            this._write = write;
            this._creationTime = Simulator.CurrentRound;
        }

        public override string ToString()
        {
            return String.Format("Request: address {0:X} (block {1:X}), write {2}, requestor {3}",
                    _address, blockAddress, _write, _requesterID);
        }

        private ulong _serviceCycle = ulong.MaxValue;
        public void service()
        {
            if (_serviceCycle != ulong.MaxValue)
                throw new Exception("Retired request serviced twice!");
            _serviceCycle = Simulator.CurrentRound;
        }

        public bool beenToNetwork = false;
        public bool beenToMemory = false;

        public void retire()
        {
            if (_serviceCycle == ulong.MaxValue)
                throw new Exception("Retired request never serviced!");
            ulong slack = Simulator.CurrentRound - _serviceCycle;

            Simulator.stats.all_slack_persrc[requesterID].Add(slack);
            Simulator.stats.all_slack.Add(slack);
            Simulator.stats.all_stall_persrc[requesterID].Add(backStallsCaused);
            Simulator.stats.all_stall.Add(backStallsCaused);
            if (beenToNetwork)
            {
                Simulator.stats.net_slack_persrc[requesterID].Add(slack);
                Simulator.stats.net_slack.Add(slack);
                Simulator.stats.net_stall_persrc[requesterID].Add(backStallsCaused);
                Simulator.stats.net_stall.Add(backStallsCaused);
            }
            if (beenToMemory)
            {
                Simulator.stats.mem_slack_persrc[requesterID].Add(slack);
                Simulator.stats.mem_slack.Add(slack);
                Simulator.stats.mem_stall_persrc[requesterID].Add(backStallsCaused);
                Simulator.stats.mem_stall.Add(backStallsCaused);
            }

            if (beenToNetwork)
            {
                Simulator.stats.req_rtt.Add(_serviceCycle - _creationTime);
            }

        }
    }


    //For reference:
    public enum OldInstructionType { Read, Write };
    public class OldRequest
    {
        public ulong blockAddress;
        public ulong timeOfArrival;
        public int threadID;
        public bool isMarked;
        public OldInstructionType type;
        public ulong associatedAddressPacketInjectionTime;
        public ulong associatedAddressPacketCreationTime;

        public Packet carrier; // this is the packet which is currently moving the request through the system

        //members copied from Req class of FairMemSim for use in MCs
        public int m_index;         ///<memory index pertaining to the block address
        public int b_index;         ///<bank index pertaining to the block address
        public ulong r_index;       ///<row index pertaining to the block address
        public int glob_b_index;    ///<global bank index (for central arbiter)

        //scheduling related
        public MemSched sched;      ///<the memory scheduler this request is destined to; determined by memory index
        public int buf_index;       ///<within the memory scheduler, this request's index in the buffer; saves the effort of searching through entire buffer

        public int bufferSlot; // that is just an optimization that I don't need to search over the buffer anymore!

        private double frontStallsCaused;
        private double backStallsCaused;
        private ulong[] locationCycles; // record how many cycles spent in each Request.TimeSources location

        public OldRequest()
        {
            isMarked = false;
        }

        public override string ToString()
        {
            return "Request: ProcID=" + threadID + " IsMarked=" + isMarked + /*" Bank=" + bankIndex.ToString() + " Row=" + rowIndex.ToString() + */" Block=" + (blockAddress).ToString() + "  " + type.ToString();
        }


        public void initialize(ulong blockAddress)
        {
            this.blockAddress = blockAddress;

            frontStallsCaused = 0;
            backStallsCaused = 0;
            locationCycles = new ulong[Enum.GetValues(typeof(Request.DelaySources)).Length];

            if (Config.PerfectLastLevelCache)
                return;

            ulong shift_row;
            ulong shift_mem;
            ulong shift_bank;

            switch (Config.memory.address_mapping)
            {

                case AddressMap.BMR:
                    /**
                     * row-level striping (inter-mem): default
                     * RMS (BMR; really original)
                     */
                    shift_row = blockAddress >> Config.memory.row_bit;

                    m_index = (int)(shift_row % (ulong)Config.memory.mem_max);
                    shift_mem = (ulong)(shift_row >> Config.memory.mem_bit);

                    b_index = (int)(shift_mem % (ulong)Config.memory.bank_max_per_mem);
                    r_index = (ulong)(shift_mem >> Config.memory.bank_bit);
                    break;

                case AddressMap.BRM:
                    /**
                     * block-level striping (inter-mem)
                     * BMS (BRM; original)
                     */

                    m_index = (int)(blockAddress % (ulong)Config.memory.mem_max);
                    shift_mem = blockAddress >> Config.memory.mem_bit;

                    shift_row = shift_mem >> Config.memory.row_bit;

                    b_index = (int)(shift_row % (ulong)Config.memory.bank_max_per_mem);
                    r_index = (ulong)(shift_row >> Config.memory.bank_bit);

                    break;

                case AddressMap.MBR:
                    /**
                     * row-level striping (inter-bank)
                     * RBS (MBR; new)
                     */

                    shift_row = blockAddress >> Config.memory.row_bit;

                    b_index = (int)(shift_row % (ulong)Config.memory.bank_max_per_mem);
                    shift_bank = (ulong)(shift_row >> Config.memory.bank_bit);

                    m_index = (int)(shift_bank % (ulong)Config.memory.mem_max);
                    r_index = (ulong)(shift_bank >> Config.memory.mem_bit);
                    break;

                case AddressMap.MRB:
                    /**
                     * block-level striping (inter-bank)
                     * BBS
                     */
                    //Console.WriteLine(blockAddress.ToString("x"));
                    b_index = (int)(blockAddress % (ulong)Config.memory.bank_max_per_mem);
                    shift_bank = blockAddress >> Config.memory.bank_bit;

                    shift_row = shift_bank >> Config.memory.row_bit;

                    m_index = (int)(shift_row % (ulong)Config.memory.mem_max);
                    r_index = shift_row >> Config.memory.mem_bit;
                    //Console.WriteLine("bmpm:{0} bb:{1} b:{2} m:{3} r:{4}", Config.memory.bank_max_per_mem, Config.memory.bank_bit, b_index.ToString("x"), m_index.ToString("x"), r_index.ToString("x"));
                    break;

                default:
                    throw new Exception("Unknown address map!");
            }

            //scheduling related
            //sched = Config.memory.mem[m_index].sched;
            sched = null;
            isMarked = false;

            glob_b_index = b_index;
        }


        public void blameFrontStall(double weight)
        {
            frontStallsCaused += weight;
        }

        public void blameBackStall(double weight)
        {
            backStallsCaused += weight;
        }

        /*
        public void blameCycle()
        {
            Request.DelaySources staller = Request.DelaySources.UNKNOWN;

            if (carrier.GetType() == typeof(MemoryRequest))
                staller = Request.DelaySources.MEMORY;
            else if (carrier.GetType() == typeof(Packet))
            {
                bool injected = ((Packet)carrier).injectionTime != ulong.MaxValue;
                if (carrier.GetType() == typeof(CachePacket))
                {
                    CachePacket carrierPacket = (CachePacket)carrier;
                    switch (carrierPacket.type)
                    {
                        case CachePacketType.RD:
                            staller = injected ? Request.DelaySources.ADDR_PACKET : Request.DelaySources.INJ_ADDR_PACKET;
                            break;
                        case CachePacketType.DAT_EX:
                        case CachePacketType.DAT_SHR:
                            staller = injected ? Request.DelaySources.DATA_PACKET : Request.DelaySources.INJ_DATA_PACKET;
                            break;
                        default:
                            throw new Exception("Unsupported packet type carrying request");
                    }
                }
                else if (carrier.GetType() == typeof(MemoryPacket))
                {
                    MemoryPacket carrierPacket = (MemoryPacket)carrier;
                    switch (carrierPacket.type)
                    {
                        case MemoryRequestType.RD:
                            if (m_index == int.MaxValue)
                            {
                                staller = Request.DelaySources.MEMORY;
                                break;
                            }
                            staller = injected ? Request.DelaySources.MC_ADDR_PACKET : Request.DelaySources.INJ_MC_ADDR_PACKET;
                            break;
                        case MemoryRequestType.DAT:
                            staller = injected ? Request.DelaySources.MC_DATA_PACKET : Request.DelaySources.INJ_MC_DATA_PACKET;
                            break;
                        default:
                            throw new Exception("Unsupported packet type carrying request");
                    }
                }
                else
                {
                    //unknown!
                    staller = Request.DelaySources.UNKNOWN;
                }
                locationCycles[(int)staller]++;
            }
        }
        */

        public void storeStats()
        {
            double sum = 0;
            foreach (double d in locationCycles)
                sum += d;
            for (int i = 0; i < locationCycles.Length; i++)
            {
                //Simulator.stats.front_stalls_persrc[threadID].Add(i, frontStallsCaused * locationCycles[i] / sum);
                //Simulator.stats.back_stalls_persrc[threadID].Add(i, backStallsCaused * locationCycles[i] / sum);
            }
        }
    }
}
