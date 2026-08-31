using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadPseudoSlotEnumerationContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        // Mirrors the exact SPT 4.x Inventory.GetItemsInSlots contract used by the
        // scoped reload bridge: resolve each EquipmentSlot value through the
        // InventoryEquipment cache, keep the equipped roots in caller order, then
        // append descendants of compound roots. A pseudo enum value is therefore
        // not rejected merely because Enum.GetValues() does not declare it; the
        // decisive runtime condition is that the equipment cache contains that
        // integer slot index.
        const int armBand = 14;
        int belt = RuntimeIdentity.DedicatedBeltEquipmentSlotValue;

        var armBandMagazine = new FakeItem("armband-mag");
        var beltMagazineA = new FakeItem("belt-mag-a");
        var beltMagazineB = new FakeItem("belt-mag-b");
        var armBandRoot = new FakeCompound("armband-root", armBandMagazine);
        var beltRoot = new FakeCompound(RuntimeIdentity.DedicatedMagazineBeltItemId, beltMagazineA, beltMagazineB);

        var equipment = new FakeEquipmentCache(17);
        equipment.Set(armBand, armBandRoot);
        equipment.Set(belt, beltRoot);

        FakeItem[] beltItems = GetItemsInSlots(equipment, new[] { belt }).ToArray();
        Assert(beltItems.Length == 3,
            "pseudo-slot15 enumeration must return the equipped Belt root plus both descendants when the runtime cache owns index 15");
        Assert(ReferenceEquals(beltItems[0], beltRoot)
            && ReferenceEquals(beltItems[1], beltMagazineA)
            && ReferenceEquals(beltItems[2], beltMagazineB),
            "SPT GetItemsInSlots ordering is root first, then compound descendants in collection order");

        FakeItem[] mixed = GetItemsInSlots(equipment, new[] { armBand, belt }).ToArray();
        Assert(mixed.Length == 5
            && ReferenceEquals(mixed[0], armBandRoot)
            && ReferenceEquals(mixed[1], beltRoot)
            && ReferenceEquals(mixed[2], armBandMagazine)
            && ReferenceEquals(mixed[3], beltMagazineA)
            && ReferenceEquals(mixed[4], beltMagazineB),
            "multi-slot enumeration preserves the complete root prefix before appending compound descendants");

        bool missingSlotFailedClosed = false;
        try
        {
            _ = GetItemsInSlots(new FakeEquipmentCache(15), new[] { belt }).ToArray();
        }
        catch (IndexOutOfRangeException)
        {
            missingSlotFailedClosed = true;
        }
        Assert(missingSlotFailedClosed,
            "slot15 is unsafe only when the runtime equipment cache lacks index 15; the reload bridge must keep that condition inside its fail-closed boundary");
    }

    static IEnumerable<FakeItem> GetItemsInSlots(FakeEquipmentCache equipment, IEnumerable<int> slots)
    {
        FakeItem[] roots = slots.Select(equipment.GetSlot).ToArray();
        return roots.Concat(roots.OfType<FakeCompound>().SelectMany(x => x.Descendants));
    }

    sealed class FakeEquipmentCache
    {
        readonly FakeItem[] slots;

        internal FakeEquipmentCache(int slotCount)
        {
            slots = new FakeItem[slotCount];
        }

        internal void Set(int slot, FakeItem item)
        {
            slots[slot] = item;
        }

        internal FakeItem GetSlot(int slot)
        {
            return slots[slot];
        }
    }

    class FakeItem
    {
        internal string TemplateId { get; }

        internal FakeItem(string templateId)
        {
            TemplateId = templateId;
        }
    }

    sealed class FakeCompound : FakeItem
    {
        internal IReadOnlyList<FakeItem> Descendants { get; }

        internal FakeCompound(string templateId, params FakeItem[] descendants)
            : base(templateId)
        {
            Descendants = descendants ?? Array.Empty<FakeItem>();
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload pseudo-slot enumeration contract regression failed: " + message);
    }
}
