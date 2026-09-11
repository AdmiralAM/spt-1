using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;

namespace SPTItemIntelligence
{
    [BepInPlugin("com.admiralam.spt.itemintelligence", "Item Intelligence Admiral", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        ItemHoverOverlaySink hoverSink;
        EftItemViewHoverIntegration hoverIntegration;
        ItemHoverRuntimeController hoverController;
        RequirementRuntimeBootstrap dataBootstrap;
        CancellationTokenSource dataCancellation;
        Task dataTask;
        ItemIntelligenceUiSettings uiSettings;
        int moduleKey = -1;
        int dataKey = -1;
        readonly object loadLock = new object();

        internal static ItemPresentationStore PresentationStore { get; private set; }

        void Awake()
        {
            if (ItemIntelligenceRegistry.Shared == null) throw new InvalidOperationException("Item Intelligence Admiral registry initialization failed.");

            PresentationStore = new ItemPresentationStore();
            uiSettings = new ItemIntelligenceUiSettings(Config);
            ItemHoverTextCache textCache = new ItemHoverTextCache(valueModeProvider: () => uiSettings.ValueMode, modulesProvider: () => uiSettings.Modules);
            hoverSink = new ItemHoverOverlaySink(uiSettings, PresentationStore, textCache, CreateFallback);
            hoverSink.InventoryOpened += RefreshInventorySession;
            uiSettings.Changed += hoverSink.Invalidate;
            hoverController = new ItemHoverRuntimeController(PresentationStore, hoverSink, textCache, CreateFallback);
            dataBootstrap = new RequirementRuntimeBootstrap(
                new ReflectionSptSnapshotTransport(),
                new RelevanceSnapshotDecoder(new ReflectionNewtonsoftSnapshotDecoder(), () => uiSettings.Modules.CraftBarter),
                new SptRequirementDataProjector(),
                PresentationStore,
                hoverController, modules: () => uiSettings.Modules);
            uiSettings.Changed += ApplyModules;
            ApplyModules();

            Logger.LogInfo("Item Intelligence Admiral v1.1 development loaded");
        }

        void ApplyModules()
        {
            ModuleSelection modules = uiSettings.Modules;
            if (moduleKey == modules.Key) return;
            moduleKey = modules.Key;
            if (!modules.TrackViews)
            {
                if (dataCancellation != null) dataCancellation.Cancel();
                dataKey = -1;
                if (hoverIntegration != null) hoverIntegration.Dispose();
                hoverIntegration = null;
                hoverSink.ClearViews();
                PresentationStore.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndex.Empty);
                ItemRelevanceRegistry.Replace(null);
                return;
            }
            if (hoverIntegration == null)
            {
            hoverIntegration = new EftItemViewHoverIntegration(
                hoverController,
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message),
                hoverSink,
                hoverSink);
            hoverIntegration.TryInstall();
            }
            if (dataKey == modules.DataKey) return;
            dataKey = modules.DataKey;
            if (dataCancellation != null) dataCancellation.Cancel();
            PresentationStore.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndex.Empty);
            StartDataLoad();
        }

        ItemHoverText CreateFallback(string templateId)
        {
            RequirementRuntimeBootstrap bootstrap = dataBootstrap;
            return bootstrap == null
                ? new ItemHoverText("ITEM INTELLIGENCE ADMIRAL", string.Empty, "DATA UNAVAILABLE")
                : bootstrap.CreateFallback(templateId);
        }

        void StartDataLoad()
        {
            dataCancellation = new CancellationTokenSource();
            CancellationToken token = dataCancellation.Token;
            dataTask = Task.Run(() =>
            {
                lock (loadLock)
                {
                if (token.IsCancellationRequested) return;
                string error;
                if (dataBootstrap.TryRefresh(token, out error))
                    Logger.LogInfo("Item Intelligence Admiral live requirement snapshot loaded: " + PresentationStore.Current.Count + " item states.");
                else if (!token.IsCancellationRequested)
                    Logger.LogWarning("Item Intelligence Admiral live requirement snapshot unavailable; diagnostic hover remains active: " + error);
                if (hoverSink != null) hoverSink.Invalidate();
                }
            }, token);
        }

        void RefreshInventorySession()
        {
            if (uiSettings == null || !uiSettings.Modules.AnyConsumer) return;
            if (dataTask != null && !dataTask.IsCompleted) return;
            StartDataLoad();
        }

        void OnGUI()
        {
            if (uiSettings != null && uiSettings.Modules.TrackViews && hoverSink != null) hoverSink.Draw();
        }

        void OnDestroy()
        {
            if (dataCancellation != null) dataCancellation.Cancel();
            if (hoverIntegration != null) hoverIntegration.Dispose();
            FirRequirementRegistry.Clear();
            ItemRelevanceRegistry.Replace(null);
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
