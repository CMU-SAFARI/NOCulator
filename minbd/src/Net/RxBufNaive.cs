using System;

namespace ICSimulator
{
    public enum RxBufMode { JIT, PSA, PROTO, NONE };

    public class RxBufNaive
    {
        // --- rxbuf
        
        // JIT: just need list of buffered flits
        Flit[] m_rx_jit_buf;

        // PSA: need list of buffered packets and state for each flit
        // in each slot
        enum PSAState { FULL, EMPTY, EVICT }
        Packet[] m_rx_psa_buf;
        PSAState[,] m_rx_psa_state;
        Flit[,] m_rx_psa_flits;

        // ---

        public delegate void Evictor(Flit f);
        public delegate void Acceptor(Packet p);

        Node m_n;
        Evictor m_ev;
        Acceptor m_a;

        public RxBufNaive(Node n, Evictor ev, Acceptor a)
        {
            m_n = n;
            m_ev = ev;
            m_a = a;

            switch (Config.rxbuf_mode)
            {
            case RxBufMode.JIT:
                m_rx_jit_buf = new Flit[Config.rxbuf_size];
                for (int i = 0; i < m_rx_jit_buf.Length; i++) m_rx_jit_buf[i] = null;
                break;
            case RxBufMode.PSA:
                m_rx_psa_buf = new Packet[Config.rxbuf_size / Config.router.maxPacketSize];
                m_rx_psa_state = new PSAState[m_rx_psa_buf.Length, Config.router.maxPacketSize];
                m_rx_psa_flits = new Flit[m_rx_psa_buf.Length, Config.router.maxPacketSize];
                for (int i = 0; i < m_rx_psa_buf.Length; i++)
                {
                    m_rx_psa_buf[i] = null;
                    for (int j = 0; j < Config.router.maxPacketSize; j++)
                    {
                        m_rx_psa_state[i,j] = PSAState.EMPTY;
                        m_rx_psa_flits[i,j] = null;
                    }
                }
                break;
            case RxBufMode.NONE:
                break;
            }
        }

        void evictFlit(Flit f)
        {
            f.packet.nrOfArrivedFlits--;
            m_ev(f);
        }

        void ejectPacket(Packet p)
        {
            m_a(p);
        }

