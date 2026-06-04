using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRaids
{
    internal static class BetterRaidsSettingsWindow
    {
        private static readonly Color PanelColor = new Color(0.13f, 0.13f, 0.13f, 0.45f);
        private static readonly Color CardColor = new Color(0.18f, 0.18f, 0.18f, 0.55f);
        private static readonly Color FallbackColor = new Color(0.28f, 0.22f, 0.12f, 0.55f);

        private static Vector2 implantScrollPosition;
        private static string selectedFactionScope = ImplantCatalogLogger.GlobalFactionScopeName;
        private static string selectedTechTier = "Spacer";
        private static string selectedBodyPart;
        private static bool includeFallback = true;

        public static void Draw(Rect inRect, BetterRaidsSettings settings)
        {
            if (settings == null)
            {
                Widgets.Label(inRect, "BetterRaids settings are not loaded.");
                return;
            }

            EnsureSelection();

            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 112f);
            Rect selectorRect = new Rect(inRect.x, headerRect.yMax + 8f, inRect.width, 78f);
            Rect contentRect = new Rect(inRect.x, selectorRect.yMax + 8f, inRect.width, inRect.height - headerRect.height - selectorRect.height - 16f);

            DrawHeader(headerRect, settings);
            DrawSelectors(selectorRect);
            DrawContent(contentRect);
        }

        private static void DrawHeader(Rect rect, BetterRaidsSettings settings)
        {
            DrawPanel(rect, PanelColor);

            Rect titleRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 28f);
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "BetterRaids Implant Pools");
            Text.Font = previousFont;

            Widgets.Label(new Rect(rect.x + 12f, rect.y + 40f, rect.width - 24f, 24f),
                "Fallback: same body part only, walking down previous tech tiers.");

            Rect row = new Rect(rect.x + 12f, rect.y + 72f, rect.width - 24f, 32f);
            Widgets.Label(new Rect(row.x, row.y + 6f, 230f, row.height), "Max fallback tech-tier drop: " + settings.MaxFallbackTechTierDrop);

            if (Widgets.ButtonText(new Rect(row.x + 240f, row.y, 56f, row.height), "-1"))
            {
                settings.MaxFallbackTechTierDrop--;
                SaveSettings(settings);
                EnsureSelection();
            }

            if (Widgets.ButtonText(new Rect(row.x + 304f, row.y, 56f, row.height), "+1"))
            {
                settings.MaxFallbackTechTierDrop++;
                SaveSettings(settings);
                EnsureSelection();
            }

            if (Widgets.ButtonText(new Rect(row.x + 376f, row.y, 150f, row.height), "Recalculate"))
            {
                SaveSettings(settings);
                ImplantCatalogLogger.LogCatalog();
                EnsureSelection();
                Messages.Message("[BetterRaids] Implant pools recalculated.", MessageTypeDefOf.TaskCompletion, false);
            }
        }

        private static void DrawSelectors(Rect rect)
        {
            DrawPanel(rect, PanelColor);

            Rect factionRect = new Rect(rect.x + 12f, rect.y + 34f, 170f, 32f);
            Rect tierRect = new Rect(factionRect.xMax + 12f, factionRect.y, 150f, 32f);
            Rect bodyPartRect = new Rect(tierRect.xMax + 12f, tierRect.y, 190f, 32f);
            Rect fallbackRect = new Rect(bodyPartRect.xMax + 18f, tierRect.y + 6f, 210f, 24f);

            Widgets.Label(new Rect(factionRect.x, rect.y + 10f, factionRect.width, 24f), "Faction scope");
            Widgets.Label(new Rect(tierRect.x, rect.y + 10f, tierRect.width, 24f), "Tech level");
            Widgets.Label(new Rect(bodyPartRect.x, rect.y + 10f, bodyPartRect.width, 24f), "Body part");

            DrawDropdown(factionRect, selectedFactionScope, ImplantCatalogLogger.GetFactionScopeNames(), delegate(string value)
            {
                selectedFactionScope = value;
                selectedBodyPart = null;
                EnsureSelection();
            });

            DrawDropdown(tierRect, selectedTechTier, ImplantCatalogLogger.GetTechTierNames(), delegate(string value)
            {
                selectedTechTier = value;
                selectedBodyPart = null;
                EnsureSelection();
            });

            DrawDropdown(bodyPartRect, selectedBodyPart ?? "none", ImplantCatalogLogger.GetBodyPartNames(selectedTechTier, includeFallback, selectedFactionScope), delegate(string value)
            {
                selectedBodyPart = value;
                implantScrollPosition = Vector2.zero;
            });

            bool previousIncludeFallback = includeFallback;
            Widgets.CheckboxLabeled(fallbackRect, "Show effective fallback pool", ref includeFallback);
            if (previousIncludeFallback != includeFallback)
            {
                selectedBodyPart = null;
                EnsureSelection();
            }
        }

        private static void DrawContent(Rect rect)
        {
            float summaryWidth = 190f;
            Rect summaryRect = new Rect(rect.x, rect.y, summaryWidth, rect.height);
            Rect poolRect = new Rect(summaryRect.xMax + 8f, rect.y, rect.width - summaryWidth - 8f, rect.height);

            DrawTierSummary(summaryRect);
            DrawSelectedPool(poolRect);
        }

        private static void DrawTierSummary(Rect rect)
        {
            DrawPanel(rect, PanelColor);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 24f), "Direct rules by tier");

            float y = rect.y + 38f;
            foreach (ImplantCatalogLogger.ImplantTierSummary summary in ImplantCatalogLogger.GetTierSummaries(selectedFactionScope))
            {
                Rect row = new Rect(rect.x + 8f, y, rect.width - 16f, 28f);
                if (summary.TechTier == selectedTechTier)
                {
                    Widgets.DrawHighlight(row);
                }

                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 56f, row.height), summary.TechTier);
                Widgets.Label(new Rect(row.xMax - 44f, row.y + 4f, 38f, row.height), summary.DirectImplantCount.ToString());
                y += 30f;
            }
        }

        private static void DrawSelectedPool(Rect rect)
        {
            DrawPanel(rect, PanelColor);

            ImplantCatalogLogger.ImplantPoolSelection selection = ImplantCatalogLogger.GetSelectedPool(selectedTechTier, selectedBodyPart, includeFallback, selectedFactionScope);
            string sourceText = selection.IsFallback ? "fallback from " + selection.SourceTechTier : "direct";
            Rect titleRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 28f);
            Widgets.Label(titleRect, selectedFactionScope + " / " + selectedTechTier + " / " + (selectedBodyPart ?? "none") + " (" + sourceText + ")");

            Rect outRect = new Rect(rect.x + 8f, rect.y + 42f, rect.width - 16f, rect.height - 50f);
            float viewHeight = Math.Max(outRect.height + 1f, 48f + selection.Entries.Count * 82f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(outRect, ref implantScrollPosition, viewRect);
            if (selection.Entries.Count == 0)
            {
                Widgets.Label(new Rect(8f, 8f, viewRect.width - 16f, 60f),
                    "No implants for this tech level/body part with current fallback depth.");
            }
            else
            {
                float y = 6f;
                foreach (ImplantCatalogLogger.ImplantPoolViewEntry entry in selection.Entries)
                {
                    DrawImplantCard(new Rect(6f, y, viewRect.width - 12f, 74f), entry, selection.IsFallback);
                    y += 82f;
                }
            }

            Widgets.EndScrollView();
        }

        private static void DrawImplantCard(Rect rect, ImplantCatalogLogger.ImplantPoolViewEntry entry, bool fallback)
        {
            DrawPanel(rect, fallback ? FallbackColor : CardColor);

            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 24f), entry.DefName + " - " + entry.Label);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 30f, rect.width - 20f, 18f),
                "usefulness=" + entry.Usefulness + " | efficiency=" + entry.Efficiency + " | bodyPart=" + entry.BodyPartDefName);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 50f, rect.width - 20f, 18f), "scope=" + entry.FactionScope + " | patch=" + entry.PackageId + " | mod=" + entry.SourceMod);
            Text.Font = previousFont;
        }

        private static void DrawDropdown(Rect rect, string label, List<string> options, Action<string> onSelected)
        {
            if (Widgets.ButtonText(rect, label))
            {
                List<FloatMenuOption> menuOptions = new List<FloatMenuOption>();
                foreach (string option in options)
                {
                    string captured = option;
                    menuOptions.Add(new FloatMenuOption(captured, delegate { onSelected(captured); }));
                }

                if (menuOptions.Count == 0)
                {
                    menuOptions.Add(new FloatMenuOption("none", null));
                }

                Find.WindowStack.Add(new FloatMenu(menuOptions));
            }
        }

        private static void EnsureSelection()
        {
            List<string> tiers = ImplantCatalogLogger.GetTechTierNames();
            if (!tiers.Contains(selectedTechTier))
            {
                selectedTechTier = tiers.Count > 0 ? tiers[0] : "Spacer";
            }

            List<string> factionScopes = ImplantCatalogLogger.GetFactionScopeNames();
            if (!factionScopes.Contains(selectedFactionScope))
            {
                selectedFactionScope = factionScopes.Count > 0 ? factionScopes[0] : ImplantCatalogLogger.GlobalFactionScopeName;
            }

            List<string> bodyParts = ImplantCatalogLogger.GetBodyPartNames(selectedTechTier, includeFallback, selectedFactionScope);
            if (bodyParts.Count == 0)
            {
                selectedBodyPart = null;
                return;
            }

            if (string.IsNullOrEmpty(selectedBodyPart) || !bodyParts.Contains(selectedBodyPart))
            {
                selectedBodyPart = bodyParts[0];
                implantScrollPosition = Vector2.zero;
            }
        }

        private static void SaveSettings(BetterRaidsSettings settings)
        {
            settings.ClampValues();
            BetterRaidsMod.Instance.WriteSettings();
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            Widgets.DrawBoxSolid(rect, color);
            Widgets.DrawBox(rect);
        }
    }
}
