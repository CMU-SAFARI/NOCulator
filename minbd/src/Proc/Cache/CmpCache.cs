/*
 * CmpCache
 *
 * Chris Fallin <cfallin@ece.cmu.edu>, 2010-09-12
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

/*
   The coherence timing model is a compromise between 100% fidelity (full
   protocol FSMs) and speed/simplicity. The model has a functional/timing
   split: state is updated immediately in the functional model (cache contents
   and block states), and a chain (actually, DAG) of packets with dependencies
   is generated for each transaction. This chain of packets is then sent
   through the interconnect timing model, respecting dependencies, and the
   request completes in the timing model when all packets are delivered.

   Note that when there is no write-contention, and there are no
   memory-ordering races, this model should give 100% fidelity. It gives up
   accuracy in the case where write contention and/or interconnect reordering
   hit corner cases of the protocol; however, the hope is that these cases will
   be rare (if the protocol is well-behaved). Functional-level behavior such as
   ping-ponging is still captured; only behavior such as protocol-level retries
   (in a NACK-based recovery scheme) or unfairness due to interconnect delay in
   write contention are not captured. The great strength of this approach is
   that we *need not work out* the corner cases -- this gives robustness and
   speed, and allows implementations to be very simple, by considering
   transactions as atomic DAGs of packets that never execute simultaneously
   with any other transaction on the same block.

   The sequence for a request is:

   - A CmpCache_Txn (transaction) is generated for a read-miss, write-miss, or
     write-upgrade. The functional state is updated here, and the deltas (nodes
     that were invalidated, downgraded; type of transaction; whether it missed
     in shared cache) are recorded in the txn for timing to use. The functional
     update *must* happen immediately: if it waits until the packets are
     actually delivered (i.e., the timing model says that things are actually
     done), then multiple transactions might start with the same (out-of-date)
     state without awareness of each other.

   - The subclass of CmpCache that implements the protocol implements
     gen_proto(). This routine takes the transaction and generates a DAG of
     packets.

   - Packets flow through the interconnect. When each is delivered, it iterates
     over its wakeup list and decrements the remaining-deps counts on each
     dependent packet. If this reaches zero on a packet, that packet is sent.
     Also, the remaining-packets count on the transaction is decremented at
     each delivery.

   - When all of a transaction's packets are delivered, it is complete.
 */

namespace ICSimulator
{
    struct CmpCache_Owners
    {
        ulong[] bitmask;
        const int ULONG_BITS = 64;   // maximum of 4096 caches

        public CmpCache_Owners(int junk)
        {
          this.bitmask = new ulong[ULONG_BITS];
          for(int i=0; i < ULONG_BITS; i++)
            bitmask[i] = 0;
        }

        public void set(int i) { int r = i/ULONG_BITS; bitmask[r] |= ((ulong)1) << (i % ULONG_BITS); }
        public void unset(int i) { int r = i/ULONG_BITS; bitmask[r] &= ~( ((ulong)1) << (i % ULONG_BITS) ); }

        public void reset() { for(int i=0; i<ULONG_BITS; i++) bitmask[i] =0; }

        public bool is_set(int i) { int r = i/ULONG_BITS; return (bitmask[r] & ( ((ulong)1) << (i % ULONG_BITS) )) != 0; }
        public bool others_set(int i) { 
          int r = i/ULONG_BITS; 

          // Special check for the bits in the same ulong
          for(int j=0; j< ULONG_BITS; j++) {
            if(r==j)
              if( (bitmask[j] & ~( ((ulong)1) << (i % ULONG_BITS) )) != 0)
                return true;
            if(bitmask[j] != 0)
              return true;
          }
          return false;
        }
        public bool any_set() { 
          for(int i=0; i<ULONG_BITS; i++) 
            if(bitmask[i] != 0)
              return true;
          return false;
        }
    }

    class CmpCache_State 
    {
        public CmpCache_Owners owners = new CmpCache_Owners(0); // bitmask of owners
        public int excl; // single node that has exclusive grant, -1 otherwise
        public bool modified; // exclusive copy is modified?
        public bool sh_dirty; // copy in shared cache is dirty?

