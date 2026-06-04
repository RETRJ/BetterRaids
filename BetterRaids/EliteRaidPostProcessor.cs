using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace BetterRaids
{
    internal static class EliteRaidPostProcessor
    {
        private const float ElitePawnRatio = 0.35f;
        private const float EliteBudgetRatio = 0.75f;
        private const float CommanderWeight = 3f;
        private const float NormalEliteWeight = 1f;
        private const float AcidProtocolCost = 10f;
        private const float ExplosiveProtocolCost = 25f;
        private const int MaxImplantsPerElite = 4;

        public static void Process(PawnGroupMakerParms parms, List<Pawn> pawns)
        {
            if (parms == null || pawns == null || pawns.Count == 0)
            {
                return;
            }

            try
            {
                List<Pawn> candidates = pawns
                    .Where(IsEligibleHumanlikeRaider)
                    .OrderByDescending(GetBasePawnCost)
                    .ToList();

                if (candidates.Count == 0)
                {
                    return;
                }

                int eliteCount = CalculateEliteCount(candidates.Count);
                if (eliteCount <= 0)
                {
                    return;
                }

                string techTierName = GetFactionTechTierName(parms);
                string factionScopeName = GetFactionScopeName(parms);
                List<ImplantCatalogLogger.ImplantUpgradeCandidate> implantCandidates =
                    ImplantCatalogLogger.GetEliteUpgradeCandidates(techTierName, factionScopeName);

                List<Pawn> selectedElites = candidates.Take(eliteCount).ToList();
                float raidPoints = Math.Max(0f, parms.points);
                float eliteBudget = raidPoints * EliteBudgetRatio;
                float totalWeight = CommanderWeight + Math.Max(0, selectedElites.Count - 1) * NormalEliteWeight;

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("[BetterRaids] Elite raid budget");
                builder.AppendLine("  factionDef=" + factionScopeName);
                builder.AppendLine("  techTier=" + techTierName);
                builder.AppendLine("  raidPoints=" + raidPoints.ToString("0.##"));
                builder.AppendLine("  elitePawnRatio=" + ElitePawnRatio.ToString("0.##"));
                builder.AppendLine("  eliteBudgetRatio=" + EliteBudgetRatio.ToString("0.##"));
                builder.AppendLine("  eligibleHumanlikePawns=" + candidates.Count);
                builder.AppendLine("  eliteCount=" + selectedElites.Count);
                builder.AppendLine("  eliteBudget=" + eliteBudget.ToString("0.##"));
                builder.AppendLine("  implantCandidates=" + implantCandidates.Count);

                for (int i = 0; i < selectedElites.Count; i++)
                {
                    Pawn pawn = selectedElites[i];
                    bool commander = i == 0;
                    float weight = commander ? CommanderWeight : NormalEliteWeight;
                    float assignedTotalCost = totalWeight > 0f ? eliteBudget * weight / totalWeight : 0f;
                    float basePawnCost = GetBasePawnCost(pawn);
                    float upgradeBudget = Math.Max(0f, assignedTotalCost - basePawnCost);

                    EliteUpgradeResult result = UpgradeElitePawn(pawn, commander, upgradeBudget, implantCandidates);
                    builder.AppendLine("  elite #" + (i + 1)
                        + " role=" + (commander ? "Commander" : "Elite")
                        + " pawn=" + SafePawnLabel(pawn)
                        + " kind=" + SafeDefName(pawn.kindDef)
                        + " baseCost=" + basePawnCost.ToString("0.##")
                        + " assignedTotalCost=" + assignedTotalCost.ToString("0.##")
                        + " upgradeBudget=" + upgradeBudget.ToString("0.##")
                        + " spent=" + result.Spent.ToString("0.##")
                        + " finalEstimatedCost=" + (basePawnCost + result.Spent).ToString("0.##"));
                    builder.AppendLine("    protocol=" + result.Protocol);
                    builder.AppendLine("    implants=" + (result.Implants.Count > 0 ? string.Join(", ", result.Implants.ToArray()) : "none"));
                }

                Verse.Log.Message(builder.ToString());
            }
            catch (Exception exception)
            {
                Verse.Log.Warning("[BetterRaids] Failed to process elite raid upgrades: " + exception);
            }
        }

        private static EliteUpgradeResult UpgradeElitePawn(Pawn pawn, bool commander, float upgradeBudget, List<ImplantCatalogLogger.ImplantUpgradeCandidate> implantCandidates)
        {
            EliteUpgradeResult result = new EliteUpgradeResult();
            string protocolDefName = commander || upgradeBudget >= 150f ? "BetterRaids_ExplosiveProtocol" : "BetterRaids_AcidProtocol";
            float protocolCost = protocolDefName == "BetterRaids_ExplosiveProtocol" ? ExplosiveProtocolCost : AcidProtocolCost;

            if (TryAddProtocol(pawn, protocolDefName))
            {
                result.Protocol = protocolDefName;
                result.Spent += protocolCost;
            }
            else
            {
                result.Protocol = "none";
            }

            float remainingBudget = Math.Max(0f, upgradeBudget - result.Spent);
            int installed = 0;
            while (installed < MaxImplantsPerElite)
            {
                ImplantCatalogLogger.ImplantUpgradeCandidate candidate = FindBestAffordableCandidate(pawn, implantCandidates, remainingBudget);
                if (candidate == null)
                {
                    break;
                }

                BodyPartRecord part = FindAvailableBodyPart(pawn, candidate.BodyPartDefName);
                if (part == null || !TryAddImplant(pawn, candidate, part))
                {
                    break;
                }

                result.Implants.Add(candidate.DefName + "@" + candidate.BodyPartDefName + "(" + candidate.EstimatedRaidPointCost.ToString("0.#") + ")");
                result.Spent += candidate.EstimatedRaidPointCost;
                remainingBudget -= candidate.EstimatedRaidPointCost;
                installed++;
            }

            return result;
        }

        private static ImplantCatalogLogger.ImplantUpgradeCandidate FindBestAffordableCandidate(Pawn pawn, List<ImplantCatalogLogger.ImplantUpgradeCandidate> candidates, float remainingBudget)
        {
            if (candidates == null || candidates.Count == 0 || remainingBudget <= 0f)
            {
                return null;
            }

            return candidates
                .Where(candidate => candidate.EstimatedRaidPointCost <= remainingBudget)
                .Where(candidate => FindAvailableBodyPart(pawn, candidate.BodyPartDefName) != null)
                .OrderByDescending(candidate => candidate.EstimatedRaidPointCost)
                .ThenBy(candidate => candidate.DefName)
                .FirstOrDefault();
        }

        private static bool TryAddProtocol(Pawn pawn, string protocolDefName)
        {
            if (pawn == null || HasHediff(pawn, "BetterRaids_AcidProtocol") || HasHediff(pawn, "BetterRaids_ExplosiveProtocol"))
            {
                return false;
            }

            HediffDef protocolDef = DefDatabase<HediffDef>.GetNamedSilentFail(protocolDefName);
            BodyPartRecord torso = FindAvailableBodyPart(pawn, "Torso");
            if (protocolDef == null || torso == null)
            {
                return false;
            }

            pawn.health.AddHediff(HediffMaker.MakeHediff(protocolDef, pawn, torso), torso);
            return true;
        }

        private static bool TryAddImplant(Pawn pawn, ImplantCatalogLogger.ImplantUpgradeCandidate candidate, BodyPartRecord part)
        {
            if (pawn == null || candidate == null || candidate.HediffDef == null || part == null)
            {
                return false;
            }

            pawn.health.AddHediff(HediffMaker.MakeHediff(candidate.HediffDef, pawn, part), part);
            return true;
        }

        private static BodyPartRecord FindAvailableBodyPart(Pawn pawn, string bodyPartDefName)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || string.IsNullOrEmpty(bodyPartDefName))
            {
                return null;
            }

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part == null || part.def == null || part.def.defName != bodyPartDefName)
                {
                    continue;
                }

                if (!HasBlockingImplantOnRelatedPart(pawn, part))
                {
                    return part;
                }
            }

            return null;
        }

        private static bool HasBlockingImplantOnRelatedPart(Pawn pawn, BodyPartRecord candidatePart)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff == null || hediff.Part == null || !IsImplantOrAddedPart(hediff))
                {
                    continue;
                }

                if (IsSameOrAncestor(hediff.Part, candidatePart) || IsSameOrAncestor(candidatePart, hediff.Part))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameOrAncestor(BodyPartRecord possibleAncestor, BodyPartRecord part)
        {
            BodyPartRecord current = part;
            while (current != null)
            {
                if (current == possibleAncestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsImplantOrAddedPart(Hediff hediff)
        {
            return hediff is Hediff_Implant || hediff is Hediff_AddedPart;
        }

        private static bool HasHediff(Pawn pawn, string hediffDefName)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return false;
            }

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            return def != null && pawn.health.hediffSet.HasHediff(def);
        }

        private static int CalculateEliteCount(int eligiblePawnCount)
        {
            if (eligiblePawnCount <= 0)
            {
                return 0;
            }

            int count = (int)Math.Round(eligiblePawnCount * ElitePawnRatio);
            if (count < 1)
            {
                count = 1;
            }

            int maxCount = Math.Max(1, (int)Math.Ceiling(eligiblePawnCount * 0.4f));
            return Math.Min(count, maxCount);
        }

        private static bool IsEligibleHumanlikeRaider(Pawn pawn)
        {
            return pawn != null
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike
                && pawn.kindDef != null
                && pawn.health != null;
        }

        private static float GetBasePawnCost(Pawn pawn)
        {
            return pawn != null && pawn.kindDef != null ? pawn.kindDef.combatPower : 0f;
        }

        private static string GetFactionTechTierName(PawnGroupMakerParms parms)
        {
            if (parms == null || parms.faction == null || parms.faction.def == null)
            {
                return "Spacer";
            }

            return parms.faction.def.techLevel.ToString();
        }

        private static string GetFactionScopeName(PawnGroupMakerParms parms)
        {
            if (parms == null || parms.faction == null || parms.faction.def == null)
            {
                return ImplantCatalogLogger.GlobalFactionScopeName;
            }

            return parms.faction.def.defName;
        }

        private static string SafePawnLabel(Pawn pawn)
        {
            return pawn != null ? pawn.LabelShortCap : "null";
        }

        private static string SafeDefName(Def def)
        {
            return def != null ? def.defName : "null";
        }

        private sealed class EliteUpgradeResult
        {
            public float Spent;
            public string Protocol = "none";
            public readonly List<string> Implants = new List<string>();
        }
    }
}
