using System;
using System.Collections.Generic;
using System.IO;

namespace ICSimulator
{
    class Controller_Neighbor_Local : Controller
    {  

        public override int mapCache(int node, ulong block)
        {
			// traffic kept in the local ring
			return (int)(block % 4) + 4 * (node / 4);
        }   
    }
}
