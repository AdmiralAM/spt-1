using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 6), UsedImplicitly]
public sealed class AccessQuestRuntimeTargets(
    ModHelper modHelper,
    TemplateTable templateTable,
    ItemHelper itemHelper,
    ISptLogger<AccessQuestRuntimeTargets> logger) : IOnLoad
{
    private const int ExpectedResolvedAccessQuestCount = 9;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        RuntimeRegistrationManifest runtimeManifest = AdmiralTraderRegistration.LoadRuntimeManifest(modPath);
        if (!runtimeManifest.RegistrationEnabled)
        {
            logger.Info("Admiral access-key runtime target resolution is disabled with runtime registration");
            return Task.CompletedTask;
        }

        string questDirectory = IOPath.Combine(modPath, "db", "quests");
        if (!Directory.Exists(questDirectory))
            throw new DirectoryNotFoundException($"Admiral quest directory is missing: {questDirectory}");

        int resolvedQuestCount = 0;
        foreach (string file in Directory.GetFiles(questDirectory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            JsonElement root = document.RootElement;
            string questName = root.GetProperty("QuestName").GetString() ?? string.Empty;
            if (questName.StartsWith("Arsenal Protocol:", StringComparison.Ordinal))
                continue;

            string questIdText = root.GetProperty("_id").GetString() ?? throw new InvalidDataException($"Access quest has no _id: {IOPath.GetFileName(file)}");
            MongoId questId = new(questIdText);
            JsonElement finish = root.GetProperty("conditions").GetProperty("AvailableForFinish")[0];
            string conditionType = finish.GetProperty("conditionType").GetString() ?? string.Empty;

            if (string.Equals(conditionType, "HandoverItem", StringComparison.Ordinal))
                continue;
            if (!string.Equals(conditionType, "FindItem", StringComparison.Ordinal))
                throw new InvalidDataException($"Access quest {questId} has unsupported runtime key condition {conditionType}");

            List<string> authoredTargets = finish.GetProperty("target").EnumerateArray().Select(element => element.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Distinct(StringComparer.Ordinal).ToList();
            if (authoredTargets.Count == 0)
                throw new InvalidDataException($"Access quest {questId} has no authored key target categories");

            List<MongoId> authoredTargetIds = authoredTargets.Select(value => new MongoId(value)).ToList();
            List<string> concreteTargets = templateTable.Items
                .Where(entry => string.Equals(entry.Value.Type, "Item", StringComparison.OrdinalIgnoreCase) && (authoredTargetIds.Contains(entry.Key) || itemHelper.IsOfBaseclasses(entry.Key, authoredTargetIds)))
                .Select(entry => entry.Key.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            if (concreteTargets.Count == 0)
                throw new InvalidDataException($"Access quest {questId} resolved zero concrete key templates from authored targets [{string.Join(", ", authoredTargets)}]");

            if (!templateTable.Quests.TryGetValue(questId, out var registeredQuest))
                throw new InvalidDataException($"Registered Admiral access quest {questId} is missing before runtime target resolution");
            if (registeredQuest.Conditions.AvailableForFinish is not { Count: 1 } finishConditions)
                throw new InvalidDataException($"Registered Admiral access quest {questId} must have exactly one finish condition");

            finishConditions[0].Target = new ListOrT<string>(concreteTargets, null!);
            resolvedQuestCount++;
        }

        if (resolvedQuestCount != ExpectedResolvedAccessQuestCount)
            throw new InvalidDataException($"Expected {ExpectedResolvedAccessQuestCount} Admiral access quests for runtime target resolution, got {resolvedQuestCount}");

        logger.Success($"Resolved concrete installed-SPT key targets for {resolvedQuestCount} Admiral access quests; explicit handover targets preserved");
        return Task.CompletedTask;
    }
}
