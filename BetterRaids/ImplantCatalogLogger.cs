using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace BetterRaids
{
    internal static class ImplantCatalogLogger
    {
        private static readonly List<ImplantPoolRule> VanillaRules = new List<ImplantPoolRule>
        {
            // Low-tech replacements. "Tribal" mods usually map this to RimWorld's Neolithic tech level.
            Rule("PegLeg", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            Rule("PegLeg", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            Rule("PegLeg", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            Rule("WoodenHand", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            Rule("WoodenHand", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            Rule("WoodenHand", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            Rule("WoodenFoot", ImplantTechTier.Animal, ImplantUsefulness.CoreCombat),
            Rule("WoodenFoot", ImplantTechTier.Neolithic, ImplantUsefulness.CoreCombat),
            Rule("WoodenFoot", ImplantTechTier.Medieval, ImplantUsefulness.CoreCombat),
            Rule("Denture", ImplantTechTier.Neolithic, ImplantUsefulness.LowCombat),
            Rule("Denture", ImplantTechTier.Medieval, ImplantUsefulness.LowCombat),

            // Industrial prosthetics. Worse than natural/bionic, but believable for industrial factions.
            Rule("SimpleProstheticLeg", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            Rule("SimpleProstheticArm", ImplantTechTier.Industrial, ImplantUsefulness.CoreCombat),
            Rule("SimpleProstheticHeart", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),
            Rule("CochlearImplant", ImplantTechTier.Industrial, ImplantUsefulness.SupportCombat),

            // Spacer combat bionics and common utility implants.
            Rule("BionicEye", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicArm", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicLeg", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicSpine", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("BionicHeart", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("BionicStomach", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("BionicEar", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("BionicTongue", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            Rule("BionicJaw", ImplantTechTier.Spacer, ImplantUsefulness.LowCombat),
            Rule("PowerClaw", ImplantTechTier.Spacer, ImplantUsefulness.CoreCombat),
            Rule("Painstopper", ImplantTechTier.Spacer, ImplantUsefulness.SupportCombat),
            Rule("Joywire", ImplantTechTier.Spacer, ImplantUsefulness.Special),
            Rule("DeathAcidifier", ImplantTechTier.Spacer, ImplantUsefulness.Special),

            // Ultra is intentionally below archotech. Core/WTL has no normal Ultra body-part replacements yet.

            // Archotech is its own post-Ultra pool.
            Rule("ArchotechEye", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            Rule("ArchotechArm", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat),
            Rule("ArchotechLeg", ImplantTechTier.Archotech, ImplantUsefulness.CoreCombat)
        };

        public static void LogCatalog()
        {
            try
            {
                Verse.Log.Message(BuildCatalogText(true));
            }
            catch (Exception exception)
            {
                Verse.Log.Warning("[BetterRaids] Failed to build implant tech pools: " + exception);
            }
        }

        public static string BuildSettingsCatalogText()
        {
            try
            {
                return BuildCatalogText(false);
            }
            catch (Exception exception)
            {
                return "[BetterRaids] Failed to build implant tech pools: " + exception;
            }
        }

        internal static List<string> GetTechTierNames()
        {
            return Enum.GetNames(typeof(ImplantTechTier)).ToList();
        }

        internal static List<ImplantTierSummary> GetTierSummaries()
        {
            List<ImplantPoolEntry> entries = BuildResolvedEntries();
            List<ImplantTierSummary> summaries = new List<ImplantTierSummary>();

            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                summaries.Add(new ImplantTierSummary(tier.ToString(), entries.Count(entry => entry.TechTier == tier)));
            }

            return summaries;
        }

        internal static List<string> GetBodyPartNames(string techTierName, bool includeFallback)
        {
            ImplantTechTier tier;
            if (!TryParseTier(techTierName, out tier))
            {
                return new List<string>();
            }

            List<ImplantPoolEntry> entries = BuildResolvedEntries();
            List<string> bodyParts = entries
                .Select(entry => entry.BodyPartDefName)
                .Where(part => !string.IsNullOrEmpty(part) && part != "unknown")
                .Distinct()
                .OrderBy(part => part)
                .ToList();

            if (!includeFallback)
            {
                return bodyParts
                    .Where(part => entries.Any(entry => entry.TechTier == tier && entry.BodyPartDefName == part))
                    .ToList();
            }

            return bodyParts
                .Where(part => GetEffectivePool(entries, tier, part).Entries.Count > 0)
                .ToList();
        }

        internal static ImplantPoolSelection GetSelectedPool(string techTierName, string bodyPartDefName, bool includeFallback)
        {
            ImplantTechTier tier;
            if (!TryParseTier(techTierName, out tier) || string.IsNullOrEmpty(bodyPartDefName))
            {
                return ImplantPoolSelection.Empty(techTierName, bodyPartDefName);
            }

            List<ImplantPoolEntry> entries = BuildResolvedEntries();
            EffectivePool pool = includeFallback
                ? GetEffectivePool(entries, tier, bodyPartDefName)
                : new EffectivePool(tier, entries
                    .Where(entry => entry.TechTier == tier && entry.BodyPartDefName == bodyPartDefName)
                    .OrderBy(entry => entry.Usefulness)
                    .ThenBy(entry => entry.DefName)
                    .ToList());

            return new ImplantPoolSelection(
                tier.ToString(),
                bodyPartDefName,
                pool.SourceTier.ToString(),
                pool.SourceTier != tier,
                pool.Entries.Select(ToViewEntry).ToList());
        }

        private static string BuildCatalogText(bool includeUnmapped)
        {
            List<ImplantPoolEntry> entries = BuildResolvedEntries();

            HashSet<string> mappedDefNames = new HashSet<string>(VanillaRules.Select(rule => rule.HediffDefName));
            List<HediffDef> unmapped = DefDatabase<HediffDef>.AllDefs
                .Where(IsImplantOrAddedPart)
                .Where(def => !mappedDefNames.Contains(def.defName))
                .OrderBy(def => GetSourceMod(def))
                .ThenBy(def => def.defName)
                .ToList();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[BetterRaids] Implant tech pools");
            builder.AppendLine("  tierSource=RimWorld TechLevel enum + World Tech Level/VFE naming");
            builder.AppendLine("  note=Neolithic is the vanilla enum value used for tribal tech");
            builder.AppendLine("  supportedTierMods:");
            builder.AppendLine("    WorldTechLevel=" + IsModLoaded("m00nl1ght.WorldTechLevel"));
            builder.AppendLine("    ProgressionCore=" + IsModLoaded("ferny.progressioncore"));
            builder.AppendLine("    VFETribals=" + IsModLoaded("oskarpotocki.vfe.tribals"));
            builder.AppendLine("  fallbackRule=same body part only, max previous tech tiers=" + GetMaxFallbackTechTierDrop());
            builder.AppendLine("  manualRules=" + VanillaRules.Count);
            builder.AppendLine("  resolvedRules=" + entries.Count);
            builder.AppendLine("  unmappedImplants=" + unmapped.Count);
            AppendTierSummary(builder, entries);
            AppendDirectPoolMatrix(builder, entries);
            AppendEffectivePoolMatrix(builder, entries);

            if (includeUnmapped)
            {
                AppendUnmappedSummary(builder, unmapped);
            }
            else
            {
                builder.AppendLine("  unmapped implants/prosthetics: hidden in Mod Config; press Recalculate to log full list");
            }

            return builder.ToString();
        }

        private static List<ImplantPoolEntry> BuildResolvedEntries()
        {
            return VanillaRules
                .Select(BuildEntry)
                .Where(entry => entry.HediffDef != null)
                .OrderBy(entry => entry.TechTier)
                .ThenBy(entry => entry.Usefulness)
                .ThenBy(entry => entry.DefName)
                .ToList();
        }

        private static ImplantPoolViewEntry ToViewEntry(ImplantPoolEntry entry)
        {
            return new ImplantPoolViewEntry(
                entry.DefName,
                entry.Label,
                entry.Usefulness.ToString(),
                entry.BodyPartDefName,
                FormatEfficiency(entry.Efficiency),
                entry.SourceMod);
        }

        private static bool TryParseTier(string techTierName, out ImplantTechTier tier)
        {
            if (!string.IsNullOrEmpty(techTierName))
            {
                foreach (ImplantTechTier candidate in Enum.GetValues(typeof(ImplantTechTier)))
                {
                    if (candidate.ToString() == techTierName)
                    {
                        tier = candidate;
                        return true;
                    }
                }
            }

            tier = ImplantTechTier.Spacer;
            return false;
        }

        private static int GetMaxFallbackTechTierDrop()
        {
            BetterRaidsSettings settings = BetterRaidsMod.Settings;
            int value = settings != null ? settings.MaxFallbackTechTierDrop : BetterRaidsSettings.DefaultMaxFallbackTechTierDrop;

            if (value < BetterRaidsSettings.MinFallbackTechTierDrop)
            {
                return BetterRaidsSettings.MinFallbackTechTierDrop;
            }

            if (value > BetterRaidsSettings.MaxFallbackTechTierDropLimit)
            {
                return BetterRaidsSettings.MaxFallbackTechTierDropLimit;
            }

            return value;
        }

        private static ImplantPoolRule Rule(string hediffDefName, ImplantTechTier techTier, ImplantUsefulness usefulness)
        {
            return new ImplantPoolRule(hediffDefName, techTier, usefulness);
        }

        private static ImplantPoolEntry BuildEntry(ImplantPoolRule rule)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(rule.HediffDefName);
            RecipeDef recipe = def != null ? FindInstallRecipe(def) : null;

            return new ImplantPoolEntry
            {
                HediffDef = def,
                DefName = rule.HediffDefName,
                Label = def != null ? def.label : "missing",
                TechTier = rule.TechTier,
                Usefulness = rule.Usefulness,
                BodyPartDefName = GetRecipeBodyPart(recipe),
                Efficiency = def != null && def.addedPartProps != null ? (float?)def.addedPartProps.partEfficiency : null,
                SourceMod = GetSourceMod(def)
            };
        }

        private static bool IsImplantOrAddedPart(HediffDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (def.addedPartProps != null)
            {
                return true;
            }

            Type hediffClass = def.hediffClass;
            return hediffClass != null && typeof(Hediff_Implant).IsAssignableFrom(hediffClass);
        }

        private static RecipeDef FindInstallRecipe(HediffDef hediffDef)
        {
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipe != null && recipe.addsHediff == hediffDef)
                {
                    return recipe;
                }
            }

            return null;
        }

        private static string GetRecipeBodyPart(RecipeDef recipe)
        {
            if (recipe == null || recipe.appliedOnFixedBodyParts == null || recipe.appliedOnFixedBodyParts.Count == 0)
            {
                return "unknown";
            }

            BodyPartDef partDef = recipe.appliedOnFixedBodyParts[0];
            return partDef != null ? partDef.defName : "unknown";
        }

        private static void AppendTierSummary(StringBuilder builder, List<ImplantPoolEntry> entries)
        {
            builder.AppendLine("  tech tiers:");
            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                builder.AppendLine("    " + tier + "=" + entries.Count(entry => entry.TechTier == tier));
            }
        }

        private static void AppendDirectPoolMatrix(StringBuilder builder, List<ImplantPoolEntry> entries)
        {
            builder.AppendLine("  direct pools by tech tier and body part:");
            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                builder.AppendLine("    " + tier + ":");
                AppendTierBodyPartPools(builder, entries.Where(entry => entry.TechTier == tier).ToList(), "      ");
            }
        }

        private static void AppendEffectivePoolMatrix(StringBuilder builder, List<ImplantPoolEntry> entries)
        {
            List<string> bodyParts = entries
                .Select(entry => entry.BodyPartDefName)
                .Where(part => !string.IsNullOrEmpty(part) && part != "unknown")
                .Distinct()
                .OrderBy(part => part)
                .ToList();

            builder.AppendLine("  effective pools with same-body-part fallback:");
            foreach (ImplantTechTier tier in Enum.GetValues(typeof(ImplantTechTier)))
            {
                builder.AppendLine("    " + tier + ":");
                bool any = false;

                for (int i = 0; i < bodyParts.Count; i++)
                {
                    EffectivePool pool = GetEffectivePool(entries, tier, bodyParts[i]);
                    if (pool.Entries.Count == 0)
                    {
                        continue;
                    }

                    any = true;
                    string fallbackText = pool.SourceTier == tier ? "direct" : "fallback=" + pool.SourceTier;
                    builder.AppendLine("      " + bodyParts[i] + " (" + fallbackText + "): " + FormatEntryList(pool.Entries));
                }

                if (!any)
                {
                    builder.AppendLine("      none");
                }
            }
        }

        private static void AppendTierBodyPartPools(StringBuilder builder, List<ImplantPoolEntry> entries, string indent)
        {
            if (entries.Count == 0)
            {
                builder.AppendLine(indent + "none");
                return;
            }

            foreach (IGrouping<string, ImplantPoolEntry> group in entries.GroupBy(entry => entry.BodyPartDefName).OrderBy(group => group.Key))
            {
                builder.AppendLine(indent + group.Key + ": " + FormatEntryList(group.ToList()));
            }
        }

        private static EffectivePool GetEffectivePool(List<ImplantPoolEntry> entries, ImplantTechTier tier, string bodyPartDefName)
        {
            ImplantTechTier current = tier;
            int fallbackDepth = 0;
            int maxFallbackDepth = GetMaxFallbackTechTierDrop();

            while (true)
            {
                List<ImplantPoolEntry> direct = entries
                    .Where(entry => entry.TechTier == current && entry.BodyPartDefName == bodyPartDefName)
                    .OrderBy(entry => entry.Usefulness)
                    .ThenBy(entry => entry.DefName)
                    .ToList();

                if (direct.Count > 0)
                {
                    return new EffectivePool(current, direct);
                }

                if (fallbackDepth >= maxFallbackDepth)
                {
                    return new EffectivePool(tier, new List<ImplantPoolEntry>());
                }

                if (!TryGetPreviousTier(current, out current))
                {
                    return new EffectivePool(tier, new List<ImplantPoolEntry>());
                }

                fallbackDepth++;
            }
        }

        private static bool TryGetPreviousTier(ImplantTechTier tier, out ImplantTechTier previous)
        {
            switch (tier)
            {
                case ImplantTechTier.Neolithic:
                    previous = ImplantTechTier.Animal;
                    return true;
                case ImplantTechTier.Medieval:
                    previous = ImplantTechTier.Neolithic;
                    return true;
                case ImplantTechTier.Industrial:
                    previous = ImplantTechTier.Medieval;
                    return true;
                case ImplantTechTier.Spacer:
                    previous = ImplantTechTier.Industrial;
                    return true;
                case ImplantTechTier.Ultra:
                    previous = ImplantTechTier.Spacer;
                    return true;
                case ImplantTechTier.Archotech:
                    previous = ImplantTechTier.Ultra;
                    return true;
                default:
                    previous = tier;
                    return false;
            }
        }

        private static string FormatEntryList(List<ImplantPoolEntry> entries)
        {
            return string.Join(", ", entries.Select(FormatEntry).ToArray());
        }

        private static string FormatEntry(ImplantPoolEntry entry)
        {
            return entry.DefName
                + "[" + entry.Usefulness
                + ", eff=" + FormatEfficiency(entry.Efficiency)
                + ", mod=" + entry.SourceMod + "]";
        }

        private static void AppendUnmappedSummary(StringBuilder builder, List<HediffDef> unmapped)
        {
            builder.AppendLine("  unmapped implants/prosthetics:");
            foreach (IGrouping<string, HediffDef> group in unmapped.GroupBy(GetSourceMod).OrderBy(group => group.Key))
            {
                builder.AppendLine("    " + group.Key + "=" + group.Count());
                foreach (HediffDef def in group.Take(40))
                {
                    builder.AppendLine("      " + def.defName + " | label=" + def.label + " | efficiency=" + FormatEfficiency(def.addedPartProps != null ? (float?)def.addedPartProps.partEfficiency : null));
                }

                int omitted = group.Count() - 40;
                if (omitted > 0)
                {
                    builder.AppendLine("      ... omitted " + omitted + " more");
                }
            }
        }

        private static string GetSourceMod(Def def)
        {
            if (def == null || def.modContentPack == null)
            {
                return "Core/unknown";
            }

            string packageId = def.modContentPack.PackageId;
            string name = def.modContentPack.Name;

            if (string.IsNullOrEmpty(packageId))
            {
                return string.IsNullOrEmpty(name) ? "Core/unknown" : name;
            }

            return string.IsNullOrEmpty(name) ? packageId : name + " (" + packageId + ")";
        }

        private static bool IsModLoaded(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return false;
            }

            string normalized = packageId.ToLowerInvariant();
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (mod != null && mod.PackageId != null && mod.PackageId.ToLowerInvariant() == normalized)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatEfficiency(float? efficiency)
        {
            return efficiency.HasValue ? efficiency.Value.ToString("0.##") : "n/a";
        }

        internal sealed class ImplantTierSummary
        {
            public readonly string TechTier;
            public readonly int DirectImplantCount;

            public ImplantTierSummary(string techTier, int directImplantCount)
            {
                TechTier = techTier;
                DirectImplantCount = directImplantCount;
            }
        }

        internal sealed class ImplantPoolSelection
        {
            public readonly string RequestedTechTier;
            public readonly string BodyPartDefName;
            public readonly string SourceTechTier;
            public readonly bool IsFallback;
            public readonly List<ImplantPoolViewEntry> Entries;

            public ImplantPoolSelection(string requestedTechTier, string bodyPartDefName, string sourceTechTier, bool isFallback, List<ImplantPoolViewEntry> entries)
            {
                RequestedTechTier = requestedTechTier;
                BodyPartDefName = bodyPartDefName;
                SourceTechTier = sourceTechTier;
                IsFallback = isFallback;
                Entries = entries;
            }

            public static ImplantPoolSelection Empty(string requestedTechTier, string bodyPartDefName)
            {
                return new ImplantPoolSelection(requestedTechTier, bodyPartDefName, requestedTechTier, false, new List<ImplantPoolViewEntry>());
            }
        }

        internal sealed class ImplantPoolViewEntry
        {
            public readonly string DefName;
            public readonly string Label;
            public readonly string Usefulness;
            public readonly string BodyPartDefName;
            public readonly string Efficiency;
            public readonly string SourceMod;

            public ImplantPoolViewEntry(string defName, string label, string usefulness, string bodyPartDefName, string efficiency, string sourceMod)
            {
                DefName = defName;
                Label = label;
                Usefulness = usefulness;
                BodyPartDefName = bodyPartDefName;
                Efficiency = efficiency;
                SourceMod = sourceMod;
            }
        }

        private sealed class ImplantPoolRule
        {
            public readonly string HediffDefName;
            public readonly ImplantTechTier TechTier;
            public readonly ImplantUsefulness Usefulness;

            public ImplantPoolRule(string hediffDefName, ImplantTechTier techTier, ImplantUsefulness usefulness)
            {
                HediffDefName = hediffDefName;
                TechTier = techTier;
                Usefulness = usefulness;
            }
        }

        private sealed class ImplantPoolEntry
        {
            public HediffDef HediffDef;
            public string DefName;
            public string Label;
            public ImplantTechTier TechTier;
            public ImplantUsefulness Usefulness;
            public string BodyPartDefName;
            public float? Efficiency;
            public string SourceMod;
        }

        private sealed class EffectivePool
        {
            public readonly ImplantTechTier SourceTier;
            public readonly List<ImplantPoolEntry> Entries;

            public EffectivePool(ImplantTechTier sourceTier, List<ImplantPoolEntry> entries)
            {
                SourceTier = sourceTier;
                Entries = entries;
            }
        }

        private enum ImplantTechTier
        {
            Animal,
            Neolithic,
            Medieval,
            Industrial,
            Spacer,
            Ultra,
            Archotech
        }

        private enum ImplantUsefulness
        {
            CoreCombat,
            SupportCombat,
            LowCombat,
            Special
        }
    }
}
