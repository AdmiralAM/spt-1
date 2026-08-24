using System;
using System.Collections.Generic;
using System.Globalization;

namespace SPTItemIntelligence
{
    public enum ItemTooltipMode
    {
        Minimal,
        Normal,
        Detailed,
        Full
    }

    public sealed class ItemHoverText
    {
        internal static readonly ItemHoverText Empty = new ItemHoverText(string.Empty, string.Empty, string.Empty);

        public ItemHoverText(string primary, string secondary, string status)
            : this(primary, secondary, status, string.Empty, 0, 0, 0, 0, 0)
        {
        }

        public ItemHoverText(
            string primary,
            string secondary,
            string status,
            string templateId,
            int ownedCount,
            int questNeededNow,
            int questNeededLater,
            int hideoutNeeded,
            int keepCount,
            string bestSource = null,
            IEnumerable<string> requirementDetails = null,
            int ownedFoundInRaid = 0,
            int questNowFoundInRaid = 0,
            int questLaterFoundInRaid = 0,
            string perSlotLine = null)
        {
            Primary = primary ?? string.Empty;
            Secondary = secondary ?? string.Empty;
            Status = status ?? string.Empty;
            TemplateId = templateId ?? string.Empty;
            OwnedCount = Math.Max(0, ownedCount);
            QuestNeededNow = Math.Max(0, questNeededNow);
            QuestNeededLater = Math.Max(0, questNeededLater);
            HideoutNeeded = Math.Max(0, hideoutNeeded);
            KeepCount = Math.Max(0, keepCount);
            OwnedFoundInRaid = Math.Min(OwnedCount, Math.Max(0, ownedFoundInRaid));
            QuestNowFoundInRaid = Math.Min(QuestNeededNow, Math.Max(0, questNowFoundInRaid));
            QuestLaterFoundInRaid = Math.Min(QuestNeededLater, Math.Max(0, questLaterFoundInRaid));

            int availableFir = OwnedFoundInRaid;
            int availableNonFir = Math.Max(0, OwnedCount - OwnedFoundInRaid);

            RequirementAllocation questNow = AllocateRequirement(QuestNeededNow, QuestNowFoundInRaid, ref availableFir, ref availableNonFir);
            QuestNowOwned = questNow.TotalOwned;
            QuestNowFoundInRaidOwned = questNow.FoundInRaidOwned;

            RequirementAllocation hideout = AllocateRequirement(HideoutNeeded, 0, ref availableFir, ref availableNonFir);
            HideoutOwned = hideout.TotalOwned;

            RequirementAllocation questLater = AllocateRequirement(QuestNeededLater, QuestLaterFoundInRaid, ref availableFir, ref availableNonFir);
            QuestLaterOwned = questLater.TotalOwned;
            QuestLaterFoundInRaidOwned = questLater.FoundInRaidOwned;

            QuestNowMissing = QuestNeededNow - QuestNowOwned;
            HideoutMissing = HideoutNeeded - HideoutOwned;
            QuestLaterMissing = QuestNeededLater - QuestLaterOwned;

            ValueLine = Primary.Length == 0 ? string.Empty : "Value: " + Primary;
            QuestNowLine = RequirementLine("Quest Now", QuestNowOwned, QuestNeededNow, QuestNowFoundInRaidOwned, QuestNowFoundInRaid);
            HideoutLine = RequirementLine("Hideout", HideoutOwned, HideoutNeeded, 0, 0);
            QuestLaterLine = RequirementLine("Quest Later", QuestLaterOwned, QuestNeededLater, QuestLaterFoundInRaidOwned, QuestLaterFoundInRaid);
            KeepLine = CountLine("Keep", KeepCount);
            PerSlotLine = perSlotLine ?? string.Empty;
            OwnedLine = OwnedFoundInRaid > 0
                ? "Owned ×" + OwnedCount.ToString(CultureInfo.InvariantCulture) + " · FIR ×" + OwnedFoundInRaid.ToString(CultureInfo.InvariantCulture)
                : CountLine("Owned", OwnedCount);
            BestSourceLine = bestSource ?? string.Empty;

            List<string> details = new List<string>();
            List<string> detailed = new List<string>();
            if (requirementDetails != null)
            {
                foreach (string detail in requirementDetails)
                {
                    if (string.IsNullOrWhiteSpace(detail)) continue;
                    string normalized = detail.Trim();
                    details.Add(normalized);
                    if (!normalized.StartsWith("Later:", StringComparison.OrdinalIgnoreCase)) detailed.Add(normalized);
                }
            }
            RequirementDetailLines = details.AsReadOnly();
            int detailedVisible = Math.Min(3, detailed.Count);
            DetailedRequirementLines = detailed.GetRange(0, detailedVisible).AsReadOnly();
            DetailedRequirementCount = detailedVisible;
            MoreRequirementsLine = detailed.Count > detailedVisible
                ? "Requirements: +" + (detailed.Count - detailedVisible).ToString(CultureInfo.InvariantCulture) + " more"
                : string.Empty;
        }

