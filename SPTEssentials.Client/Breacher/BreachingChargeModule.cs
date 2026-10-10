using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.Communications;
using EFT.InputSystem;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using EFT.UI;
using EFT.UI.Screens;
using HarmonyLib;
using UnityEngine;
using GameEffects = Systems.Effects.Effects;

namespace SPTEssentials.Client.Breacher;

internal sealed class BreachingChargeModule
{
    internal const string ItemTemplateId = "6ac93f365b9b86dcfded477e";

    private const string WttPluginGuid = "com.wtt.commonlib";
    private const string BundleName = "spte_breaching_charge.bundle";
    private const string PrefabName = "SPTEssentialsBreachingCharge";
    private const string PrefabAssetPath = "assets/breachingcharge/generated/sptessentialsbreachingcharge.prefab";
    private const string PlantActionName = "Plant Breaching Charge";
    private const float FuseDuration = 5f;
    private const float RecoveryDuration = 1f;
    private const float InteractionDistance = 4f;
    private const float DoorSurfaceOffset = 0.015f;

    private readonly Harmony _harmony = new(SPTEssentialsPlugin.PluginGuid + ".breachingcharge");
    private readonly List<PlacedBreachingCharge> _charges = new();
    private readonly Dictionary<Door, PlacedBreachingCharge> _chargesByDoor = new();

    private static BreachingChargeModule _activeModule;

    private GameObject _chargePrefab;
    private AudioClip _beepClip;
    private bool _inventoryOperationPending;
    private PendingPlacement _pendingPlacement;
    private string _pendingPlacementError;
    private PlacedBreachingCharge _recoveryTarget;
    private float _recoveryProgress;
    private GUIStyle _labelStyle;
    private PlayerCameraController _playerCameraController;

    internal void EnableSafely()
    {
        if (!SPTEssentialsPlugin.Settings.EnableBreacher.Value)
        {
            return;
        }

        _activeModule = this;
        _harmony.CreateClassProcessor(typeof(DoorActionsPatch)).Patch();
        _harmony.CreateClassProcessor(typeof(KeycardDoorActionsPatch)).Patch();
        _harmony.CreateClassProcessor(typeof(InputDispatchPatch)).Patch();
    }

    internal void Update()
    {
        if (!SPTEssentialsPlugin.Settings.EnableBreacher.Value || !TryGetRaidPlayer(out var player))
        {
            ClearRaidState();
            return;
        }

        CompletePendingPlacement(player);
        UpdateCharges();

        if (_inventoryOperationPending || player.IsInventoryOpened)
        {
            ResetRecovery();
            return;
        }

        UpdateRecovery(player);
    }

    internal void OnGui()
    {
        if (_recoveryTarget == null)
        {
            return;
        }

        _labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        var area = new Rect(Screen.width * 0.5f - 300f, Screen.height * 0.72f, 600f, 70f);
        if (_recoveryProgress <= 0f)
        {
            GUI.Label(area, "Hold F to remove the Breaching Charge", _labelStyle);
            return;
        }

        GUI.Label(area, $"Removing Breaching Charge… {Mathf.RoundToInt(_recoveryProgress * 100f)}%", _labelStyle);
    }

    internal void Shutdown()
    {
        _harmony.UnpatchSelf();
        if (ReferenceEquals(_activeModule, this))
        {
            _activeModule = null;
        }

        ClearRaidState();
        if (_beepClip != null)
        {
            UnityEngine.Object.Destroy(_beepClip);
            _beepClip = null;
        }
    }

    private void AddPlantAction(GamePlayerOwner owner, Door door, AvailableInteractionState state)
    {
        if (owner?.Player == null
            || state?.Actions == null
            || !IsEligibleDoor(door)
            || _chargesByDoor.ContainsKey(door)
            || state.Actions.Any(action => action.Name == PlantActionName)
            || !HasCharge(owner.Player))
        {
            return;
        }

        state.Actions.Add(new InteractionAction
        {
            Name = PlantActionName,
            Disabled = _inventoryOperationPending,
            Action = () =>
            {
                owner.ClearInteractionState();
                TryPlantCharge(owner.Player, door);
            }
        });
        state.Error = null;
    }

