using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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

    private ConfigEntry<string> mode;
    private ConfigEntry<string> preset;
    private ConfigEntry<bool> bundle;
    private ConfigEntry<bool> questCluster;
    private ConfigEntry<bool> traderCluster;
    private ConfigEntry<bool> fleaCluster;
    private ConfigEntry<bool> lootCluster;
    private ConfigEntry<double> traderPurchase;
    private ConfigEntry<double> traderSell;
    private ConfigEntry<double> fleaBase;
    private ConfigEntry<double> fleaFee;
    private ConfigEntry<double> looseLoot;
    private ConfigEntry<double> staticLoot;
    private ConfigEntry<double> questItems;
    private ConfigEntry<double> restartableQuestItems;
    private ConfigEntry<double> questXp;
    private ConfigEntry<double> restartableQuestXp;
    private ConfigEntry<double> questStanding;

    private bool hydrating;
    private bool initialized;
    private Type requestHandlerType;
    private MethodInfo getJsonMethod;
    private MethodInfo postJsonMethod;

    private void Awake()
    {
        BindUi();
        ResolveRequestHandler();
        HydrateFromServer();
        Subscribe();
        initialized = true;
        Logger.LogInfo("Economy Admiral F12 settings loaded. Changes are persisted to the server and apply after server restart.");
    }

    private void BindUi()
    {
        mode = Config.Bind("01. Basic", "Mode", "Audit",
            new ConfigDescription("Off disables the mod, Audit analyzes without mutation, Enforce applies the selected economy preset." + RestartText,
                new AcceptableValueList<string>("Off", "Audit", "Enforce")));
        preset = Config.Bind("01. Basic", "Preset", "Normal",
            new ConfigDescription("Economy strength. Normal is the intended balanced starting point." + RestartText,
                new AcceptableValueList<string>("Easy", "Normal", "Hard", "Custom")));
        bundle = Config.Bind("01. Basic", "Playable Economy Bundle", true,
            "Master high-level activation path. In Enforce, enabled clusters automatically use the selected preset." + RestartText);

        questCluster = Config.Bind("02. Advanced - Clusters", "Quest Economy", true,
            "Controls quest item stacks, XP, trader standing and repeatable reward pressure." + RestartText);
        traderCluster = Config.Bind("02. Advanced - Clusters", "Trader Economy", true,
            "Controls trader purchase-price and sell-payout pressure." + RestartText);
        fleaCluster = Config.Bind("02. Advanced - Clusters", "Flea Market Economy", true,
            "Controls flea purchase/base-price, anti-arbitrage/handbook and listing-fee pressure." + RestartText);
        lootCluster = Config.Bind("02. Advanced - Clusters", "Loot Economy", true,
            "Controls loose and static/container loot pressure." + RestartText);

        traderPurchase = Config.Bind("03. Advanced - Custom", "Trader Purchase Multiplier", 1.15,
            new ConfigDescription("Custom preset: multiplier applied to supported trader currency purchase costs." + RestartText,
                new AcceptableValueRange<double>(1.0, 2.0)));
        traderSell = Config.Bind("03. Advanced - Custom", "Trader Sell Payout Multiplier", 0.85,
            new ConfigDescription("Custom preset: effective share of normal trader sell payout." + RestartText,
                new AcceptableValueRange<double>(0.5, 1.0)));
        fleaBase = Config.Bind("03. Advanced - Custom", "Flea Base Price Multiplier", 1.65,
            new ConfigDescription("Custom preset: minimum flea base-price pressure multiplier." + RestartText,
                new AcceptableValueRange<double>(1.0, 2.5)));
        fleaFee = Config.Bind("03. Advanced - Custom", "Flea Listing Fee Multiplier", 1.25,
            new ConfigDescription("Custom preset: flea listing-fee multiplier." + RestartText,
                new AcceptableValueRange<double>(1.0, 2.0)));
        looseLoot = Config.Bind("03. Advanced - Custom", "Loose Loot Scale", 0.85,
            new ConfigDescription("Custom preset: native loose-loot multiplier scale." + RestartText,
                new AcceptableValueRange<double>(0.5, 1.0)));
        staticLoot = Config.Bind("03. Advanced - Custom", "Static Loot Scale", 0.85,
            new ConfigDescription("Custom preset: native static/container-loot multiplier scale." + RestartText,
                new AcceptableValueRange<double>(0.5, 1.0)));
        questItems = Config.Bind("03. Advanced - Custom", "Quest Item Reward Cap", 3.0,
            new ConfigDescription("Custom preset: normal quest item-reward budget multiple." + RestartText,
                new AcceptableValueRange<double>(0.1, 10.0)));
        restartableQuestItems = Config.Bind("03. Advanced - Custom", "Restartable Quest Item Reward Cap", 2.0,
            new ConfigDescription("Custom preset: restartable quest item-reward budget multiple." + RestartText,
                new AcceptableValueRange<double>(0.1, 10.0)));
        questXp = Config.Bind("03. Advanced - Custom", "Quest XP Reward Cap", 3.0,
            new ConfigDescription("Custom preset: normal quest XP reward multiple." + RestartText,
                new AcceptableValueRange<double>(0.1, 10.0)));
        restartableQuestXp = Config.Bind("03. Advanced - Custom", "Restartable Quest XP Reward Cap", 2.0,
            new ConfigDescription("Custom preset: restartable quest XP reward multiple." + RestartText,
                new AcceptableValueRange<double>(0.1, 10.0)));
        questStanding = Config.Bind("03. Advanced - Custom", "Quest Standing Reward Cap", 3.0,
            new ConfigDescription("Custom preset: trader-standing reward multiple." + RestartText,
                new AcceptableValueRange<double>(0.1, 10.0)));
    }

    private void ResolveRequestHandler()
    {
        requestHandlerType = Type.GetType("SPT.Common.Http.RequestHandler, SPT.Common", throwOnError: false);
        if (requestHandlerType == null)
        {
            Logger.LogError("SPT.Common.Http.RequestHandler was not found; F12 settings cannot reach the SPT server.");
            return;
        }

        getJsonMethod = requestHandlerType.GetMethod("GetJson", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        postJsonMethod = requestHandlerType.GetMethod("PostJson", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
        if (getJsonMethod == null || postJsonMethod == null)
            Logger.LogError("SPT RequestHandler GetJson/PostJson API was not found; Economy Admiral settings sync is disabled.");
    }

    private void HydrateFromServer()
    {
        if (getJsonMethod == null)
            return;

        try
        {
            var json = getJsonMethod.Invoke(null, new object[] { GetRoute }) as string;
            var snapshot = ParseSnapshot(json);
            if (snapshot == null || !snapshot.Ok)
            {
                Logger.LogWarning("Economy Admiral server settings were unavailable; F12 shows local fallback values.");
                return;
            }

            hydrating = true;
            mode.Value = snapshot.Mode;
            preset.Value = snapshot.Preset;
            bundle.Value = snapshot.EnablePlayableEconomyBundle;
            questCluster.Value = snapshot.EnableQuestEconomyCluster;
            traderCluster.Value = snapshot.EnableTraderEconomyCluster;
            fleaCluster.Value = snapshot.EnableFleaEconomyCluster;
            lootCluster.Value = snapshot.EnableLootEconomyCluster;
            traderPurchase.Value = snapshot.CustomTraderPurchasePriceMultiplier;
            traderSell.Value = snapshot.CustomTraderSellPayoutMultiplier;
            fleaBase.Value = snapshot.CustomFleaBasePriceMultiplier;
            fleaFee.Value = snapshot.CustomFleaListingFeeMultiplier;
            looseLoot.Value = snapshot.CustomLooseLootScale;
            staticLoot.Value = snapshot.CustomStaticLootScale;
            questItems.Value = snapshot.CustomQuestItemBudgetMultiple;
            restartableQuestItems.Value = snapshot.CustomRestartableQuestItemBudgetMultiple;
            questXp.Value = snapshot.CustomQuestXpMultiple;
            restartableQuestXp.Value = snapshot.CustomRestartableQuestXpMultiple;
            questStanding.Value = snapshot.CustomQuestStandingMultiple;
        }
        catch (Exception exception)
        {
            Logger.LogError($"Failed to load Economy Admiral server settings: {Unwrap(exception).Message}");
        }
        finally
        {
            hydrating = false;
        }
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
        traderPurchase.SettingChanged += OnSettingChanged;
        traderSell.SettingChanged += OnSettingChanged;
        fleaBase.SettingChanged += OnSettingChanged;
        fleaFee.SettingChanged += OnSettingChanged;
        looseLoot.SettingChanged += OnSettingChanged;
        staticLoot.SettingChanged += OnSettingChanged;
        questItems.SettingChanged += OnSettingChanged;
        restartableQuestItems.SettingChanged += OnSettingChanged;
        questXp.SettingChanged += OnSettingChanged;
        restartableQuestXp.SettingChanged += OnSettingChanged;
        questStanding.SettingChanged += OnSettingChanged;
    }

    private void OnSettingChanged(object sender, EventArgs args)
    {
        if (hydrating || !initialized || postJsonMethod == null)
            return;

        try
        {
            var request = BuildRequest();
            var json = JsonUtility.ToJson(request);
            var responseJson = postJsonMethod.Invoke(null, new object[] { SaveRoute, json }) as string;
            var response = ParseSnapshot(responseJson);
            if (response == null || !response.Ok)
            {
                Logger.LogError("Economy Admiral server rejected the F12 settings update; reloading persisted values.");
                HydrateFromServer();
                return;
            }

            Logger.LogMessage("Economy Admiral settings saved. Restart the SPT server to apply them.");
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
        CustomTraderPurchasePriceMultiplier = traderPurchase.Value,
        CustomTraderSellPayoutMultiplier = traderSell.Value,
        CustomFleaBasePriceMultiplier = fleaBase.Value,
        CustomFleaListingFeeMultiplier = fleaFee.Value,
        CustomLooseLootScale = looseLoot.Value,
        CustomStaticLootScale = staticLoot.Value,
        CustomQuestItemBudgetMultiple = questItems.Value,
        CustomRestartableQuestItemBudgetMultiple = restartableQuestItems.Value,
        CustomQuestXpMultiple = questXp.Value,
        CustomRestartableQuestXpMultiple = restartableQuestXp.Value,
        CustomQuestStandingMultiple = questStanding.Value,
    };

    private static SettingsSnapshot ParseSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonUtility.FromJson<SettingsSnapshot>(json);
    }

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : exception;

    [Serializable]
    private sealed class SettingsRequest
    {
        public string Mode;
        public string Preset;
        public bool EnablePlayableEconomyBundle;
        public bool EnableQuestEconomyCluster;
        public bool EnableTraderEconomyCluster;
        public bool EnableFleaEconomyCluster;
        public bool EnableLootEconomyCluster;
        public double CustomTraderPurchasePriceMultiplier;
        public double CustomTraderSellPayoutMultiplier;
        public double CustomFleaBasePriceMultiplier;
        public double CustomFleaListingFeeMultiplier;
        public double CustomLooseLootScale;
        public double CustomStaticLootScale;
        public double CustomQuestItemBudgetMultiple;
        public double CustomRestartableQuestItemBudgetMultiple;
        public double CustomQuestXpMultiple;
        public double CustomRestartableQuestXpMultiple;
        public double CustomQuestStandingMultiple;
    }

    [Serializable]
    private sealed class SettingsSnapshot
    {
        public bool Ok;
        public bool RestartRequired;
        public string Mode;
        public string Preset;
        public bool EnablePlayableEconomyBundle;
        public bool EnableQuestEconomyCluster;
        public bool EnableTraderEconomyCluster;
        public bool EnableFleaEconomyCluster;
        public bool EnableLootEconomyCluster;
        public double CustomTraderPurchasePriceMultiplier;
        public double CustomTraderSellPayoutMultiplier;
        public double CustomFleaBasePriceMultiplier;
        public double CustomFleaListingFeeMultiplier;
        public double CustomLooseLootScale;
        public double CustomStaticLootScale;
        public double CustomQuestItemBudgetMultiple;
        public double CustomRestartableQuestItemBudgetMultiple;
        public double CustomQuestXpMultiple;
        public double CustomRestartableQuestXpMultiple;
        public double CustomQuestStandingMultiple;
        public string Error;
    }
}
