
//#define debug

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
	// for simplicity, the current version of buffered hierarchical ring has the following assumptions
	// 1. only 2 VCs supported
	// 2. VC1 always has higher priority than VC0
	// 	  Within a VC, ring buffers always has higher priority than injection buffers

	public class Router_NIC : Router
	{
		// each type of buffer can have multiple virtual channels
		Queue<Flit>[] injBuffer;
		Queue<Flit>[] ringBuffer;
		// for counter
		// 0: ringVC0, 1: ringVC1, 2:crossVC0, 3:crossVC1
		int[] counter = new int [4];

		const int VC_count = 2;
		bool [] injectionReserve = new bool [2];
		bool [] ringReserve = new bool [2];
		int depth;
		public Router_NIC(Coord myCoord) : base(myCoord)
		{
			injBuffer = new Queue<Flit> [VC_count];
			ringBuffer = new Queue<Flit> [VC_count];
			for (int i = 0 ; i < VC_count; i++)
			{	
				injBuffer[i] = new Queue<Flit>();
				ringBuffer[i] = new Queue<Flit>();
				injectionReserve[i] = false;
				ringReserve[i] = false;
			}
			for (int i = 0; i < 4; i++)
				counter[i] = Config.HRBufferDepth;
			depth = Config.HRBufferDepth;
		}

		protected void acceptFlit(Flit f)
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
            if (linkIn[0] != null && linkIn[0].Out != null &&
                linkIn[0].Out.dest.ID == ID)
            {
                ret = linkIn[0].Out;
            }
            if (ret != null) 
				linkIn[0].Out = null;
            return ret;
        }
		public override bool canInjectFlit(Flit f)
		{
			int m_class  = getClass(f) % 2;
			if (injBuffer[m_class].Count == depth)
				return false;
			else
				return true;
		}
		public override void InjectFlit(Flit f)
		{
			int m_class = getClass(f) % 2;
			injBuffer[m_class].Enqueue(f);
		    statsInjectFlit(f);
		}

		protected override void _doStep()
		{
			Flit f = linkIn[0].Out;

			// update credit counter
			for (int n = 0; n < 2; n++)
			{
				if (linkOut[0].SideBandOut[n] != -1)
					counter[linkOut[0].SideBandOut[n]] ++;	
			}	
/*			if (ID == 1)
			{
				Console.WriteLine("linkOut[0].SideBandOut[0] = {0}", linkOut[0].SideBandOut[0]);
				Console.WriteLine("Counter[1] : {0}", counter[1]);
			}*/
			//ejection
			Flit eject = ejectLocal();
			if (eject != null)
			{
				linkIn[0].SideBandIn[1] = eject.virtualChannel;
				acceptFlit(eject);
			}
			else if (f != null) // put into ring buffer
			{
				int m_class = f.virtualChannel;
				ringBuffer[m_class].Enqueue(f);
				if (ringBuffer[m_class].Count > depth)
					throw new Exception("Ring Buffer Overflow!!");
			}					
			// inject
			for (int vc = 0; vc < 2; vc++)
			{
				Flit fring = null, finj = null;
				if (ringBuffer[vc].Count > 0)
					fring = ringBuffer[vc].Peek();
				if (injBuffer[vc].Count > 0)
				{
					finj = injBuffer[vc].Peek();
			//		Console.WriteLine("finj.isTailFlit : {0}", finj.isTailFlit)	;
				}
				/*if (fring != null && finj != null && 
					fring.isHeadFlit == false && finj.isHeadFlit == false)
					throw new Exception("two packets interleaved in the channel!!");*/
				if (injectionReserve[vc] && ringReserve[vc])
					throw new Exception("only one buffer can reserve the VC.");
							
				int grantVC = -1; // 0 : injection, 1 : ringTransfer
				if (injectionReserve[vc])
					grantVC = 0;
				else if (ringReserve[vc])
					grantVC = 1;
				else if (fring != null && counter[getClass(fring)] > 0)
					grantVC = 1;
				else if (finj != null && counter[getClass(finj)] > 0)
					grantVC = 0;

				if (grantVC == 0 && linkOut[0].In == null && finj != null && counter[getClass(finj)] > 0)
				{
					linkOut[0].In = injBuffer[vc].Dequeue();
					linkOut[0].In.virtualChannel = getClass(linkOut[0].In);
					if (!linkOut[0].In.isTailFlit)
						injectionReserve[vc] = true;
					else
						injectionReserve[vc] = false;
					counter[getClass(linkOut[0].In)] --;
				}
				else if (grantVC == 1 && linkOut[0].In == null && fring != null && counter[getClass(fring)] > 0)
				{
					linkOut[0].In = ringBuffer[vc].Dequeue();
					linkOut[0].In.virtualChannel = getClass(linkOut[0].In);
					if (!linkOut[0].In.isTailFlit)
						ringReserve[vc] = true;
					else
						ringReserve[vc] = false;
					linkIn[0].SideBandIn[0] = vc;				
					counter[getClass(linkOut[0].In)] --;
				}
			}
#if debug
		//	if (ID == 1 || ID == 2)
			{
				Console.WriteLine("{0}", ID);
				for (int n = 0; n < 4 ; n++)
					Console.WriteLine("counter[{0}]: {1}", n, counter[n]);
				for (int n = 0; n < 2; n++)
				{
					Console.WriteLine("ringBuffer[{0}] : {1}", n, ringBuffer[n].Count);
					Console.WriteLine("injBuffer[{0}] : {1}", n, injBuffer[n].Count);
					Simulator.stats.avgBufferDepth.Add(injBuffer[n].Count);
					Simulator.stats.avgBufferDepth.Add(ringBuffer[n].Count);
				}
		//		Console.ReadKey(true);
			}
#endif
		}

		// return the next VC the flit wants to go.
		// 0 : ring buffer or injection buffer, VC0
		// 1 : ring buffer or injection buffer, VC1
		// 2 : cross buffer, VC0
		// 3 : cross buffer, VC1
		public int getClass(Flit f)
		{
			int curRouter = ID % 4;
			int destRouter = (f.packet.dest.ID / 4 == ID / 4) ? f.packet.dest.ID % 4 : 4;
			if (ID / 4 == f.packet.dest.ID / 4 || curRouter != 3)
			{
				if (destRouter > curRouter)
					return 1;
				else 
					return 0;
			}
			else
			{
				if (f.packet.dest.ID / 4 > ID / 4)
					return 3;
				else 
					return 2;
			}
		}
	}
}
