using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SPTEssentials.Client.Reload;

internal sealed class MagazineRetentionModule : ClientModule
{
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".magazineretention");

    protected override string Name => "Quickload Mag Saver";

    protected override void Enable()
    {
        Patch(_harmony, typeof(RetainMagazinePatch));
    }

    [HarmonyPatch]
    private static class RetainMagazinePatch
    {
        private static bool _warningLogged;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Player.FirearmController.ReloadExternalMagResult),
                nameof(Player.FirearmController.ReloadExternalMagResult.Run));
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(
            ItemController itemController,
            Weapon weapon,
            bool quickReload,
            ref ItemAddress vestTargetAddress)
        {
            if (!SPTEssentialsPlugin.Settings.KeepQuickReloadMagazines.Value
                || !quickReload
                || vestTargetAddress != null
                || !(itemController is InventoryController inventoryController)
                || weapon == null)
            {
                return;
            }

            var oldMagazine = weapon.GetCurrentMagazine();
            if (oldMagazine == null)
            {
                return;
            }

            try
            {
                var equipment = inventoryController.Inventory?.Equipment;
                if (equipment == null)
                {
                    return;
                }

                var grids = InventoryEquipmentExtension.GetPrioritizedGridsForUnloadedObject(equipment, false);
                vestTargetAddress = GridExtensions.FindLocationForItem(grids, oldMagazine);
            }
            catch (Exception exception)
            {
                if (_warningLogged)
                {
                    return;
                }

                _warningLogged = true;
                SPTEssentialsPlugin.Log.LogWarning($"Quickload Mag Saver could not reserve a grid: {exception.Message}");
            }
        }
    }
}
