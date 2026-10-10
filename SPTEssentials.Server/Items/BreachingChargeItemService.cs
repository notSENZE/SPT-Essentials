using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using WTTServerCommonLib;

namespace SPTEssentials.Server.Items;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload + 2)]
public sealed class BreachingChargeItemService(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    EssentialsServerConfig config,
    ISptLogger<BreachingChargeItemService> logger) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!config.EnableBreacher)
        {
            logger.Info($"{ModInfo.LogPrefix} Breaching Charge item registration is disabled with Breacher.");
            return;
        }

        await wttCommon.CustomItemServiceExtended.CreateCustomItems(
            Assembly.GetExecutingAssembly(),
            Path.Join("db", "CustomItems"));

        logger.Success($"{ModInfo.LogPrefix} Breaching Charge registered through WTT-CommonLib.");
    }
}