        public CmpCache_State()
        {
            excl = -1;
            modified = false;
            sh_dirty = false;
        }
    }

    // a DAG of CmpCache_Pkt instances is created for each CmpCache_Txn. The set
    // of packets, with dependencies, is the work required by the cache coherence
    // protocol to complete the transaction.
    public class CmpCache_Pkt 
    {
        public bool send; // send a packet at this node (nodes can also be join-points w/o send)
        public int from, to;
        public ulong id;
        public int flits;
        public bool off_crit;

        public int vc_class;

        public bool done; // critical-path completion after this packet?

        public bool mem; // virtual node for going to memory
        public ulong mem_addr;
        public bool mem_write;
        public int mem_requestor;

        public ulong delay; // delay before sending (e.g., to model cache response latency)

        // out-edges
        public List<CmpCache_Pkt> wakeup; // packets that depend on this one
        // in-edges
        public int deps; // number of packets for which we are still waiting

        // associated txn
        public CmpCache_Txn txn;

        public CmpCache_Pkt ()
        {
            send = false;
            from = to = 0;
            id = 0;
            done = false;
            off_crit = false;

            mem = false;
            mem_addr = 0;

            delay = 0;
            deps = 0;

            txn = null;
        }
    }

    // a transaction is one client-initiated operation that requires a protocol
    // interaction (read or write miss, or upgrade).
    //
    // A generic cache-coherence transaction is classified along these axes:
    //
    //    Control ops:
    //  - data grant: we grant a lease (either shared or excl) to zero or one
    //                nodes per transaction. Could be due to miss or upgrade
    //                (i.e., may or may not transfer data).
    //  - data rescind: we rescind a lease (downgrade or invalidate) from these
    //                  nodes. Could be due to another node's upgrade or due to
    //                  a shared-cache replacement.
    //        - additionally, we distinguish data rescinds due to private-cache invalidates
    //          from those due to shared-cache evictions that require private cache invals.
    //
    //    Data ops:
    //  - data sh->prv: transfer data from shared to private (normal data grant)
    //  - data prv->prv: transfer data from private to private (cache-to-cache xfer)
    //  - data prv->sh: transfer data from private to shared (writeback)
    //  - data mem->sh: transfer data from memory to shared (sh cache miss)
    //  - data sh->mem: transfer data from shared to memory (sh cache writeback)
    //
    // The dependency ordering of the above is:
    //
    //    (CASE 1) not in requestor's cache, present in other private caches:
    //
    //    control request (client -> shared)
    //    inval data rescinds (others -> shared or client directly) if exclusive req
    //    inval prv->sh / prv->prv (data writeback if others invalidated)
    //    -OR-
    //    transfer prv->prv
    //    WB of evicted block if dirty
    //
    //    (CASE 2) not present in other private caches, but present in sh cache: 
    //
    //    control request (client->shared)
    //    data grant (sh->prv), transfer (sh->prv)
    //    data transfer (writeback) prv->sh upon replacement (must hit, due to inclusive cache)
    //    (CASE 3) not present in other private caches, not present in sh cache:
    //
    //    control request (client->mem)
    //    mem request (shared->mem)
    //    mem response
    //    data grant/transfer to prv, AND inval:
    //            - inval requests to owners of evicted sh-cache block, and WB to mem if clean, or WB prv->mem if dirty
    //    
    public class CmpCache_Txn
    {
        /* initiating node */
        public int node;

        /* protocol packet DAG */
        public CmpCache_Pkt pkts;
        public int n_pkts;

        /* updated at each packet arrival */
        public int n_pkts_remaining;

        /* timing completion callback */
        public Simulator.Ready cb;
    };

    public class CmpCache
    {
        ulong m_prvdelay; // private-cache probe delay (both local accesses and invalidates)
        ulong m_shdelay; // probe delay at shared cache (for first access)
        ulong m_opdelay; // pass-through delay once an operation is in progress
        int m_datapkt_size;
        bool m_sh_perfect; // shared cache perfect?
        Dictionary<ulong, CmpCache_State> m_perf_sh;

