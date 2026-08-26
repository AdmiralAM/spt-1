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
    PristineReportCorrectionService pristineReportCorrectionService,
    TypedQuestItemAccountingService typedQuestItemAccountingService,
    PrimaryAuditParityService primaryAuditParityService,
    RewardUtilityAuditService rewardUtilityAuditService,
    QuestProgressionGraphService questProgressionGraphService,
    QuestConstraintAuditService questConstraintAuditService,
    QuestAnalysisService questAnalysisService,
    BaselineProvenanceCorrectionService baselineProvenanceCorrectionService,
    QuestProvenanceDeltaService questProvenanceDeltaService,
    CompositePolicyEvaluationService compositePolicyEvaluationService,
    TargetProposalService targetProposalService,
    EnforcementPlanService enforcementPlanService,
    AdmiralTraderRuntimeAdapterService admiralTraderRuntimeAdapterService,
    SourcePressureRuntimeReportService sourcePressureRuntimeReportService
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        if (config.Mode == EconomyMode.Off)
        {
            return;
        }

        var vanillaBaseline = vanillaBaselineService.GetSnapshot();
        runtimeEvidenceService.CaptureBefore();

        // Physical SPT 4.1.3 parity proved that typed final DB + pristine startup snapshot
        // is source-correct. The primary report is now built directly from those sources.
        await auditService.RunAsync(vanillaBaseline, cancellationToken);
        await primaryAuditParityService.RunAsync(cancellationToken);

        await rewardUtilityAuditService.RunAsync(cancellationToken);
        await pristineReportCorrectionService.CorrectRewardUtilityAsync(vanillaBaseline, cancellationToken);

        var progressionSnapshot = await questProgressionGraphService.RunAsync(cancellationToken);
        await pristineReportCorrectionService.CorrectProgressionGraphAsync(vanillaBaseline, cancellationToken);

        await questConstraintAuditService.RunAsync(cancellationToken);
        await pristineReportCorrectionService.CorrectConstraintsAsync(vanillaBaseline, cancellationToken);

        var questAnalysis = await questAnalysisService.RunAsync(progressionSnapshot, cancellationToken);
        questAnalysis = await typedQuestItemAccountingService.ApplyToUnifiedAnalysisAsync(questAnalysis, cancellationToken);
        questAnalysis = await baselineProvenanceCorrectionService.ApplyToUnifiedAnalysisAsync(questAnalysis, vanillaBaseline, cancellationToken);
        var questProvenance = await questProvenanceDeltaService.RunAsync(vanillaBaseline, questAnalysis, cancellationToken);

        // Cross-mod integration remains observational and is no longer on the critical path for Enforce policy.
        var admiralTraderReport = await admiralTraderRuntimeAdapterService.RunAsync(config, cancellationToken);
        await sourcePressureRuntimeReportService.RunAsync(config, admiralTraderReport, cancellationToken);

        // Legacy observational reports remain available during Alpha, but only the enforcement service below may mutate DB state.
        await compositePolicyEvaluationService.RunAsync(questAnalysis, cancellationToken);
        await targetProposalService.RunAsync(questAnalysis, cancellationToken);
        var enforcement = await enforcementPlanService.RunAsync(questAnalysis, questProvenance, cancellationToken);

        await runtimeEvidenceService.WriteAfterAsync(vanillaBaseline, questProvenance, enforcement, cancellationToken);
    }
}