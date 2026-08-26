using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SPTQuestPlanner.Client
{
    /// <summary>
    /// Optional, versioned adapter for Admiral Trader's published capability/economy contract.
    /// Quest Planner does not require Admiral Trader at runtime; callers may supply the contract
    /// when present. Contract drift fails closed instead of silently fabricating capability goals.
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

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Capability contract JSON is malformed.", "json", ex);
            }

            RequireExactInt(root, "schemaVersion", SupportedSchemaVersion);
            RequireExactString(root, "product", ExpectedProduct);
            string owner = RequireString(root, "owner");
            RequireExactString(root, "targetSptVersion", ExpectedTargetSptVersion);

            Dictionary<string, PlannerCapabilityGoalDefinition> byCapability =
                new Dictionary<string, PlannerCapabilityGoalDefinition>(StringComparer.OrdinalIgnoreCase);

            JArray renewableOffers = RequireArray(root, "renewableOffers");
            foreach (JToken token in renewableOffers)
            {
                JObject offer = RequireObject(token, "renewableOffers entry");
                RequireExactString(offer, "sourceType", "TraderPurchase");
                RequireExactString(offer, "renewability", "Bounded");
                RequireExactBool(offer, "permanent", true);

                string capability = RequireString(offer, "capabilityFamily");
                string questGateId = RequireString(offer, "questGateId");
                string itemTpl = RequireString(offer, "itemTpl");
                int stock = RequirePositiveInt(offer, "stockPerReset");
                int buyLimit = RequirePositiveInt(offer, "buyRestrictionPerReset");

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

            JArray oneTimeRewards = RequireArray(root, "oneTimeRewards");
            foreach (JToken token in oneTimeRewards)
            {
                JObject reward = RequireObject(token, "oneTimeRewards entry");
                RequireExactString(reward, "sourceType", "QuestReward");
                RequireExactString(reward, "renewability", "OneTime");
                RequireExactBool(reward, "permanent", false);
                RequireExactBool(reward, "sampleOnly", true);
                RequirePositiveInt(reward, "units");

                string capability = RequireString(reward, "capabilityFamily");
                string questId = RequireString(reward, "questId");
                string itemTpl = RequireString(reward, "itemTpl");

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

        private static JArray RequireArray(JObject obj, string name)
        {
            JToken token;
            if (!obj.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.Array)
                throw new InvalidOperationException("Capability contract requires array '" + name + "'.");
            return (JArray)token;
        }

        private static JObject RequireObject(JToken token, string name)
        {
            JObject obj = token as JObject;
            if (obj == null) throw new InvalidOperationException("Capability contract " + name + " must be an object.");
            return obj;
        }

        private static string RequireString(JObject obj, string name)
        {
            JToken token;
            if (!obj.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)token))
                throw new InvalidOperationException("Capability contract requires non-empty string '" + name + "'.");
            return ((string)token).Trim();
        }

        private static void RequireExactString(JObject obj, string name, string expected)
        {
            string actual = RequireString(obj, name);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported capability contract " + name + ": " + actual);
        }

        private static void RequireExactInt(JObject obj, string name, int expected)
        {
            JToken token;
            if (!obj.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.Integer || (int)token != expected)
                throw new InvalidOperationException("Unsupported capability contract " + name + ". Expected " + expected + ".");
        }

        private static int RequirePositiveInt(JObject obj, string name)
        {
            JToken token;
            if (!obj.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.Integer)
                throw new InvalidOperationException("Capability contract requires integer '" + name + "'.");
            int value = (int)token;
            if (value <= 0) throw new InvalidOperationException("Capability contract '" + name + "' must be positive.");
            return value;
        }

        private static void RequireExactBool(JObject obj, string name, bool expected)
        {
            JToken token;
            if (!obj.TryGetValue(name, StringComparison.Ordinal, out token) || token.Type != JTokenType.Boolean || (bool)token != expected)
                throw new InvalidOperationException("Capability contract requires '" + name + "' = " + expected.ToString().ToLowerInvariant() + ".");
        }
    }
}
