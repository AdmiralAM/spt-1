using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

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
            IEnumerable<string> requirementDetails = null)
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

            ValueLine = Primary.Length == 0 ? string.Empty : "Value: " + Primary;
            QuestNowLine = CountLine("Quest Now", QuestNeededNow);
            QuestLaterLine = CountLine("Quest Later", QuestNeededLater);
            HideoutLine = CountLine("Hideout", HideoutNeeded);
            KeepLine = CountLine("Keep", KeepCount);
            PerSlotLine = Secondary.Length == 0 ? string.Empty : "Per slot: " + Secondary;
            OwnedLine = CountLine("Owned", OwnedCount);
            TemplateLine = TemplateId.Length == 0 ? string.Empty : "ID: " + TemplateId;
            BestSourceLine = bestSource ?? string.Empty;
            List<string> details = new List<string>();
            if (requirementDetails != null)
                foreach (string detail in requirementDetails)
                    if (!string.IsNullOrWhiteSpace(detail)) details.Add(detail.Trim());
            RequirementDetailLines = details.AsReadOnly();
            DetailedRequirementCount = Math.Min(3, details.Count);
            MoreRequirementsLine = details.Count > DetailedRequirementCount
                ? "Requirements: +" + (details.Count - DetailedRequirementCount).ToString(CultureInfo.InvariantCulture) + " more"
                : string.Empty;
        }

        public string Primary { get; }
        public string Secondary { get; }
        public string Status { get; }
        public string TemplateId { get; }
        public int OwnedCount { get; }
        public int QuestNeededNow { get; }
        public int QuestNeededLater { get; }
        public int HideoutNeeded { get; }
        public int KeepCount { get; }
        public string ValueLine { get; }
        public string QuestNowLine { get; }
        public string QuestLaterLine { get; }
        public string HideoutLine { get; }
        public string KeepLine { get; }
        public string PerSlotLine { get; }
        public string OwnedLine { get; }
        public string TemplateLine { get; }
        public string BestSourceLine { get; }
        public IReadOnlyList<string> RequirementDetailLines { get; }
        public int DetailedRequirementCount { get; }
        public string MoreRequirementsLine { get; }
        public bool HasData => Primary.Length != 0 || Secondary.Length != 0 || Status.Length != 0 || KeepCount > 0;
        public bool IsDiagnostic =>
            string.Equals(Status, "LOADING ITEM DATA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, "NO REQUIREMENT DATA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, "DATA UNAVAILABLE", StringComparison.OrdinalIgnoreCase);

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
            if (mode != ItemTooltipMode.Minimal)
            {
                if (TryLine(QuestNowLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(QuestLaterLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(HideoutLine, requestedIndex, ref current, out found)) return found;
            }
            if (TryLine(KeepLine, requestedIndex, ref current, out found)) return found;

            if (mode == ItemTooltipMode.Detailed || mode == ItemTooltipMode.Full)
            {
                if (TryLine(BestSourceLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(PerSlotLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(OwnedLine, requestedIndex, ref current, out found)) return found;
                int detailLimit = mode == ItemTooltipMode.Full ? RequirementDetailLines.Count : DetailedRequirementCount;
                for (int i = 0; i < detailLimit; i++)
                    if (TryLine(RequirementDetailLines[i], requestedIndex, ref current, out found)) return found;
                if (mode == ItemTooltipMode.Detailed && TryLine(MoreRequirementsLine, requestedIndex, ref current, out found)) return found;
            }
            if (mode == ItemTooltipMode.Full)
            {
                if (TryLine(Status, requestedIndex, ref current, out found)) return found;
                if (TryLine(TemplateLine, requestedIndex, ref current, out found)) return found;
            }

            if (current == 0 && requestedIndex == 0) return "No active requirements";
            return string.Empty;
        }

        static bool TryLine(string line, int requestedIndex, ref int current, out string found)
        {
            found = string.Empty;
            if (string.IsNullOrEmpty(line)) return false;
            if (current++ != requestedIndex) return false;
            found = line;
            return true;
        }

        static string CountLine(string label, int count)
        {
            return count <= 0 ? string.Empty : label + " ×" + count.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class ItemHoverTextFormatter
    {
        public ItemHoverText Format(ItemHoverState hover)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;

            string primary = hover.TotalValue > 0 ? FormatRoubles(hover.TotalValue) : string.Empty;
            string secondary = hover.ValuePerSlot > 0 ? FormatRoubles(hover.ValuePerSlot) + "/slot" : string.Empty;
            string status = FormatStatus(hover);
            string bestSource = FormatBestSource(hover);
            return new ItemHoverText(
                primary,
                secondary,
                status,
                hover.TemplateId,
                hover.OwnedCount,
                hover.QuestNeededNow,
                hover.QuestNeededLater,
                hover.HideoutNeeded,
                hover.KeepCount,
                bestSource,
                FormatRequirementDetails(hover.RequirementDetails));
        }

        static string FormatBestSource(ItemHoverState hover)
        {
            if (hover == null || hover.BestUnitValue <= 0 || hover.BestPriceSource == PriceSource.None) return string.Empty;
            string source;
            switch (hover.BestPriceSource)
            {
                case PriceSource.Flea:
                    source = "Flea";
                    break;
                case PriceSource.Trader:
                    source = string.IsNullOrWhiteSpace(hover.BestTraderName) ? "Trader" : hover.BestTraderName.Trim();
                    break;
                default:
                    source = "Handbook";
                    break;
            }
            return "Best: " + source + " · " + FormatRoubles(hover.BestUnitValue) + "/unit";
        }

        static IEnumerable<string> FormatRequirementDetails(IReadOnlyList<RequirementDetail> details)
        {
            if (details == null) yield break;
            for (int i = 0; i < details.Count; i++)
            {
                RequirementDetail detail = details[i];
                if (detail == null || detail.RemainingCount <= 0 || detail.Label.Length == 0) continue;
                string prefix = detail.Source == RequirementSource.CurrentQuest ? "Now" :
                    detail.Source == RequirementSource.FutureQuest ? "Later" : "Hideout";
                string line = prefix + ": " + detail.Label + " ×" + detail.RemainingCount.ToString(CultureInfo.InvariantCulture);
                if (detail.FoundInRaidRequired) line += " · FIR";
                yield return line;
            }
        }

        static string FormatStatus(ItemHoverState hover)
        {
            if (hover.IsSafeToSell) return hover.SurplusCount > 0 ? "SAFE TO SELL · surplus " + hover.SurplusCount.ToString(CultureInfo.InvariantCulture) : "SAFE TO SELL";
            if (!string.IsNullOrEmpty(hover.HoldReason)) return "KEEP · " + hover.HoldReason;
            if (hover.KeepCount > 0) return "KEEP · " + hover.KeepCount.ToString(CultureInfo.InvariantCulture) + " needed";
            return string.Empty;
        }

        static string FormatRoubles(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture) + " ₽";
        }
    }

    public sealed class ItemHoverTextCache
    {
        readonly ItemHoverTextFormatter formatter;
        readonly Dictionary<ItemPresentationState, ItemHoverText> cache = new Dictionary<ItemPresentationState, ItemHoverText>(ReferenceComparer.Instance);
        ItemPresentationIndex lastIndex;

        public ItemHoverTextCache(ItemHoverTextFormatter formatter = null)
        {
            this.formatter = formatter ?? new ItemHoverTextFormatter();
        }

        public ItemHoverText Get(ItemHoverState hover, ItemPresentationIndex index)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;
            ItemPresentationState presentation = hover.Presentation;

            if (!object.ReferenceEquals(lastIndex, index))
            {
                cache.Clear();
                lastIndex = index;
            }

            ItemHoverText text;
            if (cache.TryGetValue(presentation, out text)) return text;
            text = formatter.Format(hover);
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
