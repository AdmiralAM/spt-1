using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;

namespace SPTItemIntelligence
{
    [BepInPlugin("com.admiralam.spt.itemintelligence", "SPT Item Intelligence", "0.10.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        ItemHoverOverlaySink hoverSink;
        EftItemViewHoverIntegration hoverIntegration;
        ItemHoverRuntimeController hoverController;
        RequirementRuntimeBootstrap dataBootstrap;
        CancellationTokenSource dataCancellation;
        Task dataTask;
        ItemIntelligenceUiSettings uiSettings;

        internal static ItemPresentationStore PresentationStore { get; private set; }

        void Awake()
        {
            if (ItemIntelligenceRegistry.Shared == null) throw new InvalidOperationException("Item Intelligence registry initialization failed.");

            PresentationStore = new ItemPresentationStore();
            uiSettings = new ItemIntelligenceUiSettings(Config);
            ItemHoverTextCache textCache = new ItemHoverTextCache();
            hoverSink = new ItemHoverOverlaySink(uiSettings, PresentationStore, textCache, CreateFallback);
            uiSettings.Changed += hoverSink.Invalidate;
            hoverController = new ItemHoverRuntimeController(PresentationStore, hoverSink, textCache, CreateFallback);
            dataBootstrap = new RequirementRuntimeBootstrap(
                new ReflectionSptSnapshotTransport(),
                new ReflectionNewtonsoftSnapshotDecoder(),
                new SptRequirementDataProjector(),
                PresentationStore,
                hoverController);
            hoverIntegration = new EftItemViewHoverIntegration(
                hoverController,
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message),
                hoverSink,
                hoverSink);
            hoverIntegration.TryInstall();
            StartDataLoad();

            Logger.LogInfo("SPT Item Intelligence v0.10.1 loaded (cell-attached requirement intelligence)");
        }

        ItemHoverText CreateFallback(string templateId)
        {
            RequirementRuntimeBootstrap bootstrap = dataBootstrap;
            return bootstrap == null
                ? new ItemHoverText("ITEM INTELLIGENCE", string.Empty, "DATA UNAVAILABLE")
                : bootstrap.CreateFallback(templateId);
        }

        void StartDataLoad()
        {
            dataCancellation = new CancellationTokenSource();
            CancellationToken token = dataCancellation.Token;
            dataTask = Task.Run(() =>
            {
                string error;
                if (dataBootstrap.TryRefresh(token, out error))
                    Logger.LogInfo("Item Intelligence live requirement snapshot loaded: " + PresentationStore.Current.Count + " item states.");
                else if (!token.IsCancellationRequested)
                    Logger.LogWarning("Item Intelligence live requirement snapshot unavailable; diagnostic hover remains active: " + error);
                if (hoverSink != null) hoverSink.Invalidate();
            }, token);
        }

        void OnGUI()
        {
            if (hoverSink != null) hoverSink.Draw();
        }

        void OnDestroy()
        {
            if (dataCancellation != null) dataCancellation.Cancel();
            if (hoverIntegration != null) hoverIntegration.Dispose();
            dataTask = null;
            dataCancellation = null;
            dataBootstrap = null;
            hoverIntegration = null;
            hoverController = null;
            hoverSink = null;
            uiSettings = null;
            PresentationStore = null;
        }
    }
}
