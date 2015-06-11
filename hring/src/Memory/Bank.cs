using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace ICSimulator
{
    /**
     * The bank
     */
    public class Bank
    {
        public MemCtlr MC; //owning MC

        //bank id
        static int bank_max = 0;    //total number of banks
        public int bank_id;         //index of this bank

        //components
        public MemoryRequest cur_req;        //current memory request
        MemSched sched;     //scheduler for this bank
                            
        //other components
        public BankStat stat;   //the statistics

        //bank state
        enum RowState { Open, Closed };     //state of current row
        RowState state;                     //state of current row
        ulong cur_row;                      //index of the current row
        bool is_cur_marked;                 //whether the current request is marked
        long wait_left;                     //time left until current request is served

        public Dictionary<ulong, ulong> lastOpen = new Dictionary<ulong, ulong>();
        public Dictionary<ulong, ulong> lastRequested = new Dictionary<ulong, ulong>();

        public ulong[] outstandingReqs_perapp;
        public ulong outstandingReqs;
        /**
         * Constructor
         */
        public Bank(MemSched sched, MemCtlr MC)
        {
            //bank id
            bank_id = bank_max;
            bank_max++;
            Console.WriteLine("bank" + '\t' + bank_id.ToString() + '\t' + MC.mem_id.ToString());

            //set scheduler
            this.sched = sched;

            //allocate stat
            stat = new BankStat(this);
            outstandingReqs_perapp = new ulong[Config.N];
            outstandingReqs = 0;

            //initialize bank state
            state = RowState.Closed;
            cur_row = ulong.MaxValue;
            is_cur_marked = false;
            wait_left = 0;

            this.MC = MC;

            lastOpen.Clear();
        }

        /**
         * Progress time and (possibly) service the current request. 
         * Decrement the time left to fully service the current request.
         * If it reaches zero, service it and notify the processor.
         */
        public void tick()
        {
            for (int i = 0; i < Config.N; i++){
                //Console.WriteLine(bank_id.ToString() + '\t' + i.ToString());
                //Simulator.stats.bank_queuedepth_persrc[bank_id, i].Add(outstandingReqs_perapp[i]);
            }
            Simulator.stats.bank_queuedepth[bank_id].Add(outstandingReqs);

            //sanity check
            Debug.Assert((wait_left >= 0) && (wait_left <= Config.memory.row_conflict_latency + Config.memory.bus_busy_time));
            Debug.Assert(!(cur_req == null && wait_left != 0));
            Debug.Assert(!(cur_req != null && wait_left == 0));

            //decrement time left to serve current request
            if (wait_left > 0)
                wait_left--;

            //can't serve current request
            if (cur_req == null || wait_left != 0)
                return;

            //we can now serve the current request

            //Console.WriteLine("Request complete, sending reply");

            //serve request by removing current request from scheduler buffer
            sched.remove_req(cur_req);
            outstandingReqs--;
            if (outstandingReqs < 0)
                throw new Exception("Bank has negative number of requests!");
            outstandingReqs_perapp[cur_req.request.requesterID]--;
            if (outstandingReqs_perapp[cur_req.request.requesterID] < 0)
                throw new Exception("App has negative number of requests!");
            cur_req.cb();

            //send back the serviced request to cache (which in turn sends it to processor)
            Request request = cur_req.request;
            if (request == null) throw new Exception("No request! don't know who to send it back to!");
            //Console.WriteLine("Returning mc_data packet to cache slice at Proc {0}, ({1},{2})", mcaddrpacket.source.ID, mcaddrpacket.source.x, mcaddrpacket.source.y);


            CPU cpu = Simulator.network.nodes[request.requesterID].cpu;
            cpu.outstandingReqsMemory--;
            if (cpu.outstandingReqsMemory == 0)
            {
                Simulator.stats.memory_episode_persrc[request.requesterID].Add(Simulator.CurrentRound - cpu.outstandingReqsMemoryCycle);
                cpu.outstandingReqsMemoryCycle = Simulator.CurrentRound;
            }

            //----- STATS START -----
            stat.dec(ref BankStat.req_cnt[request.requesterID]);
            if (cur_req.isMarked)
                stat.dec(ref BankStat.marked_req_cnt[request.requesterID]);
            else
                stat.dec(ref BankStat.unmarked_req_cnt[request.requesterID]);
            //----- STATS END ------

            //reset current req
            cur_req = null;
        }

        /**
         * Tells whether bank is ready to receive another request.
         * @return if bank is ready, true; otherwise, false
         */
        public bool is_ready()
        {
            return wait_left == 0;
        }

        /**
         * Set a memory request to the bank.
         * This can only be done if there are no requests currently being serviced.
         * Time left to service the request is set to full value.
         * @param req the memory request
         */
        public void add_req(MemoryRequest req)
        {
            //check if current request has been serviced
            Debug.Assert(cur_req == null);

            //proceed to service new request; update as the current request
            cur_req = req;
            is_cur_marked = cur_req.isMarked;

            //----- STATS START -----
            stat.inc(ref BankStat.req_cnt[cur_req.request.requesterID]);
            //Simulator.stats.bank_access_persrc[bank_id, cur_req.request.requesterID].Add();
            if (cur_req.isMarked)
                stat.inc(ref BankStat.marked_req_cnt[cur_req.request.requesterID]);
            else
                stat.inc(ref BankStat.unmarked_req_cnt[cur_req.request.requesterID]);
            //----- STATS END ------
            
            //time to serve the request; bus latency
            wait_left = Config.memory.bus_busy_time;

            //time to serve the request; row access latency
            if (state == RowState.Closed) {
                //row is closed
                wait_left += Config.memory.row_closed_latency;
                state = RowState.Open;
            }
            else {
                //row is open
                if (cur_req.r_index == cur_row && !Config.memory.row_same_latency) {
                    //hit
                    stat.inc(ref stat.row_hit);
                    stat.inc(ref stat.row_hit_per_proc[cur_req.request.requesterID]);
                    //Simulator.stats.bank_rowhits_persrc[bank_id, cur_req.request.requesterID].Add();

                    wait_left += Config.memory.row_hit_latency;
                }
                else {
                    //conflict
                    stat.inc(ref stat.row_miss);
                    stat.inc(ref stat.row_miss_per_proc[cur_req.request.requesterID]);

                    wait_left += Config.memory.row_conflict_latency;

                    //Close row, mark last cycle row to be closed was open
                    lastOpen[cur_row] = Simulator.CurrentRound;
                }
            }

            //set as current row
            cur_row = cur_req.r_index;

        }

        /**
         * Check and maintain marking status of the current memory request.
         * (Called only by BatchMemSched and OtherMemSched.)
         */
        public void update_marking()
        {
            if (cur_req == null)
                return;

            if (!is_cur_marked && cur_req.isMarked) {
                is_cur_marked = true;
                stat.inc(ref BankStat.marked_req_cnt[cur_req.request.requesterID]);
                stat.dec(ref BankStat.unmarked_req_cnt[cur_req.request.requesterID]);
            }
        }

        /**
         * Get current row.
         * Does not matter whenter open or closed.
         */
        public ulong get_cur_row()
        {
            return cur_row;
        }

        /**
         * Get current memory request being served.
         * @return current memory request
         */
        public MemoryRequest get_cur_req()
        {
            return cur_req;
        }

        public override string ToString()
        {
            return "Bank " + bank_id.ToString();
        }
    }//class
}//namespace
