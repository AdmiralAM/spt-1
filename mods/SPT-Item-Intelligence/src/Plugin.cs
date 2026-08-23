using BepInEx;

namespace SPTItemIntelligence
{
    [BepInPlugin("com.admiralam.spt.itemintelligence", "SPT Item Intelligence", "0.5.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        ItemHoverOverlaySink hoverSink;
        EftItemViewHoverIntegration hoverIntegration;

        internal static ItemPresentationStore PresentationStore { get; private set; }

        void Awake()
        {
            if (ItemIntelligenceRegistry.Shared == null) throw new System.InvalidOperationException("Item Intelligence registry initialization failed.");

            PresentationStore = new ItemPresentationStore();
            hoverSink = new ItemHoverOverlaySink();
            ItemHoverRuntimeController hoverController = new ItemHoverRuntimeController(PresentationStore, hoverSink);
            hoverIntegration = new EftItemViewHoverIntegration(
                hoverController,
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message));
            hoverIntegration.TryInstall();

            Logger.LogInfo("SPT Item Intelligence v0.5.0 loaded (Phase 12 EFT hover integration)");
        }

        void OnGUI()
        {
            if (hoverSink != null) hoverSink.Draw();
        }

        void OnDestroy()
        {
            if (hoverIntegration != null) hoverIntegration.Dispose();
            hoverIntegration = null;
            hoverSink = null;
            PresentationStore = null;
        }
    }
}
