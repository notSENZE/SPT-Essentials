using System.Reflection;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using IOPath = System.IO.Path;

namespace SPTEssentials.Server.Compat;

[Injectable(InjectionType.Singleton)]
public sealed class CompatibilityService(
    TemplateTable templates,
    ISptLogger<CompatibilityService> logger)
{
    private const string PmcSpecialSlots = "@PMC_SPECIALSLOTS";
    private const string GlobalSpecialSlots = "@GLOBAL_SPECIALSLOTS";
    private const string SpecialSlots = "@SPECIAL_SLOTS";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var folder = GetConfigFolder();
        if (!Directory.Exists(folder))
        {
            logger.Info($"{ModInfo.LogPrefix} Compatibility skipped because config/compat was not found.");
            return;
        }

        var files = Directory
            .GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(IsConfigFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            logger.Info($"{ModInfo.LogPrefix} Compatibility skipped because no JSONC files were found.");
            return;
        }

        var report = new CompatibilityReport();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ApplyFileAsync(file, report, cancellationToken);
        }

        logger.Success(
            $"{ModInfo.LogPrefix} Compatibility processed {report.Rules} rule(s) from {report.Files} file(s). "
            + $"Invalid={report.InvalidRules}, MissingTargets={report.MissingTargets}, "
            + $"MissingAllowedTemplates={report.MissingAllowedTemplates}, FiltersTouched={report.FiltersTouched}, "
            + $"Added={report.EntriesAdded}, Replaced={report.EntriesReplaced}.");
    }

    private async Task ApplyFileAsync(
        string file,
        CompatibilityReport report,
        CancellationToken cancellationToken)
    {
        CompatibilityConfig? config;
        try
        {
            var raw = await File.ReadAllTextAsync(file, cancellationToken);
            config = JsonSerializer.Deserialize<CompatibilityConfig>(raw, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            report.InvalidRules++;
            logger.Error(
                $"{ModInfo.LogPrefix} Compatibility could not read '{IOPath.GetFileName(file)}': {exception.Message}");
            return;
        }

        report.Files++;
        var rules = config?.Rules ?? [];
        for (var index = 0; index < rules.Count; index++)
        {
            if (!TryNormalizeRule(rules[index], out var rule, out var reason))
            {
                report.InvalidRules++;
                logger.Error(
                    $"{ModInfo.LogPrefix} Compatibility skipped rule {index + 1} in "
                    + $"'{IOPath.GetFileName(file)}': {reason}");
                continue;
            }

            report.Rules++;
            CountMissingAllowedTemplates(rule, report);

            if (IsSpecialSlotTarget(rule.TargetTpl))
            {
                ApplySpecialSlotRule(rule, report);
                continue;
            }

            ApplyItemRule(rule, report);
        }
    }

    private void ApplyItemRule(CompatibilityRule rule, CompatibilityReport report)
    {
        if (!templates.Items.TryGetValue(new MongoId(rule.TargetTpl), out var item)
            || item.Properties is null)
        {
            report.MissingTargets++;
            logger.Warning($"{ModInfo.LogPrefix} Compatibility target '{rule.TargetTpl}' was not found.");
            return;
        }

        var touchedBefore = report.FiltersTouched;
        PatchGrids(item.Properties, rule, report);
        PatchSlots(item.Properties, rule, report);

        if (report.FiltersTouched == touchedBefore)
        {
            logger.Warning(
                $"{ModInfo.LogPrefix} Compatibility target '{rule.TargetTpl}' had no matching filters.");
        }
    }

    private void ApplySpecialSlotRule(CompatibilityRule rule, CompatibilityReport report)
    {
        var touchedBefore = report.FiltersTouched;

        foreach (var pockets in templates.Items.Values.Where(IsPocketTemplate))
        {
            foreach (var slot in SelectSpecialSlots(pockets.Properties!.Slots!, rule))
            {
                PatchSlotFilters(slot.Properties?.Filters, rule, report);
            }
        }

        if (report.FiltersTouched == touchedBefore)
        {
            report.MissingTargets++;
            logger.Warning($"{ModInfo.LogPrefix} Compatibility found no matching Special Slot filters.");
        }
    }

    private static void PatchGrids(
        TemplateItemProperties properties,
        CompatibilityRule rule,
        CompatibilityReport report)
    {
        var grids = properties.Grids?.ToList();
        if (grids is null || grids.Count == 0)
        {
            return;
        }

        if (rule.SlotNames is { Count: > 0 } && rule.GridIndexes is not { Count: > 0 })
        {
            return;
        }

        var indexes = rule.GridIndexes is { Count: > 0 }
            ? rule.GridIndexes.Where(index => index >= 0 && index < grids.Count)
            : Enumerable.Range(0, grids.Count);

        foreach (var index in indexes)
        {
            PatchGridFilters(grids[index].Properties?.Filters, rule, report);
        }
    }

    private static void PatchSlots(
        TemplateItemProperties properties,
        CompatibilityRule rule,
        CompatibilityReport report)
    {
        var slots = properties.Slots?.Where(slot => slot is not null).ToList();
        if (slots is null || slots.Count == 0)
        {
            return;
        }

        IEnumerable<Slot> selected;
        if (rule.SlotNames is { Count: > 0 })
        {
            var names = new HashSet<string>(rule.SlotNames, StringComparer.OrdinalIgnoreCase);
            selected = slots.Where(slot => names.Contains(slot!.Name!)).Select(slot => slot!);
        }
        else
        {
            var indexes = rule.GridIndexes is { Count: > 0 }
                ? rule.GridIndexes.Where(index => index >= 0 && index < slots.Count)
                : Enumerable.Range(0, slots.Count);
            selected = indexes.Select(index => slots[index]!);
        }

        foreach (var slot in selected)
        {
            PatchSlotFilters(slot.Properties?.Filters, rule, report);
        }
    }

    private static IEnumerable<Slot> SelectSpecialSlots(
        IEnumerable<Slot?> slots,
        CompatibilityRule rule)
    {
        var selectedNames = rule.SlotNames is { Count: > 0 }
            ? new HashSet<string>(rule.SlotNames, StringComparer.OrdinalIgnoreCase)
            : null;
        var selectedNumbers = rule.GridIndexes is { Count: > 0 }
            ? rule.GridIndexes.ToHashSet()
            : null;

        foreach (var slot in slots)
        {
            if (slot?.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            if (selectedNames is not null && !selectedNames.Contains(slot.Name))
            {
                continue;
            }

            if (selectedNumbers is not null
                && (!TryReadSpecialSlotNumber(slot.Name, out var number) || !selectedNumbers.Contains(number)))
            {
                continue;
            }

            yield return slot;
        }
    }

    private static void PatchGridFilters(
        IEnumerable<GridFilter>? filters,
        CompatibilityRule rule,
        CompatibilityReport report)
    {
        if (filters is null)
        {
            return;
        }

        foreach (var filter in filters)
        {
            if (filter?.Filter is null)
            {
                continue;
            }

            PatchFilter(filter.Filter, rule, report);
        }
    }

    private static void PatchSlotFilters(
        IEnumerable<SlotFilter>? filters,
        CompatibilityRule rule,
        CompatibilityReport report)
    {
        if (filters is null)
        {
            return;
        }

        foreach (var filter in filters)
        {
            if (filter?.Filter is null)
            {
                continue;
            }

            PatchFilter(filter.Filter, rule, report);
        }
    }

    private static void PatchFilter(
        HashSet<MongoId> filter,
        CompatibilityRule rule,
        CompatibilityReport report)
    {
        report.FiltersTouched++;

        if (rule.Replace)
        {
            filter.Clear();
            foreach (var tpl in rule.AllowedTpls)
            {
                filter.Add(new MongoId(tpl));
            }

            report.EntriesReplaced += rule.AllowedTpls.Count;
            return;
        }

        foreach (var tpl in rule.AllowedTpls)
        {
            if (filter.Add(new MongoId(tpl)))
            {
                report.EntriesAdded++;
            }
        }
    }

    private void CountMissingAllowedTemplates(
        CompatibilityRule rule,
        CompatibilityReport report)
    {
        foreach (var tpl in rule.AllowedTpls)
        {
            if (!templates.Items.ContainsKey(new MongoId(tpl)))
            {
                report.MissingAllowedTemplates++;
                logger.Warning($"{ModInfo.LogPrefix} Compatibility allowed template '{tpl}' was not found.");
            }
        }
    }

    private static bool TryNormalizeRule(
        CompatibilityRule? input,
        out CompatibilityRule rule,
        out string reason)
    {
        rule = new CompatibilityRule();
        reason = string.Empty;

        if (input is null)
        {
            reason = "Rule cannot be null.";
            return false;
        }

        var target = input.TargetTpl?.Trim() ?? string.Empty;
        if (!IsSpecialSlotTarget(target) && !IsTemplateId(target))
        {
            reason = "TARGET-TPL must be a 24-character template ID or a Special Slot token.";
            return false;
        }

        var allowed = (input.AllowedTpls ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allowed.Count == 0)
        {
            reason = "ALLOWED_TPLS has no entries.";
            return false;
        }

        if (allowed.Any(value => !IsTemplateId(value)))
        {
            reason = "ALLOWED_TPLS contains an invalid template ID.";
            return false;
        }

        var gridIndexes = input.GridIndexes?.Distinct().OrderBy(index => index).ToList();
        if (gridIndexes?.Any(index => index < 0) == true)
        {
            reason = "GRID_INDEXES cannot contain negative values.";
            return false;
        }

        if (IsSpecialSlotTarget(target)
            && gridIndexes?.Any(index => index is < 1 or > 6) == true)
        {
            reason = "Special Slot indexes must be between 1 and 6.";
            return false;
        }

        var slotNames = input.SlotNames?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        rule = new CompatibilityRule
        {
            TargetTpl = target,
            AllowedTpls = allowed,
            GridIndexes = gridIndexes,
            SlotNames = slotNames is { Count: > 0 } ? slotNames : null,
            Replace = input.Replace
        };
        return true;
    }

    private static bool IsSpecialSlotTarget(string value)
    {
        return string.Equals(value, PmcSpecialSlots, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, GlobalSpecialSlots, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, SpecialSlots, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemplateId(string value)
    {
        return value.Length == 24 && value.All(Uri.IsHexDigit);
    }

    private static bool TryReadSpecialSlotNumber(string name, out int number)
    {
        const string prefix = "SpecialSlot";
        return int.TryParse(name[prefix.Length..], out number);
    }

    private static bool IsPocketTemplate(TemplateItem item)
    {
        return string.Equals(item.Properties?.Name, "Pockets", StringComparison.OrdinalIgnoreCase)
            && item.Properties?.Slots is not null;
    }

    private static bool IsConfigFile(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetConfigFolder()
    {
        var modFolder = IOPath.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Could not resolve the mod folder.");
        return IOPath.Combine(modFolder, "config", "compat");
    }
}
