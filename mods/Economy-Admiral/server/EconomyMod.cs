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
    GroupedItemRuntimeEvidenceService groupedItemRuntimeEvidenceService,
    SourcePressureObservationPipelineService sourcePressureObservationPipelineService,
    EconomyHealthRuntimeReportService economyHealthRuntimeReportService,
    TraderPurchasePressureService traderPurchasePressureService,
    FleaPurchasePressureService fleaPurchasePressureService
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

        GroupedItemRewardSlot.ResetEvidence();
        var enforcement = await enforcementPlanService.RunAsync(questAnalysis, questProvenance, observation.AdmiralTrader, cancellationToken);

        traderPurchasePressureService.Apply(config);
        fleaPurchasePressureService.Apply(config);

        await groupedItemRuntimeEvidenceService.WriteAsync(enforcement, cancellationToken);
        await runtimeEvidenceService.WriteAfterAsync(vanillaBaseline, questProvenance, enforcement, cancellationToken);
    }
}
