using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 999)]
public sealed class QuestRewardHandbookPriceCatalogBootstrap(TemplateTable templates) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        QuestRewardHandbookPriceCatalog.Initialize(
            templates.Handbook.Items
                .Where(item => item.Price is > 0)
                .Select(item => new KeyValuePair<string, double>(item.Id.ToString(), item.Price!.Value)));
        return Task.CompletedTask;
    }
}
