using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ICSimulator
{
    public abstract class CentralPrioritizedDORouter : PrioritizedDORouter
    {
        protected static STC CC = new STC();

        public CentralPrioritizedDORouter(Coord myCoord)
            : base(myCoord) { }

        protected override void _doStep()
        {
            if (coord.x == Config.network_nrX / 2
                && coord.y == Config.network_nrY / 2
                && Simulator.CurrentRound % Config.STC_period == 0
                && Simulator.CurrentRound != 0)
            {
                CC.coordinate();

                //TODO: inject new packets
            }
            base._doStep();
        }
    }
}

