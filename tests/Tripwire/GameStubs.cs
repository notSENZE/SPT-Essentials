// These test doubles exercise module decisions, not EFT's inventory or Unity runtime.
namespace Comfort.Common
{
    public static class Singleton<T>
    {
        public static T Instance;
        public static bool Instantiated => Instance != null;
    }
}

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 zero => new Vector3();
        public static Vector3 up => new Vector3 { y = 1 };
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3 { x = a.x + b.x, y = a.y + b.y, z = a.z + b.z };
        public static Vector3 operator *(Vector3 a, float b) => new Vector3 { x = a.x * b, y = a.y * b, z = a.z * b };
    }
    public struct Quaternion { public static Quaternion identity => new Quaternion(); }
}

namespace EFT
{
    using EFT.InventoryLogic;
    using EFT.SynchronizableObjects;
    using UnityEngine;

    public class Player
    {
        public InventoryController InventoryController = new InventoryController();
        public HealthController HealthController = new HealthController();
        public bool IsYourPlayer = true;
    }
    public class LocalPlayer : Player { }
    public class HealthController { public bool IsAlive = true; }
    public class GamePlayerOwner { public Player Player; }
    public class GameWorld
    {
        public static event Action OnDispose;
        public static int Subscribers => OnDispose?.GetInvocationList().Length ?? 0;
        public Player MainPlayer;
        public readonly List<Item> Drops = new List<Item>();
        public bool FailDrop;
        public Vector3 LastDropPosition;
        public void Dispose() => OnDispose?.Invoke();
        public object ThrowItem(Item item, Player player, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity, bool syncable, bool validate, float delay)
        {
            LastDropPosition = position;
            if (FailDrop) return null;
            Drops.Add(item);
            return new object();
        }
    }
    public class ItemFactory
    {
        public readonly List<Item> Created = new List<Item>();
        public Item CreateItem(string id, string template, object data)
        {
            var item = new Item { Id = id, TemplateId = template, SpawnedInSession = true };
            Created.Add(item);
            return item;
        }
    }
    public static class InteractionContextHelper
    {
        public class CG_GetAvailableActions7
        {
            public GamePlayerOwner owner;
            public TripwireSynchronizableObject tripwire;
            public bool hasMultiTool;
            public void method_1(bool success) { }
        }
    }
}

namespace EFT.SynchronizableObjects
{
    public enum ETripwireState { None, Wait, Active, Exploding, Exploded, Inert }
    public class TripwireSynchronizableObject
    {
        public ETripwireState TripwireState = ETripwireState.Wait;
        public UnityEngine.Vector3 FromPosition;
    }
}

namespace EFT.InventoryLogic
{
    public class Item { public string Id, TemplateId; public bool SpawnedInSession; }
    public class InventoryController
    {
        public Guid NextId => Guid.NewGuid();
        public Inventory Inventory = new Inventory();
        public readonly List<Item> Stored = new List<Item>();
        public readonly List<CommandStatus> Events = new List<CommandStatus>();
        public bool FailEvent;
    }
    public class Inventory { public InventoryEquipment Equipment = new InventoryEquipment(); }
    public class InventoryEquipment
    {
        public readonly List<Grid> Grids = new List<Grid>();
        public IEnumerable<Grid> GetPrioritizedGridsForLoot(Item item) => Grids;
    }
    public class Grid
    {
        public bool HasSpace = true;
        public bool CountLimited;
        public bool RejectItem;
        public int Attempts;
        public GridItemAddress FindLocationForItem(Item item) => HasSpace ? new GridItemAddress { Grid = this } : null;
    }
    public class GridItemAddress { public Grid Grid; }
    public enum CommandStatus { Begin, Succeed }
    public class AddResult
    {
        public void RaiseEvents(InventoryController controller, CommandStatus status)
        {
            if (controller.FailEvent) throw new InvalidOperationException("Simulated event failure after adding item.");
            controller.Events.Add(status);
        }
    }
    public class OperationResult { public bool Failed; public AddResult Value; public object Error; }
    public static class ItemManipulator
    {
        public class CountLimitError { }

        public static OperationResult Add(Item item, GridItemAddress address, InventoryController controller, bool simulate)
        {
            if (simulate) throw new InvalidOperationException("Test expects a real addition.");
            address.Grid.Attempts++;
            if (address.Grid.CountLimited) return new OperationResult { Failed = true, Error = new CountLimitError() };
            if (address.Grid.RejectItem) return new OperationResult { Failed = true };
            controller.Stored.Add(item);
            return new OperationResult { Value = new AddResult() };
        }

        public static OperationResult AddWithoutRestrictions(Item item, GridItemAddress address, InventoryController controller)
        {
            address.Grid.Attempts++;
            if (address.Grid.RejectItem) return new OperationResult { Failed = true };
            controller.Stored.Add(item);
            return new OperationResult { Value = new AddResult() };
        }
    }
}

namespace EFT.InventoryLogic.Operations
{
    public static class ChangeItemsOperation
    {
        public static Task Loading = Task.CompletedTask;
        public static Task LoadBundles(EFT.InventoryLogic.Item item) => Loading;
    }
}

namespace EFT.Communications
{
    public enum ENotificationDurationType { Default }
    public enum ENotificationIconType { Default }
    public static class NotificationManager
    {
        public static readonly List<string> Messages = new List<string>();
        public static void DisplayMessageNotification(string message, ENotificationDurationType duration, ENotificationIconType icon, object color) => Messages.Add(message);
    }
}

namespace HarmonyLib
{
    public class Harmony { public Harmony(string id) { } }
    public class HarmonyPatch : Attribute { }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public static class AccessTools
    {
        public static System.Reflection.MethodInfo Method(Type type, string name, Type[] parameters) => type.GetMethod(name, parameters);
    }
}

namespace SPTEssentials.Client
{
    internal abstract class ClientModule
    {
        protected abstract string Name { get; }
        protected abstract void Enable();
        protected static void Patch(HarmonyLib.Harmony harmony, Type type) { }
    }
    internal static class SPTEssentialsPlugin
    {
        internal const string PluginGuid = "test";
        internal static TestSettings Settings = new TestSettings();
        internal static TestLog Log = new TestLog();
    }
    internal class TestSettings { internal TestSwitch EnableKeepTripwireKit = new TestSwitch(); }
    internal class TestSwitch { internal bool Value = true; }
    internal class TestLog
    {
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Infos = new List<string>();
        internal void LogError(string message) => Errors.Add(message);
        internal void LogInfo(string message) => Infos.Add(message);
    }
}
