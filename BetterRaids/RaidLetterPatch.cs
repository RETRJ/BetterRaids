using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRaids
{
    [HarmonyPatch(typeof(IncidentWorker), "SendStandardLetter")]
    [HarmonyPatch(new Type[]
    {
        typeof(TaggedString),
        typeof(TaggedString),
        typeof(LetterDef),
        typeof(IncidentParms),
        typeof(LookTargets),
        typeof(NamedArgument[])
    })]
    internal static class IncidentWorkerSendStandardLetterPatch
    {
        private static void Prefix(IncidentWorker __instance, IncidentParms parms, ref TaggedString baseLetterText)
        {
            if (!(__instance is IncidentWorker_Raid))
            {
                return;
            }

            string text = baseLetterText.ToString();
            baseLetterText = text + "\n\n" + RaidGenerationLogger.BuildRaidDebugText(parms);
        }
    }
}
