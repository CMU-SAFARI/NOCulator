namespace ICSimulator
{

    public class NoCLAS_DORouter : CentralPrioritizedDORouter
    {
        protected override bool _isHigherPriority(Flit a, Flit b)
        {
            ulong age_a = Simulator.CurrentRound - a.packet.creationTime;
            ulong age_b = Simulator.CurrentRound - b.packet.creationTime;

            int priors = 0;

            if (a.packet.request != null && b.packet.request != null)
                priors = CC.priorities[a.packet.request.requesterID].CompareTo(CC.priorities[b.packet.request.requesterID]);

            //Check if overthreshold exists
            if (age_a > Config.router.packetExpirationThreshold ||
                age_b > Config.router.packetExpirationThreshold)
            {
                if (a.packet.creationTime != b.packet.creationTime)
                    return a.packet.creationTime < b.packet.creationTime;
                return priors < 0;
            }

            if (priors != 0)
                return priors < 0;
            return a.packet.creationTime < b.packet.creationTime;
        }

        public NoCLAS_DORouter(Coord myCoord)
            : base(myCoord) { }
    }
}