
//#define DEBUG
using System;
using System.Collections.Generic;
namespace ICSimulator
{
    public class SortNode
    {
        public delegate int Steer(Flit f);
        public delegate int  Rank(Flit f1, Flit f2);

        Steer m_s;
        Rank  m_r;

        public Flit in_0, in_1, out_0, out_1;

        public SortNode(Steer s, Rank r)
        {
            m_s = s;
            m_r = r;
        }
        
        /** Execute arbiter block **/
        public void doStep()
        {
            Flit winner, loser;
            
            /* If in_0 is ranked earlier than in_1, in_0 wins */
            if (m_r(in_0, in_1) < 0)
            {
                winner = in_0;
                loser  = in_1;
            }
            else
            {
                loser  = in_0;
                winner = in_1;
            }
            
            /* Checks if the winner and the lower are actually correct */
            if (winner != null) winner.sortnet_winner = true;
            if (loser  != null)  loser.sortnet_winner = false;
            
            /* Direction that winner has been steered in */
            int dir = m_s(winner);

            /* If the winner is not changing, don't swap; otherwise swap */
            if (dir == 0)
            {
                out_0 = winner;
                out_1 = loser;
            }
            else
            {
                out_0 = loser;
                out_1 = winner;
            }
        }
    }

    public abstract class SortNet
    {
        public abstract void route(Flit[] input, out bool injected);
    }

    public class SortNet_CALF : SortNet
    {
        SortNode[]  nodes;
        ResubBuffer[] rBuf;
        
		ulong       resubmitSkipCount;
		ulong       resubmitCantInjectCount;
		ulong		resubmitBlockInputCount;
        int         currentInput;
		int 		startInput;

        Coord coord;
            
