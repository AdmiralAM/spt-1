using System;
using System.Collections;
using System.Diagnostics;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTPause
{
    internal sealed class PauseController : MonoBehaviour
    {
        PauseStateMachine state;
        RaidPauseContext context;
        ConfigEntry<bool> enabledSetting;
        ConfigEntry<KeyboardShortcut> toggleSetting;
        ConfigEntry<bool> pauseAudioSetting;
        ConfigEntry<bool> showPausedTimerSetting;
        Action<string> logInfo;
        Action<string> logWarning;
        float previousTimeScale;
        bool previousAudioPause;

        internal void Initialize(
            ConfigEntry<bool> enabledSetting,
            ConfigEntry<KeyboardShortcut> toggleSetting,
            ConfigEntry<bool> pauseAudioSetting,
            ConfigEntry<bool> showPausedTimerSetting,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.enabledSetting = enabledSetting;
            this.toggleSetting = toggleSetting;
            this.pauseAudioSetting = pauseAudioSetting;
            this.showPausedTimerSetting = showPausedTimerSetting;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            state = new PauseStateMachine(Stopwatch.GetTimestamp, Stopwatch.Frequency);
        }

        internal bool IsPaused { get { return state != null && state.IsPaused; } }

        void Update()
        {
            if (state == null || enabledSetting == null || !enabledSetting.Value || !ShortcutPressed(toggleSetting.Value)) return;
            if (state.IsPaused) Resume();
            else Pause();
        }

        internal void ForceResume()
        {
            if (state != null && state.IsPaused) Resume();
        }

        void Pause()
        {
            RaidPauseContext candidate;
            string reason;
            if (!RaidPauseContext.TryCreate(out candidate, out reason))
            {
                if (logWarning != null) logWarning(reason);
                return;
            }

            bool showTimer = showPausedTimerSetting != null && showPausedTimerSetting.Value;
            bool pauseAudio = pauseAudioSetting != null && pauseAudioSetting.Value;
            state.TryPause(() =>
            {
                context = candidate;
                previousTimeScale = Time.timeScale;
                previousAudioPause = AudioListener.pause;
                PauseRuntime.ShowPausedText = showTimer;
                PauseRuntime.IsPaused = true;
                if (pauseAudio) AudioListener.pause = true;
                Time.timeScale = 0f;
                if (showTimer) context.DisplayMainTimer();
            });
            if (logInfo != null) logInfo("Raid paused.");
        }

        void Resume()
        {
            state.TryResume(duration =>
            {
                PauseRuntime.IsPaused = false;
                if (context != null) context.ShiftClocks(duration);
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousAudioPause;
                if (context != null && showPausedTimerSetting != null && showPausedTimerSetting.Value)
                {
                    context.DisplayMainTimer();
                    StartCoroutine(HideTimerAfterDelay(context));
                }
                context = null;
                if (logInfo != null) logInfo("Raid resumed after " + duration.TotalSeconds.ToString("0.0") + " paused seconds.");
            });
        }

        static bool ShortcutPressed(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !Input.GetKeyDown(shortcut.MainKey)) return false;
            foreach (KeyCode modifier in shortcut.Modifiers) if (!Input.GetKey(modifier)) return false;
            return true;
        }

        static IEnumerator HideTimerAfterDelay(RaidPauseContext capturedContext)
        {
            yield return new WaitForSeconds(4f);
            if (capturedContext != null) capturedContext.HideMainTimer();
        }

        void OnDestroy()
        {
            ForceResume();
        }
    }
}
