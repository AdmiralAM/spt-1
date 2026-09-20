using SPTStackableArmorPlates;

Assert(PlateStackPolicy.IsStandalonePlate(PlateStackPolicy.ArmorPlateParentId, 2, 2, 3.3), "standalone plate");
Assert(!PlateStackPolicy.IsStandalonePlate("65649eb40bf0ed77b8044453", 1, 1, 0.5), "integrated child taxonomy");
Assert(!PlateStackPolicy.IsStandalonePlate(PlateStackPolicy.ArmorPlateParentId, 1, 1, 0), "zero-weight insert");
Assert(PlateStackPolicy.CanDonateDurability(18, 40), "damaged plate can donate");
Assert(!PlateStackPolicy.CanDonateDurability(40, 40), "pristine plate cannot donate");
Assert(PlateStackPolicy.RepairedDurability(25, 40, 10) == 35, "durability is transferred");
Assert(PlateStackPolicy.RepairedDurability(35, 40, 20) == 40, "repair is capped at survivor maximum");
Assert(PlateStackPolicy.SharesArmorSlot(0b0011, 0b0110), "different templates sharing a slot are compatible");
Assert(!PlateStackPolicy.SharesArmorSlot(0b0001, 0b0100), "plates for disjoint slots are incompatible");
Console.WriteLine("Armor Plate Field Repair policy contract: PASS");

static void Assert(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Contract failed: {name}");
    }
}
