using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SPTPause
{
    [BepInPlugin("com.admiralam.spt.pause", "SPT Pause", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        DynamicPausePatches patches;
        PauseController controller;

        void Awake()
        {
            ConfigEntry<bool> enabled = Config.Bind("General", "Enabled", true, "Enable pause in offline raids.");
            ConfigEntry<KeyboardShortcut> toggle = Config.Bind("Keybinds", "Toggle pause", new KeyboardShortcut(KeyCode.P), "Pause or resume the current offline raid.");
            ConfigEntry<bool> pauseAudio = Config.Bind("Options", "Pause audio", false, "Pause all game audio while the raid is paused.");
            ConfigEntry<bool> showPausedTimer = Config.Bind("Options", "Show PAUSED on timer", true, "Show the raid timer and replace its value with PAUSED.");

            patches = new DynamicPausePatches(message => Logger.LogInfo(message), message => Logger.LogWarning(message));
            if (!patches.TryInstall()) return;

            controller = gameObject.AddComponent<PauseController>();
            controller.Initialize(enabled, toggle, pauseAudio, showPausedTimer, message => Logger.LogInfo(message), message => Logger.LogWarning(message));
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo("SPT Pause v0.1.0 loaded (Phase 1, SPT 4.1.x dynamic timer compatibility).");
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (controller != null) controller.ForceResume();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (controller != null) controller.ForceResume();
            PauseRuntime.IsPaused = false;
            if (patches != null) patches.Dispose();
            patches = null;
            controller = null;
        }
    }
}
