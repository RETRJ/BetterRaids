using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace BetterRaids
{
    internal static class RaidPawnLimiter
    {
        public static RaidPawnLimitReport Apply(List<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0)
            {
                return RaidPawnLimitReport.Empty;
            }

            BetterRaidsSettings settings = BetterRaidsMod.Settings;
            int cap = settings != null ? settings.RaiderCap : BetterRaidsSettings.DefaultRaiderCap;
            cap = Math.Max(BetterRaidsSettings.MinRaiderCap, Math.Min(BetterRaidsSettings.MaxRaiderCap, cap));
            int originalCount = pawns.Count;
            float originalCombatPower = SumBasePawnCost(pawns);

            if (pawns.Count <= cap)
            {
                return new RaidPawnLimitReport(originalCount, pawns.Count, originalCombatPower, originalCombatPower);
            }

            List<Pawn> kept = pawns
                .Select((pawn, index) => new PawnWithIndex(pawn, index))
                .OrderByDescending(item => GetBasePawnCost(item.Pawn))
                .ThenBy(item => item.Index)
                .Take(cap)
                .Select(item => item.Pawn)
                .ToList();

            float keptCombatPower = SumBasePawnCost(kept);

            pawns.Clear();
            pawns.AddRange(kept);

            LogCapApplied(originalCount, pawns.Count, originalCombatPower, keptCombatPower, pawns);
            return new RaidPawnLimitReport(originalCount, pawns.Count, originalCombatPower, keptCombatPower);
        }

        private static void LogCapApplied(int originalCount, int keptCount, float originalCombatPower, float keptCombatPower, List<Pawn> pawns)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[BetterRaids] Raider cap applied");
            builder.AppendLine("  originalPawnCount=" + originalCount);
            builder.AppendLine("  keptPawnCount=" + keptCount);
            builder.AppendLine("  removedPawnCount=" + Math.Max(0, originalCount - keptCount));
            builder.AppendLine("  originalCombatPower=" + originalCombatPower.ToString("0.##"));
            builder.AppendLine("  keptCombatPower=" + keptCombatPower.ToString("0.##"));
            builder.AppendLine("  keptKinds=" + FormatKinds(pawns));
            Verse.Log.Message(builder.ToString());
        }

        private static string FormatKinds(List<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", pawns
                .GroupBy(pawn => pawn != null && pawn.kindDef != null ? pawn.kindDef.defName : "null")
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key + "x" + group.Count())
                .ToArray());
        }

        private static float GetBasePawnCost(Pawn pawn)
        {
            return pawn != null && pawn.kindDef != null ? pawn.kindDef.combatPower : 0f;
        }

        private static float SumBasePawnCost(List<Pawn> pawns)
        {
            float sum = 0f;
            if (pawns == null)
            {
                return sum;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                sum += GetBasePawnCost(pawns[i]);
            }

            return sum;
        }

        private sealed class PawnWithIndex
        {
            public readonly Pawn Pawn;
            public readonly int Index;

            public PawnWithIndex(Pawn pawn, int index)
            {
                Pawn = pawn;
                Index = index;
            }
        }
    }

    internal sealed class RaidPawnLimitReport
    {
        public static readonly RaidPawnLimitReport Empty = new RaidPawnLimitReport(0, 0, 0f, 0f);

        public readonly int OriginalPawnCount;
        public readonly int FinalPawnCount;
        public readonly float OriginalCombatPower;
        public readonly float FinalCombatPower;

        public RaidPawnLimitReport(int originalPawnCount, int finalPawnCount, float originalCombatPower, float finalCombatPower)
        {
            OriginalPawnCount = originalPawnCount;
            FinalPawnCount = finalPawnCount;
            OriginalCombatPower = originalCombatPower;
            FinalCombatPower = finalCombatPower;
        }
    }
}
