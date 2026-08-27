using System.Text.Json;

namespace SPTEconomy;

public static class QuestGateJsonParser
{
    public static IReadOnlyList<QuestGateNode> ParseMany(IEnumerable<string> questJsonRecords)
    {
        ArgumentNullException.ThrowIfNull(questJsonRecords);
        var nodes = questJsonRecords.Select(Parse).ToList();
        var duplicate = nodes.GroupBy(node => node.QuestId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Economy Admiral quest-gate parser: duplicate quest id '{duplicate.Key}'.");
        }
        return nodes.OrderBy(node => node.QuestId, StringComparer.Ordinal).ToList();
    }

    public static QuestGateNode Parse(string questJson)
    {
        if (string.IsNullOrWhiteSpace(questJson))
        {
            throw new InvalidOperationException("Economy Admiral quest-gate parser: quest JSON must not be empty.");
        }

        using var document = JsonDocument.Parse(questJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Economy Admiral quest-gate parser: quest root must be an object.");
        }

        var questId = RequireString(root, "_id");
        var conditions = RequireProperty(root, "conditions");
        RequireObject(conditions, $"conditions for quest '{questId}'");
        var availableForStart = RequireProperty(conditions, "AvailableForStart");
        if (availableForStart.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Economy Admiral quest-gate parser: AvailableForStart for quest '{questId}' must be an array.");
        }

        int? levelRequirement = null;
        var prerequisiteIds = new List<string>();

        foreach (var condition in availableForStart.EnumerateArray())
        {
            RequireObject(condition, $"AvailableForStart condition for quest '{questId}'");
            var conditionType = RequireString(condition, "conditionType");
            switch (conditionType)
            {
                case "Level":
                {
                    if (!TryReadPositiveInt(RequireProperty(condition, "value"), out var level))
                    {
                        throw new InvalidOperationException($"Economy Admiral quest-gate parser: invalid Level value for quest '{questId}'.");
                    }
                    levelRequirement = Math.Max(levelRequirement ?? 0, level);
                    break;
                }
                case "Quest":
                {
                    var target = RequireString(condition, "target");
                    var statuses = RequireProperty(condition, "status");
                    if (statuses.ValueKind != JsonValueKind.Array || statuses.GetArrayLength() == 0)
                    {
                        throw new InvalidOperationException($"Economy Admiral quest-gate parser: prerequisite '{target}' for quest '{questId}' has no status evidence.");
                    }
                    var successRequired = statuses.EnumerateArray().Any(value => value.TryGetInt32(out var status) && status == 4);
                    if (!successRequired)
                    {
                        throw new InvalidOperationException($"Economy Admiral quest-gate parser: prerequisite '{target}' for quest '{questId}' is not proven to require successful completion status 4.");
                    }
                    prerequisiteIds.Add(target);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Economy Admiral quest-gate parser: unsupported start condition '{conditionType}' on quest '{questId}'. Progression evidence remains fail-closed until this condition type has explicit semantics.");
            }
        }

        return new QuestGateNode
        {
            QuestId = questId,
            LevelRequirement = levelRequirement,
            PrerequisiteQuestIds = prerequisiteIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        };
    }

    private static JsonElement RequireProperty(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            throw new InvalidOperationException($"Economy Admiral quest-gate parser: required property '{name}' is missing.");
        }
        return value;
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Economy Admiral quest-gate parser: {name} must be an object.");
        }
    }

    private static string RequireString(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Economy Admiral quest-gate parser: '{name}' must be a non-empty string.");
        }
        return value.GetString()!;
    }

    private static bool TryReadPositiveInt(JsonElement value, out int result)
    {
        result = 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result))
        {
            return result > 0;
        }
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out result))
        {
            return result > 0;
        }
        return false;
    }
}
