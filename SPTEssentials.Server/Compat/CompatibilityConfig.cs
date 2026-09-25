using System.Text.Json.Serialization;

namespace SPTEssentials.Server.Compat;

internal sealed class CompatibilityConfig
{
    [JsonPropertyName("rules")]
    public List<CompatibilityRule?> Rules { get; init; } = [];
}

internal sealed class CompatibilityRule
{
    [JsonPropertyName("TARGET-TPL")]
    public string TargetTpl { get; init; } = string.Empty;

    [JsonPropertyName("ALLOWED_TPLS")]
    public List<string> AllowedTpls { get; init; } = [];

    [JsonPropertyName("GRID_INDEXES")]
    public List<int>? GridIndexes { get; init; }

    [JsonPropertyName("SLOT_NAMES")]
    public List<string>? SlotNames { get; init; }

    [JsonPropertyName("SLOT_RULES")]
    public List<CompatibilitySlotRule?>? SlotRules { get; init; }

    [JsonPropertyName("REPLACE")]
    public bool Replace { get; init; }
}

internal sealed class CompatibilitySlotRule
{
    [JsonPropertyName("ALLOWED_TPLS")]
    public List<string> AllowedTpls { get; init; } = [];

    [JsonPropertyName("SLOT_NAMES")]
    public List<string> SlotNames { get; init; } = [];
}

internal sealed class CompatibilityReport
{
    public int Files { get; set; }
    public int Rules { get; set; }
    public int InvalidRules { get; set; }
    public int MissingTargets { get; set; }
    public int MissingAllowedTemplates { get; set; }
    public int FiltersTouched { get; set; }
    public int EntriesAdded { get; set; }
    public int EntriesReplaced { get; set; }
}
