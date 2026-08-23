using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;

namespace SPTItemIntelligence
{
    public interface IRequirementSnapshotTransport
    {
        string GetSnapshotJson();
    }

    public interface IRequirementSnapshotDecoder
    {
        RequirementDataEnvelope Decode(string json);
    }

    public sealed class ReflectionSptSnapshotTransport : IRequirementSnapshotTransport
    {
        public string GetSnapshotJson()
        {
            Type requestHandler = FindType("SPT.Common.Http.RequestHandler");
            if (requestHandler == null) throw new InvalidOperationException("SPT RequestHandler is unavailable.");

            MethodInfo getJson = null;
            MethodInfo[] methods = requestHandler.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo candidate = methods[i];
                ParameterInfo[] parameters = candidate.GetParameters();
                if (candidate.Name == "GetJson" && candidate.ReturnType == typeof(string) &&
                    parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    getJson = candidate;
                    break;
                }
            }
            if (getJson == null) throw new InvalidOperationException("SPT RequestHandler.GetJson(string) is unavailable.");

            object response = getJson.Invoke(null, new object[] { RequirementDataContract.SnapshotRoute });
            string json = response as string;
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "null", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Requirement snapshot response is empty.");
            return json;
        }

        static Type FindType(string fullName)
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

    public sealed class ReflectionNewtonsoftSnapshotDecoder : IRequirementSnapshotDecoder
    {
        readonly Action<string> trace;

        public ReflectionNewtonsoftSnapshotDecoder(Action<string> trace = null)
        {
            this.trace = trace;
        }

        public RequirementDataEnvelope Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Snapshot JSON is missing.", nameof(json));
            Trace("payload chars=" + json.Length + " bulbexHits=" + Count(json, RequirementDataContract.RuntimeTraceTemplateId));
            Type tokenType = FindType("Newtonsoft.Json.Linq.JToken");
            if (tokenType == null) throw new InvalidOperationException("Newtonsoft JSON runtime is unavailable.");
            MethodInfo parse = tokenType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parse == null) throw new InvalidOperationException("Newtonsoft JToken.Parse is unavailable.");

            object root = parse.Invoke(null, new object[] { json });
            int schemaVersion = JsonNode.ReadInt(JsonNode.Get(root, "schemaVersion"), 0);
            if (schemaVersion != RequirementDataContract.SchemaVersion)
                throw new InvalidOperationException("Unsupported requirement snapshot schema " + schemaVersion + ".");

            object profile = JsonNode.Get(root, "profile");
            if (!JsonNode.ReadBool(JsonNode.Get(root, "profileReady"), !JsonNode.IsNull(profile))) profile = null;
            object quests = JsonNode.Get(root, "quests");
            object hideout = JsonNode.Get(root, "hideout");
            object prices = JsonNode.Get(root, "prices");
            if (JsonNode.IsNull(quests) || JsonNode.IsNull(hideout) || JsonNode.IsNull(prices))
                throw new InvalidOperationException("Requirement snapshot tables are incomplete.");

