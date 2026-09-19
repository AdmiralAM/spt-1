using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1), UsedImplicitly]
public sealed class RewardBundlePreflight(
    ModHelper modHelper,
    TemplateTable templateTable,
    ISptLogger<RewardBundlePreflight> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RewardBundlePolicy policy = modHelper.GetJsonDataFromFile<RewardBundlePolicy>(modPath, "manifests/reward-bundle-policy.json");
        RewardBundleEngine.ValidatePolicy(policy);

        HashSet<string> available = templateTable.Items.Keys.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        int admitted = policy.Catalog.Count(item => available.Contains(item.TemplateId) && item.RequiredTemplates.All(available.Contains));
        int optionalMissing = policy.Catalog.Count - admitted;

        if (policy.Enabled)
        {
            foreach (RewardBundleSpec spec in policy.Bundles)
                RewardBundleEngine.Generate(policy, spec, available);
            logger.Success($"Admiral reward bundle preflight passed: {policy.Bundles.Count} specifications, {admitted} catalogue entries available, {optionalMissing} unavailable entries omitted");
        }
        else
        {
            logger.Info($"Admiral reward bundle engine loaded in non-publishing mode: {admitted} catalogue entries available, {optionalMissing} unavailable entries omitted");
        }
        return Task.CompletedTask;
    }
}
