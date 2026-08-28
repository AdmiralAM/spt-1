using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ProtectionSettingsContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Assert(
            WearableProtectionContract.Encode(true, false, true)
                == "{\"armBandProtected\":true,\"beltProtected\":false,\"headBandProtected\":true}",
            "ArmBand/Belt/HeadBand booleans retain their wire-field identity");
        Assert(
            WearableProtectionContract.Encode(false, true, false)
                == "{\"armBandProtected\":false,\"beltProtected\":true,\"headBandProtected\":false}",
            "inverse per-family modes remain independent");
        Assert(
            WearableProtectionContract.Encode(false, false, false)
                == "{\"armBandProtected\":false,\"beltProtected\":false,\"headBandProtected\":false}",
            "all LostOnDeath emits an explicit all-false payload");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Protection settings contract regression failed: " + message);
    }
}
