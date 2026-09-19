# Keep Tripwire Kit checks

Run `dotnet run --project tests/Tripwire/Tripwire.Tests.csproj` with the .NET 10 SDK.

These checks compile the actual module against small game doubles. They cover its eligibility checks, one ground drop per completed disarm, preservation of the normal four-kit inventory limit, failure handling and pending loads across raid teardown. They do not prove that Harmony, Unity physics, item icons or profile persistence work in game. Build the real client against the installed SPT 4.1.5 assemblies as well.

Before release, test in SPT 4.1.5:

- Disarm with a multitool: one kit appears on the ground beside the trap, normal grenade loot unchanged.
- Disarm without a multitool, cancel, detonate or let the trap expire: no kit.
- Enter with four kits, then disarm another trap: the recovered kit stays on the ground and the normal pickup limit remains in effect.
- Place and recover a kit repeatedly: no extra kits and no Found in Raid status.
- Disable the module in F12: vanilla behavior.
- Pick up a recovered kit while below the limit, then extract, restart and transit. Verify it persists and its icon remains visible in the Hideout.

Version evidence: installed EFT 0.16.9.5.40743 / SPT 4.1.5. The local assembly supplies the completed-disarm callback and its captured `hasMultiTool` flag, the `Wait` to `Inert` transition, `ChangeItemsOperation.LoadBundles` and `GameWorld.ThrowItem`. The matching public server source copies the post-raid equipment tree into the profile in [InRaidHelper.SetInventory](https://github.com/SP-Tushonka/server-csharp/blob/7d7add556a6f781e9a531fa3b0cf4cc925986e03/Libraries/SPTushonka.Server.Core/Helpers/InRaid/InRaidHelper.cs), called for PMC raid endings and transits by [LocationLifecycleService](https://github.com/SP-Tushonka/server-csharp/blob/7d7add556a6f781e9a531fa3b0cf4cc925986e03/Libraries/SPTushonka.Server.Core/Services/InRaid/LocationLifecycleService.cs).
