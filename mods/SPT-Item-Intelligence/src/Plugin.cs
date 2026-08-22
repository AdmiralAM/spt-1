using BepInEx;

namespace SPTItemIntelligence
{
    [BepInPlugin("com.admiralam.spt.itemintelligence", "SPT Item Intelligence", "0.4.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            if (ItemIntelligenceRegistry.Shared == null) throw new System.InvalidOperationException("Item Intelligence registry initialization failed.");
            Logger.LogInfo("SPT Item Intelligence v0.4.0 loaded (Phase 4 requirement index ready)");
        }
    }
}
