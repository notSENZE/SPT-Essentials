using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SPTEssentials.Client.RaidModdable;

internal sealed class RaidModdableModule : ClientModule
{
    private const string MultitoolTemplateId = "544fb5454bdc2df8738b456a";
    private const string HandguardParentId = "55818a104bdc2db9688b4569";
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".raidmoddable");

    protected override string Name => "Raid Moddable";

    protected override void Enable()
    {
        Patch(_harmony, typeof(RaidModdablePatch));
        Patch(_harmony, typeof(CanBeMovedPatch));
        Patch(_harmony, typeof(MovePathCheckPatch));
        Patch(_harmony, typeof(CompoundApplyPatch));
    }

    private static bool CanUseMultitool(Mod mod)
    {
        if (!SPTEssentialsPlugin.Settings.EnableRaidModdable.Value
            || mod == null
            || !CanModifyWithMultitool(mod)
            || !Singleton<GameWorld>.Instantiated)
        {
            return false;
        }

        var player = Singleton<GameWorld>.Instance?.MainPlayer;
        return player != null
            && player.IsYourPlayer
            && player.Inventory?.AllRealPlayerItems != null
            && player.Inventory.AllRealPlayerItems.Any(
                item => item != null && item.StringTemplateId == MultitoolTemplateId);
    }

    private static bool CanModifyWithMultitool(Mod mod)
    {
        return mod.ToolModdable
            || string.Equals(
                mod.Template?.ParentId?.ToString(),
                HandguardParentId,
                System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVitalPartWithoutMultitool(
        IEnumerable<Slot> vitalParts,
        Slot slot,
        Item item)
    {
        return vitalParts.Contains(slot)
            && (!(item is Mod mod) || !CanUseMultitool(mod));
    }

    [HarmonyPatch(typeof(Mod), nameof(Mod.RaidModdable), MethodType.Getter)]
    private static class RaidModdablePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Mod __instance, ref bool __result)
        {
            if (!__result && CanUseMultitool(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Mod), nameof(Mod.CanBeMoved))]
    private static class CanBeMovedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Mod __instance, ref Option<bool> __result)
        {
            if (CanUseMultitool(__instance)
                && __result.Failed
                && __result.Error is CannotModifyVitalInRaidError)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    private static class MovePathCheckPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ItemManipulator),
                nameof(ItemManipulator.MovePathCheck),
                new[] { typeof(Item), typeof(ItemAddress), typeof(ItemController) });
        }

        [HarmonyPostfix]
        private static void Postfix(Item __0, ref Option<None> __result)
        {
            if (__0 is Mod mod
                && CanUseMultitool(mod)
                && __result.Failed
                && __result.Error is CannotModifyVitalInRaidError)
            {
                __result = None._;
            }
        }
    }

    [HarmonyPatch]
    private static class CompoundApplyPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(CompoundItem),
                nameof(CompoundItem.Apply),
                new[] { typeof(ItemController), typeof(Item), typeof(int), typeof(bool) });
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var replacement = AccessTools.Method(
                typeof(RaidModdableModule),
                nameof(IsVitalPartWithoutMultitool));
            var replacementCount = 0;

            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method
                    && method.DeclaringType == typeof(Enumerable)
                    && method.Name == nameof(Enumerable.Contains)
                    && method.IsGenericMethod
                    && method.GetGenericArguments().Length == 1
                    && method.GetGenericArguments()[0] == typeof(Slot))
                {
                    var loadItem = new CodeInstruction(OpCodes.Ldarg_2);
                    loadItem.labels.AddRange(instruction.labels);
                    loadItem.blocks.AddRange(instruction.blocks);
                    instruction.labels.Clear();
                    instruction.blocks.Clear();

                    yield return loadItem;
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replacementCount++;
                }

                yield return instruction;
            }

            if (replacementCount != 1)
            {
                SPTEssentialsPlugin.Log.LogWarning(
                    $"Raid Moddable expected one vital-slot check but found {replacementCount}.");
            }
        }
    }
}