        public string Primary { get; }
        public string Secondary { get; }
        public string Status { get; }
        public string TemplateId { get; }
        public int OwnedCount { get; }
        public int OwnedFoundInRaid { get; }
        public int QuestNeededNow { get; }
        public int QuestNeededLater { get; }
        public int HideoutNeeded { get; }
        public int KeepCount { get; }
        public int QuestNowFoundInRaid { get; }
        public int QuestLaterFoundInRaid { get; }
        public int QuestNowOwned { get; }
        public int QuestNowFoundInRaidOwned { get; }
        public int HideoutOwned { get; }
        public int QuestLaterOwned { get; }
        public int QuestLaterFoundInRaidOwned { get; }
        public int QuestNowMissing { get; }
        public int HideoutMissing { get; }
        public int QuestLaterMissing { get; }
        public string ValueLine { get; }
        public string QuestNowLine { get; }
        public string QuestLaterLine { get; }
        public string HideoutLine { get; }
        public string KeepLine { get; }
        public string PerSlotLine { get; }
        public string OwnedLine { get; }
        public string BestSourceLine { get; }
        public IReadOnlyList<string> RequirementDetailLines { get; }
        public IReadOnlyList<string> DetailedRequirementLines { get; }
        public int DetailedRequirementCount { get; }
        public string MoreRequirementsLine { get; }
        public bool HasData => Primary.Length != 0 || Secondary.Length != 0 || Status.Length != 0 || KeepCount > 0;
        public bool IsDiagnostic => string.Equals(Status, "LOADING ITEM DATA", StringComparison.OrdinalIgnoreCase) || string.Equals(Status, "NO REQUIREMENT DATA", StringComparison.OrdinalIgnoreCase) || string.Equals(Status, "DATA UNAVAILABLE", StringComparison.OrdinalIgnoreCase);

        public int GetLineCount(ItemTooltipMode mode)
        {
            int count = 0;
            while (GetLine(mode, count).Length != 0) count++;
            return count;
        }

