using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTEconomy;

namespace EconomyAdmiral;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1000), UsedImplicitly]
public sealed class EconomyMod(
    EconomyRuntimeConfigService runtimeConfigService,
    VanillaBaselineService vanillaBaselineService,
    RuntimeEvidenceService runtimeEvidenceService,
    EconomyAuditService auditService,
    RewardUtilityAuditService rewardUtilityAuditService,
    QuestProgressionGraphService questProgressionGraphService,
    QuestConstraintAuditService questConstraintAuditService,
    QuestAnalysisService questAnalysisService,
    QuestProvenanceDeltaService questProvenanceDeltaService,
    EnforcementPlanService enforcementPlanService,
    NativeRepeatableQuestPressureService nativeRepeatableQuestPressureService,
    GroupedItemRuntimeEvidenceService groupedItemRuntimeEvidenceService,
    SourcePressureObservationPipelineService sourcePressureObservationPipelineService,
    EconomyHealthRuntimeReportService economyHealthRuntimeReportService,
    EconomyEnforcementTransactionSnapshotService enforcementTransactionSnapshotService,
    TraderPurchasePressureService traderPurchasePressureService,
    TraderSellPressureService traderSellPressureService,
    FleaPurchasePressureService fleaPurchasePressureService,
    FleaListingFeePressureService fleaListingFeePressureService,
    LootPressureService lootPressureService
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        if (config.Mode == EconomyMode.Off)
            return;

        var vanillaBaseline = vanillaBaselineService.GetSnapshot();
        runtimeEvidenceService.CaptureBefore();

        await auditService.RunAsync(vanillaBaseline, cancellationToken);
        var progressionSnapshot = await questProgressionGraphService.RunAsync(vanillaBaseline, cancellationToken);
        var questAnalysis = await questAnalysisService.RunAsync(progressionSnapshot, vanillaBaseline, cancellationToken);
        await rewardUtilityAuditService.RunAsync(questAnalysis, vanillaBaseline, cancellationToken);
        await questConstraintAuditService.RunAsync(questAnalysis, vanillaBaseline, cancellationToken);

        var questProvenance = await questProvenanceDeltaService.RunAsync(vanillaBaseline, questAnalysis, cancellationToken);

        var observation = await sourcePressureObservationPipelineService.RunAsync(config, vanillaBaseline, cancellationToken);
        await economyHealthRuntimeReportService.RunAsync(config, observation.SourcePressure, cancellationToken);

        questAnalysis = PlayableQuestRewardPolicy.ApplyToEnforcement(config, questAnalysis);

        EconomyEnforcementTransactionSnapshot? transactionSnapshot = null;
        if (config.Mode == EconomyMode.Enforce)
            transactionSnapshot = enforcementTransactionSnapshotService.Capture(config);

        try
        {
            GroupedItemRewardSlot.ResetEvidence();
            var enforcement = await enforcementPlanService.RunAsync(questAnalysis, questProvenance, observation.AdmiralTrader, cancellationToken);

            if (config.Mode == EconomyMode.Enforce
                && enforcement.PlannedMutationCount > 0
                && !enforcement.TransactionCommitted)
            {
                throw new InvalidOperationException(
                    $"Quest enforcement did not commit its planned transaction: planned={enforcement.PlannedMutationCount}, " +
                    $"rolledBack={enforcement.TransactionRolledBack}, error={enforcement.TransactionError ?? "none"}.");
            }

            nativeRepeatableQuestPressureService.Apply(config);
            traderPurchasePressureService.Apply(config);
            traderSellPressureService.Apply(config);
            fleaPurchasePressureService.Apply(config);
            fleaListingFeePressureService.Apply(config);
            lootPressureService.Apply(config);

            await groupedItemRuntimeEvidenceService.WriteAsync(enforcement, cancellationToken);
            await runtimeEvidenceService.WriteAfterAsync(vanillaBaseline, questProvenance, enforcement, cancellationToken);
        }
        catch (Exception applyException) when (transactionSnapshot is not null)
        {
            try
            {
                transactionSnapshot.RollbackAndVerify();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Economy Admiral Enforce failed and the complete economy rollback could not be proven.",
                    applyException,
                    rollbackException);
            }

            throw new InvalidOperationException(
                $"Economy Admiral Enforce failed; all captured Quest/Trader/Flea/Loot/native-repeatable mutations were rolled back and verified ({transactionSnapshot.EntryCount} entries).",
                applyException);
        }
    }
}
