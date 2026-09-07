using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace SPTEssentials.Server.Items;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload)]
public sealed class FieldArmorRepairKitService(
    CustomItemService customItemService,
    TemplateTable templates,
    EssentialsServerConfig config,
    ISptLogger<FieldArmorRepairKitService> logger) : IOnLoad
{
    public static readonly MongoId ItemId = new("6a83285922c45103d3c1cc9a");

    private static readonly MongoId BaseRepairKitId = new("591094e086f7747caa7bb2ef");
    private static readonly MongoId RepairKitParentId = new("616eb7aea207f41933308f46");
    private const string HandbookParentId = "5b47574386f77428ca22b345";
    private const double Price = 245_000;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!config.EnableFieldRepair)
        {
            logger.Info($"{ModInfo.LogPrefix} Field Repair is disabled.");
            return Task.CompletedTask;
        }

        if (templates.Items.ContainsKey(ItemId))
        {
            logger.Warning($"{ModInfo.LogPrefix} Field Armor Repair Kit already exists; registration skipped.");
            return Task.CompletedTask;
        }

        var locale = new LocaleDetails
        {
            Name = "Field Armor Repair Kit",
            ShortName = "FARK",
            Description = "A compact maintenance kit for emergency armor repairs during a raid. Drag it onto damaged body armor, an armored rig, a ballistic plate or a helmet to use it."
        };

        var result = customItemService.CreateItemFromClone(
            new NewItemFromCloneDetails
            {
                ItemTplToClone = BaseRepairKitId,
                ParentId = RepairKitParentId,
                NewId = ItemId,
                NewItemName = "field_armor_repair_kit",
                HandbookParentId = HandbookParentId,
                HandbookPriceRoubles = Price,
                FleaPriceRoubles = Price,
                AddToHandbook = true,
                AddToFleaPriceDb = true,
                OverrideProperties = new TemplateItemProperties
                {
                    BackgroundColor = "red",
                    CanRequireOnRagfair = false,
                    CanSellOnRagfair = true,
                    ExaminedByDefault = true,
                    Height = 2,
                    MaxRepairResource = 300,
                    Name = "field_armor_repair_kit",
                    RarityPvE = "Superrare",
                    Weight = 2.5,
                    Width = 2
                },
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = locale,
                    ["de"] = locale
                }
            },
            Assembly.GetExecutingAssembly()
        );

        if (!result.Success)
        {
            logger.Error($"{ModInfo.LogPrefix} Field Armor Repair Kit registration failed: {string.Join("; ", result.Errors)}");
            return Task.CompletedTask;
        }

        AddToSpecialSlotFilters();
        logger.Success($"{ModInfo.LogPrefix} Field Armor Repair Kit registered with 300 repair points at 245,000 roubles.");
        return Task.CompletedTask;
    }

    private void AddToSpecialSlotFilters()
    {
        foreach (var pockets in templates.Items.Values.Where(IsPocketTemplate))
        {
            foreach (var slot in pockets.Properties!.Slots!)
            {
                if (slot?.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) != true)
                {
                    continue;
                }

                foreach (var filter in slot.Properties?.Filters ?? [])
                {
                    filter.Filter ??= [];
                    filter.Filter.Add(ItemId);
                }
            }
        }
    }

    private static bool IsPocketTemplate(TemplateItem item)
    {
        return string.Equals(item.Properties?.Name, "Pockets", StringComparison.OrdinalIgnoreCase)
            && item.Properties?.Slots is not null;
    }
}