    private void TryPlantCharge(Player player, Door door)
    {
        if (_inventoryOperationPending || !IsEligibleDoor(door) || _chargesByDoor.ContainsKey(door))
        {
            return;
        }

        if (GetChargePrefab() == null)
        {
            SPTEssentialsPlugin.Log.LogError("The Breaching Charge prefab could not be loaded from its bundle.");
            Notify("The Breaching Charge model could not be loaded. Please check the log.");
            return;
        }

        var item = player.Inventory.GetAllItemByTemplate(ItemTemplateId).FirstOrDefault();
        if (item == null)
        {
            Notify("You do not have a Breaching Charge.");
            return;
        }

        if (!TryFindDoorSurface(player, door, out var position, out var normal))
        {
            Notify("The Breaching Charge cannot be attached here.");
            return;
        }

        var controller = player.InventoryController;
        var removeResult = ItemManipulator.Remove(item, controller, true);
        if (removeResult.Failed)
        {
            SPTEssentialsPlugin.Log.LogError($"Breaching Charge removal failed: {removeResult.Error}");
            Notify("The Breaching Charge could not be used. Please check the log.");
            return;
        }

        _inventoryOperationPending = true;
        try
        {
            var world = Singleton<GameWorld>.Instance;
            controller.RunNetworkTransaction(removeResult.Value, result =>
            {
                _inventoryOperationPending = false;
                if (result == null || result.Failed)
                {
                    _pendingPlacementError = result?.Error ?? "The inventory transaction returned no result.";
                    return;
                }

                _pendingPlacement = new PendingPlacement(world, player, door, position, normal);
            });
        }
        catch (Exception exception)
        {
            _inventoryOperationPending = false;
            SPTEssentialsPlugin.Log.LogError($"Breaching Charge transaction could not start: {exception}");
            Notify("The Breaching Charge could not be used. Please check the log.");
        }
    }

    private void CompletePendingPlacement(Player currentPlayer)
    {
        if (!string.IsNullOrEmpty(_pendingPlacementError))
        {
            SPTEssentialsPlugin.Log.LogError($"Breaching Charge transaction failed: {_pendingPlacementError}");
            _pendingPlacementError = null;
            Notify("The Breaching Charge could not be used. Please check the log.");
        }

        var placement = _pendingPlacement;
        if (placement == null)
        {
            return;
        }

        _pendingPlacement = null;
        if (!Singleton<GameWorld>.Instantiated
            || !ReferenceEquals(Singleton<GameWorld>.Instance, placement.World)
            || !ReferenceEquals(currentPlayer, placement.Player)
            || !IsEligibleDoor(placement.Door)
            || _chargesByDoor.ContainsKey(placement.Door))
        {
            return;
        }

        try
        {
            var charge = CreatePlacedCharge(placement);
            _charges.Add(charge);
            _chargesByDoor[placement.Door] = charge;
            Notify("Breaching Charge armed. Five seconds.");
        }
        catch (Exception exception)
        {
            SPTEssentialsPlugin.Log.LogError($"Breaching Charge creation failed: {exception}");
            Notify("The Breaching Charge could not be placed after it was used. Please check the log.");
        }
    }

