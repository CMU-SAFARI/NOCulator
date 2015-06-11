using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;


namespace ICSimulator
{
    public class Credit
    {
        // indicate which virtual channel in the receiver gain a new empty slot
        public int[] in_vc = { -1, -1, -1, -1 }; //at most 4 flits are directed out to 4 directions at one time, if they are all from the same neighbor
        //then we need at most 4 items to indicate the credit

        public Credit(int vc, int out_pc)
        {
            this.in_vc[out_pc] = vc;
        }

        public void addCredit(int vc, int out_pc)
        {
            in_vc[out_pc] = vc;
        }
    }

    /**
     * Deflection router that routes the flits as a single worm. 
     * Each router uses a 'parking-space buffer' to ensure that no packets are lost. 
     */
    public abstract class DORouter : Router
    {
        public Flit injectionSlot;
        //public Flit injectPort;
        public Flit[] injectionPipeline;
        public Flit[,] ejectionPipeline;

        // credit based parameters
        public int[,] creditCounter;
        public Credit[] creditOutPort;
        public Credit[] creditInPort;
        public int injectionCredit;


        // Parameters about input virtual channels
        // There are 5 physical channels, each physical channel has 4 virtual channels
        public Queue<Flit>[,] inputVC = new Queue<Flit>[5, Config.router.nrVCperPC];

        public int[,] currentOutputVC_PC = new int[5, Config.router.nrVCperPC]; // physical output channel of the current packet in the input virtual channel
        public int[,] currentOutputVC_VC = new int[5, Config.router.nrVCperPC]; // virtual output channel of the current packet in the input virtual channel
        public bool[,] isInputVCAssigned = new bool[5, Config.router.nrVCperPC]; // true if the virtual input channel is assigned to a virtual output channel.
        public Packet[,] currentPacketAddedToVC = new Packet[5, Config.router.nrVCperPC]; // the packet that is currently added to the virtual channel. If null, then the virtual channel is free. 

        public int[,] bufferLoad = new int[5, Config.router.nrVCperPC]; // how many packets are in a VC and are on their way to the VC! 
        public int[] bufferPortLoad = new int[5];

        //number of flits residing on each outgoing link
        //used to select the least loaded output channel in MIN_AD routers
        public int[] outQueueLength = new int[4];

        // output virtual channels
        public bool[,] isOutputVCAssigned = new bool[4, Config.router.nrVCperPC]; // true if the virtual output channel is assigned a virtual input channel.
        private bool[,] outputChannelChecked = new bool[4, Config.router.nrVCperPC];

        public int nextInjectionVC = 0;

        protected ulong[] flitTraversalsPerRequest = new ulong[Config.N];

        public DORouter(Coord myCoord)
            : base(myCoord)
        {
            if (Config.router.useCreditBasedControl)
            {
                creditCounter = new int[5, Config.router.nrVCperPC];
                creditOutPort = new Credit[4];
                creditInPort = new Credit[4];
            }
            else
            {
                //TODO: Initialize credit-faking objects
            }

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < Config.router.nrVCperPC; j++)
                {
                    //if the count of this queues are not checked, 
                    //then it is like there is infinite buffer space at each virtual channel
                    //In injectFlit method, it checks if there is enough space left in the virtual channel
                    inputVC[i, j] = new Queue<Flit>();
                    currentOutputVC_PC[i, j] = -1;
                    currentOutputVC_VC[i, j] = -1;

                    //credit based control initialization
                    if (Config.router.useCreditBasedControl)
                    {
                        creditCounter[i, j] = Config.router.sizeOfVCBuffer;
                    }
                }
            }
            routerName = "DORouter";
            injectionCredit = Config.router.sizeOfVCBuffer;

