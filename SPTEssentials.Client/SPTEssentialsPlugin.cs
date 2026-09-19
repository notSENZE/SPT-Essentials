using BepInEx;
using BepInEx.Logging;
using SPTEssentials.Client.AirFilter;
using SPTEssentials.Client.CompactHud;
using SPTEssentials.Client.FieldRepair;
using SPTEssentials.Client.Pause;
using SPTEssentials.Client.Reload;
using SPTEssentials.Client.Reticle;
using SPTEssentials.Client.SpecialSlots;
using SPTEssentials.Client.Tripwire;

namespace SPTEssentials.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.SPT.core", "4.1.5")]
public sealed class SPTEssentialsPlugin : BaseUnityPlugin
{
    internal const string PluginGuid = "com.senze.sptessentials";
    internal const string PluginName = "Senze-SPTEssentials";
    internal const string PluginVersion = "1.1.0";

    internal static ManualLogSource Log { get; private set; }
    internal static EssentialsConfig Settings { get; private set; }
    internal static SPTEssentialsPlugin Instance { get; private set; }

    private RaidPauseModule _pause;
    private CompactHudController _compactHud;
    private CustomReticleColorModule _reticleColor;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        Settings = new EssentialsConfig(Config);
        CompactHudSettings.Bind(Config, Settings.EnableCompactHud);

        _pause = new RaidPauseModule();
        _compactHud = new CompactHudController();
        _reticleColor = new CustomReticleColorModule();

        _pause.EnableSafely();
        new MagazineRetentionModule().EnableSafely();
        new KeepTripwireKitModule().EnableSafely();
        new ExtraSpecialSlotsLayoutModule().EnableSafely();
        new FieldArmorRepairModule().EnableSafely();
        new RaidOnlyAirFilterModule().EnableSafely();
        _reticleColor.EnableSafely();

        Log.LogInfo($"{PluginName} {PluginVersion} loaded for SPT 4.1.x.");
    }

    private void Update()
    {
        _pause?.Update();
        _compactHud?.Tick();
    }

    private void OnGUI()
    {
        _pause?.OnGui();
        _compactHud?.Draw();
    }

    private void OnDestroy()
    {
        _pause?.Shutdown();
        _compactHud?.Dispose();
        _reticleColor?.Shutdown();
        Instance = null;
    }
}
