using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SPTPause
{
    internal sealed class RaidPauseContext
    {
        readonly List<ReflectionClockAnchors> anchors;
        readonly List<object> mainTimerPanels;
        readonly Behaviour playerOwner;
        readonly bool playerOwnerWasEnabled;

        RaidPauseContext(List<ReflectionClockAnchors> anchors, List<object> mainTimerPanels, Behaviour playerOwner)
        {
            this.anchors = anchors;
            this.mainTimerPanels = mainTimerPanels;
            this.playerOwner = playerOwner;
            playerOwnerWasEnabled = playerOwner != null && playerOwner.enabled;
        }

        internal static bool TryCreate(out RaidPauseContext context, out string reason)
        {
            context = null;
            reason = string.Empty;

            Type abstractGameType = ReflectionTools.FindType("EFT.AbstractGame");
            object game = ReflectionTools.FindObject(abstractGameType);
            if (game == null || !ReflectionTools.GetBool(game, "InRaid"))
            {
                reason = "Pause is available only inside an active raid.";
                return false;
            }

            object gameType = ReflectionTools.GetMember(game, "GameType");
            if (gameType == null || !string.Equals(gameType.ToString(), "Offline", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Pause is restricted to offline SPT raids and is disabled in hideout/network sessions.";
                return false;
            }

            Type gameWorldType = ReflectionTools.FindType("EFT.GameWorld");
            object gameWorld = ReflectionTools.FindObject(gameWorldType);
            object mainPlayer = ReflectionTools.GetMember(gameWorld, "MainPlayer");
            if (gameWorld == null || mainPlayer == null)
            {
                reason = "The raid world is not ready yet.";
                return false;
            }

            Behaviour playerOwner = null;
            Type playerOwnerType = ReflectionTools.FindType("EFT.GamePlayerOwner");
            Component mainPlayerComponent = mainPlayer as Component;
            if (playerOwnerType != null && mainPlayerComponent != null)
                playerOwner = mainPlayerComponent.GetComponent(playerOwnerType) as Behaviour;
            if (playerOwner == null)
            {
                reason = "The local player input owner could not be resolved safely; pause was not applied.";
                return false;
            }

            object gameTimer = ReflectionTools.GetMember(game, "GameTimer");
            ReflectionClockAnchors timerAnchors = ReflectionClockAnchors.CaptureNamedProperties(gameTimer, "StartDateTime", "EscapeDateTime");
            if (timerAnchors.Count == 0)
            {
                reason = "The SPT 4.1 raid timer shape could not be resolved safely; pause was not applied.";
                return false;
            }

            List<ReflectionClockAnchors> anchors = new List<ReflectionClockAnchors> { timerAnchors };
            object gameDateTime = ReflectionTools.GetMember(gameWorld, "GameDateTime");
            ReflectionClockAnchors timeOfDayAnchor = ReflectionClockAnchors.CaptureFloatField(gameDateTime, "_realtimeSinceStartup");
            if (timeOfDayAnchor.Count > 0) anchors.Add(timeOfDayAnchor);

            List<object> mainTimerPanels = new List<object>();
            Type timerPanelType = ReflectionTools.FindType("EFT.UI.BattleTimer.TimerPanel");
            Type mainTimerPanelType = ReflectionTools.FindType("EFT.UI.BattleTimer.MainTimerPanel");
            if (timerPanelType != null)
            {
                UnityEngine.Object[] panels = Resources.FindObjectsOfTypeAll(timerPanelType);
                for (int i = 0; i < panels.Length; i++)
                {
                    object panel = panels[i];
                    ReflectionClockAnchors panelAnchors = ReflectionClockAnchors.CaptureDateTimeFields(panel, timerPanelType);
                    if (panelAnchors.Count > 0) anchors.Add(panelAnchors);
                    if (mainTimerPanelType != null && mainTimerPanelType.IsInstanceOfType(panel)) mainTimerPanels.Add(panel);
                }
            }

            context = new RaidPauseContext(anchors, mainTimerPanels, playerOwner);
            return true;
        }

        internal void SetPlayerInputPaused(bool paused)
        {
            if (playerOwner == null) return;
            playerOwner.enabled = paused ? false : playerOwnerWasEnabled;
        }

        internal void ShiftClocks(TimeSpan duration)
        {
            for (int i = 0; i < anchors.Count; i++) anchors[i].Shift(duration);
        }

        internal void DisplayMainTimer()
        {
            InvokePanels("DisplayTimer");
        }

        internal void HideMainTimer()
        {
            InvokePanels("HideTimer");
        }

        void InvokePanels(string methodName)
        {
            for (int i = 0; i < mainTimerPanels.Count; i++)
            {
                object panel = mainTimerPanels[i];
                try
                {
                    MethodInfo method = panel.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null) method.Invoke(panel, null);
                }
                catch
                {
                    // UI may be disposed during a scene transition; global time restoration must continue.
                }
            }
        }
    }
}
