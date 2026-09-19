using System;
using BepInEx;
using BepInEx.Bootstrap;

namespace AdmiralCompatibilitySuite;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(BeltPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(UseItemsAnywhereAdapter.UpstreamPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.admiralam.compatibility-suite";
    public const string PluginName = "Admiral Compatibility Suite";
    public const string PluginVersion = "0.1.0";
    internal const string BeltPluginGuid = "com.admiralam.spt.belt-armband-inventory";

    private UseItemsAnywhereAdapter useItemsAnywhere;

    private void Awake()
    {
        if (!Chainloader.PluginInfos.ContainsKey(BeltPluginGuid)
            || !Chainloader.PluginInfos.ContainsKey(UseItemsAnywhereAdapter.UpstreamPluginGuid))
        {
            return;
        }

        useItemsAnywhere = new UseItemsAnywhereAdapter(Logger.LogInfo, Logger.LogWarning);
        if (!useItemsAnywhere.TryInstall())
        {
            useItemsAnywhere.Dispose();
            useItemsAnywhere = null;
        }
    }

    private void OnDestroy()
    {
        useItemsAnywhere?.Dispose();
        useItemsAnywhere = null;
    }
}