    private PlacedBreachingCharge CreatePlacedCharge(PendingPlacement placement)
    {
        var prefab = GetChargePrefab();
        if (prefab == null)
        {
            throw new InvalidOperationException("The Breaching Charge prefab is unavailable.");
        }

        var rotation = Quaternion.LookRotation(placement.Normal, Vector3.up);
        var root = UnityEngine.Object.Instantiate(prefab, placement.Position, rotation);
        root.name = "SPT Essentials Breaching Charge (Armed)";
        root.transform.SetParent(placement.Door.transform, true);

        var collider = root.GetComponentInChildren<Collider>(true);
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true;
        var audioSource = root.GetComponent<AudioSource>() ?? root.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 35f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        var marker = root.AddComponent<PlacedBreachingCharge>();
        marker.Door = placement.Door;
        marker.PlacedBy = placement.Player;
        marker.DetonationTime = Time.time + FuseDuration;
        marker.NextBeepTime = Time.time + 0.5f;
        marker.AudioSource = audioSource;
        return marker;
    }

    private GameObject GetChargePrefab()
    {
        if (_chargePrefab != null)
        {
            return _chargePrefab;
        }

        foreach (var loadedBundle in AssetBundle.GetAllLoadedAssetBundles())
        {
            var assetPath = loadedBundle.GetAllAssetNames()
                .FirstOrDefault(name => string.Equals(name, PrefabAssetPath, StringComparison.OrdinalIgnoreCase));
            if (assetPath == null)
            {
                continue;
            }

            _chargePrefab = loadedBundle.LoadAsset<GameObject>(assetPath);
            if (_chargePrefab != null)
            {
                return _chargePrefab;
            }
        }

        if (!Chainloader.PluginInfos.TryGetValue(WttPluginGuid, out var pluginInfo)
            || pluginInfo.Instance is not WTTClientCommonLib.WTTClientCommonLib commonLib
            || commonLib.AssetLoader == null)
        {
            return null;
        }

        _chargePrefab = commonLib.AssetLoader.LoadPrefabFromBundle(BundleName, PrefabName);
        return _chargePrefab;
    }

    private void UpdateCharges()
    {
        for (var index = _charges.Count - 1; index >= 0; index--)
        {
            var charge = _charges[index];
            if (charge == null)
            {
                _charges.RemoveAt(index);
                continue;
            }

            if (Time.time >= charge.DetonationTime)
            {
                Detonate(charge);
                continue;
            }

            if (Time.time >= charge.NextBeepTime)
            {
                PlayBeep(charge);
                var remaining = charge.DetonationTime - Time.time;
                charge.NextBeepTime = Time.time + (remaining <= 1.25f ? 0.2f : 0.75f);
            }
        }
    }

    private void PlayBeep(PlacedBreachingCharge charge)
    {
        _beepClip ??= CreateBeepClip();
        charge.AudioSource?.PlayOneShot(_beepClip, 0.9f);
    }

    private void Detonate(PlacedBreachingCharge charge)
    {
        var position = charge.transform.position;
        var door = charge.Door;
        var placedBy = charge.PlacedBy;

        RemoveCharge(charge);

        if (Singleton<GameEffects>.Instantiated)
        {
            Singleton<GameEffects>.Instance.EmitGrenade("Grenade_new", position, Vector3.up, 1f);
        }

        if (door == null)
        {
            return;
        }

        door.KickOpen(placedBy != null ? placedBy.Position : position, true);
    }

    private void UpdateRecovery(Player player)
    {
        var camera = GetPlayerCamera(player);
        if (camera == null || !TryFindRecoveryTarget(player, camera, out var target))
        {
            ResetRecovery();
            return;
        }

        if (!ReferenceEquals(_recoveryTarget, target))
        {
            _recoveryTarget = target;
            _recoveryProgress = 0f;
        }

        if (!Input.GetKey(KeyCode.F))
        {
            _recoveryProgress = 0f;
            return;
        }

        _recoveryProgress += Time.deltaTime / RecoveryDuration;
        if (_recoveryProgress < 1f)
        {
            return;
        }

        ResetRecovery();
        _ = RecoverChargeAsync(target, player);
    }

