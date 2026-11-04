using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using EFT;
using HarmonyLib;

namespace SPTEssentials.Client.Sprint;

internal sealed class SprintWhilePackingMagazinesModule : ClientModule
{
    private const string ControllerTypeName = "ContinuousLoadAmmo.Controllers.LoadAmmoController";
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".sprintwhilepackingmagazines");
    private static bool _sprintConditionWasApplied;
    private static bool _speedLimitWasApplied;

    protected override string Name => "Sprint While Packing Magazines";

    protected override void Enable()
    {
        var controllerType = AccessTools.TypeByName(ControllerTypeName);
        if (controllerType == null)
        {
            SPTEssentialsPlugin.Log.LogInfo(
                "Sprint While Packing Magazines: Continuous Load Ammo was not found; compatibility remains inactive.");
            return;
        }

        var setPlayerState = AccessTools.Method(controllerType, "SetPlayerStateAsync", new[] { typeof(bool) });
        var stateMachine = setPlayerState?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        var moveNext = stateMachine == null ? null : AccessTools.Method(stateMachine, "MoveNext");
        if (moveNext == null)
        {
            throw new MissingMethodException(
                "Continuous Load Ammo's SetPlayerStateAsync state machine could not be found.");
        }

        _harmony.Patch(
            moveNext,
            transpiler: new HarmonyMethod(
                typeof(SprintWhilePackingMagazinesModule),
                nameof(Transpiler)));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var setPhysicalCondition = AccessTools.Method(
            typeof(MovementContext),
            nameof(MovementContext.SetPhysicalCondition),
            new[] { typeof(EPhysicalCondition), typeof(bool) });
        var changeSpeedLimit = AccessTools.Method(
            typeof(MovementContext),
            nameof(MovementContext.ChangeSpeedLimit),
            new[] { typeof(float), typeof(Player.ESpeedLimit) });
        var removeSpeedLimit = AccessTools.Method(
            typeof(MovementContext),
            nameof(MovementContext.RemoveStateSpeedLimit),
            new[] { typeof(Player.ESpeedLimit) });

        var setPhysicalConditionReplacement = AccessTools.Method(
            typeof(SprintWhilePackingMagazinesModule),
            nameof(SetPackingPhysicalCondition));
        var changeSpeedLimitReplacement = AccessTools.Method(
            typeof(SprintWhilePackingMagazinesModule),
            nameof(SetPackingSpeedLimit));
        var removeSpeedLimitReplacement = AccessTools.Method(
            typeof(SprintWhilePackingMagazinesModule),
            nameof(RemovePackingSpeedLimit));

        var replacementCount = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(setPhysicalCondition))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = setPhysicalConditionReplacement;
                replacementCount++;
            }
            else if (instruction.Calls(changeSpeedLimit))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = changeSpeedLimitReplacement;
                replacementCount++;
            }
            else if (instruction.Calls(removeSpeedLimit))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = removeSpeedLimitReplacement;
                replacementCount++;
            }

            yield return instruction;
        }

        if (replacementCount != 3)
        {
            SPTEssentialsPlugin.Log.LogWarning(
                $"Sprint While Packing Magazines expected three Continuous Load Ammo calls but found {replacementCount}.");
        }
    }

    private static bool SetPackingPhysicalCondition(
        MovementContext movementContext,
        EPhysicalCondition condition,
        bool enabled)
    {
        if (condition != EPhysicalCondition.SprintDisabled)
        {
            return movementContext.SetPhysicalCondition(condition, enabled);
        }

        if (enabled)
        {
            _sprintConditionWasApplied = !SPTEssentialsPlugin.Settings.EnableSprintWhilePackingMagazines.Value;
            return _sprintConditionWasApplied
                && movementContext.SetPhysicalCondition(condition, true);
        }

        if (!_sprintConditionWasApplied)
        {
            return false;
        }

        _sprintConditionWasApplied = false;
        return movementContext.SetPhysicalCondition(condition, false);
    }

    private static void SetPackingSpeedLimit(
        MovementContext movementContext,
        float speed,
        Player.ESpeedLimit limit)
    {
        _speedLimitWasApplied = !SPTEssentialsPlugin.Settings.EnableSprintWhilePackingMagazines.Value;
        if (_speedLimitWasApplied)
        {
            movementContext.ChangeSpeedLimit(speed, limit);
        }
    }

    private static void RemovePackingSpeedLimit(MovementContext movementContext, Player.ESpeedLimit limit)
    {
        if (_speedLimitWasApplied)
        {
            movementContext.RemoveStateSpeedLimit(limit);
        }

        _speedLimitWasApplied = false;
    }
}
