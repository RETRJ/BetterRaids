using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
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
        private const string BionicModuleBaseTag = "BM_BionicModuleBaseTag";

        private enum ImplantInstallKind
        {
            Replacement,
            Additive,
            Module,
            Unknown
        }

        public static EliteRaidUpgradeReport Process(PawnGroupMakerParms parms, List<Pawn> pawns)
        {
            if (parms == null || pawns == null || pawns.Count == 0)
            {
                return EliteRaidUpgradeReport.Empty;
            }

            try
            {
                List<Pawn> candidates = pawns
                    .Where(IsEligibleHumanlikeRaider)
                    .OrderByDescending(GetBasePawnCost)
                    .ToList();

                if (candidates.Count == 0)
                {
                    return EliteRaidUpgradeReport.Empty;
                }

                int eliteCount = CalculateEliteCount(candidates.Count);
                if (eliteCount <= 0)
                {
                    return EliteRaidUpgradeReport.Empty;
                }

                string techTierName = GetFactionTechTierName(parms);
                string factionScopeName = GetFactionScopeName(parms);
                List<ImplantCatalogLogger.ImplantUpgradeCandidate> implantCandidates =
                    ImplantCatalogLogger.GetEliteUpgradeCandidates(techTierName, factionScopeName);
                List<BionicModuleCandidate> moduleCandidates = GetBionicModuleCandidates(techTierName);

                List<Pawn> selectedElites = candidates.Take(eliteCount).ToList();
                float raidPoints = Math.Max(0f, parms.points);
                float eliteBudget = raidPoints * EliteBudgetRatio;
                float totalWeight = CommanderWeight + Math.Max(0, selectedElites.Count - 1) * NormalEliteWeight;
                float totalSpent = 0f;

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("[BetterRaids] Elite raid budget");
                builder.AppendLine("  factionDef=" + factionScopeName);
                builder.AppendLine("  techTier=" + techTierName);
                builder.AppendLine("  raidPoints=" + raidPoints.ToString("0.##"));
                builder.AppendLine("  threatScalePercent=" + GetThreatScalePercent());
                builder.AppendLine("  elitePawnRatio=" + ElitePawnRatio.ToString("0.##"));
                builder.AppendLine("  eliteBudgetRatio=" + EliteBudgetRatio.ToString("0.##"));
                builder.AppendLine("  eligibleHumanlikePawns=" + candidates.Count);
                builder.AppendLine("  eliteCount=" + selectedElites.Count);
                builder.AppendLine("  eliteBudget=" + eliteBudget.ToString("0.##"));
                builder.AppendLine("  implantCandidates=" + implantCandidates.Count);
                builder.AppendLine("  bionicModuleCandidates=" + moduleCandidates.Count);

                for (int i = 0; i < selectedElites.Count; i++)
                {
                    Pawn pawn = selectedElites[i];
                    bool commander = i == 0;
                    float weight = commander ? CommanderWeight : NormalEliteWeight;
                    float assignedTotalCost = totalWeight > 0f ? eliteBudget * weight / totalWeight : 0f;
                    float basePawnCost = GetBasePawnCost(pawn);
                    float upgradeBudget = Math.Max(0f, assignedTotalCost - basePawnCost);

                    EliteUpgradeResult result = UpgradeElitePawn(pawn, commander, upgradeBudget, implantCandidates, moduleCandidates);
                    totalSpent += result.Spent;
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
                    builder.AppendLine("    modules=" + (result.Modules.Count > 0 ? string.Join(", ", result.Modules.ToArray()) : "none"));
                }

                Verse.Log.Message(builder.ToString());
                return new EliteRaidUpgradeReport(selectedElites.Count, totalSpent);
            }
            catch (Exception exception)
            {
                Verse.Log.Warning("[BetterRaids] Failed to process elite raid upgrades: " + exception);
                return EliteRaidUpgradeReport.Empty;
            }
        }

        private static EliteUpgradeResult UpgradeElitePawn(Pawn pawn, bool commander, float upgradeBudget, List<ImplantCatalogLogger.ImplantUpgradeCandidate> implantCandidates, List<BionicModuleCandidate> moduleCandidates)
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
            while (true)
            {
                ImplantCatalogLogger.ImplantUpgradeCandidate candidate = FindBestAffordableCandidate(pawn, implantCandidates, remainingBudget);
                if (candidate != null)
                {
                    BodyPartRecord part = FindBodyPart(pawn, candidate.BodyPartDefName);
                    if (part == null || !TryAddImplant(pawn, candidate, part))
                    {
                        implantCandidates = WithoutCandidate(implantCandidates, candidate);
                        continue;
                    }

                    result.Implants.Add(candidate.DefName + "@" + candidate.BodyPartDefName + "(" + candidate.EstimatedRaidPointCost.ToString("0.#") + ")");
                    result.Spent += candidate.EstimatedRaidPointCost;
                    remainingBudget -= candidate.EstimatedRaidPointCost;
                    continue;
                }

                BionicModuleCandidate moduleCandidate = FindBestAffordableModuleCandidate(pawn, moduleCandidates, remainingBudget);
                if (moduleCandidate == null)
                {
                    break;
                }

                BodyPartRecord modulePart = FindInstallableModuleBodyPart(pawn, moduleCandidate);
                if (modulePart == null || !TryAddModule(pawn, moduleCandidate, modulePart))
                {
                    moduleCandidates = WithoutModuleCandidate(moduleCandidates, moduleCandidate);
                    continue;
                }

                result.Modules.Add(moduleCandidate.DefName + "@" + modulePart.def.defName + "(" + moduleCandidate.EstimatedRaidPointCost.ToString("0.#") + ")");
                result.Spent += moduleCandidate.EstimatedRaidPointCost;
                remainingBudget -= moduleCandidate.EstimatedRaidPointCost;
            }

            return result;
        }

        private static ImplantCatalogLogger.ImplantUpgradeCandidate FindBestAffordableCandidate(Pawn pawn, List<ImplantCatalogLogger.ImplantUpgradeCandidate> candidates, float remainingBudget)
        {
            if (candidates == null || candidates.Count == 0 || remainingBudget <= 0f)
            {
                return null;
            }

            List<ImplantCatalogLogger.ImplantUpgradeCandidate> eligible = candidates
                .Where(candidate => candidate.EstimatedRaidPointCost <= remainingBudget)
                .Where(candidate => CanInstallCandidate(pawn, candidate))
                .ToList();

            return SelectWeightedRandomCandidate(eligible);
        }

        private static ImplantCatalogLogger.ImplantUpgradeCandidate SelectWeightedRandomCandidate(List<ImplantCatalogLogger.ImplantUpgradeCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            Dictionary<string, float> categoryWeights = new Dictionary<string, float>();
            for (int i = 0; i < candidates.Count; i++)
            {
                ImplantCatalogLogger.ImplantUpgradeCandidate candidate = candidates[i];
                if (!categoryWeights.ContainsKey(candidate.Usefulness))
                {
                    categoryWeights.Add(candidate.Usefulness, Math.Max(0.01f, candidate.UsefulnessSelectionWeight));
                }
            }

            float totalWeight = categoryWeights.Values.Sum();
            float roll = Rand.Range(0f, totalWeight);
            string selectedUsefulness = candidates[0].Usefulness;
            foreach (KeyValuePair<string, float> categoryWeight in categoryWeights)
            {
                roll -= categoryWeight.Value;
                if (roll <= 0f)
                {
                    selectedUsefulness = categoryWeight.Key;
                    break;
                }
            }

            List<ImplantCatalogLogger.ImplantUpgradeCandidate> selectedCategory = candidates
                .Where(candidate => candidate.Usefulness == selectedUsefulness)
                .ToList();

            return selectedCategory.Count > 0
                ? selectedCategory[Rand.Range(0, selectedCategory.Count)]
                : candidates[Rand.Range(0, candidates.Count)];
        }

        private static BionicModuleCandidate FindBestAffordableModuleCandidate(Pawn pawn, List<BionicModuleCandidate> candidates, float remainingBudget)
        {
            if (candidates == null || candidates.Count == 0 || remainingBudget <= 0f)
            {
                return null;
            }

            List<BionicModuleCandidate> eligible = candidates
                .Where(candidate => candidate.EstimatedRaidPointCost <= remainingBudget)
                .Where(candidate => FindInstallableModuleBodyPart(pawn, candidate) != null)
                .ToList();

            if (eligible.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < eligible.Count; i++)
            {
                totalWeight += eligible[i].SelectionWeight;
            }

            float roll = Rand.Range(0f, totalWeight);
            for (int i = 0; i < eligible.Count; i++)
            {
                roll -= eligible[i].SelectionWeight;
                if (roll <= 0f)
                {
                    return eligible[i];
                }
            }

            return eligible[eligible.Count - 1];
        }

        private static List<BionicModuleCandidate> GetBionicModuleCandidates(string techTierName)
        {
            List<BionicModuleCandidate> candidates = new List<BionicModuleCandidate>();
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipe == null || recipe.addsHediff == null || !HasBionicModuleTag(recipe.addsHediff))
                {
                    continue;
                }

                if (!IsBionicModularityInstallRecipe(recipe))
                {
                    continue;
                }

                if (recipe.appliedOnFixedBodyParts == null || recipe.appliedOnFixedBodyParts.Count == 0)
                {
                    continue;
                }

                ThingDef moduleThing = DefDatabase<ThingDef>.GetNamedSilentFail(recipe.addsHediff.defName);
                if (!IsModuleAllowedForTech(moduleThing, techTierName))
                {
                    continue;
                }

                List<string> bodyPartDefNames = recipe.appliedOnFixedBodyParts
                    .Where(part => part != null)
                    .Select(part => part.defName)
                    .Distinct()
                    .ToList();

                candidates.Add(new BionicModuleCandidate(
                    recipe.addsHediff,
                    recipe.addsHediff.defName,
                    recipe.addsHediff.label,
                    bodyPartDefNames,
                    GetRecipeIncompatibleHediffTags(recipe),
                    EstimateBionicModuleCost(moduleThing),
                    GetBionicModuleSelectionWeight(recipe.addsHediff)));
            }

            return candidates
                .GroupBy(candidate => candidate.DefName)
                .Select(MergeModuleCandidateGroup)
                .OrderByDescending(candidate => candidate.SelectionWeight)
                .ThenBy(candidate => candidate.DefName)
                .ToList();
        }

        private static BionicModuleCandidate MergeModuleCandidateGroup(IGrouping<string, BionicModuleCandidate> group)
        {
            BionicModuleCandidate first = group.First();
            return new BionicModuleCandidate(
                first.HediffDef,
                first.DefName,
                first.Label,
                group.SelectMany(candidate => candidate.BodyPartDefNames).Distinct().ToList(),
                group.SelectMany(candidate => candidate.IncompatibleHediffTags).Distinct().ToList(),
                first.EstimatedRaidPointCost,
                first.SelectionWeight);
        }

        private static bool IsBionicModularityInstallRecipe(RecipeDef recipe)
        {
            return recipe != null
                && recipe.workerClass != null
                && recipe.workerClass.FullName == "BionicModularity.Recipe_InstallModule";
        }

        private static bool IsModuleAllowedForTech(ThingDef moduleThing, string techTierName)
        {
            if (moduleThing == null)
            {
                return true;
            }

            return GetTechRank(moduleThing.techLevel.ToString()) <= GetTechRank(techTierName);
        }

        private static int GetTechRank(string techTierName)
        {
            switch (techTierName)
            {
                case "Animal":
                    return 0;
                case "Neolithic":
                    return 1;
                case "Medieval":
                    return 2;
                case "Industrial":
                    return 3;
                case "Spacer":
                    return 4;
                case "Ultra":
                    return 5;
                case "Archotech":
                    return 6;
                default:
                    return 4;
            }
        }

        private static float EstimateBionicModuleCost(ThingDef moduleThing)
        {
            if (moduleThing == null)
            {
                return 75f;
            }

            float techBaseCost;
            switch (moduleThing.techLevel)
            {
                case TechLevel.Industrial:
                    techBaseCost = 45f;
                    break;
                case TechLevel.Spacer:
                    techBaseCost = 90f;
                    break;
                case TechLevel.Ultra:
                    techBaseCost = 140f;
                    break;
                default:
                    techBaseCost = 75f;
                    break;
            }

            return Math.Max(techBaseCost, moduleThing.BaseMarketValue / 10f);
        }

        private static float GetBionicModuleSelectionWeight(HediffDef moduleHediff)
        {
            if (HasHediffTag(moduleHediff, "BM_BionicModuleTag_Combat"))
            {
                return 0.7f;
            }

            if (HasHediffTag(moduleHediff, "BM_BionicModuleTag_Support"))
            {
                return 0.15f;
            }

            if (HasHediffTag(moduleHediff, "BM_BionicModuleTag_Ability") || HasHediffTag(moduleHediff, "BM_BionicModuleTag_Power"))
            {
                return 0.1f;
            }

            return 0.05f;
        }

        private static List<string> GetRecipeIncompatibleHediffTags(RecipeDef recipe)
        {
            object value = AccessTools.Field(typeof(RecipeDef), "incompatibleWithHediffTags") != null
                ? AccessTools.Field(typeof(RecipeDef), "incompatibleWithHediffTags").GetValue(recipe)
                : null;

            List<string> tags = value as List<string>;
            return tags != null ? new List<string>(tags) : new List<string>();
        }

        private static List<ImplantCatalogLogger.ImplantUpgradeCandidate> WithoutCandidate(List<ImplantCatalogLogger.ImplantUpgradeCandidate> candidates, ImplantCatalogLogger.ImplantUpgradeCandidate rejected)
        {
            return candidates == null
                ? new List<ImplantCatalogLogger.ImplantUpgradeCandidate>()
                : candidates.Where(candidate => candidate != rejected).ToList();
        }

        private static List<BionicModuleCandidate> WithoutModuleCandidate(List<BionicModuleCandidate> candidates, BionicModuleCandidate rejected)
        {
            return candidates == null
                ? new List<BionicModuleCandidate>()
                : candidates.Where(candidate => candidate != rejected).ToList();
        }

        private static bool TryAddProtocol(Pawn pawn, string protocolDefName)
        {
            if (pawn == null || HasHediff(pawn, "BetterRaids_AcidProtocol") || HasHediff(pawn, "BetterRaids_ExplosiveProtocol"))
            {
                return false;
            }

            HediffDef protocolDef = DefDatabase<HediffDef>.GetNamedSilentFail(protocolDefName);
            BodyPartRecord torso = FindBodyPart(pawn, "Torso");
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

            if (!CanInstallCandidate(pawn, candidate))
            {
                return false;
            }

            Hediff hediff = HediffMaker.MakeHediff(candidate.HediffDef, pawn, part);
            PrepareNewHediffForRaider(hediff);
            pawn.health.AddHediff(hediff, part);
            return true;
        }

        private static bool TryAddModule(Pawn pawn, BionicModuleCandidate candidate, BodyPartRecord part)
        {
            if (pawn == null || candidate == null || candidate.HediffDef == null || part == null)
            {
                return false;
            }

            if (!CanInstallModuleCandidate(pawn, candidate, part))
            {
                return false;
            }

            pawn.health.AddHediff(HediffMaker.MakeHediff(candidate.HediffDef, pawn, part), part);
            return true;
        }

        private static bool CanInstallCandidate(Pawn pawn, ImplantCatalogLogger.ImplantUpgradeCandidate candidate)
        {
            if (pawn == null || candidate == null || candidate.HediffDef == null)
            {
                return false;
            }

            BodyPartRecord part = FindBodyPart(pawn, candidate.BodyPartDefName);
            if (part == null)
            {
                return false;
            }

            ImplantInstallKind installKind = GetInstallKind(candidate.HediffDef);
            switch (installKind)
            {
                case ImplantInstallKind.Replacement:
                    return !HasOverlappingReplacement(pawn, part);
                case ImplantInstallKind.Additive:
                    return !HasHediff(pawn, candidate.HediffDef) && !HasConflictingHediffTags(pawn, candidate.HediffDef, null);
                case ImplantInstallKind.Module:
                    return false;
                default:
                    return false;
            }
        }

        private static bool CanInstallModuleCandidate(Pawn pawn, BionicModuleCandidate candidate, BodyPartRecord part)
        {
            if (pawn == null || candidate == null || candidate.HediffDef == null || part == null)
            {
                return false;
            }

            if (!HasArtificialBaseOnPart(pawn, part))
            {
                return false;
            }

            if (HasHediffOnPart(pawn, candidate.HediffDef, part))
            {
                return false;
            }

            return !HasConflictingRecipeTags(pawn, candidate.IncompatibleHediffTags, part);
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

        private static BodyPartRecord FindInstallableModuleBodyPart(Pawn pawn, BionicModuleCandidate candidate)
        {
            if (pawn == null || candidate == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return null;
            }

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part == null || part.def == null || !candidate.BodyPartDefNames.Contains(part.def.defName))
                {
                    continue;
                }

                if (CanInstallModuleCandidate(pawn, candidate, part))
                {
                    return part;
                }
            }

            return null;
        }

        private static bool HasArtificialBaseOnPart(Pawn pawn, BodyPartRecord part)
        {
            if (pawn == null || part == null || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff == null || hediff.Part != part)
                {
                    continue;
                }

                ImplantInstallKind kind = GetInstallKind(hediff.def);
                if (kind == ImplantInstallKind.Replacement || kind == ImplantInstallKind.Additive)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasHediffOnPart(Pawn pawn, HediffDef hediffDef, BodyPartRecord part)
        {
            if (pawn == null || hediffDef == null || part == null || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff != null && hediff.def == hediffDef && hediff.Part == part)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasConflictingHediffTags(Pawn pawn, HediffDef newHediffDef, BodyPartRecord samePartOnly)
        {
            if (pawn == null || newHediffDef == null || newHediffDef.tags == null || newHediffDef.tags.Count == 0 || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff existing = pawn.health.hediffSet.hediffs[i];
                if (existing == null || existing.def == null || existing.def.tags == null)
                {
                    continue;
                }

                if (samePartOnly != null && existing.Part != samePartOnly)
                {
                    continue;
                }

                for (int tagIndex = 0; tagIndex < newHediffDef.tags.Count; tagIndex++)
                {
                    string tag = newHediffDef.tags[tagIndex];
                    if (IsExclusiveHediffTag(tag) && existing.def.tags.Contains(tag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsExclusiveHediffTag(string tag)
        {
            return tag == "ExtraLeftArm" || tag == "ExtraRightArm";
        }

        private static bool HasConflictingRecipeTags(Pawn pawn, List<string> incompatibleTags, BodyPartRecord samePartOnly)
        {
            if (pawn == null || incompatibleTags == null || incompatibleTags.Count == 0 || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff existing = pawn.health.hediffSet.hediffs[i];
                if (existing == null || existing.def == null || existing.def.tags == null)
                {
                    continue;
                }

                if (samePartOnly != null && existing.Part != samePartOnly)
                {
                    continue;
                }

                for (int tagIndex = 0; tagIndex < incompatibleTags.Count; tagIndex++)
                {
                    if (existing.def.tags.Contains(incompatibleTags[tagIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void PrepareNewHediffForRaider(Hediff hediff)
        {
            if (hediff == null || hediff.def == null)
            {
                return;
            }

            if ((HasHediffTag(hediff.def, "ExtraLeftArm") || HasHediffTag(hediff.def, "ExtraRightArm")) && hediff.def.maxSeverity > hediff.Severity)
            {
                hediff.Severity = hediff.def.maxSeverity;
            }
        }

        private static bool HasOverlappingReplacement(Pawn pawn, BodyPartRecord candidatePart)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff == null || hediff.Part == null || GetInstallKind(hediff.def) != ImplantInstallKind.Replacement)
                {
                    continue;
                }

                if (hediff.Part == candidatePart || IsSameOrAncestor(candidatePart, hediff.Part) || IsSameOrAncestor(hediff.Part, candidatePart))
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

        private static ImplantInstallKind GetInstallKind(HediffDef hediffDef)
        {
            if (hediffDef == null)
            {
                return ImplantInstallKind.Unknown;
            }

            if (HasBionicModuleTag(hediffDef))
            {
                return ImplantInstallKind.Module;
            }

            if (hediffDef.addedPartProps != null)
            {
                return ImplantInstallKind.Replacement;
            }

            Type hediffClass = hediffDef.hediffClass;
            if (hediffClass != null && typeof(Hediff_Implant).IsAssignableFrom(hediffClass))
            {
                return ImplantInstallKind.Additive;
            }

            return ImplantInstallKind.Unknown;
        }

        private static bool HasBionicModuleTag(HediffDef hediffDef)
        {
            return HasHediffTag(hediffDef, BionicModuleBaseTag);
        }

        private static bool HasHediffTag(HediffDef hediffDef, string tag)
        {
            return hediffDef != null && hediffDef.tags != null && hediffDef.tags.Contains(tag);
        }

        private static bool HasHediff(Pawn pawn, string hediffDefName)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return false;
            }

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            return def != null && HasHediff(pawn, def);
        }

        private static bool HasHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || hediffDef == null)
            {
                return false;
            }

            return pawn.health.hediffSet.HasHediff(hediffDef);
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

        private static int GetThreatScalePercent()
        {
            BetterRaidsSettings settings = BetterRaidsMod.Settings;
            return settings != null ? settings.ThreatScalePercent : BetterRaidsSettings.DefaultThreatScalePercent;
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
            public readonly List<string> Modules = new List<string>();
        }

        private sealed class BionicModuleCandidate
        {
            public readonly HediffDef HediffDef;
            public readonly string DefName;
            public readonly string Label;
            public readonly List<string> BodyPartDefNames;
            public readonly List<string> IncompatibleHediffTags;
            public readonly float EstimatedRaidPointCost;
            public readonly float SelectionWeight;

            public BionicModuleCandidate(HediffDef hediffDef, string defName, string label, List<string> bodyPartDefNames, List<string> incompatibleHediffTags, float estimatedRaidPointCost, float selectionWeight)
            {
                HediffDef = hediffDef;
                DefName = defName;
                Label = label;
                BodyPartDefNames = bodyPartDefNames ?? new List<string>();
                IncompatibleHediffTags = incompatibleHediffTags ?? new List<string>();
                EstimatedRaidPointCost = estimatedRaidPointCost;
                SelectionWeight = Math.Max(0.01f, selectionWeight);
            }
        }
    }

    internal sealed class EliteRaidUpgradeReport
    {
        public static readonly EliteRaidUpgradeReport Empty = new EliteRaidUpgradeReport(0, 0f);

        public readonly int EliteCount;
        public readonly float TotalSpent;

        public EliteRaidUpgradeReport(int eliteCount, float totalSpent)
        {
            EliteCount = eliteCount;
            TotalSpent = totalSpent;
        }
    }
}
