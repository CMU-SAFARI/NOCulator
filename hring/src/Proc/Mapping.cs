using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    public abstract class NodeMapping
    {
        public Coord getCoord(int ID)
        {
            Coord c = new Coord(ID);
            return c;
        }

        public abstract bool hasCPU(int id); // has CPU and coherent cache?
        public abstract bool hasSh(int id);  // has shared cache?
        public abstract bool hasMem(int id); // has memory controller?
    }

    public class NodeMapping_AllCPU_SharedCache : NodeMapping
    {
        public override bool hasCPU(int id) { return true; }
        public override bool hasSh(int id) { return true; }
        public override bool hasMem(int id)
        {
            foreach (Coord c in Config.memory.MCLocations)
                if (c.ID == id)
                    return true;
            return false;
        }
    }
}