            ejectionPipeline = new Flit[4, Config.router.ejectionPipeline_extraLatency + 1];

        }

        /**
         * Decides which packet should go over the physical channel. 
         * Returns false, if no packet can be scheduled. 
         * In other words, input (port + virtual channel) selection for a particular output port.
         * Input selection for a particular output invoked in route methoD
         */
        public abstract bool switchNextFlit(int out_pc, out int pc, out int vc);

        /**
         * Decides which virtual input channel should be allocated to a free virtual output channel. 
         * Returns false if no virtual input channel can be allocated. 
         * The flit in input channel pc, vc should go through the output channel out_pc, out_vc
         * Invoked in assignFlits method.
         */
        public abstract bool arbitrateOutputChannel(int out_pc, int out_vc, out int pc, out int vc);

        /** The parameter pc is the physical channel that the parameter flit is coming from
         *  Invoked inside assignFlits method
         **/
        private void assignThisFlit(int pc, Flit flit)
        {
            flit.currentX = coord.x;
            flit.currentY = coord.y;

            if (Config.router.idealNetwork)
                throw new Exception("No traffic should occur with an ideal network!");

            // DEBUG
            if (!flit.isHeadFlit && currentPacketAddedToVC[pc, flit.virtualChannel] != flit.packet)
                throw new Exception("The previous packet has not yet fully arrived in the virtual channel");
            // DEBUG
            // ENERGY STATS
            if (inputVC[pc, flit.virtualChannel].Count < Config.router.sizeOfVCBuffer)
                inputVC[pc, flit.virtualChannel].Enqueue(flit);
            else if (inputVC[pc, flit.virtualChannel].Count > Config.router.sizeOfVCBuffer)
                throw new Exception("Buffer overflow!");

            //Simulator.num_buffer_writes++;
            //Simulator.total_current_number_of_flits_in_buffers++;

            if (flit.isHeadFlit)
            {
                currentPacketAddedToVC[pc, flit.virtualChannel] = flit.packet;
            }

            // set the virtual channel idle if this was the last flit of the packet. 
            // The packet arrived fully, so the virtual channel is free now.
            if (flit.isTailFlit)
            {
                currentPacketAddedToVC[pc, flit.virtualChannel] = null;
            }

            /** Now assign the MIN_AD direction!
             * It checks the directions in a clockwise manner, starting from UP
             * Sets the packets MIN_AD_dir to the productive direction with the least number 
             * of flits in its outQueue.
             * MIN_AD direction is used in the MIN_AD classes that override DO classes.
             */

            int minQueueLength = int.MaxValue;
            int idealDir = -1;
            if (flit.isHeadFlit)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (isDirectionProductive(flit.dest, i) && outQueueLength[i] < minQueueLength)
                    {
                        minQueueLength = outQueueLength[i];
                        idealDir = i;
                    }
                }
                flit.packet.MIN_AD_dir = idealDir;
            }
        }

        /** Assign flits waiting in inSlots and the injection port to their corresponding virtual channels.
            * Invokes assignThisFlit 5 times and frees up the slot in the input pipeline.
            *    Then, for each flit assign a virtual channel using arbitrateOutputChannel method.
        * */
        public void assignFlits()
        {
            //base.assignFlits();

            // VIRTUAL CHANNEL ALLOCATION STAGE: assign incoming flits to virtual channels!
            //flits coming from the neighbors
            for (int i = 0; i < 4; i++)
            {
                if (linkIn[i] != null && linkIn[i].Out != null)
                {
                    Flit flit = linkIn[i].Out;
                    assignThisFlit(i, flit);
                    linkIn[i].Out = null;
                }
            }
            //flits coming from local processor
            //4 is the direction index for local, in fact it is the fifth
            if (injectionSlot != null)   // injection slot
            {
                Flit flit = injectionSlot;
                assignThisFlit(4, flit);
                injectionSlot = null;
            }

            // ROUTING STAGE: assign free output virtual channel to the input virtual channel. 
            // Order the output channels according to 
            // 1. free over not-free
            // 2. less buffer-load vs. more buffer-load. 
            // Then, in this order, assign the virtual input channels using arbitrateOutputChannel(). 

            // determine number of ready Packets 
            int nrOfReadyPackets = 0;

            // 4 neighbors, 4 directions to go
            for (int out_pc = 0; out_pc < 4; out_pc++)
                for (int out_vc = 0; out_vc < Config.router.nrVCperPC; out_vc++)
                    outputChannelChecked[out_pc, out_vc] = false;

            // 4 neighbors + 1 local processor attached = 5 directions to come
            for (int in_pc = 0; in_pc < 5; in_pc++)
                for (int in_vc = 0; in_vc < Config.router.nrVCperPC; in_vc++)
                {
                    if (inputVC[in_pc, in_vc].Count > 0 && !isInputVCAssigned[in_pc, in_vc])
                        nrOfReadyPackets++;
                }

            bool freeOutputChannelExists = true;
            int iterations = 0;

            int minLoad = int.MaxValue;
            int minPC = -1;
            int minVC = -1;

            while (nrOfReadyPackets > 0 && freeOutputChannelExists && iterations < 4 * Config.router.nrVCperPC)
            {
                freeOutputChannelExists = false;

                if (nrOfReadyPackets > 2)
                    minPC = -2;

                minLoad = int.MaxValue;
                minPC = -1;
                minVC = -1;

                int out_pc = Simulator.rand.Next(4);
                int out_vc = Simulator.rand.Next(Config.router.nrVCperPC);

                for (int i = 0; i < 4 && minLoad > 0; i++)
                {
                    for (int j = 0; j < Config.router.nrVCperPC && minLoad > 0; j++)
                    {
                        if (neighbor(out_pc) != null && !isOutputVCAssigned[out_pc, out_vc] && !outputChannelChecked[out_pc, out_vc])
                        {
                            int channelLoad = getChannelLoad(out_pc, out_vc);

                            // check whether an output virtual channel is free and available. 
                            if (channelLoad < minLoad)
                            {
                                minLoad = channelLoad;
                                minPC = out_pc;
                                minVC = out_vc;
                                freeOutputChannelExists = true;
                            }
                        }
                        out_pc++;
                        if (out_pc == 4)
                            out_pc = 0;

                        out_vc++;
                        if (out_vc == Config.router.nrVCperPC)
                            out_vc = 0;
                    }
                }

                if (freeOutputChannelExists)
                {
                    outputChannelChecked[minPC, minVC] = true;
                    // allocate a virtual input channel if there exists one. 
                    int pc, vc;
                    bool allocation_successful = arbitrateOutputChannel(minPC, minVC, out pc, out vc);

                    if (allocation_successful)
                    {
                        nrOfReadyPackets--;
                        isOutputVCAssigned[minPC, minVC] = true; // set this to 0 when the tail-flit was scheduled.                    
                        currentOutputVC_PC[pc, vc] = minPC;
                        currentOutputVC_VC[pc, vc] = minVC;
                        isInputVCAssigned[pc, vc] = true;  // set this to false when the tail-flit was scheduled.
                        outQueueLength[minPC]++;     //miray
                    }
                    iterations++;
                }
            }
        }

        protected virtual int getChannelLoad(int out_pc, int out_vc)
        {
            // credit based control
            if (Config.router.useCreditBasedControl)
            {
                return (Config.router.sizeOfVCBuffer - creditCounter[out_pc, out_vc]);
            }

            // ATTENTION: This is not quite accurate because some nodes may still be in the pipeline before actually being added to the buffer! 
            return ((DORouter)neighbor(out_pc)).bufferLoad[(out_pc + 2) % 4, out_vc];
        }

        /**
         * Returns true if the direction assigned to this flit is 
         * the same as XY-routing direction.
         */
        protected virtual bool isDirectionGood(int out_pc, int out_vc, int p, int v)
        {
            return dimension_order_route(inputVC[p, v].Peek()) == out_pc;
        }

        void ejectPacket(Packet p)
        {
            statsEjectPacket(p);

            m_n.receivePacket(p);
        }

        private void acceptFlit(Flit f)
        {
            statsEjectFlit(f);

            switch (Config.rxbuf_mode)
            {
                case RxBufMode.NONE:
                    f.packet.nrOfArrivedFlits++;
                    if (f.packet.nrOfArrivedFlits == f.packet.nrOfFlits)
                        ejectPacket(f.packet);
                    break;
                default:
                    throw new Exception("Unhandled rxbuf_mode");
            }
        }

        protected virtual void deliverFlits()
        {
            //TODO: use a different latency for ejection!

            //1.  Deliver packets to the processor
            for (int dir = 0; dir < 4; dir++)
            {
                if (ejectionPipeline[dir, Config.router.ejectionPipeline_extraLatency] != null)
                {
                    acceptFlit(ejectionPipeline[dir, Config.router.ejectionPipeline_extraLatency]);
                    ejectionPipeline[dir, Config.router.ejectionPipeline_extraLatency] = null;
                }
            }

            // move up packets in the ejectionPipeline. 
            for (int dir = 0; dir < 4; dir++)
            {
                for (int stage = Config.router.ejectionPipeline_extraLatency; stage > 0; stage--)
                {
                    ejectionPipeline[dir, stage] = ejectionPipeline[dir, stage - 1];
                    ejectionPipeline[dir, stage - 1] = null;
                }
            }

            // decide whether a packet has arrived. 
            for (int dir = 0; dir < 4; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null && linkIn[dir].Out.packet.dest.Equals(this.coord))
                {
                    ejectionPipeline[dir, 0] = linkIn[dir].Out;
                    linkIn[dir].Out = null;
                }
            }
        }

        protected override void _doStep()
        {
            deliverFlits();
            assignFlits();
            /* 
             outSlot.account();
             inputPort.account();   // the flow is inputPort --> pipeline --> inSlot --> assignFlit(flit) (depending on router) --> outSlot --> outputPort
             inSlot.account();
             outputPort.account();
            
             foreach (LinkQueue q in inputPipeline)
                 q.account();
             */
            // ENERGY STATISTICS
            int nrOfArbitationParticipants = 0;
            for (int pc = 0; pc < 5; pc++)
                for (int vc = 0; vc < Config.router.nrVCperPC; vc++)
                    if (inputVC[pc, vc].Count > 0)
                    {
                        nrOfArbitationParticipants++;
                        //everyone stalls, decrement stall if progressing instead
                        //Application_Processor.accountForFlitCycle(inputVC[pc, vc].Peek(), Application_Processor.PacketLocation.NetworkStall, 1);
                    }

            /*
            Simulator.num_arbiter_accesses_flit += (ulong)nrOfArbitationParticipants;
            if (nrOfArbitationParticipants > 0)
                Simulator.num_arbitration_decisions++;
            */
            // ENERGY STATISTICS

            for (int out_pc = 0; out_pc < 4; out_pc++)
            {
                Flit flitToSchedule;
                int pc, vc;

                //switchNextFlit decides which packet should go over the out_pc physical channel
                //switchNextFlit returns false if no packet can be scheduled
                bool allocation_successful = switchNextFlit(out_pc, out pc, out vc);

                //if there is a flit that can go over the physical channel out_pc
                if (allocation_successful)
                {
                    //Application_Processor.accountForFlitCycle(inputVC[pc, vc].Peek(), Application_Processor.PacketLocation.NetworkProgress, 1);//+ Config.router.ejectionPipeline_extraLatency);
                    //Application_Processor.accountForFlitCycle(inputVC[pc, vc].Peek(), Application_Processor.PacketLocation.NetworkStall, -1);
                    // DEBUG
                    if (currentOutputVC_PC[pc, vc] != out_pc)
                        throw new Exception("The current packet should not be scheduled to this output pc");
                    if (!isInputVCAssigned[pc, vc])
                        throw new Exception("The selected input channel is not even assigned");
                    if (isDownstreamVCFull(out_pc, currentOutputVC_VC[pc, vc]))
                        throw new Exception("Cannot inject because the buffer is full!");
                    // DEBUG

                    flitToSchedule = inputVC[pc, vc].Dequeue();
                    bufferLoad[pc, vc]--;
                    bufferPortLoad[pc]--;

                    //credit based control
                    if (Config.router.useCreditBasedControl)
                    {
                        if (pc == 4)
                        {
                            creditCounter[4, vc]++;
                        }
                        else
                        {
                            Credit credit_back;
                            if (creditOutPort[pc] == null)
                            {
                                credit_back = new Credit(vc, out_pc);
                                creditOutPort[pc] = credit_back;
                            }
                            else
                            {
                                creditOutPort[pc].addCredit(vc, out_pc);
                            }
                        }
                    }

                    // ENERGY STATS
                    /*
                    Simulator.num_buffer_reads++;
                    Simulator.total_current_number_of_flits_in_buffers--;
                    if (Simulator.total_current_number_of_flits_in_buffers < 0)
                        throw new Exception("!!!");
                    */

                    int out_vc = currentOutputVC_VC[pc, vc];
                    flitToSchedule.virtualChannel = out_vc;

                    if (flitToSchedule.isTailFlit)
                    {
                        // make outputVC allocatable again and also make the inputVC non-assigned. 
                        isInputVCAssigned[pc, vc] = false;
                        currentOutputVC_PC[pc, vc] = -1;
                        currentOutputVC_VC[pc, vc] = -1;
                        isOutputVCAssigned[out_pc, out_vc] = false;
                        outQueueLength[out_pc]--;
                    }

                    linkOut[out_pc].In = flitToSchedule;

                    // packets attain 'service' from routers
                    if (flitToSchedule.packet.request != null)
                        flitTraversalsPerRequest[flitToSchedule.packet.request.requesterID]++;

                    if (flitToSchedule.packet.dest.ID != neighbor(out_pc).ID)
                    {
                        ((DORouter)neighbor(out_pc)).bufferLoad[(out_pc + 2) % 4, out_vc]++;
                        ((DORouter)neighbor(out_pc)).bufferPortLoad[(out_pc + 2) % 4]++;
                    }

                    // credit based control
                    if (Config.router.useCreditBasedControl)
                    {
                        if (flitToSchedule.packet.dest.ID != neighbor(out_pc).ID)
                        {
                            creditCounter[out_pc, out_vc]--;
                        }
                    }
                }
            }
        }

        public void creditUpdate()
        {
            for (int i = 0; i < 4; i++)
            {
                if (creditInPort[i] != null)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (creditInPort[i].in_vc[j] > -1)
                        {
                            (creditCounter[i, creditInPort[i].in_vc[j]])++;

                            //debug
                            if (creditCounter[i, creditInPort[i].in_vc[j]] > Config.router.sizeOfVCBuffer)
                            {
                                Console.Out.WriteLine("Current round is " + Simulator.CurrentRound);
                                throw new Exception("Credit wrong");
                            }
                        }

                    }
                    creditInPort[i] = null;
                }
            }
        }

        //Returns true if it is not possible to send anything in the direction of out_pc, out_vc
        //returns true if it is full
        public bool isDownstreamVCFull(int out_pc, int out_vc)
        {
            // credit based control
            if (Config.router.useCreditBasedControl)
            {
                return (neighbor(out_pc) == null || creditCounter[out_pc, out_vc] == 0);
            }

            return (neighbor(out_pc) == null || ((DORouter)neighbor(out_pc)).bufferLoad[(out_pc + 2) % 4, out_vc] >= Config.router.sizeOfVCBuffer);
        }

        /**injectFlit is used to inject a flit from the local processor
         * The flit's virtualChannel is assigned and the nextInjectionVC 
         * is decided to be used for the next packet's flit if this flit is the last flit of a packet.
         * */
        public override void InjectFlit(Flit f)
        {
            if (!canInjectFlit(f))
                throw new Exception("Failed injection.");

            statsInjectFlit(f);
            injectionSlot = f;

            f.virtualChannel = nextInjectionVC;
            bufferLoad[4, nextInjectionVC]++;

            //credit based control
            if (Config.router.useCreditBasedControl)
            {
                creditCounter[4, f.virtualChannel]--;
            }

            if (f.isTailFlit)
            {
                // search virtual channel with least buffer load!
                int minBL = int.MaxValue;
                int minVC = -1;
                int vc = nextInjectionVC + 1;
                if (vc == Config.router.nrVCperPC)
                    vc = 0;

                for (int i = 0; i < Config.router.nrVCperPC; i++)
                {
                    if (bufferLoad[4, vc] < minBL)
                    {
                        // least buffer load or oldest head-flit. 
                        minBL = bufferLoad[4, vc];
                        minVC = vc;
                    }

                    vc++;
                    if (vc == Config.router.nrVCperPC)
                        vc = 0;
                }
                nextInjectionVC = minVC;
            }
        }


        /** return true if there is the least loaded virtual channel of 
                 * the physical channel from the local processor can accept new flits*/
        public override bool canInjectFlit(Flit f)
        {
            if (Config.router.useCreditBasedControl)
            {
                return (creditCounter[4, nextInjectionVC] > 0);
            }

            // In the case with limited buffer size, this needs to be adjusted. 
            return (bufferLoad[4, nextInjectionVC] < Config.router.sizeOfVCBuffer);
        }
    }

    public abstract class PrioritizedDORouter : DORouter
    {
        protected PrioritizedDORouter(Coord myCoord)
            : base(myCoord) { }

        public override bool switchNextFlit(int out_pc, out int pc, out int vc)
        {
            pc = -1; vc = -1;
            Flit bestFlit = null;
            for (int p = 0; p < 5; p++)
            {
                for (int v = 0; v < Config.router.nrVCperPC; v++)
                {
                    if (isInputVCAssigned[p, v] && inputVC[p, v].Count > 0 && currentOutputVC_PC[p, v] == out_pc)
                    {
                        Flit firstFlit = inputVC[p, v].Peek();
                        if (isHigherPriority(firstFlit, bestFlit) && !isDownstreamVCFull(out_pc, currentOutputVC_VC[p, v]))
                        {
                            bestFlit = firstFlit;
                            pc = p;
                            vc = v;
                        }
                    }
                }
            }
            return (pc != -1 && vc != -1);
        }
        public override bool arbitrateOutputChannel(int out_pc, int out_vc, out int pc, out int vc)
        {
            pc = -1; vc = -1;
            Flit bestFlit = null;
            for (int p = 0; p < 5; p++)
            {
                for (int v = 0; v < Config.router.nrVCperPC; v++)
                {
                    if (inputVC[p, v].Count > 0 &&
                        !isInputVCAssigned[p, v] &&
                        isDirectionGood(out_pc, out_vc, p, v))
                    {
                        // DEBUG
                        if (!inputVC[p, v].Peek().isHeadFlit)
                            throw new Exception("If the input VC is unassigned, the first flit in the queue must be a header flit.");
                        // DEBUG

                        Flit firstFlit = inputVC[p, v].Peek();
                        if (isHigherPriority(firstFlit, bestFlit))
                        {
                            bestFlit = firstFlit;
                            pc = p;
                            vc = v;
                        }
                    }
                }
            }
            return (pc != -1 && vc != -1);
        }

        /// <summary> Returns true if the first argument is higher priority </summary>
        public bool isHigherPriority(Flit a, Flit b)
        {
            if (a == null)
                return false;
            if (b == null)
                return true;
            return _isHigherPriority(a, b);
        }
        protected abstract bool _isHigherPriority(Flit a, Flit b);
    }

    public class OldestFirstDORouter : PrioritizedDORouter
    {
        public OldestFirstDORouter(Coord myCoord)
            : base(myCoord) { }
        protected override bool _isHigherPriority(Flit a, Flit b)
        {
            return a.packet.creationTime < b.packet.creationTime;
        }
    }// end of OldestFirstDORouter


    public class RoundRobinDORouter : DORouter
    {
        // round robin counters!
        private int nextPCForSwitching = 0;
        private int nextVCForSwitching = 0;
        private int nextPCForArbitration = 0;
        private int nextVCForArbitration = 0;

        public RoundRobinDORouter(Coord myCoord)
            : base(myCoord) { }

        /**
        * Decides which packet should go over the physical channel. Returns false, if no packet can be scheduled. 
        */
        public override bool switchNextFlit(int out_pc, out int pc, out int vc)
        {
            pc = -1; vc = -1;
            bool channelFound = false;
            int nrOfChannelsVisited = 0;
            while (!channelFound && nrOfChannelsVisited < 5 * Config.router.nrVCperPC)
            {
                if (isInputVCAssigned[nextPCForSwitching, nextVCForSwitching] && inputVC[nextPCForSwitching, nextVCForSwitching].Count > 0 && currentOutputVC_PC[nextPCForSwitching, nextVCForSwitching] == out_pc)
                {
                    if (!isDownstreamVCFull(out_pc, currentOutputVC_VC[nextPCForSwitching, nextVCForSwitching]))
                    {
                        pc = nextPCForSwitching;
                        vc = nextVCForSwitching;
                        channelFound = true;
                    }
                }
                nextVCForSwitching++;
                if (nextVCForSwitching == Config.router.nrVCperPC)
                {
                    nextVCForSwitching = 0;
                    nextPCForSwitching++;
                    if (nextPCForSwitching == 5)
                        nextPCForSwitching = 0;
                }
                nrOfChannelsVisited++;
            }
            return (pc != -1 && vc != -1);
        }

        /**
         * Decides which virtual input channel should be allocated to a free virtual output channel. 
         * Returns false if no virtual input channel can be allocated. 
         */
        public override bool arbitrateOutputChannel(int out_pc, int out_vc, out int pc, out int vc)
        {
            pc = -1; vc = -1;
            bool channelFound = false;
            int nrOfChannelsVisited = 0;
            while (!channelFound && nrOfChannelsVisited < 5 * Config.router.nrVCperPC)
            {
                if (inputVC[nextPCForArbitration, nextVCForArbitration].Count > 0 &&
                       !isInputVCAssigned[nextPCForArbitration, nextVCForArbitration] &&
                         isDirectionGood(out_pc, out_vc, nextPCForArbitration, nextVCForArbitration))
                {
                    // DEBUG
                    if (!inputVC[nextPCForArbitration, nextVCForArbitration].Peek().isHeadFlit)
                        throw new Exception("If the input VC is unassigned, the first flit in the queue must be a header flit.");
                    // DEBUG

                    channelFound = true;
                    pc = nextPCForArbitration;
                    vc = nextVCForArbitration;
                }
                nextVCForArbitration++;
                if (nextVCForArbitration == Config.router.nrVCperPC)
                {
                    nextVCForArbitration = 0;
                    nextPCForArbitration++;
                    if (nextPCForArbitration == 5)
                        nextPCForArbitration = 0;
                }
                nrOfChannelsVisited++;
            }

            return (pc != -1 && vc != -1);
        }

    }// end of RoundRobinDORouter

    public class MIN_AD_OldestFirst_Router : OldestFirstDORouter
    {

        // channels 0...Config.router.nrVCperPC/2 are *-channels
        // channels Config.router.nrVCperPC/2+1...Config.router.nrVCperPC are non-*-channels

        public MIN_AD_OldestFirst_Router(Coord myCoord)
            : base(myCoord)
        {
            if (Config.router.nrVCperPC < 2 || Config.router.nrVCperPC % 2 != 0)
                throw new Exception("Cannot allocate an odd number of virtual channels to MIN-AD router");
            routerName = "MIN_AD_OldestFirst_Router";
        }


        /**
         * If the virtual channel is a *-channel, returns true if the direction is a dimension-order direction
         * If the out_vc is NOT a *-channel, then returns true if the direction is productive. 
         */
        protected override bool isDirectionGood(int out_pc, int out_vc, int p, int v)
        {
            if (isStarChannel(out_vc))
            {
                return isDirectionProductive(inputVC[p, v].Peek().dest, out_pc);
                //   return inputVC[p, v].Peek().packet.MIN_AD_dir == out_pc; 
            }
            else
            {
                return dimension_order_route(inputVC[p, v].Peek()) == out_pc;
            }
        }

        public bool isStarChannel(int vc)
        {
            return vc < Config.router.nrVCperPC - 1;
        }

        protected override int getChannelLoad(int out_pc, int out_vc)
        {
            if (Config.router.useCreditBasedControl)
            {
                return (Config.router.sizeOfVCBuffer - creditCounter[out_pc, out_vc]);
            }

            // ATTENTION: This is not quite accurate because some nodes may still be in the pipeline before actually being added to the buffer! 
            return ((DORouter)neighbor(out_pc)).bufferPortLoad[(out_pc + 2) % 4];
        }

    }// end of MIN_AD_OldestFirst_Router

    public class MIN_AD_RoundRobin_Router : RoundRobinDORouter
    {

        // channels 0...Config.router.nrVCperPC/2 are *-channels
        // channels Config.router.nrVCperPC/2+1...Config.router.nrVCperPC are non-*-channels

        public MIN_AD_RoundRobin_Router(Coord myCoord)
            : base(myCoord)
        {
            if (Config.router.nrVCperPC < 2 || Config.router.nrVCperPC % 2 != 0)
                throw new Exception("Cannot allocate an odd number of virtual channels to MIN-AD router");
            routerName = "MIN_AD_RoundRobin_Router";
        }


        /**
         * If the virtual channel is a *-channel, returns true if the direction is a dimension-order direction
         * If the out_vc is NOT a *-channel, then returns true if the direction is productive. 
         */
        protected override bool isDirectionGood(int out_pc, int out_vc, int p, int v)
        {
            if (isStarChannel(out_vc))
            {
                return inputVC[p, v].Peek().packet.MIN_AD_dir == out_pc;
            }
            else
            {
                return dimension_order_route(inputVC[p, v].Peek()) == out_pc;
            }
        }

        private bool isStarChannel(int vc)
        {
            return vc < Config.router.nrVCperPC - 1;
        }

    }// end of MIN_AD_RoundRobin_Router
}