        int m_blkshift;
        int m_N;

        Sets<CmpCache_State> m_sh;
        Sets<bool>[] m_prv;

        // address mapping for shared cache slices
        //int map_addr(ulong addr) { return Simulator.network.mapping.homeNode(addr >> Config.cache_block).ID; }
        
        int map_addr(int node, ulong addr) { return Simulator.controller.mapCache(node, addr >> Config.cache_block); }

        // address mapping for memory controllers
        //int map_addr_mem(ulong addr) { return Simulator.network.mapping.memNode(addr >> Config.cache_block).ID; }

        int map_addr_mem(int node, ulong addr) { return Simulator.controller.mapMC(node, addr >> Config.cache_block); }

        // closest out of 'nodes' set
        int closest(int node, CmpCache_Owners nodes)
        {
            int best = -1;
            int best_dist = m_N;
            Coord here = new Coord(node);

            for (int i = 0; i < m_N; i++)
                if (nodes.is_set(i))
                {
                    int dist = (int)Simulator.distance(new Coord(i), here);
                    if (dist < best_dist)
                    {
                        best = i;
                        best_dist = dist;
                    }
                }

            return best;
        }

        public CmpCache()
        {
            m_N = Config.N;
            m_blkshift = Config.cache_block;
            m_prv = new Sets<bool>[m_N];
            for (int i = 0; i < m_N; i++)
                m_prv[i] = new Sets<bool>(m_blkshift, 1 << Config.coherent_cache_assoc, 1 << (Config.coherent_cache_size - Config.cache_block - Config.coherent_cache_assoc));

            if (!Config.simple_nocoher)
            {
                if (Config.sh_cache_perfect)
                    m_perf_sh = new Dictionary<ulong, CmpCache_State>();
                else
                    m_sh = new Sets<CmpCache_State>(m_blkshift, 1 << Config.sh_cache_assoc, (1 << (Config.sh_cache_size - Config.cache_block - Config.sh_cache_assoc)) * m_N);
            }

            m_prvdelay = (ulong)Config.cohcache_lat;
            m_shdelay = (ulong)Config.shcache_lat;
            m_opdelay = (ulong)Config.cacheop_lat;
            m_datapkt_size = Config.router.dataPacketSize;
            m_sh_perfect = Config.sh_cache_perfect;
        }

