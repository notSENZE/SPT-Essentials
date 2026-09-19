using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using EFT.SynchronizableObjects;
using SPTEssentials.Client;
using SPTEssentials.Client.Tripwire;

var module = typeof(KeepTripwireKitModule);
var patch = module.GetNestedType("DisarmCompletedPatch", BindingFlags.NonPublic);
var prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
var returnKit = module.GetMethod("ReturnKitAsync", BindingFlags.Static | BindingFlags.NonPublic);
var passed = 0;

void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    passed++;
    Console.WriteLine("PASS: " + name);
}

InteractionContextHelper.CG_GetAvailableActions7 Setup()
{
    if (GameWorld.Subscribers != 0) throw new Exception("Leaked disposal subscription.");
    SPTEssentialsPlugin.Settings.EnableKeepTripwireKit.Value = true;
    SPTEssentialsPlugin.Log.Errors.Clear();
    SPTEssentialsPlugin.Log.Infos.Clear();
    ChangeItemsOperation.Loading = Task.CompletedTask;
    Singleton<ItemFactory>.Instance = new ItemFactory();
    var player = new LocalPlayer();
    player.InventoryController.Inventory.Equipment.Grids.Add(new Grid());
    Singleton<GameWorld>.Instance = new GameWorld { MainPlayer = player };
    return new InteractionContextHelper.CG_GetAvailableActions7
    {
        owner = new GamePlayerOwner { Player = player },
        tripwire = new TripwireSynchronizableObject(),
        hasMultiTool = true
    };
}

object Before(InteractionContextHelper.CG_GetAvailableActions7 action, bool success = true)
{
    var args = new object[] { action, success, null };
    prefix.Invoke(null, args);
    return args[2];
}

void Complete(InteractionContextHelper.CG_GetAvailableActions7 action, ETripwireState state = ETripwireState.Inert)
{
    var context = Before(action);
    action.tripwire.TripwireState = state;
    postfix.Invoke(null, new[] { context });
}

var action = Setup();
SPTEssentialsPlugin.Settings.EnableKeepTripwireKit.Value = false;
Check(Before(action) == null, "disabled module skips recovery");
action = Setup();
Check(Before(action, false) == null, "cancelled disarm skips recovery");
action.hasMultiTool = false;
Check(Before(action) == null, "disarm without multitool skips recovery");
foreach (var state in new[] { ETripwireState.None, ETripwireState.Active, ETripwireState.Exploding, ETripwireState.Exploded, ETripwireState.Inert })
{
    action = Setup();
    action.tripwire.TripwireState = state;
    Check(Before(action) == null, "ineligible starting state: " + state);
}
action = Setup();
action.owner.Player = new LocalPlayer();
Check(Before(action) == null, "other player cannot receive kit");
action = Setup();
action.owner.Player.HealthController.IsAlive = false;
Check(Before(action) == null, "dead player cannot initiate recovery");
action = Setup();
Singleton<GameWorld>.Instance = null;
Check(Before(action) == null, "missing game world skips recovery");

foreach (var state in new[] { ETripwireState.Wait, ETripwireState.Active, ETripwireState.Exploded })
{
    action = Setup();
    Complete(action, state);
    Check(Singleton<ItemFactory>.Instance.Created.Count == 0, "no kit unless disarm changes state to Inert: " + state);
}

action = Setup();
Complete(action);
var controller = action.owner.Player.InventoryController;
var world = Singleton<GameWorld>.Instance;
Check(controller.Stored.Count == 0 && world.Drops.Count == 1, "recovered kit is placed on the ground, not in inventory");
Check(world.Drops[0].TemplateId == "666b11055a706400b717cfa5" && !world.Drops[0].SpawnedInSession, "correct ground-kit template, not Found in Raid");
Check(world.LastDropPosition.y > 0, "ground kit starts just above the disarmed trap");
Check(controller.Events.Count == 0 && controller.Inventory.Equipment.Grids.All(grid => grid.Attempts == 0), "recovery never invokes inventory operations");
Check(SPTEssentialsPlugin.Log.Infos.Count == 1, "ground recovery is recorded in the log");
Complete(action);
Check(world.Drops.Count == 1 && Singleton<ItemFactory>.Instance.Created.Count == 1, "repeated completion cannot duplicate kit");
action.tripwire.TripwireState = ETripwireState.Wait;
Complete(action);
Check(world.Drops.Count == 2 && world.Drops[0].Id != world.Drops[1].Id, "reused trap object can drop a new kit with a new ID");

action = Setup();
controller = action.owner.Player.InventoryController;
controller.Inventory.Equipment.Grids[0].CountLimited = true;
Complete(action);
Check(controller.Stored.Count == 0 && Singleton<GameWorld>.Instance.Drops.Count == 1, "existing four-kit count limit is not bypassed");
Check(controller.Inventory.Equipment.Grids[0].Attempts == 0, "count-limited inventory grid is never touched");

action = Setup();
controller = action.owner.Player.InventoryController;
controller.FailEvent = true;
Complete(action);
Check(controller.Stored.Count == 0 && Singleton<GameWorld>.Instance.Drops.Count == 1 && SPTEssentialsPlugin.Log.Errors.Count == 0, "inventory event path is no longer used");

action = Setup();
ChangeItemsOperation.Loading = Task.FromException(new InvalidOperationException("Simulated bundle failure."));
Complete(action);
Check(action.owner.Player.InventoryController.Stored.Count == 0 && Singleton<GameWorld>.Instance.Drops.Count == 0 && SPTEssentialsPlugin.Log.Errors.Count == 1, "bundle failure is observed and does not mutate inventory");

action = Setup();
action.owner.Player.InventoryController.Inventory.Equipment.Grids.Clear();
Singleton<GameWorld>.Instance.FailDrop = true;
Complete(action);
Check(SPTEssentialsPlugin.Log.Errors.Count == 1, "failed loot creation is reported");

foreach (var endMode in new[] { "dispose", "replace world", "replace player", "death" })
{
    action = Setup();
    var originalWorld = Singleton<GameWorld>.Instance;
    controller = action.owner.Player.InventoryController;
    var loading = new TaskCompletionSource();
    ChangeItemsOperation.Loading = loading.Task;
    var context = Before(action);
    action.tripwire.TripwireState = ETripwireState.Inert;
    var recovery = (Task)returnKit.Invoke(null, new[] { context });
    Check(!recovery.IsCompleted && GameWorld.Subscribers == 1, "resource load is awaited: " + endMode);
    switch (endMode)
    {
        case "dispose": originalWorld.Dispose(); break;
        case "replace world": Singleton<GameWorld>.Instance = new GameWorld { MainPlayer = action.owner.Player }; break;
        case "replace player": originalWorld.MainPlayer = new LocalPlayer(); break;
        case "death": action.owner.Player.HealthController.IsAlive = false; break;
    }
    loading.SetResult();
    await recovery;
    Check(controller.Stored.Count == 0 && originalWorld.Drops.Count == (endMode == "death" ? 1 : 0), "no stale inventory delivery: " + endMode);
    Check(GameWorld.Subscribers == 0, "disposal subscription cleaned up: " + endMode);
}

Console.WriteLine($"{passed} checks passed. These are isolated module tests, not an EFT runtime test.");
