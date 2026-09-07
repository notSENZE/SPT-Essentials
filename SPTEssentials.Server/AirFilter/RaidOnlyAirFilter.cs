using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEssentials.Server.AirFilter;

internal static class RaidActivity
{
    private static readonly ConcurrentDictionary<MongoId, bool> ActivePmcRaids = new();

    internal static void Set(MongoId sessionId, bool active)
    {
        if (active)
        {
            ActivePmcRaids[sessionId] = true;
        }
        else
        {
            ActivePmcRaids.TryRemove(sessionId, out _);
        }
    }

    internal static bool IsPmcRaid(PmcData profile)
    {
        var sessionId = profile.SessionId ?? profile.Id;
        return sessionId.HasValue && ActivePmcRaids.ContainsKey(sessionId.Value);
    }
}

[HarmonyPatch(typeof(MatchController), nameof(MatchController.StartLocalRaidAsync))]
internal static class StartRaidPatch
{
    [HarmonyPrefix]
    private static void Prefix(MongoId sessionId, StartLocalRaidRequestData request)
    {
        var isPmc = !string.Equals(request.PlayerSide, "Savage", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.PlayerSide, "Scav", StringComparison.OrdinalIgnoreCase);
        RaidActivity.Set(sessionId, isPmc);
    }

    [HarmonyPostfix]
    private static void Postfix(MongoId sessionId, Task<StartLocalRaidResponseData> __result)
    {
        _ = ClearFailedStartAsync(sessionId, __result);
    }

    private static async Task ClearFailedStartAsync(
        MongoId sessionId,
        Task<StartLocalRaidResponseData> startTask)
    {
        try
        {
            await startTask.ConfigureAwait(false);
        }
        catch
        {
            RaidActivity.Set(sessionId, false);
        }
    }
}

[HarmonyPatch(typeof(MatchController), nameof(MatchController.EndLocalRaidAsync))]
internal static class EndRaidPatch
{
    [HarmonyPostfix]
    private static void Postfix(MongoId sessionId, Task __result)
    {
        _ = ClearAfterRaidAsync(sessionId, __result);
    }

    private static async Task ClearAfterRaidAsync(MongoId sessionId, Task endTask)
    {
        try
        {
            await endTask.ConfigureAwait(false);
        }
        finally
        {
            RaidActivity.Set(sessionId, false);
        }
    }
}

[HarmonyPatch]
internal static class AirFilterConsumptionPatch
{
    private static readonly FieldInfo HideoutTableField = AccessTools.Field(
        typeof(HideoutHelper),
        "<hideoutTable>P");

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(HideoutHelper), "UpdateAirFilters");
    }

    [HarmonyPrefix]
    private static void Prefix(HideoutHelper __instance, PmcData pmcData, out RateState __state)
    {
        __state = default;
        if (pmcData is null || RaidActivity.IsPmcRaid(pmcData))
        {
            return;
        }

        if (HideoutTableField.GetValue(__instance) is not HideoutTable hideoutTable
            || hideoutTable.Settings is null)
        {
            return;
        }

        __state = new RateState(true, hideoutTable, hideoutTable.Settings.AirFilterUnitFlowRate);
        hideoutTable.Settings.AirFilterUnitFlowRate = 0;
    }

    [HarmonyPostfix]
    private static void Postfix(RateState __state)
    {
        if (__state.Applied)
        {
            __state.Table.Settings.AirFilterUnitFlowRate = __state.OriginalRate;
        }
    }

    internal readonly record struct RateState(
        bool Applied,
        HideoutTable Table,
        double? OriginalRate);
}
