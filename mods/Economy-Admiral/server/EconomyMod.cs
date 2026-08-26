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
    PrimaryAuditParityService primaryAuditParityService,
    RewardUtilityAuditService rewardUtilityAuditService,
    QuestProgressionGraphService questProgressionGraphService,
    QuestConstraintAuditService questConstraintAuditService,
    QuestAnalysisService questAnalysisService,
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
            return;

        var vanillaBaseline = vanillaBaselineService.GetSnapshot();
        runtimeEvidenceService.CaptureBefore();

        // Core typed reports use final DB state directly against the immutable pristine startup snapshot.
        await auditService.RunAsync(vanillaBaseline, cancellationToken);
        await primaryAuditParityService.RunAsync(cancellationToken);
        await rewardUtilityAuditService.RunAsync(vanillaBaseline, cancellationToken);

        var progressionSnapshot = await questProgressionGraphService.RunAsync(vanillaBaseline, cancellationToken);

        await questConstraintAuditService.RunAsync(cancellationToken);
        await pristineReportCorrectionService.CorrectConstraintsAsync(vanillaBaseline, cancellationToken);

        var questAnalysis = await questAnalysisService.RunAsync(progressionSnapshot, vanillaBaseline, cancellationToken);
        var questProvenance = await questProvenanceDeltaService.RunAsync(vanillaBaseline, questAnalysis, cancellationToken);

        var admiralTraderReport = await admiralTraderRuntimeAdapterService.RunAsync(config, cancellationToken);
        await sourcePressureRuntimeReportService.RunAsync(config, admiralTraderReport, cancellationToken);

        await compositePolicyEvaluationService.RunAsync(questAnalysis, cancellationToken);
        await targetProposalService.RunAsync(questAnalysis, cancellationToken);
        var enforcement = await enforcementPlanService.RunAsync(questAnalysis, questProvenance, cancellationToken);

        await runtimeEvidenceService.WriteAfterAsync(vanillaBaseline, questProvenance, enforcement, cancellationToken);
    }
}
