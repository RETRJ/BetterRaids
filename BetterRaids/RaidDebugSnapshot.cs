using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace BetterRaids
{
    internal sealed class RaidPointContext
    {
        public readonly float GroupMakerPointsBeforeBetterRaids;
        public readonly float GroupMakerPointsAfterBetterRaids;
        public readonly int ThreatScalePercent;

        public RaidPointContext(float groupMakerPointsBeforeBetterRaids, float groupMakerPointsAfterBetterRaids, int threatScalePercent)
        {
            GroupMakerPointsBeforeBetterRaids = groupMakerPointsBeforeBetterRaids;
            GroupMakerPointsAfterBetterRaids = groupMakerPointsAfterBetterRaids;
            ThreatScalePercent = threatScalePercent;
        }
    }

    internal sealed class RaidDebugSnapshot
    {
        public string FactionDefName;
        public string TechLevel;
        public float GroupMakerPointsBeforeBetterRaids;
        public float GroupMakerPointsAfterBetterRaids;
        public int ThreatScalePercent;
        public int OriginalPawnCount;
        public int FinalPawnCount;
        public float OriginalPawnKindCombatPower;
        public float FinalPawnKindCombatPower;
        public int EliteCount;
        public float EliteUpgradeSpent;

        public float FinalEstimatedRaidCost
        {
            get { return FinalPawnKindCombatPower + EliteUpgradeSpent; }
        }
    }

    internal static class RaidDebugSnapshotStore
    {
        private static RaidDebugSnapshot lastCombatRaid;

        public static void SetLastCombatRaid(RaidDebugSnapshot snapshot)
        {
            lastCombatRaid = snapshot;
        }

        public static RaidDebugSnapshot GetLastCombatRaidFor(IncidentParms parms)
        {
            if (lastCombatRaid == null || parms == null || parms.faction == null || parms.faction.def == null)
            {
                return lastCombatRaid;
            }

            return lastCombatRaid.FactionDefName == parms.faction.def.defName ? lastCombatRaid : null;
        }
    }

    internal static class RaidDebugFormatter
    {
        public static string BuildText(IncidentParms incidentParms, RaidDebugSnapshot snapshot)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[BetterRaids Debug]");
            builder.AppendLine("Incident letter points: " + FormatPoints(incidentParms != null ? (float?)incidentParms.points : null));
            builder.AppendLine("Faction def: " + SafeIncidentFactionDefName(incidentParms));
            builder.AppendLine("Faction tech: " + SafeIncidentTechLevel(incidentParms));

            if (snapshot == null)
            {
                builder.AppendLine("Group-maker points: not captured yet");
                return builder.ToString().TrimEndNewlines();
            }

            builder.AppendLine("Group-maker points before BetterRaids: " + FormatPoints(snapshot.GroupMakerPointsBeforeBetterRaids));
            builder.AppendLine("Group-maker points after BetterRaids: " + FormatPoints(snapshot.GroupMakerPointsAfterBetterRaids));
            builder.AppendLine("Threat scale: " + snapshot.ThreatScalePercent + "%");
            builder.AppendLine("Pawn count: " + snapshot.FinalPawnCount + " / original " + snapshot.OriginalPawnCount);
            builder.AppendLine("Pawn kind combat power: " + snapshot.FinalPawnKindCombatPower.ToString("0.##")
                + " / original " + snapshot.OriginalPawnKindCombatPower.ToString("0.##"));
            builder.AppendLine("Elite count: " + snapshot.EliteCount);
            builder.AppendLine("Elite upgrade points spent: " + snapshot.EliteUpgradeSpent.ToString("0.##"));
            builder.AppendLine("Final estimated raid cost: " + snapshot.FinalEstimatedRaidCost.ToString("0.##"));
            return builder.ToString().TrimEndNewlines();
        }

        private static string FormatPoints(float? points)
        {
            return points.HasValue ? points.Value.ToString("0.##") : "null";
        }

        private static string SafeIncidentFactionDefName(IncidentParms parms)
        {
            if (parms == null || parms.faction == null || parms.faction.def == null)
            {
                return "null";
            }

            return parms.faction.def.defName;
        }

        private static string SafeIncidentTechLevel(IncidentParms parms)
        {
            if (parms == null || parms.faction == null || parms.faction.def == null)
            {
                return "null";
            }

            return parms.faction.def.techLevel.ToString();
        }

        private static string TrimEndNewlines(this string value)
        {
            return value == null ? string.Empty : value.TrimEnd('\r', '\n');
        }
    }
}
