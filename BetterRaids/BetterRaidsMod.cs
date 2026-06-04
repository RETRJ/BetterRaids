using HarmonyLib;
using UnityEngine;
using Verse;

namespace BetterRaids
{
    public class BetterRaidsMod : Mod
    {
        internal static BetterRaidsMod Instance;
        internal static BetterRaidsSettings Settings;

        public BetterRaidsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<BetterRaidsSettings>();
            new Harmony("retrj.betterraids").PatchAll();
            Log.Message("[BetterRaids] Loaded successfully. Harmony patches applied.");
            LongEventHandler.ExecuteWhenFinished(ImplantCatalogLogger.LogCatalog);
        }

        public override string SettingsCategory()
        {
            return "BetterRaids";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            BetterRaidsSettingsWindow.Draw(inRect, Settings);
        }
    }
}
