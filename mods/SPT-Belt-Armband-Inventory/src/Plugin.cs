using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace SPTBeltArmbandInventory
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.admiralam.spt.belt-armband-inventory";
        public const string PluginName = "SPT Belt Armband Inventory";
        public const string PluginVersion = "0.1.0";

        ConfigEntry<bool> modEnabled;
        ConfigEntry<BeltSlotPosition> position;
        DynamicBeltPatches patches;
        PanelRefreshPatches refreshPatches;
        LootPriorityPatches lootPatches;
        UnloadPriorityPatches unloadPatches;

        void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true, "Enable Belt/Armband Inventory. Restart required.");
            position = Config.Bind("Layout", "Belt position", BeltSlotPosition.BelowPockets, "Place the belt row above or below Pockets. Restart required.");

            if (!modEnabled.Value)
            {
                Logger.LogInfo("SPT Belt Armband Inventory is disabled in configuration.");
                return;
            }

            if (LegacyBeltSlotDetected())
            {
                Logger.LogWarning("Trenchfoot-BeltSlot is already loaded. Remove/disable that DLL before enabling SPT Belt Armband Inventory; no duplicate patch was installed.");
                return;
            }

            patches = new DynamicBeltPatches(Logger.LogInfo, Logger.LogWarning);
            if (!patches.TryInstall(position.Value))
            {
                patches.Dispose();
                patches = null;
                return;
            }

            refreshPatches = new PanelRefreshPatches(Logger.LogInfo, Logger.LogWarning);
            if (!refreshPatches.TryInstall())
            {
                refreshPatches.Dispose();
                refreshPatches = null;
                Logger.LogWarning("Belt UI remains active, but equipping/removing a belt while a container panel is already open may require reopening that screen.");
            }

            lootPatches = new LootPriorityPatches(Logger.LogInfo, Logger.LogWarning);
            if (!lootPatches.TryInstall())
            {
                lootPatches.Dispose();
                lootPatches = null;
                Logger.LogWarning("Belt UI remains active, but automatic loot placement will use vanilla container priorities.");
            }

            unloadPatches = new UnloadPriorityPatches(Logger.LogInfo, Logger.LogWarning);
            if (!unloadPatches.TryInstall())
            {
                unloadPatches.Dispose();
                unloadPatches = null;
                Logger.LogWarning("Belt UI remains active, but unload placement will use vanilla grid priorities.");
            }
        }

        void Update()
        {
            if (refreshPatches != null) PanelRefreshRuntime.Flush();
        }

        bool LegacyBeltSlotDetected()
        {
            try
            {
                Type chainloader = Type.GetType("BepInEx.Bootstrap.Chainloader, BepInEx", false);
                PropertyInfo pluginInfos = chainloader == null ? null : chainloader.GetProperty("PluginInfos", BindingFlags.Static | BindingFlags.Public);
                IDictionary dictionary = pluginInfos == null ? null : pluginInfos.GetValue(null, null) as IDictionary;
                return dictionary != null && (dictionary.Contains("com.trenchfoot.beltslot") || dictionary.Contains("BeltSlot"));
            }
            catch { return false; }
        }

        void OnDestroy()
        {
            if (unloadPatches != null) unloadPatches.Dispose();
            unloadPatches = null;
            if (lootPatches != null) lootPatches.Dispose();
            lootPatches = null;
            if (refreshPatches != null) refreshPatches.Dispose();
            refreshPatches = null;
            if (patches != null) patches.Dispose();
            patches = null;
        }
    }
}
