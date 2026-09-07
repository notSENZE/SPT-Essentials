using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SPTEssentials.Client.SpecialSlots;

internal sealed class ExtraSpecialSlotsLayoutModule : ClientModule
{
    private const int ColumnCount = 3;
    private static readonly FieldInfo SpecialSlotPanel = AccessTools.Field(
        typeof(SearchableSlotView),
        "_specSlotsPanel");

    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".specialslots");

    protected override string Name => "Expanded Special Slots";

    protected override void Enable()
    {
        Patch(_harmony, typeof(CreateSlotsPatch));
    }

    [HarmonyPatch(typeof(SearchableSlotView), "CreateSlots", typeof(Item))]
    private static class CreateSlotsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SearchableSlotView __instance, Item item)
        {
            if (!SPTEssentialsPlugin.Settings.EnableExpandedSpecialSlots.Value
                || !(item is CompoundItem pockets)
                || pockets.Slots.Count(slot =>
                    slot?.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true) < 6)
            {
                return;
            }

            var panel = SpecialSlotPanel?.GetValue(__instance) as RectTransform;
            if (panel != null)
            {
                ArrangeAsGrid(panel);
            }
        }
    }

    private static void ArrangeAsGrid(RectTransform panel)
    {
        var grid = panel.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            var oldLayout = panel.GetComponent<HorizontalLayoutGroup>();
            var oldPadding = oldLayout?.padding;
            var oldSpacing = oldLayout?.spacing ?? 0f;
            if (oldLayout != null)
            {
                UnityEngine.Object.DestroyImmediate(oldLayout);
            }

            grid = panel.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = oldPadding == null
                ? new RectOffset()
                : new RectOffset(oldPadding.left, oldPadding.right, oldPadding.top, oldPadding.bottom);
            grid.spacing = new Vector2(oldSpacing, oldSpacing);
        }

        var cell = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1));
        grid.cellSize = new Vector2(cell.X, cell.Y);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = ColumnCount;

        var rowCount = Math.Max(1, (panel.childCount + ColumnCount - 1) / ColumnCount);
        var height = grid.padding.vertical
            + rowCount * grid.cellSize.y
            + Math.Max(0, rowCount - 1) * grid.spacing.y;

        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        if (panel.parent is RectTransform parent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
    }
}
