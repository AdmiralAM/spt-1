using BepInEx;

namespace SPTBeltArmbandInventory
{
    [BepInPlugin("com.admiralam.spt.belt-armband-inventory.boundary-discovery", "B&A&HB Runtime Boundary Discovery", "0.1.0")]
    public sealed class RuntimeBoundaryDiscoveryPlugin : BaseUnityPlugin
    {
        void Awake()
        {
            bool resolved = new RuntimeInventoryBoundaryDiscovery(Logger.LogInfo, Logger.LogWarning).Run();
            if (resolved)
            {
                Logger.LogInfo("B&A&HB LOAD-SAFE GATE: discovery resolved; custom taxonomy intentionally remains disabled for this artifact.");
            }
            else
            {
                Logger.LogWarning("B&A&HB LOAD-SAFE GATE: discovery unresolved; fail-closed mode active and custom taxonomy intentionally remains disabled.");
            }
        }
    }
}
