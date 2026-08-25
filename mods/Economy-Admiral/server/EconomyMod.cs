using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTEconomy;

namespace EconomyAdmiral;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1000), UsedImplicitly]
public sealed class EconomyMod(
    EconomyRuntimeConfigService runtimeConfigService,
    EconomyAuditService auditService,
    RewardUtilityAuditService rewardUtilityAuditService,
    QuestProgressionGraphService questProgressionGraphService,
    QuestConstraintAuditService questConstraintAuditService,
    QuestAnalysisService questAnalysisService,
    CompositePolicyEvaluationService compositePolicyEvaluationService,
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

        await auditService.RunAsync(cancellationToken);
        await rewardUtilityAuditService.RunAsync(cancellationToken);
        await questProgressionGraphService.RunAsync(cancellationToken);
        await questConstraintAuditService.RunAsync(cancellationToken);
        await questAnalysisService.RunAsync(cancellationToken);
        await compositePolicyEvaluationService.RunAsync(cancellationToken);
        await enforcementPlanService.RunAsync(cancellationToken);
    }
}
