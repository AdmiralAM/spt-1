using System;
using System.Collections.Generic;
using System.Reflection;

namespace SPTQuestPlanner.Client
{
    public enum PlannerClientDelayState
    {
        NotDelayed = 0,
        PendingKnown = 1,
        ElapsedPendingRefresh = 2,
        TimingUnresolved = 3
    }

    public sealed class PlannerClientQuestDelay
    {
        public PlannerClientQuestDelay(string questId, PlannerClientDelayState state, long? availableAtUnixSeconds, long? remainingSeconds)
        {
            QuestId = questId ?? string.Empty;
            State = state;
            AvailableAtUnixSeconds = availableAtUnixSeconds;
            RemainingSeconds = remainingSeconds;
        }

        public string QuestId { get; private set; }
        public PlannerClientDelayState State { get; private set; }
        public long? AvailableAtUnixSeconds { get; private set; }
        public long? RemainingSeconds { get; private set; }
        public bool BlocksRaidAction { get { return State != PlannerClientDelayState.NotDelayed; } }
        public bool HasKnownRemainingTime { get { return State == PlannerClientDelayState.PendingKnown && RemainingSeconds.HasValue; } }
    }

    public sealed class PlannerClientDelayIndex
    {
        public PlannerClientDelayIndex(long generatedAtUnixSeconds, IReadOnlyDictionary<string, PlannerClientQuestDelay> quests)
        {
            GeneratedAtUnixSeconds = generatedAtUnixSeconds;
            Quests = quests ?? new Dictionary<string, PlannerClientQuestDelay>(StringComparer.Ordinal);
        }

        public long GeneratedAtUnixSeconds { get; private set; }
        public IReadOnlyDictionary<string, PlannerClientQuestDelay> Quests { get; private set; }

        public PlannerClientQuestDelay GetQuest(string questId)
        {
            PlannerClientQuestDelay value;
            return !string.IsNullOrWhiteSpace(questId) && Quests.TryGetValue(questId, out value) ? value : null;
        }
    }

    public static class PlannerClientDelayIndexBuilder
    {
        private const int RawAvailableAfterStatus = 9;

        public static PlannerClientDelayIndex Build(string stateJson)
        {
            if (string.IsNullOrWhiteSpace(stateJson)) throw new ArgumentException("State JSON is missing.", "stateJson");
            Type tokenType = FindType("Newtonsoft.Json.Linq.JToken");
            if (tokenType == null) throw new InvalidOperationException("Newtonsoft JSON runtime is unavailable.");
            MethodInfo parse = tokenType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parse == null) throw new InvalidOperationException("Newtonsoft JToken.Parse is unavailable.");
            object root = parse.Invoke(null, new object[] { stateJson });

            long generated = ReadLong(Get(root, "generatedAtUnixSeconds"), 0L);
            Dictionary<string, PlannerClientQuestDelay> result = new Dictionary<string, PlannerClientQuestDelay>(StringComparer.Ordinal);
            object player = Get(root, "player");
            object questStates = Get(player, "questStates");
            foreach (KeyValuePair<string, object> entry in Properties(questStates))
            {
                object node = entry.Value;
                string questId = ReadString(Get(node, "questId"));
                if (string.IsNullOrWhiteSpace(questId)) questId = entry.Key;
                if (string.IsNullOrWhiteSpace(questId)) continue;

                int rawStatus = ReadInt(Get(node, "rawStatus"), -1);
                long? availableAt = ReadNullableLong(Get(node, "availableAfterUnixSeconds"));
                result[questId] = BuildQuest(questId, rawStatus, availableAt, generated);
            }

            return new PlannerClientDelayIndex(generated, result);
        }

        private static PlannerClientQuestDelay BuildQuest(string questId, int rawStatus, long? availableAt, long generated)
        {
            if (rawStatus != RawAvailableAfterStatus)
                return new PlannerClientQuestDelay(questId, PlannerClientDelayState.NotDelayed, availableAt, null);
            if (!availableAt.HasValue || availableAt.Value <= 0 || generated <= 0)
                return new PlannerClientQuestDelay(questId, PlannerClientDelayState.TimingUnresolved, availableAt, null);

            long remaining = availableAt.Value - generated;
            if (remaining > 0)
                return new PlannerClientQuestDelay(questId, PlannerClientDelayState.PendingKnown, availableAt, remaining);
            return new PlannerClientQuestDelay(questId, PlannerClientDelayState.ElapsedPendingRefresh, availableAt, 0);
        }