        // accept one ejected flit into rxbuf
        public void acceptFlit(Flit f)
        {
            switch (Config.rxbuf_mode)
            {
            case RxBufMode.JIT:
            {
                // try to find a free buffer slot first
                int slot = -1;
                for (int i = 0; i < m_rx_jit_buf.Length; i++)
                    if (m_rx_jit_buf[i] == null)
                    {
                        slot = i;
#if DEBUG
                        Console.WriteLine("ID {0}: flit {1}.{2} into slot {3} ({4} of {5}) (age {6})",
                                          m_n.coord.ID, f.packet.ID, f.flitNr, slot, f.packet.nrOfArrivedFlits, f.packet.nrOfFlits, Simulator.CurrentRound - f.packet.creationTime);
#endif
                        break;
                    }
                if (slot == -1 && Config.naive_rx_buf_evict)
                {
                    // must evict: find lowest-prio victim
                    int best = -1; // best means lowest-age
                    for (int i = 0; i < m_rx_jit_buf.Length; i++)
                        if (best == -1 ||
                            rank(m_rx_jit_buf[i], m_rx_jit_buf[best]) > 0)
                            best = i;

                    // if lowest-prio is lower than current flit, evict and make space
                    if (rank(m_rx_jit_buf[best], f) > 0)
                    {
                        evictFlit(m_rx_jit_buf[best]);
                        slot = best;

#if DEBUG
                        Console.WriteLine("ID {0}: flit {1}.{2} into slot {3} (age {4} evict-age {55555
                                          m_n.coord.ID, f.packet.ID, f.flitNr, slot,
                                          Simulator.CurrentRound - f.packet.creationTime,
                                          Simulator.CurrentRound - m_evictSlot.packet.creationTime);
#endif
                    }
                }

                if (slot != -1)
                {
                    // fill slot, and deliver packet if complete
                    m_rx_jit_buf[slot] = f;
                    f.packet.nrOfArrivedFlits++;
                    if (f.packet.nrOfArrivedFlits == f.packet.nrOfFlits)
                    {
#if DEBUG
                        Console.WriteLine("ID {0}: deliver {1} ({2} flits)", m_n.coord.ID, f.packet.ID, f.packet.nrOfFlits);
#endif
                        // if delivered packet, free slots
                        for (int i = 0; i < m_rx_jit_buf.Length; i++)
                            if (m_rx_jit_buf[i] != null &&
                                m_rx_jit_buf[i].packet == f.packet)
                            {
#if DEBUG
                                Console.WriteLine("ID {0}: nulling {1}", m_n.coord.ID, i);
#endif
                                m_rx_jit_buf[i] = null;
                            }

                        ejectPacket(f.packet);
                    }
                }
                else
                {
                    // no space and can't evict -- must bounce
                    m_ev(f);
#if DEBUG
                    Console.WriteLine("ID {0}: bouncing {1}.{2}", m_n.coord.ID, f.flitNr, f.packet.ID);
#endif
                }

                break;
            }

            case RxBufMode.PSA:
            {
                // step 1: belongs to a currently buffering packet?
                int currentlyBuf = -1;
                for (int i = 0; i < m_rx_psa_buf.Length; i++)
                    if (m_rx_psa_buf[i] == f.packet)
                    {
                        currentlyBuf = i;
                        break;
                    }
                if (currentlyBuf == -1) // not buffered: try to allocate empty slot
                {
                    for (int i = 0; i < m_rx_psa_buf.Length; i++)
                        if (m_rx_psa_buf[i] == null)
                        {
                            currentlyBuf = i;
                            m_rx_psa_buf[i] = f.packet;
                            break;
                        }
                }
                if (currentlyBuf == -1 && Config.naive_rx_buf_evict) // still not buffered: try to evict a lower-prio pkt
                {
                    int best = -1;
                    for (int i = 0; i < m_rx_psa_buf.Length; i++)
                        if (m_rx_psa_buf[i] != null && rank(m_rx_psa_buf[i].flits[0], f) > 0 &&
                            (best == -1 ||
                             rank(m_rx_psa_buf[i].flits[0], m_rx_psa_buf[best].flits[0]) > 0))
                            best = i;
                    if (best != -1)
                    {
                        for (int i = 0; i < Config.router.maxPacketSize; i++)
                            if (m_rx_psa_flits[best, i] != null)
                                m_rx_psa_state[best, i] = PSAState.EVICT;

                        m_rx_psa_buf[best] = null;
                    
                        currentlyBuf = best;
                        m_rx_psa_buf[best] = f.packet;
                    }
                }

                if (currentlyBuf != -1)
                {
                    PSAState slotState = m_rx_psa_state[currentlyBuf, f.flitNr];
                    if (slotState == PSAState.FULL)
                        throw new Exception("ERROR: slot already full");
                    else if (slotState == PSAState.EVICT)
                    {
                        evictFlit(m_rx_psa_flits[currentlyBuf, f.flitNr]);
                        m_rx_psa_state[currentlyBuf, f.flitNr] = PSAState.EMPTY;
                        m_rx_psa_flits[currentlyBuf, f.flitNr] = null;
                    }

                    m_rx_psa_state[currentlyBuf, f.flitNr] = PSAState.FULL;
                    m_rx_psa_flits[currentlyBuf, f.flitNr] = f;
                    f.packet.nrOfArrivedFlits++;
                    if (f.packet.nrOfArrivedFlits == f.packet.nrOfFlits)
                    {
                        ejectPacket(f.packet);
                        for (int i = 0; i < f.packet.nrOfFlits; i++)
                        {
                            m_rx_psa_state[currentlyBuf, i] = PSAState.EMPTY;
                            m_rx_psa_flits[currentlyBuf, i] = null;
                        }
                        m_rx_psa_buf[currentlyBuf] = null;
                    }
                }
                else
                {
                    // can't take it: bounce it right back
                    m_ev(f);
                }

                break;
            }

            case RxBufMode.NONE:
                f.packet.nrOfArrivedFlits++;
                if (f.packet.nrOfArrivedFlits == f.packet.nrOfFlits)
                    ejectPacket(f.packet);
                break;
            }
        }

        int rank(Flit f1, Flit f2)
        {
            return m_n.router.rank(f1, f2);
        }
    }
}
