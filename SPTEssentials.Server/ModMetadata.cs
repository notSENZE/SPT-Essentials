using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace SPTEssentials.Server;

public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = ModInfo.Guid;
    public string Name { get; init; } = ModInfo.Name;
    public string Author { get; init; } = "Senze";
    public string License { get; init; } = "MIT";
    public Version Version { get; init; } = new(ModInfo.Version);
    public Range SptVersion { get; init; } = new("~4.1.6");
    public string? Url { get; init; } = "https://github.com/notSENZE/SPT-Essentials";
    public List<string>? Contributors { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        ["com.wtt.commonlib"] = new Range("~3.0.0")
    };
    public List<string>? Incompatibilities { get; init; } = ["com.better.spt"];
    public bool HasPrepatcher { get; init; }
}