        public SortNet_CALF(SortNode.Rank r, Coord coord)
        {
            nodes = new SortNode[4];
            this.coord = coord;

            if (Config.inputBuffer)
           	{
            	rBuf = new ResubBuffer[4];
                rBuf[0] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[1] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[2] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[3] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                startInput = 0;
           	}
            else if (Config.resubmitBuffer) 
			{
				if (Config.resubmitLineBuffers)
				{   
                    rBuf = new ResubBuffer[4];
                    rBuf[0] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                    rBuf[1] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                    rBuf[2] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                    rBuf[3] = new ResubBuffer(Config.sizeOfRSBuffer/4);
				}	
				else
				{
                    rBuf = new ResubBuffer[1];
                	rBuf[0]  = new ResubBuffer(Config.sizeOfRSBuffer + Config.pipelineCount);
                }

				currentInput = 0;
			    resubmitSkipCount = 0;
				resubmitCantInjectCount = 0;
				resubmitBlockInputCount = Config.redirectCount;
			}
            else if (Config.outputBuffers)
            {
                rBuf = new ResubBuffer[4];
                rBuf[0] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[1] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[2] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[3] = new ResubBuffer(Config.sizeOfRSBuffer/4);
            }
            /*else if (Config.inputResubmitBuffers)
            {
                rBuf = new ResubBuffer[4];
                rBuf[0] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[1] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[2] = new ResubBuffer(Config.sizeOfRSBuffer/4);
                rBuf[3] = new ResubBuffer(Config.sizeOfRSBuffer/4);
            }*/

            SortNode.Steer stage1_steer = delegate(Flit f)
            {
                if (f == null)
                    return 0;
                
                /* If the preferred direction for the simulator is to go vertically redirect to that switch */
                return (f.prefDir == Simulator.DIR_UP || f.prefDir == Simulator.DIR_DOWN) ?
                    0 : // NS switch
                    1;  // EW switch
            };
           
            // node 0: {N,E} -> {NS, EW}
            nodes[0] = new SortNode(stage1_steer, r);
            // node 1: {S,W} -> {NS, EW}
            nodes[1] = new SortNode(stage1_steer, r);

            // node 2: {in_top,in_bottom} -> {N,S}
            nodes[2] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_UP) ? 0 : 1;
                    }, r);
            // node 3: {in_top,in_bottm} -> {E,W}
            nodes[3] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_RIGHT) ? 0 : 1;
                    }, r);
        }

        // takes Flit[5] as input; indices DIR_{UP,DOWN,LEFT,RIGHT} and 4 for local.
        // permutes in-place. input[4] is left null; if flit was injected, 'injected' is set to true.
        public override void route(Flit[] input, out bool injected)
        {
        	bool redirection = false;
            injected = false;
            //Flit[] extraBuf = null;
            Flit[] tmp = new Flit[4];
            
            if(Config.resubmitBuffer)
            {
                int count = rBuf[0].count();
                Simulator.stats.bufferCount.Add(count);
                Simulator.stats.bufferUtil.Add(count/rBuf[0].getMaxSize());
            }
            if (Config.inputResubmitBuffers)
            {
                for (int i = 0; i < 4; i++)
                {
                    int count = rBuf[i].count();
                    Simulator.stats.bufferCount.Add(count);
                    Simulator.stats.bufferUtil.Add(count/rBuf[i].getMaxSize());
                }
            }

			if(Config.resubmitBuffer && Config.inputBuffer  || 
			   Config.resubmitBuffer && Config.prioByDefl   ||
			   Config.prioByDefl     && Config.inputBuffer  )
			{
				throw new Exception("More than one buffer / prio scheme selected");			
			}

            if (!Config.calf_new_inj_ej) {
                int resubmitInjectCount = 0;

				/* Inject flits from the resubmitBuffer */
                if (Config.resubmitBuffer)
                    resubmitInjectCount = rebufInjection(input, ref redirection);
                //else if (Config.inputBuffer)
				//{
                //    inputBufferStage(input);
                //	bufferInjectEject(input);//extraBuf =  
                //}
                else if (Config.inputResubmitBuffers)
                    resubmitInjectCount = inputResubmitInjection(input, ref redirection);
                 
                /* Normal injection */  
				if (!(Config.resubmitBlocksInjection && resubmitInjectCount > 0)) {	
	                // injection: if free slot, insert flit
	                if (input[4] != null) 
	                {
	                    for (int i = 0; i < 4; i++) 
	                    {
	                        if (input[i] == null) 
	                        { 
	                            input[i] = input[4];
                                input[i].orig_input = -1;
                                
#if DEBUG
                Console.WriteLine("\tinjecting flit {0}.{1} at node ({2},{3}) cyc {4}", input[i].packet.ID, input[i].flitNr, input[i].currentX, input[i].currentY, Simulator.CurrentRound);
#endif
                                if (Simulator.rand.Next(0,100) < Config.initialInfectionRate)
                                	input[i].infected = true;
                                else
                                	input[i].infected = false;
                                	
	                            /* Prioritizing flits */
           						if (Simulator.rand.Next(0,100) < Config.randomFlitPrioPercent)
           							input[i].initPrio = 1;
           						else
           							input[i].initPrio = 0;
                                injected = true;
	                            break;
	                        }
	                    }
	                    input[4] = null;
	                }
				}
				
            }

            ///////////////// INPUT BUFFERS////////////////////////
            Flit[] newInput;
            
            if (Config.inputBuffer)
            {
                newInput = inputBufferStage(input);
            }
            else
                newInput = input;

            //////////////////// SILVER FLIT //////////////////////
            bool hasGolden = false; 
            int[] flitPositions = new int[4];
            int  flitCount = 0;
            for (int i = 0; i < 4; i++)
            {
                if (newInput[i] != null)
                {
                    flitPositions[flitCount] = i;
                    flitCount++;
                    newInput[i].isSilver = false;
                    if (Simulator.network.golden.isGolden(newInput[i]))
                        hasGolden = true;
                }
            }
            if (flitCount != 0)
            {
                if (Config.alwaysSilver || !hasGolden)
                {
                    switch(Config.silverMode)
                    {
                        case "random": int randNum = flitPositions[Simulator.rand.Next(flitCount)];
                                       newInput[randNum].isSilver  = true;
                                       newInput[randNum].wasSilver = true;
                                       newInput[randNum].nrWasSilver++;
                                       break;
                    }
                }
            }

            /* Twist ports or not */
            if (Config.sortnet_twist) {
                nodes[0].in_0 = newInput[Simulator.DIR_UP];
                nodes[0].in_1 = newInput[Simulator.DIR_RIGHT];
                nodes[1].in_0 = newInput[Simulator.DIR_DOWN];
                nodes[1].in_1 = newInput[Simulator.DIR_LEFT];
            }
            else {
                nodes[0].in_0 = newInput[Simulator.DIR_UP];
                nodes[0].in_1 = newInput[Simulator.DIR_DOWN];
                nodes[1].in_0 = newInput[Simulator.DIR_LEFT];
                nodes[1].in_1 = newInput[Simulator.DIR_RIGHT];
            }

            /* Execute first arbiter stage */
            nodes[0].doStep();
            nodes[1].doStep();
            
            
            tmp[Simulator.DIR_UP]    = nodes[0].out_0;
            tmp[Simulator.DIR_DOWN]  = nodes[1].out_0;
            tmp[Simulator.DIR_RIGHT] = nodes[0].out_1;
            tmp[Simulator.DIR_LEFT]  = nodes[1].out_1;
            

            if (Config.hardPotato)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (tmp[i] != null && i != tmp[i].prefDir)
                    {
                        //double latency     = (Simulator.CurrentRound - tmp[i].injectionTime);
                        //double probability = ((Config.hardPotatoConstant) * Math.Log(latency)) / latency;
                        switch(tmp[i].hardstate)
                        {
                            /*case Flit.HardState.Normal:
                                if(Simulator.rand.NextDouble() < probablility)
                                    tmp[i].hardstate = Flit.HardState.Excited;
                                break;*/
                            case Flit.HardState.Excited:
                                tmp[i].hardstate = Flit.HardState.Normal;
                                break;
                            case Flit.HardState.Running:
                                tmp[i].hardstate = Flit.HardState.Normal;
                                break;
                        }
                    }
                }
            }

			if (Config.resubmitBuffer && Config.middleResubmit) {   
            	/* Order deflected flits */
                int[] best = rebufRemovalPriority(tmp, ref redirection);
                
                /* Remove the deflected flits in order */
                rebufRemoveFlits(tmp, best);
            }
            
            /* Connect the first arbiter stage to the second */
            nodes[2].in_0 = tmp[Simulator.DIR_UP];		//nodes[0].out_0;
            nodes[2].in_1 = tmp[Simulator.DIR_DOWN];	//nodes[1].out_0;
            nodes[3].in_0 = tmp[Simulator.DIR_RIGHT];	//nodes[0].out_1;
            nodes[3].in_1 = tmp[Simulator.DIR_LEFT];	//nodes[1].out_1;

            /* Execute the second arbiter stage */
            nodes[2].doStep();
            nodes[3].doStep();
            
            /* Temporary storage for resubmit buffer ejection */
            tmp[Simulator.DIR_UP]    = nodes[2].out_0;
            tmp[Simulator.DIR_DOWN]  = nodes[2].out_1;
            tmp[Simulator.DIR_RIGHT] = nodes[3].out_0;
            tmp[Simulator.DIR_LEFT]  = nodes[3].out_1;
            
            int deflectionCount = 0;

           	for (int i = 0; i < 4; i++)
           	{
           		if (tmp[i] != null)
           		{
       				//int orig_input = tmp[i].orig_input;

	           		if (tmp[i].prefDir != i)
	           		{
	           			tmp[i].Deflected = true;
	           			tmp[i].wasDeflected = true;
	           			tmp[i].nrWasDeflected++;
	           			deflectionCount++;
                        if (Config.hardPotato)
                        {
                            double latency     = (Simulator.CurrentRound - tmp[i].injectionTime);
                            double probability = ((Config.hardPotatoConstant) * Math.Log(latency)) / latency;
                            switch(tmp[i].hardstate)
                            {
                                case Flit.HardState.Normal:
                                    if(Simulator.rand.NextDouble() < probability)
                                        tmp[i].hardstate = Flit.HardState.Excited;
                                    break;
                                case Flit.HardState.Excited:
                                    tmp[i].hardstate = Flit.HardState.Normal;
                                    break;
                                case Flit.HardState.Running:
                                    tmp[i].hardstate = Flit.HardState.Normal;
                                    break;
                            }
                        }
                            
       				    /*if (Config.inputBuffer && Config.inputBuffer_retry && orig_input == -1)
       					    continue;
                        
                        if (Config.inputBuffer && Config.inputBuffer_retry)
	           			{
	           				if(rBuf[orig_input].isFull())
	           					rBuf[orig_input].removeFlit();
	           				else
	           					tmp[i] = null;
	           			}*/
	           		}
	           		else
	           		{
                         switch(tmp[i].hardstate)
                         {
                            case Flit.HardState.Excited:
                                tmp[i].hardstate = Flit.HardState.Running;
                                break;
                         }
                        /*
       				    if (Config.inputBuffer && Config.inputBuffer_retry && orig_input == -1)
       					    continue;
	           			
                        if (Config.inputBuffer && Config.inputBuffer_retry)
	           			{
	           				rBuf[orig_input].removeFlit();
	           			}
                        */
	           		}
	           	}
           	}
            
            switch(deflectionCount)
            {
                case 0: Simulator.stats.deflected_0.Add(); break;
                case 1: Simulator.stats.deflected_1.Add(); break;
                case 2: Simulator.stats.deflected_2.Add(); break;
                case 3: Simulator.stats.deflected_3.Add(); break;
            }

            for (int i = 0; i < 4; i++)
                if(tmp[i] != null)
                {
                    if(tmp[i].prefDir != i)
                    {
                        if (tmp[i].isSilver)
                            Simulator.stats.silver_deflected.Add();
                    }
                    else
                        if (tmp[i].isSilver)
                            Simulator.stats.silver_productive.Add();
                }           
                

            /* Prioritizing by deflected flits */
            if (Config.prioByDefl)
                prioritizeByDefl(tmp);
            /* Buffer that removes deflected flits and resubmits them later */
            else if (Config.resubmitBuffer && !Config.middleResubmit) {   
            	// Order deflected flits 
                int[] best = rebufRemovalPriority(tmp, ref redirection);
                
                // Remove the deflected flits in order 
                rebufRemoveFlits(tmp, best);
            }
            else if (Config.outputBuffers)
            {
                int[] best = rebufRemovalPriority(tmp, ref redirection);
                outputBufferRemove(tmp, best);
                outputBufferInject(tmp);
            }
            else if (Config.inputResubmitBuffers)
            {
                inputResubmitEjection(tmp);   
            }
            
            /*Console.WriteLine("\n {0}", coord);
            for (int i = 0; i < 4; i++)
            {
                if(tmp[i] != null)
                    Console.WriteLine("BEFORE temp[{0}] = {1}.{2}", i, tmp[i].packet.ID, tmp[i].flitNr);
            }*/
            
            Flit[] newTmp = null;
            if (Config.inputBuffer)
            {
                newTmp = inputBufferValidateStage(tmp);
            }
            else
                newTmp = tmp;
            
            /*for (int i = 0; i < 4; i++)
            {
                if(newTmp[i] != null)
                    Console.WriteLine("AFTER temp[{0}] = {1}.{2}", i, newTmp[i].packet.ID, newTmp[i].flitNr);
            }*/

            /* Assign the outputs */
            input[Simulator.DIR_UP]    = newTmp[0];
            input[Simulator.DIR_DOWN]  = newTmp[2];
            input[Simulator.DIR_RIGHT] = newTmp[1];
            input[Simulator.DIR_LEFT]  = newTmp[3];

            /*if(Config.inputBuffer_retry)
           	{
	           	// Store extra bits 
	        	for (int i = 0; i < 4; i++)
	        	{
	        		if (extraBuf[i] != null) {
	        			rBuf[i].addFlit(extraBuf[i]);
	        		}
	        	}
        	}*/
        }
        
        protected Flit[] inputBufferStage(Flit[] input)
        {
            Flit[] newInput = new Flit[5];
            input[4] = null;
            // Put inputs in buffers
            for (int i = 0; i < 4; i++)
            {
                newInput[i] = null;
                if (input[i] != null)
                {
                    rBuf[i].addFlit(input[i]);
                }
            }

            // Remove flits from buffers and put a flag on them for their
            // original input

            for (int i = 0; i < 4; i++)
            {
                if (newInput[i] != null)
                    throw new Exception("Line isn't clear");

                if (rBuf[i].isEmpty())
                {
                    rBuf[i].clearTry();
                    continue;
                }
                
                //Console.WriteLine("{0}",coord);
                //rBuf[i].printBuffer();

                if (rBuf[i].isFull())
                {
                    newInput[i] = rBuf[i].getNextFlit();
                    newInput[i].orig_input = i;
                    newInput[i].must_schedule = true;
                    rBuf[i].clearTry();
                }
                else
                {
                    newInput[i] = rBuf[i].getNextFlit();
                    newInput[i].orig_input = i;
                    newInput[i].must_schedule = false;
                    rBuf[i].tryFlit();
                }
            }
            return newInput;
        }

        protected Flit[] inputBufferValidateStage(Flit[] input)
        {
            Flit[] output = new Flit[4];
            for (int i = 0; i < 4; i++)
            {
                output[i] = input[i];
            }
            
            for (int i = 0; i < 4; i++)
            {
                //rBuf[i].printBuffer();
                if (input[i] != null && input[i].must_schedule)
                    output[i] = rBuf[input[i].orig_input].removeFlit();

                if (rBuf[i].triedFlit())
                {
                    //bool found = false;
                    for (int j = 0; j < 4; j++)
                    {
                        if (input[j] != null && input[j].orig_input == i)
                        {
                            if (input[j].must_schedule)
                            {
                                throw new Exception("Need to submit a scheduled flit");
                            }
                            //found = true;
                            if (input[j].prefDir != j && !(input[j].dest.x == coord.x&& input[j].dest.y == coord.y))
                            {
                                output[j] = null;
                                //Console.WriteLine("\tRETRY!");
                            }
                            else
                            {
                                output[j] = rBuf[i].removeFlit();
                            }
                        }
                    }
                    //if (!found)
                    //{
                    //    throw new Exception("Buffer tried a flit that cannot return");
                    //}
                }
                rBuf[i].clearTry();
            }
            return output;
        }

        protected int inputResubmitInjection(Flit[] input, ref bool redirection)
        {
            int count = 0;
            int dir = 0; //startInput; //Simulator.rand.Next(4);
            //startInput++;
            //if(startInput == 4) startInput = 0;

            for (int i = 0; i < 4; i++)
            {
                int curdir = dir + i;
                //if (curdir > 3) curdir -= 4;
                if (input[curdir] == null && !rBuf[curdir].isEmpty())
                {
                    input[curdir] = rBuf[curdir].removeFlit();
                    count++;
                    //break;
                }
            }
            return count;   
        }

        protected void inputResubmitEjection(Flit[] f)
        {
            int dir = 0;//startInput; //0; Simulator.rand.Next(4);
            //startInput++;
            //if(startInput == 4) startInput = 0;

            for (int i = 0; i < 4; i++)
            {
                int curdir = dir + i;
                //if (curdir > 3) curdir -= 4;
                if (f[curdir] != null && i != f[curdir].prefDir && !rBuf[curdir].isFull())
                {
                    Simulator.stats.rebuf_totalChecks.Add();
                    bool storeInResubmitBuffer = true;
                    /* If redirection is occuring, don't allow more flits into the buffer */
                    /*if (redirection) {
                        Simulator.stats.rebuf_isRedirection.Add();
                        if (Config.noResubmitRedirection)
                        {
                            storeInResubmitBuffer = false;
                            return best;
                        }
                    }*/

                    /* If the flit is golden, don't allow it into the buffer */
                    if (Simulator.network.golden.isGolden(f[i])) {
						Simulator.stats.rebuf_isGolden.Add();
						if (Config.noResubmitGolden)
                            storeInResubmitBuffer = false;
                    }

                    /* If this flit just came out of the rebuf, should it go back in? */
                    if (f[i].wasInRebuf) {
                        Simulator.stats.rebuf_isRebufTwice.Add();
                        if (Config.noResubmitTwice) 
                            storeInResubmitBuffer = false; 
                    }

                    /* If the flit needs to be ejected at this router, don't resubmit it? */
                    if (f[i].currentX == f[i].dest.x && f[i].currentY == f[i].dest.y) {
                        Simulator.stats.rebuf_isLocalDest.Add();
                        if (Config.noResubmitLocalDest) 
                            storeInResubmitBuffer = false;
                    }
                    
                    /* If the deflected flit still went in a productive direction, should it go in the
                     *  resubmit buffer? 
                     */
                    if (isProductive(f[i], i)) {
                        Simulator.stats.rebuf_isProductive.Add();
                        if (Config.noResubmitProductive) 
                            storeInResubmitBuffer = false;
                    }

                    /* If the distance is less than the no resubmit distance, don't put it in the buffer */
                    if (f[i].distance <= Config.noResubmitDist) {
                        Simulator.stats.rebuf_isClose.Add();
                        if (Config.noResubmitClose) 
                            storeInResubmitBuffer = false;
                    }
                     
                    if(storeInResubmitBuffer)
                    {
                        rBuf[curdir].addFlit(f[curdir]);
                        f[curdir].nrInRebuf++;
                        f[curdir] = null;
                    }
                    //break;
                }
            }
            return;
        }

        protected void outputBufferRemove(Flit[] f, int[] best)
        {
            for(int i = 0; i < Config.nrOutputRemove; i++)
            {   
                if (best[i] != -1)
                {
                    int index = best[i];
                    Flit temp = f[index];
                    int dir = temp.prefDir;
                    
                    if (dir < 1 || dir > 3)
                        continue;

                    if (!rBuf[dir].isFull())
                    {   
                        rBuf[temp.prefDir].addFlit(temp);
                        f[index] = null;
                    }
                }
                else
                    break;
            }
        }

        protected void outputBufferInject(Flit[] f)
        {
            for (int i = 0; i < Config.nrOutputInject; i++)
            {
                if (f[i] == null)
                {
                    if(!rBuf[i].isEmpty())
                        f[i] = rBuf[i].removeFlit();
                }
            }
        }
        /* For input buffers */
        protected Flit[] bufferInjectEject(Flit[] input)
        {
        	Flit[] extraBuf = new Flit[4];
        	
        	/* Pull in the inputs */
        	for (int i = 0; i < 4; i++)
        	{
        		if (input[i] != null) {
        			if (!rBuf[i].isFull()) {
	        			rBuf[i].addFlit(input[i]);
	        		}
	        		else {
	        			extraBuf[i] = input[i];
	        		}
	        	}
        		input[i] = null;
        	}
        	
        	/* Output flits */
        	if (Config.inputBuffer_retry)
        	{
        		for (int i = 0; i < 4; i++)
        		{
        			if (!rBuf[i].isEmpty())
        			{
                        if (rBuf[i].isFull()) {
                            input[i] = rBuf[i].removeFlit();
                            input[i].orig_input = -1;
                            rBuf[i].addFlit(extraBuf[i]);
                        }
                        else {
        				    input[i] = rBuf[i].getNextFlit();
                            input[i].orig_input = i;
                        }
                        input[i].wasInRebuf = true;
        				input[i].nrInRebuf++;
        			}
        		}
        	}
        	else
        	{
	        	for (int i = startInput; i < 4; i += 2)
	        	{
	        		if (!rBuf[i].isEmpty()) {
	        			input[i] = rBuf[i].removeFlit();
	        			input[i].wasInRebuf = true;
	        			input[i].nrInRebuf++;
	        		}
	        	}
        	}
        	/*if(!Config.inputBuffer_retry)
           	{
	           	// Store extra bits 
	        	for (int i = 0; i < 4; i++)
	        	{
	        		if (extraBuf[i] != null) {
	        			if (!rBuf[i].isFull()) {
	        				rBuf[i].addFlit(extraBuf[i]);
	        			}
	        			else {
	        				if(input[i] != null) {
	        					throw new Exception("Something strange happened");
	        				}
	        				else {
	        					input[i] = extraBuf[i];
	        				}
	        			}
	        		}
	        	}
        	}*/
        	// Flip between 0 and 1
        	switch (startInput){
        		case 0: startInput = 1; break;
        		case 1: startInput = 0; break;
        	}
        	return extraBuf;
        }

        /* 
         * Injects flits from the resubmit buffer 
         */
        protected int rebufInjection(Flit[] input, ref bool redirection)
        {
            int resubmitInjectCount = 0;
            bool redirectHold = false;
            
            // Storage for redirection into buffer
            Flit[] temp = new Flit[4];
            temp[0] = null;
            temp[1] = null;
            temp[2] = null;
            temp[3] = null;
            
            // Sanity checks
            if (!Config.resubmitBuffer)
                throw new Exception("No resubmit buffer to do resubmitbuffer injection");

            if (Config.rebufRemovalCount > 4 || Config.rebufRemovalCount <= 0)
                throw new Exception("Invalid rebufRemovalCount");

            if (Config.rebufInjectCount > 4 || Config.rebufInjectCount <= 0)
                throw new Exception("Invalid rebufInjectCount");
            
            // If the threshold of the number of times, that the resubmit buffer cannot inject, is exceeded,
            //   start the redirection
            if (Config.redirection) {
            	
            	/* Start redirection process if the buffer couldn't inject for the threshold */
                if (resubmitCantInjectCount > Config.redirection_threshold)
                {
                    Simulator.stats.redirectionCount.Add();
                    resubmitBlockInputCount = 0;
                }

                /* HACK HACK HACK quicker redirection? */
                if (rBuf[0].isEmpty() && resubmitBlockInputCount < Config.redirectCount + 1)
                {
                    //Console.WriteLine("????????????? end redirection buffer is empty");
                    resubmitBlockInputCount = Config.redirectCount + 1;
                }
              	/* If there hasn't been enough redirects, enable redirection */
                if (resubmitBlockInputCount < Config.redirectCount + 1)
                    redirection = true; 

                /* HACK HACK HACK for timing issues */
                if (Config.pipelineCount > 0 && resubmitBlockInputCount == 0)
                    redirectHold = true;
                
				if (redirection && !redirectHold) {
                    // Force resubmit buffer to output flits even if all ports are filled
                    //  First open up an input
                    if (Config.resubmitLineBuffers) {
                        for (int i = 0; i < 5; i++) {
                            if (input[i] != null && !Simulator.network.golden.isGolden(input[i])) {
                                temp[i]  = input[i];
                                input[i] = null;
                            }
                        }
                    }
                    else {
                        // Eject a random port
                        int i = Simulator.rand.Next(0,4); 
//----------------------------------------------------------------------------------------------------------------------------------------
                        if (input[i] != null && !Simulator.network.golden.isGolden(input[i])) {   
                            temp[0]  = input[i];
                            input[i] = null;
                        }
                    }
                }
            }
            
            // Resubmit buffer injection
            // If 1 resubmit buffer
            if (!Config.resubmitLineBuffers) {
	            for (int i = 0; i < 4; i++) {
	                /* HACK HACK HACK added with timing fixes */
                    if (rBuf[0].isEmpty())
                    {
                        resubmitCantInjectCount = 0;
                        break;
                    }
                    if (input[i] == null) {
	                    resubmitCantInjectCount = 0;
	                    if (rBuf[0].isEmpty()) {
	                    	if (redirection) {
	                    		input[i] = temp[0];
	                    		temp[0]  = null;
	                    	}	
	                        break;
	                    }
	                    else {
	                        resubmitInjectCount++;
	                        Flit tempFlit = rBuf[0].getNextFlit();
	                        ulong timeInRebuf = Simulator.CurrentRound - tempFlit.rebufInTime;
                            
                            /* HACK HACK HACK Timing hack */
                            if (!redirection || redirectHold)
                            {
                                if(timeInRebuf < (ulong)(Config.pipelineCount + 1))
                                    break;
                            }
                            else
                            {
                                if(timeInRebuf < 1)
                                    break;
                            }

                            input[i] = rBuf[0].removeFlit();
                            input[i].rebufOutTime = Simulator.CurrentRound;
	                        input[i].wasInRebuf = true;
	                        input[i].nrInRebuf++;
	                        
	                        //ulong timeInRebuf = input[i].rebufOutTime - input[i].rebufInTime;
	                        Simulator.stats.timeInRebuf.Add(timeInRebuf);
	                        statsInjectResubmit(input[i]);
	                        
	                        // If the amount to be injected has been injected, stop
	                        if (resubmitInjectCount >= Config.rebufInjectCount)
	                            break;
	                    }
	                }
	            }
	        }
	        // If 4 resubmit buffers
            else {
	            for (int i = 0; i < 4; i++) {
	                if(currentInput >= 4)
	                    currentInput = 0;
	
	                if (input[currentInput] == null) {
	                    resubmitCantInjectCount = 0;
	                   
	                    if (rBuf[currentInput].isEmpty()) {
	                        if (redirection) {
	                            input[currentInput] = temp[currentInput];
	                            temp[currentInput]  = null;
	                        }
                            continue;
	                    }
	                    else {
	                        resubmitInjectCount++;
	                        input[currentInput] = rBuf[currentInput].removeFlit();
	                        input[currentInput].wasInRebuf = true;
	                        input[currentInput].nrInRebuf++;
	                        
	                        input[i].rebufOutTime = Simulator.CurrentRound;  
	                        ulong timeInRebuf = input[i].rebufOutTime - input[i].rebufInTime;
	                        Simulator.stats.timeInRebuf.Add(timeInRebuf);
	                        statsInjectResubmit(input[currentInput]);
	
	                        /* If the redirection mode is on, fill all 4 lines */
	                        if (!redirection)
	                            if (resubmitInjectCount >= Config.rebufInjectCount) 
	                                break;
	                    }
	                }
	                currentInput++;
	            }
	        }
              
            
            if (Config.redirection) {
            	/* Increment blocked counter if redirecting */
                if(resubmitInjectCount == 0) {
                    if(Config.resubmitLineBuffers) {
                        if(!rBuf[0].isEmpty() && !rBuf[1].isEmpty() && !rBuf[2].isEmpty() && !rBuf[3].isEmpty())
                            resubmitCantInjectCount++;
                    }
                    else {
                        if(!rBuf[0].isEmpty())
                            resubmitCantInjectCount++;
                    }
                }
                /* Store redirected flits */
                if (redirection) {
                    int count;

                    if (Config.resubmitLineBuffers)
                        count = 4;
                    else
                        count = 1;
                        
                    for (int i = 0; i < count; i++) {
                        if (temp[i] != null) {
                            Simulator.stats.redirectedFlits.Add();
                        	rBuf[i].addFlit(temp[i]);
                        	if(temp[i].nrInRebuf > 0) 
                        	statsEjectResubmit(temp[i]);

		                    temp[i].rebufInTime = Simulator.CurrentRound;
			                
			                if(temp[i].nrInRebuf > 0)         
							{
								ulong timeBetweenRebuf = temp[i].rebufInTime - temp[i].rebufOutTime;
			                	Simulator.stats.timeBetweenRebuf.Add(timeBetweenRebuf);
			               	}
			               	
		                    temp[i].rebufLoopCount++;
                    
                    		temp[i] = null;
                    	}
                    }
                    resubmitBlockInputCount++;
                }
            }

			return resubmitInjectCount;
        }

		/*
		 * Prioritize removal of flits to put in resubmit buffer 
		 */
        protected int[] rebufRemovalPriority(Flit[] f, ref bool redirection)
        {
            int[] best = new int[4];
            int   temp;
            bool  storeInResubmitBuffer = true;
            
            best[0] = best[1] = best[2] = best[3] = -1;
            
            /* Find deflected flits */
            for (int i = 0; i < 4; i++) {
                storeInResubmitBuffer = true;
                
                /* If there is a flit, figure out whether to put it in the rebuf or not */
                if (f[i] != null) {
                    /* If the flit was deflected, add it to the prioritzation list */
                    if (f[i].prefDir != i) {	
                  		
                        /* If redirection is occuring, don't allow more flits into the buffer */
                        if (redirection) {
                        	Simulator.stats.rebuf_isRedirection.Add();
                        	if (Config.noResubmitRedirection)
                            {
                                storeInResubmitBuffer = false;
                                return best;
                            }
                        }

                        /* If the flit is golden, don't allow it into the buffer */
                        if (Simulator.network.golden.isGolden(f[i])) {
							Simulator.stats.rebuf_isGolden.Add();
							if (Config.noResubmitGolden)
                            	storeInResubmitBuffer = false;
                        }

                        /* If this flit just came out of the rebuf, should it go back in? */
                        if (f[i].wasInRebuf) {
                            Simulator.stats.rebuf_isRebufTwice.Add();
                            if (Config.noResubmitTwice) 
                            	storeInResubmitBuffer = false; 
                        }

                        /* If the flit needs to be ejected at this router, don't resubmit it? */
                        if (f[i].currentX == f[i].dest.x && f[i].currentY == f[i].dest.y) {
                            Simulator.stats.rebuf_isLocalDest.Add();
                            if (Config.noResubmitLocalDest) 
                            	storeInResubmitBuffer = false;
                        }
                        
                        /* If the deflected flit still went in a productive direction, should it go in the
                         *  resubmit buffer? 
                         */
                        if (isProductive(f[i], i)) {
                        	Simulator.stats.rebuf_isProductive.Add();
                            if (Config.noResubmitProductive) 
                            	storeInResubmitBuffer = false;
                        }

                        /* If the distance is less than the no resubmit distance, don't put it in the buffer */
                        if (f[i].distance <= Config.noResubmitDist) {
                            Simulator.stats.rebuf_isClose.Add();
                            if (Config.noResubmitClose) 
                            	storeInResubmitBuffer = false;
                        }
                            
                        if (f[i].packet != null && Controller.smallest_mpki == f[i].packet.requesterID) {
                            Simulator.stats.rebuf_smallest_mpki.Add();
                            if (Config.app_aware_buffer && Config.smallest_mpki && Config.reverse_mpki)
                                storeInResubmitBuffer = false;
                        }
                        else {
                            Simulator.stats.rebuf_bigger_mpki.Add();
                            if (Config.app_aware_buffer && Config.smallest_mpki && !Config.reverse_mpki)
                                storeInResubmitBuffer = false;
                        }

                        if (f[i].packet != null && Controller.largest_mpki == f[i].packet.requesterID) {
                            Simulator.stats.rebuf_largest_mpki.Add();
                            if (Config.app_aware_buffer && Config.largest_mpki && Config.reverse_mpki)
                                storeInResubmitBuffer = false;
                        }
                        else {
                            Simulator.stats.rebuf_smaller_mpki.Add();
                            if (Config.app_aware_buffer && Config.largest_mpki && !Config.reverse_mpki)
                                storeInResubmitBuffer = false;
                        }
                        
                        Simulator.stats.rebuf_totalChecks.Add();
                        /* If it passed all the noResubmits, put it in the buffer priority array */
                        if (storeInResubmitBuffer) {	
                            best[3] = i;
                            /* Prioritize the flits being put into the resubmit buffer */
                            for (int j = 2; j >= 0; j--) {
                                /* If the slot is empty, or is of lesser priority, swap */
                                if (best[j] == -1 || reBufEjPriority(f[best[j]], f[best[j+1]]) > 0) {
                                    temp = best[j];
                                    best[j] = best[j+1];
                                    best[j+1] = temp;
                                }
                                else
                                    break;
                            }
                        }    
                    }
                    else
                    { 
                    	if(f[i].wasInRebuf)
                    	{
                            Simulator.stats.rebuf_nrOfLoopsInRebuf.Add(f[i].rebufLoopCount);
		                    
                            f[i].rebufLoopCount = 0;
		                    /* Refresh wasInBuf */
		                    if (Config.wasInRebufCycleRefresh)
		                        f[i].wasInRebuf = false;
	                    }
                    }
               }
            }  
            return best;
        }
        
		/*
		 * Resubmit buffer flit removal 
		 */
        protected void rebufRemoveFlits(Flit[] f, int[] best)
        {
        	int bufIndex = 0;
        	int i = 0;
        	
            /* Insert flits into resubmit buffer in order of priority */
            for (; i < Config.rebufRemovalCount; i++) {
                // If there are no more deflected flits exit
                if (best[i] == -1)
                    break;
                
                // If there are 4 buffers pick the appropriate one
                if(Config.resubmitLineBuffers)
                    bufIndex = best[i];

				// If skip increase count
                if (Config.noResubmitSkip && resubmitSkipCount < Config.noResubmitSkipCount) {
                    resubmitSkipCount++;
                    if(f[i].wasInRebuf)
                	{
                		//Simulator.stats.rebuf_nrOfLoopsInRebuf.Add(f[best[i]].rebufLoopCount);
	                    //f[best[i]].rebufLoopCount = 0;
	                    /* Refresh wasInBuf */
	                    if (Config.wasInRebufCycleRefresh)
	                        f[best[i]].wasInRebuf = false;
                    }
                }
                // Remove flit and put it into the resubmit buffer	
                else if (!rBuf[bufIndex].isFull()) {
                    if(Config.noResubmitSkip)
                        resubmitSkipCount = 0;

                    Simulator.stats.resubmittedFlits.Add();
                    rBuf[bufIndex].addFlit(f[best[i]]);
                    statsEjectResubmit(f[best[i]]);
                    f[best[i]].rebufInTime = Simulator.CurrentRound;
	                
	                if(f[best[i]].nrInRebuf > 0)         
					{
						ulong timeBetweenRebuf = f[best[i]].rebufInTime - f[best[i]].rebufOutTime;
	                	Simulator.stats.timeBetweenRebuf.Add(timeBetweenRebuf);
	               	}
	               	
                    f[best[i]].rebufLoopCount++;
                    f[best[i]].rebufEnteredCount++;
                    f[best[i]].wasInRebuf = true;
                    f[best[i]].nrInRebuf++;
                    f[best[i]] = null;
                }
                else if (!Config.resubmitLineBuffers)
                	break;
            }
            for(;i < 4; i++) {
            	if (best[i] != -1 && f[best[i]].wasInRebuf)
            	{
            		Simulator.stats.rebuf_nrOfLoopsInRebuf.Add(f[best[i]].rebufLoopCount);
                    f[best[i]].rebufLoopCount = 0;
                    /* Refresh wasInBuf */
                    if (Config.wasInRebufCycleRefresh)
                        f[best[i]].wasInRebuf = false;
                }
            }
            
            /* Buffer stats */
            statsResubmitBuffer(rBuf[0]);

            if (Config.resubmitLineBuffers) {
                statsResubmitBuffer(rBuf[1]);
                statsResubmitBuffer(rBuf[2]);
                statsResubmitBuffer(rBuf[3]);
            }
        }

        /* 
         * Prioritizing by deflections 
         */
        protected void prioritizeByDefl(Flit[] f)
        {
            for (int i = 0; i < 4; i++) 
            {                 
				if (Config.wasDeflectCycleRefresh && f[i] != null)
				    f[i].wasDeflected = false;
				    
				/* If there is a flit, figure out whether to set the deflected bit */
				if (f[i] != null)
				{
					if(f[i].prefDir != i) 
					{
                        /* If the flit was productive, don't set it to be deflective */
                        if (Config.noDeflectProductive && isProductive(f[i], i))
                            f[i].wasDeflected = false;
                        else if (Config.noDeflectProductive)
                            f[i].wasDeflected = true;
                        else
                        {
                            /* Prio schemes */
                            f[i].wasDeflected = true;
                        }
                    }
                }
            }
        }

        /* 
         * Returns true if the flit went in a direction towards its destination 
         */
        protected bool isProductive(Flit f, int dir)
        {
            switch (dir) 
            {
                case Simulator.DIR_UP:    if (f.dest.y > f.currentY) return true;
										  break;
			
				case Simulator.DIR_DOWN:  if (f.dest.y < f.currentY) return true;
							              break;
			
				case Simulator.DIR_RIGHT: if (f.dest.x > f.currentX) return true;
							              break;
			
				case Simulator.DIR_LEFT:  if (f.dest.x < f.currentX) return true;
								          break;
                default: throw new Exception("Not a direction");
			} 
            return false;
        }

        /* 
         * Prioritizes flits that go into the resubmit buffer 
         */
        protected int reBufEjPriority(Flit f1, Flit f2) 
        {
            if (f1 == null || f2 == null)
                throw new Exception("Can't compare to a null flit");
			
            int ret;
			/* Ranking schemes */
            switch(Config.resubmitBy)
            {
                case "Random":          ret = (1 == Simulator.rand.Next(2)) ? -1 : 1;          
                                        break;
                case "InjectionTime":   ret = (f1.injectionTime == f2.injectionTime) ?  0 :
                                              (f1.injectionTime >  f2.injectionTime) ? -1 : 1; 
                                        break;
                case "Priority":        ret = (f1.priority == f2.priority) ?  0 : 
                                              (f1.priority >  f2.priority) ? -1 : 1; 
                                        break;
                case "Deflections":     ret = (f1.nrOfDeflections == f2.nrOfDeflections) ?  0 :
                                              (f1.nrOfDeflections >  f2.nrOfDeflections) ? -1 : 1; 
                                        break;
                case "MPKI_Larger":     ret = (Simulator.controller.MPKI[f1.packet.requesterID] == Simulator.controller.MPKI[f2.packet.requesterID]) ? 0 : (Simulator.controller.MPKI[f1.packet.requesterID] > Simulator.controller.MPKI[f2.packet.requesterID]) ? -1 : 1;
                                        break;
                case "Bias":            ret = -1; 
                                        break;
                default: throw new Exception("Not a resubmit scheme");
            }
            
            if (Config.resubmitByRandomVariant && ret == 0)
                ret = (1 == Simulator.rand.Next(2)) ? -1 : 1;
            
            if (Config.flip_resubmitPrio)
                ret = (ret == -1) ? 1 : (ret == 1) ? -1 : 0;

            return ret;
        }

		/* 
		 * Takes stats when the resubmit buffer puts flits back into the network 
		 */
        protected void statsInjectResubmit(Flit f)
        {
            if (null == f)
                throw new Exception("Null flit in inject resubmit stats");

            Simulator.stats.inject_resubmit.Add();
            
			if (f.isHeadFlit) 
				Simulator.stats.inject_resubmit_head.Add();
            
			
			if (f.packet != null)
            {
                Simulator.stats.inject_resubmit_bysrc[f.packet.src.ID].Add();
                //Simulator.stats.inject_flit_srcdest[f.packet.src.ID, f.packet.dest.ID].Add();
            }
        }
		
        /* 
         * Takes stats when the resubmit buffer takes flits from the network 
         */
        protected void statsEjectResubmit(Flit f)
        {
        	if(f == null)
        		throw new Exception("null flit in stats");
        		
            int tmp_dist = Math.Abs(f.currentX - f.packet.dest.x) + 
						   Math.Abs(f.currentY - f.packet.dest.y);
							
            // per-flit stats
            Simulator.stats.eject_resubmit.Add();
            Simulator.stats.eject_resubmit_bydest[f.packet.dest.ID].Add();
			Simulator.stats.eject_resubmit_distance.Add(f.distance);
			
			Simulator.stats.rebuf_InjectionTime.Add(f.injectionTime);
			Simulator.stats.rebuf_NrOfDeflections.Add(f.nrOfDeflections);
			Simulator.stats.rebuf_nrInRebuf.Add(f.nrInRebuf);
			Simulator.stats.rebuf_Distance.Add(tmp_dist);
			
			Simulator.stats.rebuf_isGoldenInBuffer.Add();
			
			if (f.isHeadFlit) 
				Simulator.stats.rebuf_HeadFlits.Add();
			
			if (f.isTailFlit) 
				Simulator.stats.rebuf_TailFlits.Add();
			
			if (f.wasInRebuf) 
				Simulator.stats.rebuf_WasInRebuf.Add();
        }

		/*
		 * Takes stats once a cycle about the resubmit buffer 
		 */
        protected void statsResubmitBuffer(ResubBuffer reBuf)
        {
            Simulator.stats.resubmit_flit_count_bycycle.Add(reBuf.count());
            //Simulator.stats.resubmit_flit_count_byloc[reBuf.getNextFlit().currentX,reBuf.getNextFlit().currentY].Add(reBuf.count());
        }
    }

    public class SortNet_COW : SortNet // Cheap Ordered Wiring?
    {
        SortNode[] nodes;

        public SortNet_COW(SortNode.Rank r)
        {
            nodes = new SortNode[6];

            SortNode.Steer stage1_steer = delegate(Flit f)
            {
                if (f == null) return 0;
                return (f.sortnet_winner) ? 0 : 1;
            };

            SortNode.Steer stage2_steer = delegate(Flit f)
            {
                if (f == null) return 0;
                return (f.prefDir == Simulator.DIR_UP || f.prefDir == Simulator.DIR_RIGHT) ?
                    0 : 1;
            };
           
            nodes[0] = new SortNode(stage1_steer, r);
            nodes[1] = new SortNode(stage1_steer, r);

            nodes[2] = new SortNode(stage2_steer, r);
            nodes[3] = new SortNode(stage2_steer, r);

            nodes[4] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_UP) ? 0 : 1;
                    }, r);
            nodes[5] = new SortNode(delegate(Flit f)
                    {
                        if (f == null) return 0;
                        return (f.prefDir == Simulator.DIR_DOWN) ? 0 : 1;
                    }, r);
        }

        // takes Flit[5] as input; indices DIR_{UP,DOWN,LEFT,RIGHT} and 4 for local.
        // permutes in-place. input[4] is left null; if flit was injected, 'injected' is set to true.
        public override void route(Flit[] input, out bool injected)
        {
            injected = false;

            if (!Config.calf_new_inj_ej)
            {
                // injection: if free slot, insert flit
                if (input[4] != null)
                {
                    for (int i = 0; i < 4; i++)
                        if (input[i] == null)
                        {
                            input[i] = input[4];
                            injected = true;
                            break;
                        }

                    input[4] = null;
                }
            }

            // NS, EW -> NS, EW
            if (!Config.sortnet_twist)
            {
                nodes[0].in_0 = input[Simulator.DIR_UP];
                nodes[0].in_1 = input[Simulator.DIR_RIGHT];
                nodes[1].in_0 = input[Simulator.DIR_DOWN];
                nodes[1].in_1 = input[Simulator.DIR_LEFT];
            }
            else
            {
                nodes[0].in_0 = input[Simulator.DIR_UP];
                nodes[0].in_1 = input[Simulator.DIR_DOWN];
                nodes[1].in_0 = input[Simulator.DIR_LEFT];
                nodes[1].in_1 = input[Simulator.DIR_RIGHT];
            }
            nodes[0].doStep();
            nodes[1].doStep();
            nodes[2].in_0 = nodes[0].out_0;
            nodes[3].in_0 = nodes[1].out_0;
            nodes[3].in_1 = nodes[0].out_1;
            nodes[2].in_1 = nodes[1].out_1;
            nodes[2].doStep();
            nodes[3].doStep();
            nodes[4].in_0 = nodes[2].out_0;
            nodes[4].in_1 = nodes[3].out_0;
            nodes[5].in_0 = nodes[2].out_1;
            nodes[5].in_1 = nodes[3].out_1;
            nodes[4].doStep();
            nodes[5].doStep();
            input[Simulator.DIR_UP] = nodes[4].out_0;
            input[Simulator.DIR_RIGHT] = nodes[4].out_1;
            input[Simulator.DIR_DOWN] = nodes[5].out_0;
            input[Simulator.DIR_LEFT] = nodes[5].out_1;
        }
    }

    public abstract class Router_SortNet : Router
    {
        // injectSlot is from Node; injectSlot2 is higher-priority from
        // network-level re-injection (e.g., placeholder schemes)
        protected Flit m_injectSlot, m_injectSlot2;

        SortNet m_sort;
        protected ResubBuffer[] rBuf;

        public Router_SortNet(Coord myCoord)
            : base(myCoord)
        {
            m_injectSlot = null;
            m_injectSlot2 = null;

            if (Config.sortnet_full)
                m_sort = new SortNet_COW(new SortNode.Rank(rank));
            else
                m_sort = new SortNet_CALF(new SortNode.Rank(rank), this.coord);

            if (!Config.edge_loop)
                throw new Exception("SortNet (CALF) router does not support mesh without edge loop. Use -edge_loop option.");
            
            /*if (Config.inputBuffer)
            {
                rBuf = new ResubBuffer[4];
                for(int i = 0; i < 4; i++)
                    rBuf[i] = new ResubBuffer();
            }*/
        }

        Flit handleGolden(Flit f)
        {
            if (f == null)
                return f;

            if (f.state == Flit.State.Normal)
                return f;

            if (f.state == Flit.State.Rescuer)
            {
                if (m_injectSlot == null)
                {
                    m_injectSlot = f;
                    f.state = Flit.State.Placeholder;
                }
                else
                    m_injectSlot.state = Flit.State.Carrier;

                return null;
            }

            if (f.state == Flit.State.Carrier)
            {
                f.state = Flit.State.Normal;
                Flit newPlaceholder = new Flit(null, 0);
                newPlaceholder.state = Flit.State.Placeholder;

                if (m_injectSlot != null)
                    m_injectSlot2 = newPlaceholder;
                else
                    m_injectSlot = newPlaceholder;

                return f;
            }

            if (f.state == Flit.State.Placeholder)
                throw new Exception("Placeholder should never be ejected!");

            return null;
        }

        // accept one ejected flit into rxbuf
        void acceptFlit(Flit f)
        {
            statsEjectFlit(f);
            if (f.packet.nrOfArrivedFlits + 1 == f.packet.nrOfFlits)
                statsEjectPacket(f.packet);

            m_n.receiveFlit(f);
        }

        Flit ejectLocal()
        {
            // eject locally-destined flit (highest-ranked, if multiple)
            Flit ret = null;
            int bestDir = -1;
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                    linkIn[dir].Out.state != Flit.State.Placeholder &&
                    linkIn[dir].Out.dest.ID == ID &&
                    (ret == null || rank(linkIn[dir].Out, ret) < 0))
                {
                    ret = linkIn[dir].Out;
                    bestDir = dir;
                }

            if (bestDir != -1) linkIn[bestDir].Out = null;
#if DEBUG
            if (ret != null)
                Console.WriteLine("ejecting flit {0}.{1} at node {2} cyc {3}", ret.packet.ID, ret.flitNr, coord, Simulator.CurrentRound);
#endif
            ret = handleGolden(ret);

            return ret;
        }

        Flit[] m_ej = new Flit[4] { null, null, null, null };
        int m_ej_rr = 0;

        Flit ejectLocalNew()
        {
            for (int dir = 0; dir < 4; dir++)
                if (linkIn[dir] != null && linkIn[dir].Out != null &&
                        linkIn[dir].Out.dest.ID == ID &&
                        m_ej[dir] == null)
                {
                    m_ej[dir] = linkIn[dir].Out;
                    linkIn[dir].Out = null;
                }

            m_ej_rr++; m_ej_rr %= 4;

            Flit ret = null;
            if (m_ej[m_ej_rr] != null)
            {
                ret = m_ej[m_ej_rr];
                m_ej[m_ej_rr] = null;
            }

            return ret;
        }

        Flit[] input = new Flit[5]; // keep this as a member var so we don't
                                    // have to allocate on every step (why can't
                                     // we have arrays on the stack like in C?)

        protected override void _doStep()
        {
            for (int dir = 0; dir < 4; dir++)
            {
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                    linkIn[dir].Out.hops++;
           	}
            
            /* Injects input buffers */
            /*if (Config.inputBuffer)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (!rBuf[i].isFull() && !rBuf[i].isEmpty())
                    {
                        input[i] = rBuf[i].getNextFlit();
                        input[i].orig_input = i;
                    }
                    else if (!rBuf[i].isEmpty())
                    {
                        input[i] = rBuf[i].removeFlit();
                        input[i].orig_input = -1;
                    }

                    if (linkIn[i].Out != null)
                        rBuf[i].addFlit(linkIn[i].Out);
                    linkIn[i].Out = null;
                }
                
                for (int i = 0; i < 4; i++)
                {
                    linkIn[i].Out = input[i];
                    input[i] = null;
                }
            }*/
            
            /* Ejection selection and ejection */
            Flit[] eject = new Flit[4];
            eject[0] = eject[1] = eject[2] = eject[3] = null;
            int wantToEject = 0;
            for (int i = 0; i < 4; i++)
                if (linkIn[i] != null && linkIn[i].Out != null)
                {
                    if(linkIn[i].Out.dest.x == coord.x && linkIn[i].Out.dest.y == coord.y)
                        wantToEject++;
                }
            
            switch(wantToEject)
            {
                case 0: Simulator.stats.eject_0.Add(); break;
                case 1: Simulator.stats.eject_1.Add(); break;
                case 2: Simulator.stats.eject_2.Add(); break;
                case 3: Simulator.stats.eject_3.Add(); break;
                case 4: Simulator.stats.eject_4.Add(); break;
                default: throw new Exception("Eject problem");
            }

            for (int i = 0; i < Config.ejectCount; i++)
            {
                if (Config.calf_new_inj_ej)
                    eject[i] = ejectLocalNew();
                else
                    eject[i] =  ejectLocal();

                if (eject[i] == null)
                    break;
                /*else if (Config.inputBuffer && eject[i].orig_input != -1)
                {
                    rBuf[eject[i].orig_input].removeFlit();
                }*/
            }
            

            /* Setup the inputs */
            for (int dir = 0; dir < 4; dir++)
            {
                /* If there is a link, and its not null, set the inputs */
                if (linkIn[dir] != null && linkIn[dir].Out != null)
                {
                    input[dir] = linkIn[dir].Out;
                    input[dir].inDir = dir;
                }
                else
                    input[dir] = null;
            }
            
			
            /* Injection */
            Flit inj = null;
            bool injected = false;

            /* If the 2nd slot has data, remove it to be injected */
            if (m_injectSlot2 != null)
            {
                inj = m_injectSlot2;
                m_injectSlot2 = null;
            }
            /* Otherwise if the 1st slot has data, remove it to be injected */
            else if (m_injectSlot != null)
            {
                inj = m_injectSlot;
                m_injectSlot = null;
            }

            /* Port 4 becomes the injected line */
            input[4] = inj;

            /* If there is data, set the injection direction */
            if (inj != null)
                inj.inDir = -1;

            /* Go thorugh inputs, find their preferred directions */
            for (int i = 0; i < 5; i++)
                if (input[i] != null)
                {
                    PreferredDirection pd = determineDirection(input[i]);
                    /* If it wants to go in the x direction, prefer that first */
                    if (pd.xDir != Simulator.DIR_NONE)
                        input[i].prefDir = pd.xDir;
                    else
                        input[i].prefDir = pd.yDir;
                }

            /* Route */
            m_sort.route(input, out injected);

            //Console.WriteLine("---");
            for (int i = 0; i < 4; i++)
            {
                if (input[i] != null)
                {
                    //Console.WriteLine("input dir {0} pref dir {1} output dir {2} age {3}",
                    //        input[i].inDir, input[i].prefDir, i, Router_Flit_OldestFirst.age(input[i]));
                    input[i].Deflected = input[i].prefDir != i;
                    
                    /*if (Config.inputBuffer && input[i].orig_input != -1)
                        input[i] = null;*/
                }
            }
            
            /* Inject if the line is empty and there is something to inject */
            if (Config.calf_new_inj_ej)
            {
                if (inj != null && input[inj.prefDir] == null)
                {
                    input[inj.prefDir] = inj;
                    injected = true;
                }
            }

            /* If something was injected, move items in the injection buffer. *
             *   Otherwise, take stats                                        */
            if (!injected)
            {
                if (m_injectSlot == null)
                    m_injectSlot = inj;
                else
                    m_injectSlot2 = inj;
            }
            else
                statsInjectFlit(inj);

            for (int i = 0; i < Config.ejectCount; i++)
            {
                if (eject[i] == null)
                    break;
                /* Stats for priority */
                if (eject[i] != null)
                {
                    if (eject[i].wasDeflected == true) Simulator.stats.prio_totalFlitsDeflected.Add();
                    Simulator.stats.prio_totalFlitsEj.Add();
                }
                /* End stats */


                /* Put ejected flit in reassembly buffer */
                if (eject[i] != null)
                    acceptFlit(eject[i]);
            }

            /* Links redirected inputs to outputs */
            for (int dir = 0; dir < 4; dir++)
                if (input[dir] != null)
                {
                    if (linkOut[dir] == null)
                        throw new Exception(String.Format("router {0} does not have link in dir {1}",
                                    coord, dir));
                    linkOut[dir].In = input[dir];
                }
        }

        public override bool canInjectFlit(Flit f)
        {
            return m_injectSlot == null;
        }

        public override void InjectFlit(Flit f)
        {
            if (m_injectSlot != null)
                throw new Exception("Trying to inject twice in one cycle");

            m_injectSlot = f;
        }

        public override void visitFlits(Flit.Visitor fv)
        {
            if (m_injectSlot != null)
                fv(m_injectSlot);
            if (m_injectSlot2 != null)
                fv(m_injectSlot2);
        }

        //protected abstract int rank(Flit f1, Flit f2);
    }

    public class Router_SortNet_GP : Router_SortNet
    {
        public Router_SortNet_GP(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_GP._rank(f1, f2);
        }
    }

    public class Router_SortNet_OldestFirst : Router_SortNet
    {
        public Router_SortNet_OldestFirst(Coord myCoord)
            : base(myCoord)
        {
        }

        public override int rank(Flit f1, Flit f2)
        {
            return Router_Flit_OldestFirst._rank(f1, f2);
        }
    }
}
