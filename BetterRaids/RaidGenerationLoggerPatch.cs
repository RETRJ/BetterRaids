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
        private static void Postfix(PawnGroupMakerParms parms, ref IEnumerable<Pawn> __result)
        {
            if (parms == null || parms.groupKind != PawnGroupKindDefOf.Combat)
            {
                return;
            }

            __result = LogGeneratedPawns(__result, parms);
        }

        private static IEnumerable<Pawn> LogGeneratedPawns(IEnumerable<Pawn> result, PawnGroupMakerParms parms)
        {
            List<Pawn> pawns = new List<Pawn>();

            foreach (Pawn pawn in result)
            {
                pawns.Add(pawn);
            }

            EliteRaidPostProcessor.Process(parms, pawns);
            RaidGenerationLogger.Log(parms, pawns);

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                yield return pawn;
            }
        }
    }

    internal static class RaidGenerationLogger
    {
        public static void Log(PawnGroupMakerParms parms, List<Pawn> pawns)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("[BetterRaids] PawnGroupMakerUtility.GeneratePawns");
                builder.AppendLine("  faction=" + SafeFactionName(parms));
                builder.AppendLine("  factionDef=" + SafeFactionDefName(parms));
                builder.AppendLine("  techLevel=" + SafeTechLevel(parms));
                builder.AppendLine("  groupKind=" + SafeDefName(parms != null ? parms.groupKind : null));
                builder.AppendLine("  points=" + (parms != null ? parms.points.ToString("0.##") : "null"));
                builder.AppendLine("  pawnCount=" + pawns.Count);

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
        }

        public static string BuildRaidDebugText(IncidentParms parms)
        {
            string points = parms != null ? parms.points.ToString("0.#") : "null";
            return "[BetterRaids Debug]"
                + "\nFaction def: " + SafeIncidentFactionDefName(parms)
                + "\nFaction tech: " + SafeIncidentTechLevel(parms)
                + "\nRaid points: " + points;
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

                if (implantsOnly && !(hediff is Hediff_Implant) && !(hediff is Hediff_AddedPart))
                {
                    continue;
                }

                string partName = hediff.Part != null ? "@" + hediff.Part.def.defName : "";
                string hediffName = hediff.def != null ? hediff.def.defName : "null";
                names.Add(hediffName + partName);
            }

            return names.Count > 0 ? string.Join(", ", names.ToArray()) : "none";
        }
    }
}
