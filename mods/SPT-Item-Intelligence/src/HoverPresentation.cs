using System;
using System.Collections.Generic;
using System.Threading;

namespace SPTItemIntelligence
{
    public sealed class ItemHoverState
    {
        internal static readonly ItemHoverState Empty = new ItemHoverState(ItemPresentationState.Empty);

        internal ItemHoverState(ItemPresentationState presentation)
        {
            Presentation = presentation ?? ItemPresentationState.Empty;
        }

        public ItemPresentationState Presentation { get; }
        public bool HasData => Presentation != ItemPresentationState.Empty && (Presentation.HasPriceData || Presentation.HasRequirementData);
        public string TemplateId => Presentation.TemplateId;
        public long TotalValue => Presentation.TotalValue;
        public long ValuePerSlot => Presentation.ValuePerSlot;
        public PriceSource BestPriceSource => Presentation.BestPriceSource;
        public long BestUnitValue => Presentation.Price == null ? 0 : Presentation.Price.BestUnitValue;
        public string BestTraderName => Presentation.Price == null ? string.Empty : Presentation.Price.TraderName;
        public ValueTier TotalTier => Presentation.TotalTier;
        public ValueTier PerSlotTier => Presentation.PerSlotTier;
        public bool IsSafeToSell => Presentation.IsSafeToSell;
        public string HoldReason => Presentation.HoldReason;
        public int OwnedCount => Presentation.Requirement == null ? 0 : Presentation.Requirement.OwnedCount;
        public int KeepCount => Presentation.Requirement == null ? 0 : Presentation.Requirement.KeepCount;
        public int SurplusCount => Presentation.Requirement == null ? 0 : Presentation.Requirement.SurplusCount;
        public int QuestNeededNow => Presentation.Requirement == null ? 0 : Presentation.Requirement.QuestNeededNow;
        public int QuestNeededLater => Presentation.Requirement == null ? 0 : Presentation.Requirement.QuestNeededLater;
        public int HideoutNeeded => Presentation.Requirement == null ? 0 : Presentation.Requirement.HideoutNeeded;
        public IReadOnlyList<RequirementDetail> RequirementDetails =>
            Presentation.Requirement == null ? ItemRequirementState.Empty.Details : Presentation.Requirement.Details;
    }

    public sealed class ItemHoverPresentationAdapter
    {
        readonly ItemPresentationStore store;
        ItemPresentationState lastPresentation = ItemPresentationState.Empty;
        ItemHoverState active = ItemHoverState.Empty;

        public ItemHoverPresentationAdapter(ItemPresentationStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ItemHoverState Active => Volatile.Read(ref active);

        public ItemHoverState OnHoverEnter(string templateId)
        {
            ItemPresentationState presentation = store.Get(templateId);
            ItemHoverState next;

            if (presentation == ItemPresentationState.Empty)
            {
                lastPresentation = ItemPresentationState.Empty;
                next = ItemHoverState.Empty;
            }
            else if (object.ReferenceEquals(lastPresentation, presentation))
            {
                next = Active;
            }
            else
            {
                lastPresentation = presentation;
                next = new ItemHoverState(presentation);
            }

            Interlocked.Exchange(ref active, next);
            return next;
        }

        public void OnHoverExit()
        {
            lastPresentation = ItemPresentationState.Empty;
            Interlocked.Exchange(ref active, ItemHoverState.Empty);
        }
    }
}
