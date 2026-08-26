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

        var loadedInput = new AdmiralTraderRuntimeAdapterReport
        {
            Installed = true,
            ContractAvailable = true,
            ContractState = "LoadedPrototypeContract",
            ModGuid = "com.admiralam.spt.admiraltrader",
            AttributionConfidence = "ExplicitAdapter",
            OfferCount = offers.Length,
            BoundedRenewableOfferCount = offers.Length,
            MinimumEffectiveProgressionLevel = 5,
            MaximumEffectiveProgressionLevel = 11,
            Offers = offers,
        };
        var loaded = SourcePressureRuntimeReportBuilder.Build(loadedInput);

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

        var absentInput = new AdmiralTraderRuntimeAdapterReport
        {
            Installed = false,
            ContractAvailable = false,
            ContractState = "NotInstalled",
            ModGuid = "com.admiralam.spt.admiraltrader",
            AttributionConfidence = "ExplicitAdapter",
            Offers = Array.Empty<AdmiralTraderOfferAdapterEvidence>(),
        };
        var absent = SourcePressureRuntimeReportBuilder.Build(absentInput);
        Require(absent.LoadedAdapterCount == 0 && absent.SourceCount == 0 && absent.CapacityEvidenceCount == 0, "not-installed adapter must not fabricate evidence");

        var degradedInput = absentInput with
        {
            Installed = true,
            ContractState = "ContractUnavailable",
            ContractDiagnostic = "missing gameplay-policy",
        };
        var degraded = SourcePressureRuntimeReportBuilder.Build(degradedInput);
        Require(degraded.LoadedAdapterCount == 0, "contract-unavailable adapter must not be counted as usable evidence");
        Require(degraded.SourceCount == 0 && degraded.CapacityEvidenceCount == 0, "contract-unavailable adapter must suppress source-pressure evidence");

        MustFail("empty modGuid", () => SourcePressureRuntimeReportBuilder.Build(loadedInput with { ModGuid = " " }));
        MustFail("installed OfferCount mismatch", () => SourcePressureRuntimeReportBuilder.Build(loadedInput with { OfferCount = 6 }));
        MustFail("installed bounded count mismatch", () => SourcePressureRuntimeReportBuilder.Build(loadedInput with { BoundedRenewableOfferCount = 6 }));
        MustFail("not-installed contract available", () => SourcePressureRuntimeReportBuilder.Build(absentInput with { ContractAvailable = true }));
        MustFail("unavailable contract carrying offers", () => SourcePressureRuntimeReportBuilder.Build(degradedInput with { Offers = offers, OfferCount = 7, BoundedRenewableOfferCount = 7 }));

        Console.WriteLine("Economy Admiral runtime source-pressure report smoke PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Economy Admiral runtime source-pressure report smoke: {message}");
        }
    }

    private static void MustFail(string name, Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException($"Economy Admiral runtime source-pressure report smoke expected '{name}' to fail.");
    }
}
