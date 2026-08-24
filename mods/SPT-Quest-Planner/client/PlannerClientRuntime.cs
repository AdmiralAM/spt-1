using System;
using System.Reflection;
using System.Threading;

namespace SPTQuestPlanner.Client
{
    public static class PlannerClientContract
    {
        public const string TopologyRoute = "/admiralam/quest-planner/topology";
        public const string StateRoute = "/admiralam/quest-planner/state";
        public const string LocaleRoute = "/admiralam/quest-planner/locales";
        public const int SchemaVersion = 9;
    }

    public sealed class PlannerPayload
    {
        public PlannerPayload(int schemaVersion, long generatedAtUnixSeconds, string json)
        {
            SchemaVersion = schemaVersion;
            GeneratedAtUnixSeconds = generatedAtUnixSeconds;
            Json = json;
        }
        public int SchemaVersion { get; private set; }
        public long GeneratedAtUnixSeconds { get; private set; }
        public string Json { get; private set; }
    }

    public interface IPlannerTransport { string GetJson(string route); }
    public interface IPlannerPayloadDecoder
    {
        PlannerPayload DecodeTopology(string json);
        PlannerPayload DecodeState(string json);
    }

    public sealed class ReflectionSptPlannerTransport : IPlannerTransport
    {
        public string GetJson(string route)
        {
            if (string.IsNullOrWhiteSpace(route)) throw new ArgumentException("Route is missing.", "route");
            Type requestHandler = FindType("SPT.Common.Http.RequestHandler");
            if (requestHandler == null) throw new InvalidOperationException("SPT RequestHandler is unavailable.");
            MethodInfo getJson = null;
            foreach (MethodInfo candidate in requestHandler.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                if (candidate.Name == "GetJson" && candidate.ReturnType == typeof(string) &&
                    parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    getJson = candidate;
                    break;
                }
            }
            if (getJson == null) throw new InvalidOperationException("SPT RequestHandler.GetJson(string) is unavailable.");
            string json = getJson.Invoke(null, new object[] { route }) as string;
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "null", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Quest Planner route returned an empty response: " + route);
            return json;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { Type type = assembly.GetType(fullName, false, false); if (type != null) return type; }
                catch { }
            }
            return null;
        }
    }

    public sealed class ReflectionNewtonsoftPlannerDecoder : IPlannerPayloadDecoder
    {
        public PlannerPayload DecodeTopology(string json) { return Decode(json, false); }
        public PlannerPayload DecodeState(string json) { return Decode(json, true); }

        private static PlannerPayload Decode(string json, bool requireGeneratedAt)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Payload JSON is missing.", "json");
            Type tokenType = FindType("Newtonsoft.Json.Linq.JToken");
            if (tokenType == null) throw new InvalidOperationException("Newtonsoft JSON runtime is unavailable.");
            MethodInfo parse = tokenType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parse == null) throw new InvalidOperationException("Newtonsoft JToken.Parse is unavailable.");
            object root = parse.Invoke(null, new object[] { json });
            int schema = ReadInt(Get(root, "schemaVersion"), 0);
            if (schema != PlannerClientContract.SchemaVersion)
                throw new InvalidOperationException("Unsupported Quest Planner schema " + schema + ".");
            long generated = ReadLong(Get(root, "generatedAtUnixSeconds"), 0L);
            if (requireGeneratedAt && generated <= 0L)
                throw new InvalidOperationException("Quest Planner state payload has no generation timestamp.");
            return new PlannerPayload(schema, generated, json);
        }

        private static object Get(object token, string name)
        {
            if (token == null) return null;
            PropertyInfo item = token.GetType().GetProperty("Item", new[] { typeof(object) });
            if (item != null) { try { return item.GetValue(token, new object[] { name }); } catch { } }
            PropertyInfo stringItem = token.GetType().GetProperty("Item", new[] { typeof(string) });
            if (stringItem != null) { try { return stringItem.GetValue(token, new object[] { name }); } catch { } }
            return null;
        }
        private static int ReadInt(object token, int fallback) { int value; return int.TryParse(token == null ? null : token.ToString(), out value) ? value : fallback; }
        private static long ReadLong(object token, long fallback) { long value; return long.TryParse(token == null ? null : token.ToString(), out value) ? value : fallback; }
        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { Type type = assembly.GetType(fullName, false, false); if (type != null) return type; }
                catch { }
            }
            return null;
        }
    }

    public sealed class PlannerClientCache
    {
        private readonly object gate = new object();
        private PlannerPayload topology;
        private PlannerTopologyIndex topologyIndex;
        private PlannerRequirementIndex requirementIndex;
        private PlannerLocationIndex locationIndex;
        private PlannerLocaleIndex localeIndex;
        private PlannerPayload state;
        private PlannerClientIndex index;
        private long revision;

        public long Revision { get { lock (gate) return revision; } }
        public bool HasTopology { get { lock (gate) return topology != null && topologyIndex != null && requirementIndex != null && locationIndex != null; } }
        public bool HasLocale { get { lock (gate) return localeIndex != null; } }
        public bool HasState { get { lock (gate) return state != null && index != null; } }
        public PlannerPayload Topology { get { lock (gate) return topology; } }
        public PlannerTopologyIndex TopologyIndex { get { lock (gate) return topologyIndex; } }
        public PlannerRequirementIndex RequirementIndex { get { lock (gate) return requirementIndex; } }
        public PlannerLocationIndex LocationIndex { get { lock (gate) return locationIndex; } }
        public PlannerLocaleIndex LocaleIndex { get { lock (gate) return localeIndex; } }
        public PlannerPayload State { get { lock (gate) return state; } }
        public PlannerClientIndex Index { get { lock (gate) return index; } }

        public void ReplaceTopology(
            PlannerPayload value,
            PlannerTopologyIndex typedIndex,
            PlannerRequirementIndex typedRequirements,
            PlannerLocationIndex typedLocations)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (typedIndex == null) throw new ArgumentNullException("typedIndex");
            if (typedRequirements == null) throw new ArgumentNullException("typedRequirements");
            if (typedLocations == null) throw new ArgumentNullException("typedLocations");
            lock (gate)
            {
                topology = value;
                topologyIndex = typedIndex;
                requirementIndex = typedRequirements;
                locationIndex = typedLocations;
                revision++;
            }
        }

        public void ReplaceLocale(PlannerLocaleIndex value)
        {
            if (value == null) throw new ArgumentNullException("value");
            lock (gate)
            {
                // Locale data is presentation-only. It must not invalidate the derived
                // planning cache or force a RaidPlan rebuild when labels arrive.
                localeIndex = value;
            }
        }

        public void ReplaceState(PlannerPayload value, PlannerClientIndex typedIndex)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (typedIndex == null) throw new ArgumentNullException("typedIndex");
            lock (gate)
            {
                if (state != null && value.GeneratedAtUnixSeconds < state.GeneratedAtUnixSeconds) return;
                state = value;
                index = typedIndex;
                revision++;
            }
        }
    }

    public sealed class PlannerRefreshCoordinator
    {
        private readonly IPlannerTransport transport;
        private readonly IPlannerPayloadDecoder decoder;
        private readonly PlannerClientCache cache;
        private int refreshing;
        private int localeAttempted;
        private string localeError;

        public PlannerRefreshCoordinator(IPlannerTransport transport, IPlannerPayloadDecoder decoder, PlannerClientCache cache)
        {
            this.transport = transport ?? throw new ArgumentNullException("transport");
            this.decoder = decoder ?? throw new ArgumentNullException("decoder");
            this.cache = cache ?? throw new ArgumentNullException("cache");
        }

        public string LocaleError { get { return localeError; } }

        public bool EnsureTopology(CancellationToken token, out string error)
        {
            error = null;
            if (cache.HasTopology) return true;
            token.ThrowIfCancellationRequested();
            try
            {
                PlannerPayload payload = decoder.DecodeTopology(transport.GetJson(PlannerClientContract.TopologyRoute));
                PlannerTopologyIndex typedIndex = PlannerTopologyIndexBuilder.Build(payload.Json);
                PlannerRequirementIndex typedRequirements = PlannerRequirementIndexBuilder.Build(payload.Json);
                PlannerLocationIndex typedLocations = PlannerLocationIndexBuilder.Build(payload.Json);
                cache.ReplaceTopology(payload, typedIndex, typedRequirements, typedLocations);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        public bool EnsureLocale(CancellationToken token, out string error)
        {
            error = localeError;
            if (cache.HasLocale) return true;
            if (Interlocked.CompareExchange(ref localeAttempted, 1, 0) != 0) return false;
            token.ThrowIfCancellationRequested();
            try
            {
                PlannerLocaleIndex locale = PlannerLocaleIndexBuilder.Build(transport.GetJson(PlannerClientContract.LocaleRoute));
                cache.ReplaceLocale(locale);
                localeError = null;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                localeError = ex.GetBaseException().Message;
                error = localeError;
                return false;
            }
        }

        public bool TryRefreshState(CancellationToken token, out string error)
        {
            error = null;
            if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0)
            {
                error = "Refresh already in progress.";
                return false;
            }
            try
            {
                token.ThrowIfCancellationRequested();
                if (!EnsureTopology(token, out error)) return false;
                string ignoredLocaleError;
                EnsureLocale(token, out ignoredLocaleError); // presentation-only; one bounded attempt only
                PlannerPayload payload = decoder.DecodeState(transport.GetJson(PlannerClientContract.StateRoute));
                PlannerClientIndex typedIndex = PlannerClientIndexBuilder.Build(payload.Json);
                cache.ReplaceState(payload, typedIndex);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
            finally { Volatile.Write(ref refreshing, 0); }
        }
    }
}
