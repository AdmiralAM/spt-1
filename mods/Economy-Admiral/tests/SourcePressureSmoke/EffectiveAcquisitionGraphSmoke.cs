using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class EffectiveAcquisitionGraphSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var resolved = EffectiveAcquisitionGraph.Resolve([
            Path("rub", "currency", 1d),
            Path("input", "buy-input", null, [new("rub", 10d)]),
            Path("output", "craft-output", null, [new("input", 2d)]),
        ]);
        var output = resolved.Items.Single(x => x.ItemTemplateId == "output");
        Require(output.Known && output.Cost == 20d, "recursive known path cost must resolve deterministically");

        var unknown = EffectiveAcquisitionGraph.Resolve([
            Path("output", "barter", null, [new("missing", 2d)]),
        ]).Items.Single();
        Require(!unknown.Known && unknown.State == "UnknownDependencies", "unknown dependency must remain Unknown");

        var cycle = EffectiveAcquisitionGraph.Resolve([
            Path("a", "a-to-b", null, [new("b", 1d)]),
            Path("b", "b-to-a", null, [new("a", 1d)]),
        ]);
        Require(cycle.CycleBlockCount > 0 && cycle.Items.All(x => !x.Known), "cycles must terminate and remain unknown");

        var depth = EffectiveAcquisitionGraph.Resolve([
            Path("a", "a", null, [new("b", 1d)]),
            Path("b", "b", null, [new("c", 1d)]),
            Path("c", "c", null, [new("d", 1d)]),
            Path("d", "d", 1d),
        ], maxDepth: 2);
        Require(depth.DepthBlockCount > 0, "depth cap must stop pathological traversal");

        var deterministicA = EffectiveAcquisitionGraph.Resolve([
            Path("x", "z", 5d), Path("x", "a", 5d),
        ]).Items.Single();
        var deterministicB = EffectiveAcquisitionGraph.Resolve([
            Path("x", "a", 5d), Path("x", "z", 5d),
        ]).Items.Single();
        Require(deterministicA.SelectedPathId == "a" && deterministicB.SelectedPathId == "a", "equal-cost path selection must be input-order independent");

        Console.WriteLine("Economy Admiral effective acquisition graph smoke PASS");
    }

    private static AcquisitionCostPath Path(string item, string id, double? fixedCost = null, IReadOnlyList<AcquisitionCostDependency>? deps = null) => new()
    {
        ItemTemplateId = item,
        PathId = id,
        Channel = AcquisitionChannel.TraderBarter,
        FixedReferenceCost = fixedCost,
        Dependencies = deps ?? Array.Empty<AcquisitionCostDependency>(),
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral acquisition graph smoke: {message}");
    }
}
