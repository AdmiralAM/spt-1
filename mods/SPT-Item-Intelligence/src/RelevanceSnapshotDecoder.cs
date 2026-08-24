using System;
using System.Collections.Generic;

namespace SPTItemIntelligence
{
    public sealed class RelevanceSnapshotDecoder : IRequirementSnapshotDecoder
    {
        readonly IRequirementSnapshotDecoder inner;

        public RelevanceSnapshotDecoder(IRequirementSnapshotDecoder inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public RequirementDataEnvelope Decode(string json)
        {
            RequirementDataEnvelope snapshot = inner.Decode(json);
            ItemRelevanceRegistry.Replace(ProjectStatic(snapshot.prices));
            return snapshot;
        }

        static Dictionary<string, ItemRelevanceState> ProjectStatic(object prices)
        {
            Dictionary<string, ItemRelevanceState> result = new Dictionary<string, ItemRelevanceState>(StringComparer.Ordinal);
            foreach (object entry in JsonNode.Values(prices))
            {
                string templateId = RequirementContribution.NormalizeId(JsonNode.ReadString(JsonNode.Get(entry, "templateId", "TemplateId")));
                if (templateId.Length == 0) continue;
                int craftCount = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(entry, "craftCount", "CraftCount"), 0));
                int barterCount = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(entry, "barterCount", "BarterCount"), 0));
                if (craftCount > 0 || barterCount > 0)
                    result[templateId] = new ItemRelevanceState(craftCount, barterCount);
            }
            return result;
        }
    }
}