        public void access(int node, ulong addr, bool write, Simulator.Ready cb,
                out bool L1hit, out bool L1upgr, out bool L1ev, out bool L1wb,
                out bool L2access, out bool L2hit, out bool L2ev, out bool L2wb, out bool c2c)
        {
            CmpCache_Txn txn = null;
            int sh_slice = map_addr(node, addr);

            // ------------- first, we probe the cache (private, and shared if necessary) to
            //               determine current state.

            // probe private cache
            CmpCache_State state;
            bool prv_state;
            bool prv_hit = m_prv[node].probe(addr, out prv_state);

            bool sh_hit = false;
            
            if (m_sh_perfect)
            {
                ulong blk = addr >> m_blkshift;
                sh_hit = true;
                if (m_perf_sh.ContainsKey(blk))
                    state = m_perf_sh[blk];
                else
                {
                    state = new CmpCache_State();
                    m_perf_sh[blk] = state;
                }
            }
            else
                sh_hit = m_sh.probe(addr, out state);

            bool prv_excl = sh_hit ? (state.excl == node) : false;

            if (prv_hit)
                // we always update the timestamp on the private cache
                m_prv[node].update(addr, Simulator.CurrentRound);

            // out-params
            L1hit = prv_hit;
            L1upgr = L1hit && !prv_excl;
            L2hit = sh_hit;
            c2c = false; // will be set below for appropriate cases
            L1ev = false; // will be set below
            L1wb = false; // will be set below
            L2ev = false; // will be set below
            L2wb = false; // will be set below
            L2access = false; // will be set below

            // ----------------- now, we execute one of four cases:
            //                   1a. present in private cache, with appropriate ownership.
            //                   1b. present in private cache, but not excl (for a write)
            //                   2. not present in private cache, but in shared cache.
            //                   3. not present in private or shared cache.
            //
            // in each case, we update functional state and generate the packet DAG as we go.

            if (prv_hit && (!write || prv_excl)) // CASE 1a: present in prv cache, have excl if write
            {
                // just set modified-bit in state, then we're done (no protocol interaction)
                if (write) state.modified = true;
            }
            else if (prv_hit && write && !prv_excl) // CASE 1b: present in prv cache, need upgr
            {
                txn = new CmpCache_Txn();
                txn.node = node;

                // request packet
                CmpCache_Pkt req_pkt = add_ctl_pkt(txn, node, sh_slice, false);
                CmpCache_Pkt done_pkt = null;

                // present in others?
                if (state.owners.others_set(node))
                {
                    done_pkt = do_inval(txn, state, req_pkt, node, addr);
                }
                else
                {
                    // not present in others, but we didn't have excl -- send empty grant
                    // (could happen if others have evicted and we are the only one left)
                    done_pkt = add_ctl_pkt(txn, sh_slice, node, true);
                    done_pkt.delay = m_shdelay;
                    add_dep(req_pkt, done_pkt);
                }

                state.owners.reset();
                state.owners.set(node);
                state.excl = node;
                state.modified = true;
            }
            else if (!prv_hit && sh_hit) // CASE 2: not in prv cache, but in sh cache
            {
                txn = new CmpCache_Txn();
                txn.node = node;

                // update functional shared state
                if (!m_sh_perfect)
                    m_sh.update(addr, Simulator.CurrentRound);

                // request packet
                CmpCache_Pkt req_pkt = add_ctl_pkt(txn, node, sh_slice, false);
                CmpCache_Pkt done_pkt = null;

                if (state.owners.any_set()) // in other caches?
                {
                    if (write) // need to invalidate?
                    {
                        if (state.excl != -1) // someone else has exclusive -- c-to-c xfer
                        {
                            c2c = true; // out-param

                            CmpCache_Pkt xfer_req = add_ctl_pkt(txn, sh_slice, state.excl, false);
                            CmpCache_Pkt xfer_dat = add_data_pkt(txn, state.excl, node, true);
                            done_pkt = xfer_dat;

                            xfer_req.delay = m_shdelay;
                            xfer_dat.delay = m_prvdelay;

                            add_dep(req_pkt, xfer_req);
                            add_dep(xfer_req, xfer_dat);

                            bool evicted_state;
                            m_prv[state.excl].inval(addr, out evicted_state);
                        }
                        else // others have it -- inval to all, c-to-c from closest
                        {
                            int close = closest(node, state.owners);
                            if (close != -1) c2c = true; // out-param

                            done_pkt = do_inval(txn, state, req_pkt, node, addr, close);
                        }

                        // for a write, we need exclusive -- update state
                        state.owners.reset();
                        state.owners.set(node);
                        state.excl = node;
                        state.modified = true;
                    }
                    else // just a read -- joining sharer set, c-to-c from closest
                    {

                        if (state.excl != -1)
                        {
                            CmpCache_Pkt xfer_req = add_ctl_pkt(txn, sh_slice, state.excl, false);
                            CmpCache_Pkt xfer_dat = add_data_pkt(txn, state.excl, node, true);
                            done_pkt = xfer_dat;

                            c2c = true; // out-param

                            xfer_req.delay = m_shdelay;
                            xfer_dat.delay = m_prvdelay;

                            add_dep(req_pkt, xfer_req);
                            add_dep(xfer_req, xfer_dat);

                            // downgrade must also trigger writeback
                            if (state.modified)
                            {
                                CmpCache_Pkt wb_dat = add_data_pkt(txn, state.excl, sh_slice, false);
                                add_dep(xfer_req, wb_dat);
                                state.modified = false;
                                state.sh_dirty = true;
                            }
                        }
                        else
                        {
                            int close = closest(node, state.owners);
                            if (close != -1) c2c = true; // out-param

                            CmpCache_Pkt xfer_req = add_ctl_pkt(txn, sh_slice, close, false);
                            CmpCache_Pkt xfer_dat = add_data_pkt(txn, close, node, true);
                            done_pkt = xfer_dat;

                            xfer_req.delay = m_shdelay;
                            xfer_dat.delay = m_prvdelay;

                            add_dep(req_pkt, xfer_req);
                            add_dep(xfer_req, xfer_dat);
                        }

                        state.owners.set(node);
                        state.excl = -1;
                    }
                }
                else
                {
                    // not in other prv caches, need to get from shared slice
                    L2access = true;

                    CmpCache_Pkt dat_resp = add_data_pkt(txn, sh_slice, node, true);
                    done_pkt = dat_resp;

                    add_dep(req_pkt, done_pkt);

                    dat_resp.delay = m_shdelay;

                    state.owners.reset();
                    state.owners.set(node);
                    state.excl = node;
                    state.modified = write;
                }

                // insert into private cache, get evicted block (if any)
                ulong evict_addr;
                bool evict_data;
                bool evicted = m_prv[node].insert(addr, true, out evict_addr, out evict_data, Simulator.CurrentRound);

                // add either a writeback or a release packet
                if (evicted)
                {
                    L1ev = true;
                    do_evict(txn, done_pkt, node, evict_addr, out L1wb);
                }
            }
            else if (!prv_hit && !sh_hit) // CASE 3: not in prv or shared cache
            {
                // here, we need to go to memory
                Debug.Assert(!m_sh_perfect);

                txn = new CmpCache_Txn();
                txn.node = node;

                L2access = true;

                // request packet
                CmpCache_Pkt req_pkt = add_ctl_pkt(txn, node, sh_slice, false);

                // cache response packet
                CmpCache_Pkt resp_pkt = add_data_pkt(txn, sh_slice, node, true);
                resp_pkt.delay = m_opdelay; // req already active -- just a pass-through op delay here

                // memory request packet
                int mem_slice = map_addr_mem(node, addr);
                CmpCache_Pkt memreq_pkt = add_ctl_pkt(txn, sh_slice, mem_slice, false);
                memreq_pkt.delay = m_shdelay;

                // memory-access virtual node
                CmpCache_Pkt mem_access = add_ctl_pkt(txn, 0, 0, false);
                mem_access.send = false;
                mem_access.mem = true;
                mem_access.mem_addr = addr;
                mem_access.mem_write = false; // cache-line fill
                mem_access.mem_requestor = node;

                // memory response packet
                CmpCache_Pkt memresp_pkt = add_data_pkt(txn, mem_slice, sh_slice, false);

                // connect up the critical path first
                add_dep(req_pkt, memreq_pkt);
                add_dep(memreq_pkt, mem_access);
                add_dep(mem_access, memresp_pkt);
                add_dep(memresp_pkt, resp_pkt);

                // now, handle replacement in the shared cache...
                CmpCache_State new_state = new CmpCache_State();

                new_state.owners.reset();
                new_state.owners.set(node);
                new_state.excl = node;
                new_state.modified = write;
                new_state.sh_dirty = false;

                ulong sh_evicted_addr;
                CmpCache_State sh_evicted_state;
                bool evicted = m_sh.insert(addr, new_state, out sh_evicted_addr, out sh_evicted_state, Simulator.CurrentRound);

                if (evicted)
                {
                    // shared-cache eviction (different from the private-cache evictions elsewhere):
                    // we must evict any private-cache copies, because we model an inclusive hierarchy.

                    L2ev = true;

                    CmpCache_Pkt prv_evict_join = add_joinpt(txn, false);

                    if (sh_evicted_state.excl != -1) // evicted block lives only in one prv cache
                    {
                        // invalidate request to prv cache before sh cache does eviction
                        CmpCache_Pkt prv_invl = add_ctl_pkt(txn, sh_slice, sh_evicted_state.excl, false);
                        add_dep(memresp_pkt, prv_invl);
                        CmpCache_Pkt prv_wb;

                        prv_invl.delay = m_opdelay;

                        if (sh_evicted_state.modified)
                        {
                            // writeback
                            prv_wb = add_data_pkt(txn, sh_evicted_state.excl, sh_slice, false);
                            prv_wb.delay = m_prvdelay;
                            sh_evicted_state.sh_dirty = true;
                        }
                        else
                        {
                            // simple ACK
                            prv_wb = add_ctl_pkt(txn, sh_evicted_state.excl, sh_slice, false);
                            prv_wb.delay = m_prvdelay;
                        }

                        add_dep(prv_invl, prv_wb);
                        add_dep(prv_wb, prv_evict_join);

                        bool prv_evicted_dat;
                        m_prv[sh_evicted_state.excl].inval(sh_evicted_addr, out prv_evicted_dat);
                    }
                    else if (sh_evicted_state.owners.any_set()) // evicted block has greater-than-one sharer set
                    {
                        for (int i = 0; i < m_N; i++)
                            if (sh_evicted_state.owners.is_set(i))
                            {
                                CmpCache_Pkt prv_invl = add_ctl_pkt(txn, sh_slice, i, false);
                                CmpCache_Pkt prv_ack = add_ctl_pkt(txn, i, sh_slice, false);

                                prv_invl.delay = m_opdelay;
                                prv_ack.delay = m_prvdelay;

                                add_dep(memresp_pkt, prv_invl);
                                add_dep(prv_invl, prv_ack);
                                add_dep(prv_ack, prv_evict_join);

                                bool prv_evicted_dat;
                                m_prv[i].inval(sh_evicted_addr, out prv_evicted_dat);
                            }
                    }
                    else // evicted block has no owners (was only in shared cache)
                    {
                        add_dep(memresp_pkt, prv_evict_join);
                    }

                    // now writeback to memory, if we were dirty
                    if (sh_evicted_state.sh_dirty)
                    {
                        CmpCache_Pkt mem_wb = add_data_pkt(txn, sh_slice, mem_slice, false);
                        mem_wb.delay = m_opdelay;
                        add_dep(prv_evict_join, mem_wb);
                        CmpCache_Pkt mem_wb_op = add_ctl_pkt(txn, 0, 0, false);
                        mem_wb_op.send = false;
                        mem_wb_op.mem = true;
                        mem_wb_op.mem_addr = sh_evicted_addr;
                        mem_wb_op.mem_write = true;
                        mem_wb_op.mem_requestor = node;
                        add_dep(mem_wb, mem_wb_op);
                        L2wb = true;
                    }
                }

                // ...and insert and handle replacement in the private cache
                ulong evict_addr;
                bool evict_data;
                bool prv_evicted = m_prv[node].insert(addr, true, out evict_addr, out evict_data, Simulator.CurrentRound);

                // add either a writeback or a release packet
                if (prv_evicted)
                {
                    L1ev = true;
                    do_evict(txn, resp_pkt, node, evict_addr, out L1wb);
                }
            }
            else // shouldn't happen.
                Debug.Assert(false);

            // now start the transaction, if one was needed
            if (txn != null)
            {
                txn.cb = cb;

                assignVCclasses(txn.pkts);

                // start running the protocol DAG. It may be an empty graph (for a silent upgr), in
                // which case the deferred start (after cache delay)
                Simulator.Defer(delegate()
                        {
                        start_pkts(txn);
                        }, Simulator.CurrentRound + m_prvdelay);
            }
            // no transaction -- just the cache access delay. schedule deferred callback.
            else
            {
                Simulator.Defer(cb, Simulator.CurrentRound + m_prvdelay);
            }

        }

