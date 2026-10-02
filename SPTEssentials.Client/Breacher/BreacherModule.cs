using System;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SPTEssentials.Client.Breacher;

internal sealed class BreacherModule : ClientModule
{
    private const int RequiredShots = 2;
    private static readonly BreacherShotCounter ShotCounter = new BreacherShotCounter(RequiredShots);
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".breacher");

    protected override string Name => "Breacher";

    protected override void Enable()
    {
        Patch(_harmony, typeof(ApplyHitPatch));
        GameWorld.OnDispose += Reset;
        SPTEssentialsPlugin.Settings.EnableBreacher.SettingChanged += OnSettingChanged;
    }

    internal void Shutdown()
    {
        GameWorld.OnDispose -= Reset;
        SPTEssentialsPlugin.Settings.EnableBreacher.SettingChanged -= OnSettingChanged;
        _harmony.UnpatchSelf();
        Reset();
    }

    private static void OnSettingChanged(object sender, EventArgs eventArgs)
    {
        if (!SPTEssentialsPlugin.Settings.EnableBreacher.Value)
        {
            Reset();
        }
    }

    private static void Reset()
    {
        ShotCounter.Clear();
    }

    private static bool IsEligibleDoor(Door door)
    {
        return door != null
            && door.GetType() == typeof(Door)
            && !door.IsBroken
            && door.Operatable
            && !door.NoInteractionsAllowed
            && (door.DoorState == EDoorState.Locked || door.DoorState == EDoorState.Shut);
    }

    private static Door FindDoor(BallisticCollider ballisticCollider, DamageInfo damageInfo)
    {
        var door = damageInfo.HitCollider?.GetComponentInParent<Door>();
        if (door != null)
        {
            return door;
        }

        return ballisticCollider?.GetComponentInParent<Door>();
    }

    [HarmonyPatch(typeof(BallisticCollider), nameof(BallisticCollider.ApplyHit))]
    private static class ApplyHitPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BallisticCollider __instance, DamageInfo __0)
        {
            if (!SPTEssentialsPlugin.Settings.EnableBreacher.Value
                || !Singleton<GameWorld>.Instantiated
                || !(__0.Weapon is Weapon weapon)
                || !string.Equals(weapon.WeapClass, "shotgun", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var player = __0.Player?.iPlayer;
            if (player == null || !player.IsYourPlayer)
            {
                return;
            }

            var door = FindDoor(__instance, __0);
            if (!IsEligibleDoor(door))
            {
                return;
            }

            var doorId = door.GetInstanceID();
            if (!ShotCounter.RegisterShot(doorId, weapon.Id, __0.FireIndex))
            {
                return;
            }

            door.KickOpen(player.Position, true);
        }
    }
}
