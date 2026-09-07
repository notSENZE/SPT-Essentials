using EFT.Hideout;
using HarmonyLib;

namespace SPTEssentials.Client.AirFilter;

internal sealed class RaidOnlyAirFilterModule : ClientModule
{
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".airfilter");

    protected override string Name => "Smart Air Filter";

    protected override void Enable()
    {
        Patch(_harmony, typeof(AirFilterStartPatch));
        Patch(_harmony, typeof(AirFilterUpdatePatch));
    }

    [HarmonyPatch(typeof(AirFilteringUnitBehaviour), nameof(AirFilteringUnitBehaviour.Start), typeof(float))]
    private static class AirFilterStartPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref float profileDecayTime)
        {
            if (!SPTEssentialsPlugin.Settings.EnableSmartAirFilter.Value)
            {
                return;
            }

            profileDecayTime = 0f;
        }
    }

    [HarmonyPatch(typeof(AirFilteringUnitBehaviour), nameof(AirFilteringUnitBehaviour.Update), typeof(float))]
    private static class AirFilterUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(AirFilteringUnitBehaviour __instance, out ConsumptionState __state)
        {
            __state = default;
            if (!SPTEssentialsPlugin.Settings.EnableSmartAirFilter.Value)
            {
                return;
            }

            var consumer = __instance?.ResourceConsumer;
            if (consumer == null)
            {
                return;
            }

            __state = new ConsumptionState(true, consumer, consumer.Consumption);
            consumer.Consumption = 0f;
        }

        [HarmonyPostfix]
        private static void Postfix(ConsumptionState __state)
        {
            if (__state.Applied && __state.Consumer != null)
            {
                __state.Consumer.Consumption = __state.OriginalConsumption;
            }
        }
    }

    private readonly struct ConsumptionState
    {
        internal ConsumptionState(bool applied, ResourceConsumer consumer, float originalConsumption)
        {
            Applied = applied;
            Consumer = consumer;
            OriginalConsumption = originalConsumption;
        }

        internal bool Applied { get; }
        internal ResourceConsumer Consumer { get; }
        internal float OriginalConsumption { get; }
    }
}
