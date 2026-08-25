using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace EconomyAdmiral;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1000), UsedImplicitly]
public sealed class EconomyMod(EconomyAuditService auditService) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        return auditService.RunAsync(cancellationToken);
    }
}
