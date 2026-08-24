using SPTQuestPlanner;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace SPTQuestPlanner.Server;

[Injectable]
public sealed class PlannerLocaleProjectionService(
    JsonUtil jsonUtil,
    LocaleService localeService,
    PlannerStaticDataCache staticDataCache)
{
    public ValueTask<string> BuildAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PlannerStaticData staticData = staticDataCache.Get();
        Dictionary<string, string> localeDb = localeService.GetLocaleDb();
        string locale = localeService.GetDesiredGameLocale();

        Dictionary<string, string> questNames = new(StringComparer.Ordinal);
        foreach (QuestNode quest in staticData.Extraction.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = quest.QuestId + " name";
            if (localeDb.TryGetValue(key, out string? name) && !string.IsNullOrWhiteSpace(name))
                questNames[quest.QuestId] = name.Trim();
        }

        HashSet<string> templateIds = new(StringComparer.Ordinal);
        foreach (ItemRequirement requirement in staticData.Extraction.ItemRequirements)
        {
            foreach (string templateId in requirement.TemplateIds)
                if (!string.IsNullOrWhiteSpace(templateId)) templateIds.Add(templateId);
        }
        foreach (QuestObjectiveFact objective in staticData.ObjectiveExtraction.Objectives)
        {
            foreach (string target in objective.Targets)
                if (!string.IsNullOrWhiteSpace(target)) templateIds.Add(target);
        }

        Dictionary<string, string> itemNames = new(StringComparer.Ordinal);
        foreach (string templateId in templateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = templateId + " Name";
            if (localeDb.TryGetValue(key, out string? name) && !string.IsNullOrWhiteSpace(name))
                itemNames[templateId] = name.Trim();
        }

        PlannerLocaleEnvelope envelope = new(
            PlannerDataContract.SchemaVersion,
            locale,
            questNames,
            itemNames);
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class QuestPlannerLocaleRouter(JsonUtil jsonUtil, PlannerLocaleProjectionService localeService)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction(
                PlannerDataContract.LocaleRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await localeService.BuildAsync(cancellationToken))
        ])
{ }
