using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
using IOPath = System.IO.Path;

namespace SPTEssentials.Server.SpecialSlots;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload)]
public sealed class ExtraSpecialSlotsService(
    TemplateTable templates,
    ICloner cloner,
    EssentialsServerConfig config,
    ISptLogger<ExtraSpecialSlotsService> logger) : IOnLoad
{
    private const string SvmAssemblyName = "ServerValueModifier";

    private static readonly MongoId DefaultPmcPockets = new("627a4e6b255f7527fb05a0f6");
    private static readonly MongoId DefaultScavPockets = new("557ffd194bdc2d28148b457f");
    private static readonly MongoId SvmPmcPockets = new("a8edfb0bce53d103d3f62b9b");
    private static readonly MongoId SvmScavPockets = new("a8edfb0bce53d103d3f6219b");

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!config.EnableExpandedSpecialSlots)
        {
            logger.Info($"{ModInfo.LogPrefix} Expanded Special Slots is disabled.");
            return Task.CompletedTask;
        }

        PrepareSvmPockets();

        var pocketCount = 0;
        var addedCount = 0;
        foreach (var pockets in templates.Items.Values.Where(IsPocketTemplate).ToArray())
        {
            pocketCount++;
            addedCount += AddMissingSlots(pockets);
        }

        if (pocketCount == 0)
        {
            logger.Warning($"{ModInfo.LogPrefix} No compatible Pockets templates were found for Expanded Special Slots.");
        }
        else
        {
            logger.Success($"{ModInfo.LogPrefix} Expanded Special Slots added {addedCount} missing slot(s) across {pocketCount} Pockets template(s).");
        }

        return Task.CompletedTask;
    }

    private int AddMissingSlots(TemplateItem pockets)
    {
        var slots = pockets.Properties?.Slots?.Where(slot => slot is not null).ToList();
        if (slots is null)
        {
            return 0;
        }

        var source = slots.FirstOrDefault(slot =>
            string.Equals(slot.Name, "SpecialSlot3", StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            return 0;
        }

        var existing = slots.Select(slot => ReadSlotNumber(slot.Name)).ToHashSet();
        var added = 0;
        for (var number = 4; number <= 6; number++)
        {
            if (existing.Contains(number))
            {
                continue;
            }

            var slot = cloner.Clone(source)
                ?? throw new InvalidOperationException("The third Special Slot could not be cloned.");
            slot.Name = $"SpecialSlot{number}";
            slot.Id = MakeSlotId(pockets.Id, number);
            slot.Parent = pockets.Id;
            slots.Add(slot);
            added++;
        }

        pockets.Properties!.Slots = slots;
        return added;
    }

    private void PrepareSvmPockets()
    {
        var svm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, SvmAssemblyName, StringComparison.OrdinalIgnoreCase));
        if (svm is null)
        {
            return;
        }

        try
        {
            if (!TryReadSvmPocketChoice(svm.Location, out var choice))
            {
                return;
            }

            if (choice.UsePmcPockets)
            {
                AddPocketPlaceholder(DefaultPmcPockets, SvmPmcPockets);
            }

            if (choice.UseScavPockets)
            {
                AddPocketPlaceholder(DefaultScavPockets, SvmScavPockets);
            }
        }
        catch (Exception exception)
        {
            logger.Warning($"{ModInfo.LogPrefix} SVM Pockets preparation was skipped: {exception.Message}");
        }
    }

    private void AddPocketPlaceholder(MongoId sourceId, MongoId targetId)
    {
        if (templates.Items.ContainsKey(targetId)
            || !templates.Items.TryGetValue(sourceId, out var source))
        {
            return;
        }

        var placeholder = cloner.Clone(source)
            ?? throw new InvalidOperationException($"Pockets template {sourceId} could not be cloned.");
        placeholder.Id = targetId;
        templates.Items[targetId] = placeholder;
    }

    private static bool TryReadSvmPocketChoice(string assemblyPath, out SvmPocketChoice choice)
    {
        choice = default;
        var modDirectory = IOPath.GetDirectoryName(assemblyPath);
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            return false;
        }

        var loaderFile = IOPath.Combine(modDirectory, "Loader", "loader.json");
        if (!File.Exists(loaderFile))
        {
            return false;
        }

        using var loader = ReadJson(loaderFile);
        if (!TryGetProperty(loader.RootElement, "CurrentlySelectedPreset", out var selected)
            || selected.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var presetName = selected.GetString();
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return false;
        }

        if (!presetName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            presetName += ".json";
        }

        if (!string.Equals(IOPath.GetFileName(presetName), presetName, StringComparison.Ordinal))
        {
            return false;
        }

        var presetFile = IOPath.Combine(modDirectory, "Presets", presetName);
        if (!File.Exists(presetFile))
        {
            return false;
        }

        using var preset = ReadJson(presetFile);
        choice = new SvmPocketChoice(
            SectionEnabled(preset.RootElement, "CSM", "EnableCSM", "CustomPocket"),
            SectionEnabled(preset.RootElement, "Scav", "EnableScav", "ScavCustomPockets"));
        return true;
    }

    private static JsonDocument ReadJson(string path)
    {
        return JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
    }

    private static bool SectionEnabled(JsonElement root, string sectionName, params string[] flags)
    {
        if (!TryGetProperty(root, sectionName, out var section)
            || section.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return flags.All(flag =>
            TryGetProperty(section, flag, out var value)
            && value.ValueKind == JsonValueKind.True);
    }

    private static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        foreach (var property in source.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsPocketTemplate(TemplateItem item)
    {
        if (!string.Equals(item.Properties?.Name, "Pockets", StringComparison.OrdinalIgnoreCase)
            || item.Properties?.Slots is null)
        {
            return false;
        }

        var numbers = item.Properties.Slots
            .Where(slot => slot is not null)
            .Select(slot => ReadSlotNumber(slot!.Name))
            .ToHashSet();
        return numbers.IsSupersetOf([1, 2, 3]);
    }

    private static int ReadSlotNumber(string? name)
    {
        const string prefix = "SpecialSlot";
        return name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
            && int.TryParse(name[prefix.Length..], out var number)
                ? number
                : 0;
    }

    private static MongoId MakeSlotId(MongoId pocketsId, int slotNumber)
    {
        var value = $"{ModInfo.Guid}|{pocketsId}|SpecialSlot{slotNumber}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new MongoId(Convert.ToHexString(digest)[..24].ToLowerInvariant());
    }

    private readonly record struct SvmPocketChoice(bool UsePmcPockets, bool UseScavPockets);
}
