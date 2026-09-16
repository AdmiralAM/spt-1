using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;

namespace SPTItemIntelligence
{
    [BepInPlugin("com.admiralam.spt.itemintelligence", "Item Intelligence Admiral", "1.2.1")]
    [BepInDependency("xyz.drakia.Sense", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        ItemHoverOverlaySink hoverSink;
        EftItemViewHoverIntegration hoverIntegration;
        ItemHoverRuntimeController hoverController;
        RequirementRuntimeBootstrap dataBootstrap;
        CancellationTokenSource dataCancellation;
        Task dataTask;
        ItemIntelligenceUiSettings uiSettings;
        AmandsSenseIntegration senseIntegration;
        int moduleKey = -1;
        int dataKey = -1;
        readonly object loadLock = new object();
        readonly RaidRequirementLedger raidLedger = new RaidRequirementLedger();
        RaidInventoryRuntimeScanner raidInventoryScanner;
        Coroutine inventoryRefreshCoroutine;
        float lastRaidInventoryScanAt = float.NegativeInfinity;
        const float RaidInventoryMinimumScanSeconds = .35f;

        internal static ItemPresentationStore PresentationStore { get; private set; }

        void Awake()
        {
            if (ItemIntelligenceRegistry.Shared == null) throw new InvalidOperationException("Item Intelligence Admiral registry initialization failed.");

            PresentationStore = new ItemPresentationStore();
            GameUiText.SetRussian(GameLanguageDetector.DetectRussian());
            uiSettings = new ItemIntelligenceUiSettings(Config);
            ItemHoverTextCache textCache = new ItemHoverTextCache(valueModeProvider: () => uiSettings.ValueMode, modulesProvider: () => uiSettings.Modules);
            hoverSink = new ItemHoverOverlaySink(uiSettings, PresentationStore, textCache, CreateFallback, raidLedger);
            hoverSink.InventoryOpened += RefreshInventorySession;
            raidInventoryScanner = new RaidInventoryRuntimeScanner(message => Logger.LogInfo(message));
            hoverSink.RaidInventoryRefreshRequested += RefreshRaidInventory;
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

            Logger.LogInfo("Item Intelligence Admiral v1.2.1 loaded; UI language=" + (GameUiText.Russian ? "ru" : "en"));
        }

        void ApplyModules()
        {
            ModuleSelection modules = uiSettings.Modules;
            if (uiSettings.SenseIntegration)
            {
                if (senseIntegration == null)
                {
                    senseIntegration = new AmandsSenseIntegration(uiSettings, PresentationStore,
                        message => Logger.LogInfo(message), message => Logger.LogWarning(message),
                        raidLedger, () => hoverSink.Invalidate(), CaptureRaidBaseline);
                    senseIntegration.TryInstall();
                }
            }
            else if (senseIntegration != null)
            {
                senseIntegration.Dispose();
                senseIntegration = null;
            }
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
                ? new ItemHoverText("ITEM INTELLIGENCE ADMIRAL", string.Empty,
                    GameUiText.T("Data unavailable", "Данные недоступны"),
                    string.Empty, 0, 0, 0, 0, 0, dataState: ItemDataState.Unavailable)
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
            if (inventoryRefreshCoroutine != null) return;
            inventoryRefreshCoroutine = StartCoroutine(RefreshInventorySessionAfterBurst());
        }

        IEnumerator RefreshInventorySessionAfterBurst()
        {
            // Item views are initialized in bursts. One delayed refresh keeps the profile snapshot
            // current without beginning a network/projector pass for every cell in a new window.
            yield return new WaitForSecondsRealtime(.15f);
            inventoryRefreshCoroutine = null;
            if (uiSettings == null || !uiSettings.Modules.AnyConsumer) yield break;
            if (dataTask == null || dataTask.IsCompleted) StartDataLoad();
        }

        void CaptureRaidBaseline()
        {
            if (raidInventoryScanner != null) raidInventoryScanner.CaptureBaseline(raidLedger);
        }

        void RefreshRaidInventory()
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastRaidInventoryScanAt < RaidInventoryMinimumScanSeconds) return;
            lastRaidInventoryScanAt = now;
            if (raidInventoryScanner != null && raidInventoryScanner.Refresh(raidLedger) && hoverSink != null) hoverSink.Invalidate();
        }

        void OnGUI()
        {
            if (uiSettings != null && uiSettings.Modules.TrackViews && hoverSink != null) hoverSink.Draw();
        }

        void OnDestroy()
        {
            if (dataCancellation != null) dataCancellation.Cancel();
            if (inventoryRefreshCoroutine != null) StopCoroutine(inventoryRefreshCoroutine);
            if (hoverIntegration != null) hoverIntegration.Dispose();
            if (senseIntegration != null) senseIntegration.Dispose();
            FirRequirementRegistry.Clear();
            ItemRelevanceRegistry.Replace(null);
            dataTask = null;
            dataCancellation = null;
            dataBootstrap = null;
            hoverIntegration = null;
            hoverController = null;
            hoverSink = null;
            uiSettings = null;
            senseIntegration = null;
            raidInventoryScanner = null;
            inventoryRefreshCoroutine = null;
            PresentationStore = null;
        }
    }
}
