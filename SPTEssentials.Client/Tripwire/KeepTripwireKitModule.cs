using System;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using EFT.SynchronizableObjects;
using HarmonyLib;
using UnityEngine;

namespace SPTEssentials.Client.Tripwire;

internal sealed class KeepTripwireKitModule : ClientModule
{
    private const string KitTemplateId = "666b11055a706400b717cfa5";
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".keeptripwirekit");

    protected override string Name => "Keep Tripwire Kit";

    protected override void Enable()
    {
        Patch(_harmony, typeof(DisarmCompletedPatch));
    }

    private static async Task ReturnKitAsync(DisarmContext context)
    {
        GameWorld.OnDispose += context.OnRaidEnded;
        try
        {
            var controller = context.Player.InventoryController;
            var kit = Singleton<ItemFactory>.Instance.CreateItem(controller.NextId.ToString(), KitTemplateId, null);
            kit.SpawnedInSession = false;

            await ChangeItemsOperation.LoadBundles(kit);

            if (context.RaidEnded
                || context.World == null
                || !Singleton<GameWorld>.Instantiated
                || !ReferenceEquals(Singleton<GameWorld>.Instance, context.World)
                || context.Player == null
                || !ReferenceEquals(context.World.MainPlayer, context.Player))
            {
                return;
            }

            var loot = context.World.ThrowItem(
                kit,
                context.Player,
                context.DropPosition,
                Quaternion.identity,
                Vector3.zero,
                Vector3.zero,
                true,
                true,
                0f);

            if (loot == null)
            {
                SPTEssentialsPlugin.Log.LogError("Keep Tripwire Kit: the game could not create the recovered kit's loot object.");
                Notify("Tripwire kit recovery failed. Please check the log.");
                return;
            }

            SPTEssentialsPlugin.Log.LogInfo("Keep Tripwire Kit: dropped one installation kit beside the disarmed trap.");
            Notify("Tripwire installation kit dropped beside the disarmed trap.");
        }
        catch (Exception exception)
        {
            SPTEssentialsPlugin.Log.LogError($"Keep Tripwire Kit: recovery interrupted: {exception}");
        }
        finally
        {
            GameWorld.OnDispose -= context.OnRaidEnded;
        }
    }

    private static void Notify(string message)
    {
        NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Default, ENotificationIconType.Default, null);
    }

    private sealed class DisarmContext
    {
        internal GameWorld World;
        internal Player Player;
        internal TripwireSynchronizableObject Tripwire;
        internal Vector3 DropPosition;
        internal bool RaidEnded;

        internal void OnRaidEnded()
        {
            RaidEnded = true;
        }
    }

    [HarmonyPatch]
    private static class DisarmCompletedPatch
    {
        private static MethodBase TargetMethod()
        {
            // SPT 4.1.5's completion callback retains the tool used for this action.
            return AccessTools.Method(typeof(InteractionContextHelper.CG_GetAvailableActions7), "method_1", new[] { typeof(bool) });
        }

        [HarmonyPrefix]
        private static void Prefix(InteractionContextHelper.CG_GetAvailableActions7 __instance, bool __0, out DisarmContext __state)
        {
            __state = null;
            if (!SPTEssentialsPlugin.Settings.EnableKeepTripwireKit.Value
                || !__0
                || !__instance.hasMultiTool
                || __instance.tripwire == null
                || __instance.tripwire.TripwireState != ETripwireState.Wait
                || !Singleton<GameWorld>.Instantiated)
            {
                return;
            }

            var world = Singleton<GameWorld>.Instance;
            var player = __instance.owner?.Player;
            if (!(player is LocalPlayer)
                || world == null
                || !ReferenceEquals(world.MainPlayer, player)
                || !player.IsYourPlayer
                || !player.HealthController.IsAlive
                || player.InventoryController == null)
            {
                return;
            }

            __state = new DisarmContext
            {
                World = world,
                Player = player,
                Tripwire = __instance.tripwire,
                DropPosition = __instance.tripwire.FromPosition + Vector3.up * 0.15f
            };
        }

        [HarmonyPostfix]
        private static void Postfix(DisarmContext __state)
        {
            if (__state == null
                || __state.Tripwire == null
                || __state.Tripwire.TripwireState != ETripwireState.Inert)
            {
                return;
            }

            _ = ReturnKitAsync(__state);
        }
    }
}
