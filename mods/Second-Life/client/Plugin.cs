using BepInEx;
using BepInEx.Configuration;

namespace Admiral.SecondLife.Client
{
    [BepInPlugin("com.admiralam.secondlife", "Second Life Admiral", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        RuntimeBridge bridge;

        void Awake()
        {
            ConfigEntry<bool> enabled = Config.Bind(
                "General",
                "Enabled",
                false,
                "Enable one guarded recovery per supported solo raid. Disabled until the recovery executor passes preflight.");

            bridge = new RuntimeBridge(
                enabled,
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message));

            if (!bridge.TryInstall())
            {
                bridge.Dispose();
                bridge = null;
                return;
            }

            Logger.LogInfo("Second Life Admiral v0.1.0 loaded; recovery is fail-closed and disabled by default.");
        }

        void OnDestroy()
        {
            if (bridge != null) bridge.Dispose();
            bridge = null;
        }
    }
}
