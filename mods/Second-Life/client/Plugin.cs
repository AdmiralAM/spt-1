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
            ConfigEntry<string> eligiblePistolTemplates = Config.Bind(
                "Emergency armament",
                "Eligible pistol template IDs",
                string.Empty,
                "Comma-separated pistol template IDs. Empty allows every owned pistol anywhere inside the stash tree, including nested containers.");
            ConfigEntry<float> recoveryDelaySeconds = Config.Bind(
                "Recovery",
                "Offer delay seconds",
                0f,
                new ConfigDescription("Real-time delay before the paid recovery offer.", new AcceptableValueRange<float>(0f, 60f)));
            ConfigEntry<float> minimumCorpseDistance = Config.Bind(
                "Recovery",
                "Minimum corpse distance",
                100f,
                new ConfigDescription("Minimum recovery-spawn distance from the first corpse.", new AcceptableValueRange<float>(0f, 500f)));
            ConfigEntry<float> minimumPlayerDistance = Config.Bind(
                "Recovery",
                "Minimum live-player distance",
                75f,
                new ConfigDescription("Minimum recovery-spawn distance from every other live player.", new AcceptableValueRange<float>(0f, 500f)));

            bridge = new RuntimeBridge(
                enabled,
                eligiblePistolTemplates,
                recoveryDelaySeconds,
                minimumCorpseDistance,
                minimumPlayerDistance,
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
