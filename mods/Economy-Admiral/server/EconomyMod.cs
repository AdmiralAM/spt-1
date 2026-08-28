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
    EconomyHealthRuntimeReportService economyHealthRuntimeReportService
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

        // Observation remains startup-only. It cannot create a new mutation dimension, but the explicit
        // Admiral Trader contract is retained as ownership evidence so Beta enforcement can fail closed
        // for that trader when its maintained contract is absent/incompatible.
        var observation = await sourcePressureObservationPipelineService.RunAsync(config, vanillaBaseline, cancellationToken);
        await economyHealthRuntimeReportService.RunAsync(config, observation.SourcePressure, cancellationToken);

        // Keep detection/audit thresholds untouched. Only the copy handed to Enforce receives the
        // user-authorized Playable Economy v1 reward caps, so Audit remains explanatory while Enforce
        // becomes materially tighter without changing provenance or transaction safety.
        var enforcementAnalysis = PlayableQuestRewardPolicy.ApplyToEnforcement(config, questAnalysis);

        GroupedItemRewardSlot.ResetEvidence();
        var enforcement = await enforcementPlanService.RunAsync(enforcementAnalysis, questProvenance, observation.AdmiralTrader, cancellationToken);
        await groupedItemRuntimeEvidenceService.WriteAsync(enforcement, cancellationToken);
        await runtimeEvidenceService.WriteAfterAsync(vanillaBaseline, questProvenance, enforcement, cancellationToken);
    }
}
