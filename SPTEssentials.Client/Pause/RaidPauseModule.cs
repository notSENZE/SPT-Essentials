using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Comfort.Common;
using EFT;
using EFT.UI.BattleTimer;
using HarmonyLib;
using UnityEngine;

namespace SPTEssentials.Client.Pause;

internal sealed class RaidPauseModule : ClientModule
{
    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".pause");

    protected override string Name => "Raid Pause";

    protected override void Enable()
    {
        Patch(_harmony, typeof(GameTimerStartPatch));
        Patch(_harmony, typeof(TimerPanelShowPatch));
        Patch(_harmony, typeof(WorldTickPatch));
        Patch(_harmony, typeof(OtherWorldTickPatch));
        Patch(_harmony, typeof(EndByTimerPatch));
        Patch(_harmony, typeof(MainTimerPanelPatch));
    }

    internal void Update()
    {
        if (!SPTEssentialsPlugin.Settings.EnableRaidPause.Value)
        {
            RaidPauseController.Resume();
            return;
        }

        if (RaidPauseController.IsPaused && !RaidPauseController.IsRaidActive())
        {
            RaidPauseController.Resume();
            return;
        }

        if (SPTEssentialsPlugin.Settings.PauseShortcut.Value.IsDown())
        {
            RaidPauseController.Toggle();
        }
    }

    internal void OnGui()
    {
        RaidPauseController.DrawOverlay();
    }

    internal void Shutdown()
    {
        RaidPauseController.Resume();
    }

    [HarmonyPatch(typeof(GameTimer), nameof(GameTimer.Start))]
    private static class GameTimerStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameTimer __instance)
        {
            RaidPauseController.RegisterTimer(__instance);
        }
    }

    [HarmonyPatch(typeof(TimerPanel), nameof(TimerPanel.Show), typeof(DateTime), typeof(StringBuilder))]
    private static class TimerPanelShowPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TimerPanel __instance)
        {
            RaidPauseController.RegisterTimerPanel(__instance);
        }
    }

    [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.DoWorldTick))]
    private static class WorldTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !RaidPauseController.IsPaused;
        }
    }

    [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.DoOtherWorldTick))]
    private static class OtherWorldTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !RaidPauseController.IsPaused;
        }
    }

    [HarmonyPatch(typeof(EndByTimerScenario), nameof(EndByTimerScenario.Update))]
    private static class EndByTimerPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !RaidPauseController.IsPaused;
        }
    }

    [HarmonyPatch(typeof(MainTimerPanel), nameof(MainTimerPanel.UpdateTimer))]
    private static class MainTimerPanelPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MainTimerPanel __instance)
        {
            if (!RaidPauseController.IsPaused)
            {
                return true;
            }

            if (__instance.TimerText != null)
            {
                __instance.TimerText.text = "PAUSED";
            }

            return false;
        }
    }
}

internal static class RaidPauseController
{
    private static readonly HashSet<GameTimer> Timers = new HashSet<GameTimer>();
    private static readonly HashSet<TimerPanel> TimerPanels = new HashSet<TimerPanel>();
    private static readonly FieldInfo TimerPanelDateTimeField =
        AccessTools.Field(typeof(TimerPanel), "_dateTime");

    private static DateTime _pausedAt;
    private static float _previousTimeScale = 1f;
    private static GameDateTime _lockedGameDateTime;
    private static GUIStyle _overlayStyle;

    internal static bool IsPaused { get; private set; }

    internal static void RegisterTimer(GameTimer timer)
    {
        if (timer != null)
        {
            Timers.Add(timer);
        }
    }

    internal static void RegisterTimerPanel(TimerPanel timerPanel)
    {
        if (timerPanel != null)
        {
            TimerPanels.Add(timerPanel);
        }
    }

    internal static void Toggle()
    {
        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    internal static bool IsRaidActive()
    {
        try
        {
            return Singleton<GameWorld>.Instantiated
                && Singleton<GameWorld>.Instance?.MainPlayer != null;
        }
        catch
        {
            return false;
        }
    }

    internal static void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        var pausedDuration = DateTime.Now - _pausedAt;
        ShiftTimers(pausedDuration);
        ShiftTimerPanels(pausedDuration);

        try
        {
            _lockedGameDateTime?.Unlock();
        }
        catch (Exception exception)
        {
            SPTEssentialsPlugin.Log.LogWarning($"Raid clock unlock failed: {exception.Message}");
        }

        _lockedGameDateTime = null;
        Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
        IsPaused = false;
    }

    internal static void DrawOverlay()
    {
        if (!IsPaused)
        {
            return;
        }

        if (_overlayStyle == null)
        {
            _overlayStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold
            };
            _overlayStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(0f, 32f, Screen.width, 52f), "PAUSED", _overlayStyle);
    }

    private static void Pause()
    {
        if (!IsRaidActive())
        {
            return;
        }

        _pausedAt = DateTime.Now;
        _previousTimeScale = Time.timeScale;

        try
        {
            _lockedGameDateTime = Singleton<GameWorld>.Instance.GameDateTime;
            _lockedGameDateTime?.Lock();
        }
        catch (Exception exception)
        {
            SPTEssentialsPlugin.Log.LogWarning($"Raid clock lock failed: {exception.Message}");
        }

        IsPaused = true;
        Time.timeScale = 0f;
    }

    private static void ShiftTimers(TimeSpan duration)
    {
        Timers.RemoveWhere(timer => timer == null);
        foreach (var timer in Timers)
        {
            if (timer._startDateTime.HasValue)
            {
                timer._startDateTime = timer._startDateTime.Value.Add(duration);
            }
            if (timer._escapeDateTime.HasValue)
            {
                timer._escapeDateTime = timer._escapeDateTime.Value.Add(duration);
            }
            if (timer.nullable_2.HasValue)
            {
                timer.nullable_2 = timer.nullable_2.Value.Add(duration);
            }
        }
    }

    private static void ShiftTimerPanels(TimeSpan duration)
    {
        TimerPanels.RemoveWhere(timerPanel => timerPanel == null);
        if (TimerPanelDateTimeField == null)
        {
            return;
        }

        foreach (var timerPanel in TimerPanels)
        {
            try
            {
                var endTime = (DateTime)TimerPanelDateTimeField.GetValue(timerPanel);
                TimerPanelDateTimeField.SetValue(timerPanel, endTime.Add(duration));
            }
            catch (Exception exception)
            {
                SPTEssentialsPlugin.Log.LogWarning($"Raid timer adjustment failed: {exception.Message}");
            }
        }
    }
}
