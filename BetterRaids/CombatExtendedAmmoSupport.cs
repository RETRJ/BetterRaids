using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace BetterRaids
{
    internal static class CombatExtendedAmmoSupport
    {
        private const int MinMagazineCount = 2;
        private const int MaxMagazineCount = 2;
        private const string CombatExtendedPackageId = "ceteam.combatextended";
        private const string PraetorianPawnKindDefName = "BetterRaids_Empire_WarcasketPraetorian";

        private static bool? combatExtendedLoaded;
        private static bool warnedReflectionFailure;

        public static void EnsureRaidAmmo(List<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0 || !IsCombatExtendedLoaded() || !IsAmmoSystemEnabled())
            {
                return;
            }

            int updated = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (TryEnsurePawnAmmo(pawns[i]))
                {
                    updated++;
                }
            }

            if (updated > 0)
            {
                Log.Message("[BetterRaids] Combat Extended ammo fallback supplied " + updated + " elite pawn(s).");
            }
        }

        private static bool TryEnsurePawnAmmo(Pawn pawn)
        {
            try
            {
                if (!ShouldSupplyAmmo(pawn) || pawn.equipment == null || pawn.equipment.Primary == null || pawn.inventory == null)
                {
                    return false;
                }

                object ammoUser = FindCompByFullName(pawn.equipment.Primary.AllComps, "CombatExtended.CompAmmoUser");
                if (ammoUser == null || !GetBoolProperty(ammoUser, "UseAmmo"))
                {
                    return false;
                }

                ThingDef ammoDef = GetPropertyValue(ammoUser, "CurrentAmmo") as ThingDef;
                if (ammoDef == null)
                {
                    InvokeResetAmmoCount(ammoUser);
                    ammoDef = GetPropertyValue(ammoUser, "CurrentAmmo") as ThingDef;
                }

                if (ammoDef == null)
                {
                    return false;
                }

                int magazineSize = Math.Max(Math.Max(GetIntProperty(ammoUser, "MagSizeOverride"), GetIntProperty(ammoUser, "MagSize")), 1);
                int targetCount = magazineSize * Rand.RangeInclusive(MinMagazineCount, MaxMagazineCount);
                int currentCount = CountInventory(pawn, ammoDef);
                if (currentCount >= targetCount)
                {
                    return false;
                }

                Thing ammo = ThingMaker.MakeThing(ammoDef);
                ammo.stackCount = targetCount - currentCount;
                if (!pawn.inventory.innerContainer.TryAdd(ammo))
                {
                    return false;
                }

                UpdateCombatExtendedInventory(pawn);
                return true;
            }
            catch (Exception exception)
            {
                if (!warnedReflectionFailure)
                {
                    warnedReflectionFailure = true;
                    Log.Warning("[BetterRaids] Failed to apply Combat Extended ammo fallback: " + exception);
                }

                return false;
            }
        }

        private static bool ShouldSupplyAmmo(Pawn pawn)
        {
            if (pawn == null || pawn.kindDef == null)
            {
                return false;
            }

            if (pawn.kindDef.defName == PraetorianPawnKindDefName)
            {
                return true;
            }

            return pawn.health != null
                && pawn.health.hediffSet != null
                && (pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("BetterRaids_AcidProtocol"))
                    || pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("BetterRaids_ExplosiveProtocol")));
        }

        private static bool IsCombatExtendedLoaded()
        {
            if (combatExtendedLoaded.HasValue)
            {
                return combatExtendedLoaded.Value;
            }

            combatExtendedLoaded = false;
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (mod != null
                    && mod.PackageId != null
                    && string.Equals(mod.PackageId, CombatExtendedPackageId, StringComparison.OrdinalIgnoreCase))
                {
                    combatExtendedLoaded = true;
                    break;
                }
            }

            return combatExtendedLoaded.Value;
        }

        private static bool IsAmmoSystemEnabled()
        {
            Type controllerType = FindType("CombatExtended.Controller");
            if (controllerType == null)
            {
                return false;
            }

            FieldInfo settingsField = controllerType.GetField("settings", BindingFlags.Public | BindingFlags.Static);
            object settings = settingsField != null ? settingsField.GetValue(null) : null;
            if (settings == null)
            {
                return false;
            }

            PropertyInfo property = settings.GetType().GetProperty("EnableAmmoSystem", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(bool))
            {
                return (bool)property.GetValue(settings, null);
            }

            FieldInfo field = settings.GetType().GetField("EnableAmmoSystem", BindingFlags.Public | BindingFlags.Instance);
            return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(settings);
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static object FindCompByFullName(List<ThingComp> comps, string fullName)
        {
            if (comps == null)
            {
                return null;
            }

            for (int i = 0; i < comps.Count; i++)
            {
                ThingComp comp = comps[i];
                if (comp != null && comp.GetType().FullName == fullName)
                {
                    return comp;
                }
            }

            return null;
        }

        private static bool GetBoolProperty(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(instance, null);
        }

        private static int GetIntProperty(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int))
            {
                return (int)property.GetValue(instance, null);
            }

            return 0;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property != null ? property.GetValue(instance, null) : null;
        }

        private static void InvokeResetAmmoCount(object ammoUser)
        {
            MethodInfo method = ammoUser.GetType().GetMethod("ResetAmmoCount", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(ammoUser, new object[] { null });
            }
        }

        private static int CountInventory(Pawn pawn, ThingDef thingDef)
        {
            int count = 0;
            List<Thing> innerList = pawn.inventory.innerContainer.InnerListForReading;
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing thing = innerList[i];
                if (thing != null && thing.def == thingDef)
                {
                    count += thing.stackCount;
                }
            }

            return count;
        }

        private static void UpdateCombatExtendedInventory(Pawn pawn)
        {
            object inventory = FindCompByFullName(pawn.AllComps, "CombatExtended.CompInventory");
            if (inventory == null)
            {
                return;
            }

            MethodInfo method = inventory.GetType().GetMethod("UpdateInventory", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(inventory, null);
            }
        }
    }
}
