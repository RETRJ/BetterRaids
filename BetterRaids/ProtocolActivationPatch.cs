using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRaids
{
    [HarmonyPatch]
    internal static class ProtocolActivationPatch
    {
        private const string AcidProtocolDefName = "BetterRaids_AcidProtocol";
        private const string ExplosiveProtocolDefName = "BetterRaids_ExplosiveProtocol";
        private const string DeathAcidifierDefName = "DeathAcidifier";
        private const string EliteMarkerDefName = "BetterRaids_EliteMarker";
        private const string CommanderMarkerDefName = "BetterRaids_CommanderMarker";
        private const string BionicModuleBaseTag = "BM_BionicModuleBaseTag";

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types;
            try
            {
                types = typeof(RecipeWorker).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            Type[] parameters =
            {
                typeof(Pawn),
                typeof(BodyPartRecord),
                typeof(Pawn),
                typeof(List<Thing>),
                typeof(Bill)
            };

            HashSet<MethodBase> methods = new HashSet<MethodBase>();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null || type.IsAbstract || !typeof(RecipeWorker).IsAssignableFrom(type))
                {
                    continue;
                }

                MethodInfo method = AccessTools.Method(type, "ApplyOnPawn", parameters);
                if (method != null && methods.Add(method))
                {
                    yield return method;
                }
            }
        }

        private static bool Prefix(RecipeWorker __instance, Pawn pawn, BodyPartRecord part)
        {
            try
            {
                if (!ShouldActivateProtocol(__instance, pawn))
                {
                    return true;
                }

                ActivateProtocol(pawn);
                return false;
            }
            catch (Exception exception)
            {
                Log.Warning("[BetterRaids] Failed to evaluate implant removal protocol: " + exception);
                return true;
            }
        }

        private static bool ShouldActivateProtocol(RecipeWorker worker, Pawn pawn)
        {
            if (worker == null || worker.recipe == null || pawn == null || pawn.Dead || pawn.health == null || pawn.health.hediffSet == null)
            {
                return false;
            }

            HediffDef removedHediff = worker.recipe.removesHediff;
            if (!IsProtectedImplant(removedHediff))
            {
                return false;
            }

            return HasHediff(pawn, AcidProtocolDefName) || HasHediff(pawn, ExplosiveProtocolDefName);
        }

        internal static void ActivateProtocol(Pawn pawn)
        {
            bool explosive = HasHediff(pawn, ExplosiveProtocolDefName);
            string protocolName = explosive ? "Explosive Protocol" : "Acid Protocol";

            if (explosive)
            {
                if (pawn.Spawned && pawn.Map != null)
                {
                    GenExplosion.DoExplosion(pawn.Position, pawn.Map, 3f, DamageDefOf.Bomb, pawn, 30);
                }
            }
            else
            {
                EnsureDeathAcidifier(pawn);
            }

            if (!pawn.Dead)
            {
                pawn.Kill(null, null);
            }

            Messages.Message("[BetterRaids] " + protocolName + " activated: implant extraction attempt detected.",
                new LookTargets(pawn),
                explosive ? MessageTypeDefOf.ThreatBig : MessageTypeDefOf.NegativeHealthEvent,
                true);

            Log.Message("[BetterRaids] " + protocolName + " activated on " + SafePawnLabel(pawn) + " after protected implant removal attempt.");
        }

        private static void EnsureDeathAcidifier(Pawn pawn)
        {
            HediffDef deathAcidifierDef = DefDatabase<HediffDef>.GetNamedSilentFail(DeathAcidifierDefName);
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || deathAcidifierDef == null || HasHediff(pawn, deathAcidifierDef))
            {
                return;
            }

            BodyPartRecord torso = FindBodyPart(pawn, "Torso");
            if (torso != null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(deathAcidifierDef, pawn, torso), torso);
            }
        }

        private static bool IsProtectedImplant(HediffDef hediffDef)
        {
            if (hediffDef == null || IsBetterRaidsInternalHediff(hediffDef))
            {
                return false;
            }

            if (hediffDef.countsAsAddedPartOrImplant || hediffDef.addedPartProps != null || HasHediffTag(hediffDef, BionicModuleBaseTag))
            {
                return true;
            }

            Type hediffClass = hediffDef.hediffClass;
            return hediffClass != null
                && (typeof(Hediff_Implant).IsAssignableFrom(hediffClass)
                    || typeof(Hediff_AddedPart).IsAssignableFrom(hediffClass));
        }

        private static bool IsBetterRaidsInternalHediff(HediffDef hediffDef)
        {
            return hediffDef.defName == AcidProtocolDefName
                || hediffDef.defName == ExplosiveProtocolDefName
                || hediffDef.defName == EliteMarkerDefName
                || hediffDef.defName == CommanderMarkerDefName;
        }

        private static bool HasHediff(Pawn pawn, string hediffDefName)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            return hediffDef != null && pawn.health.hediffSet.HasHediff(hediffDef);
        }

        private static bool HasHediff(Pawn pawn, HediffDef hediffDef)
        {
            return pawn != null && pawn.health != null && pawn.health.hediffSet != null && hediffDef != null && pawn.health.hediffSet.HasHediff(hediffDef);
        }

        private static BodyPartRecord FindBodyPart(Pawn pawn, string bodyPartDefName)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || string.IsNullOrEmpty(bodyPartDefName))
            {
                return null;
            }

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part != null && part.def != null && part.def.defName == bodyPartDefName)
                {
                    return part;
                }
            }

            return null;
        }

        private static bool HasHediffTag(HediffDef hediffDef, string tag)
        {
            return hediffDef != null && hediffDef.tags != null && hediffDef.tags.Contains(tag);
        }

        private static string SafePawnLabel(Pawn pawn)
        {
            return pawn != null ? pawn.LabelShortCap : "unknown pawn";
        }
    }
}
