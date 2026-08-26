using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace SPTQuestPlanner.Client
{
    /// <summary>
    /// Optional, versioned adapter for Admiral Trader's published capability/economy contract.
    /// Quest Planner does not require Admiral Trader or a compile-time JSON package reference.
    /// When a caller supplies the contract, the adapter reads the Newtonsoft runtime already
    /// present in SPT through reflection. Contract drift fails closed.
    /// </summary>
    public static class PlannerAdmiralCapabilityContractAdapter
    {
        private const int SupportedSchemaVersion = 2;
        private const string ExpectedProduct = "Admiral Trader";
        private const string ExpectedTargetSptVersion = "4.1.3";

        public static IReadOnlyList<PlannerCapabilityGoalDefinition> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Capability contract JSON is required.", "json");

            JsonNode root = JsonNode.Parse(json);
            root.RequireType("Object", "root");
            root.RequireExactInt("schemaVersion", SupportedSchemaVersion);
            root.RequireExactString("product", ExpectedProduct);
            string owner = root.RequireString("owner");
            root.RequireExactString("targetSptVersion", ExpectedTargetSptVersion);

            Dictionary<string, PlannerCapabilityGoalDefinition> byCapability =
                new Dictionary<string, PlannerCapabilityGoalDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (JsonNode offer in root.RequireArray("renewableOffers"))
            {
                offer.RequireType("Object", "renewableOffers entry");
                offer.RequireExactString("sourceType", "TraderPurchase");
                offer.RequireExactString("renewability", "Bounded");
                offer.RequireExactBool("permanent", true);

                string capability = offer.RequireString("capabilityFamily");
                string questGateId = offer.RequireString("questGateId");
                string itemTpl = offer.RequireString("itemTpl");
                int stock = offer.RequirePositiveInt("stockPerReset");
                int buyLimit = offer.RequirePositiveInt("buyRestrictionPerReset");

                AddUnique(byCapability, new PlannerCapabilityGoalDefinition(
                    capability,
                    questGateId,
                    owner,
                    PlannerCapabilitySupplyKind.BoundedRenewable,
                    itemTpl,
                    stock,
                    buyLimit,
                    "Admiral Trader economy-admiral-contract schema v" + SupportedSchemaVersion));
            }

            foreach (JsonNode reward in root.RequireArray("oneTimeRewards"))
            {
                reward.RequireType("Object", "oneTimeRewards entry");
                reward.RequireExactString("sourceType", "QuestReward");
                reward.RequireExactString("renewability", "OneTime");
                reward.RequireExactBool("permanent", false);
                reward.RequireExactBool("sampleOnly", true);
                reward.RequirePositiveInt("units");

                string capability = reward.RequireString("capabilityFamily");
                string questId = reward.RequireString("questId");
                string itemTpl = reward.RequireString("itemTpl");

                AddUnique(byCapability, new PlannerCapabilityGoalDefinition(
                    capability,
                    questId,
                    owner,
                    PlannerCapabilitySupplyKind.OneTimeSample,
                    itemTpl,
                    evidenceSource: "Admiral Trader economy-admiral-contract schema v" + SupportedSchemaVersion));
            }

            return byCapability.Values
                .OrderBy(value => value.CapabilityId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void AddUnique(
            IDictionary<string, PlannerCapabilityGoalDefinition> byCapability,
            PlannerCapabilityGoalDefinition definition)
        {
            if (byCapability.ContainsKey(definition.CapabilityId))
                throw new InvalidOperationException("Capability contract contains duplicate capability family: " + definition.CapabilityId);
            byCapability.Add(definition.CapabilityId, definition);
        }

        private sealed class JsonNode
        {
            private readonly object token;
            private readonly Type tokenType;

            private JsonNode(object token, Type tokenType)
            {
                this.token = token ?? throw new ArgumentNullException("token");
                this.tokenType = tokenType ?? throw new ArgumentNullException("tokenType");
            }

            public static JsonNode Parse(string json)
            {
                Type type = FindType("Newtonsoft.Json.Linq.JToken");
                if (type == null)
                    throw new InvalidOperationException("Newtonsoft JSON runtime is unavailable for optional capability-contract parsing.");

                MethodInfo parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (parse == null)
                    throw new InvalidOperationException("Newtonsoft JToken.Parse(string) is unavailable.");

                try
                {
                    object parsed = parse.Invoke(null, new object[] { json });
                    if (parsed == null) throw new InvalidOperationException("Capability contract JSON parsed to null.");
                    return new JsonNode(parsed, type);
                }
                catch (TargetInvocationException ex)
                {
                    throw new ArgumentException("Capability contract JSON is malformed.", "json", ex.InnerException ?? ex);
                }
            }

            public string TypeName
            {
                get
                {
                    PropertyInfo property = tokenType.GetProperty("Type", BindingFlags.Public | BindingFlags.Instance);
                    object value = property == null ? null : property.GetValue(token, null);
                    return value == null ? string.Empty : value.ToString();
                }
            }

            public void RequireType(string expected, string context)
            {
                if (!string.Equals(TypeName, expected, StringComparison.Ordinal))
                    throw new InvalidOperationException("Capability contract " + context + " must be JSON " + expected + ".");
            }

            public JsonNode Get(string name)
            {
                PropertyInfo indexer = token.GetType().GetProperty("Item", new[] { typeof(object) }) ??
                                       token.GetType().GetProperty("Item", new[] { typeof(string) });
                object child = null;
                if (indexer != null)
                {
                    ParameterInfo[] parameters = indexer.GetIndexParameters();
                    object key = parameters.Length == 1 && parameters[0].ParameterType == typeof(object) ? (object)name : name;
                    child = indexer.GetValue(token, new[] { key });
                }
                return child == null ? null : new JsonNode(child, tokenType);
            }

            public string RequireString(string name)
            {
                JsonNode child = Get(name);
                if (child == null || !string.Equals(child.TypeName, "String", StringComparison.Ordinal))
                    throw new InvalidOperationException("Capability contract requires non-empty string '" + name + "'.");
                string value = child.token.ToString();
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("Capability contract requires non-empty string '" + name + "'.");
                return value.Trim();
            }

            public int RequirePositiveInt(string name)
            {
                int value = RequireInt(name);
                if (value <= 0)
                    throw new InvalidOperationException("Capability contract '" + name + "' must be positive.");
                return value;
            }

            public void RequireExactInt(string name, int expected)
            {
                int value = RequireInt(name);
                if (value != expected)
                    throw new InvalidOperationException("Unsupported capability contract " + name + ". Expected " + expected + ".");
            }

            public void RequireExactString(string name, string expected)
            {
                string value = RequireString(name);
                if (!string.Equals(value, expected, StringComparison.Ordinal))
                    throw new InvalidOperationException("Unsupported capability contract " + name + ": " + value);
            }

            public void RequireExactBool(string name, bool expected)
            {
                JsonNode child = Get(name);
                if (child == null || !string.Equals(child.TypeName, "Boolean", StringComparison.Ordinal))
                    throw new InvalidOperationException("Capability contract requires boolean '" + name + "'.");
                bool value;
                if (!bool.TryParse(child.token.ToString(), out value) || value != expected)
                    throw new InvalidOperationException("Capability contract requires '" + name + "' = " + expected.ToString().ToLowerInvariant() + ".");
            }

            public IEnumerable<JsonNode> RequireArray(string name)
            {
                JsonNode child = Get(name);
                if (child == null || !string.Equals(child.TypeName, "Array", StringComparison.Ordinal))
                    throw new InvalidOperationException("Capability contract requires array '" + name + "'.");

                IEnumerable enumerable = child.token as IEnumerable;
                if (enumerable == null)
                    throw new InvalidOperationException("Capability contract array '" + name + "' is not enumerable.");

                foreach (object item in enumerable)
                    if (item != null) yield return new JsonNode(item, tokenType);
            }

            private int RequireInt(string name)
            {
                JsonNode child = Get(name);
                if (child == null || !string.Equals(child.TypeName, "Integer", StringComparison.Ordinal))
                    throw new InvalidOperationException("Capability contract requires integer '" + name + "'.");
                int value;
                if (!int.TryParse(child.token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    throw new InvalidOperationException("Capability contract integer '" + name + "' is invalid.");
                return value;
            }

            private static Type FindType(string fullName)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        Type type = assembly.GetType(fullName, false, false);
                        if (type != null) return type;
                    }
                    catch { }
                }
                return null;
            }
        }
    }
}
