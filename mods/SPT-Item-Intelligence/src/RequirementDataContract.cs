using System;

namespace SPTItemIntelligence
{
    public static class RequirementDataContract
    {
        public const int SchemaVersion = 1;
        public const string SnapshotRoute = "/spt-item-intelligence/v1/snapshot";
    }

    public sealed class RequirementDataEnvelope
    {
        public RequirementDataEnvelope(long generatedAtUnixSeconds, object profile, object quests, object hideout)
        {
            schemaVersion = RequirementDataContract.SchemaVersion;
            this.generatedAtUnixSeconds = Math.Max(0, generatedAtUnixSeconds);
            this.profile = profile;
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.hideout = hideout ?? throw new ArgumentNullException(nameof(hideout));
        }

        public int schemaVersion { get; }
        public long generatedAtUnixSeconds { get; }
        public bool profileReady => profile != null;
        public object profile { get; }
        public object quests { get; }
        public object hideout { get; }
    }
}
