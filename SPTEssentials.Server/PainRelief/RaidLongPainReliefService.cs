using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEssentials.Server.PainRelief;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload)]
public sealed class RaidLongPainReliefService(
    TemplateTable templates,
    EssentialsServerConfig config,
    ISptLogger<RaidLongPainReliefService> logger) : IOnLoad
{
    private const double RaidLongDurationSeconds = 86_400;

    private static readonly MongoId IbuprofenId = new("5af0548586f7743a532b7e99");
    private static readonly MongoId MorphineId = new("544fb3f34bdc2d03748b456a");

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!config.EnableRaidLongPainRelief)
        {
            logger.Info($"{ModInfo.LogPrefix} Raid-Long Pain Relief is disabled.");
            return Task.CompletedTask;
        }

        var updatedItems = 0;
        updatedItems += SetPainReliefDuration(IbuprofenId) ? 1 : 0;
        updatedItems += SetPainReliefDuration(MorphineId) ? 1 : 0;

        if (updatedItems == 2)
        {
            logger.Success($"{ModInfo.LogPrefix} Ibuprofen and Morphine now provide raid-long pain relief.");
        }
        else
        {
            logger.Warning(
                $"{ModInfo.LogPrefix} Raid-Long Pain Relief updated {updatedItems} of 2 expected item templates.");
        }

        return Task.CompletedTask;
    }

    private bool SetPainReliefDuration(MongoId itemId)
    {
        if (!templates.Items.TryGetValue(itemId, out var item)
            || item.Properties?.EffectsDamage is null
            || !item.Properties.EffectsDamage.TryGetValue(DamageEffectType.Pain, out var painEffect)
            || painEffect is null)
        {
            return false;
        }

        painEffect.Duration = RaidLongDurationSeconds;
        return true;
    }
}
