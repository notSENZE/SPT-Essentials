using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.Repairing;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace SPTEssentials.Client.FieldRepair;

internal sealed class FieldArmorRepairModule : ClientModule
{
    internal const string TemplateId = "6a83285922c45103d3c1cc9a";

    private const float ResourceEpsilon = 0.001f;

    private static readonly FieldInfo ItemControllerField = AccessTools.Field(
        typeof(ItemUiContext),
        "_itemController");

    private static readonly Harmony Harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".fieldrepair");

    private static InstantRepairContext _activeRepair;
    private static bool _repairControllerPatched;

    protected override string Name => "Field Repair";

    protected override void Enable()
    {
        Patch(Harmony, typeof(RepairButtonPatch));
        Patch(Harmony, typeof(OpenRepairWindowPatch));
    }

    private static bool EnsureRepairControllerPatch()
    {
        if (_repairControllerPatched)
        {
            return true;
        }

        try
        {
            Patch(Harmony, typeof(RepairItemsByKitPatch));
            _repairControllerPatched = true;
            SPTEssentialsPlugin.Log.LogInfo("FARK raid repair handler activated.");
            return true;
        }
        catch (Exception exception)
        {
            SPTEssentialsPlugin.Log.LogError($"Could not activate the FARK raid repair handler: {exception}");
            return false;
        }
    }

    private static bool IsFieldKit(RepairKit repairKit)
    {
        return repairKit != null
            && string.Equals(repairKit.TemplateId, TemplateId, StringComparison.OrdinalIgnoreCase)
            && repairKit.Resource > ResourceEpsilon;
    }

    private static bool IsRaidActive()
    {
        return Singleton<GameWorld>.Instantiated
            && Singleton<GameWorld>.Instance?.MainPlayer != null;
    }

    private static IEnumerable<Item> GetRepairableArmor(Item item, RepairKit repairKit)
    {
        if (item == null || repairKit == null)
        {
            return Enumerable.Empty<Item>();
        }

        return item
            .GetItemComponentsInChildren<RepairableComponent>(true)
            .Select(component => component.Item)
            .Where(child => child.GetItemComponent<ArmorComponent>() != null
                && repairKit.CanRepair(child));
    }

    private static bool CanEnableRepair(ItemContextInteractionsSwitcher switcher)
    {
        if (!SPTEssentialsPlugin.Settings.EnableFieldArmorRepair.Value
            || switcher == null
            || !switcher.Gameplay
            || switcher.BadContextType
            || switcher._item == null
            || switcher._itemController == null
            || !switcher.Examined
            || !(switcher._itemContext is RepairItemContext repairContext)
            || !IsFieldKit(repairContext.RepairKit)
            || !switcher._itemController.Examined(repairContext.RepairKit))
        {
            return false;
        }

        return GetRepairableArmor(switcher._item, repairContext.RepairKit).Any();
    }

    private static IRepairStrategy CreateRepairStrategy(Item item, RepairController repairController)
    {
        return item.GetItemComponent<ArmorHolderComponent>() != null
            ? (IRepairStrategy)new ArmorRepairStrategy(item, repairController)
            : new DefaultRepairStrategy(item, repairController);
    }

    private static async Task RepairImmediately(
        RepairController repairController,
        ItemContext itemContext,
        RepairKit repairKit,
        ItemController itemController)
    {
        var repairableItems = GetRepairableArmor(itemContext.Item, repairKit)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (repairableItems.Count == 0)
        {
            ShowNotification("FARK cannot repair this item.", ENotificationIconType.Alert);
            return;
        }

        var strategy = CreateRepairStrategy(itemContext.Item, repairController);
        var repairer = repairController.CreateRepairKitsCollection(repairKit);
        repairer.AddRepairKit(repairKit);
        strategy.RepairKitsCollections.Clear();
        strategy.RepairKitsCollections.Add(repairer);
        strategy.CurrentRepairer = repairer;

        var repairAmount = strategy.HowMuchRepairScoresCanAccept();
        if (repairAmount <= ResourceEpsilon)
        {
            ShowNotification("This item does not need repairs.", ENotificationIconType.Alert);
            return;
        }

        var context = new InstantRepairContext(repairController, repairKit, repairableItems);
        _activeRepair = context;
        Action repairChanged = delegate { };
        repairController.OnSuccessfulRepairChangedEvent += repairChanged;

        try
        {
            var result = await strategy.RepairItem(repairAmount, repairKit);
            if (result == null || result.Failed)
            {
                ShowNotification(result?.Error ?? "FARK repair failed.", ENotificationIconType.Alert);
                return;
            }

            itemContext.UpdateView();
            itemContext.Source?.UpdateView();

            if (repairKit.Resource <= ResourceEpsilon)
            {
                RemoveDepletedKit(repairKit, itemController);
            }

            if (Singleton<GUISounds>.Instantiated)
            {
                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.RepairComplete);
            }

            ShowNotification(
                $"FARK restored {context.RestoredDurability:0.##} durability and used {context.PointsUsed:0.##} points.",
                ENotificationIconType.Default);
        }
        catch (Exception exception)
        {
            SPTEssentialsPlugin.Log.LogError($"FARK instant repair failed: {exception}");
            ShowNotification("FARK repair failed. Check the client log for details.", ENotificationIconType.Alert);
        }
        finally
        {
            repairController.OnSuccessfulRepairChangedEvent -= repairChanged;
            if (ReferenceEquals(_activeRepair, context))
            {
                _activeRepair = null;
            }
        }
    }

    private static void RemoveDepletedKit(RepairKit repairKit, ItemController itemController)
    {
        var removeResult = ItemManipulator.Remove(repairKit, itemController, false);
        if (!removeResult.Succeeded)
        {
            SPTEssentialsPlugin.Log.LogWarning($"Could not remove depleted FARK: {removeResult.Error}");
            return;
        }

        itemController.RunNetworkTransaction(removeResult.Value, null);
    }

    private static void ShowNotification(string message, ENotificationIconType iconType)
    {
        NotificationManager.DisplayMessageNotification(
            message,
            ENotificationDurationType.Default,
            iconType,
            null);
    }

    [HarmonyPatch(typeof(ItemContextInteractionsSwitcher), nameof(ItemContextInteractionsSwitcher.IsActive), typeof(EItemInfoButton))]
    private static class RepairButtonPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            ItemContextInteractionsSwitcher __instance,
            EItemInfoButton button,
            ref bool __result)
        {
            if (!__result && button == EItemInfoButton.Repair && CanEnableRepair(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ItemUiContext), nameof(ItemUiContext.OpenRepairWindow))]
    private static class OpenRepairWindowPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            ItemUiContext __instance,
            RepairController repairController,
            ItemContext itemContext)
        {
            if (!SPTEssentialsPlugin.Settings.EnableFieldArmorRepair.Value
                || !IsRaidActive()
                || !(itemContext is RepairItemContext repairContext)
                || !IsFieldKit(repairContext.RepairKit))
            {
                return true;
            }

            if (_activeRepair != null)
            {
                ShowNotification("A FARK repair is already in progress.", ENotificationIconType.Alert);
                return false;
            }

            if (!EnsureRepairControllerPatch())
            {
                ShowNotification("FARK repair could not be initialized. Check the client log for details.", ENotificationIconType.Alert);
                return false;
            }

            var itemController = ItemControllerField?.GetValue(__instance) as ItemController;
            if (repairController == null || itemController == null || itemContext.Item == null)
            {
                ShowNotification("FARK repair is unavailable in this context.", ENotificationIconType.Alert);
                return false;
            }

            _ = RepairImmediately(
                repairController,
                itemContext,
                repairContext.RepairKit,
                itemController);
            return false;
        }
    }

    [HarmonyPatch(typeof(RepairController), nameof(RepairController.RepairItemsByRepairKit))]
    private static class RepairItemsByKitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            RepairController __instance,
            EFT.RepairItem[] repairKitsInfo,
            string targetItem,
            ref Task<IResult> __result)
        {
            var context = _activeRepair;
            if (context == null
                || !ReferenceEquals(context.RepairController, __instance)
                || !IsRaidActive())
            {
                return true;
            }

            var pointsUsed = repairKitsInfo?.Sum(info => info.Count) ?? 0f;
            if (pointsUsed <= ResourceEpsilon)
            {
                __result = Task.FromResult<IResult>(new SuccessfulResult());
                return false;
            }

            if (!context.Items.TryGetValue(targetItem, out var item))
            {
                context.ApplyRemainingResource();
                __result = Task.FromResult<IResult>(new FailedResult("Repair target is not part of the active FARK operation.", 0));
                return false;
            }

            var repairable = item.GetItemComponent<RepairableComponent>();
            var pointsPerDurability = __instance.GetRepairPrice(context.RepairKit.RepairKitTemplate, 1d, item);
            if (repairable == null || pointsPerDurability <= 0d || double.IsNaN(pointsPerDurability))
            {
                context.ApplyRemainingResource();
                __result = Task.FromResult<IResult>(new FailedResult("FARK could not calculate the repair.", 0));
                return false;
            }

            var oldDurability = repairable.Durability;
            var oldMaxDurability = repairable.MaxDurability;
            var repairedDurability = (float)(pointsUsed / pointsPerDurability);
            var degradationRange = repairable.RepairKitDegradation;
            var degradation = (float)Math.Round(
                UnityEngine.Random.Range(degradationRange.x, degradationRange.y) * oldMaxDurability,
                2);

            var newMaxDurability = Math.Max(0f, oldMaxDurability - degradation);
            var newDurability = Math.Min(oldMaxDurability, oldDurability + repairedDurability);
            newDurability = Math.Min(newDurability, newMaxDurability);

            repairable.MaxDurability = newMaxDurability;
            repairable.Durability = newDurability;
            context.PointsUsed += pointsUsed;
            context.ApplyRemainingResource();
            context.RestoredDurability += Math.Max(0f, newDurability - oldDurability);

            __result = Task.FromResult<IResult>(new SuccessfulResult());
            return false;
        }
    }

    private sealed class InstantRepairContext
    {
        internal InstantRepairContext(
            RepairController repairController,
            RepairKit repairKit,
            IReadOnlyDictionary<string, Item> items)
        {
            RepairController = repairController;
            RepairKit = repairKit;
            Items = items;
            StartingResource = repairKit.Resource;
        }

        internal RepairController RepairController { get; }

        internal RepairKit RepairKit { get; }

        internal IReadOnlyDictionary<string, Item> Items { get; }

        private float StartingResource { get; }

        internal float PointsUsed { get; set; }

        internal float RestoredDurability { get; set; }

        internal void ApplyRemainingResource()
        {
            RepairKit.RepairKitComponent.Resource = Math.Max(0f, StartingResource - PointsUsed);
        }

    }
}
