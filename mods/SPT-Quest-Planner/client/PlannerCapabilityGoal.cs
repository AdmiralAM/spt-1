using System;

namespace SPTQuestPlanner.Client
{
    public enum PlannerCapabilitySupplyKind
    {
        Unknown = 0,
        OneTimeSample = 1,
        BoundedRenewable = 2,
        UnboundedRenewable = 3
    }

    public sealed class PlannerCapabilityGoalDefinition
    {
        public PlannerCapabilityGoalDefinition(
            string capabilityId,
            string gateQuestId,
            string owner,
            PlannerCapabilitySupplyKind supplyKind,
            string itemTemplateId = null,
            int? maxUnitsPerReset = null,
            int? maxAcquisitionsPerReset = null,
            string evidenceSource = null)
        {
            CapabilityId = Require(capabilityId, "capabilityId");
            GateQuestId = Require(gateQuestId, "gateQuestId");
            Owner = Require(owner, "owner");
            if (!Enum.IsDefined(typeof(PlannerCapabilitySupplyKind), supplyKind))
                throw new ArgumentOutOfRangeException("supplyKind");
            ValidateOptionalPositive(maxUnitsPerReset, "maxUnitsPerReset");
            ValidateOptionalPositive(maxAcquisitionsPerReset, "maxAcquisitionsPerReset");

            if (supplyKind == PlannerCapabilitySupplyKind.BoundedRenewable &&
                !maxUnitsPerReset.HasValue && !maxAcquisitionsPerReset.HasValue)
                throw new ArgumentException("Bounded renewable capability evidence requires at least one explicit positive reset limit.");

            if ((supplyKind == PlannerCapabilitySupplyKind.OneTimeSample ||
                 supplyKind == PlannerCapabilitySupplyKind.UnboundedRenewable ||
                 supplyKind == PlannerCapabilitySupplyKind.Unknown) &&
                (maxUnitsPerReset.HasValue || maxAcquisitionsPerReset.HasValue))
                throw new ArgumentException("Finite reset limits are only valid for bounded renewable capability evidence.");

            SupplyKind = supplyKind;
            ItemTemplateId = NormalizeOptional(itemTemplateId);
            MaxUnitsPerReset = maxUnitsPerReset;
            MaxAcquisitionsPerReset = maxAcquisitionsPerReset;
            EvidenceSource = NormalizeOptional(evidenceSource);
        }

        public string CapabilityId { get; private set; }
        public string GateQuestId { get; private set; }
        public string Owner { get; private set; }
        public PlannerCapabilitySupplyKind SupplyKind { get; private set; }
        public string ItemTemplateId { get; private set; }
        public int? MaxUnitsPerReset { get; private set; }
        public int? MaxAcquisitionsPerReset { get; private set; }
        public string EvidenceSource { get; private set; }
        public bool HasBoundedSupplyEvidence
        {
            get { return SupplyKind == PlannerCapabilitySupplyKind.BoundedRenewable; }
        }

        private static string Require(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(parameter + " is required.", parameter);
            return value.Trim();
        }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void ValidateOptionalPositive(int? value, string parameter)
        {
            if (value.HasValue && value.Value <= 0)
                throw new ArgumentOutOfRangeException(parameter, "Capability supply reset limits must be positive when present.");
        }
    }

    public sealed class PlannerCapabilityGoal
    {
        public PlannerCapabilityGoal(
            PlannerCapabilityGoalDefinition definition,
            PlannerRaidDecisionIntent questIntent,
            bool gateAlreadyCompleted = false)
        {
            Definition = definition ?? throw new ArgumentNullException("definition");
            QuestIntent = questIntent ?? throw new ArgumentNullException("questIntent");
            GateAlreadyCompleted = gateAlreadyCompleted;
        }

        public PlannerCapabilityGoalDefinition Definition { get; private set; }
        public PlannerRaidDecisionIntent QuestIntent { get; private set; }
        public bool GateAlreadyCompleted { get; private set; }
        public bool HasActionableQuestWork { get { return QuestIntent.HasActionableFocusFrontier; } }
        public bool HasEligibilityUnknowns { get { return QuestIntent.HasUnknownFocusEligibility; } }
        public bool HasTerminalConflict { get { return QuestIntent.HasTerminalFocusConflict; } }
    }

    public static class PlannerCapabilityGoalBuilder
    {
        private const int CompletedDisposition = 5;

        public static PlannerCapabilityGoal Build(
            PlannerCapabilityGoalDefinition definition,
            PlannerTopologyIndex topology,
            PlannerClientIndex state)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (topology == null) throw new ArgumentNullException("topology");
            if (state == null) throw new ArgumentNullException("state");
            if (topology.GetQuest(definition.GateQuestId) == null)
                throw new InvalidOperationException("Capability gate quest is absent from the final Planner topology: " + definition.GateQuestId);

            PlannerRaidDecisionIntent intent = PlannerRaidDecisionIntentBuilder.Build(
                definition.GateQuestId,
                topology,
                state);
            PlannerQuestClientState gateState = state.GetQuest(definition.GateQuestId);
            bool gateAlreadyCompleted = gateState != null && gateState.Disposition == CompletedDisposition;
            return new PlannerCapabilityGoal(definition, intent, gateAlreadyCompleted);
        }
    }
}
