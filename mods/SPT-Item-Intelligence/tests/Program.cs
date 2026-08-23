using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Program
{
    static int assertions;

    static void Main()
    {
        ItemRegistry registry = ItemRegistry.CreateDefault();
        Expect(object.ReferenceEquals(ItemIntelligenceRegistry.Shared, ItemIntelligenceRegistry.Shared), "shared registry is canonical");
        Expect(registry.Resolve((object)null).Category == ItemCategory.Unknown, "null uses unknown fallback");
        Expect(registry.Resolve((object)null).HasTag(ItemTag.Unknown), "unknown fallback is tagged");

        ItemDefinition food = Resolve(registry, "food", "FoodItem", new Dictionary<string, object> { ["Hydration"] = 40, ["Energy"] = 20 });
        Expect(food.Category == ItemCategory.Food, "food category");
        Expect(food.HasTag(ItemTag.Hydration) && food.HasTag(ItemTag.Energy), "food semantic tags");
        ItemDefinition meds = Resolve(registry, "meds", "MedicalItem", new Dictionary<string, object> { ["HpResource"] = 100, ["Effects"] = "HeavyBleeding Pain Fracture" });
        Expect(meds.Category == ItemCategory.Meds, "meds category");
        Expect(meds.HasTag(ItemTag.Healing) && meds.HasTag(ItemTag.Bleed) && meds.HasTag(ItemTag.Pain) && meds.HasTag(ItemTag.Fracture), "medical tags");
        Expect(Resolve(registry, "ammo", "Item", new Dictionary<string, object> { ["Caliber"] = "Caliber545x39" }).Category == ItemCategory.Ammo, "ammo category");
        Expect(Resolve(registry, "weapon", "Item", new Dictionary<string, object> { ["WeaponClass"] = "assaultRifle" }).Category == ItemCategory.Weapon, "weapon category");
        ItemDefinition armor = Resolve(registry, "armor", "ArmorComponent", new Dictionary<string, object> { ["ArmorClass"] = 5 });
        Expect(armor.Category == ItemCategory.Armor && armor.HasTag(ItemTag.Armor), "armor category and tag");
        ItemDefinition backpack = Resolve(registry, "backpack", "BackpackItem", null);
        Expect(backpack.Category == ItemCategory.Backpack && backpack.HasTag(ItemTag.Storage), "backpack category and storage tag");
        ItemDefinition container = Resolve(registry, "container", "ContainerItem", new Dictionary<string, object> { ["Grids"] = new object[0] });
        Expect(container.Category == ItemCategory.Container && container.HasTag(ItemTag.Storage), "container category and storage tag");
        Expect(Resolve(registry, "key", "KeyComponent", new Dictionary<string, object> { ["MaximumNumberOfUsage"] = 10 }).Category == ItemCategory.Key, "key category");
        ItemDefinition quest = Resolve(registry, "quest", "Item", new Dictionary<string, object> { ["QuestItem"] = true });
        Expect(quest.Category == ItemCategory.Quest && quest.HasTag(ItemTag.Quest), "quest category and tag");
        Expect(Resolve(registry, "barter", "BarterItem", null).Category == ItemCategory.Barter, "barter category");
        ItemDefinition unknown = Resolve(registry, "mystery", "GenericThing", null);
        Expect(unknown.Category == ItemCategory.Unknown && unknown.HasTag(ItemTag.Unknown), "unmatched item remains unknown");
        ItemDescriptor normalized = new ItemDescriptor("  ABC  ", null, "  Tactical   battery  ", "  TB  ", "BarterItem");
        ItemDefinition normalizedDefinition = registry.Resolve(normalized);
        Expect(normalizedDefinition.TemplateId == "abc", "template id normalization");
        Expect(normalizedDefinition.Name == "Tactical battery" && normalizedDefinition.ShortName == "TB", "name normalization");
        registry.RegisterParent("parent-food", ItemCategory.Food);
        ItemDefinition inherited = registry.Resolve(new ItemDescriptor("child-food", "PARENT-FOOD", "Ration", "Ration", "GenericThing"));
        Expect(inherited.Category == ItemCategory.Food, "parent category mapping");
        ItemDefinition exact = new ItemDefinition("exact-id", "Known", "Known", ItemCategory.Key, null);
        registry.Register(exact);
        Expect(object.ReferenceEquals(registry.Resolve(new ItemDescriptor("EXACT-ID", null, "Ignored", "Ignored", "GenericThing")), exact), "exact registry override");
        FakeItem reflected = new FakeItem { Template = new FakeTemplate { _id = " reflected-id ", _parent = "parent-food", _name = "  Field   ration ", ShortName = "FR", Props = new FakeProps { Energy = 15 } } };
        ItemDefinition reflectedDefinition = registry.Resolve(reflected);
        Expect(reflectedDefinition.TemplateId == "reflected-id" && reflectedDefinition.Name == "Field ration", "reflection adapter normalization");
        Expect(reflectedDefinition.Category == ItemCategory.Food && reflectedDefinition.HasTag(ItemTag.Energy), "reflection adapter semantics");
        ItemDefinition cachedA = Resolve(registry, "cached", "BarterItem", null);
        ItemDefinition cachedB = Resolve(registry, "cached", "IgnoredType", null);
        Expect(object.ReferenceEquals(cachedA, cachedB), "template definitions are cached");
        ItemDefinition fastPathSeed = Resolve(registry, "fast-path", "BarterItem", null);
        ItemDefinition fastPathHit = registry.Resolve(new FastPathItem());
        Expect(object.ReferenceEquals(fastPathSeed, fastPathHit), "cached object resolution bypasses full descriptor reflection");

        int phase2Assertions = Phase2Tests.Run();
        int phase3Assertions = Phase3Tests.Run();
        int phase4Assertions = Phase4Tests.Run();
        int phase5Assertions = Phase5Tests.Run();
        int phase6Assertions = Phase6PricingTests.Run();
        int phase7Assertions = Phase7PresentationTests.Run();
        int phase8Assertions = Phase8HoverTests.Run();
        int phase9Assertions = Phase9HoverFormattingTests.Run();
        int phase10Assertions = Phase10HoverRuntimeTests.Run();
        int phase11Assertions = Phase11DiagnosticsTests.Run();
        int phase12Assertions = Phase12EftHoverIntegrationTests.Run();
        int phase13Assertions = Phase13RuntimeBootstrapTests.Run();
        Console.WriteLine("Item Intelligence: " + assertions + " Phase 1, " + phase2Assertions + " Phase 2, " + phase3Assertions + " Phase 3, " + phase4Assertions + " Phase 4, " + phase5Assertions + " Phase 5, " + phase6Assertions + " Phase 6, " + phase7Assertions + " Phase 7, " + phase8Assertions + " Phase 8, " + phase9Assertions + " Phase 9, " + phase10Assertions + " Phase 10, " + phase11Assertions + " Phase 11, " + phase12Assertions + " Phase 12 and " + phase13Assertions + " Phase 13 assertions passed.");
    }

    static ItemDefinition Resolve(ItemRegistry registry, string id, string type, IDictionary<string, object> signals) => registry.Resolve(new ItemDescriptor(id, null, "  Test   Item  ", " Test ", type, signals));
    static void Expect(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException("Assertion failed: " + message); }
    sealed class FastPathItem { public string _tpl = " FAST-PATH "; public object Template => throw new InvalidOperationException("Full descriptor reflection should not run on a cache hit."); }
    sealed class FakeItem { public FakeTemplate Template; }
    sealed class FakeTemplate { public string _id; public string _parent; public string _name; public string ShortName; public FakeProps Props; }
    sealed class FakeProps { public int Energy; }
}
