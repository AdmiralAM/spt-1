using System;
using System.Threading;

namespace SPTItemIntelligence
{
    public interface IItemHoverViewSink
    {
        void Show(ItemHoverText text);
        void Clear();
    }

    public sealed class ItemHoverRuntimeController
    {
        readonly ItemPresentationStore store;
        readonly ItemHoverPresentationAdapter adapter;
        readonly ItemHoverTextCache textCache;
        readonly IItemHoverViewSink sink;
        readonly Func<string, ItemHoverText> fallbackFactory;

        string activeTemplateId = string.Empty;
        ItemHoverText activeText = ItemHoverText.Empty;

        public ItemHoverRuntimeController(
            ItemPresentationStore store,
            IItemHoverViewSink sink,
            ItemHoverTextCache textCache = null,
            Func<string, ItemHoverText> fallbackFactory = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            adapter = new ItemHoverPresentationAdapter(store);
            this.textCache = textCache ?? new ItemHoverTextCache();
            this.fallbackFactory = fallbackFactory;
        }

        public ItemHoverText ActiveText => Volatile.Read(ref activeText);
        public bool HasActiveItem => activeTemplateId.Length != 0;

        public ItemHoverText OnHoverEnter(string templateId)
        {
            string normalized = RequirementContribution.NormalizeId(templateId);
            ItemHoverState hover = adapter.OnHoverEnter(normalized);
            if (hover.HasData)
            {
                activeTemplateId = hover.TemplateId;
                return Publish(hover);
            }

            activeTemplateId = fallbackFactory == null ? string.Empty : normalized;
            return PublishFallback(normalized);
        }

        public void OnHoverExit()
        {
            activeTemplateId = string.Empty;
            adapter.OnHoverExit();
            PublishClear();
        }

        // Call only from the existing presentation-refresh event/path. This is intentionally
        // not a polling hook: it reprojects the currently hovered item after a new immutable
        // presentation snapshot is published.
        public ItemHoverText RefreshActive()
        {
            string templateId = activeTemplateId;
            if (templateId.Length == 0) return ItemHoverText.Empty;

            ItemHoverState hover = adapter.OnHoverEnter(templateId);
            if (!hover.HasData) return PublishFallback(templateId);

            activeTemplateId = hover.TemplateId;
            return Publish(hover);
        }

        ItemHoverText Publish(ItemHoverState hover)
        {
            ItemHoverText next = textCache.Get(hover, store.Current);
            if (!next.HasData)
            {
                PublishClear();
                return ItemHoverText.Empty;
            }
            return PublishText(next);
        }

        ItemHoverText PublishFallback(string templateId)
        {
            if (fallbackFactory == null || string.IsNullOrEmpty(templateId))
            {
                PublishClear();
                return ItemHoverText.Empty;
            }

            ItemHoverText fallback;
            try { fallback = fallbackFactory(templateId); }
            catch { fallback = ItemHoverText.Empty; }
            if (fallback == null || !fallback.HasData)
            {
                PublishClear();
                return ItemHoverText.Empty;
            }
            return PublishText(fallback);
        }

        ItemHoverText PublishText(ItemHoverText next)
        {
            ItemHoverText current = ActiveText;
            if (object.ReferenceEquals(current, next)) return next;
            Interlocked.Exchange(ref activeText, next);
            sink.Show(next);
            return next;
        }

        void PublishClear()
        {
            ItemHoverText current = ActiveText;
            if (object.ReferenceEquals(current, ItemHoverText.Empty)) return;

            Interlocked.Exchange(ref activeText, ItemHoverText.Empty);
            sink.Clear();
        }
    }
}