        public string GetLine(ItemTooltipMode mode, int requestedIndex)
        {
            if (requestedIndex < 0) return string.Empty;
            int current = 0;
            string found;

            if (IsDiagnostic)
            {
                if (TryLine(Primary, requestedIndex, ref current, out found)) return found;
                if (TryLine(Secondary, requestedIndex, ref current, out found)) return found;
                if (TryLine(Status, requestedIndex, ref current, out found)) return found;
                return string.Empty;
            }

            if (TryLine(ValueLine, requestedIndex, ref current, out found)) return found;
            if (mode == ItemTooltipMode.Full)
            {
                if (TryLine(Secondary, requestedIndex, ref current, out found)) return found;
                if (TryLine(PerSlotLine, requestedIndex, ref current, out found)) return found;
            }
            if (mode != ItemTooltipMode.Minimal)
            {
                if (TryLine(QuestNowLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(HideoutLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(QuestLaterLine, requestedIndex, ref current, out found)) return found;
            }
            if (TryLine(KeepLine, requestedIndex, ref current, out found)) return found;

            if (mode == ItemTooltipMode.Detailed || mode == ItemTooltipMode.Full)
            {
                if (TryLine(OwnedLine, requestedIndex, ref current, out found)) return found;
                IReadOnlyList<string> selected = mode == ItemTooltipMode.Full ? RequirementDetailLines : DetailedRequirementLines;
                for (int i = 0; i < selected.Count; i++) if (TryLine(selected[i], requestedIndex, ref current, out found)) return found;
                if (mode == ItemTooltipMode.Detailed && TryLine(MoreRequirementsLine, requestedIndex, ref current, out found)) return found;
            }
            if (current == 0 && requestedIndex == 0) return "No active requirements";
            return string.Empty;
        }

        static RequirementAllocation AllocateRequirement(int required, int foundInRaidRequired, ref int availableFir, ref int availableNonFir)
        {
            int firRequired = Math.Min(required, Math.Max(0, foundInRaidRequired));
            int anyRequired = Math.Max(0, required - firRequired);
            int firForFir = Math.Min(availableFir, firRequired);
            availableFir -= firForFir;
            int nonFirForAny = Math.Min(availableNonFir, anyRequired);
            availableNonFir -= nonFirForAny;
            int anyStillMissing = anyRequired - nonFirForAny;
            int firForAny = Math.Min(availableFir, anyStillMissing);
            availableFir -= firForAny;
            return new RequirementAllocation(firForFir + nonFirForAny + firForAny, firForFir);
        }

        static bool TryLine(string line, int requestedIndex, ref int current, out string found)
        {
            found = string.Empty;
            if (string.IsNullOrEmpty(line)) return false;
            if (current++ != requestedIndex) return false;
            found = line;
            return true;
        }

        static string CountLine(string label, int count) => count <= 0 ? string.Empty : label + " ×" + count.ToString(CultureInfo.InvariantCulture);

        static string RequirementLine(string label, int owned, int required, int firOwned, int firRequired)
        {
            if (required <= 0) return string.Empty;
            string line = label + ": " + owned.ToString(CultureInfo.InvariantCulture) + "/" + required.ToString(CultureInfo.InvariantCulture);
            if (firRequired > 0) line += " · FIR " + firOwned.ToString(CultureInfo.InvariantCulture) + "/" + firRequired.ToString(CultureInfo.InvariantCulture);
            return owned >= required && firOwned >= firRequired ? line + " ✓" : line;
        }

        readonly struct RequirementAllocation
        {
            public RequirementAllocation(int totalOwned, int foundInRaidOwned) { TotalOwned = totalOwned; FoundInRaidOwned = foundInRaidOwned; }
            public int TotalOwned { get; }
            public int FoundInRaidOwned { get; }
        }
    }

    public sealed class ItemHoverTextFormatter
    {
        public ItemHoverText Format(ItemHoverState hover) => Format(hover, ItemValueMode.Vendor);

        public ItemHoverText Format(ItemHoverState hover, ItemValueMode valueMode)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;

            bool fleaPrimary = valueMode == ItemValueMode.Flea;
            long unitValue = fleaPrimary ? hover.FleaUnitValue : hover.TraderUnitValue;
            string trader = string.IsNullOrWhiteSpace(hover.BestTraderName) ? "Vendor" : hover.BestTraderName.Trim();
            string source = fleaPrimary ? "Flea" : trader;
            string primary = unitValue > 0 ? FormatRoubles(unitValue) + " · " + source : string.Empty;
            long alternateValue = fleaPrimary ? hover.TraderUnitValue : hover.FleaUnitValue;
            string alternateSource = fleaPrimary ? trader : "Flea";
            string secondary = alternateValue > 0 ? alternateSource + ": " + FormatRoubles(alternateValue) : string.Empty;
            string perSlot = hover.ValuePerSlot > 0 ? "Per slot: " + FormatRoubles(hover.ValuePerSlot) : string.Empty;
            FirRequirementState fir = FirRequirementRegistry.Get(hover.TemplateId);
            return new ItemHoverText(primary, secondary, string.Empty, hover.TemplateId, hover.OwnedCount, hover.QuestNeededNow, hover.QuestNeededLater, hover.HideoutNeeded, hover.KeepCount, string.Empty, FormatRequirementDetails(hover.RequirementDetails), fir.OwnedFoundInRaid, fir.QuestNowFoundInRaid, fir.QuestLaterFoundInRaid, perSlot);
        }

        static IEnumerable<string> FormatRequirementDetails(IReadOnlyList<RequirementDetail> details)
        {
            if (details == null) yield break;
            List<DetailAggregate> ordered = new List<DetailAggregate>();
            Dictionary<string, DetailAggregate> grouped = new Dictionary<string, DetailAggregate>(StringComparer.Ordinal);
            for (int i = 0; i < details.Count; i++)
            {
                RequirementDetail detail = details[i];
                if (detail == null || detail.RemainingCount <= 0 || detail.Label.Length == 0) continue;
                string key = ((int)detail.Source).ToString(CultureInfo.InvariantCulture) + "|" + detail.Label + "|" + (detail.FoundInRaidRequired ? "1" : "0");
                DetailAggregate aggregate;
                if (!grouped.TryGetValue(key, out aggregate))
                {
                    aggregate = new DetailAggregate(detail.Source, detail.Label, detail.FoundInRaidRequired);
                    grouped.Add(key, aggregate);
                    ordered.Add(aggregate);
                }
                aggregate.RemainingCount += detail.RemainingCount;
            }
            for (int i = 0; i < ordered.Count; i++)
            {
                DetailAggregate detail = ordered[i];
                string prefix = detail.Source == RequirementSource.CurrentQuest ? "Now" : detail.Source == RequirementSource.FutureQuest ? "Later" : "Hideout";
                string line = prefix + ": " + detail.Label + " ×" + detail.RemainingCount.ToString(CultureInfo.InvariantCulture);
                if (detail.FoundInRaidRequired) line += " · FIR";
                yield return line;
            }
        }

        sealed class DetailAggregate
        {
            public DetailAggregate(RequirementSource source, string label, bool foundInRaidRequired) { Source = source; Label = label; FoundInRaidRequired = foundInRaidRequired; }
            public RequirementSource Source { get; }
            public string Label { get; }
            public bool FoundInRaidRequired { get; }
            public int RemainingCount { get; set; }
        }

        static string FormatRoubles(long value) => value.ToString("N0", CultureInfo.InvariantCulture) + " ₽";
    }