        // evict a block from given node, and construct either writeback or release packet.
        // updates functional state accordingly.
        void do_evict(CmpCache_Txn txn, CmpCache_Pkt init_dep, int node, ulong evict_addr, out bool wb)
        {
            ulong blk = evict_addr >> m_blkshift;
            int sh_slice = map_addr(node, evict_addr);

            CmpCache_State evicted_st;
            if (m_sh_perfect)
            {
                Debug.Assert(m_perf_sh.ContainsKey(blk));
                evicted_st = m_perf_sh[blk];
            }
            else
            {
                bool hit = m_sh.probe(evict_addr, out evicted_st);
                Debug.Assert(hit); // inclusive sh cache -- MUST be present in sh cache
            }

            if(evicted_st.excl == node && evicted_st.modified)
            {
                CmpCache_Pkt wb_pkt = add_data_pkt(txn, node, sh_slice, false);
                wb_pkt.delay = m_opdelay; // pass-through delay: operation already in progress
                add_dep(init_dep, wb_pkt);

                evicted_st.owners.reset();
                evicted_st.excl = -1;
                evicted_st.sh_dirty = true;
                wb = true;
            }
            else
            {
                CmpCache_Pkt release_pkt = add_ctl_pkt(txn, node, sh_slice, false);
                release_pkt.delay = m_opdelay;
                add_dep(init_dep, release_pkt);

                evicted_st.owners.unset(node);
                if (evicted_st.excl == node) evicted_st.excl = -1;
                wb = false;
            }

            if (m_sh_perfect && !evicted_st.owners.any_set())
                m_perf_sh.Remove(blk);
        }

