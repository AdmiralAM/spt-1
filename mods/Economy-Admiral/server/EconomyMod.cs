using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTEconomy;

namespace EconomyAdmiral;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1000), UsedImplicitly]
public sealed class EconomyMod(
    EconomyAuditService auditService,
    RewardUtilityAuditService rewardUtilityAuditService
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        await auditService.RunAsync(cancellationToken);
        await rewardUtilityAuditService.RunAsync(cancellationToken);
    }
}
