using System;
using BepInEx;
using BepInEx.Bootstrap;

namespace AdmiralCompatibilitySuite;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(ExternalCompatibilityClaims.PackNStrapPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(UseItemsAnywhereAdapter.UpstreamPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(UiFixesBeltAdapter.UpstreamPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    private static readonly System.Action<string> QuietInfo = _ => { };
    public const string PluginGuid = "com.admiralam.compatibility-suite";
    public const string PluginName = "Admiral Compatibility Suite";
    public const string PluginVersion = "0.1.0";
    internal const string BeltPluginGuid = "com.admiralam.spt.belt-armband-inventory";

    private UseItemsAnywhereAdapter useItemsAnywhere;
    private UiFixesBeltAdapter uiFixesBelt;
    private TextureReadbackAdapter textureReadback;

    private void Awake()
    {
        textureReadback = new TextureReadbackAdapter(Logger.LogWarning);
        textureReadback.TryInstall();

        if (!Chainloader.PluginInfos.ContainsKey(BeltPluginGuid))
        {
            return;
        }

        ExternalCompatibilityClaims.TryClaimClient(
            Chainloader.PluginInfos.ContainsKey(ExternalCompatibilityClaims.PackNStrapPluginGuid),
            QuietInfo,
            Logger.LogWarning);

        if (Chainloader.PluginInfos.ContainsKey(UseItemsAnywhereAdapter.UpstreamPluginGuid))
        {
            useItemsAnywhere = new UseItemsAnywhereAdapter(QuietInfo, Logger.LogWarning);
            if (!useItemsAnywhere.TryInstall())
            {
                useItemsAnywhere.Dispose();
                useItemsAnywhere = null;
            }
        }

        if (Chainloader.PluginInfos.ContainsKey(UiFixesBeltAdapter.UpstreamPluginGuid))
        {
            uiFixesBelt = new UiFixesBeltAdapter(QuietInfo, Logger.LogWarning);
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
        textureReadback?.Dispose();
        textureReadback = null;
    }
}
