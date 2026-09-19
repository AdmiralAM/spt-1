using SPTStackableArmorPlates;

Assert(PlateStackPolicy.IsStandalonePlate(PlateStackPolicy.ArmorPlateParentId, 2, 2, 3.3), "standalone plate");
Assert(!PlateStackPolicy.IsStandalonePlate("65649eb40bf0ed77b8044453", 1, 1, 0.5), "integrated child taxonomy");
Assert(!PlateStackPolicy.IsStandalonePlate(PlateStackPolicy.ArmorPlateParentId, 1, 1, 0), "zero-weight insert");
Assert(PlateStackPolicy.DesiredStackSize(1) == 4, "raise vanilla stack");
Assert(PlateStackPolicy.DesiredStackSize(8) == 8, "preserve larger external stack");
Console.WriteLine("Stackable Armor Plates policy contract: PASS");

static void Assert(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Contract failed: {name}");
    }
}
