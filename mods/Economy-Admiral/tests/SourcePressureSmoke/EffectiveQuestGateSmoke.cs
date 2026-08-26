using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class EffectiveQuestGateSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void MustFail(string name, Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { Console.WriteLine($"PASS {name}"); return; }
            throw new InvalidOperationException($"Expected '{name}' to fail.");
        }

        var sidearms = new[]
        {
            new QuestGateNode
            {
                QuestId = "59ca4829e098dfafa03888d2",
                LevelRequirement = 5,
            },
            new QuestGateNode
            {
                QuestId = "b016df9d2bea4269cc59d531",
                LevelRequirement = 8,
                PrerequisiteQuestIds = new[] { "59ca4829e098dfafa03888d2" },
            },
            new QuestGateNode
            {
                QuestId = "8cba3e2ec639a4aa2c26c4da",
                LevelRequirement = 12,
                PrerequisiteQuestIds = new[] { "b016df9d2bea4269cc59d531" },
            },
        };

        var resolved = EffectiveQuestGateEvidenceResolver.Resolve("8cba3e2ec639a4aa2c26c4da", sidearms);
        Require(resolved.MaximumPrerequisiteDepth == 2, "Sidearms Munitions depth must include Qualification -> Fieldwork -> Munitions.");
        Require(resolved.EffectiveMinimumLevel == 12, "Effective gate must use authored quest-chain level constraints, not LL1 trader metadata.");
        Require(resolved.KnownLevelConstraintCount == 3, "All three sidearms stages carry explicit level evidence.");
        Require(resolved.CompleteQuestGraphEvidence, "Complete sidearms fixture should remain complete.");

        var offer = new AdmiralTraderOfferAdapterEvidence
        {
            OfferId = "6cf0fc22a55417075c5af23e",
            ItemTemplateId = "5cc80f53e4a949000e1ea4f8",
            QuestGateId = "8cba3e2ec639a4aa2c26c4da",
            LoyaltyLevel = 1,
            StockPerReset = 80,
            BuyRestrictionPerReset = 80,
            Source = new AcquisitionSourceEvidence
            {
                ItemTemplateId = "5cc80f53e4a949000e1ea4f8",
                SourceId = "admiral-trader:6cf0fc22a55417075c5af23e",
                Channel = AcquisitionChannel.TraderPurchase,
                Renewable = true,
                EarliestProgressionLevel = null,
                ProvenanceClass = AdmiralTraderAdapterEvidence.AttributionConfidence,
            },
            Capacity = new RenewableSupplyCapacityEvidence
            {
                ItemTemplateId = "5cc80f53e4a949000e1ea4f8",
                SourceId = "admiral-trader:6cf0fc22a55417075c5af23e",
                Channel = AcquisitionChannel.TraderPurchase,
                SupplyBound = RenewableSupplyBound.Bounded,
                MaxUnitsPerReset = 80,
                MaxAcquisitionsPerReset = 80,
            },
        };

        var enriched = AdmiralTraderItemAdapter.ApplyEffectiveQuestGates(new[] { offer }, sidearms).Single();
        Require(enriched.LoyaltyLevel == 1, "LL1 metadata must remain unchanged.");
        Require(enriched.Source.EarliestProgressionLevel == 12, "Economic availability must use the resolved quest gate, not LL1.");
        Require(enriched.EffectiveGate?.MaximumPrerequisiteDepth == 2, "Offer must retain resolved quest-depth evidence.");

        MustFail("missing prerequisite", () => EffectiveQuestGateEvidenceResolver.Resolve(
            "q2",
            new[]
            {
                new QuestGateNode { QuestId = "q2", LevelRequirement = 10, PrerequisiteQuestIds = new[] { "missing" } },
            }
        ));

        MustFail("cycle", () => EffectiveQuestGateEvidenceResolver.Resolve(
            "q1",
            new[]
            {
                new QuestGateNode { QuestId = "q1", LevelRequirement = 5, PrerequisiteQuestIds = new[] { "q2" } },
                new QuestGateNode { QuestId = "q2", LevelRequirement = 10, PrerequisiteQuestIds = new[] { "q1" } },
            }
        ));

        MustFail("invalid level", () => EffectiveQuestGateEvidenceResolver.Resolve(
            "q1",
            new[] { new QuestGateNode { QuestId = "q1", LevelRequirement = 0 } }
        ));

        Console.WriteLine("Economy Admiral effective quest gate smoke PASS");
    }
}
