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
            new BeltInventoryNode("belt", "equipment", BeltDeathPolicy.ArmBand, rcTpl),
            new BeltInventoryNode("mag", "belt", "main", "magazine")
        };
        Assert(BeltDeathPolicy.ShouldKeep("belt", rcTree, rcTpl), "explicit RC root is retained");
        Assert(BeltDeathPolicy.ShouldKeep("mag", rcTree, rcTpl), "RC descendants are retained");

        var ordinaryTree = new[]
        {
            new BeltInventoryNode("equipment", null, null, null),
            new BeltInventoryNode("armband", "equipment", BeltDeathPolicy.ArmBand, ordinaryArmbandTpl),
            new BeltInventoryNode("child", "armband", "main", "unexpected-child")
        };
        Assert(!BeltDeathPolicy.ShouldKeep("armband", ordinaryTree, rcTpl), "ordinary armband does not inherit RC death protection");
        Assert(!BeltDeathPolicy.ShouldKeep("child", ordinaryTree, rcTpl), "ordinary armband descendants do not inherit RC death protection");
        Assert(BeltDeathPolicy.GetKeptTreeIds(ordinaryTree, rcTpl).Count == 0, "non-RC ArmBand tree remains fully vanilla");

        var wrongHost = new[]
        {
            new BeltInventoryNode("belt", "equipment", "TacticalVest", rcTpl),
            new BeltInventoryNode("mag", "belt", "main", "magazine")
        };
        Assert(BeltDeathPolicy.GetKeptTreeIds(wrongHost, rcTpl).Count == 0, "RC template outside ArmBand host does not gain protected-tree semantics");
        Assert(BeltDeathPolicy.GetKeptTreeIds(rcTree, null).Count == 0, "missing explicit protected template fails closed");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Death policy template-scope regression failed: " + message);
    }
}
