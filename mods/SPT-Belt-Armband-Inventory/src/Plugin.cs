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

        ConfigEntry<bool> enabled;
        ConfigEntry<BeltSlotPosition> position;
        DynamicBeltPatches patches;

        void Awake()
        {
            enabled = Config.Bind("General", "Enabled", true, "Enable Belt/Armband Inventory. Restart required.");
            position = Config.Bind("Layout", "Belt position", BeltSlotPosition.BelowPockets, "Place the belt row above or below Pockets. Restart required.");

            if (!enabled.Value)
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
            }
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
            if (patches != null) patches.Dispose();
            patches = null;
        }
    }
}
