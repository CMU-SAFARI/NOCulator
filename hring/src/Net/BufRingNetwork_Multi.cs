using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ICSimulator
{
    class MyTracer
    {
        public static ulong id = 0, id2 = 0;

        public static void trace(Flit f, string loc, BufRingMultiNetwork_Coord c)
        {
            //return;

            if (f.packet.ID >= id && f.packet.ID < id2 && f.flitNr == 0) {
                BufRingMultiNetwork_Coord srcC = new BufRingMultiNetwork_Coord(f.packet.src.ID, Config.bufrings_levels - 1);
                BufRingMultiNetwork_Coord destC = new BufRingMultiNetwork_Coord(f.packet.dest.ID,
                        Config.bufrings_levels - 1);

                Console.WriteLine("cycle {0} flit {3}.0 (src {4} dest {5} ID {6}) at coord {1} loc {2}",
                        Simulator.CurrentRound, c, loc, f.packet.ID,
                        srcC, destC, f.packet.dest.ID);
            }
        }
    }

    public interface IBufRingMultiBackpressure
    {
        bool getCredit(Flit f, int bubble);
    }

    public struct BufRingMultiNetwork_Coord
    {
        // level corresponds to the index of the last coord dimension that is
        // valid, ie, prefix length
        public int level;   
        public int[] coord;
        public int id;

        public override string ToString()
        {
            string s = "coord (";
            for (int i = 0; i < coord.Length; i++) {
                if (i > 0) s += ",";
                s += coord[i].ToString();
            }
            s += String.Format(")/{0}", level);
            return s;
        }

        public BufRingMultiNetwork_Coord(int _id, int _level)
        {
            id = _id;
            level = _level;
            coord = new int[Config.bufrings_levels];

            for (int i = level; i >= 0; i--) {
                coord[i] = id % Config.bufrings_branching;
                id /= Config.bufrings_branching;
            }
        }

        public BufRingMultiNetwork_Coord(BufRingMultiNetwork_Coord other)
        {
            id = other.id;
            level = other.level;
            coord = new int[Config.bufrings_levels];

            for (int i = 0; i < Config.bufrings_levels; i++)
                coord[i] = other.coord[i];
        }

        // if this is a coordinate at a non-leaf node (IRI), should we
        // route the given flit up the hierarchy? Yes if its
        // prefix does not match our prefix.
        public bool routeG(BufRingMultiNetwork_Coord flitCoord)
        {
            //Console.WriteLine("routeG: my coord {0}, flitCoord {1}", this, flitCoord);
            for (int i = 0; i <= level; i++)
                if (flitCoord.coord[i] != coord[i])
                    return true;
            return false;

        }

        // if this is a coordinate at a non-leaf node (IRI), should we
        // route the given flit down the hierarchy? Yes if its
        // prefix does match our prefix.
        public bool routeL(BufRingMultiNetwork_Coord flitCoord)
        {
            for (int i = 0; i <= level; i++)
                if (flitCoord.coord[i] != coord[i])
                    return false;
            return true;
        }
    }

    public class BufRingMultiNetwork_Router : Router
    {
        BufRingMultiNetwork_NIC[] _nics;
        int _nic_count;
        ulong _lastInject = 0;

        public BufRingMultiNetwork_Router(Coord c) : base(c)
        {
            _nics = new BufRingMultiNetwork_NIC[Config.bufrings_n];
            _nic_count = 0;
        }

        protected override void _doStep()
        {
            // nothing
        }

        public override bool canInjectFlit(Flit f)
        {
            if (_lastInject == Simulator.CurrentRound)
                return false;

            for (int i = 0; i < _nic_count; i++)
                if (_nics[i].Inject == null)
                    return true;
            return false;
        }

        public override void InjectFlit(Flit f)
        {
            int b = Simulator.rand.Next(Config.bufrings_n);
            for (int i = 0; i < _nic_count; i++) {
                int idx = (b + i) % Config.bufrings_n;
                if (_nics[idx].Inject == null) {
                    _nics[idx].Inject = f;
                    statsInjectFlit(f);
                    _lastInject = Simulator.CurrentRound;
                    return;
                }
            }
            throw new Exception("Could not inject flit -- no free slots!");
        }

        public void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }

        public void addNIC(BufRingMultiNetwork_NIC nic)
        {
            _nics[_nic_count++] = nic;
            nic.setRouter(this);
        }
    }

    public class BufRingMultiNetwork_NIC : IBufRingMultiBackpressure
    {
        BufRingMultiNetwork_Router _router;
        BufRingMultiNetwork_Coord _coord;
        IBufRingMultiBackpressure _downstream;

        Flit _inject;
        Link _in, _out;
        Queue<Flit> _buf;
        int _credits;

        public BufRingMultiNetwork_Coord Coord { get { return _coord; } }

        public Flit Inject {
            get {
                return _inject;
            }
            set {
                _inject = value;
                _inject.bufrings_coord = new BufRingMultiNetwork_Coord(value.packet.dest.ID, Config.bufrings_levels - 1);
            }
        }

        public bool getCredit(Flit f, int bubble)
        {
            if (Config.bufrings_inf_credit) return true;

            if (_credits > bubble) {
                _credits--;
                return true;
            }
            else
                return false;
        }

        public BufRingMultiNetwork_NIC(BufRingMultiNetwork_Coord coord)
        {
            _coord = coord;
            _buf = new Queue<Flit>();
            _credits = Config.bufrings_localbuf;

            _inject = null;
        }

        public void setRouter(BufRingMultiNetwork_Router router)
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

                MyTracer.trace(f, "NIC input", _coord);

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
                if (_downstream.getCredit(f, 0)) {
                    _buf.Dequeue();
                    _credits++;
                    _out.In = f;
                    Simulator.stats.bufrings_nic_dequeue.Add();
                    somethingMoved = true;
                }
            }

            // handle injections
            if (_out.In == null && _inject != null) {
                if (_downstream.getCredit(_inject, 2)) {
                    _out.In = _inject;
                    _inject = null;
                    Simulator.stats.bufrings_nic_inject.Add();
                    somethingMoved = true;
                }
            }
            if (_inject != null)
                Simulator.stats.bufrings_nic_starve.Add();

            if (_out.In != null)
                Simulator.stats.bufrings_link_traverse[Config.bufrings_levels - 1].Add();

            Simulator.stats.bufrings_nic_occupancy.Add(_buf.Count);

            return somethingMoved;
        }

        public void setInput(Link l)
        {
            _in = l;
        }

        public void setOutput(Link l, IBufRingMultiBackpressure downstream)
        {
            _out = l;
            _downstream = downstream;
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

        public void dumpNet()
        {
            System.Console.Write("dump NIC {0}: buf ( ", _coord);
            foreach (Flit flit in _buf)
                System.Console.Write("{0}.{1} -> {2} ", flit.packet.ID, flit.flitNr, new BufRingMultiNetwork_Coord(flit.packet.dest.ID, 2).ToString());
            System.Console.WriteLine(") credits {0}", _credits);
        }
    }

    public class BufRingMultiNetwork_IRI
    {
        Link _gin, _gout, _lin, _lout;
        Queue<Flit> _bufGL, _bufLG, _bufL, _bufG;
        int _creditGL, _creditLG, _creditL, _creditG;

        IBufRingMultiBackpressure _downstreamL, _downstreamG;

        BufRingMultiNetwork_Coord _coord;

        public BufRingMultiNetwork_Coord Coord { get { return _coord; } }

        BufRingMultiNetwork_IRI_LocalPort _localBack;
        BufRingMultiNetwork_IRI_GlobalPort _globalBack;

        public IBufRingMultiBackpressure LocalPort { get { return _localBack; } }
        public IBufRingMultiBackpressure GlobalPort { get { return _globalBack; } }

        public class BufRingMultiNetwork_IRI_LocalPort : IBufRingMultiBackpressure
        {
            public BufRingMultiNetwork_IRI _iri;

            public BufRingMultiNetwork_IRI_LocalPort(BufRingMultiNetwork_IRI iri)
            {
                _iri = iri;
            }

            public bool getCredit(Flit f, int bubble)
            {
                if (Config.bufrings_inf_credit) return true;

                return _iri.getCredit_local(f, bubble);
            }
        }

        public class BufRingMultiNetwork_IRI_GlobalPort : IBufRingMultiBackpressure
        {
            public BufRingMultiNetwork_IRI _iri;

            public BufRingMultiNetwork_IRI_GlobalPort(BufRingMultiNetwork_IRI iri)
            {
                _iri = iri;
            }

            public bool getCredit(Flit f, int bubble)
            {
                if (Config.bufrings_inf_credit) return true;

                return _iri.getCredit_global(f, bubble);
            }
        }

        // credit request from our global-ring interface
        protected bool getCredit_global(Flit f, int bubble)
        {
            MyTracer.trace(f, "getCredit global", _coord);

            if (!_coord.routeL(f.bufrings_coord)) {
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

        // credit request from our local-ring interface
        protected bool getCredit_local(Flit f, int bubble)
        {
            MyTracer.trace(f, "getCredit local", _coord);

            if (_coord.routeG(f.bufrings_coord)) {
                MyTracer.trace(f, "getCredit local LG", _coord);
                //if (f.packet.ID == MyTracer.id) Console.WriteLine("credits {0} bubble {1}", _creditLG, bubble);
                if (_creditLG > bubble) {
                    _creditLG--;
                    return true;
                }
                else
                    return false;
            }
            else {
                MyTracer.trace(f, "getCredit local L", _coord);
                if (_creditL > bubble) {
                    _creditL--;
                    return true;
                }
                else
                    return false;
            }
        }

        public BufRingMultiNetwork_IRI(BufRingMultiNetwork_Coord c)
        {
            _coord = c;

            //Console.WriteLine("new IRI: coord {0}", c);

            _bufGL = new Queue<Flit>();
            _bufLG = new Queue<Flit>();
            _bufL = new Queue<Flit>();
            _bufG = new Queue<Flit>();

            _creditGL = Config.bufrings_G2L;
            _creditLG = Config.bufrings_L2G;
            _creditL = Config.bufrings_localbuf;
            _creditG = Config.bufrings_globalbuf;

            _localBack = new BufRingMultiNetwork_IRI_LocalPort(this);
            _globalBack = new BufRingMultiNetwork_IRI_GlobalPort(this);
        }

        public void setGlobalInput(Link l) { _gin = l; }
        public void setGlobalOutput(Link l, IBufRingMultiBackpressure b) { _gout = l; _downstreamG = b; }
        public void setLocalInput(Link l) { _lin = l; }
        public void setLocalOutput(Link l, IBufRingMultiBackpressure b) { _lout = l; _downstreamL = b; }

        public bool doStep()
        {
            bool somethingMoved = false;

            // handle inputs

            // global input
            if (_gin.Out != null) {
                Flit f = _gin.Out;
                _gin.Out = null;

                MyTracer.trace(f, "IRI global input", _coord);

                somethingMoved = true;

                if (_coord.routeL(f.bufrings_coord)) {
                    _bufGL.Enqueue(f);
                    MyTracer.trace(f,
                            String.Format("IRI global->local transfer, queue length {0}",
                                _bufLG.Count), _coord);
                    Simulator.stats.bufrings_iri_enqueue_gl[_coord.level].Add();
                }
                else {
                    _bufG.Enqueue(f);
                    Simulator.stats.bufrings_iri_enqueue_g[_coord.level].Add();
                }

            }

            // local input
            if (_lin.Out != null) {
                Flit f = _lin.Out;
                _lin.Out = null;

                MyTracer.trace(f, "IRI local input", _coord);

                somethingMoved = true;

                if (_coord.routeG(f.bufrings_coord)) {
                    _bufLG.Enqueue(f);
                    MyTracer.trace(f,
                            String.Format("IRI local->global transfer, queue length {0}",
                                _bufLG.Count), _coord);
                    Simulator.stats.bufrings_iri_enqueue_lg[_coord.level].Add();
                }
                else {
                    _bufL.Enqueue(f);
                    Simulator.stats.bufrings_iri_enqueue_l[_coord.level].Add();
                }
            }

            // handle outputs
            
            // global output (on-ring traffic)
            if (_gout.In == null && _bufG.Count > 0) {
                Flit f = _bufG.Peek();
                if (_downstreamG.getCredit(f, 0)) {
                    _bufG.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_g[_coord.level].Add();
                    _creditG++;
                    _gout.In = f;
                    somethingMoved = true;
                }
            }
            // global output (transfer traffic)
            if (_gout.In == null && _bufLG.Count > 0) {
                Flit f = _bufLG.Peek();
                if (_downstreamG.getCredit(f, (_coord.coord.Length - _coord.level))) { // bubble flow control: black magic
                    _bufLG.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_lg[_coord.level].Add();
                    _creditLG++;
                    _gout.In = f;
                    somethingMoved = true;
                }
            }

            // local output (transfer traffic)
            if (_lout.In == null && _bufGL.Count > 0) {
                Flit f = _bufGL.Peek();
                if (_downstreamL.getCredit(f, 0)) {
                    _bufGL.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_gl[_coord.level].Add();
                    _creditGL++;
                    _lout.In = f;
                    somethingMoved = true;
                }
                else {
                    BufRingMultiNetwork_IRI_GlobalPort i = _downstreamL as BufRingMultiNetwork_IRI_GlobalPort;
#if DEBUG
                    Console.WriteLine("GL block at IRI {0} flit {1}.{2} cycle {3} downstream is IRI {4}",
                            _coord, f.packet.ID, f.flitNr, Simulator.CurrentRound,
                            (i != null) ? i._iri._coord.ToString() : "(nic)");
#endif
                }
            }

            // local output (on-ring traffic)
            if (_lout.In == null && _bufL.Count > 0) {
                Flit f = _bufL.Peek();
                if (_downstreamL.getCredit(f, 0)) {
                    _bufL.Dequeue();
                    Simulator.stats.bufrings_iri_dequeue_l[_coord.level].Add();
                    _creditL++;
                    _lout.In = f;
                    somethingMoved = true;
                }
            }

            if (_gout.In != null)
                Simulator.stats.bufrings_link_traverse[_coord.level].Add();
            if (_lout.In != null)
                Simulator.stats.bufrings_link_traverse[_coord.level + 1].Add();

            Simulator.stats.bufrings_iri_occupancy_g[_coord.level].Add(_bufG.Count);
            Simulator.stats.bufrings_iri_occupancy_l[_coord.level].Add(_bufL.Count);
            Simulator.stats.bufrings_iri_occupancy_gl[_coord.level].Add(_bufGL.Count);
            Simulator.stats.bufrings_iri_occupancy_lg[_coord.level].Add(_bufLG.Count);

            Simulator.stats.bufrings_ring_util[_coord.level].Add(_gout.In != null ? 1 : 0);

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

        public void dumpNet()
        {
            System.Console.Write("dump IRI {0}: local buf ( ", _coord);
            foreach (Flit flit in _bufL)
                System.Console.Write("{0}.{1} -> {2} ", flit.packet.ID, flit.flitNr, new BufRingMultiNetwork_Coord(flit.packet.dest.ID, 2).ToString());
            System.Console.Write(") cred {0} global buf ( ", _creditL);
            foreach (Flit flit in _bufG)
                System.Console.Write("{0}.{1} -> {2} ", flit.packet.ID, flit.flitNr, new BufRingMultiNetwork_Coord(flit.packet.dest.ID, 2).ToString());
            System.Console.Write(") cred {0} local->global buf ( ", _creditG);
            foreach (Flit flit in _bufLG)
                System.Console.Write("{0}.{1} -> {2} ", flit.packet.ID, flit.flitNr, new BufRingMultiNetwork_Coord(flit.packet.dest.ID, 2).ToString());
            System.Console.Write(") cred {0} global->local buf ( ", _creditLG);
            foreach (Flit flit in _bufGL)
                System.Console.Write("{0}.{1} -> {2} ", flit.packet.ID, flit.flitNr, new BufRingMultiNetwork_Coord(flit.packet.dest.ID, 2).ToString());
            System.Console.WriteLine(") cred {0}", _creditGL);
        }
    }

    public class BufRingMultiNetwork : Network
    {
        List<BufRingMultiNetwork_NIC> _nics;
        List<BufRingMultiNetwork_IRI> _iris;
        BufRingMultiNetwork_Router[] _routers;

        public BufRingMultiNetwork(int dimX, int dimY) : base(dimX, dimY)
        {
            X = dimX;
            Y = dimY;
        }

        // set up a ring with local nodes at level 'level'
        void setup_ring(BufRingMultiNetwork_Coord coord, int level, out BufRingMultiNetwork_IRI iri, out int count)
        {
            count = 0;

            //Console.WriteLine("setup_ring: coord {0}, level {1}", coord, level);

            if (level == (Config.bufrings_levels - 1)) {
                BufRingMultiNetwork_NIC first = null, last = null;
                for (int n = 0; n < Config.bufrings_branching; n++) {
                    int id = coord.id + n;
                    //Console.WriteLine("setup_ring: coord {0}, level {1}: node {2}, global id {3}",
                    //        coord, level, n, id);
                    BufRingMultiNetwork_Coord c = new BufRingMultiNetwork_Coord(id, level);
                    BufRingMultiNetwork_NIC nic = new BufRingMultiNetwork_NIC(c);
                    _routers[id].addNIC(nic);
                    _nics.Add(nic);

                    if (last != null) {
                        Link l = new Link(Config.bufrings_locallat);
                        links.Add(l);
                        nic.setInput(l);
                        last.setOutput(l, nic);
                        //Console.WriteLine("link nic coord {0} -> nic coord {1}",
                        //        last.Coord, nic.Coord);
                    }
                    else {
                        first = nic;
                    }
                    last = nic;
                }

                BufRingMultiNetwork_Coord iricoord = new BufRingMultiNetwork_Coord(coord);
                iricoord.level--;

                iri = new BufRingMultiNetwork_IRI(iricoord);
                _iris.Add(iri);
                Link iriIn = new Link(Config.bufrings_locallat - 1), iriOut = new Link(Config.bufrings_locallat - 1);
                links.Add(iriIn);
                links.Add(iriOut);

                last.setOutput(iriIn, iri.LocalPort);
                iri.setLocalInput(iriIn);
                iri.setLocalOutput(iriOut, first);
                first.setInput(iriOut);

                //Console.WriteLine("link nic coord {0} -> IRI coord {1} local",
                //        last.Coord, iri.Coord);
                //Console.WriteLine("link IRI coord {0} local -> nic coord {1}",
                //        iri.Coord, first.Coord);

                count = Config.bufrings_branching;
            }
            else {
                BufRingMultiNetwork_IRI first = null, last = null;
                int id = coord.id;
                for (int n = 0; n < Config.bufrings_branching; n++) {
                    BufRingMultiNetwork_Coord c = new BufRingMultiNetwork_Coord(coord);
                    BufRingMultiNetwork_IRI I;
                    int subcount;

                    c.level++;
                    c.coord[c.level - 1] = n;
                    c.id = id;

                    setup_ring(c, level + 1, out I, out subcount);
                    id += subcount;
                    count += subcount;

                    if (last != null) {
                        Link l = new Link(Config.bufrings_globallat - 1);
                        links.Add(l);
                        I.setGlobalInput(l);
                        last.setGlobalOutput(l, I.GlobalPort);
                        //Console.WriteLine("link IRI coord {0} global -> IRI coord {1} global",
                        //        last.Coord, I.Coord);
                    }
                    else {
                        first = I;
                    }
                    last = I;
                }

                if (level > 0) {
                    BufRingMultiNetwork_Coord iricoord = new BufRingMultiNetwork_Coord(coord);
                    iricoord.level--;
                    iri = new BufRingMultiNetwork_IRI(iricoord);
                    _iris.Add(iri);
                    Link iriIn = new Link(Config.bufrings_globallat - 1), iriOut = new Link(Config.bufrings_globallat - 1);
                    links.Add(iriIn);
                    links.Add(iriOut);

                    last.setGlobalOutput(iriIn, iri.LocalPort);
                    iri.setLocalInput(iriIn);
                    iri.setLocalOutput(iriOut, first.GlobalPort);
                    first.setGlobalInput(iriOut);

                    //Console.WriteLine("link IRI coord {0} global -> IRI coord {1} local",
                    //        last.Coord, iri.Coord);
                    //Console.WriteLine("link IRI coord {0} local -> IRI coord {1} global",
                    //        iri.Coord, first.Coord);
                }
                else {
                    Link l = new Link(Config.bufrings_globallat - 1);
                    links.Add(l);

                    last.setGlobalOutput(l, first.GlobalPort);
                    first.setGlobalInput(l);
                    
                    //Console.WriteLine("link IRI coord {0} global -> IRI coord {1} global",
                    //        last.Coord, first.Coord);

                    iri = null;
                }
            }
        }

        public override void setup()
        {
            // boilerplate
            nodes = new Node[Config.N];
            cache = new CmpCache();
            ParseFinish(Config.finish);
            workload = new Workload(Config.traceFilenames);
            mapping = new NodeMapping_AllCPU_SharedCache();

            _nics = new List<BufRingMultiNetwork_NIC>();
            _iris = new List<BufRingMultiNetwork_IRI>();

            links = new List<Link>();
            _routers = new BufRingMultiNetwork_Router[Config.N];

            //Console.WriteLine("setup: N = {0}", Config.N);

            // create routers and nodes
            for (int n = 0; n < Config.N; n++)
            {
                Coord c = new Coord(n);
                nodes[n] = new Node(mapping, c);
                _routers[n] = new BufRingMultiNetwork_Router(c);
                _routers[n].setNode(nodes[n]);
                nodes[n].setRouter(_routers[n]);
            }

            // for each copy of the network...
            for (int copy = 0; copy < Config.bufrings_n; copy++) {
                BufRingMultiNetwork_Coord c = new BufRingMultiNetwork_Coord(0, 0);
                BufRingMultiNetwork_IRI iri;
                int count;
                setup_ring(c, 0, out iri, out count);
            }

        }

       	
        public override void doStep()
        {
            bool somethingMoved = false;

            doStats();

            for (int n = 0; n < Config.N; n++)
                nodes[n].doStep();
            // step the network sim: first, routers

            foreach (BufRingMultiNetwork_NIC nic in _nics)
                if (nic.doStep())
                    somethingMoved = true;
            foreach (BufRingMultiNetwork_IRI iri in _iris)
                if (iri.doStep())
                {
                    somethingMoved = true;
                }

            bool stalled = false;
            foreach (BufRingMultiNetwork_NIC nic in _nics)
                if (nic.Inject != null)
                    stalled = true;

            // now, step each link
            foreach (Link l in links)
                l.doStep();

            if (stalled && !somethingMoved) {
                nuke();
#if DEBUG
                dumpNet();
                System.Environment.Exit(0);
#endif
            }
        }

        void nuke()
        {
            //Console.WriteLine("NUKE! Cycle {0}.", Simulator.CurrentRound);

            Simulator.stats.bufrings_nuke.Add();

            // first, collect all flits from the network and reset credits, etc
            Queue<Flit> flits = new Queue<Flit>();
            foreach (BufRingMultiNetwork_NIC nic in _nics)
                nic.nuke(flits);
            foreach (BufRingMultiNetwork_IRI iri in _iris)
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

        void dumpNet()
        {
            foreach (BufRingMultiNetwork_NIC nic in _nics)
                nic.dumpNet();
            foreach (BufRingMultiNetwork_IRI iri in _iris)
                iri.dumpNet();
        }
    }
}
