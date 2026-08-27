using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

/// <summary>
/// Resolves authored access-key category targets to concrete installed-SPT item
/// templates. EFT FindItem conditions compare concrete template ids; publishing
/// category/base-class ids directly leaves valid keys permanently at 0/N.
/// The authored quest ids, source target ids, counts and progression remain
/// unchanged; only the runtime target set is materialized from the installed DB.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 6), UsedImplicitly]
public sealed class AccessQuestRuntimeTargets(
    ModHelper modHelper,
    TemplateTable templateTable,
    ItemHelper itemHelper,
    ISptLogger<AccessQuestRuntimeTargets> logger) : IOnLoad
{
    private const int ExpectedAccessQuestCount = 10;

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

            string questIdText = root.GetProperty("_id").GetString()
                ?? throw new InvalidDataException($"Access quest has no _id: {IOPath.GetFileName(file)}");
            MongoId questId = new(questIdText);

            JsonElement finish = root.GetProperty("conditions").GetProperty("AvailableForFinish")[0];
            if (!string.Equals(finish.GetProperty("conditionType").GetString(), "FindItem", StringComparison.Ordinal))
                throw new InvalidDataException($"Access quest {questId} must remain FindItem for runtime key-target resolution");

            List<string> authoredTargets = finish.GetProperty("target")
                .EnumerateArray()
                .Select(element => element.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (authoredTargets.Count == 0)
                throw new InvalidDataException($"Access quest {questId} has no authored key target categories");

            List<MongoId> authoredTargetIds = authoredTargets.Select(value => new MongoId(value)).ToList();
            List<string> concreteTargets = templateTable.Items
                .Where(entry =>
                    string.Equals(entry.Value.Type, "Item", StringComparison.OrdinalIgnoreCase)
                    && (authoredTargetIds.Contains(entry.Key) || itemHelper.IsOfBaseclasses(entry.Key, authoredTargetIds)))
                .Select(entry => entry.Key.ToString())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            if (concreteTargets.Count == 0)
                throw new InvalidDataException(
                    $"Access quest {questId} resolved zero concrete key templates from authored targets [{string.Join(", ", authoredTargets)}]");

            if (!templateTable.Quests.TryGetValue(questId, out var registeredQuest))
                throw new InvalidDataException($"Registered Admiral access quest {questId} is missing before runtime target resolution");
            if (registeredQuest.Conditions.AvailableForFinish is not { Count: 1 } finishConditions)
                throw new InvalidDataException($"Registered Admiral access quest {questId} must have exactly one finish condition");

            SetConcreteTargets(finishConditions[0], concreteTargets);
            resolvedQuestCount++;
        }

        if (resolvedQuestCount != ExpectedAccessQuestCount)
            throw new InvalidDataException(
                $"Expected {ExpectedAccessQuestCount} Admiral access quests for runtime target resolution, got {resolvedQuestCount}");

        logger.Success($"Resolved concrete installed-SPT key targets for {resolvedQuestCount} Admiral access quests");
        return Task.CompletedTask;
    }

    private static void SetConcreteTargets(QuestCondition condition, List<string> concreteTargets)
    {
        PropertyInfo targetProperty = typeof(QuestCondition).GetProperty(nameof(QuestCondition.Target))
            ?? throw new MissingMemberException(typeof(QuestCondition).FullName, nameof(QuestCondition.Target));
        object runtimeTarget = Activator.CreateInstance(
                targetProperty.PropertyType,
                [concreteTargets, null])
            ?? throw new InvalidOperationException("Unable to construct SPT quest target wrapper");
        targetProperty.SetValue(condition, runtimeTarget);
    }
}
