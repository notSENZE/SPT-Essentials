using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTEssentials.Server.AirFilter;
using SPTEssentials.Server.Compat;

namespace SPTEssentials.Server;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 1_000)]
public sealed class ServerMod(
    CompatibilityService compatibilityService,
    EssentialsServerConfig config,
    ISptLogger<ServerMod> logger) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (config.EnableSmartAirFilter)
        {
            var harmony = new Harmony(ModInfo.Guid + ".server.airfilter");
            harmony.CreateClassProcessor(typeof(StartRaidPatch)).Patch();
            harmony.CreateClassProcessor(typeof(EndRaidPatch)).Patch();
            harmony.CreateClassProcessor(typeof(AirFilterConsumptionPatch)).Patch();
        }

        if (config.EnableCompatibilityRules)
        {
            await compatibilityService.LoadAsync(cancellationToken);
        }

        logger.Success($"{ModInfo.LogPrefix} {ModInfo.Name} {ModInfo.Version} server component loaded.");
    }
}
