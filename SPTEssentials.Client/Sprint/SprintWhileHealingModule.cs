using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SPTEssentials.Client.Sprint;

internal sealed class SprintWhileHealingModule : ClientModule
{
    private const string CmsTemplateId = "5d02778e86f774203e7dedbe";
    private const string Surv12TemplateId = "5d02797c86f774203f38e30a";
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".sprintwhilehealing");

    protected override string Name => "Sprint While Healing";

    protected override void Enable()
    {
        Patch(_harmony, typeof(CanSprintPatch));
    }

    [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.CanSprint), MethodType.Getter)]
    private static class CanSprintPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MovementContext __instance, Player ____player, ref bool __result)
        {
            if (__result
                || !SPTEssentialsPlugin.Settings.EnableSprintWhileHealing.Value
                || ____player == null
                || !____player.IsYourPlayer
                || !(____player.HandsController is Player.MedsController medsController)
                || IsSurgeryKit(medsController.Item))
            {
                return;
            }

            var condition = __instance.PhysicalCondition;
            var isUsingMeds = (condition & EPhysicalCondition.UsingMeds) != 0;
            var isHealingLegs = (condition & EPhysicalCondition.HealingLegs) != 0;
            if (!isUsingMeds && !isHealingLegs)
            {
                return;
            }

            if ((condition & EPhysicalCondition.SprintDisabled) != 0)
            {
                return;
            }

            if ((condition & EPhysicalCondition.OnPainkillers) != 0)
            {
                __result = true;
                return;
            }

            __result = (condition & (EPhysicalCondition.LeftLegDamaged | EPhysicalCondition.RightLegDamaged)) == 0;
        }

        private static bool IsSurgeryKit(EFT.InventoryLogic.Item item)
        {
            if (item?.Template is MedsTemplate medsTemplate
                && medsTemplate.DamageEffects != null
                && medsTemplate.DamageEffects.ContainsKey(EDamageEffectType.DestroyedPart))
            {
                return true;
            }

            var templateId = item?.StringTemplateId;
            return templateId == CmsTemplateId || templateId == Surv12TemplateId;
        }
    }
}
