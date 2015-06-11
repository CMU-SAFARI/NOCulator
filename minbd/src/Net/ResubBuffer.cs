using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class ResubBuffer
    {
        List<Flit> buffer;
        int maxSize;
		public delegate int CompareFlits(Flit f1, Flit f2);
		bool tried;
		
        public ResubBuffer()
        {
            buffer  = new List<Flit>(Config.sizeOfRSBuffer);
            maxSize = Config.sizeOfRSBuffer;
        }
        
        public ResubBuffer(int size)
        {
            buffer  = new List<Flit>(size);
            maxSize = size;
        }
        
        /* Outputs the next flit */
        public Flit getNextFlit()
        {

            int next = getNextFlitIndex();
            switch (next)
			{
                case -1:
                	throw new Exception("Releasing nothing, check that the buffer is empty before accessing it");
                default:
                    return buffer[next];
            }
        }
        
        /* Gives the next flit's index */
        public int getNextFlitIndex()
        {
            if (isEmpty())
                return -1;
			
            switch(Config.RSBuffer_sort)
            {
            	case "fifo": return 0;
            	
            	case "oldestFirst": 	return search(oldestFirst);
            	
            	case "mostDeflected": 	return search(mostDeflected);
            	
            	case "mostInRebuf": 	return search(mostInRebuf);
            	
            	case "highestPriority": return search(highestPriority);
            	
            	case "lowestPriority": 	return search(lowestPriority);
            	
            	default: throw new Exception("No priority given to resubBuffer output");
            }
            /*if (Config.RSBuffer_fifo)
                return 0;
			
			else if (Config.RSBuffer_oldestFirst)
				return search(oldestFirst);
			
			else if (Config.RSBuffer_mostDeflected)
				return search(mostDeflected);
            
			else if (Config.RSBuffer_mostInRebuf)
				return search(mostInRebuf);
				
			else if (Config.RSBuffer_highestPriority)
				return search(highestPriority);
			
			else
                throw new Exception("No priority given to resubBuffer output");*/
        }
		
		private int oldestFirst(Flit f1, Flit f2)
		{
			if(f1.injectionTime > f2.injectionTime)
				return -1;
			else if(f1.injectionTime < f2.injectionTime)
				return  1;
			else
				return (Config.RSBuffer_randomVariant) ? (Simulator.rand.Next(0,3) - 1) : 0;
			
		}
		
		private int mostDeflected(Flit f1, Flit f2)
		{
			if(f1.nrOfDeflections > f2.nrOfDeflections)
				return -1;
			else if(f1.nrOfDeflections < f2.nrOfDeflections)
				return  1;
			else
				return (Config.RSBuffer_randomVariant) ? (Simulator.rand.Next(3) - 1) : 0;
			
		}
		
		private int mostInRebuf(Flit f1, Flit f2)
		{
			if(f1.nrInRebuf > f2.nrInRebuf)
				return -1;
			else if(f1.nrInRebuf < f2.nrInRebuf)
				return  1;
			else
				return (Config.RSBuffer_randomVariant) ? (Simulator.rand.Next(3) - 1) : 0;
			
		}

		private int highestPriority(Flit f1, Flit f2)
		{
			if(f1.priority > f2.priority)
				return -1;
			else if(f1.priority < f2.priority)
				return  1;
			else
				return (Config.RSBuffer_randomVariant) ? (Simulator.rand.Next(3) - 1) : 0;
			
		}
		
		private int lowestPriority(Flit f1, Flit f2)
		{
			if(f1.priority < f2.priority)
				return -1;
			else if(f1.priority > f2.priority)
				return  1;
			else
				return (Config.RSBuffer_randomVariant) ? (Simulator.rand.Next(3) - 1) : 0;
			
		}
		
		/* Searches for an element in the buffer using the given scheme */
		private int search(CompareFlits compare)
		{
			int win = 0;
			if (buffer.Count == 0) return -1;
			
			for(int i = 1; i < buffer.Count; i++)
				if(0 < compare(buffer[win], buffer[i]))
					win = i;
					
			return win;
		}
        
        /* Adds a flit to the buffer */
        public void addFlit(Flit f)
        {
        	if(f == null)
        		throw new Exception("Adding a null flit to the resubmit buffer");
        		
            if(isFull())
                throw new Exception("Can't add a flit if the buffer is full.");
            else
                buffer.Add(f);
        }

        /* Removes the flit from the buffer depending on the scheme */
        public Flit removeFlit()
        {
            int next = getNextFlitIndex();
            Flit ret;
            
            switch (next)
			{
                case -1:
                    throw new Exception("Can't remove a flit with an empty buffer.");
                default:
                    ret = buffer[next];
                    buffer.RemoveAt(next);
                    if (ret == null)
                        throw new Exception("Null flit removed from buffer");
                    ret.wasInRebuf = true;
                    return ret;
            }
        }

        public bool isEmpty()
        {
            return (buffer.Count == 0);
        }

        /* If the buffer is infinite, it always returns false */
        public bool isFull()
        {
            if (Config.isInfiniteRSBuffer) 
				return false;
            else if (buffer.Count == maxSize) 
				return true;
			else if (buffer.Count > maxSize)
				throw new Exception("There are more flits in the resubmit buffer than there is space for!");
            else 
				return false;
        }

		public Flit getFlit(int i)
		{
			if (i < 0 || i > buffer.Count - 1)
				throw new Exception("Attempting to remove from an empty or non existant buffer slot");
			
			return buffer[i];
		}

        public int count()
        {
        	if (buffer.Count < 0 || (buffer.Count > maxSize && !Config.isInfiniteRSBuffer))
        		throw new Exception("Illegal amount of flits in the resubmit buffer");
        		
            return buffer.Count;
        }

        public int getMaxSize()
        {
            return maxSize;
        }

        public void printBuffer()
        {
            Console.WriteLine("Cycle: {0}----------------------\n", Simulator.CurrentRound);
            for(int i = 0; i < buffer.Count; i++)
            {
                if(buffer[i] == null)
                    break;
                else
                    Console.WriteLine("{0}.{1} \n", buffer[i].packet.ID, buffer[i].flitNr);
            }
            Console.WriteLine("=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=\n");
        }
        
        public bool triedFlit()
        {
            return tried;
        }

        public void tryFlit()
        {
            tried = true;
        }
        public void clearTry()
        {
            tried = false;
        }

        public void insertFlit(Flit f, int i)
        {
            buffer.Insert(i,f);
        }
    }
}