        // construct a set of invalidation packets, all depending on init_dep, and
        // joining at a join-point that we return. Also invalidate the given addr
        // in the other prv caches.
        CmpCache_Pkt do_inval(CmpCache_Txn txn, CmpCache_State state, CmpCache_Pkt init_dep, int node, ulong addr)
        {
            return do_inval(txn, state, init_dep, node, addr, -1);
        }
        CmpCache_Pkt do_inval(CmpCache_Txn txn, CmpCache_State state, CmpCache_Pkt init_dep, int node, ulong addr, int c2c)
        {
            int sh_slice = map_addr(node, addr);

            // join-point (virtual packet). this is the completion point (DONE flag)
            CmpCache_Pkt invl_join = add_joinpt(txn, true);

            // invalidate from shared slice to each other owner
            for (int i = 0; i < m_N; i++)
                if (state.owners.is_set(i) && i != node)
                {
                    CmpCache_Pkt invl_pkt = add_ctl_pkt(txn, sh_slice, i, false);
                    invl_pkt.delay = m_shdelay;

                    CmpCache_Pkt invl_resp =
                        (c2c == i) ?
                        add_data_pkt(txn, i, node, false) :
                        add_ctl_pkt(txn, i, node, false);
                    invl_resp.delay = m_prvdelay;

                    add_dep(init_dep, invl_pkt);
                    add_dep(invl_pkt, invl_resp);
                    add_dep(invl_resp, invl_join);

                    // invalidate in this prv cache.
                    bool evicted_data;
                    m_prv[i].inval(addr, out evicted_data);
                }

            return invl_join;
        }

