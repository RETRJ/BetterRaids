using HarmonyLib;
using UnityEngine;
using Verse;

namespace BetterRaids
{
    [HarmonyPatch(typeof(PawnUIOverlay), "DrawPawnGUIOverlay")]
    internal static class EliteRaidVisualMarkerPatch
    {
        private static readonly Color EliteColor = new Color(0.35f, 0.85f, 1f);
        private static readonly Color CommanderColor = new Color(1f, 0.78f, 0.15f);

        private static readonly System.Reflection.FieldInfo PawnField =
            AccessTools.Field(typeof(PawnUIOverlay), "pawn");

        private static void Postfix(PawnUIOverlay __instance)
        {
            Pawn pawn = PawnField != null ? PawnField.GetValue(__instance) as Pawn : null;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            if (HasHediff(pawn, "BetterRaids_CommanderMarker"))
            {
                DrawMarker(pawn, "CMD", CommanderColor);
                return;
            }

            if (HasHediff(pawn, "BetterRaids_EliteMarker"))
            {
                DrawMarker(pawn, "ELITE", EliteColor);
            }
        }

        private static void DrawMarker(Pawn pawn, string text, Color color)
        {
            Vector2 position = GenMapUI.LabelDrawPosFor(pawn, -0.6f);
            position.y -= 24f;
            GenMapUI.DrawThingLabel(position, text, color);
        }

        private static bool HasHediff(Pawn pawn, string hediffDefName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
            return def != null && pawn.health.hediffSet.HasHediff(def);
        }
    }
}
