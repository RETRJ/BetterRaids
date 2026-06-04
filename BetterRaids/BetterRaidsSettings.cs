using Verse;

namespace BetterRaids
{
    internal sealed class BetterRaidsSettings : ModSettings
    {
        public const int DefaultMaxFallbackTechTierDrop = 2;
        public const int MinFallbackTechTierDrop = 0;
        public const int MaxFallbackTechTierDropLimit = 6;

        public int MaxFallbackTechTierDrop = DefaultMaxFallbackTechTierDrop;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref MaxFallbackTechTierDrop, "maxFallbackTechTierDrop", DefaultMaxFallbackTechTierDrop);
            ClampValues();
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
        }
    }
}
