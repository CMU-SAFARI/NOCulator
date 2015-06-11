using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ICSimulator
{

    class OUR : AbstractMemSched
    {
        public int miniframe_cycles_left = Config.memory.OUR_miniframe_cycles;
        public int frame_cycles_left = Config.memory.OUR_frame_cycles;
        public int threshold_cycles = Config.memory.OUR_threshold_cycles;

        public int num_groups;
        public float[] AU;
        public float[] AUMinFrame;
        public float[] groupAU;
        public float[] groupAUMinFrame;
        public float[] totAU;

        public int[] rankGroups;

        public int[] utilityMinFrame;
        public int[] utility;
        public int[] rank;

        public int[] priority;
        public int[] nCoresInGroup;

        public OUR(int total_size, Bank[] bank)
            : base(total_size, bank)
        {
            num_groups = Simulator.network.workload.GroupCount;

            priority = new int[num_groups];

            for(int g = 0; g < num_groups; g++)
            {
                priority[g] = 1;
            }

            rankGroups = new int[num_groups];

            AU = new float[Config.N];
            AUMinFrame = new float[Config.N];
            groupAU = new float[Config.N];
            groupAUMinFrame = new float[Config.N];
            totAU = new float[Config.N];

            rankGroups = new int[num_groups];

            utilityMinFrame = new int[Config.N];
            utility = new int[Config.N];
            rank = new int[Config.N];

            priority = new int[num_groups];
            nCoresInGroup = new int[num_groups];
        }

        public override void tick()
        {
            base.tick();

            //mark old requests
            for (int b = 0; b < bank_max; b++) {
                for (int j = 0; j < buf_load[b]; j++) {
                    MemoryRequest req = buf[b, j];
                    if (MemCtlr.cycle - req.timeOfArrival > (ulong)threshold_cycles) {
                        req.isMarked = true;
                    }
                }
            }

                if (miniframe_cycles_left > 0){
                    miniframe_cycles_left--;
                } 
                else {
                          miniframe_cycles_left = Config.memory.OUR_miniframe_cycles; 
            //MiniFrame
            //  1) Reset mini frame utilization to 0
                //     2) Reset priority
                //  3) Calculate MC rank
                //     4) Calculate total thread attained utilization
                    int[] beta = new int[num_groups];
                    //calculate average thread attained utilization (avgGroupAU)
                    for(int g = 0; g < num_groups; g++) {
                        groupAUMinFrame[g] = 0;
                        nCoresInGroup[g] = 0;
                        beta[g] = 0;
                    }
                    
                    //Sum up utilization from all channels per thread
                    for(int p = 0; p < Config.N; p++) {
                            AUMinFrame[p]= utilityMinFrame[p];
                    }
    
              for(int p = 0; p < Config.N; p++) {
                        groupAUMinFrame[Simulator.network.workload.getGroup(p)] += AUMinFrame[p];
                        //calculate # of cores in group
                        nCoresInGroup[getGroup(p)]++;
                    }

                    float[] avgGroupAU = new float[num_groups];
                    for(int g = 0; g < num_groups; g++) {
                        avgGroupAU[g] = groupAUMinFrame[g]/nCoresInGroup[g];
                    }
                    //adjust avg group utilization by beta factor
                    for(int p=0;p<Config.N;p++){
                        if(AUMinFrame[p]!=0){
                            beta[getGroup(p)]++;
                        }
                    }
                    for(int g=0;g<num_groups;g++){
                        beta[g]=beta[g]/nCoresInGroup[g];
                        avgGroupAU[g]= beta[g]*avgGroupAU[g];
                    }

                    //calculate rank by group
                    int current_rank = 1;
                    int idx = 0;
                    for(int g = 0; g < num_groups; g++) {
                        rankGroups[g] = -1;
                    }
                    for(int i = 0; i < num_groups; i++) {
                        float maxGroupAU = -1;
                        for(int g = 0; g < num_groups; g++) {
                            if(rankGroups[g] == -1) {
                                if(avgGroupAU[g] > maxGroupAU) {
                                    maxGroupAU = avgGroupAU[i];    
                                    idx = g;
                                }
                            }
                        }
                        rankGroups[idx] = current_rank;
                        current_rank++;
                    }
              for(int p = 0; p < Config.N; p++) {
                        rank[p] = rankGroups[getGroup(p)];
                  }

                    //calculate totAU
              for(int p = 0; p < Config.N; p++) {
                        AUMinFrame[p]=(float)Math.Ceiling(AUMinFrame[p]);
                        totAU[p] = AUMinFrame[p];
                    }

                    //check if thread overused its share
                    for (int g = 0; g < num_groups; g++) {
                        float groupShare = (Config.memory.OUR_miniframe_cycles * Config.memory.mem_max * nCoresInGroup[g])/Config.N;
                        if(groupAU[g] > groupShare) {
                            priority[g] = 0;
                        }

                    }
              for(int p = 0; p < Config.N; p++)
              {
                utilityMinFrame[p] = 0;
              }
    
            }     

                if (frame_cycles_left > 0){
                    frame_cycles_left--;
                } 
                else {
                          frame_cycles_left = Config.memory.OUR_frame_cycles;
            //Frame:
            //  1) Reset utilization to 0
            //  2) Set Priorities to 1
              //set utilitization and req per frame to 0
              for(int p = 0; p < Config.N; p++)
              {
                utility[p] = 0;
              }
                  for(int g = 0; g < num_groups; g++)
                     priority[g] = 1;
                }

        }

        /**
         * This is the main function of interest. Another memory scheduler needs to 
         * implement this function in a different way!
         */
        public override MemoryRequest get_next_req(MemCtlr mem)
        {
            MemoryRequest next_req = null;
            //search in global/local banks
            int bank_index;
                int next_req_bank = -1;
            if (Config.memory.is_shared_MC) {
                bank_index = mem.mem_id * Config.memory.bank_max_per_mem;
            }
            else {
                bank_index = 0;
            }
            //Select a memory request according to priorities:
              //Order: Ready > Timeout > priority > rowHit > rank > totAU > BankRank > time
            for (int b = bank_index; b < bank_index + mem.bank_max; b++)  // for each bank
                {
                    //Rule 1: Ready
                if (!bank[b].is_ready() || buf_load[b] == 0)
                    continue;
                for (int j = 0; j < buf_load[b]; j++) {
                        //Mark, priority, row hit, rank, totAU, bank rank, bank rank per thread, FCFS 
                        if(next_req == null) {
                            next_req = buf[b,j];
                            next_req_bank = b;
                            continue;
                        }
    
                        if(!helper.is_MARK_PRIO_FR_RANK_TOTAU_BR_BRT_FCFS(next_req, buf[b,j], 
                                  rank[next_req.memoryRequesterID], rank[buf[b,j].memoryRequesterID], 
                                  priority[getGroup(next_req.memoryRequesterID)], priority[getGroup(buf[b,j].memoryRequesterID)],
                                  bankRank[next_req.memoryRequesterID], bankRank[buf[b,j].memoryRequesterID], 
                                  bankRankPerThread[next_req.glob_b_index,next_req.memoryRequesterID],bankRankPerThread[b,buf[b,j].memoryRequesterID],
                                  totAU[next_req.memoryRequesterID],totAU[buf[b,j].memoryRequesterID])) {
                            next_req = buf[b,j];
                            next_req_bank = b;
                        }

                     }
                }
                //Note: All requests (in this simulator) are either load, store, or writeback
                if(next_req != null) {
                    bankRank[next_req.memoryRequesterID]--;
                    bankRankPerThread[next_req_bank,next_req.memoryRequesterID]--;
                    utility[next_req.memoryRequesterID]++;
                    utilityMinFrame[next_req.memoryRequesterID]++;
                }

            return next_req;

        }

        int getGroup(int cpu)
        {
            return Simulator.network.workload.getGroup(cpu);
        }
    }
}
