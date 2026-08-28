using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class DeathPolicyTemplateScopeRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string rcTpl = RuntimeIdentity.CandidateItemId;
        const string ordinaryArmbandTpl = "5b3f3af486f774679e752c1f";

        var rcTree = new[]
        {
            new BeltInventoryNode("equipment", null, null, null),
            new BeltInventoryNode("armband", "equipment", BeltDeathPolicy.ArmBand, rcTpl),
            new BeltInventoryNode("arm-child", "armband", "main", "magazine")
        };
        Assert(BeltDeathPolicy.ShouldKeep("armband", rcTree, rcTpl), "explicit RC root is retained");
        Assert(BeltDeathPolicy.ShouldKeep("arm-child", rcTree, rcTpl), "RC descendants are retained");

        var ordinaryTree = new[]
        {
            new BeltInventoryNode("equipment", null, null, null),
            new BeltInventoryNode("armband", "equipment", BeltDeathPolicy.ArmBand, ordinaryArmbandTpl),
            new BeltInventoryNode("child", "armband", "main", "unexpected-child")
        };
        Assert(!BeltDeathPolicy.ShouldKeep("armband", ordinaryTree, rcTpl), "ordinary armband does not inherit RC death protection");
        Assert(!BeltDeathPolicy.ShouldKeep("child", ordinaryTree, rcTpl), "ordinary armband descendants do not inherit RC death protection");
        Assert(BeltDeathPolicy.GetKeptTreeIds(ordinaryTree, rcTpl).Count == 0, "non-RC ArmBand tree remains fully vanilla");

        var roots = new[]
        {
            new ProtectedWearableRoot(BeltDeathPolicy.ArmBand, RuntimeIdentity.CandidateItemId),
            new ProtectedWearableRoot(BeltDeathPolicy.ArmBand, RuntimeIdentity.WristWalletItemId),
            new ProtectedWearableRoot(RuntimeIdentity.DedicatedBeltWireSlotId, RuntimeIdentity.DedicatedMagazineBeltItemId),
            new ProtectedWearableRoot(RuntimeIdentity.DedicatedHeadBandWireSlotId, RuntimeIdentity.EmergencyHeadBandItemId)
        };
        var allWearables = new[]
        {
            new BeltInventoryNode("equipment", null, null, null),
            new BeltInventoryNode("armband", "equipment", BeltDeathPolicy.ArmBand, RuntimeIdentity.WristWalletItemId),
            new BeltInventoryNode("cash", "armband", "main", "cash"),
            new BeltInventoryNode("belt", "equipment", RuntimeIdentity.DedicatedBeltWireSlotId, RuntimeIdentity.DedicatedMagazineBeltItemId),
            new BeltInventoryNode("mag", "belt", "main", "magazine"),
            new BeltInventoryNode("headband", "equipment", RuntimeIdentity.DedicatedHeadBandWireSlotId, RuntimeIdentity.EmergencyHeadBandItemId),
            new BeltInventoryNode("smokes", "headband", "main", "cigarettes"),
            new BeltInventoryNode("wrong", "equipment", RuntimeIdentity.DedicatedBeltWireSlotId, "unrelated-template")
        };
        var kept = BeltDeathPolicy.GetKeptTreeIds(allWearables, roots);
        Assert(kept.Contains("armband") && kept.Contains("cash"), "ArmBand protected tree is retained");
        Assert(kept.Contains("belt") && kept.Contains("mag"), "Belt protected tree is retained");
        Assert(kept.Contains("headband") && kept.Contains("smokes"), "HeadBand protected tree is retained");
        Assert(!kept.Contains("wrong"), "unregistered template in dedicated slot remains vanilla");

        var armBandOnlyRoots = new[]
        {
            new ProtectedWearableRoot(BeltDeathPolicy.ArmBand, RuntimeIdentity.CandidateItemId),
            new ProtectedWearableRoot(BeltDeathPolicy.ArmBand, RuntimeIdentity.WristWalletItemId)
        };
        var armBandOnly = BeltDeathPolicy.GetKeptTreeIds(allWearables, armBandOnlyRoots);
        Assert(armBandOnly.Contains("armband") && armBandOnly.Contains("cash"), "ArmBand-only policy keeps exactly its owned tree");
        Assert(!armBandOnly.Contains("belt") && !armBandOnly.Contains("mag"), "disabled Belt family receives no special death protection");
        Assert(!armBandOnly.Contains("headband") && !armBandOnly.Contains("smokes"), "disabled HeadBand family receives no special death protection");

        var beltOnlyRoots = new[]
        {
            new ProtectedWearableRoot(RuntimeIdentity.DedicatedBeltWireSlotId, RuntimeIdentity.DedicatedMagazineBeltItemId)
        };
        var beltOnly = BeltDeathPolicy.GetKeptTreeIds(allWearables, beltOnlyRoots);
        Assert(beltOnly.Contains("belt") && beltOnly.Contains("mag"), "Belt-only policy keeps Belt root and descendants");
        Assert(!beltOnly.Contains("armband") && !beltOnly.Contains("cash"), "Belt-only policy does not leak into ArmBand");
        Assert(!beltOnly.Contains("headband") && !beltOnly.Contains("smokes"), "Belt-only policy does not leak into HeadBand");

        var headBandOnlyRoots = new[]
        {
            new ProtectedWearableRoot(RuntimeIdentity.DedicatedHeadBandWireSlotId, RuntimeIdentity.EmergencyHeadBandItemId)
        };
        var headBandOnly = BeltDeathPolicy.GetKeptTreeIds(allWearables, headBandOnlyRoots);
        Assert(headBandOnly.Contains("headband") && headBandOnly.Contains("smokes"), "HeadBand-only policy keeps HeadBand root and descendants");
        Assert(!headBandOnly.Contains("armband") && !headBandOnly.Contains("cash"), "HeadBand-only policy does not leak into ArmBand");
        Assert(!headBandOnly.Contains("belt") && !headBandOnly.Contains("mag"), "HeadBand-only policy does not leak into Belt");

        var lostAll = BeltDeathPolicy.GetKeptTreeIds(allWearables, Array.Empty<ProtectedWearableRoot>());
        Assert(lostAll.Count == 0, "LostOnDeath for every family delegates all loss semantics back to vanilla SPT");

        var wrongHost = new[]
        {
            new BeltInventoryNode("belt", "equipment", "TacticalVest", rcTpl),
            new BeltInventoryNode("mag", "belt", "main", "magazine")
        };
        Assert(BeltDeathPolicy.GetKeptTreeIds(wrongHost, rcTpl).Count == 0, "RC template outside ArmBand host does not gain protected-tree semantics");
        Assert(BeltDeathPolicy.GetKeptTreeIds(rcTree, (string)null).Count == 0, "missing explicit protected template fails closed");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Death policy template-scope regression failed: " + message);
    }
}
