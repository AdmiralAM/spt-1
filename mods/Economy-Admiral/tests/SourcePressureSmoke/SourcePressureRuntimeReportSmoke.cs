using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class SourcePressureRuntimeReportSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var offers = Enumerable.Range(1, 7).Select(index => new AdmiralTraderOfferAdapterEvidence
        {
            OfferId = $"offer{index}", ItemTemplateId = $"tpl{index}", QuestGateId = $"quest{index}", LoyaltyLevel = 1,
            StockPerReset = 10 * index, BuyRestrictionPerReset = 10 * index,
            Source = new AcquisitionSourceEvidence
            {
                ItemTemplateId = $"tpl{index}", SourceId = $"admiral-trader:offer{index}", Channel = AcquisitionChannel.TraderPurchase,
                Renewable = true, EarliestProgressionLevel = index + 4, ProvenanceClass = "ExplicitAdapter",
            },
            Capacity = new RenewableSupplyCapacityEvidence
            {
                ItemTemplateId = $"tpl{index}", SourceId = $"admiral-trader:offer{index}", Channel = AcquisitionChannel.TraderPurchase,
                SupplyBound = RenewableSupplyBound.Bounded, MaxUnitsPerReset = 10 * index, MaxAcquisitionsPerReset = 10 * index,
            },
        }).ToArray();

        var finalDb = FinalDb();
        var loadedInput = new AdmiralTraderRuntimeAdapterReport
        {
            Installed = true, ContractAvailable = true, ContractState = "LoadedPrototypeContract",
            ModGuid = "com.admiralam.spt.admiraltrader", AttributionConfidence = "ExplicitAdapter",
            OfferCount = offers.Length, BoundedRenewableOfferCount = offers.Length,
            MinimumEffectiveProgressionLevel = 5, MaximumEffectiveProgressionLevel = 11, Offers = offers,
        };
        var loaded = SourcePressureRuntimeReportBuilder.Build(finalDb, loadedInput);

        Require(loaded.SchemaVersion == 2, "schema version mismatch");
        Require(loaded.EvidenceCoverage.Contains("FinalDbCore", StringComparison.Ordinal), "coverage must declare final DB evidence");
        Require(loaded.LoadedAdapterCount == 1, "loaded adapter count mismatch");
        Require(loaded.SourceCount == 8 && loaded.CapacityEvidenceCount == 7, "final DB source plus seven adapter offers expected");
        Require(loaded.Items.Any(x => x.ItemTemplateId == "final-db-item" && x.ProvenanceClasses.Contains("ModAdded")), "final DB provenance must survive aggregation");
        Require(loaded.Capacity.Single(x => x.ItemTemplateId == "final-db-item").UnknownCapacityRenewableSourceCount == 1, "uncovered final DB capacity must remain unknown");
        Require(loaded.ChannelCoverage.Single(x => x.Channel == AcquisitionChannel.WorldLoot).State == "UnknownNoMaintainedAdapter", "world loot must remain explicitly unknown");
        Require(loaded.AcquisitionGraph.ResolvedItemCount == 1 && loaded.StartupMilliseconds == 1.25d, "graph/startup evidence must survive report build");

        var absentInput = new AdmiralTraderRuntimeAdapterReport
        {
            Installed = false, ContractAvailable = false, ContractState = "NotInstalled",
            ModGuid = "com.admiralam.spt.admiraltrader", AttributionConfidence = "ExplicitAdapter",
            Offers = Array.Empty<AdmiralTraderOfferAdapterEvidence>(),
        };
        var absent = SourcePressureRuntimeReportBuilder.Build(finalDb, absentInput);
        Require(absent.LoadedAdapterCount == 0 && absent.SourceCount == 1, "not-installed adapter must preserve final DB evidence without fabricating adapter sources");

        var degraded = SourcePressureRuntimeReportBuilder.Build(finalDb, absentInput with
        {
            Installed = true, ContractState = "ContractUnavailable", ContractDiagnostic = "missing gameplay-policy",
        });
        Require(degraded.LoadedAdapterCount == 0 && degraded.SourceCount == 1, "contract-unavailable adapter must be suppressed while final DB evidence remains");

        MustFail(() => SourcePressureRuntimeReportBuilder.Build(finalDb, loadedInput with { ModGuid = " " }));
        MustFail(() => SourcePressureRuntimeReportBuilder.Build(finalDb, loadedInput with { OfferCount = 6 }));
        MustFail(() => SourcePressureRuntimeReportBuilder.Build(finalDb, absentInput with { ContractAvailable = true }));
        Console.WriteLine("Economy Admiral runtime source-pressure report smoke PASS");
    }

    private static FinalDbSourceObservation FinalDb()
    {
        var source = new AcquisitionSourceEvidence
        {
            ItemTemplateId = "final-db-item", SourceId = "quest:modded", Channel = AcquisitionChannel.RepeatableQuestReward,
            Renewable = true, EarliestProgressionLevel = 10, ProvenanceClass = "ModAdded",
        };
        var graph = EffectiveAcquisitionGraph.Resolve([
            new AcquisitionCostPath { ItemTemplateId = "rub", PathId = "currency", Channel = AcquisitionChannel.TraderPurchase, FixedReferenceCost = 1d, Dependencies = Array.Empty<AcquisitionCostDependency>() },
        ]);
        return new FinalDbSourceObservation
        {
            Sources = [source], CostPaths = [], AcquisitionGraph = graph, StartupMilliseconds = 1.25d,
            ChannelCoverage = Enum.GetValues<AcquisitionChannel>().Select(channel => new ChannelObservationCoverage
            {
                Channel = channel,
                State = channel == AcquisitionChannel.WorldLoot ? "UnknownNoMaintainedAdapter" : "ObservedFinalDb",
                ObservedSourceCount = channel == AcquisitionChannel.RepeatableQuestReward ? 1 : 0,
            }).ToArray(),
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral runtime source-pressure report smoke: {message}");
    }

    private static void MustFail(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Economy Admiral runtime source-pressure report smoke expected failure.");
    }
}