        private static object Get(object token, string name)
        {
            if (token == null) return null;
            PropertyInfo stringItem = token.GetType().GetProperty("Item", new[] { typeof(string) });
            if (stringItem != null)
            {
                try { return stringItem.GetValue(token, new object[] { name }); } catch { }
            }
            PropertyInfo objectItem = token.GetType().GetProperty("Item", new[] { typeof(object) });
            if (objectItem != null)
            {
                try { return objectItem.GetValue(token, new object[] { name }); } catch { }
            }
            return null;
        }

        private static IEnumerable<KeyValuePair<string, object>> Properties(object token)
        {
            if (token == null) yield break;
            MethodInfo properties = token.GetType().GetMethod("Properties", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (properties == null) yield break;
            System.Collections.IEnumerable sequence = properties.Invoke(token, null) as System.Collections.IEnumerable;
            if (sequence == null) yield break;
            foreach (object property in sequence)
            {
                if (property == null) continue;
                PropertyInfo name = property.GetType().GetProperty("Name");
                PropertyInfo value = property.GetType().GetProperty("Value");
                string key = name == null ? null : name.GetValue(property, null) as string;
                object item = value == null ? null : value.GetValue(property, null);
                if (!string.IsNullOrWhiteSpace(key)) yield return new KeyValuePair<string, object>(key, item);
            }
        }

        private static string ReadString(object token) { return token == null ? null : token.ToString(); }
        private static int ReadInt(object token, int fallback) { int value; return int.TryParse(ReadString(token), out value) ? value : fallback; }
        private static long ReadLong(object token, long fallback) { long value; return long.TryParse(ReadString(token), out value) ? value : fallback; }
        private static long? ReadNullableLong(object token) { long value; return long.TryParse(ReadString(token), out value) ? value : (long?)null; }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type type = assemblies[i].GetType(fullName, false, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }
    }

    public sealed class PlannerRaidFocusDelayEvidence
    {
        public PlannerRaidFocusDelayEvidence(
            IReadOnlyList<string> pendingKnownQuestIds,
            IReadOnlyList<string> elapsedPendingRefreshQuestIds,
            IReadOnlyList<string> timingUnresolvedQuestIds)
        {
            PendingKnownQuestIds = pendingKnownQuestIds ?? Array.Empty<string>();
            ElapsedPendingRefreshQuestIds = elapsedPendingRefreshQuestIds ?? Array.Empty<string>();
            TimingUnresolvedQuestIds = timingUnresolvedQuestIds ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> PendingKnownQuestIds { get; private set; }
        public IReadOnlyList<string> ElapsedPendingRefreshQuestIds { get; private set; }
        public IReadOnlyList<string> TimingUnresolvedQuestIds { get; private set; }
        public bool HasWaitingBranches { get { return PendingKnownQuestIds.Count + ElapsedPendingRefreshQuestIds.Count + TimingUnresolvedQuestIds.Count > 0; } }
    }

    public static class PlannerRaidFocusDelayEvidenceBuilder
    {
        public static PlannerRaidFocusDelayEvidence Build(PlannerRaidDecisionIntent intent, PlannerClientDelayIndex delays)
        {
            if (intent == null) throw new ArgumentNullException("intent");
            if (delays == null) throw new ArgumentNullException("delays");

            List<string> pending = new List<string>();
            List<string> elapsed = new List<string>();
            List<string> unresolved = new List<string>();
            for (int i = 0; i < intent.FocusFrontierQuestIds.Count; i++)
            {
                string questId = intent.FocusFrontierQuestIds[i];
                PlannerClientQuestDelay delay = delays.GetQuest(questId);
                if (delay == null) continue;
                switch (delay.State)
                {
                    case PlannerClientDelayState.PendingKnown: pending.Add(questId); break;
                    case PlannerClientDelayState.ElapsedPendingRefresh: elapsed.Add(questId); break;
                    case PlannerClientDelayState.TimingUnresolved: unresolved.Add(questId); break;
                }
            }
            pending.Sort(StringComparer.Ordinal);
            elapsed.Sort(StringComparer.Ordinal);
            unresolved.Sort(StringComparer.Ordinal);
            return new PlannerRaidFocusDelayEvidence(pending.ToArray(), elapsed.ToArray(), unresolved.ToArray());
        }
    }
}
