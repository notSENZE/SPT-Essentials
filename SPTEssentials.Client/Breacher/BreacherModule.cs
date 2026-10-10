using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using RocketProjectile = EFT.RocketLauncher.RocketProjectile;

namespace SPTEssentials.Client.Breacher;

internal sealed class BreacherModule : ClientModule
{
    private const float LockHitRadius = 0.3f;
    private const float ExplosiveDoorSearchRadius = 1.25f;
    private static readonly BreacherShotCounter ShotCounter = new BreacherShotCounter();
    private static readonly HashSet<int> DiagnosedColliders = new HashSet<int>();
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".breacher");

    protected override string Name => "Breacher";

    protected override void Enable()
    {
        Patch(_harmony, typeof(ApplyHitPatch));
        Patch(_harmony, typeof(RocketExplosionPatch));
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
        DiagnosedColliders.Clear();
    }

    private static bool IsEligibleDoor(Door door)
    {
        return door != null
            && !(door is KeycardDoor)
            && !(door is SlidingDoor)
            && !door.IsBroken
            && door.Operatable
            && !door.NoInteractionsAllowed
            && door.DoorState == EDoorState.Locked;
    }

    private static bool IsEligibleExplosiveDoor(Door door)
    {
        return door != null
            && door.specificInteractionContext == ESpecificInteractionContext.None
            && (door.DoorState == EDoorState.Locked || door.DoorState == EDoorState.Shut);
    }

    private static Door FindDoor(Collider collider, BallisticCollider ballisticCollider = null)
    {
        var door = collider?.GetComponentInParent<Door>();
        return door ?? ballisticCollider?.GetComponentInParent<Door>();
    }

    private static Door FindNearestExplosiveDoor(
        Vector3 explosionPosition,
        Collider preferredCollider = null,
        BallisticCollider preferredBallisticCollider = null)
    {
        var preferredDoor = FindDoor(preferredCollider, preferredBallisticCollider);
        if (IsEligibleExplosiveDoor(preferredDoor))
        {
            return preferredDoor;
        }

        Door nearestDoor = null;
        var nearestDistance = ExplosiveDoorSearchRadius;
        foreach (var door in UnityEngine.Object.FindObjectsOfType<Door>())
        {
            if (!IsEligibleExplosiveDoor(door))
            {
                continue;
            }

            var distance = GetDistanceToDoor(door, explosionPosition);
            if (distance <= nearestDistance)
            {
                nearestDoor = door;
                nearestDistance = distance;
            }
        }

        return nearestDoor;
    }

    private static float GetDistanceToDoor(Door door, Vector3 position)
    {
        var colliders = door.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            var shortestDistance = float.MaxValue;
            foreach (var collider in colliders)
            {
                var closestPoint = collider.bounds.ClosestPoint(position);
                shortestDistance = Mathf.Min(
                    shortestDistance,
                    Vector3.Distance(position, closestPoint));
            }

            return shortestDistance;
        }

        var renderers = door.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return Vector3.Distance(position, bounds.ClosestPoint(position));
        }

        return Vector3.Distance(position, door.transform.position);
    }

    private static void OpenExplosiveDoor(Door door, Vector3 sourcePosition)
    {
        if (IsEligibleExplosiveDoor(door))
        {
            door.KickOpen(sourcePosition, true);
        }
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

    private static bool HitLock(Door door, DamageInfo damageInfo)
    {
        var lockTransform = door.LockHandle != null
            ? door.LockHandle.transform
            : door.Handle;

        if (lockTransform == null)
        {
            return false;
        }

        var hitTransform = damageInfo.HitCollider?.transform;
        if (hitTransform != null
            && (hitTransform == lockTransform || hitTransform.IsChildOf(lockTransform)))
        {
            return true;
        }

        return Vector3.Distance(damageInfo.HitPoint, lockTransform.position) <= LockHitRadius;
    }

    private static void LogDoorDiagnostic(
        BallisticCollider ballisticCollider,
        DamageInfo damageInfo,
        Door door)
    {
        if (!BreacherDoorRules.IsBreachingAmmo(damageInfo.SourceId))
        {
            return;
        }

        var hitCollider = damageInfo.HitCollider;
        var diagnosticObject = (UnityEngine.Object)hitCollider ?? ballisticCollider;
        if (diagnosticObject == null || !DiagnosedColliders.Add(diagnosticObject.GetInstanceID()))
        {
            return;
        }

        if (door == null)
        {
            SPTEssentialsPlugin.Log.LogInfo(
                $"Breacher diagnostic: no Door parent found. "
                + $"HitCollider='{hitCollider?.name ?? "none"}', "
                + $"BallisticCollider='{ballisticCollider?.name ?? "none"}', "
                + $"Hierarchy='{BuildHierarchyPath(hitCollider?.transform ?? ballisticCollider?.transform)}'.");
            return;
        }

        var lockTransform = door.LockHandle != null
            ? door.LockHandle.transform
            : door.Handle;
        var distance = lockTransform == null
            ? "n/a"
            : Vector3.Distance(damageInfo.HitPoint, lockTransform.position).ToString("0.000");
        var candidateParts = string.Join(
            ", ",
            door.GetComponentsInChildren<Transform>(true)
                .Where(transform => LooksLikeLockPart(transform.name))
                .Select(transform => transform.name)
                .Distinct()
                .Take(12));

        SPTEssentialsPlugin.Log.LogInfo(
            $"Breacher diagnostic: DoorType='{door.GetType().FullName}', "
            + $"DoorName='{door.name}', DoorId='{door.Id}', KeyId='{door.KeyId}', "
            + $"State='{door.DoorState}', HitCollider='{hitCollider?.name ?? "none"}', "
            + $"LockTransform='{lockTransform?.name ?? "none"}', LockDistance='{distance}', "
            + $"CandidateParts='{candidateParts}'.");
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "none";
        }

        var names = new List<string>();
        while (transform != null && names.Count < 10)
        {
            names.Add(transform.name);
            transform = transform.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static bool LooksLikeLockPart(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.IndexOf("lock", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("handle", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("latch", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("pad", StringComparison.OrdinalIgnoreCase) >= 0;
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
                || !string.Equals(weapon.WeapClass, "shotgun", StringComparison.OrdinalIgnoreCase)
                || !BreacherDoorRules.IsBreachingWeapon(weapon.TemplateId))
            {
                return;
            }

            var player = __0.Player?.iPlayer;
            if (player == null || !player.IsYourPlayer)
            {
                return;
            }

            var door = FindDoor(__instance, __0);
            LogDoorDiagnostic(__instance, __0, door);
            if (!IsEligibleDoor(door))
            {
                return;
            }

            if (!BreacherDoorRules.TryGetRequiredShots(
                    door.KeyId,
                    weapon.TemplateId,
                    __0.SourceId,
                    out var requiredShots)
                || !HitLock(door, __0))
            {
                return;
            }

            var doorId = door.GetInstanceID();
            if (!ShotCounter.RegisterShot(doorId, weapon.Id, __0.FireIndex, requiredShots))
            {
                return;
            }

            door.KickOpen(player.Position, true);
        }
    }

    [HarmonyPatch(typeof(RocketProjectile), "Explosion")]
    private static class RocketExplosionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            EFT.Ballistics.Shot ____rocketShot,
            IPlayer ____owner,
            Weapon ____weapon)
        {
            if (!SPTEssentialsPlugin.Settings.EnableBreacher.Value
                || ____owner == null
                || !____owner.IsYourPlayer
                || !ExplosiveBreacherRules.IsBreachingRocketLauncher(
                    ____weapon?.TemplateId))
            {
                return;
            }

            var explosionPosition = ____rocketShot != null
                ? ____rocketShot.HitPoint
                : ____owner.Position;
            var door = FindNearestExplosiveDoor(
                explosionPosition,
                ____rocketShot?.HitCollider,
                ____rocketShot?.HittedBallisticCollider);
            OpenExplosiveDoor(door, ____owner.Position);
        }
    }
}
