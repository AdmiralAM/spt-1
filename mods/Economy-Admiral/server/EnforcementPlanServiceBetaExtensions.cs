namespace SPTEconomy;

public static class EnforcementPlanServiceBetaExtensions
{
    private static readonly HashSet<string> AutomaticRewardFlags = new(StringComparer.Ordinal)
    {
        "HIGH_ITEM_VALUE_LOW_STRUCTURE",
        "RESTARTABLE_HIGH_ITEM_VALUE",
        "HIGH_XP_LOW_DEPTH",
        "RESTARTABLE_HIGH_XP",
        "HIGH_STANDING_LOW_DEPTH",
        RestartableStandingPressureCore.Flag,
    };

    public static Task<EnforcementPlanReport> RunAsync(
        this EnforcementPlanService service,
        QuestAnalysisReport analysis,
        QuestProvenanceDeltaReport provenance,
        AdmiralTraderRuntimeAdapterReport admiralTrader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(admiralTrader);

        var gatedRows = analysis.Quests.Select(row =>
        {
            // Reclassify the final typed reward signals through the maintained pressure classifier before
            // ownership gating. This closes the authored Restartable standing path: the dedicated
            // RestartableHighStanding threshold/cap already existed, but the legacy analysis flags never
            // emitted RESTARTABLE_HIGH_STANDING, so those rewards could fall back to normal standing policy.
            var classifiedRow = row with
            {
                ObservationalFlags = QuestRewardPressureClassifier.Reclassify(
                    new QuestRewardPressureSignals
                    {
                        Restartable = row.Restartable,
                        HandbookValueVsVanillaMedian = row.HandbookValueVsVanillaMedian,
                        XpVsVanillaMedian = row.XpVsVanillaMedian,
                        StandingVsVanillaMedian = row.StandingVsVanillaMedian,
                        PrerequisiteDepthVsVanillaMedian = row.PrerequisiteDepthVsVanillaMedian,
                        StructuredConstraintsVsVanillaMedian = row.StructuredConstraintsVsVanillaMedian,
                        ExistingFlags = row.ObservationalFlags,
                    },
                    analysis.Policy).ToList(),
            };

            var gate = TraderOwnershipEnforcementGate.Evaluate(classifiedRow.TraderId, admiralTrader);
            if (gate.AutomaticRewardMutationAllowed) return classifiedRow;

            return classifiedRow with
            {
                // Remove only automatic mutation-driving flags. Non-mutating diagnostics stay visible,
                // while explicit manual targets remain available inside EnforcementPlanService and are
                // still constrained by the existing provenance/dimension gates.
                ObservationalFlags = classifiedRow.ObservationalFlags
                    .Where(flag => !AutomaticRewardFlags.Contains(flag))
                    .OrderBy(flag => flag, StringComparer.Ordinal)
                    .ToList(),
            };
        }).ToList();

        var gatedAnalysis = analysis with
        {
            Quests = gatedRows,
            FlagCounts = gatedRows.SelectMany(row => row.ObservationalFlags)
                .GroupBy(flag => flag, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Note = analysis.Note + " Beta reward-pressure reclassification applies maintained restartable item/XP/standing signals before ownership gating. Admiral Trader automatic reward normalization still requires the maintained explicit Gameplay Alpha v4 contract; incompatible/absent contract evidence suppresses only automatic mutation-driving flags, never provenance checks or explicit manual targets.",
        };

        return service.RunAsync(gatedAnalysis, provenance, cancellationToken);
    }
}
