using System;
using System.Collections.Generic;

namespace SPTItemIntelligence
{
    // Quest-only normalization based on the established AllQuestsCheckmarks semantics.
    // Owned inventory and hideout projection stay delegated to the existing SPT projector.
    public sealed class AqcQuestRequirementProjector : IRequirementDataProjector
    {
        readonly SptRequirementDataProjector inner;

        public AqcQuestRequirementProjector(Action<string> trace = null)
        {
            inner = new SptRequirementDataProjector(trace);
        }

        public RequirementProjection Project(RequirementDataEnvelope snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            RequirementProjection baseline = inner.Project(snapshot);
            List<RequirementContribution> normalized = new List<RequirementContribution>();

            // Keep hideout contributions from the proven projector, but rebuild all quest
            // contributions with AQC-style filtering/deduplication.
            for (int i = 0; i < baseline.Contributions.Count; i++)
            {
                RequirementContribution contribution = baseline.Contributions[i];
                if (contribution.Source == RequirementSource.Hideout) normalized.Add(contribution);
            }

            ProjectQuests(snapshot.profile, snapshot.quests, normalized);
            return new RequirementProjection(snapshot.generatedAtUnixSeconds, baseline.Owned, normalized);
        }

        static void ProjectQuests(object profile, object questTable, List<RequirementContribution> output)
        {
            Dictionary<string, QuestProgress> progress = ReadProgress(profile);

            foreach (KeyValuePair<string, object> pair in JsonNode.Pairs(questTable))
            {
                object quest = pair.Value;
                string questId = JsonNode.ReadString(JsonNode.Get(quest, "_id", "id", "Id"));
                if (string.IsNullOrWhiteSpace(questId)) questId = pair.Key;
                if (string.IsNullOrWhiteSpace(questId)) continue;
                questId = questId.Trim();

                QuestProgress state;
                progress.TryGetValue(questId, out state);
                if (state != null && state.IsComplete) continue;

                string questLabel = JsonNode.ReadString(JsonNode.Get(quest, "QuestName", "questName", "name", "Name")).Trim();
                if (questLabel.Length == 0) questLabel = "Quest " + questId;
                RequirementSource source = state != null && state.IsCurrent
                    ? RequirementSource.CurrentQuest
                    : RequirementSource.FutureQuest;

                object conditions = JsonNode.Get(JsonNode.Get(quest, "conditions", "Conditions"), "AvailableForFinish", "availableForFinish");
                List<QuestCondition> parsed = ParseConditions(conditions);
                Dictionary<string, SelectedRequirement> selected = new Dictionary<string, SelectedRequirement>(StringComparer.Ordinal);

                for (int i = 0; i < parsed.Count; i++)
                {
                    QuestCondition condition = parsed[i];
                    if (state != null && state.IsConditionComplete(condition.Id)) continue;
                    if (!IsSupported(condition.Kind)) continue;

                    for (int targetIndex = 0; targetIndex < condition.Targets.Count; targetIndex++)
                    {
                        string target = condition.Targets[targetIndex];
                        if (target.Length == 0 || condition.Count <= 0) continue;

                        if (condition.Kind == "leaveitematlocation")
                        {
                            // AQC suppresses LeaveItemAtLocation when the same item is already
                            // represented by a FindItem/HandoverItem condition in that quest.
                            if (HasFindOrHandoverTarget(parsed, target)) continue;

                            SelectedRequirement existingLeave;
                            if (selected.TryGetValue(target, out existingLeave))
                            {
                                if (existingLeave.Kind == "leaveitematlocation") existingLeave.Count += condition.Count;
                                continue;
                            }

                            selected[target] = new SelectedRequirement(condition.Kind, condition.Count, condition.FoundInRaid);
                            continue;
                        }

                        // AQC stores one non-leave item requirement per quest/item and does not
                        // count Find + Handover as two independent requirements for the same item.
                        if (!selected.ContainsKey(target))
                            selected[target] = new SelectedRequirement(condition.Kind, condition.Count, condition.FoundInRaid);
                    }
                }

                foreach (KeyValuePair<string, SelectedRequirement> requirement in selected)
                {
                    output.Add(new RequirementContribution(
                        requirement.Key,
                        source,
                        requirement.Value.Count,
                        0,
                        requirement.Value.FoundInRaid,
                        label: questLabel));
                }
            }
        }