    public sealed class ItemHoverTextCache
    {
        readonly ItemHoverTextFormatter formatter;
        readonly Func<ItemValueMode> valueModeProvider;
        readonly Dictionary<ItemPresentationState, ItemHoverText> cache = new Dictionary<ItemPresentationState, ItemHoverText>(ReferenceComparer.Instance);
        ItemPresentationIndex lastIndex;
        ItemValueMode lastValueMode;
        bool hasValueMode;

        public ItemHoverTextCache(ItemHoverTextFormatter formatter = null, Func<ItemValueMode> valueModeProvider = null)
        {
            this.formatter = formatter ?? new ItemHoverTextFormatter();
            this.valueModeProvider = valueModeProvider ?? (() => ItemValueMode.Vendor);
        }

        public ItemHoverText Get(ItemHoverState hover, ItemPresentationIndex index)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;
            ItemPresentationState presentation = hover.Presentation;
            ItemValueMode valueMode = valueModeProvider();
            if (!object.ReferenceEquals(lastIndex, index) || !hasValueMode || valueMode != lastValueMode)
            {
                cache.Clear(); lastIndex = index; lastValueMode = valueMode; hasValueMode = true;
            }
            ItemHoverText text;
            if (cache.TryGetValue(presentation, out text)) return text;
            text = formatter.Format(hover, valueMode);
            cache[presentation] = text;
            return text;
        }

        sealed class ReferenceComparer : IEqualityComparer<ItemPresentationState>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(ItemPresentationState x, ItemPresentationState y) => object.ReferenceEquals(x, y);
            public int GetHashCode(ItemPresentationState obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