    private async Task RecoverChargeAsync(PlacedBreachingCharge charge, Player player)
    {
        if (charge == null)
        {
            return;
        }

        var remainingFuse = Mathf.Max(0.1f, charge.DetonationTime - Time.time);
        charge.DetonationTime = float.PositiveInfinity;
        _inventoryOperationPending = true;
        try
        {
            var controller = player.InventoryController;
            var item = Singleton<ItemFactory>.Instance.CreateItem(controller.NextId.ToString(), ItemTemplateId, null);
            item.SpawnedInSession = false;
            await ChangeItemsOperation.LoadBundles(item);

            if (!TryFindInventoryAddress(player, item, out var address))
            {
                Notify("No room for the Breaching Charge. It remains attached.");
                return;
            }

            var addResult = ItemManipulator.Add(item, address, controller, false);
            if (addResult.Failed)
            {
                SPTEssentialsPlugin.Log.LogError($"Breaching Charge recovery failed: {addResult.Error}");
                Notify("The Breaching Charge could not be removed. It remains attached.");
                return;
            }

            addResult.Value.RaiseEvents(controller, CommandStatus.Begin);
            addResult.Value.RaiseEvents(controller, CommandStatus.Succeed);
            RemoveCharge(charge);
            Notify("Breaching Charge removed.");
        }
        catch (Exception exception)
        {
            SPTEssentialsPlugin.Log.LogError($"Breaching Charge recovery interrupted: {exception}");
            Notify("The Breaching Charge could not be removed. It remains attached.");
        }
        finally
        {
            if (charge != null && _charges.Contains(charge))
            {
                charge.DetonationTime = Time.time + remainingFuse;
            }

            _inventoryOperationPending = false;
        }
    }

    private void RemoveCharge(PlacedBreachingCharge charge)
    {
        if (charge == null)
        {
            return;
        }

        _charges.Remove(charge);
        if (charge.Door != null)
        {
            _chargesByDoor.Remove(charge.Door);
        }

        if (ReferenceEquals(_recoveryTarget, charge))
        {
            ResetRecovery();
        }

        UnityEngine.Object.Destroy(charge.gameObject);
    }

    private bool TryFindRecoveryTarget(Player player, Camera camera, out PlacedBreachingCharge target)
    {
        var hits = Physics.SphereCastAll(
            camera.transform.position,
            0.12f,
            camera.transform.forward,
            InteractionDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(player.Transform.Original))
            {
                continue;
            }

            var candidate = hit.collider.GetComponentInParent<PlacedBreachingCharge>();
            if (candidate != null && _charges.Contains(candidate))
            {
                target = candidate;
                return true;
            }

            if (!hit.collider.isTrigger)
            {
                break;
            }
        }

