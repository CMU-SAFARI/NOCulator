namespace ICSimulator
{
    public class STC_DORouter : CentralPrioritizedDORouter
    {
        protected override bool _isHigherPriority(Flit a, Flit b)
        {
            if (a.packet.batchID != b.packet.batchID)
                return a.packet.batchID < b.packet.batchID;

            if (a.packet.request != null && b.packet.request != null)
                if (CC.priorities[a.packet.request.requesterID] != CC.priorities[b.packet.request.requesterID])
                    return CC.priorities[a.packet.request.requesterID] > CC.priorities[b.packet.request.requesterID];
            return a.packet.creationTime < b.packet.creationTime;
        }
        public STC_DORouter(Coord myCoord)
            : base(myCoord) { }
    }
}