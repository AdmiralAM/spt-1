using System;
using System.Collections.Generic;

namespace SPTItemIntelligence
{
    // Retain the adapter name for compatibility. This is independently owned SPT logic.
    public sealed class AqcQuestRequirementProjector : IRequirementDataProjector
    {
        readonly SptRequirementDataProjector inner;
        public AqcQuestRequirementProjector(Action<string> trace = null) { inner = new SptRequirementDataProjector(trace); }
        public RequirementProjection Project(RequirementDataEnvelope snapshot)
        {
            RequirementProjection projection = inner.Project(snapshot);
            // Compatibility readers only; runtime presentation uses its immutable generation.
            var states = new Dictionary<string, FirRequirementState>(StringComparer.Ordinal);
            foreach (var pair in RequirementIndexBuilder.Build(projection).Entries)
            {
                var a = pair.Value.Allocation;
                states[pair.Key] = new FirRequirementState(a.OwnedFir, a.NowFirRequired, a.LaterFirRequired);
            }
            FirRequirementRegistry.Publish(states);
            return projection;
        }
    }
}