        target = null;
        return false;
    }

    private bool TryFindDoorSurface(Player player, Door door, out Vector3 position, out Vector3 normal)
    {
        var camera = GetPlayerCamera(player);
        if (camera != null)
        {
            var hits = Physics.RaycastAll(
                camera.transform.position,
                camera.transform.forward,
                InteractionDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.collider?.GetComponentInParent<Door>() != door)
                {
                    continue;
                }

                normal = hit.normal.normalized;
                position = hit.point + normal * DoorSurfaceOffset;
                return true;
            }
        }

        var renderers = door.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            position = default;
            normal = default;
            return false;
        }

        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1))
        {
            bounds.Encapsulate(renderer.bounds);
        }

        var source = camera != null ? camera.transform.position : player.Position;
        position = bounds.ClosestPoint(source);
        normal = source - bounds.center;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.001f)
        {
            normal = -door.transform.forward;
        }

        normal.Normalize();
        position += normal * DoorSurfaceOffset;
        return true;
    }

    private static bool TryFindInventoryAddress(Player player, Item item, out ItemAddress address)
    {
        address = null;
        foreach (var grid in player.Inventory.Equipment.GetPrioritizedGridsForLoot(item))
        {
            var candidate = grid.FindLocationForItem(item);
            if (candidate == null)
            {
                continue;
            }

            address = candidate;
            return true;
        }

        return false;
    }

    private Camera GetPlayerCamera(Player player)
    {
        if (_playerCameraController != null
            && ReferenceEquals(_playerCameraController.Player, player)
            && _playerCameraController.Camera != null)
        {
            return _playerCameraController.Camera;
        }

        _playerCameraController = UnityEngine.Object.FindObjectsOfType<PlayerCameraController>()
            .FirstOrDefault(controller => ReferenceEquals(controller.Player, player) && controller.Camera != null);
        return _playerCameraController?.Camera;
    }

    private static bool TryGetRaidPlayer(out Player player)
    {
        player = null;
        var screenManager = EftScreenManager.Instance;
        if (screenManager?.CurrentScreenController?.ScreenType != EEftScreenType.BattleUI
            || !Singleton<GameWorld>.Instantiated)
        {
            return false;
        }

        player = Singleton<GameWorld>.Instance?.MainPlayer;
        return player is LocalPlayer
            && player.IsYourPlayer
            && player.HealthController.IsAlive
            && player.InventoryController != null;
    }

    private static bool IsEligibleDoor(Door door)
    {
        return door != null
            && door.specificInteractionContext == ESpecificInteractionContext.None
            && (door.DoorState == EDoorState.Locked || door.DoorState == EDoorState.Shut);
    }

    private static bool HasCharge(Player player)
    {
        return player?.Inventory?.GetAllItemByTemplate(ItemTemplateId).Any() == true;
    }

    private static AudioClip CreateBeepClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.09f;
        var samples = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[samples];
        for (var index = 0; index < samples; index++)
        {
            var time = index / (float)sampleRate;
            var envelope = 1f - index / (float)samples;
            data[index] = Mathf.Sin(2f * Mathf.PI * 1400f * time) * envelope * 0.28f;
        }

        var clip = AudioClip.Create("SPT Essentials Breaching Charge Beep", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void ClearRaidState()
    {
        _pendingPlacement = null;
        _pendingPlacementError = null;
        _inventoryOperationPending = false;
        ResetRecovery();

        foreach (var charge in _charges)
        {
            if (charge != null)
            {
                UnityEngine.Object.Destroy(charge.gameObject);
            }
        }

        _charges.Clear();
        _chargesByDoor.Clear();
    }

    private void ResetRecovery()
    {
        _recoveryTarget = null;
        _recoveryProgress = 0f;
    }

    private static void Notify(string message)
    {
        NotificationManager.DisplayMessageNotification(
            message,
            ENotificationDurationType.Default,
            ENotificationIconType.Default,
            null);
    }

    [HarmonyPatch(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableActions), typeof(GamePlayerOwner), typeof(Door))]
    private static class DoorActionsPatch
    {
        private static void Postfix(GamePlayerOwner owner, Door door, AvailableInteractionState __result)
        {
            _activeModule?.AddPlantAction(owner, door, __result);
        }
    }

    [HarmonyPatch(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableActions), typeof(GamePlayerOwner), typeof(KeycardDoor), typeof(bool))]
    private static class KeycardDoorActionsPatch
    {
        private static void Postfix(GamePlayerOwner owner, KeycardDoor door, AvailableInteractionState __result)
        {
            _activeModule?.AddPlantAction(owner, door, __result);
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.DispatchInput))]
    private static class InputDispatchPatch
    {
        private static void Prefix(List<ECommand> commandsList)
        {
            if (_activeModule?._recoveryTarget == null || commandsList == null)
            {
                return;
            }

            commandsList.RemoveAll(command =>
                command == ECommand.BeginInteracting
                || command == ECommand.EndInteracting);
        }
    }

    private sealed class PendingPlacement
    {
        internal readonly GameWorld World;
        internal readonly Player Player;
        internal readonly Door Door;
        internal readonly Vector3 Position;
        internal readonly Vector3 Normal;

        internal PendingPlacement(GameWorld world, Player player, Door door, Vector3 position, Vector3 normal)
        {
            World = world;
            Player = player;
            Door = door;
            Position = position;
            Normal = normal;
        }
    }
}
