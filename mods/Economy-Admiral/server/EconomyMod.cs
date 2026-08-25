using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTEconomy;

namespace EconomyAdmiral;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1000), UsedImplicitly]
public sealed class EconomyMod(
    EconomyAuditService auditService,
    RewardUtilityAuditService rewardUtilityAuditService,
    QuestProgressionGraphService questProgressionGraphService,
    QuestConstraintAuditService questConstraintAuditService,
    QuestAnalysisService questAnalysisService,
    EnforcementPlanService enforcementPlanService
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        await auditService.RunAsync(cancellationToken);
        await rewardUtilityAuditService.RunAsync(cancellationToken);
        await questProgressionGraphService.RunAsync(cancellationToken);
        await questConstraintAuditService.RunAsync(cancellationToken);
        await questAnalysisService.RunAsync(cancellationToken);
        await enforcementPlanService.RunAsync(cancellationToken);
    }
}