        ulong pkt_id = 0;

        CmpCache_Pkt _add_pkt(CmpCache_Txn txn, int from, int to, bool data, bool send, bool done)
        {
            Debug.Assert(to >= 0 && to < m_N);

            CmpCache_Pkt pkt = new CmpCache_Pkt();
            pkt.wakeup = new List<CmpCache_Pkt>();
            pkt.id = pkt_id++;
            pkt.from = from;
            pkt.to = to;
            pkt.txn = txn;

            pkt.flits = data ? m_datapkt_size : 1;

            pkt.vc_class = 0; // gets filled in once DAG is complete

            pkt.done = done;
            pkt.send = send;

            pkt.deps = 0;
            pkt.delay = 0;
            pkt.mem_addr = 0;

            txn.n_pkts++;
            txn.n_pkts_remaining++;

            if (txn.pkts == null)
                txn.pkts = pkt;

            return pkt;
        }

        CmpCache_Pkt add_ctl_pkt(CmpCache_Txn txn, int from, int to, bool done)
        {
            return _add_pkt(txn, from, to, false, true, done);
        }

        CmpCache_Pkt add_data_pkt(CmpCache_Txn txn, int from, int to, bool done)
        {
            return _add_pkt(txn, from, to, true, true, done);
        }

        CmpCache_Pkt add_joinpt(CmpCache_Txn txn, bool done)
        {
            return _add_pkt(txn, 0, 0, false, false, done);
        }

