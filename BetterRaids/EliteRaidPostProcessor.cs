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
        private const float MinElitePawnRatio = 0.40f;
        private const float MaxElitePawnRatio = 0.80f;
        private const float EliteBudgetRatio = 0.75f;
        private const float CommanderWeight = 3f;
        private const float NormalEliteWeight = 1f;
        private const float AcidProtocolCost = 10f;
        private const float ExplosiveProtocolCost = 25f;
        private const string BionicModuleBaseTag = "BM_BionicModuleBaseTag";
        private const string RequiredEmpResistanceImplantDefName = "CranialInsulation";
        private const float OneHigherTierImplantChance = 0.10f;
        private const float TwoHigherTierImplantsChance = 0.05f;
        private const float MaturedAdjustmentSeverity = 0.99f;

        private static readonly string[] ShoulderWeaponKeywords =
        {
            "turret",
            "mortar",
            "rocket",
            "launcher",
            "pod"
        };

        private static readonly HashSet<string> UnsafePreMapHediffDefNames = new HashSet<string>
        {
            "USH_InstalledGoldenSkinReplacement",
            "USH_InstalledGoldenTeethReplacement",
            "USH_InstalledPlasteelSkinReplacement",
            "USH_InstalledPlasteelTeethReplacement"
        };

        private static readonly HashSet<string> AuxiliaryAiBrainImplantDefNames = new HashSet<string>
        {
            "ConstructorCore",
            "DiplomatCore",
            "DoctorCore",
            "FarmerCore",
            "MinerCore"
        };

        private static readonly Dictionary<string, List<ImplantCatalogLogger.ImplantUpgradeCandidate>> EliteCandidateCache =
            new Dictionary<string, List<ImplantCatalogLogger.ImplantUpgradeCandidate>>();

        private static readonly Dictionary<string, List<ImplantCatalogLogger.ImplantUpgradeCandidate>> DirectEliteCandidateCache =
            new Dictionary<string, List<ImplantCatalogLogger.ImplantUpgradeCandidate>>();

        private static readonly Dictionary<string, List<BionicModuleCandidate>> BionicModuleCandidateCache =
            new Dictionary<string, List<BionicModuleCandidate>>();

        private enum ImplantInstallKind
        {
            Replacement,
            Additive,
            Module,
            Unknown
        }

        private enum EliteRole
        {
            Commander,
            Heavy,
            Sniper,
            Brawler,
            Assault,
            Support
        }

        internal static void ClearCaches()
        {
            EliteCandidateCache.Clear();
            DirectEliteCandidateCache.Clear();
            BionicModuleCandidateCache.Clear();
        }

        public static EliteRaidUpgradeReport Process(PawnGroupMakerParms parms, List<Pawn> pawns)
        {
            if (parms == null || pawns == null || pawns.Count == 0)
            {
                return EliteRaidUpgradeReport.Empty;
            }

            try
            {
                EnsureEmpireMeleeWeapons(pawns);

                List<Pawn> candidates = pawns
                    .Where(IsEligibleHumanlikeRaider)
                    .OrderByDescending(GetBasePawnCost)
                    .ToList();

                if (candidates.Count == 0)
                {
                    return EliteRaidUpgradeReport.Empty;
                }

                float elitePawnRatio = RollElitePawnRatio();
                int eliteCount = CalculateEliteCount(candidates.Count, elitePawnRatio);
                if (eliteCount <= 0)
                {
                    return EliteRaidUpgradeReport.Empty;
                }

                string techTierName = GetFactionTechTierName(parms);
                string factionScopeName = GetFactionScopeName(parms);
                List<ImplantCatalogLogger.ImplantUpgradeCandidate> implantCandidates =
                    GetCachedEliteUpgradeCandidates(techTierName, factionScopeName);
                string higherTechTierName = null;
                List<ImplantCatalogLogger.ImplantUpgradeCandidate> higherTierImplantCandidates =
                    ImplantCatalogLogger.TryGetNextTechTierName(techTierName, out higherTechTierName)
                        ? GetCachedDirectEliteUpgradeCandidates(higherTechTierName, factionScopeName)
                        : new List<ImplantCatalogLogger.ImplantUpgradeCandidate>();
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
                builder.AppendLine("  elitePawnRatio=" + elitePawnRatio.ToString("0.##"));
                builder.AppendLine("  eliteBudgetRatio=" + EliteBudgetRatio.ToString("0.##"));
                builder.AppendLine("  eligibleHumanlikePawns=" + candidates.Count);
                builder.AppendLine("  eliteCount=" + selectedElites.Count);
                builder.AppendLine("  eliteBudget=" + eliteBudget.ToString("0.##"));
                builder.AppendLine("  implantCandidates=" + implantCandidates.Count);
                builder.AppendLine("  higherTier=" + (!string.IsNullOrEmpty(higherTechTierName) ? higherTechTierName : "none"));
                builder.AppendLine("  higherTierImplantCandidates=" + higherTierImplantCandidates.Count);
                builder.AppendLine("  bionicModuleCandidates=" + moduleCandidates.Count);

                for (int i = 0; i < selectedElites.Count; i++)
                {
                    Pawn pawn = selectedElites[i];
                    bool commander = i == 0;
                    EliteRole role = DetermineEliteRole(pawn, commander);
                    float weight = commander ? CommanderWeight : NormalEliteWeight;
                    float assignedTotalCost = totalWeight > 0f ? eliteBudget * weight / totalWeight : 0f;
                    float basePawnCost = GetBasePawnCost(pawn);
                    float upgradeBudget = Math.Max(0f, assignedTotalCost - basePawnCost);
                    int higherTierAllowance = RollHigherTierImplantAllowance();

                    EliteUpgradeResult result = UpgradeElitePawn(pawn, role, techTierName, upgradeBudget, implantCandidates, higherTierImplantCandidates, moduleCandidates, higherTierAllowance);
                    totalSpent += result.Spent;
                    builder.AppendLine("  elite #" + (i + 1)
                        + " role=" + role
                        + " pawn=" + SafePawnLabel(pawn)
                        + " kind=" + SafeDefName(pawn.kindDef)
                        + " baseCost=" + basePawnCost.ToString("0.##")
                        + " assignedTotalCost=" + assignedTotalCost.ToString("0.##")
                        + " upgradeBudget=" + upgradeBudget.ToString("0.##")
                        + " spent=" + result.Spent.ToString("0.##")
                        + " finalEstimatedCost=" + (basePawnCost + result.Spent).ToString("0.##")
                        + " higherTierAllowance=" + higherTierAllowance
                        + " higherTierUsed=" + result.HigherTierImplantsUsed);
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

        private static EliteUpgradeResult UpgradeElitePawn(Pawn pawn, EliteRole role, string techTierName, float upgradeBudget, List<ImplantCatalogLogger.ImplantUpgradeCandidate> implantCandidates, List<ImplantCatalogLogger.ImplantUpgradeCandidate> higherTierImplantCandidates, List<BionicModuleCandidate> moduleCandidates, int higherTierAllowance)
        {
            EliteUpgradeResult result = new EliteUpgradeResult();
            bool commander = role == EliteRole.Commander;
            bool allowAdvancedEliteSystems = AllowsAdvancedEliteSystems(techTierName);
            string protocolDefName = commander || upgradeBudget >= 150f ? "BetterRaids_ExplosiveProtocol" : "BetterRaids_AcidProtocol";
            float protocolCost = protocolDefName == "BetterRaids_ExplosiveProtocol" ? ExplosiveProtocolCost : AcidProtocolCost;

            TryAddVisualMarker(pawn, commander);
            MakeEliteUnwaveringlyLoyal(pawn);
            if (allowAdvancedEliteSystems)
            {
                TryAddRequiredEmpResistance(pawn, result);
            }

            if (allowAdvancedEliteSystems && TryAddProtocol(pawn, protocolDefName))
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
                bool higherTierPick = false;
                ImplantCatalogLogger.ImplantUpgradeCandidate candidate = null;

                if (higherTierAllowance > result.HigherTierImplantsUsed)
                {
                    candidate = FindBestAffordableCandidate(pawn, higherTierImplantCandidates, remainingBudget, role);
                    higherTierPick = candidate != null;
                }

                if (candidate == null)
                {
                    candidate = FindBestAffordableCandidate(pawn, implantCandidates, remainingBudget, role);
                }

                if (candidate != null)
                {
                    BodyPartRecord part = FindInstallableBodyPart(pawn, candidate);
                    if (part == null || !TryAddImplant(pawn, candidate, part))
                    {
                        if (higherTierPick)
                        {
                            higherTierImplantCandidates = WithoutCandidate(higherTierImplantCandidates, candidate);
                        }
                        else
                        {
                            implantCandidates = WithoutCandidate(implantCandidates, candidate);
                        }

                        continue;
                    }

                    if (higherTierPick)
                    {
                        result.HigherTierImplantsUsed++;
                    }

                    result.Implants.Add(candidate.DefName + "@" + SafeBodyPartLabel(part) + (higherTierPick ? "[higher]" : "") + "(" + candidate.EstimatedRaidPointCost.ToString("0.#") + ")");
                    result.Spent += candidate.EstimatedRaidPointCost;
                    remainingBudget -= candidate.EstimatedRaidPointCost;
                    continue;
                }

                BionicModuleCandidate moduleCandidate = FindBestAffordableModuleCandidate(pawn, moduleCandidates, remainingBudget, role);
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

        private static ImplantCatalogLogger.ImplantUpgradeCandidate FindBestAffordableCandidate(Pawn pawn, List<ImplantCatalogLogger.ImplantUpgradeCandidate> candidates, float remainingBudget, EliteRole role)
        {
            if (candidates == null || candidates.Count == 0 || remainingBudget <= 0f)
            {
                return null;
            }

            List<WeightedImplantCandidate> eligible = new List<WeightedImplantCandidate>();
            for (int i = 0; i < candidates.Count; i++)
            {
                ImplantCatalogLogger.ImplantUpgradeCandidate candidate = candidates[i];
                if (candidate == null || candidate.EstimatedRaidPointCost > remainingBudget || !CanInstallCandidate(pawn, candidate))
                {
                    continue;
                }

                float weight = GetImplantRoleWeight(candidate, role);
                if (weight > 0f)
                {
                    eligible.Add(new WeightedImplantCandidate(candidate, weight));
                }
            }

            return SelectWeightedRandomCandidate(eligible);
        }

        private static ImplantCatalogLogger.ImplantUpgradeCandidate SelectWeightedRandomCandidate(List<WeightedImplantCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].Weight;
            }

            float roll = Rand.Range(0f, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].Weight;
                if (roll <= 0f)
                {
                    return candidates[i].Candidate;
                }
            }

            return candidates[candidates.Count - 1].Candidate;
        }

        private static BionicModuleCandidate FindBestAffordableModuleCandidate(Pawn pawn, List<BionicModuleCandidate> candidates, float remainingBudget, EliteRole role)
        {
            if (candidates == null || candidates.Count == 0 || remainingBudget <= 0f)
            {
                return null;
            }

            List<WeightedModuleCandidate> eligible = new List<WeightedModuleCandidate>();
            for (int i = 0; i < candidates.Count; i++)
            {
                BionicModuleCandidate candidate = candidates[i];
                if (candidate == null || candidate.EstimatedRaidPointCost > remainingBudget || FindInstallableModuleBodyPart(pawn, candidate) == null)
                {
                    continue;
                }

                float weight = GetModuleRoleWeight(candidate, role);
                if (weight > 0f)
                {
                    eligible.Add(new WeightedModuleCandidate(candidate, weight));
                }
            }

            if (eligible.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < eligible.Count; i++)
            {
                totalWeight += eligible[i].Weight;
            }

            float roll = Rand.Range(0f, totalWeight);
            for (int i = 0; i < eligible.Count; i++)
            {
                roll -= eligible[i].Weight;
                if (roll <= 0f)
                {
                    return eligible[i].Candidate;
                }
            }

            return eligible[eligible.Count - 1].Candidate;
        }

        private static float GetImplantRoleWeight(ImplantCatalogLogger.ImplantUpgradeCandidate candidate, EliteRole role)
        {
            if (candidate == null)
            {
                return 0f;
            }

            float weight = Math.Max(0.01f, candidate.UsefulnessSelectionWeight);
            weight *= GetBodyPartRoleMultiplier(candidate.BodyPartDefName, role);
            weight *= GetDefNameRoleMultiplier(candidate.DefName, role);

            return Math.Max(0.01f, weight);
        }

        private static float GetModuleRoleWeight(BionicModuleCandidate candidate, EliteRole role)
        {
            if (candidate == null)
            {
                return 0f;
            }

            float weight = Math.Max(0.01f, candidate.SelectionWeight);
            float bestBodyPartMultiplier = 1f;
            for (int i = 0; i < candidate.BodyPartDefNames.Count; i++)
            {
                bestBodyPartMultiplier = Math.Max(bestBodyPartMultiplier, GetBodyPartRoleMultiplier(candidate.BodyPartDefNames[i], role));
            }

            return Math.Max(0.01f, weight * bestBodyPartMultiplier * GetDefNameRoleMultiplier(candidate.DefName, role));
        }

        private static float GetBodyPartRoleMultiplier(string bodyPartDefName, EliteRole role)
        {
            switch (role)
            {
                case EliteRole.Commander:
                    return IsOneOf(bodyPartDefName, "Brain", "Eye", "Torso", "Spine", "Heart") ? 2f : 1.25f;
                case EliteRole.Sniper:
                    if (bodyPartDefName == "Eye")
                    {
                        return 3.25f;
                    }

                    if (IsOneOf(bodyPartDefName, "Brain", "Ear"))
                    {
                        return 2f;
                    }

                    return IsOneOf(bodyPartDefName, "Hand", "Shoulder", "Arm") ? 1.35f : 0.75f;
                case EliteRole.Brawler:
                    if (IsOneOf(bodyPartDefName, "Shoulder", "Arm", "Hand", "Leg", "Spine", "Jaw"))
                    {
                        return 2.5f;
                    }

                    return IsOneOf(bodyPartDefName, "Eye", "Heart") ? 1.2f : 0.75f;
                case EliteRole.Heavy:
                    if (IsOneOf(bodyPartDefName, "Torso", "Rib", "Spine", "Heart", "Stomach", "Lung", "Kidney", "Liver"))
                    {
                        return 2.4f;
                    }

                    return IsOneOf(bodyPartDefName, "Leg", "Shoulder") ? 1.35f : 0.85f;
                case EliteRole.Support:
                    if (IsOneOf(bodyPartDefName, "Brain", "Ear", "Eye", "Heart", "Lung", "Kidney", "Liver", "Stomach"))
                    {
                        return 1.9f;
                    }

                    return 0.9f;
                default:
                    return IsOneOf(bodyPartDefName, "Eye", "Shoulder", "Arm", "Hand", "Leg", "Spine") ? 1.6f : 1f;
            }
        }

        private static float GetDefNameRoleMultiplier(string defName, EliteRole role)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return 1f;
            }

            if (defName == "Painstopper")
            {
                return role == EliteRole.Brawler || role == EliteRole.Heavy ? 4f : 3f;
            }

            switch (role)
            {
                case EliteRole.Commander:
                    if (ContainsAny(defName, "Commando", "Archotech", "Advanced", "Tactical", "Neural", "Cortex"))
                    {
                        return 2.2f;
                    }

                    break;
                case EliteRole.Sniper:
                    if (ContainsAny(defName, "Sharpshooter", "Eye", "Cornea", "Pupil", "Recon", "Target", "Tactical"))
                    {
                        return 3f;
                    }

                    break;
                case EliteRole.Brawler:
                    if (ContainsAny(defName, "Brawler", "Claw", "Blade", "Talon", "Arm", "Hand", "Leg", "Spine", "Melee"))
                    {
                        return 2.6f;
                    }

                    break;
                case EliteRole.Heavy:
                    if (ContainsAny(defName, "Armor", "Skin", "Rib", "Coagulator", "Adrenaline", "Heart", "Spine", "Stomach", "Mortar", "Turret", "Rocket"))
                    {
                        return 2.4f;
                    }

                    break;
                case EliteRole.Support:
                    if (ContainsAny(defName, "Sensor", "Filter", "Ear", "Brain", "Doctor", "Medical", "Healing", "Immuno", "Detox", "Voice"))
                    {
                        return 2f;
                    }

                    break;
                default:
                    if (ContainsAny(defName, "Tactical", "Commando", "Advanced", "Bionic"))
                    {
                        return 1.5f;
                    }

                    break;
            }

            return 1f;
        }

        private static List<BionicModuleCandidate> GetBionicModuleCandidates(string techTierName)
        {
            string cacheKey = techTierName ?? "null";
            List<BionicModuleCandidate> cached;
            if (BionicModuleCandidateCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

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

            List<BionicModuleCandidate> resolved = candidates
                .GroupBy(candidate => candidate.DefName)
                .Select(MergeModuleCandidateGroup)
                .OrderByDescending(candidate => candidate.SelectionWeight)
                .ThenBy(candidate => candidate.DefName)
                .ToList();

            BionicModuleCandidateCache[cacheKey] = resolved;
            return resolved;
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

        private static bool TryAddVisualMarker(Pawn pawn, bool commander)
        {
            if (pawn == null)
            {
                return false;
            }

            string markerDefName = commander ? "BetterRaids_CommanderMarker" : "BetterRaids_EliteMarker";
            if (HasHediff(pawn, markerDefName))
            {
                return false;
            }

            HediffDef markerDef = DefDatabase<HediffDef>.GetNamedSilentFail(markerDefName);
            if (markerDef == null)
            {
                return false;
            }

            pawn.health.AddHediff(HediffMaker.MakeHediff(markerDef, pawn));
            return true;
        }

        private static bool AllowsAdvancedEliteSystems(string techTierName)
        {
            return string.Equals(techTierName, "Spacer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(techTierName, "Ultra", StringComparison.OrdinalIgnoreCase)
                || string.Equals(techTierName, "Archotech", StringComparison.OrdinalIgnoreCase);
        }

        private static void MakeEliteUnwaveringlyLoyal(Pawn pawn)
        {
            if (pawn != null && pawn.guest != null)
            {
                pawn.guest.Recruitable = false;
            }
        }

        private static bool TryAddRequiredEmpResistance(Pawn pawn, EliteUpgradeResult result)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(RequiredEmpResistanceImplantDefName);
            BodyPartRecord torso = FindBodyPart(pawn, "Torso");
            if (hediffDef == null || torso == null || HasHediff(pawn, hediffDef))
            {
                return false;
            }

            ImplantCatalogLogger.ImplantUpgradeCandidate candidate = new ImplantCatalogLogger.ImplantUpgradeCandidate(
                hediffDef,
                hediffDef.defName,
                hediffDef.label,
                "Torso",
                "SupportCombat",
                0f,
                1f);

            if (!TryAddImplant(pawn, candidate, torso))
            {
                return false;
            }

            if (result != null)
            {
                result.Implants.Add(hediffDef.defName + "@Torso[required-emp]");
            }

            return true;
        }

        private static bool TryAddImplant(Pawn pawn, ImplantCatalogLogger.ImplantUpgradeCandidate candidate, BodyPartRecord part)
        {
            if (pawn == null || candidate == null || candidate.HediffDef == null || part == null)
            {
                return false;
            }

            if (IsUnsafeDuringPawnGeneration(candidate.HediffDef) || !CanInstallCandidateOnPart(pawn, candidate, part))
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

            if (IsUnsafeDuringPawnGeneration(candidate.HediffDef) || !CanInstallModuleCandidate(pawn, candidate, part))
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

            if (IsUnsafeDuringPawnGeneration(candidate.HediffDef))
            {
                return false;
            }

            return FindInstallableBodyPart(pawn, candidate) != null;
        }

        private static bool CanInstallCandidateOnPart(Pawn pawn, ImplantCatalogLogger.ImplantUpgradeCandidate candidate, BodyPartRecord part)
        {
            if (pawn == null || candidate == null || candidate.HediffDef == null || part == null)
            {
                return false;
            }

            if (IsUnsafeDuringPawnGeneration(candidate.HediffDef) || part.def == null || part.def.defName != candidate.BodyPartDefName)
            {
                return false;
            }

            if (IsShoulderWeaponImplant(candidate.HediffDef) && HasShoulderWeaponImplant(pawn))
            {
                return false;
            }

            if (IsAuxiliaryAiBrainImplant(candidate.HediffDef) && HasAuxiliaryAiBrainImplant(pawn))
            {
                return false;
            }

            if (IsBodyPartImplantCapReached(pawn, part.def.defName))
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

            if (IsUnsafeDuringPawnGeneration(candidate.HediffDef))
            {
                return false;
            }

            if (IsBodyPartImplantCapReached(pawn, part.def.defName))
            {
                return false;
            }

            if (!HasModuleCompatibleBaseOnPart(pawn, part, candidate))
            {
                return false;
            }

            if (HasHediffOnPart(pawn, candidate.HediffDef, part))
            {
                return false;
            }

            return !HasConflictingRecipeTags(pawn, candidate.IncompatibleHediffTags, part);
        }

        private static bool IsUnsafeDuringPawnGeneration(HediffDef hediffDef)
        {
            if (hediffDef == null)
            {
                return true;
            }

            if (UnsafePreMapHediffDefNames.Contains(hediffDef.defName))
            {
                return true;
            }

            if (hediffDef.comps == null)
            {
                return false;
            }

            for (int i = 0; i < hediffDef.comps.Count; i++)
            {
                Type compClass = hediffDef.comps[i] != null ? hediffDef.comps[i].compClass : null;
                string fullName = compClass != null ? compClass.FullName : null;
                if (fullName == "USH_GE.HediffCompRemoveDuplicates" || (fullName != null && fullName.EndsWith(".HediffCompRemoveDuplicates", StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
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

        private static BodyPartRecord FindInstallableBodyPart(Pawn pawn, ImplantCatalogLogger.ImplantUpgradeCandidate candidate)
        {
            if (pawn == null || candidate == null || pawn.health == null || pawn.health.hediffSet == null || string.IsNullOrEmpty(candidate.BodyPartDefName))
            {
                return null;
            }

            List<BodyPartRecord> parts = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part != null && part.def != null && part.def.defName == candidate.BodyPartDefName && CanInstallCandidateOnPart(pawn, candidate, part))
                {
                    parts.Add(part);
                }
            }

            return parts.Count > 0 ? parts[Rand.Range(0, parts.Count)] : null;
        }

        private static BodyPartRecord FindInstallableModuleBodyPart(Pawn pawn, BionicModuleCandidate candidate)
        {
            if (pawn == null || candidate == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return null;
            }

            List<BodyPartRecord> parts = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part == null || part.def == null || !candidate.BodyPartDefNames.Contains(part.def.defName))
                {
                    continue;
                }

                if (CanInstallModuleCandidate(pawn, candidate, part))
                {
                    parts.Add(part);
                }
            }

            return parts.Count > 0 ? parts[Rand.Range(0, parts.Count)] : null;
        }

        private static bool HasModuleCompatibleBaseOnPart(Pawn pawn, BodyPartRecord part, BionicModuleCandidate moduleCandidate)
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

                if (IsModuleCompatibleBaseHediff(hediff.def, moduleCandidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsModuleCompatibleBaseHediff(HediffDef hediffDef, BionicModuleCandidate moduleCandidate)
        {
            if (hediffDef == null || IsBetterRaidsProtocol(hediffDef) || HasBionicModuleTag(hediffDef))
            {
                return false;
            }

            ImplantInstallKind kind = GetInstallKind(hediffDef);
            if (kind != ImplantInstallKind.Replacement && kind != ImplantInstallKind.Additive)
            {
                return false;
            }

            if (HasBionicModularityCompatibilityTag(hediffDef))
            {
                return true;
            }

            if (hediffDef.addedPartProps != null && hediffDef.addedPartProps.partEfficiency < 1f)
            {
                return false;
            }

            string defName = hediffDef.defName ?? string.Empty;
            return ContainsAny(defName,
                "Bionic",
                "Archotech",
                "Synthetic",
                "Advanced",
                "Power",
                "Modular",
                "Mechaneural",
                "Anima",
                "DrillArm",
                "FieldHand");
        }

        private static bool HasBionicModularityCompatibilityTag(HediffDef hediffDef)
        {
            if (hediffDef == null || hediffDef.tags == null)
            {
                return false;
            }

            for (int i = 0; i < hediffDef.tags.Count; i++)
            {
                string tag = hediffDef.tags[i];
                if (tag == BionicModuleBaseTag || (!string.IsNullOrEmpty(tag) && tag.StartsWith("BM_BionicModuleTag_", StringComparison.Ordinal)))
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

        private static bool HasShoulderWeaponImplant(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff != null && IsShoulderWeaponImplant(hediff.def))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsShoulderWeaponImplant(HediffDef hediffDef)
        {
            if (hediffDef == null || string.IsNullOrEmpty(hediffDef.defName))
            {
                return false;
            }

            string defName = hediffDef.defName;
            if (defName.IndexOf("shoulder", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            for (int i = 0; i < ShoulderWeaponKeywords.Length; i++)
            {
                if (defName.IndexOf(ShoulderWeaponKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAuxiliaryAiBrainImplant(HediffDef hediffDef)
        {
            if (hediffDef == null || string.IsNullOrEmpty(hediffDef.defName))
            {
                return false;
            }

            return AuxiliaryAiBrainImplantDefNames.Contains(hediffDef.defName)
                || hediffDef.defName.StartsWith("EPIA_AuxiliaryAI_", StringComparison.Ordinal);
        }

        private static bool HasAuxiliaryAiBrainImplant(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff != null && IsAuxiliaryAiBrainImplant(hediff.def))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBodyPartImplantCapReached(Pawn pawn, string bodyPartDefName)
        {
            int cap = GetBodyPartImplantCap(bodyPartDefName);
            return cap >= 0 && CountImplantsOnBodyPart(pawn, bodyPartDefName) >= cap;
        }

        private static int GetBodyPartImplantCap(string bodyPartDefName)
        {
            BetterRaidsSettings settings = BetterRaidsMod.Settings;
            if (bodyPartDefName == "Brain")
            {
                return settings != null ? settings.MaxBrainImplants : BetterRaidsSettings.DefaultMaxBrainImplants;
            }

            if (bodyPartDefName == "Rib")
            {
                return settings != null ? settings.MaxRibImplants : BetterRaidsSettings.DefaultMaxRibImplants;
            }

            if (bodyPartDefName == "Torso")
            {
                return settings != null ? settings.MaxTorsoImplants : BetterRaidsSettings.DefaultMaxTorsoImplants;
            }

            return -1;
        }

        private static int CountImplantsOnBodyPart(Pawn pawn, string bodyPartDefName)
        {
            if (pawn == null || string.IsNullOrEmpty(bodyPartDefName) || pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.hediffs == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (hediff == null || hediff.Part == null || hediff.Part.def == null || hediff.Part.def.defName != bodyPartDefName)
                {
                    continue;
                }

                if (IsCountedImplantHediff(hediff.def))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsCountedImplantHediff(HediffDef hediffDef)
        {
            if (hediffDef == null || IsBetterRaidsProtocol(hediffDef))
            {
                return false;
            }

            return GetInstallKind(hediffDef) != ImplantInstallKind.Unknown;
        }

        private static bool IsBetterRaidsProtocol(HediffDef hediffDef)
        {
            return hediffDef != null
                && (hediffDef.defName == "BetterRaids_AcidProtocol"
                    || hediffDef.defName == "BetterRaids_ExplosiveProtocol"
                    || hediffDef.defName == "BetterRaids_EliteMarker"
                    || hediffDef.defName == "BetterRaids_CommanderMarker");
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

            if (ShouldMatureAdjustmentForRaider(hediff.def) && hediff.Severity < MaturedAdjustmentSeverity)
            {
                hediff.Severity = MaturedAdjustmentSeverity;
            }

            if ((HasHediffTag(hediff.def, "ExtraLeftArm") || HasHediffTag(hediff.def, "ExtraRightArm")) && hediff.def.maxSeverity > hediff.Severity)
            {
                hediff.Severity = hediff.def.maxSeverity;
            }
        }

        private static bool ShouldMatureAdjustmentForRaider(HediffDef hediffDef)
        {
            if (hediffDef == null || string.IsNullOrEmpty(hediffDef.defName))
            {
                return false;
            }

            return hediffDef.defName.StartsWith("EPOE_InstinctOptimized", StringComparison.Ordinal)
                || hediffDef.defName == "EPOE_OrganicOptimized";
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

        private static float RollElitePawnRatio()
        {
            return Rand.Range(MinElitePawnRatio, MaxElitePawnRatio);
        }

        private static int CalculateEliteCount(int eligiblePawnCount, float elitePawnRatio)
        {
            if (eligiblePawnCount <= 0)
            {
                return 0;
            }

            int count = (int)Math.Round(eligiblePawnCount * elitePawnRatio);
            if (count < 1)
            {
                count = 1;
            }

            return Math.Min(count, eligiblePawnCount);
        }

        private static int RollHigherTierImplantAllowance()
        {
            float roll = Rand.Value;
            if (roll < TwoHigherTierImplantsChance)
            {
                return 2;
            }

            if (roll < TwoHigherTierImplantsChance + OneHigherTierImplantChance)
            {
                return 1;
            }

            return 0;
        }

        private static EliteRole DetermineEliteRole(Pawn pawn, bool commander)
        {
            if (commander)
            {
                return EliteRole.Commander;
            }

            string kindDefName = SafeDefName(pawn != null ? pawn.kindDef : null);
            string weaponDefName = GetPrimaryWeaponDefName(pawn);

            if (IsMeleePawn(pawn, kindDefName, weaponDefName))
            {
                return EliteRole.Brawler;
            }

            if (IsHeavyPawn(pawn, kindDefName))
            {
                return EliteRole.Heavy;
            }

            if (ContainsAny(weaponDefName, "Sniper", "LongRifle", "ChargeLance", "Lance", "Marksman", "AntiMateriel"))
            {
                return EliteRole.Sniper;
            }

            if (ContainsAny(weaponDefName, "Grenade", "Launcher", "EMP", "Smoke", "Firefoam", "Incendiary", "Molotov"))
            {
                return EliteRole.Support;
            }

            return EliteRole.Assault;
        }

        private static void EnsureEmpireMeleeWeapons(List<Pawn> pawns)
        {
            if (pawns == null)
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!ShouldForceEmpireMeleeWeapon(pawn))
                {
                    continue;
                }

                ThingWithComps weapon = MakeEmpireMeleeWeapon();
                if (weapon == null)
                {
                    continue;
                }

                pawn.equipment.AddEquipment(weapon);
                Log.Message("[BetterRaids] Added fallback Empire melee weapon"
                    + "\n  pawn=" + SafePawnLabel(pawn)
                    + "\n  kind=" + SafeDefName(pawn.kindDef)
                    + "\n  weapon=" + SafeDefName(weapon.def));
            }
        }

        private static bool ShouldForceEmpireMeleeWeapon(Pawn pawn)
        {
            if (pawn == null || pawn.kindDef == null || pawn.equipment == null || pawn.equipment.Primary != null)
            {
                return false;
            }

            if (pawn.Faction == null || pawn.Faction.def == null || pawn.Faction.def.defName != "Empire")
            {
                return false;
            }

            string kindDefName = pawn.kindDef.defName;
            return kindDefName == "Empire_Fighter_Champion" || kindDefName == "Empire_Fighter_StellicGuardMelee";
        }

        private static ThingWithComps MakeEmpireMeleeWeapon()
        {
            List<ThingDef> weaponDefs = new List<ThingDef>();
            AddThingDefIfPresent(weaponDefs, "MeleeWeapon_MonoSword");
            AddThingDefIfPresent(weaponDefs, "MeleeWeapon_Zeushammer");
            AddThingDefIfPresent(weaponDefs, "MeleeWeapon_PlasmaSword");
            AddThingDefIfPresent(weaponDefs, "MeleeWeapon_LongSword");
            if (weaponDefs.Count == 0)
            {
                return null;
            }

            ThingDef weaponDef = weaponDefs.RandomElement();
            ThingDef stuffDef = weaponDef.MadeFromStuff ? GenStuff.RandomStuffFor(weaponDef) : null;
            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef, stuffDef) as ThingWithComps;
            if (weapon == null)
            {
                return null;
            }

            CompQuality quality = weapon.TryGetComp<CompQuality>();
            if (quality != null)
            {
                quality.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
            }

            return weapon;
        }

        private static void AddThingDefIfPresent(List<ThingDef> thingDefs, string defName)
        {
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (thingDef != null)
            {
                thingDefs.Add(thingDef);
            }
        }

        private static bool IsMeleePawn(Pawn pawn, string kindDefName, string weaponDefName)
        {
            if (ContainsAny(kindDefName, "Champion", "Melee", "Brawler", "Berserker"))
            {
                return true;
            }

            if (ContainsAny(weaponDefName, "Sword", "Knife", "Mace", "Spear", "Axe", "Zeushammer", "Monosword", "PlasmaSword", "Gladius"))
            {
                return true;
            }

            ThingWithComps primary = pawn != null && pawn.equipment != null ? pawn.equipment.Primary : null;
            return primary != null && primary.def != null && primary.def.IsMeleeWeapon;
        }

        private static bool IsHeavyPawn(Pawn pawn, string kindDefName)
        {
            if (ContainsAny(kindDefName, "Cataphract", "Heavy", "Centurion", "Tank"))
            {
                return true;
            }

            if (GetBasePawnCost(pawn) >= 140f)
            {
                return true;
            }

            if (pawn == null || pawn.apparel == null || pawn.apparel.WornApparel == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.apparel.WornApparel.Count; i++)
            {
                Apparel apparel = pawn.apparel.WornApparel[i];
                string apparelDefName = apparel != null && apparel.def != null ? apparel.def.defName : null;
                if (ContainsAny(apparelDefName, "Cataphract", "MarineArmor", "PowerArmor", "ReconArmor", "Heavy"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetPrimaryWeaponDefName(Pawn pawn)
        {
            ThingWithComps primary = pawn != null && pawn.equipment != null ? pawn.equipment.Primary : null;
            return primary != null && primary.def != null ? primary.def.defName : string.Empty;
        }

        private static List<ImplantCatalogLogger.ImplantUpgradeCandidate> GetCachedEliteUpgradeCandidates(string techTierName, string factionScopeName)
        {
            string cacheKey = BuildEliteCandidateCacheKey(techTierName, factionScopeName, true);
            List<ImplantCatalogLogger.ImplantUpgradeCandidate> cached;
            if (EliteCandidateCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            List<ImplantCatalogLogger.ImplantUpgradeCandidate> candidates = ImplantCatalogLogger.GetEliteUpgradeCandidates(techTierName, factionScopeName);
            EliteCandidateCache[cacheKey] = candidates;
            return candidates;
        }

        private static List<ImplantCatalogLogger.ImplantUpgradeCandidate> GetCachedDirectEliteUpgradeCandidates(string techTierName, string factionScopeName)
        {
            string cacheKey = BuildEliteCandidateCacheKey(techTierName, factionScopeName, false);
            List<ImplantCatalogLogger.ImplantUpgradeCandidate> cached;
            if (DirectEliteCandidateCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            List<ImplantCatalogLogger.ImplantUpgradeCandidate> candidates = ImplantCatalogLogger.GetDirectEliteUpgradeCandidates(techTierName, factionScopeName);
            DirectEliteCandidateCache[cacheKey] = candidates;
            return candidates;
        }

        private static string BuildEliteCandidateCacheKey(string techTierName, string factionScopeName, bool includeFallback)
        {
            BetterRaidsSettings settings = BetterRaidsMod.Settings;
            int fallbackDepth = includeFallback && settings != null
                ? settings.MaxFallbackTechTierDrop
                : -1;

            return (techTierName ?? "null") + "|" + (factionScopeName ?? "null") + "|" + fallbackDepth;
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

        private static string SafeBodyPartLabel(BodyPartRecord part)
        {
            return part != null ? part.Label : "null";
        }

        private static bool IsOneOf(string value, params string[] candidates)
        {
            if (string.IsNullOrEmpty(value) || candidates == null)
            {
                return false;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (value == candidates[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAny(string value, params string[] keywords)
        {
            if (string.IsNullOrEmpty(value) || keywords == null)
            {
                return false;
            }

            for (int i = 0; i < keywords.Length; i++)
            {
                if (!string.IsNullOrEmpty(keywords[i]) && value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class EliteUpgradeResult
        {
            public float Spent;
            public string Protocol = "none";
            public int HigherTierImplantsUsed;
            public readonly List<string> Implants = new List<string>();
            public readonly List<string> Modules = new List<string>();
        }

        private sealed class WeightedImplantCandidate
        {
            public readonly ImplantCatalogLogger.ImplantUpgradeCandidate Candidate;
            public readonly float Weight;

            public WeightedImplantCandidate(ImplantCatalogLogger.ImplantUpgradeCandidate candidate, float weight)
            {
                Candidate = candidate;
                Weight = Math.Max(0.01f, weight);
            }
        }

        private sealed class WeightedModuleCandidate
        {
            public readonly BionicModuleCandidate Candidate;
            public readonly float Weight;

            public WeightedModuleCandidate(BionicModuleCandidate candidate, float weight)
            {
                Candidate = candidate;
                Weight = Math.Max(0.01f, weight);
            }
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
