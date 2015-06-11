using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    public interface IBufRingBackpressure
    {
        bool getCredit(Flit f, object sender, int bubble);
    }

    public class BufRingNetwork_Router : Router
    {
        BufRingNetwork_NIC[] _nics;
        int _nic_count;

        public BufRingNetwork_Router(Coord c) : base(c)
        {
            _nics = new BufRingNetwork_NIC[Config.bufrings_n];
            _nic_count = 0;
        }

        protected override void _doStep()
        {
            // nothing
        }

        public override bool canInjectFlit(Flit f)
        {
            for (int i = 0; i < _nic_count; i++)
                if (_nics[i].Inject == null)
                    return true;
            return false;
        }

        public override void InjectFlit(Flit f)
        {
            int baseIdx = Simulator.rand.Next(_nic_count);
            for (int i = 0; i < _nic_count; i++)
                if (_nics[(i+baseIdx)%_nic_count].Inject == null) {
                    _nics[(i+baseIdx)%_nic_count].Inject = f;
                    return;
                }
            throw new Exception("Could not inject flit -- no free slots!");
        }

        public void statsIJ(Flit f)
        {
            statsInjectFlit(f);
        }

        public void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }

        public void addNIC(BufRingNetwork_NIC nic)
        {
            _nics[_nic_count++] = nic;
        }
    }

    public class BufRingNetwork_NIC : IBufRingBackpressure
    {
        BufRingNetwork_Router _router;
        int _ring, _id;
        IBufRingBackpressure _downstream;

        Flit _inject;
        Link _in, _out;
        Queue<Flit> _buf;
        int _credits;

        public Flit Inject { get { return _inject; } set { _inject = value; } }

        public bool getCredit(Flit f, object sender, int bubble)
        {
            if (_credits > bubble) {
                _credits--;
                return true;
            }
            else
                return false;
        }

        public BufRingNetwork_NIC(int localring, int id)
        {
            _ring = localring;
            _id = id;
            _buf = new Queue<Flit>();
            _credits = Config.bufrings_localbuf;

            _inject = null;
        }

        public void setRouter(BufRingNetwork_Router router)
        {
            _router = router;
        }

        public bool doStep()
        {
            bool somethingMoved = false;

            // handle input from ring
            if (_in.Out != null) {
                Flit f = _in.Out;
                _in.Out = null;

                somethingMoved = true;

                if (f.packet.dest.ID == _router.coord.ID) {
                    _credits++;
                    _router.acceptFlit(f);
                }
                else {
                    _buf.Enqueue(f);
                    Simulator.stats.bufrings_nic_enqueue.Add();
                }
            }

            // handle through traffic
            if (_buf.Count > 0) {
                Flit f = _buf.Peek();
                if (_downstream.getCredit(f, this, 0)) {
                    _buf.Dequeue();
                    _credits++;
                    _out.In = f;
                    Simulator.stats.bufrings_nic_dequeue.Add();
                    somethingMoved = true;
                }
            }

            // handle injections
            if (_out.In == null && _inject != null) {
                if (_downstream.getCredit(_inject, this, 2)) {
                    _out.In = _inject;
                    _router.statsIJ(_inject);
                    _inject = null;
                    Simulator.stats.bufrings_nic_inject.Add();
                    somethingMoved = true;
                }
            }
            if (_inject != null)
                Simulator.stats.bufrings_nic_starve.Add();

            if (_out.In != null)
                Simulator.stats.bufrings_link_traverse[1].Add();

            Simulator.stats.bufrings_nic_occupancy.Add(_buf.Count);

            return somethingMoved;
        }

        public void setInput(Link l)
        {
            _in = l;
        }

        public void setOutput(Link l, IBufRingBackpressure downstream)
        {
            _out = l;
            _downstream = downstream;
        }

        public static void Map(int ID, out int localring, out int localid)
        {
            localring = ID/Config.bufrings_branching;
            localid = ID%Config.bufrings_branching;
        }

        public void nuke(Queue<Flit> flits)
        {
            while (_buf.Count > 0) {
                flits.Enqueue(_buf.Dequeue());
            }
            if (_inject != null)
                flits.Enqueue(_inject);

            _credits = Config.bufrings_localbuf;
            _inject = null;
        }
    }

    public class BufRingNetwork_IRI : IBufRingBackpressure
    {
        Link _gin, _gout, _lin, _lout;
        Queue<Flit> _bufGL, _bufLG, _bufL, _bufG;
        int _creditGL, _creditLG, _creditL, _creditG;
        int _id;
        IBufRingBackpressure _downstreamL, _downstreamG;

        public bool getCredit(Flit f, object sender, int bubble)
        {
            int ring, localid;
            BufRingNetwork_NIC.Map(f.packet.dest.ID, out ring, out localid);

            if (sender is BufRingNetwork_IRI) { // on global ring
                if (ring != _id) {
                    if (_creditG > bubble) {
                        _creditG--;
                        return true;
                    }
                    else
                        return false;
                }
                else {
                    if (_creditGL > bubble) {
                        _creditGL--;
                        return true;
                    }
                    else
                        return false;
                }
            }
            else if (sender is BufRingNetwork_NIC) { // on local ring
                if (ring != _id) {
                    if (_creditLG > bubble) {
                        _creditLG--;
                        return true;
                    }
                    else
                        return false;
                }
                else {
                    if (_creditL > bubble) {
                        _creditL--;
                        return true;
                    }
                    else
                        return false;
                }
            }

            return false;
        }

        public BufRingNetwork_IRI(int localring)
        {
            if (Config.N != Config.bufrings_branching*Config.bufrings_branching)
                throw new Exception("Wrong size for 2-level bufrings network! Check that N = (bufrings_branching)^2.");

            _id = localring;

            _bufGL = new Queue<Flit>();
            _bufLG = new Queue<Flit>();
            _bufL = new Queue<Flit>();
            _bufG = new Queue<Flit>();

            _creditGL = Config.bufrings_G2L;
            _creditLG = Config.bufrings_L2G;
            _creditL = Config.bufrings_localbuf;
            _creditG = Config.bufrings_globalbuf;
        }

        public void setGlobalInput(Link l) { _gin = l; }
        public void setGlobalOutput(Link l, IBufRingBackpressure b) { _gout = l; _downstreamG = b; }
        public void setLocalInput(Link l) { _lin = l; }
        public void setLocalOutput(Link l, IBufRingBackpressure b) { _lout = l; _downstreamL = b; }

        public bool doStep()
        {
            bool somethingMoved = false;

            // handle inputs

            // global input
            if (_gin.Out != null) {
                Flit f = _gin.Out;
                _gin.Out = null;

                somethingMoved = true;

                int ring, localid;
                BufRingNetwork_NIC.Map(f.packet.dest.ID, out ring, out localid);
                if (ring == _id) {
                    _bufGL.Enqueue(f);
                    Simulator.stats.bufrings_iri_enqueue_gl[0].Add();
                }
                else {
                    _bufG.Enqueue(f);
                    Simulator.stats.bufrings_iri_enqueue_g[0].Add();
                }

            }

            // local input
            if (_lin.Out != null) {
                Flit f = _lin.Out;
                _lin.Out = null;

                somethingMoved = true;

                int ring, localid;
                BufRingNetwork_NIC.Map(f.packet.dest.ID, out ring, out localid);
                if (ring == _id) {
                    _bufL.Enqueue(f);
                    Simulator.stats.bufrings_iri_enqueue_l[0].Add();
                }
                else {
                    _bufLG.Enqueue(f);
                    Simulator.stats.bufrings_iri_enqueue_lg[0].Add();
                }
            }

            // handle outputs
            
            // global output (on-ring traffic)
            if (_gout.In == null && _bufG.Count > 0) {
                Flit f = _bufG.Peek();
                if (_downstreamG.getCredit(f, this, 0)) {
                    _bufG.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_g[0].Add();
                    _creditG++;
                    _gout.In = f;
                    somethingMoved = true;
                }
            }
            // global output (transfer traffic)
            if (_gout.In == null && _bufLG.Count > 0) {
                Flit f = _bufLG.Peek();
                if (_downstreamG.getCredit(f, this, 1)) {
                    _bufLG.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_lg[0].Add();
                    _creditLG++;
                    _gout.In = f;
                    somethingMoved = true;
                }
            }

            // local output (on-ring traffic)
            if (_lout.In == null && _bufL.Count > 0) {
                Flit f = _bufL.Peek();
                if (_downstreamL.getCredit(f, this, 0)) {
                    _bufL.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_l[0].Add();
                    _creditL++;
                    _lout.In = f;
                    somethingMoved = true;
                }
            }
            // local output (transfer traffic)
            if (_lout.In == null && _bufGL.Count > 0) {
                Flit f = _bufGL.Peek();
                if (_downstreamL.getCredit(f, this, 1)) {
                    _bufGL.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_gl[0].Add();
                    _creditGL++;
                    _lout.In = f;
                    somethingMoved = true;
                }
            }

            if (_gout.In != null)
                Simulator.stats.bufrings_link_traverse[0].Add();
            if (_lout.In != null)
                Simulator.stats.bufrings_link_traverse[1].Add();

            Simulator.stats.bufrings_iri_occupancy_g[0].Add(_bufG.Count);
            Simulator.stats.bufrings_iri_occupancy_l[0].Add(_bufL.Count);
            Simulator.stats.bufrings_iri_occupancy_gl[0].Add(_bufGL.Count);
            Simulator.stats.bufrings_iri_occupancy_lg[0].Add(_bufLG.Count);

            return somethingMoved;
        }

        public void nuke(Queue<Flit> flits)
        {
            while (_bufGL.Count > 0)
                flits.Enqueue(_bufGL.Dequeue());
            while (_bufG.Count > 0)
                flits.Enqueue(_bufG.Dequeue());
            while (_bufL.Count > 0)
                flits.Enqueue(_bufL.Dequeue());
            while (_bufLG.Count > 0)
                flits.Enqueue(_bufLG.Dequeue());

            _creditGL = Config.bufrings_G2L;
            _creditLG = Config.bufrings_L2G;
            _creditL = Config.bufrings_localbuf;
            _creditG = Config.bufrings_globalbuf;
        }
    }

    public class BufRingNetwork : Network
    {
        BufRingNetwork_NIC[] _nics;
        BufRingNetwork_IRI[] _iris;
        BufRingNetwork_Router[] _routers;

        public BufRingNetwork(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }

        public override void setup()
        {
            // boilerplate
            nodes = new Node[Config.N];
            cache = new CmpCache();
            ParseFinish(Config.finish);
            workload = new Workload(Config.traceFilenames);
            mapping = new NodeMapping_AllCPU_SharedCache();

            links = new List<Link>();
            _routers = new BufRingNetwork_Router[Config.N];

            // create routers and nodes
            for (int n = 0; n < Config.N; n++)
            {
                Coord c = new Coord(n);
                nodes[n] = new Node(mapping, c);
                _routers[n] = new BufRingNetwork_Router(c);
                _routers[n].setNode(nodes[n]);
                nodes[n].setRouter(_routers[n]);
            }

            int B = Config.bufrings_branching;

            // create the NICs and IRIs
            _nics = new BufRingNetwork_NIC[Config.N * Config.bufrings_n];
            _iris = new BufRingNetwork_IRI[B * Config.bufrings_n];

            // for each copy of the network...
            for (int copy = 0; copy < Config.bufrings_n; copy++) {

                // for each local ring...
                for (int ring = 0; ring < B; ring++) {

                    // create global ring interface
                    _iris[copy*B + ring] = new BufRingNetwork_IRI(ring);

                    // create local NICs (ring stops)
                    for (int local = 0; local < B; local++)
                        _nics[copy*Config.N + ring*B + local] = new BufRingNetwork_NIC(ring, local);
                    // connect with links
                    for (int local = 1; local < B; local++)
                    {
                        Link l = new Link(Config.bufrings_locallat - 1);
                        links.Add(l);
                        _nics[copy*Config.N + ring*B + local - 1].setOutput(l,
                                _nics[copy*Config.N + ring*B + local]);
                        _nics[copy*Config.N + ring*B + local].setInput(l);
                    }
                    Link iriIn = new Link(Config.bufrings_locallat - 1), iriOut = new Link(Config.bufrings_locallat - 1);
                    links.Add(iriIn);
                    links.Add(iriOut);
                    _nics[copy*Config.N + ring*B + B-1].setOutput(iriIn,
                            _iris[copy*B + ring]);
                    _nics[copy*Config.N + ring*B + 0].setInput(iriOut);

                    _iris[copy*B + ring].setLocalInput(iriIn);
                    _iris[copy*B + ring].setLocalOutput(iriOut,
                            _nics[copy*Config.N + ring*B + 0]);

                }

                // connect IRIs with links to make up global ring
                for (int ring = 0; ring < B; ring++) {
                    Link globalLink = new Link(Config.bufrings_globallat - 1);
                    links.Add(globalLink);
                    _iris[copy*B + ring].setGlobalOutput(globalLink,
                            _iris[copy*B + (ring+1)%B]);
                    _iris[copy*B + (ring+1)%B].setGlobalInput(globalLink);
                }

                // add the corresponding NIC to each node/router
                for (int id = 0; id < Config.N; id++) {
                    int ring, local;
                    BufRingNetwork_NIC.Map(id, out ring, out local);

                    _routers[id].addNIC(_nics[copy * Config.N + ring*B + local]);
                    _nics[copy * Config.N + ring*B + local].setRouter(_routers[id]);
                }
            }

        }

       	
        public override void doStep()
        {
            bool somethingMoved = false;

            doStats();

            for (int n = 0; n < Config.N; n++)
                nodes[n].doStep();
            // step the network sim: first, routers

            foreach (BufRingNetwork_NIC nic in _nics)
                if (nic.doStep())
                    somethingMoved = true;
            foreach (BufRingNetwork_IRI iri in _iris)
                if (iri.doStep())
                    somethingMoved = true;

            bool stalled = false;
            foreach (BufRingNetwork_NIC nic in _nics)
                if (nic.Inject != null)
                    stalled = true;

            // now, step each link
            foreach (Link l in links)
                l.doStep();

            if (stalled && !somethingMoved)
                nuke();
        }

        void nuke()
        {
            Console.WriteLine("NUKE! Cycle {0}.", Simulator.CurrentRound);

            Simulator.stats.bufrings_nuke.Add();

            // first, collect all flits from the network and reset credits, etc
            Queue<Flit> flits = new Queue<Flit>();
            foreach (BufRingNetwork_NIC nic in _nics)
                nic.nuke(flits);
            foreach (BufRingNetwork_IRI iri in _iris)
                iri.nuke(flits);

            foreach (Link l in links)
                if (l.Out != null) {
                    flits.Enqueue(l.Out);
                    l.Out = null;
                }

            // now deliver all collected flits
            while (flits.Count > 0) {
                Flit f = flits.Dequeue();
                _routers[f.packet.dest.ID].acceptFlit(f);
            }
        }

        public override void close()
        {
        }
    }
}
