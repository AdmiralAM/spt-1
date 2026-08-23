using System;

namespace SPTItemIntelligence
{
    public static class RequirementDataContract
    {
        public const int SchemaVersion = 2;
        public const string SnapshotRoute = "/spt-item-intelligence/v2/snapshot";
        public const string RuntimeTraceTemplateId = "619cbfeb6b8a1b37a54eebfa";
    }

    public sealed class RequirementDataEnvelope
    {
        public RequirementDataEnvelope(long generatedAtUnixSeconds, object profile, object quests, object hideout)
            : this(generatedAtUnixSeconds, profile, quests, hideout, Array.Empty<ItemPriceSnapshotEntry>())
        {
        }

        public RequirementDataEnvelope(long generatedAtUnixSeconds, object profile, object quests, object hideout, object prices)
        {
            schemaVersion = RequirementDataContract.SchemaVersion;
            this.generatedAtUnixSeconds = Math.Max(0, generatedAtUnixSeconds);
            this.profile = profile;
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.hideout = hideout ?? throw new ArgumentNullException(nameof(hideout));
            this.prices = prices ?? throw new ArgumentNullException(nameof(prices));
        }

        public int schemaVersion { get; }
        public long generatedAtUnixSeconds { get; }
        public bool profileReady => profile != null;
        public object profile { get; }
        public object quests { get; }
        public object hideout { get; }
        public object prices { get; }
    }

    public sealed class ItemPriceSnapshotEntry
    {
        public ItemPriceSnapshotEntry(string templateId, long traderUnitValue, string traderName, long fleaUnitValue, long fallbackUnitValue, int width, int height)
        {
            this.templateId = templateId ?? string.Empty;
            this.traderUnitValue = Math.Max(0, traderUnitValue);
            this.traderName = traderName ?? string.Empty;
            this.fleaUnitValue = Math.Max(0, fleaUnitValue);
            this.fallbackUnitValue = Math.Max(0, fallbackUnitValue);
            this.width = Math.Max(1, width);
            this.height = Math.Max(1, height);
        }

        public string templateId { get; }
        public long traderUnitValue { get; }
        public string traderName { get; }
        public long fleaUnitValue { get; }
        public long fallbackUnitValue { get; }
        public int width { get; }
        public int height { get; }
    }
}
