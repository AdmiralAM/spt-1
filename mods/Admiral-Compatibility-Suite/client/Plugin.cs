using System;
using BepInEx;
using BepInEx.Bootstrap;

namespace AdmiralCompatibilitySuite;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(BeltPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(UseItemsAnywhereAdapter.UpstreamPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(UiFixesBeltAdapter.UpstreamPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.admiralam.compatibility-suite";
    public const string PluginName = "Admiral Compatibility Suite";
    public const string PluginVersion = "0.1.0";
    internal const string BeltPluginGuid = "com.admiralam.spt.belt-armband-inventory";

    private UseItemsAnywhereAdapter useItemsAnywhere;
    private UiFixesBeltAdapter uiFixesBelt;

    private void Awake()
    {
        if (!Chainloader.PluginInfos.ContainsKey(BeltPluginGuid))
        {
            return;
        }

        if (Chainloader.PluginInfos.ContainsKey(UseItemsAnywhereAdapter.UpstreamPluginGuid))
        {
            useItemsAnywhere = new UseItemsAnywhereAdapter(Logger.LogInfo, Logger.LogWarning);
            if (!useItemsAnywhere.TryInstall())
            {
                useItemsAnywhere.Dispose();
                useItemsAnywhere = null;
            }
        }

        if (Chainloader.PluginInfos.ContainsKey(UiFixesBeltAdapter.UpstreamPluginGuid))
        {
            uiFixesBelt = new UiFixesBeltAdapter(Logger.LogInfo, Logger.LogWarning);
            if (!uiFixesBelt.TryInstall())
            {
                uiFixesBelt.Dispose();
                uiFixesBelt = null;
            }
        }
    }

    private void OnDestroy()
    {
        useItemsAnywhere?.Dispose();
        useItemsAnywhere = null;
        uiFixesBelt?.Dispose();
        uiFixesBelt = null;
    }
}
