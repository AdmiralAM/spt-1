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
            string perSlotLine = null,
            string bestSellLine = null,
            string bestTraderLine = null,
            string fleaPriceLine = null, ItemRequirementAllocation allocation = null, ModuleSelection modules = null)
        {
            modules = modules ?? ModuleSelection.Default;
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

            Allocation = allocation ?? new ItemRequirementAllocation(OwnedCount, OwnedFoundInRaid, QuestNeededNow, QuestNeededLater, HideoutNeeded, QuestNowFoundInRaid, QuestLaterFoundInRaid);
            QuestNowOwned = Allocation.NowAllocated;
            QuestNowFoundInRaidOwned = Allocation.NowFirAllocated;
            HideoutOwned = Allocation.HideoutAllocated;
            QuestLaterOwned = Allocation.LaterAllocated;
            QuestLaterFoundInRaidOwned = Allocation.LaterFirAllocated;
            QuestNowMissing = Allocation.NowMissing;
            HideoutMissing = Allocation.HideoutMissing;
            QuestLaterMissing = Allocation.LaterMissing;
            ValueLine = Primary.Length == 0 ? string.Empty : GameUiText.T("Value: ", "Цена: ") + Primary;
            BestSellLine = bestSellLine ?? string.Empty;
            BestTraderLine = bestTraderLine ?? string.Empty;
            FleaPriceLine = fleaPriceLine ?? string.Empty;
            QuestNowLine = RequirementLine(
                GameUiText.T("Active quest", "Активный квест"), QuestNowOwned, QuestNeededNow, QuestNowFoundInRaidOwned, QuestNowFoundInRaid);
            HideoutLine = RequirementLine(GameUiText.T("Hideout", "Убежище"), HideoutOwned, HideoutNeeded, 0, 0);
            QuestLaterLine = RequirementLine(
                GameUiText.T("Future quest", "Будущий квест"), QuestLaterOwned, QuestNeededLater, QuestLaterFoundInRaidOwned, QuestLaterFoundInRaid);
            KeepLine = CountLine(GameUiText.T("Keep", "Оставить"), KeepCount);
            PerSlotLine = perSlotLine ?? string.Empty;

            ItemRelevanceState relevance = modules.CraftBarter ? ItemRelevanceRegistry.Get(TemplateId) : ItemRelevanceState.Empty;
            SummaryLine = allocation == null ? string.Empty :
                (allocation.Coverage == RequirementCoverage.NotNeeded ? GameUiText.T("Not Needed", "Не нужен") : allocation.Coverage == RequirementCoverage.Enough ? GameUiText.T("Enough", "Достаточно") : GameUiText.T("Need More ×", "Нужно ещё ×") + allocation.Missing.ToString(CultureInfo.InvariantCulture)) +
                (allocation.MustKeep ? GameUiText.T(" · Keep ×", " · Оставить ×") + allocation.Keep.ToString(CultureInfo.InvariantCulture) : string.Empty);
            SummaryOwnedLine = GameUiText.T("Owned ×", "В наличии ×") + OwnedCount.ToString(CultureInfo.InvariantCulture) + GameUiText.T(" · FIR ×", " · Найдено в рейде ×") + OwnedFoundInRaid.ToString(CultureInfo.InvariantCulture);
            string ownedLine = OwnedFoundInRaid > 0
                ? GameUiText.T("Owned ×", "В наличии ×") + OwnedCount.ToString(CultureInfo.InvariantCulture) + GameUiText.T(" · FIR ×", " · Найдено в рейде ×") + OwnedFoundInRaid.ToString(CultureInfo.InvariantCulture)
                : CountLine(GameUiText.T("Owned", "В наличии"), OwnedCount);
            if (relevance.OnYouCount > 0)
            {
                ownedLine = (ownedLine.Length == 0 ? string.Empty : ownedLine + " · ") +
                    GameUiText.T("On You ×", "При себе ×") + relevance.OnYouCount.ToString(CultureInfo.InvariantCulture);
            }
            OwnedLine = ownedLine;
            BestSourceLine = bestSource ?? string.Empty;
            CraftLine = CountLine(GameUiText.T("Craft", "Крафт"), relevance.CraftCount);
            BarterLine = CountLine(GameUiText.T("Barter", "Бартер"), relevance.BarterCount);

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
                ? GameUiText.T("Requirements: +", "Требования: +") + (detailed.Count - detailedVisible).ToString(CultureInfo.InvariantCulture) + GameUiText.T(" more", " ещё")
                : string.Empty;
        }

        public ItemRequirementAllocation Allocation { get; }
        public string SummaryLine { get; }
        public string SummaryOwnedLine { get; }
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
        public string BestSellLine { get; }
        public string BestTraderLine { get; }
        public string FleaPriceLine { get; }
        public string QuestNowLine { get; }
        public string QuestLaterLine { get; }
        public string HideoutLine { get; }
        public string KeepLine { get; }
        public string PerSlotLine { get; }
        public string CraftLine { get; }
        public string BarterLine { get; }
        public string OwnedLine { get; }
        public string BestSourceLine { get; }
        public IReadOnlyList<string> RequirementDetailLines { get; }
        public IReadOnlyList<string> DetailedRequirementLines { get; }
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

            if (TryLine(SummaryLine, requestedIndex, ref current, out found)) return found;
            if (SummaryLine.Length > 0 && TryLine(SummaryOwnedLine, requestedIndex, ref current, out found)) return found;

            if (mode == ItemTooltipMode.Normal || mode == ItemTooltipMode.Full)
            {
                if (TryLine(BestTraderLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(FleaPriceLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(PerSlotLine, requestedIndex, ref current, out found)) return found;
                if (mode == ItemTooltipMode.Full)
                {
                    if (TryLine(CraftLine, requestedIndex, ref current, out found)) return found;
                    if (TryLine(BarterLine, requestedIndex, ref current, out found)) return found;
                }
            }
            else if (TryLine(ValueLine, requestedIndex, ref current, out found))
            {
                return found;
            }

            if (mode != ItemTooltipMode.Minimal)
            {
                if (TryLine(QuestNowLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(HideoutLine, requestedIndex, ref current, out found)) return found;
                if (TryLine(QuestLaterLine, requestedIndex, ref current, out found)) return found;
            }
            if (SummaryLine.Length == 0 && TryLine(KeepLine, requestedIndex, ref current, out found)) return found;

            if (mode == ItemTooltipMode.Detailed || mode == ItemTooltipMode.Full)
            {
                if (SummaryLine.Length == 0 && TryLine(OwnedLine, requestedIndex, ref current, out found)) return found;
                IReadOnlyList<string> selected = mode == ItemTooltipMode.Full ? RequirementDetailLines : DetailedRequirementLines;
                for (int i = 0; i < selected.Count; i++)
                    if (TryLine(selected[i], requestedIndex, ref current, out found)) return found;
                if (mode == ItemTooltipMode.Detailed && TryLine(MoreRequirementsLine, requestedIndex, ref current, out found)) return found;
            }
            if (current == 0 && requestedIndex == 0) return GameUiText.T("No active requirements", "Нет активных требований");
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

        static string RequirementLine(string label, int owned, int required, int firOwned, int firRequired)
        {
            if (required <= 0) return string.Empty;
            string line = label + ": " + owned.ToString(CultureInfo.InvariantCulture) + "/" + required.ToString(CultureInfo.InvariantCulture);
            if (firRequired > 0)
                line += GameUiText.T(" · FIR ", " · Найдено в рейде ") + firOwned.ToString(CultureInfo.InvariantCulture) + "/" + firRequired.ToString(CultureInfo.InvariantCulture);
            return owned >= required && firOwned >= firRequired ? line + " ✓" : line;
        }

    }

    public sealed class ItemHoverTextFormatter
    {
        public ItemHoverText Format(ItemHoverState hover)
        {
            return Format(hover, ItemValueMode.Vendor);
        }

        public ItemHoverText Format(ItemHoverState hover, ItemValueMode valueMode, ModuleSelection modules = null)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;
            modules = modules ?? ModuleSelection.Default;
            if ((!modules.Tooltips || !modules.Value) && hover.Presentation.Price != null)
                hover = new ItemHoverState(new ItemPresentationState(hover.TemplateId, hover.Presentation.Requirement, null));

            bool fleaPreferred = valueMode == ItemValueMode.Flea;
            string trader = string.IsNullOrWhiteSpace(hover.BestTraderName) ? GameUiText.T("Trader", "Торговец") : hover.BestTraderName.Trim();
            long preferredValue = fleaPreferred ? hover.FleaUnitValue : hover.TraderUnitValue;
            string preferredSource = fleaPreferred ? GameUiText.T("Flea", "Барахолка") : trader;
            long alternateValue = fleaPreferred ? hover.TraderUnitValue : hover.FleaUnitValue;
            string alternateSource = fleaPreferred ? trader : GameUiText.T("Flea", "Барахолка");
            bool useAlternateAsPrimary = preferredValue <= 0 && alternateValue > 0;
            long unitValue = useAlternateAsPrimary ? alternateValue : preferredValue;
            string source = useAlternateAsPrimary ? alternateSource : preferredSource;
            string primary = unitValue > 0 ? FormatRoubles(unitValue) + " · " + source : string.Empty;
            string secondary = !useAlternateAsPrimary && alternateValue > 0
                ? alternateSource + ": " + FormatRoubles(alternateValue)
                : string.Empty;

            string bestTrader = hover.TraderUnitValue > 0
                ? GameUiText.T("Trader: ", "Торговец: ") + trader + " · " + FormatRoubles(hover.TraderUnitValue)
                : string.Empty;
            string fleaPrice = hover.FleaUnitValue > 0
                ? GameUiText.T("Flea: ", "Барахолка: ") + FormatRoubles(hover.FleaUnitValue)
                : string.Empty;
            string perSlot = hover.ValuePerSlot > 0
                ? GameUiText.T("Per slot: ", "За слот: ") + FormatRoubles(hover.ValuePerSlot)
                : string.Empty;

            ItemRequirementAllocation truth = hover.Presentation.Requirement.Allocation;
            return new ItemHoverText(
                primary,
                secondary,
                string.Empty,
                hover.TemplateId,
                hover.OwnedCount,
                hover.QuestNeededNow,
                hover.QuestNeededLater,
                hover.HideoutNeeded,
                hover.KeepCount,
                string.Empty,
                FormatRequirementDetails(hover.RequirementDetails),
                truth.OwnedFir,
                truth.NowFirRequired,
                truth.LaterFirRequired,
                perSlot,
                string.Empty,
                bestTrader,
                fleaPrice, truth, modules);
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
                string prefix = detail.Source == RequirementSource.CurrentQuest ? GameUiText.T("Now", "Сейчас") :
                    detail.Source == RequirementSource.FutureQuest ? GameUiText.T("Later", "Позже") : GameUiText.T("Hideout", "Убежище");
                string line = prefix + ": " + detail.Label + " ×" + detail.RemainingCount.ToString(CultureInfo.InvariantCulture);
                if (detail.FoundInRaidRequired) line += GameUiText.T(" · FIR", " · Найдено в рейде");
                yield return line;
            }
        }

        sealed class DetailAggregate
        {
            public DetailAggregate(RequirementSource source, string label, bool foundInRaidRequired)
            {
                Source = source;
                Label = label;
                FoundInRaidRequired = foundInRaidRequired;
            }
            public RequirementSource Source { get; }
            public string Label { get; }
            public bool FoundInRaidRequired { get; }
            public int RemainingCount { get; set; }
        }

        static string FormatRoubles(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture) + " ₽";
        }
    }

    public sealed class ItemHoverTextCache
    {
        readonly ItemHoverTextFormatter formatter;
        readonly Func<ItemValueMode> valueModeProvider;
        readonly Func<ModuleSelection> modulesProvider;
        int lastModuleKey = -1;
        int lastLanguageKey = -1;
        readonly Dictionary<ItemPresentationState, ItemHoverText> cache = new Dictionary<ItemPresentationState, ItemHoverText>(ReferenceComparer.Instance);
        ItemPresentationIndex lastIndex;
        ItemValueMode lastValueMode;
        bool hasValueMode;

        public ItemHoverTextCache(ItemHoverTextFormatter formatter = null, Func<ItemValueMode> valueModeProvider = null, Func<ModuleSelection> modulesProvider = null)
        {
            this.formatter = formatter ?? new ItemHoverTextFormatter();
            this.valueModeProvider = valueModeProvider ?? (() => ItemValueMode.Vendor);
            this.modulesProvider = modulesProvider ?? (() => ModuleSelection.Default);
        }

        public ItemHoverText Get(ItemHoverState hover, ItemPresentationIndex index)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;
            ItemPresentationState presentation = hover.Presentation;
            ItemValueMode valueMode = valueModeProvider();
            ModuleSelection modules = modulesProvider();

            int languageKey = GameUiText.LanguageKey;
            if (!object.ReferenceEquals(lastIndex, index) || !hasValueMode || valueMode != lastValueMode || lastModuleKey != modules.Key || lastLanguageKey != languageKey)
            {
                cache.Clear();
                lastIndex = index;
                lastValueMode = valueMode;
                hasValueMode = true;
                lastModuleKey = modules.Key;
                lastLanguageKey = languageKey;
            }

            ItemHoverText text;
            if (cache.TryGetValue(presentation, out text)) return text;
            text = formatter.Format(hover, valueMode, modules);
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
