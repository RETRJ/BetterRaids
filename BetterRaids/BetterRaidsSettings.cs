using Verse;

namespace BetterRaids
{
    internal sealed class BetterRaidsSettings : ModSettings
    {
        public const int DefaultMaxFallbackTechTierDrop = 1;
        public const int MinFallbackTechTierDrop = 0;
        public const int MaxFallbackTechTierDropLimit = 6;
        public const int DefaultThreatScalePercent = 100;
        public const int MinThreatScalePercent = 0;
        public const int MaxThreatScalePercent = 1000;
        public const int DefaultRaiderCap = 50;
        public const int MinRaiderCap = 20;
        public const int MaxRaiderCap = 100;
        public const int DefaultMaxBrainImplants = 4;
        public const int DefaultMaxRibImplants = 4;
        public const int DefaultMaxTorsoImplants = 7;
        public const int MinBodyPartImplantCap = 0;
        public const int MaxBodyPartImplantCap = 20;

        public int MaxFallbackTechTierDrop = DefaultMaxFallbackTechTierDrop;
        public int ThreatScalePercent = DefaultThreatScalePercent;
        public int RaiderCap = DefaultRaiderCap;
        public int MaxBrainImplants = DefaultMaxBrainImplants;
        public int MaxRibImplants = DefaultMaxRibImplants;
        public int MaxTorsoImplants = DefaultMaxTorsoImplants;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref MaxFallbackTechTierDrop, "maxFallbackTechTierDrop", DefaultMaxFallbackTechTierDrop);
            Scribe_Values.Look(ref ThreatScalePercent, "threatScalePercent", DefaultThreatScalePercent);
            Scribe_Values.Look(ref RaiderCap, "raiderCap", DefaultRaiderCap);
            Scribe_Values.Look(ref MaxBrainImplants, "maxBrainImplants", DefaultMaxBrainImplants);
            Scribe_Values.Look(ref MaxRibImplants, "maxRibImplants", DefaultMaxRibImplants);
            Scribe_Values.Look(ref MaxTorsoImplants, "maxTorsoImplants", DefaultMaxTorsoImplants);
            ClampValues();
        }

        public float ThreatScaleFactor
        {
            get { return ThreatScalePercent / 100f; }
        }

        public void ClampValues()
        {
            if (MaxFallbackTechTierDrop < MinFallbackTechTierDrop)
            {
                MaxFallbackTechTierDrop = MinFallbackTechTierDrop;
            }

            if (MaxFallbackTechTierDrop > MaxFallbackTechTierDropLimit)
            {
                MaxFallbackTechTierDrop = MaxFallbackTechTierDropLimit;
            }

            if (ThreatScalePercent < MinThreatScalePercent)
            {
                ThreatScalePercent = MinThreatScalePercent;
            }

            if (ThreatScalePercent > MaxThreatScalePercent)
            {
                ThreatScalePercent = MaxThreatScalePercent;
            }

            if (RaiderCap < MinRaiderCap)
            {
                RaiderCap = MinRaiderCap;
            }

            if (RaiderCap > MaxRaiderCap)
            {
                RaiderCap = MaxRaiderCap;
            }

            MaxBrainImplants = ClampImplantCap(MaxBrainImplants);
            MaxRibImplants = ClampImplantCap(MaxRibImplants);
            MaxTorsoImplants = ClampImplantCap(MaxTorsoImplants);
        }

        private static int ClampImplantCap(int value)
        {
            if (value < MinBodyPartImplantCap)
            {
                return MinBodyPartImplantCap;
            }

            if (value > MaxBodyPartImplantCap)
            {
                return MaxBodyPartImplantCap;
            }

            return value;
        }
    }
}