            long generated = JsonNode.ReadLong(JsonNode.Get(root, "generatedAtUnixSeconds"), 0);
            Trace("decoder profileReady=" + (!JsonNode.IsNull(profile)) + " quests=" + CountValues(quests) + " hideoutAreas=" + CountValues(JsonNode.Get(hideout, "areas", "Areas")));
            return new RequirementDataEnvelope(generated, profile, quests, hideout, prices);
        }

        void Trace(string message)
        {
            if (trace != null) trace("[II TRACE] client " + message);
        }

        static int Count(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value)) return 0;
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        static int CountValues(object source)
        {
            int count = 0;
            foreach (object ignored in JsonNode.Values(source)) count++;
            return count;
        }

        static Type FindType(string fullName)
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

    public interface IPriceDataProjector
    {
        ItemPriceIndex Project(object prices);
    }

    public sealed class SptPriceDataProjector : IPriceDataProjector
    {
        public ItemPriceIndex Project(object prices)
        {
            List<ItemPriceInput> inputs = new List<ItemPriceInput>();
            foreach (object entry in JsonNode.Values(prices))
            {
                string templateId = RequirementContribution.NormalizeId(JsonNode.ReadString(JsonNode.Get(entry, "templateId", "TemplateId")));
                if (templateId.Length == 0) continue;
                inputs.Add(new ItemPriceInput(
                    templateId,
                    JsonNode.ReadLong(JsonNode.Get(entry, "traderUnitValue", "TraderUnitValue"), 0),
                    JsonNode.ReadString(JsonNode.Get(entry, "traderName", "TraderName")),
                    JsonNode.ReadLong(JsonNode.Get(entry, "fleaUnitValue", "FleaUnitValue"), 0),
                    JsonNode.ReadLong(JsonNode.Get(entry, "fallbackUnitValue", "FallbackUnitValue"), 0),
                    JsonNode.ReadInt(JsonNode.Get(entry, "width", "Width"), 1),
                    JsonNode.ReadInt(JsonNode.Get(entry, "height", "Height"), 1)));
            }
            return ItemPriceIndexBuilder.Build(inputs);
        }
    }

    public sealed class SptRequirementDataProjector : IRequirementDataProjector
    {
        readonly Action<string> trace;

        public SptRequirementDataProjector(Action<string> trace = null)
        {
            this.trace = trace;
        }

        public RequirementProjection Project(RequirementDataEnvelope snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.profileReady) throw new InvalidOperationException("Profile is not ready.");

            List<OwnedTemplateCount> owned = ProjectOwned(snapshot.profile);
            List<RequirementContribution> contributions = new List<RequirementContribution>();
            ProjectQuests(snapshot.profile, snapshot.quests, contributions);
            ProjectHideout(snapshot.profile, snapshot.hideout, contributions);
            int ownedBulbex = 0;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].TemplateId == RequirementDataContract.RuntimeTraceTemplateId) ownedBulbex += owned[i].Count;
            int bulbexContributions = 0;
            for (int i = 0; i < contributions.Count; i++)
                if (contributions[i].TemplateId == RequirementDataContract.RuntimeTraceTemplateId) bulbexContributions++;
            Trace("projector ownedBulbex=" + ownedBulbex + " bulbexContributions=" + bulbexContributions + " totalContributions=" + contributions.Count);
            return new RequirementProjection(snapshot.generatedAtUnixSeconds, owned, contributions);
        }

        static List<OwnedTemplateCount> ProjectOwned(object profile)
        {
            Dictionary<string, int> totals = new Dictionary<string, int>(StringComparer.Ordinal);
            object inventory = JsonNode.Get(profile, "Inventory", "inventory");
            object items = JsonNode.Get(inventory, "items", "Items");
            foreach (object item in JsonNode.Values(items))
            {
                string templateId = RequirementContribution.NormalizeId(JsonNode.ReadString(JsonNode.Get(item, "_tpl", "tpl", "TemplateId")));
                if (templateId.Length == 0) continue;
                object upd = JsonNode.Get(item, "upd", "Upd");
                int count = Math.Max(1, JsonNode.ReadInt(JsonNode.Get(upd, "StackObjectsCount", "stackObjectsCount"), 1));
                int current;
                totals.TryGetValue(templateId, out current);
                totals[templateId] = current + count;
            }

            List<OwnedTemplateCount> result = new List<OwnedTemplateCount>(totals.Count);
            foreach (KeyValuePair<string, int> pair in totals) result.Add(new OwnedTemplateCount(pair.Key, pair.Value));
            return result;
        }

        static void ProjectQuests(object profile, object questTable, List<RequirementContribution> output)
        {
            Dictionary<string, QuestProgress> progress = new Dictionary<string, QuestProgress>(StringComparer.OrdinalIgnoreCase);
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
                progress[id.Trim()] = new QuestProgress(JsonNode.ReadString(JsonNode.Get(quest, "status", "Status")), completed);
            }

            foreach (KeyValuePair<string, object> pair in JsonNode.Pairs(questTable))
            {
                object quest = pair.Value;
                string questId = JsonNode.ReadString(JsonNode.Get(quest, "_id", "id", "Id"));
                if (string.IsNullOrWhiteSpace(questId)) questId = pair.Key;
                if (string.IsNullOrWhiteSpace(questId)) continue;
                string questLabel = JsonNode.ReadString(JsonNode.Get(quest, "QuestName", "questName", "name", "Name")).Trim();
                if (questLabel.Length == 0) questLabel = "Quest " + questId;

                QuestProgress state;
                progress.TryGetValue(questId, out state);
                if (state != null && state.IsComplete) continue;
                RequirementSource source = state != null && state.IsCurrent ? RequirementSource.CurrentQuest : RequirementSource.FutureQuest;

                object conditions = JsonNode.Get(JsonNode.Get(quest, "conditions", "Conditions"), "AvailableForFinish", "availableForFinish");
                List<QuestCondition> parsed = ParseQuestConditions(conditions);
                for (int i = 0; i < parsed.Count; i++)
                {
                    QuestCondition condition = parsed[i];
                    if (state != null && state.IsConditionComplete(condition.Id)) continue;
                    if (condition.Kind == "finditem" && HasMatchingHandover(parsed, condition)) continue;
                    if (condition.Kind != "handoveritem" && condition.Kind != "finditem" &&
                        condition.Kind != "leaveitematlocation" && condition.Kind != "placebeacon") continue;

                    for (int targetIndex = 0; targetIndex < condition.Targets.Count; targetIndex++)
                    {
                        string target = condition.Targets[targetIndex];
                        if (target.Length == 0 || condition.Count <= 0) continue;
                        output.Add(new RequirementContribution(target, source, condition.Count, 0, condition.FoundInRaid, label: questLabel));
                    }
                }
            }
        }

        static List<QuestCondition> ParseQuestConditions(object conditions)
        {
            List<QuestCondition> result = new List<QuestCondition>();
            foreach (object condition in JsonNode.Values(conditions))
            {
                string kind = JsonNode.ReadString(JsonNode.Get(condition, "conditionType", "ConditionType")).Trim().ToLowerInvariant();
                string id = JsonNode.ReadString(JsonNode.Get(condition, "id", "_id", "Id")).Trim();
                int count = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(condition, "value", "Value"), 0));
                bool fir = JsonNode.ReadBool(JsonNode.Get(condition, "onlyFoundInRaid", "OnlyFoundInRaid"), false);
                List<string> targets = new List<string>();
                object targetNode = JsonNode.Get(condition, "target", "Target");
                foreach (object target in JsonNode.ValuesOrSelf(targetNode))
                {
                    string targetId = RequirementContribution.NormalizeId(JsonNode.ReadString(target));
                    if (targetId.Length != 0) targets.Add(targetId);
                }
                result.Add(new QuestCondition(id, kind, count, fir, targets));
            }
            return result;
        }

        static bool HasMatchingHandover(List<QuestCondition> conditions, QuestCondition find)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                QuestCondition candidate = conditions[i];
                if (candidate.Kind != "handoveritem" || candidate.Count != find.Count) continue;
                for (int f = 0; f < find.Targets.Count; f++)
                    if (candidate.Targets.Contains(find.Targets[f])) return true;
            }
            return false;
        }

        void ProjectHideout(object profile, object hideoutTable, List<RequirementContribution> output)
        {
            Dictionary<string, int> currentLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            object profileHideout = JsonNode.Get(profile, "Hideout", "hideout");
            foreach (object area in JsonNode.Values(JsonNode.Get(profileHideout, "Areas", "areas")))
            {
                string type = JsonNode.ReadString(JsonNode.Get(area, "type", "Type", "_id", "id"));
                if (type.Length == 0) continue;
                int level = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(area, "level", "Level"), 0));
                if (JsonNode.ReadBool(JsonNode.Get(area, "constructing", "Constructing"), false)) level++;
                int known;
                if (!currentLevels.TryGetValue(type, out known) || level > known) currentLevels[type] = level;
            }

            ProjectHideoutAreas(JsonNode.Get(hideoutTable, "areas", "Areas"), currentLevels, output);
            ProjectHideoutAreas(JsonNode.Get(hideoutTable, "customAreas", "CustomAreas"), currentLevels, output);
        }

        void ProjectHideoutAreas(object areas, Dictionary<string, int> currentLevels, List<RequirementContribution> output)
        {
            foreach (object area in JsonNode.Values(areas))
            {
                string type = JsonNode.ReadString(JsonNode.Get(area, "type", "Type", "_id", "id"));
                string areaLabel = HideoutAreaName(type);
                int currentLevel;
                currentLevels.TryGetValue(type, out currentLevel);
                foreach (KeyValuePair<string, object> stagePair in JsonNode.Pairs(JsonNode.Get(area, "stages", "Stages")))
                {
                    int stage = JsonNode.ReadInt(stagePair.Key, JsonNode.ReadInt(JsonNode.Get(stagePair.Value, "level", "Level"), 0));
                    if (stage <= currentLevel) continue;
                    foreach (object requirement in JsonNode.Values(JsonNode.Get(stagePair.Value, "requirements", "Requirements")))
                    {
                        string templateId = RequirementContribution.NormalizeId(JsonNode.ReadString(JsonNode.Get(requirement, "templateId", "TemplateId", "_tpl", "tpl")));
                        int count = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(requirement, "count", "Count", "value", "Value"), 0));
                        string requirementType = JsonNode.ReadString(JsonNode.Get(requirement, "type", "Type", "requirementType", "RequirementType"));
                        if (templateId.Length == 0 || count <= 0) continue;
                        bool itemRequirement = requirementType.Length == 0 || requirementType.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0 || requirementType == "1";
                        if (templateId == RequirementDataContract.RuntimeTraceTemplateId)
                            Trace("projector hideout stage=" + stage + " currentLevel=" + currentLevel + " type=" + requirementType + " count=" + count + " accepted=" + itemRequirement);
                        if (!itemRequirement) continue;
                        string label = areaLabel + " L" + stage.ToString(CultureInfo.InvariantCulture);
                        output.Add(new RequirementContribution(templateId, RequirementSource.Hideout, count, label: label));
                    }
                }
            }
        }

        void Trace(string message)
        {
            if (trace != null) trace("[II TRACE] client " + message);
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

        sealed class QuestCondition
        {
            public QuestCondition(string id, string kind, int count, bool fir, List<string> targets)
            {
                Id = id ?? string.Empty;
                Kind = kind ?? string.Empty;
                Count = count;
                FoundInRaid = fir;
                Targets = targets ?? new List<string>();
            }
            public string Id { get; }
            public string Kind { get; }
            public int Count { get; }
            public bool FoundInRaid { get; }
            public List<string> Targets { get; }
        }

        static string HideoutAreaName(string rawType)
        {
            int type;
            if (!int.TryParse(rawType, NumberStyles.Integer, CultureInfo.InvariantCulture, out type))
                return string.IsNullOrWhiteSpace(rawType) ? "Hideout" : rawType.Trim();
            switch (type)
            {
                case 0: return "Vents";
                case 1: return "Security";
                case 2: return "Lavatory";
                case 3: return "Stash";
                case 4: return "Generator";
                case 5: return "Heating";
                case 6: return "Water Collector";
                case 7: return "Medstation";
                case 8: return "Nutrition Unit";
                case 9: return "Rest Space";
                case 10: return "Workbench";
                case 11: return "Intelligence Center";
                case 12: return "Shooting Range";
                case 13: return "Library";
                case 14: return "Scav Case";
                case 15: return "Illumination";
                case 16: return "Hall of Fame";
                case 17: return "Air Filtering Unit";
                case 18: return "Solar Power";
                case 19: return "Booze Generator";
                case 20: return "Bitcoin Farm";
                case 21: return "Christmas Tree";
                case 22: return "Defective Wall";
                case 23: return "Gym";
                case 24: return "Weapon Stand";
                case 25: return "Weapon Stand 2";
                case 26: return "Equipment Presets Stand";
                case 27: return "Cultist Circle";
                default: return "Hideout Area " + type.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    public enum RequirementBootstrapState
    {
        Loading,
        Ready,
        Unavailable
    }

    public sealed class RequirementRuntimeBootstrap
    {
        readonly IRequirementSnapshotTransport transport;
        readonly IRequirementSnapshotDecoder decoder;
        readonly IRequirementDataProjector projector;
        readonly IPriceDataProjector priceProjector;
        readonly ItemPresentationStore presentationStore;
        readonly ItemHoverRuntimeController hoverController;
        readonly Action<string> trace;
        int state = (int)RequirementBootstrapState.Loading;
        string detail = "LOADING ITEM DATA";

        public RequirementRuntimeBootstrap(
            IRequirementSnapshotTransport transport,
            IRequirementSnapshotDecoder decoder,
            IRequirementDataProjector projector,
            ItemPresentationStore presentationStore,
            ItemHoverRuntimeController hoverController,
            IPriceDataProjector priceProjector = null,
            Action<string> trace = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            this.projector = projector ?? throw new ArgumentNullException(nameof(projector));
            this.presentationStore = presentationStore ?? throw new ArgumentNullException(nameof(presentationStore));
            this.hoverController = hoverController ?? throw new ArgumentNullException(nameof(hoverController));
            this.priceProjector = priceProjector ?? new SptPriceDataProjector();
            this.trace = trace;
        }

        public RequirementBootstrapState State => (RequirementBootstrapState)Volatile.Read(ref state);
        public string Detail => Volatile.Read(ref detail);

        public bool TryRefresh(CancellationToken cancellationToken, out string error)
        {
            Interlocked.Exchange(ref state, (int)RequirementBootstrapState.Loading);
            Interlocked.Exchange(ref detail, "LOADING ITEM DATA");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string json = transport.GetSnapshotJson();
                cancellationToken.ThrowIfCancellationRequested();
                RequirementDataEnvelope snapshot = decoder.Decode(json);
                if (snapshot == null || !snapshot.profileReady) throw new InvalidOperationException("Profile is not ready.");
                RequirementProjection projection = projector.Project(snapshot);
                RequirementIndex index = RequirementIndexBuilder.Build(projection);
                ItemRequirementStateIndex requirements = ItemRequirementStateBuilder.Build(index);
                ItemPriceIndex prices = priceProjector.Project(snapshot.prices);
                cancellationToken.ThrowIfCancellationRequested();
                presentationStore.Refresh(requirements, prices);
                TraceRuntimeBoundary(index, requirements);
                hoverController.RefreshActive();
                Interlocked.Exchange(ref detail, "NO REQUIREMENT DATA");
                Interlocked.Exchange(ref state, (int)RequirementBootstrapState.Ready);
                error = null;
                return true;
            }
            catch (OperationCanceledException)
            {
                error = "Requirement data load was cancelled.";
            }
            catch (Exception exception)
            {
                error = exception.InnerException == null ? exception.Message : exception.InnerException.Message;
            }

            Interlocked.Exchange(ref detail, "DATA UNAVAILABLE");
            Interlocked.Exchange(ref state, (int)RequirementBootstrapState.Unavailable);
            hoverController.RefreshActive();
            return false;
        }

        public ItemHoverText CreateFallback(string templateId)
        {
            return new ItemHoverText("ITEM INTELLIGENCE", string.Empty, Detail);
        }

        void TraceRuntimeBoundary(RequirementIndex index, ItemRequirementStateIndex requirements)
        {
            string target = RequirementDataContract.RuntimeTraceTemplateId;
            RequirementIndexEntry indexed = index.Get(target);
            ItemRequirementState state = requirements.Get(target);
            ItemPresentationState presentation = presentationStore.Current.Get(target);
            ItemHoverText text = new ItemHoverTextFormatter().Format(new ItemHoverState(presentation));
            ItemMarkerPresentation marker = ItemMarkerPresentation.From(text);
            if (trace == null) return;
            trace("[II TRACE] client index owned=" + indexed.OwnedCount + " hideout=" + indexed.HideoutNeeded + " questNow=" + indexed.QuestNeededNow + " questLater=" + indexed.QuestNeededLater + " keep=" + indexed.KeepCount);
            trace("[II TRACE] client state owned=" + state.OwnedCount + " hideout=" + state.HideoutNeeded + " questNow=" + state.QuestNeededNow + " questLater=" + state.QuestNeededLater + " keep=" + state.KeepCount + " presentationRequirement=" + presentation.HasRequirementData + " marker=" + marker.Kind);
        }
    }

    static class JsonNode
    {
        public static object Get(object source, params string[] names)
        {
            if (source == null || IsNull(source)) return null;
            IDictionary dictionary = source as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                    for (int i = 0; i < names.Length; i++)
                        if (string.Equals(ReadString(entry.Key), names[i], StringComparison.OrdinalIgnoreCase)) return NullToNull(entry.Value);
            }

            Type type = source.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                object indexed;
                if (TryIndexer(source, names[i], out indexed) && !IsNull(indexed)) return indexed;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
                try
                {
                    PropertyInfo property = type.GetProperty(names[i], flags);
                    if (property != null && property.GetIndexParameters().Length == 0) return NullToNull(property.GetValue(source, null));
                }
                catch { }
                try
                {
                    FieldInfo field = type.GetField(names[i], flags);
                    if (field != null) return NullToNull(field.GetValue(source));
                }
                catch { }
            }
            return null;
        }

        public static IEnumerable<object> Values(object source)
        {
            if (source == null || IsNull(source) || source is string) yield break;
            IDictionary dictionary = source as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary) yield return NullToNull(entry.Value);
                yield break;
            }
            IEnumerable enumerable = source as IEnumerable;
            if (enumerable == null) yield break;
            foreach (object value in enumerable)
            {
                object key;
                object pairValue;
                yield return TryPair(value, out key, out pairValue) ? pairValue : value;
            }
        }

        public static IEnumerable<object> ValuesOrSelf(object source)
        {
            if (source == null || IsNull(source)) yield break;
            if (source is string || !IsArrayLike(source))
            {
                yield return source;
                yield break;
            }
            foreach (object value in Values(source)) yield return value;
        }

        public static IEnumerable<KeyValuePair<string, object>> Pairs(object source)
        {
            if (source == null || IsNull(source) || source is string) yield break;
            IDictionary dictionary = source as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                    yield return new KeyValuePair<string, object>(ReadString(entry.Key), NullToNull(entry.Value));
                yield break;
            }
            IEnumerable enumerable = source as IEnumerable;
            if (enumerable == null) yield break;
            int index = 0;
            foreach (object value in enumerable)
            {
                object key;
                object pairValue;
                if (TryPair(value, out key, out pairValue)) yield return new KeyValuePair<string, object>(ReadString(key), pairValue);
                else yield return new KeyValuePair<string, object>((index++).ToString(), value);
            }
        }

        public static string ReadString(object value)
        {
            if (value == null || IsNull(value)) return string.Empty;
            return value.ToString() ?? string.Empty;
        }

        public static int ReadInt(object value, int fallback)
        {
            int parsed;
            return int.TryParse(ReadString(value), out parsed) ? parsed : fallback;
        }

        public static long ReadLong(object value, long fallback)
        {
            long parsed;
            return long.TryParse(ReadString(value), out parsed) ? parsed : fallback;
        }

        public static bool ReadBool(object value, bool fallback)
        {
            bool parsed;
            if (bool.TryParse(ReadString(value), out parsed)) return parsed;
            int numeric;
            return int.TryParse(ReadString(value), out numeric) ? numeric != 0 : fallback;
        }

        public static bool IsNull(object value)
        {
            if (value == null) return true;
            try
            {
                PropertyInfo type = value.GetType().GetProperty("Type", BindingFlags.Public | BindingFlags.Instance);
                object tokenType = type == null ? null : type.GetValue(value, null);
                string name = tokenType == null ? string.Empty : tokenType.ToString();
                return string.Equals(name, "Null", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Undefined", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        static object NullToNull(object value) { return IsNull(value) ? null : value; }

        static bool IsArrayLike(object source)
        {
            if (source is IList || source is Array) return true;
            string name = source.GetType().Name;
            return name.IndexOf("Array", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool TryIndexer(object source, string name, out object value)
        {
            PropertyInfo[] properties;
            try { properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance); }
            catch { value = null; return false; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                ParameterInfo[] parameters = property.GetIndexParameters();
                if (parameters.Length != 1 || (parameters[0].ParameterType != typeof(string) && parameters[0].ParameterType != typeof(object))) continue;
                try
                {
                    value = property.GetValue(source, new object[] { name });
                    if (value != null) return true;
                }
                catch { }
            }
            value = null;
            return false;
        }

        static bool TryPair(object source, out object key, out object value)
        {
            key = null;
            value = null;
            if (source == null) return false;
            try
            {
                Type type = source.GetType();
                PropertyInfo keyProperty = type.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo valueProperty = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (keyProperty == null || valueProperty == null || keyProperty.GetIndexParameters().Length != 0 || valueProperty.GetIndexParameters().Length != 0)
                    return false;
                key = keyProperty.GetValue(source, null);
                value = NullToNull(valueProperty.GetValue(source, null));
                return key != null;
            }
            catch { return false; }
        }
    }
}
