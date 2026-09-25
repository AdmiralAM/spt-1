using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralCompatibilitySuite.Server;

// A small server-side balance component; no client plugin or F12 setting is needed.
[Injectable(TypePriority = OnLoadOrder.PostLoad + 1000)]
public sealed class SuppressorBalance(
    TemplateTable templates,
    ISptLogger<SuppressorBalance> logger) : IOnLoad
{
    private static readonly MongoId SuppressorCategory = new("550aa4cd4bdc2dd8348b456c");

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Do not compound the same adjustment when the standalone donor is installed.
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                string.Equals(assembly.GetName().Name, "BetterSuppressors", StringComparison.OrdinalIgnoreCase)))
        {
            logger.Warning("Standalone BetterSuppressors is loaded; Suite suppressor balance is disabled.");
            return Task.CompletedTask;
        }

        int adjusted = 0;
        foreach (TemplateItem item in templates.Items.Values)
        {
            if (item.Type != "Item" || item.Properties is null || !IsSuppressor(item))
                continue;

            var properties = item.Properties;
            if (properties.DurabilityBurnModificator is double burn && burn > 1d)
                properties.DurabilityBurnModificator = 1d + (burn - 1d) * 0.5d;
            if (properties.Accuracy is double accuracy)
                properties.Accuracy = accuracy + 1.5d;
            if (properties.Velocity is double velocity)
                properties.Velocity = velocity + 2d;
            adjusted++;
        }

        logger.Info($"Admiral suppressor balance adjusted {adjusted} suppressor templates.");
        return Task.CompletedTask;
    }

    private bool IsSuppressor(TemplateItem item)
    {
        var seen = new HashSet<MongoId>();
        MongoId parent = item.Parent;
        while (seen.Add(parent))
        {
            if (parent == SuppressorCategory)
                return true;
            if (!templates.Items.TryGetValue(parent, out TemplateItem? category))
                break;
            parent = category.Parent;
        }

        return false;
    }
}
