using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class SourcePressureRuntimeReportSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var offers = Enumerable.Range(1, 7)
            .Select(index => new AdmiralTraderOfferAdapterEvidence
            {
                OfferId = $"offer{index}",
                ItemTemplateId = $"tpl{index}",
                QuestGateId = $"quest{index}",
                LoyaltyLevel = 1,
                StockPerReset = 10 * index,
                BuyRestrictionPerReset = 10 * index,
                Source = new AcquisitionSourceEvidence
                {
                    ItemTemplateId = $"tpl{index}",
                    SourceId = $"admiral-trader:offer{index}",
                    Channel = AcquisitionChannel.TraderPurchase,
                    Renewable = true,
                    EarliestProgressionLevel = index + 4,
                    ProvenanceClass = "ExplicitAdapter",
                },
                Capacity = new RenewableSupplyCapacityEvidence
                {
                    ItemTemplateId = $"tpl{index}",
                    SourceId = $"admiral-trader:offer{index}",
                    Channel = AcquisitionChannel.TraderPurchase,
                    SupplyBound = RenewableSupplyBound.Bounded,
                    MaxUnitsPerReset = 10 * index,
                    MaxAcquisitionsPerReset = 10 * index,
                },
            })
            .ToArray();

        var loaded = SourcePressureRuntimeReportBuilder.Build(new AdmiralTraderRuntimeAdapterReport
        {
            Installed = true,
            ModGuid = "com.admiralam.spt.admiraltrader",
            AttributionConfidence = "ExplicitAdapter",
            OfferCount = offers.Length,
            BoundedRenewableOfferCount = offers.Length,
            MinimumEffectiveProgressionLevel = 5,
            MaximumEffectiveProgressionLevel = 11,
            Offers = offers,
        });

        Require(loaded.SchemaVersion == 1, "schema version mismatch");
        Require(loaded.EvidenceCoverage == "ExplicitAdaptersOnly", "coverage must remain explicit-adapters-only");
        Require(loaded.LoadedAdapterCount == 1, "loaded adapter count mismatch");
        Require(loaded.LoadedAdapters.SequenceEqual(new[] { "com.admiralam.spt.admiraltrader" }), "loaded adapter identity mismatch");
        Require(loaded.SourceCount == 7 && loaded.CapacityEvidenceCount == 7, "seven offers must produce seven source/capacity records");
        Require(loaded.Items.Count == 7 && loaded.Capacity.Count == 7, "seven unique items must enter pressure/capacity summaries");
        Require(loaded.Items.All(item => item.HasCompleteProgressionEvidence), "explicit adapter progression evidence must remain complete");
        Require(loaded.Items.All(item => item.ProvenanceClasses.SequenceEqual(new[] { "ExplicitAdapter" })), "explicit provenance must survive aggregation");
        Require(loaded.Capacity.All(item => item.HasOnlyKnownBoundedRenewablePaths), "all maintained adapter paths must remain bounded");
        Require(loaded.Items.Select(item => item.ItemTemplateId).SequenceEqual(loaded.Items.Select(item => item.ItemTemplateId).OrderBy(value => value, StringComparer.Ordinal)), "item report ordering must be deterministic");

        var absent = SourcePressureRuntimeReportBuilder.Build(new AdmiralTraderRuntimeAdapterReport
        {
            Installed = false,
            ModGuid = "com.admiralam.spt.admiraltrader",
            AttributionConfidence = "ExplicitAdapter",
            Offers = Array.Empty<AdmiralTraderOfferAdapterEvidence>(),
        });

        Require(absent.EvidenceCoverage == "ExplicitAdaptersOnly", "empty report must not claim full-economy coverage");
        Require(absent.LoadedAdapterCount == 0 && absent.LoadedAdapters.Count == 0, "not-installed adapter must not be reported as loaded");
        Require(absent.SourceCount == 0 && absent.CapacityEvidenceCount == 0, "not-installed adapter must not fabricate evidence");
        Require(absent.Items.Count == 0 && absent.Capacity.Count == 0, "not-installed state must produce empty observational summaries");

        Console.WriteLine("Economy Admiral runtime source-pressure report smoke PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Economy Admiral runtime source-pressure report smoke: {message}");
        }
    }
}
