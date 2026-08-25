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
    TypedQuestItemAccountingService typedQuestItemAccountingService,
    RewardUtilityAuditService rewardUtilityAuditService,
    QuestProgressionGraphService questProgressionGraphService,
    QuestConstraintAuditService questConstraintAuditService,
    QuestAnalysisService questAnalysisService,
    BaselineProvenanceCorrectionService baselineProvenanceCorrectionService,
    CompositePolicyEvaluationService compositePolicyEvaluationService,
    TargetProposalService targetProposalService,
    EnforcementPlanService enforcementPlanService
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

        await auditService.RunAsync(cancellationToken);
        await typedQuestItemAccountingService.RepairPrimaryAuditReportAsync(cancellationToken);
        await rewardUtilityAuditService.RunAsync(cancellationToken);
        var progressionSnapshot = await questProgressionGraphService.RunAsync(cancellationToken);
        await questConstraintAuditService.RunAsync(cancellationToken);

        var questAnalysis = await questAnalysisService.RunAsync(progressionSnapshot, cancellationToken);
        questAnalysis = await typedQuestItemAccountingService.ApplyToUnifiedAnalysisAsync(questAnalysis, cancellationToken);
        questAnalysis = await baselineProvenanceCorrectionService.ApplyToUnifiedAnalysisAsync(questAnalysis, vanillaBaseline, cancellationToken);
        await compositePolicyEvaluationService.RunAsync(questAnalysis, cancellationToken);
        await targetProposalService.RunAsync(questAnalysis, cancellationToken);
        await enforcementPlanService.RunAsync(questAnalysis, cancellationToken);

        await runtimeEvidenceService.WriteAfterAsync(cancellationToken);
    }
}