        static Dictionary<string, QuestProgress> ReadProgress(object profile)
        {
            Dictionary<string, QuestProgress> result = new Dictionary<string, QuestProgress>(StringComparer.OrdinalIgnoreCase);
            foreach (object quest in JsonNode.Values(JsonNode.Get(profile, "Quests", "quests")))
            {
                string id = JsonNode.ReadString(JsonNode.Get(quest, "qid", "QID", "questId", "_id"));
                if (string.IsNullOrWhiteSpace(id)) continue;

                List<string> completed = new List<string>();
                foreach (object conditionId in JsonNode.Values(JsonNode.Get(quest, "completedConditions", "CompletedConditions")))
                {
                    string normalized = JsonNode.ReadString(conditionId).Trim();
                    if (normalized.Length > 0) completed.Add(normalized);
                }

                result[id.Trim()] = new QuestProgress(
                    JsonNode.ReadString(JsonNode.Get(quest, "status", "Status")),
                    completed);
            }
            return result;
        }

        static List<QuestCondition> ParseConditions(object conditions)
        {
            List<QuestCondition> result = new List<QuestCondition>();
            foreach (object condition in JsonNode.Values(conditions))
            {
                string kind = JsonNode.ReadString(JsonNode.Get(condition, "conditionType", "ConditionType")).Trim().ToLowerInvariant();
                string id = JsonNode.ReadString(JsonNode.Get(condition, "id", "_id", "Id")).Trim();
                int count = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(condition, "value", "Value"), 0));
                bool fir = JsonNode.ReadBool(JsonNode.Get(condition, "onlyFoundInRaid", "OnlyFoundInRaid"), false);
                List<string> targets = new List<string>();
                foreach (object target in JsonNode.ValuesOrSelf(JsonNode.Get(condition, "target", "Target")))
                {
                    string normalized = RequirementContribution.NormalizeId(JsonNode.ReadString(target));
                    if (normalized.Length > 0) targets.Add(normalized);
                }
                result.Add(new QuestCondition(id, kind, count, fir, targets));
            }
            return result;
        }

        static bool IsSupported(string kind)
        {
            return kind == "handoveritem" || kind == "finditem" || kind == "leaveitematlocation";
        }

        static bool HasFindOrHandoverTarget(List<QuestCondition> conditions, string target)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                QuestCondition condition = conditions[i];
                if (condition.Kind != "handoveritem" && condition.Kind != "finditem") continue;
                if (condition.Targets.Contains(target)) return true;
            }
            return false;
        }

        sealed class SelectedRequirement
        {
            public SelectedRequirement(string kind, int count, bool foundInRaid)
            {
                Kind = kind ?? string.Empty;
                Count = Math.Max(0, count);
                FoundInRaid = foundInRaid;
            }
            public string Kind { get; }
            public int Count { get; set; }
            public bool FoundInRaid { get; }
        }

        sealed class QuestCondition
        {
            public QuestCondition(string id, string kind, int count, bool foundInRaid, List<string> targets)
            {
                Id = id ?? string.Empty;
                Kind = kind ?? string.Empty;
                Count = count;
                FoundInRaid = foundInRaid;
                Targets = targets ?? new List<string>();
            }
            public string Id { get; }
            public string Kind { get; }
            public int Count { get; }
            public bool FoundInRaid { get; }
            public List<string> Targets { get; }
        }

        sealed class QuestProgress
        {
            readonly string status;
            readonly HashSet<string> completedConditions;

            public QuestProgress(string status, IEnumerable<string> completedConditions)
            {
                this.status = (status ?? string.Empty).Trim().ToLowerInvariant();
                this.completedConditions = new HashSet<string>(completedConditions ?? new string[0], StringComparer.OrdinalIgnoreCase);
            }

            public bool IsCurrent => status == "started" || status == "availableforfinish" || status == "2" || status == "3";
            public bool IsComplete => status == "success" || status == "fail" || status == "failed" || status == "4" || status == "5" || status == "7" || status == "8";
            public bool IsConditionComplete(string conditionId) => !string.IsNullOrEmpty(conditionId) && completedConditions.Contains(conditionId);
        }
    }
}