        void add_dep(CmpCache_Pkt from, CmpCache_Pkt to)
        {
            from.wakeup.Add(to);
            to.deps++;
        }

        void start_pkts(CmpCache_Txn txn)
        {
            if (txn.n_pkts_remaining > 0)
                send_pkt(txn, txn.pkts);
            else
                txn.cb();
        }

        void send_pkt(CmpCache_Txn txn, CmpCache_Pkt pkt)
        {
            if (pkt.delay > 0)
            {
                ulong due = Simulator.CurrentRound + pkt.delay;
                pkt.delay = 0;
                Simulator.Defer(delegate()
                        {
                        send_pkt(txn, pkt);
                        }, due);
            }
            else if (pkt.send)
            {
                send_noc(txn.node, pkt.from, pkt.to, pkt.flits,
                        delegate()
                        {
                        pkt_callback(txn, pkt);
                        }, pkt.off_crit, pkt.vc_class);
            }
            else if (pkt.mem)
            {
                access_mem(pkt.mem_requestor, pkt.mem_addr, pkt.mem_write,
                        delegate()
                        {
                        pkt_callback(txn, pkt);
                        });
            }
            else
                pkt_callback(txn, pkt);
        }

        void pkt_callback(CmpCache_Txn txn, CmpCache_Pkt pkt)
        {
            txn.n_pkts_remaining--;

            if (pkt.done)
                txn.cb();


            foreach (CmpCache_Pkt dep in pkt.wakeup)
            {
                if (pkt.done || pkt.off_crit) dep.off_crit = true;

                dep.deps--;
                if (dep.deps == 0)
                    send_pkt(txn, dep);
            }
        }

        void send_noc(int reqNode, int from, int to, int flits, Simulator.Ready cb, bool off_crit, int vc)
        {
            int cl = off_crit ? 2 : // packet class (used for split queues): 0 = ctl, 1 = data, 2 = off-crit (writebacks)
                (flits > 1 ? 1 : 0);

            CachePacket p = new CachePacket(reqNode, from, to, flits, cl, vc, cb);
            Simulator.network.nodes[from].queuePacket(p);
        }

        void access_mem(int requestor, ulong addr, bool write, Simulator.Ready cb)
        {
            Request req = new Request(requestor, addr, write);

            int node = map_addr_mem(requestor, addr);
            Simulator.network.nodes[node].mem.access(req, cb);
        }

        private Queue<CmpCache_Pkt> workQ = new Queue<CmpCache_Pkt>(); // avoid alloc'ing this for each call
        void assignVCclasses(CmpCache_Pkt root)
        {
            // basic idea: we traverse the DAG using a work-list algorithm, assigning VC classes as follows:
            //  - any network packet node sets the VC of its successors to at least its own VC plus 1.
            //  - any data packet gets VC at least 4.
            //  - non-network-packet nodes carry VC numbers anyway to propagate dependence information.
            //  - VC classes start at 0 and increment as this algo runs.
            workQ.Enqueue(root);
            while (workQ.Count > 0)
            {
                CmpCache_Pkt pkt = workQ.Dequeue();
                if (pkt.flits > 1) pkt.vc_class = Math.Max(4, pkt.vc_class);
                int succ = pkt.send ? pkt.vc_class + 1 : pkt.vc_class;
                foreach (CmpCache_Pkt s in pkt.wakeup)
                {
                    int old = s.vc_class;
                    s.vc_class = Math.Max(succ, s.vc_class);
                    if (s.vc_class > old) workQ.Enqueue(s);
                }
            }
        }
    }

}
