using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace EconomyAdmiralClient;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.admiralam.spt.economyadmiral.client";
    public const string PluginName = "Economy Admiral";
    public const string PluginVersion = "0.1.0";

    private const string GetRoute = "/economy-admiral/settings/get";
    private const string SaveRoute = "/economy-admiral/settings/save";
    private const string RestartText = " Saved to the Economy Admiral server config; applies after the next SPT server restart.";
    private const string ManualText = " Used only when Full Preset Bundle is OFF; cluster OFF always wins.";

    private ConfigEntry<string> mode;
    private ConfigEntry<string> preset;
    private ConfigEntry<bool> bundle;
    private ConfigEntry<bool> questCluster;
    private ConfigEntry<bool> traderCluster;
    private ConfigEntry<bool> fleaCluster;
    private ConfigEntry<bool> lootCluster;
    private ConfigEntry<bool> questItemStacksEnabled;
    private ConfigEntry<bool> questXpEnabled;
    private ConfigEntry<bool> questStandingEnabled;
    private ConfigEntry<bool> restartableQuestEnabled;
    private ConfigEntry<bool> traderPurchaseEnabled;
    private ConfigEntry<bool> traderSellEnabled;
    private ConfigEntry<bool> fleaPriceEnabled;
    private ConfigEntry<bool> fleaFeeEnabled;
    private ConfigEntry<bool> looseLootEnabled;
    private ConfigEntry<bool> staticLootEnabled;
    private ConfigEntry<double> traderPurchase;
    private ConfigEntry<double> traderSell;
    private ConfigEntry<double> fleaBase;
    private ConfigEntry<double> fleaBelowHandbook;
    private ConfigEntry<double> fleaHandbook;
    private ConfigEntry<double> fleaFee;
    private ConfigEntry<double> looseLoot;
    private ConfigEntry<double> staticLoot;
    private ConfigEntry<double> questItems;
    private ConfigEntry<double> restartableQuestItems;
    private ConfigEntry<double> restartableQuestItemCount;
    private ConfigEntry<double> questXp;
    private ConfigEntry<double> restartableQuestXp;
    private ConfigEntry<double> questStanding;
    private ConfigEntry<double> restartableQuestStanding;

    private bool hydrating;
    private bool initialized;
    private MethodInfo getJsonMethod;
    private MethodInfo postJsonMethod;

    private void Awake()
    {
        Config.SaveOnConfigSet = false;
        BindUi();
        ResolveRequestHandler();
        var hydrated = HydrateFromServer();
        Subscribe();
        initialized = true;
        Logger.LogInfo(hydrated
            ? "Economy Admiral F12 settings loaded from the server config."
            : "Economy Admiral F12 settings loaded, but server hydration failed; changes will be rejected until transport is available.");
    }

    private void BindUi()
    {
        mode = Config.Bind("01. Basic", "Mode", "Enforce",
            new ConfigDescription("Off disables Economy Admiral. Audit is diagnostics-only. Enforce applies the selected preset." + RestartText,
                new AcceptableValueList<string>("Off", "Audit", "Enforce")));
        preset = Config.Bind("01. Basic", "Preset", "Normal",
            new ConfigDescription("Economy strength. Normal is the recommended balanced starting point." + RestartText,
                new AcceptableValueList<string>("Easy", "Normal", "Hard", "Custom")));
        bundle = Config.Bind("01. Basic", "Full Preset Bundle", true,
            "Recommended: ON. Every enabled cluster uses the selected preset as one coherent economy profile. Turn OFF only for selective mechanism control." + RestartText);

        questCluster = Config.Bind("02. Advanced - Clusters", "Quest Economy", true,
            "Hard gate for all automatic quest reward pressure. OFF leaves quest rewards untouched by Economy Admiral." + RestartText);
        traderCluster = Config.Bind("02. Advanced - Clusters", "Trader Economy", true,
            "Hard gate for trader purchase-price and sell-payout pressure." + RestartText);
        fleaCluster = Config.Bind("02. Advanced - Clusters", "Flea Market Economy", true,
            "Hard gate for flea price/anti-arbitrage and listing-fee pressure." + RestartText);
        lootCluster = Config.Bind("02. Advanced - Clusters", "Loot Economy", true,
            "Hard gate for loose and static/container loot pressure." + RestartText);

        questItemStacksEnabled = Config.Bind("03. Advanced - Quest Mechanisms", "Item Reward Stack Pressure", false,
            "Automatic bounded item/reward-value normalization, including generated repeatable Rouble/GP reward budgets." + ManualText + RestartText);
        questXpEnabled = Config.Bind("03. Advanced - Quest Mechanisms", "XP Reward Pressure", false,
            "Automatic quest XP reward normalization." + ManualText + RestartText);
        questStandingEnabled = Config.Bind("03. Advanced - Quest Mechanisms", "Trader Standing Reward Pressure", false,
            "Automatic quest trader-standing reward normalization." + ManualText + RestartText);
        restartableQuestEnabled = Config.Bind("03. Advanced - Quest Mechanisms", "Repeatable / Restartable Pressure", false,
            "Allows preset-derived reward pressure on restartable/repeatable quests. OFF excludes them from automatic quest pressure." + ManualText + RestartText);

        traderPurchaseEnabled = Config.Bind("04. Advanced - Trader Mechanisms", "Purchase Price Pressure", false,
            "Supported RUB/USD/EUR trader purchase-price pressure." + ManualText + RestartText);
        traderSellEnabled = Config.Bind("04. Advanced - Trader Mechanisms", "Sell Payout Pressure", false,
            "Reduced trader sell payouts." + ManualText + RestartText);
        fleaPriceEnabled = Config.Bind("05. Advanced - Flea Mechanisms", "Price and Anti-Arbitrage Pressure", false,
            "Flea base-price, handbook-floor and anti-arbitrage pressure." + ManualText + RestartText);
        fleaFeeEnabled = Config.Bind("05. Advanced - Flea Mechanisms", "Listing Fee Pressure", false,
            "Flea listing-fee pressure." + ManualText + RestartText);
        looseLootEnabled = Config.Bind("06. Advanced - Loot Mechanisms", "Loose Loot Pressure", false,
            "Scales native loose-loot multipliers." + ManualText + RestartText);
        staticLootEnabled = Config.Bind("06. Advanced - Loot Mechanisms", "Static / Container Loot Pressure", false,
            "Scales native static/container-loot multipliers." + ManualText + RestartText);

        questItems = BindDouble("07. Custom - Quests", "Quest Item Reward Cap", 1.50, 0.1, 10.0, "normal quest item-reward budget multiple");
        restartableQuestItems = BindDouble("07. Custom - Quests", "Restartable Quest Reward Value Cap", 1.15, 0.1, 10.0, "restartable generated Rouble/GP and item-value budget multiple");
        restartableQuestItemCount = BindDouble("07. Custom - Quests", "Restartable Quest Item Count Cap", 1.15, 0.1, 10.0, "restartable generated item-count potential multiple");
        questXp = BindDouble("07. Custom - Quests", "Quest XP Reward Cap", 1.50, 0.1, 10.0, "normal quest XP reward multiple");
        restartableQuestXp = BindDouble("07. Custom - Quests", "Restartable Quest XP Reward Cap", 1.15, 0.1, 10.0, "restartable quest XP reward multiple");
        questStanding = BindDouble("07. Custom - Quests", "Quest Standing Reward Cap", 1.50, 0.1, 10.0, "normal trader-standing reward multiple");
        restartableQuestStanding = BindDouble("07. Custom - Quests", "Restartable Quest Standing Reward Cap", 1.15, 0.1, 10.0, "restartable trader-standing reward multiple");

        traderPurchase = BindDouble("08. Custom - Traders", "Trader Purchase Multiplier", 1.15, 1.0, 2.0, "multiplier applied to supported trader currency purchase costs");
        traderSell = BindDouble("08. Custom - Traders", "Trader Sell Payout Multiplier", 0.85, 0.5, 1.0, "effective share of normal trader sell payout");
        fleaBase = BindDouble("09. Custom - Flea", "Flea Base Price Multiplier", 1.65, 1.0, 2.5, "minimum flea base-price pressure multiplier");
        fleaBelowHandbook = BindDouble("09. Custom - Flea", "Max Below-Handbook Difference (%)", 45.0, 0.0, 100.0, "maximum allowed flea price difference below handbook price; lower is stricter");
        fleaHandbook = BindDouble("09. Custom - Flea", "Handbook Price Multiplier", 1.10, 1.0, 2.0, "handbook floor multiplier used by flea pressure");
        fleaFee = BindDouble("09. Custom - Flea", "Flea Listing Fee Multiplier", 1.25, 1.0, 2.0, "flea listing-fee multiplier");
        looseLoot = BindDouble("10. Custom - Loot", "Loose Loot Scale", 0.85, 0.5, 1.0, "native loose-loot multiplier scale");
        staticLoot = BindDouble("10. Custom - Loot", "Static Loot Scale", 0.85, 0.5, 1.0, "native static/container-loot multiplier scale");
    }

    private ConfigEntry<double> BindDouble(string section, string name, double value, double min, double max, string text) =>
        Config.Bind(section, name, value,
            new ConfigDescription("Used only by the Custom preset: " + text + "." + RestartText, new AcceptableValueRange<double>(min, max)));

    private void ResolveRequestHandler()
    {
        Type requestHandlerType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            requestHandlerType = assembly.GetType("SPT.Common.Http.RequestHandler", false);
            if (requestHandlerType != null) break;
        }
        requestHandlerType ??= Type.GetType("SPT.Common.Http.RequestHandler, SPT.Common", false);
        if (requestHandlerType == null)
        {
            Logger.LogError("SPT.Common.Http.RequestHandler was not found; Economy Admiral F12 transport is unavailable.");
            return;
        }
        getJsonMethod = requestHandlerType.GetMethod("GetJson", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        postJsonMethod = requestHandlerType.GetMethod("PostJson", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
        if (getJsonMethod == null || postJsonMethod == null)
        {
            Logger.LogError($"SPT RequestHandler API mismatch in {requestHandlerType.Assembly.FullName}; Economy Admiral F12 transport is unavailable.");
            getJsonMethod = null;
            postJsonMethod = null;
            return;
        }
        Logger.LogInfo($"Economy Admiral F12 transport resolved via {requestHandlerType.Assembly.GetName().Name}.");
    }

    private string InvokeGet() => getJsonMethod?.Invoke(null, new object[] { GetRoute }) as string;
    private string InvokePost(string json) => postJsonMethod?.Invoke(null, new object[] { SaveRoute, json }) as string;

    private bool HydrateFromServer()
    {
        if (getJsonMethod == null) return false;
        try
        {
            var snapshot = ParseSnapshot(InvokeGet());
            if (snapshot == null || !snapshot.Ok) return false;
            ApplySnapshot(snapshot);
            return true;
        }
        catch (Exception exception)
        {
            Logger.LogError($"Failed to load Economy Admiral server settings: {Unwrap(exception).Message}");
            return false;
        }
    }

    private void ApplySnapshot(SettingsSnapshot snapshot)
    {
        hydrating = true;
        try
        {
            mode.Value = snapshot.Mode;
            preset.Value = snapshot.Preset;
            bundle.Value = snapshot.EnablePlayableEconomyBundle;
            questCluster.Value = snapshot.EnableQuestEconomyCluster;
            traderCluster.Value = snapshot.EnableTraderEconomyCluster;
            fleaCluster.Value = snapshot.EnableFleaEconomyCluster;
            lootCluster.Value = snapshot.EnableLootEconomyCluster;
            questItemStacksEnabled.Value = snapshot.EnableItemRewardStackNormalization;
            questXpEnabled.Value = snapshot.EnableQuestXpPressure;
            questStandingEnabled.Value = snapshot.EnableQuestStandingPressure;
            restartableQuestEnabled.Value = snapshot.EnableRestartableQuestPressure;
            traderPurchaseEnabled.Value = snapshot.EnableTraderPurchasePressure;
            traderSellEnabled.Value = snapshot.EnableTraderSellPressure;
            fleaPriceEnabled.Value = snapshot.EnableFleaPurchasePressure;
            fleaFeeEnabled.Value = snapshot.EnableFleaListingFeePressure;
            looseLootEnabled.Value = snapshot.EnableLooseLootPressure || snapshot.EnableLootPressure;
            staticLootEnabled.Value = snapshot.EnableStaticLootPressure || snapshot.EnableLootPressure;
            traderPurchase.Value = snapshot.CustomTraderPurchasePriceMultiplier;
            traderSell.Value = snapshot.CustomTraderSellPayoutMultiplier;
            fleaBase.Value = snapshot.CustomFleaBasePriceMultiplier;
            fleaBelowHandbook.Value = snapshot.CustomFleaMaxPriceDifferenceBelowHandbookPercent;
            fleaHandbook.Value = snapshot.CustomFleaHandbookPriceMultiplier;
            fleaFee.Value = snapshot.CustomFleaListingFeeMultiplier;
            looseLoot.Value = snapshot.CustomLooseLootScale;
            staticLoot.Value = snapshot.CustomStaticLootScale;
            questItems.Value = snapshot.CustomQuestItemBudgetMultiple;
            restartableQuestItems.Value = snapshot.CustomRestartableQuestItemBudgetMultiple;
            restartableQuestItemCount.Value = snapshot.CustomRestartableQuestItemCountMultiple;
            questXp.Value = snapshot.CustomQuestXpMultiple;
            restartableQuestXp.Value = snapshot.CustomRestartableQuestXpMultiple;
            questStanding.Value = snapshot.CustomQuestStandingMultiple;
            restartableQuestStanding.Value = snapshot.CustomRestartableQuestStandingMultiple;
        }
        finally { hydrating = false; }
    }

    private void Subscribe()
    {
        mode.SettingChanged += OnSettingChanged;
        preset.SettingChanged += OnSettingChanged;
        bundle.SettingChanged += OnSettingChanged;
        questCluster.SettingChanged += OnSettingChanged;
        traderCluster.SettingChanged += OnSettingChanged;
        fleaCluster.SettingChanged += OnSettingChanged;
        lootCluster.SettingChanged += OnSettingChanged;
        questItemStacksEnabled.SettingChanged += OnSettingChanged;
        questXpEnabled.SettingChanged += OnSettingChanged;
        questStandingEnabled.SettingChanged += OnSettingChanged;
        restartableQuestEnabled.SettingChanged += OnSettingChanged;
        traderPurchaseEnabled.SettingChanged += OnSettingChanged;
        traderSellEnabled.SettingChanged += OnSettingChanged;
        fleaPriceEnabled.SettingChanged += OnSettingChanged;
        fleaFeeEnabled.SettingChanged += OnSettingChanged;
        looseLootEnabled.SettingChanged += OnSettingChanged;
        staticLootEnabled.SettingChanged += OnSettingChanged;
        traderPurchase.SettingChanged += OnSettingChanged;
        traderSell.SettingChanged += OnSettingChanged;
        fleaBase.SettingChanged += OnSettingChanged;
        fleaBelowHandbook.SettingChanged += OnSettingChanged;
        fleaHandbook.SettingChanged += OnSettingChanged;
        fleaFee.SettingChanged += OnSettingChanged;
        looseLoot.SettingChanged += OnSettingChanged;
        staticLoot.SettingChanged += OnSettingChanged;
        questItems.SettingChanged += OnSettingChanged;
        restartableQuestItems.SettingChanged += OnSettingChanged;
        restartableQuestItemCount.SettingChanged += OnSettingChanged;
        questXp.SettingChanged += OnSettingChanged;
        restartableQuestXp.SettingChanged += OnSettingChanged;
        questStanding.SettingChanged += OnSettingChanged;
        restartableQuestStanding.SettingChanged += OnSettingChanged;
    }

    private void OnSettingChanged(object sender, EventArgs args)
    {
        if (hydrating || !initialized) return;
        if (postJsonMethod == null || getJsonMethod == null)
        {
            Logger.LogError("Economy Admiral setting was not saved: SPT server transport is unavailable.");
            HydrateFromServer();
            return;
        }
        try
        {
            var request = BuildRequest();
            var response = ParseSnapshot(InvokePost(JsonUtility.ToJson(request)));
            if (response == null || !response.Ok)
                throw new InvalidOperationException("server SAVE did not return a valid success snapshot");
            var persisted = ParseSnapshot(InvokeGet());
            if (persisted == null || !persisted.Ok || !Matches(request, persisted))
                throw new InvalidOperationException("server round-trip verification did not match the requested settings");
            ApplySnapshot(persisted);
            Logger.LogMessage("Economy Admiral settings saved to server config. Restart the SPT server to apply them.");
        }
        catch (Exception exception)
        {
            Logger.LogError($"Failed to save Economy Admiral settings: {Unwrap(exception).Message}");
            HydrateFromServer();
        }
    }

    private SettingsRequest BuildRequest() => new SettingsRequest
    {
        Mode = mode.Value,
        Preset = preset.Value,
        EnablePlayableEconomyBundle = bundle.Value,
        EnableQuestEconomyCluster = questCluster.Value,
        EnableTraderEconomyCluster = traderCluster.Value,
        EnableFleaEconomyCluster = fleaCluster.Value,
        EnableLootEconomyCluster = lootCluster.Value,
        EnableItemRewardStackNormalization = questItemStacksEnabled.Value,
        EnableQuestXpPressure = questXpEnabled.Value,
        EnableQuestStandingPressure = questStandingEnabled.Value,
        EnableRestartableQuestPressure = restartableQuestEnabled.Value,
        EnableTraderPurchasePressure = traderPurchaseEnabled.Value,
        EnableTraderSellPressure = traderSellEnabled.Value,
        EnableFleaPurchasePressure = fleaPriceEnabled.Value,
        EnableFleaListingFeePressure = fleaFeeEnabled.Value,
        EnableLootPressure = false,
        EnableLooseLootPressure = looseLootEnabled.Value,
        EnableStaticLootPressure = staticLootEnabled.Value,
        CustomTraderPurchasePriceMultiplier = traderPurchase.Value,
        CustomTraderSellPayoutMultiplier = traderSell.Value,
        CustomFleaBasePriceMultiplier = fleaBase.Value,
        CustomFleaMaxPriceDifferenceBelowHandbookPercent = fleaBelowHandbook.Value,
        CustomFleaHandbookPriceMultiplier = fleaHandbook.Value,
        CustomFleaListingFeeMultiplier = fleaFee.Value,
        CustomLooseLootScale = looseLoot.Value,
        CustomStaticLootScale = staticLoot.Value,
        CustomQuestItemBudgetMultiple = questItems.Value,
        CustomRestartableQuestItemBudgetMultiple = restartableQuestItems.Value,
        CustomRestartableQuestItemCountMultiple = restartableQuestItemCount.Value,
        CustomQuestXpMultiple = questXp.Value,
        CustomRestartableQuestXpMultiple = restartableQuestXp.Value,
        CustomQuestStandingMultiple = questStanding.Value,
        CustomRestartableQuestStandingMultiple = restartableQuestStanding.Value,
    };

    private static bool Matches(SettingsRequest request, SettingsSnapshot snapshot) =>
        string.Equals(request.Mode, snapshot.Mode, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(request.Preset, snapshot.Preset, StringComparison.OrdinalIgnoreCase) &&
        request.EnablePlayableEconomyBundle == snapshot.EnablePlayableEconomyBundle &&
        request.EnableQuestEconomyCluster == snapshot.EnableQuestEconomyCluster &&
        request.EnableTraderEconomyCluster == snapshot.EnableTraderEconomyCluster &&
        request.EnableFleaEconomyCluster == snapshot.EnableFleaEconomyCluster &&
        request.EnableLootEconomyCluster == snapshot.EnableLootEconomyCluster &&
        request.EnableItemRewardStackNormalization == snapshot.EnableItemRewardStackNormalization &&
        request.EnableQuestXpPressure == snapshot.EnableQuestXpPressure &&
        request.EnableQuestStandingPressure == snapshot.EnableQuestStandingPressure &&
        request.EnableRestartableQuestPressure == snapshot.EnableRestartableQuestPressure &&
        request.EnableTraderPurchasePressure == snapshot.EnableTraderPurchasePressure &&
        request.EnableTraderSellPressure == snapshot.EnableTraderSellPressure &&
        request.EnableFleaPurchasePressure == snapshot.EnableFleaPurchasePressure &&
        request.EnableFleaListingFeePressure == snapshot.EnableFleaListingFeePressure &&
        request.EnableLootPressure == snapshot.EnableLootPressure &&
        request.EnableLooseLootPressure == snapshot.EnableLooseLootPressure &&
        request.EnableStaticLootPressure == snapshot.EnableStaticLootPressure &&
        Nearly(request.CustomTraderPurchasePriceMultiplier, snapshot.CustomTraderPurchasePriceMultiplier) &&
        Nearly(request.CustomTraderSellPayoutMultiplier, snapshot.CustomTraderSellPayoutMultiplier) &&
        Nearly(request.CustomFleaBasePriceMultiplier, snapshot.CustomFleaBasePriceMultiplier) &&
        Nearly(request.CustomFleaMaxPriceDifferenceBelowHandbookPercent, snapshot.CustomFleaMaxPriceDifferenceBelowHandbookPercent) &&
        Nearly(request.CustomFleaHandbookPriceMultiplier, snapshot.CustomFleaHandbookPriceMultiplier) &&
        Nearly(request.CustomFleaListingFeeMultiplier, snapshot.CustomFleaListingFeeMultiplier) &&
        Nearly(request.CustomLooseLootScale, snapshot.CustomLooseLootScale) &&
        Nearly(request.CustomStaticLootScale, snapshot.CustomStaticLootScale) &&
        Nearly(request.CustomQuestItemBudgetMultiple, snapshot.CustomQuestItemBudgetMultiple) &&
        Nearly(request.CustomRestartableQuestItemBudgetMultiple, snapshot.CustomRestartableQuestItemBudgetMultiple) &&
        Nearly(request.CustomRestartableQuestItemCountMultiple, snapshot.CustomRestartableQuestItemCountMultiple) &&
        Nearly(request.CustomQuestXpMultiple, snapshot.CustomQuestXpMultiple) &&
        Nearly(request.CustomRestartableQuestXpMultiple, snapshot.CustomRestartableQuestXpMultiple) &&
        Nearly(request.CustomQuestStandingMultiple, snapshot.CustomQuestStandingMultiple) &&
        Nearly(request.CustomRestartableQuestStandingMultiple, snapshot.CustomRestartableQuestStandingMultiple);

    private static bool Nearly(double left, double right) => Math.Abs(left - right) < 0.000001;
    private static SettingsSnapshot ParseSnapshot(string json) => string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SettingsSnapshot>(json);
    private static Exception Unwrap(Exception exception) => exception is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : exception;

    [Serializable]
    private sealed class SettingsRequest
    {
        public string Mode; public string Preset;
        public bool EnablePlayableEconomyBundle; public bool EnableQuestEconomyCluster; public bool EnableTraderEconomyCluster; public bool EnableFleaEconomyCluster; public bool EnableLootEconomyCluster;
        public bool EnableItemRewardStackNormalization; public bool EnableQuestXpPressure; public bool EnableQuestStandingPressure; public bool EnableRestartableQuestPressure;
        public bool EnableTraderPurchasePressure; public bool EnableTraderSellPressure; public bool EnableFleaPurchasePressure; public bool EnableFleaListingFeePressure;
        public bool EnableLootPressure; public bool EnableLooseLootPressure; public bool EnableStaticLootPressure;
        public double CustomTraderPurchasePriceMultiplier; public double CustomTraderSellPayoutMultiplier;
        public double CustomFleaBasePriceMultiplier; public double CustomFleaMaxPriceDifferenceBelowHandbookPercent; public double CustomFleaHandbookPriceMultiplier; public double CustomFleaListingFeeMultiplier;
        public double CustomLooseLootScale; public double CustomStaticLootScale;
        public double CustomQuestItemBudgetMultiple; public double CustomRestartableQuestItemBudgetMultiple; public double CustomRestartableQuestItemCountMultiple;
        public double CustomQuestXpMultiple; public double CustomRestartableQuestXpMultiple;
        public double CustomQuestStandingMultiple; public double CustomRestartableQuestStandingMultiple;
    }

    [Serializable]
    private sealed class SettingsSnapshot
    {
        public bool Ok; public bool RestartRequired; public string Mode; public string Preset;
        public bool EnablePlayableEconomyBundle; public bool EnableQuestEconomyCluster; public bool EnableTraderEconomyCluster; public bool EnableFleaEconomyCluster; public bool EnableLootEconomyCluster;
        public bool EnableItemRewardStackNormalization; public bool EnableQuestXpPressure; public bool EnableQuestStandingPressure; public bool EnableRestartableQuestPressure;
        public bool EnableTraderPurchasePressure; public bool EnableTraderSellPressure; public bool EnableFleaPurchasePressure; public bool EnableFleaListingFeePressure;
        public bool EnableLootPressure; public bool EnableLooseLootPressure; public bool EnableStaticLootPressure;
        public double CustomTraderPurchasePriceMultiplier; public double CustomTraderSellPayoutMultiplier;
        public double CustomFleaBasePriceMultiplier; public double CustomFleaMaxPriceDifferenceBelowHandbookPercent; public double CustomFleaHandbookPriceMultiplier; public double CustomFleaListingFeeMultiplier;
        public double CustomLooseLootScale; public double CustomStaticLootScale;
        public double CustomQuestItemBudgetMultiple; public double CustomRestartableQuestItemBudgetMultiple; public double CustomRestartableQuestItemCountMultiple;
        public double CustomQuestXpMultiple; public double CustomRestartableQuestXpMultiple;
        public double CustomQuestStandingMultiple; public double CustomRestartableQuestStandingMultiple;
        public string Error;
    }
}
