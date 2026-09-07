using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace SPTEssentials.Server;

[Injectable(InjectionType.Singleton)]
public sealed class EssentialsServerConfig
{
    private const string ConfigFileName = "com.senze.sptessentials.cfg";

    private readonly ISptLogger<EssentialsServerConfig> _logger;

    public bool EnableFieldRepair { get; private set; } = true;
    public bool EnableSmartAirFilter { get; private set; } = true;
    public bool EnableExpandedSpecialSlots { get; private set; } = true;
    public bool EnableCompatibilityRules { get; private set; } = true;

    public EssentialsServerConfig(ISptLogger<EssentialsServerConfig> logger)
    {
        _logger = logger;
        Load();
    }

    private void Load()
    {
        var configPath = FindConfigPath();
        if (configPath is null)
        {
            _logger.Info(
                $"{ModInfo.LogPrefix} {ConfigFileName} was not found yet. Server modules will use their defaults.");
            return;
        }

        try
        {
            var values = ReadBooleanValues(configPath);
            EnableFieldRepair = Read(values, "Enable Field Repair", EnableFieldRepair);
            EnableSmartAirFilter = Read(values, "Enable Smart Air Filter", EnableSmartAirFilter);
            EnableExpandedSpecialSlots = Read(
                values,
                "Enable Expanded Special Slots",
                EnableExpandedSpecialSlots);
            EnableCompatibilityRules = Read(
                values,
                "Enable Compatibility Rules",
                EnableCompatibilityRules);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(
                $"{ModInfo.LogPrefix} Could not read {ConfigFileName}: {exception.Message}. Server modules will use their defaults.");
        }
    }

    private static Dictionary<string, bool> ReadBooleanValues(string path)
    {
        var values = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceLine in File.ReadLines(path))
        {
            var line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (bool.TryParse(value, out var enabled))
            {
                values[name] = enabled;
            }
        }

        return values;
    }

    private static bool Read(
        IReadOnlyDictionary<string, bool> values,
        string name,
        bool defaultValue)
    {
        return values.TryGetValue(name, out var value) ? value : defaultValue;
    }

    private static string? FindConfigPath()
    {
        var modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var current = string.IsNullOrWhiteSpace(modDirectory)
            ? null
            : new DirectoryInfo(modDirectory);

        while (current is not null)
        {
            var bepinexDirectory = Path.Combine(current.FullName, "BepInEx");
            if (Directory.Exists(bepinexDirectory))
            {
                return Path.Combine(bepinexDirectory, "config", ConfigFileName);
            }

            current = current.Parent;
        }

        return null;
    }
}
