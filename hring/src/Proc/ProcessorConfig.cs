namespace ICSimulator
{
    public class ProcessorConfig : ConfigGroup
    {
        //public int bankCount = 1 << 3;
        public BitValuePair cacheAssocBV = new BitValuePair(4);
        public int cacheAssocBits { get { return cacheAssocBV.bits; } }
        public int cacheAssocSize { get { return (int)cacheAssocBV.val; } }

        public BitValuePair cacheSizeBV = new BitValuePair(17);
        public int cacheBits { get { return cacheSizeBV.bits; } }
        public int cacheSize { get { return (int)cacheSizeBV.val; } }

        public BitValuePair cacheBlockBV = new BitValuePair(5);
        public int cacheBlockBits { get { return cacheBlockBV.bits; } }
        public int cacheBlockSize { get { return (int)cacheBlockBV.val; } }

        public ulong cacheMissLatency = 200;
        public bool ignoreWritebacks = false;
        /*
        public double injectionRate_end = 0.05;
        public double injectionRate_inc = 0.0025;
        public double injectionRate_start = 0.05; //was 0.0025, but only want it to run once
        */
        public bool injectionByPacket = true;
        public int instWindowSize = 128;
        public int nrMSHRs = 128;
        public int instructionsPerCycle = 3;
        public bool isSharedCache = false;
        public bool isSharedMC = false;
        public int nrConsecCalcInst = 0;
        public int nrConsecMemInst = 0;
        public int nrOffsetMemInst = 0;
        public int secondChance = 0;
        public bool simulateL2CacheMisses = true;

        protected override bool setSpecialParameter(string param, string val) { return false; }
        public override void finalize() { }
    }
}
