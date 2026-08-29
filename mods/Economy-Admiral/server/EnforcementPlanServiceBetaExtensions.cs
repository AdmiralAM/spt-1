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
            var gate = TraderOwnershipEnforcementGate.Evaluate(row.TraderId, admiralTrader);
            if (gate.AutomaticRewardMutationAllowed) return row;

            return row with
            {
                // Remove only automatic mutation-driving flags. Non-mutating diagnostics stay visible,
                // while explicit manual targets remain available inside EnforcementPlanService and are
                // still constrained by the existing provenance/dimension gates.
                ObservationalFlags = row.ObservationalFlags
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
            Note = analysis.Note + " Beta ownership gate: Admiral Trader automatic reward normalization requires the maintained explicit Gameplay Alpha v4 contract; incompatible/absent contract evidence suppresses only automatic mutation-driving flags, never provenance checks or explicit manual targets.",
        };

        return service.RunAsync(gatedAnalysis, provenance, cancellationToken);
    }
}
