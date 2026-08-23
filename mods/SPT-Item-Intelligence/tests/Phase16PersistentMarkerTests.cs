using System;
using SPTItemIntelligence;

static class Phase16PersistentMarkerTests
{
    public static int Run()
    {
        int assertions = 0;
        ItemPresentationStore store = new ItemPresentationStore();
        RecordingHoverSink hover = new RecordingHoverSink();
        RecordingMarkerSink markers = new RecordingMarkerSink();
        ItemHoverRuntimeController controller = new ItemHoverRuntimeController(store, hover);
        EftItemViewHoverIntegration integration = new EftItemViewHoverIntegration(controller, null, null, markers, markers);
        PersistentInventoryItemView view = new PersistentInventoryItemView
        {
            Item = new Item { TemplateId = " TPL-PERSISTENT " }
        };

        Expect(integration.DispatchRegister(view), "Init registers the ItemView before hover", ref assertions);
        Expect(markers.RegisterCount == 1 && markers.TemplateId == "tpl-persistent", "registration normalizes and retains the template id", ref assertions);
        Expect(integration.DispatchEnter(view), "pointer enter activates marker detail state", ref assertions);
        integration.DispatchExit(view);
        Expect(markers.UnregisterCount == 0, "pointer exit preserves the registered marker", ref assertions);
        integration.DispatchUnregister(view);
        Expect(markers.UnregisterCount == 1 && object.ReferenceEquals(markers.Unregistered, view), "Kill removes the exact pooled ItemView", ref assertions);
        integration.Dispose();
        Expect(markers.ClearCount == 1, "plugin disposal clears all persistent markers", ref assertions);
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 16 assertion failed: " + message);
    }

    sealed class PersistentInventoryItemView
    {
        public Item Item { get; set; }
        public void Init() { }
        public void Kill() { }
        public void OnPointerEnter(object eventData) { }
        public void OnPointerExit(object eventData) { }
    }

    sealed class Item { public string TemplateId { get; set; } }

    sealed class RecordingHoverSink : IItemHoverViewSink
    {
        public void Show(ItemHoverText text) { }
        public void Clear() { }
    }

    sealed class RecordingMarkerSink : IItemHoverAnchorSink, IItemViewRegistrySink
    {
        public int RegisterCount;
        public int UnregisterCount;
        public int ClearCount;
        public string TemplateId;
        public object Unregistered;
        public void SetAnchor(object itemView) { }
        public void ClearAnchor() { }
        public void RegisterView(object itemView, string templateId) { RegisterCount++; TemplateId = templateId; }
        public void UnregisterView(object itemView) { UnregisterCount++; Unregistered = itemView; }
        public void ClearViews() { ClearCount++; }
    }
}
