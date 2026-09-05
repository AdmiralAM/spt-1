using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ProtectionSettingsContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string mixed = WearableProtectionContract.Encode(true, false, true);
        Assert(
            mixed == "{\"armBandProtected\":true,\"beltProtected\":false,\"headBandProtected\":true}",
            "ArmBand/Belt/HeadBand booleans retain their wire-field identity");
        Assert(
            WearableProtectionContract.Encode(false, true, false)
                == "{\"armBandProtected\":false,\"beltProtected\":true,\"headBandProtected\":false}",
            "inverse per-family modes remain independent");
        Assert(
            WearableProtectionContract.Encode(false, false, false)
                == "{\"armBandProtected\":false,\"beltProtected\":false,\"headBandProtected\":false}",
            "all LostOnDeath emits an explicit all-false payload");

        Assert(WearableProtectionContract.IsAcknowledgement(mixed, mixed),
            "exact applied snapshot is accepted as protection acknowledgement");
        Assert(WearableProtectionContract.IsAcknowledgement("  " + mixed + "\r\n", mixed),
            "transport-only outer whitespace does not invalidate acknowledgement");
        Assert(!WearableProtectionContract.IsAcknowledgement("", mixed),
            "empty response must not be reported as successful sync");
        Assert(!WearableProtectionContract.IsAcknowledgement(
            WearableProtectionContract.Encode(false, false, true), mixed),
            "mismatched server snapshot must fail acknowledgement");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Protection settings contract regression failed: " + message);
    }
}
