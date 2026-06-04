using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterRaids
{
    internal class Recipe_DisableProtocolWithJailbreaker : Recipe_Surgery
    {
        private const string AcidProtocolDefName = "BetterRaids_AcidProtocol";
        private const string ExplosiveProtocolDefName = "BetterRaids_ExplosiveProtocol";

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            BodyPartRecord torso = FindBodyPart(pawn, "Torso");
            if (torso != null && HasAnyProtocol(pawn))
            {
                yield return torso;
            }
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            return pawn != null && HasAnyProtocol(pawn) && base.AvailableOnNow(thing, part);
        }

        public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part)
        {
            string label = base.GetLabelWhenUsedOn(pawn, part);
            if (pawn == null || !HasAnyProtocol(pawn))
            {
                return label;
            }

            JailbreakerTier tier = GetTier(recipe);
            float protectedValue = EstimateProtectedPawnValue(pawn);
            float chance = CalculateSuccessChance(tier, protectedValue, FindBestColonistDoctor());
            if (tier == JailbreakerTier.Archotech)
            {
                return label + " (100%, value " + protectedValue.ToString("0") + ")";
            }

            return label + " (" + (chance * 100f).ToString("0.0") + "%, value " + protectedValue.ToString("0") + ")";
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            if (!HasAnyProtocol(pawn))
            {
                Messages.Message("[BetterRaids] No armed kill-switch protocol found.", new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            JailbreakerTier tier = GetTier(recipe);
            float protectedValue = EstimateProtectedPawnValue(pawn);
            float chance = CalculateSuccessChance(tier, protectedValue, billDoer);
            bool success = tier == JailbreakerTier.Archotech || Rand.Chance(chance);

            if (success)
            {
                int removed = RemoveProtocols(pawn);
                Messages.Message(
                    "[BetterRaids] Kill-switch protocol disabled"
                    + FormatChanceSuffix(tier, chance, protectedValue)
                    + ".",
                    new LookTargets(pawn),
                    MessageTypeDefOf.PositiveEvent,
                    true);

                Log.Message("[BetterRaids] Jailbreaker success"
                    + "\n  pawn=" + SafePawnLabel(pawn)
                    + "\n  tier=" + tier
                    + "\n  protectedValue=" + protectedValue.ToString("0")
                    + "\n  chance=" + (chance * 100f).ToString("0.0") + "%"
                    + "\n  protocolsRemoved=" + removed);
                return;
            }

            string failureText = "[BetterRaids] Jailbreaker failed"
                + FormatChanceSuffix(tier, chance, protectedValue)
                + ". Kill-switch protocol activated.";
            Find.LetterStack.ReceiveLetter(
                "Jailbreaker failed",
                failureText,
                LetterDefOf.NegativeEvent,
                new LookTargets(pawn));

            Log.Message("[BetterRaids] Jailbreaker failure"
                + "\n  pawn=" + SafePawnLabel(pawn)
                + "\n  tier=" + tier
                + "\n  protectedValue=" + protectedValue.ToString("0")
                + "\n  chance=" + (chance * 100f).ToString("0.0") + "%");

            ProtocolActivationPatch.ActivateProtocol(pawn);
        }

        private static string FormatChanceSuffix(JailbreakerTier tier, float chance, float protectedValue)
        {
            if (tier == JailbreakerTier.Archotech)
            {
                return " (archotech bypass, protected value " + protectedValue.ToString("0") + ")";
            }

            return " (chance " + (chance * 100f).ToString("0.0") + "%, protected value " + protectedValue.ToString("0") + ")";
        }

        private static JailbreakerTier GetTier(RecipeDef recipeDef)
        {
            string defName = recipeDef != null ? recipeDef.defName : string.Empty;
            if (defName == "BetterRaids_DisableProtocolCrude")
            {
                return JailbreakerTier.Crude;
            }

            if (defName == "BetterRaids_DisableProtocolMilitary")
            {
                return JailbreakerTier.Military;
            }

            if (defName == "BetterRaids_DisableProtocolAI")
            {
                return JailbreakerTier.AI;
            }

            return JailbreakerTier.Archotech;
        }

        private static float CalculateSuccessChance(JailbreakerTier tier, float protectedValue, Pawn billDoer)
        {
            if (tier == JailbreakerTier.Archotech)
            {
                return 1f;
            }

            float deviceValue;
            float baseCap;
            float maxCap;
            switch (tier)
            {
                case JailbreakerTier.Crude:
                    deviceValue = 410f;
                    baseCap = 0.10f;
                    maxCap = 0.15f;
                    break;
                case JailbreakerTier.Military:
                    deviceValue = 2020f;
                    baseCap = 0.35f;
                    maxCap = 0.45f;
                    break;
                default:
                    deviceValue = 5350f;
                    baseCap = 0.80f;
                    maxCap = 0.90f;
                    break;
            }

            float intellectual = 0f;
            if (billDoer != null && billDoer.skills != null)
            {
                intellectual = billDoer.skills.GetSkill(SkillDefOf.Intellectual).Level;
            }

            float skillFactor = Clamp01((intellectual - 10f) / 10f);
            float cap = baseCap + (maxCap - baseCap) * skillFactor;
            float value = Math.Max(1f, protectedValue);
            float valueMultiplier = 0.15f + 0.35f * (1f - (float)Math.Exp(-value / 50000f));
            float rawChance = deviceValue * (1f + valueMultiplier) / value;
            return Clamp01(SoftMin(rawChance, cap, 4f));
        }

        private static float SoftMin(float a, float b, float q)
        {
            double numerator = a * b;
            double denominator = Math.Pow(Math.Pow(a, q) + Math.Pow(b, q), 1.0 / q);
            if (denominator <= 0.0)
            {
                return 0f;
            }

            return (float)(numerator / denominator);
        }

        private static float EstimateProtectedPawnValue(Pawn pawn)
        {
            float value = 0f;
            if (pawn.kindDef != null)
            {
                value += Math.Max(0f, pawn.kindDef.combatPower) * 10f;
            }

            if (pawn.health != null && pawn.health.hediffSet != null)
            {
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    HediffDef hediffDef = hediffs[i].def;
                    if (hediffDef == null || !IsImplantLike(hediffDef))
                    {
                        continue;
                    }

                    ThingDef thingDef = hediffDef.spawnThingOnRemoved ?? DefDatabase<ThingDef>.GetNamedSilentFail(hediffDef.defName);
                    if (thingDef != null)
                    {
                        value += Math.Max(0f, thingDef.BaseMarketValue);
                    }
                    else
                    {
                        value += 250f;
                    }
                }
            }

            return Math.Max(1000f, value);
        }

        private static bool IsImplantLike(HediffDef hediffDef)
        {
            if (hediffDef == null)
            {
                return false;
            }

            if (hediffDef.defName == AcidProtocolDefName || hediffDef.defName == ExplosiveProtocolDefName)
            {
                return false;
            }

            if (hediffDef.countsAsAddedPartOrImplant || hediffDef.addedPartProps != null)
            {
                return true;
            }

            Type hediffClass = hediffDef.hediffClass;
            return hediffClass != null
                && (typeof(Hediff_Implant).IsAssignableFrom(hediffClass)
                    || typeof(Hediff_AddedPart).IsAssignableFrom(hediffClass));
        }

        private static int RemoveProtocols(Pawn pawn)
        {
            int removed = 0;
            removed += RemoveAllHediffs(pawn, AcidProtocolDefName);
            removed += RemoveAllHediffs(pawn, ExplosiveProtocolDefName);
            return removed;
        }

        private static int RemoveAllHediffs(Pawn pawn, string hediffDefName)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || hediffDef == null)
            {
                return 0;
            }

            int removed = 0;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff hediff = hediffs[i];
                if (hediff != null && hediff.def == hediffDef)
                {
                    pawn.health.RemoveHediff(hediff);
                    removed++;
                }
            }

            return removed;
        }

        private static bool HasAnyProtocol(Pawn pawn)
        {
            return HasHediff(pawn, AcidProtocolDefName) || HasHediff(pawn, ExplosiveProtocolDefName);
        }

        private static bool HasHediff(Pawn pawn, string hediffDefName)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            return pawn != null && pawn.health != null && pawn.health.hediffSet != null && hediffDef != null && pawn.health.hediffSet.HasHediff(hediffDef);
        }

        private static BodyPartRecord FindBodyPart(Pawn pawn, string bodyPartDefName)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
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

        private static Pawn FindBestColonistDoctor()
        {
            Pawn bestPawn = null;
            int bestScore = -1;
            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return null;
            }

            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                if (map == null || map.mapPawns == null)
                {
                    continue;
                }

                List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
                for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                {
                    Pawn pawn = pawns[pawnIndex];
                    if (pawn == null || pawn.Dead || pawn.Downed || pawn.skills == null || pawn.WorkTagIsDisabled(WorkTags.Caring))
                    {
                        continue;
                    }

                    int medical = pawn.skills.GetSkill(SkillDefOf.Medicine).Level;
                    int intellectual = pawn.skills.GetSkill(SkillDefOf.Intellectual).Level;
                    int score = medical * 100 + intellectual;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPawn = pawn;
                    }
                }
            }

            return bestPawn;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        private static string SafePawnLabel(Pawn pawn)
        {
            return pawn != null ? pawn.LabelShortCap : "unknown pawn";
        }

        private enum JailbreakerTier
        {
            Crude,
            Military,
            AI,
            Archotech
        }
    }
}
