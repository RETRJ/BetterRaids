using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRaids
{
    [HarmonyPatch(typeof(PawnGroupMakerUtility))]
    [HarmonyPatch("GeneratePawns")]
    internal static class PawnGroupMakerUtilityGeneratePawnsPatch
    {
        private static void Prefix(PawnGroupMakerParms parms, out RaidPointContext __state)
        {
            __state = null;
            if (parms == null || parms.groupKind != PawnGroupKindDefOf.Combat)
            {
                return;
            }

            BetterRaidsSettings settings = BetterRaidsMod.Settings;
            int threatScalePercent = settings != null ? settings.ThreatScalePercent : BetterRaidsSettings.DefaultThreatScalePercent;
            float originalPoints = parms.points;

            if (settings == null || settings.ThreatScalePercent == BetterRaidsSettings.DefaultThreatScalePercent)
            {
                __state = new RaidPointContext(originalPoints, parms.points, threatScalePercent);
                return;
            }

            settings.ClampValues();
            parms.points = Math.Max(0f, parms.points * settings.ThreatScaleFactor);
            __state = new RaidPointContext(originalPoints, parms.points, settings.ThreatScalePercent);
        }

        private static void Postfix(PawnGroupMakerParms parms, ref IEnumerable<Pawn> __result, RaidPointContext __state)
        {
            if (parms == null || parms.groupKind != PawnGroupKindDefOf.Combat)
            {
                return;
            }

            __result = LogGeneratedPawns(__result, parms, __state);
        }

        private static IEnumerable<Pawn> LogGeneratedPawns(IEnumerable<Pawn> result, PawnGroupMakerParms parms, RaidPointContext pointContext)
        {
            List<Pawn> pawns = new List<Pawn>();

            foreach (Pawn pawn in result)
            {
                pawns.Add(pawn);
            }

            RaidPawnLimitReport limitReport = RaidPawnLimiter.Apply(pawns);
            EliteRaidUpgradeReport upgradeReport = EliteRaidPostProcessor.Process(parms, pawns);
            RaidDebugSnapshot snapshot = RaidGenerationLogger.Log(parms, pawns, pointContext, limitReport, upgradeReport);
            RaidDebugSnapshotStore.SetLastCombatRaid(snapshot);

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                yield return pawn;
            }
        }
    }

    internal static class RaidGenerationLogger
    {
        public static RaidDebugSnapshot Log(PawnGroupMakerParms parms, List<Pawn> pawns, RaidPointContext pointContext, RaidPawnLimitReport limitReport, EliteRaidUpgradeReport upgradeReport)
        {
            RaidDebugSnapshot snapshot = BuildSnapshot(parms, pawns, pointContext, limitReport, upgradeReport);

            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("[BetterRaids] PawnGroupMakerUtility.GeneratePawns");
                builder.AppendLine("  faction=" + SafeFactionName(parms));
                builder.AppendLine("  factionDef=" + SafeFactionDefName(parms));
                builder.AppendLine("  techLevel=" + SafeTechLevel(parms));
                builder.AppendLine("  groupKind=" + SafeDefName(parms != null ? parms.groupKind : null));
                builder.AppendLine("  points=" + (parms != null ? parms.points.ToString("0.##") : "null"));
                builder.AppendLine("  groupMakerPointsBeforeBetterRaids=" + snapshot.GroupMakerPointsBeforeBetterRaids.ToString("0.##"));
                builder.AppendLine("  groupMakerPointsAfterBetterRaids=" + snapshot.GroupMakerPointsAfterBetterRaids.ToString("0.##"));
                builder.AppendLine("  threatScalePercent=" + snapshot.ThreatScalePercent);
                builder.AppendLine("  pawnCount=" + pawns.Count);
                builder.AppendLine("  originalPawnCount=" + snapshot.OriginalPawnCount);
                builder.AppendLine("  pawnKindCombatPower=" + snapshot.FinalPawnKindCombatPower.ToString("0.##"));
                builder.AppendLine("  originalPawnKindCombatPower=" + snapshot.OriginalPawnKindCombatPower.ToString("0.##"));
                builder.AppendLine("  eliteUpgradeSpent=" + snapshot.EliteUpgradeSpent.ToString("0.##"));
                builder.AppendLine("  finalEstimatedRaidCost=" + snapshot.FinalEstimatedRaidCost.ToString("0.##"));

                for (int i = 0; i < pawns.Count; i++)
                {
                    AppendPawn(builder, pawns[i], i + 1);
                }

                Verse.Log.Message(builder.ToString());
            }
            catch (Exception exception)
            {
                Verse.Log.Warning("[BetterRaids] Failed to log generated pawns: " + exception);
            }

            return snapshot;
        }

        public static string BuildRaidDebugText(IncidentParms parms)
        {
            return RaidDebugFormatter.BuildText(parms, RaidDebugSnapshotStore.GetLastCombatRaidFor(parms));
        }

        private static RaidDebugSnapshot BuildSnapshot(PawnGroupMakerParms parms, List<Pawn> pawns, RaidPointContext pointContext, RaidPawnLimitReport limitReport, EliteRaidUpgradeReport upgradeReport)
        {
            return new RaidDebugSnapshot
            {
                FactionDefName = SafeFactionDefName(parms),
                TechLevel = SafeTechLevel(parms),
                GroupMakerPointsBeforeBetterRaids = pointContext != null ? pointContext.GroupMakerPointsBeforeBetterRaids : parms != null ? parms.points : 0f,
                GroupMakerPointsAfterBetterRaids = pointContext != null ? pointContext.GroupMakerPointsAfterBetterRaids : parms != null ? parms.points : 0f,
                ThreatScalePercent = pointContext != null ? pointContext.ThreatScalePercent : BetterRaidsSettings.DefaultThreatScalePercent,
                OriginalPawnCount = limitReport != null ? limitReport.OriginalPawnCount : pawns != null ? pawns.Count : 0,
                FinalPawnCount = pawns != null ? pawns.Count : 0,
                OriginalPawnKindCombatPower = limitReport != null ? limitReport.OriginalCombatPower : SumBasePawnCost(pawns),
                FinalPawnKindCombatPower = SumBasePawnCost(pawns),
                EliteCount = upgradeReport != null ? upgradeReport.EliteCount : 0,
                EliteUpgradeSpent = upgradeReport != null ? upgradeReport.TotalSpent : 0f
            };
        }

        private static void AppendPawn(StringBuilder builder, Pawn pawn, int index)
        {
            builder.AppendLine("  pawn #" + index);
            builder.AppendLine("    name=" + (pawn != null ? pawn.LabelShortCap : "null"));
            builder.AppendLine("    kind=" + SafeDefName(pawn != null ? pawn.kindDef : null));
            builder.AppendLine("    faction=" + SafePawnFactionName(pawn));
            builder.AppendLine("    equipment=" + JoinEquipment(pawn));
            builder.AppendLine("    apparel=" + JoinApparel(pawn));
            builder.AppendLine("    hediffs=" + JoinHediffs(pawn, false));
            builder.AppendLine("    implants=" + JoinHediffs(pawn, true));
        }

        private static string SafeFactionName(PawnGroupMakerParms parms)
        {
            if (parms == null || parms.faction == null)
            {
                return "null";
            }

            return parms.faction.Name;
        }

        private static string SafeFactionDefName(PawnGroupMakerParms parms)
        {
            if (parms == null || parms.faction == null || parms.faction.def == null)
            {
                return "null";
            }

            return parms.faction.def.defName;
        }

        private static string SafeTechLevel(PawnGroupMakerParms parms)
        {
            if (parms == null || parms.faction == null || parms.faction.def == null)
            {
                return "null";
            }

            return parms.faction.def.techLevel.ToString();
        }

        private static string SafePawnFactionName(Pawn pawn)
        {
            if (pawn == null || pawn.Faction == null || pawn.Faction.def == null)
            {
                return "null";
            }

            return pawn.Faction.def.defName;
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

        private static string SafeDefName(Def def)
        {
            return def != null ? def.defName : "null";
        }

        private static string JoinEquipment(Pawn pawn)
        {
            if (pawn == null || pawn.equipment == null || pawn.equipment.AllEquipmentListForReading == null || pawn.equipment.AllEquipmentListForReading.Count == 0)
            {
                return "none";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < pawn.equipment.AllEquipmentListForReading.Count; i++)
            {
                ThingWithComps equipment = pawn.equipment.AllEquipmentListForReading[i];
                names.Add(equipment != null && equipment.def != null ? equipment.def.defName : "null");
            }

            return string.Join(", ", names.ToArray());
        }

        private static string JoinApparel(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null || pawn.apparel.WornApparel == null || pawn.apparel.WornApparel.Count == 0)
            {
                return "none";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < pawn.apparel.WornApparel.Count; i++)
            {
                Apparel apparel = pawn.apparel.WornApparel[i];
                names.Add(apparel != null && apparel.def != null ? apparel.def.defName : "null");
            }

            return string.Join(", ", names.ToArray());
        }

        private static string JoinHediffs(Pawn pawn, bool implantsOnly)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null || pawn.health.hediffSet.hediffs.Count == 0)
            {
                return "none";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff == null)
                {
                    continue;
                }

                if (implantsOnly && !(hediff is Hediff_Implant) && !(hediff is Hediff_AddedPart) && !HasBionicModuleTag(hediff.def))
                {
                    continue;
                }

                string partName = hediff.Part != null ? "@" + hediff.Part.def.defName : "";
                string hediffName = hediff.def != null ? hediff.def.defName : "null";
                names.Add(hediffName + partName);
            }

            return names.Count > 0 ? string.Join(", ", names.ToArray()) : "none";
        }

        private static bool HasBionicModuleTag(HediffDef hediffDef)
        {
            return hediffDef != null && hediffDef.tags != null && hediffDef.tags.Contains("BM_BionicModuleBaseTag");
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
                Pawn pawn = pawns[i];
                sum += pawn != null && pawn.kindDef != null ? pawn.kindDef.combatPower : 0f;
            }

            return sum;
        }
    }
}
