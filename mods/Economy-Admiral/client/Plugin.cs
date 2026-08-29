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

        traderPurchase = BindDouble("Trader Purchase Multiplier", 1.15, 1.0, 2.0, "multiplier applied to supported trader currency purchase costs");
        traderSell = BindDouble("Trader Sell Payout Multiplier", 0.85, 0.5, 1.0, "effective share of normal trader sell payout");
        fleaBase = BindDouble("Flea Base Price Multiplier", 1.65, 1.0, 2.5, "minimum flea base-price pressure multiplier");
        fleaFee = BindDouble("Flea Listing Fee Multiplier", 1.25, 1.0, 2.0, "flea listing-fee multiplier");
        looseLoot = BindDouble("Loose Loot Scale", 0.85, 0.5, 1.0, "native loose-loot multiplier scale");
        staticLoot = BindDouble("Static Loot Scale", 0.85, 0.5, 1.0, "native static/container-loot multiplier scale");
        questItems = BindDouble("Quest Item Reward Cap", 3.0, 0.1, 10.0, "normal quest item-reward budget multiple");
        restartableQuestItems = BindDouble("Restartable Quest Item Reward Cap", 2.0, 0.1, 10.0, "restartable quest item-reward budget multiple");
        questXp = BindDouble("Quest XP Reward Cap", 3.0, 0.1, 10.0, "normal quest XP reward multiple");
        restartableQuestXp = BindDouble("Restartable Quest XP Reward Cap", 2.0, 0.1, 10.0, "restartable quest XP reward multiple");
        questStanding = BindDouble("Quest Standing Reward Cap", 3.0, 0.1, 10.0, "trader-standing reward multiple");
    }

    private ConfigEntry<double> BindDouble(string name, double value, double min, double max, string text) =>
        Config.Bind("03. Advanced - Custom", name, value,
            new ConfigDescription("Custom preset: " + text + "." + RestartText, new AcceptableValueRange<double>(min, max)));

    private void ResolveRequestHandler()
    {
        Type requestHandlerType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            requestHandlerType = assembly.GetType("SPT.Common.Http.RequestHandler", throwOnError: false);
            if (requestHandlerType != null)
                break;
        }

        requestHandlerType ??= Type.GetType("SPT.Common.Http.RequestHandler, SPT.Common", throwOnError: false);
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
        if (getJsonMethod == null)
            return false;

        try
        {
            var snapshot = ParseSnapshot(InvokeGet());
            if (snapshot == null || !snapshot.Ok)
            {
                Logger.LogError("Economy Admiral server settings GET returned no valid snapshot.");
                return false;
            }

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
        if (hydrating || !initialized)
            return;

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

    private static bool Matches(SettingsRequest request, SettingsSnapshot snapshot) =>
        string.Equals(request.Mode, snapshot.Mode, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(request.Preset, snapshot.Preset, StringComparison.OrdinalIgnoreCase) &&
        request.EnablePlayableEconomyBundle == snapshot.EnablePlayableEconomyBundle &&
        request.EnableQuestEconomyCluster == snapshot.EnableQuestEconomyCluster &&
        request.EnableTraderEconomyCluster == snapshot.EnableTraderEconomyCluster &&
        request.EnableFleaEconomyCluster == snapshot.EnableFleaEconomyCluster &&
        request.EnableLootEconomyCluster == snapshot.EnableLootEconomyCluster &&
        Nearly(request.CustomTraderPurchasePriceMultiplier, snapshot.CustomTraderPurchasePriceMultiplier) &&
        Nearly(request.CustomTraderSellPayoutMultiplier, snapshot.CustomTraderSellPayoutMultiplier) &&
        Nearly(request.CustomFleaBasePriceMultiplier, snapshot.CustomFleaBasePriceMultiplier) &&
        Nearly(request.CustomFleaListingFeeMultiplier, snapshot.CustomFleaListingFeeMultiplier) &&
        Nearly(request.CustomLooseLootScale, snapshot.CustomLooseLootScale) &&
        Nearly(request.CustomStaticLootScale, snapshot.CustomStaticLootScale) &&
        Nearly(request.CustomQuestItemBudgetMultiple, snapshot.CustomQuestItemBudgetMultiple) &&
        Nearly(request.CustomRestartableQuestItemBudgetMultiple, snapshot.CustomRestartableQuestItemBudgetMultiple) &&
        Nearly(request.CustomQuestXpMultiple, snapshot.CustomQuestXpMultiple) &&
        Nearly(request.CustomRestartableQuestXpMultiple, snapshot.CustomRestartableQuestXpMultiple) &&
        Nearly(request.CustomQuestStandingMultiple, snapshot.CustomQuestStandingMultiple);

    private static bool Nearly(double left, double right) => Math.Abs(left - right) < 0.000001;

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
