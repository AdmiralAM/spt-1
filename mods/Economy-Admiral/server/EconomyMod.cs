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
    EnforcementPlanService enforcementPlanService
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        if (config.Mode == EconomyMode.Off)
            return;

        var vanillaBaseline = vanillaBaselineService.GetSnapshot();
        runtimeEvidenceService.CaptureBefore();

        // Core reports read typed final DB state directly against the immutable pristine snapshot.
        // Accepted proof/correction layers and future cross-mod source-pressure adapters are not on startup path.
        await auditService.RunAsync(vanillaBaseline, cancellationToken);
        await rewardUtilityAuditService.RunAsync(vanillaBaseline, cancellationToken);
        var progressionSnapshot = await questProgressionGraphService.RunAsync(vanillaBaseline, cancellationToken);
        await questConstraintAuditService.RunAsync(vanillaBaseline, cancellationToken);

        var questAnalysis = await questAnalysisService.RunAsync(progressionSnapshot, vanillaBaseline, cancellationToken);
        var questProvenance = await questProvenanceDeltaService.RunAsync(vanillaBaseline, questAnalysis, cancellationToken);
        var enforcement = await enforcementPlanService.RunAsync(questAnalysis, questProvenance, cancellationToken);

        await runtimeEvidenceService.WriteAfterAsync(vanillaBaseline, questProvenance, enforcement, cancellationToken);
    }
}
