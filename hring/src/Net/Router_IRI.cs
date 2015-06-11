
//#define debug

using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
	public class Router_IRI : Router
	{
		Queue<Flit>[] gringBuffer = new Queue<Flit>[2];
		Queue<Flit>[] lringBuffer = new Queue<Flit>[2];
		Queue<Flit>[] g2lBuffer = new Queue<Flit>[2];
		Queue<Flit>[] l2gBuffer = new Queue<Flit>[2];
		int [] lcounter = new int[4];
		int [] gcounter = new int[4];
		bool [] ginjectReserve = new bool [2];
		bool [] linjectReserve = new bool [2];
		bool [] gringReserve = new bool [2];
		bool [] lringReserve = new bool [2];
		int depth = Config.HRBufferDepth;
		public Router_IRI(int ID) : base()
		{
			coord.ID = ID;
			for (int n = 0; n < 2; n ++)
			{
				gringBuffer[n] = new Queue<Flit>();
				lringBuffer[n] = new Queue<Flit>();
				g2lBuffer[n] = new Queue<Flit>();
				l2gBuffer[n] = new Queue<Flit>();
			}
			LLinkIn = new Link[2];
			GLinkIn = new Link[2];
			LLinkOut = new Link[2];
			GLinkOut = new Link[2];
			for (int n = 0; n < 4; n++)
			{
				lcounter[n] = depth;
				gcounter[n] = depth;
			}
		}
		protected override void _doStep()
		{						
			// update credit counters
			for (int n = 0; n < 2; n++)
			{
				if (GLinkOut[0].SideBandOut[n] != -1)
					gcounter[GLinkOut[0].SideBandOut[n]] ++;
				if (LLinkOut[0].SideBandOut[n] != -1)
					lcounter[LLinkOut[0].SideBandOut[n]] ++;
			}
			
			// input flits into buffers
			if (GLinkIn[0].Out != null)
			{
				int m_class = GLinkIn[0].Out.virtualChannel;
				if (m_class >= 2) // enter the g2lbuffer
				{
					g2lBuffer[m_class % 2].Enqueue(GLinkIn[0].Out);
					if (g2lBuffer[m_class % 2].Count > depth)
						throw new Exception("g2lBuffer overflow!!");
				}
				else 
				{
					gringBuffer[m_class].Enqueue(GLinkIn[0].Out);
					if (gringBuffer[m_class].Count > depth)
						throw new Exception("gringBuffer overflow!!");
				}
				GLinkIn[0].Out = null;
			}
			if (LLinkIn[0].Out != null)
			{
				int m_class = LLinkIn[0].Out.virtualChannel;
				if (m_class >= 2)
				{
					l2gBuffer[m_class % 2].Enqueue(LLinkIn[0].Out);
					if (l2gBuffer[m_class % 2].Count > depth)
						throw new Exception("l2gBuffer overflow!!");
				}
				else
				{
					lringBuffer[m_class].Enqueue(LLinkIn[0].Out);
					if (lringBuffer[m_class].Count > depth)
						throw new Exception("lringBuffer overflow!!");
				}
			}						
			// injection: buffer to output ports
			for (int vc = 0 ; vc < 2; vc ++)
			{
				Flit fgring = null, fginj = null, flring = null, flinj = null;
				if (gringBuffer[vc].Count > 0)
					fgring = gringBuffer[vc].Peek();
				if (l2gBuffer[vc].Count > 0)
					fginj = l2gBuffer[vc].Peek();
				if (lringBuffer[vc].Count > 0)
					flring = lringBuffer[vc].Peek();
				if (g2lBuffer[vc].Count > 0)
					flinj = g2lBuffer[vc].Peek();
		/*		if (fgring != null && fginj != null && 
					fgring.isHeadFlit == false && fginj.isHeadFlit == false ||
					flring != null && flinj != null &&
					flring.isHeadFlit == false && flinj.isHeadFlit == false)
					throw new Exception("two packets interleaved in the channel!!");*/
				
				// inject to global ring
				int ggrantVC = -1;
				if (ginjectReserve[vc])
					ggrantVC = 0;
				else if (gringReserve[vc])
					ggrantVC = 1;
				else if (fgring != null && gcounter[getClass(fgring)] > 0)
					ggrantVC = 1;
				else if (fginj != null && gcounter[getClass(fginj)] > 0)
					ggrantVC = 0;

				if (ggrantVC == 0 && GLinkOut[0].In == null && fginj != null && gcounter[getClass(fginj)] > 0)
				{
					GLinkOut[0].In = l2gBuffer[vc].Dequeue();
					GLinkOut[0].In.virtualChannel = getClass(fginj);
					gcounter[getClass(fginj)] --;
					if (fginj.isTailFlit)
						ginjectReserve[vc] = false; 
					else
						ginjectReserve[vc] = true;
					LLinkIn[0].SideBandIn[1] = vc+2;
				}
				else if (ggrantVC == 1 && GLinkOut[0].In == null && fgring != null && gcounter[getClass(fgring)] > 0)
				{
					GLinkOut[0].In = gringBuffer[vc].Dequeue();
					GLinkOut[0].In.virtualChannel = getClass(fgring);
					gcounter[getClass(fgring)] --;
					if (fgring.isTailFlit)
						gringReserve[vc] = false;
					else 
						gringReserve[vc] = true;
					GLinkIn[0].SideBandIn[0] = vc;
				}
				// inject to local ring
				int lgrantVC = -1;
				if (linjectReserve[vc])
					lgrantVC = 0;
				else if (lringReserve[vc])
					lgrantVC = 1;
				else if (flring != null && lcounter[getClass(flring)] > 0)
					lgrantVC = 1;
				else if (flinj != null && lcounter[getClass(flinj)] > 0)
					lgrantVC = 0;

				if (lgrantVC == 0 && LLinkOut[0].In == null && flinj != null && lcounter[getClass(flinj)] > 0)
				{
					LLinkOut[0].In = g2lBuffer[vc].Dequeue();
					LLinkOut[0].In.virtualChannel = getClass(flinj);
					lcounter[getClass(flinj)] --;
					if (flinj.isTailFlit)
						linjectReserve[vc] = false; 
					else
						linjectReserve[vc] = true;
					GLinkIn[0].SideBandIn[1] = vc+2;
				}
				else if (lgrantVC == 1 && LLinkOut[0].In == null && flring != null && lcounter[getClass(flring)] > 0)
				{
					LLinkOut[0].In = lringBuffer[vc].Dequeue();
					LLinkOut[0].In.virtualChannel = getClass(flring);
					lcounter[getClass(flring)] --;
					if (flring.isTailFlit)
						lringReserve[vc] = false;
					else 
						lringReserve[vc] = true;
					LLinkIn[0].SideBandIn[0] = vc;
				}
			}
#if debug
//			if (ID == 0)
			{
				Console.WriteLine("bridge ID = {0}", ID);
				for (int n = 0; n < 4; n++)
					Console.WriteLine("gcounter[{0}] : {1}", n, gcounter[n]);
				for (int n = 0; n < 2; n ++)
				{
					Console.WriteLine("gringBuffer[{0}] depth = {1}", n, gringBuffer[n].Count);
					Console.WriteLine("l2gBuffer[{0}] depth = {1}", n, l2gBuffer[n].Count);
					Console.WriteLine("ginjectReserve[{0}] = {1}", n, ginjectReserve[n]);
					Console.WriteLine("gringReserve[{0}] = {1}", n, gringReserve[n]);
				}
				Console.ReadKey(true);
			}
#endif
		}
		
		public int getClass(Flit f)
		{
			bool gotoGlobalRing;
			if (f.packet.dest.ID / 4 != ID)
				gotoGlobalRing = true;
			else 
				gotoGlobalRing = false;
			if (gotoGlobalRing)
			{
				if (f.packet.dest.ID / 4 > ID && f.packet.dest.ID / 4 - ID > 1)
					return 1;
				else if (f.packet.dest.ID / 4 > ID && f.packet.dest.ID / 4 - ID == 1)
					return 2;
				else if (f.packet.dest.ID / 4 < ID && (f.packet.dest.ID / 4 + 4 - ID) % 4 > 1)
					return 0;
				else if (f.packet.dest.ID / 4 < ID && (f.packet.dest.ID / 4 + 4 - ID) % 4 == 1)
					return 2;
				else 
				{
					Console.WriteLine("destID = {0}, ID = {1}", f.packet.dest.ID, ID);
					throw new Exception("unknown class!!");
				}
			}
			else //gotoLocalRing. IRI has the highest numbering. So always go on the lower virtual channel
				return 0;
		}
		public override bool canInjectFlit(Flit f)
        {
        	return false;
        }
        public override void InjectFlit(Flit f)
        {
        	return;
        }
	}
}

