using System.Threading;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace SPTBeltArmbandInventory.Server;

internal sealed record WearableProtectionRequest(
    bool ArmBandProtected,
    bool BeltProtected,
    bool HeadBandProtected) : IRequestData;

internal sealed record WearableProtectionSnapshot(
    bool ArmBandProtected,
    bool BeltProtected,
    bool HeadBandProtected);

internal static class WearableProtectionRuntime
{
    private static readonly object Sync = new();

    private static readonly ProtectedWearableRoot[] ArmBandRoots =
    [
        new(BeltDeathPolicy.ArmBand, RuntimeIdentity.CandidateItemId),
        new(BeltDeathPolicy.ArmBand, RuntimeIdentity.WristWalletItemId)
    ];

    private static readonly ProtectedWearableRoot[] BeltRoots =
    [
        new(RuntimeIdentity.DedicatedBeltWireSlotId, RuntimeIdentity.DedicatedMagazineBeltItemId)
    ];

    private static readonly ProtectedWearableRoot[] HeadBandRoots =
    [
        new(RuntimeIdentity.DedicatedHeadBandWireSlotId, RuntimeIdentity.EmergencyHeadBandItemId)
    ];

    private static bool armBandProtected = true;
    private static bool beltProtected = true;
    private static bool headBandProtected = true;
    private static ProtectedWearableRoot[] activeRoots = BuildRoots(true, true, true);

    // Root arrays are immutable after publication. Death/insurance readers take one
    // atomic snapshot so a concurrent F12 update cannot expose a stale/torn policy.
    internal static ProtectedWearableRoot[] ActiveRoots => Volatile.Read(ref activeRoots);

    internal static WearableProtectionSnapshot Snapshot()
    {
        lock (Sync)
            return new WearableProtectionSnapshot(armBandProtected, beltProtected, headBandProtected);
    }

    internal static WearableProtectionSnapshot Apply(WearableProtectionRequest request)
    {
        lock (Sync)
        {
            armBandProtected = request.ArmBandProtected;
            beltProtected = request.BeltProtected;
            headBandProtected = request.HeadBandProtected;
            ProtectedWearableRoot[] nextRoots = BuildRoots(armBandProtected, beltProtected, headBandProtected);
            Volatile.Write(ref activeRoots, nextRoots);
            return new WearableProtectionSnapshot(armBandProtected, beltProtected, headBandProtected);
        }
    }

    private static ProtectedWearableRoot[] BuildRoots(bool armBand, bool belt, bool headBand)
    {
        int count = (armBand ? ArmBandRoots.Length : 0)
            + (belt ? BeltRoots.Length : 0)
            + (headBand ? HeadBandRoots.Length : 0);
        var result = new ProtectedWearableRoot[count];
        int offset = 0;
        if (armBand)
        {
            Array.Copy(ArmBandRoots, 0, result, offset, ArmBandRoots.Length);
            offset += ArmBandRoots.Length;
        }
        if (belt)
        {
            Array.Copy(BeltRoots, 0, result, offset, BeltRoots.Length);
            offset += BeltRoots.Length;
        }
        if (headBand)
            Array.Copy(HeadBandRoots, 0, result, offset, HeadBandRoots.Length);
        return result;
    }
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class WearableProtectionRouter(
    JsonUtil jsonUtil,
    ISptLogger<WearableProtectionRouter> logger)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<WearableProtectionRequest>(
                WearableProtectionContract.Route,
                (url, info, sessionId, output, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WearableProtectionSnapshot snapshot = WearableProtectionRuntime.Apply(info);
                    logger.Info($"B&A&HB protection policy updated: ArmBand={(snapshot.ArmBandProtected ? "Protected" : "Lost")}, Belt={(snapshot.BeltProtected ? "Protected" : "Lost")}, HeadBand={(snapshot.HeadBandProtected ? "Protected" : "Lost")}.");
                    string response = jsonUtil.Serialize(snapshot)
                        ?? throw new InvalidOperationException("B&A&HB protection snapshot serialization failed.");
                    return ValueTask.FromResult(response);
                })
        ])
{ }
