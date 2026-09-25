using System;
using System.IO;
using System.Linq;
using SPTItemIntelligence;

static class Phase40AmmoPenetrationTests
{
    public static int Run()
    {
        int assertions = 0;
        Expect(Resolve(19.99f, 39.99f, 59.99f, 10f, 0f, 0f) == string.Empty,
            "very low, low and medium ratings do not qualify as a penetration class", ref assertions);
        Expect(Resolve(60f, 59.99f, 0f, 0f, 0f, 0f) == "I",
            "the exact High threshold counts and class I is retained", ref assertions);
        Expect(Resolve(20f, 60f, 59.99f, 0f, 0f, 0f) == "II",
            "the highest qualifying armor class wins", ref assertions);
        Expect(Resolve(0f, 0f, 60f, 79.99f, 0f, 0f) == "IV",
            "High and Very High both qualify through the last matching class", ref assertions);
        Expect(Resolve(80f, 80f, 80f, 80f, 80f, 80f) == "VI",
            "a Very High rating on class VI resolves to VI", ref assertions);
        Expect(AmmoPenetrationClassPolicy.Resolve(null) == string.Empty,
            "missing runtime rating data hides the badge cleanly", ref assertions);
        Expect(AmmoPenetrationClassPolicy.Resolve(_ => float.NaN) == string.Empty,
            "invalid native chance values never invent a class", ref assertions);
        Expect(ResolveAmmo(3) == string.Empty, "low-penetration ammo has no invented badge", ref assertions);
        Expect(ResolveAmmo(20) == "I", "class I ammo uses its template penetration power", ref assertions);
        Expect(ResolveAmmo(31) == "II", "class II ammo uses its template penetration power", ref assertions);
        Expect(ResolveAmmo(44) == "III", "class III ammo uses its template penetration power", ref assertions);
        Expect(ResolveAmmo(62) == "V", "high-penetration ammo reaches class V", ref assertions);
        Expect(ResolveAmmo(44, modded: true) == "III", "subclassed modded ammo uses the same native rating path", ref assertions);
        string nonAmmoClass;
        Expect(AmmoPenetrationClassResolver.TryResolve(new TestItem { Template = new object() }, out nonAmmoClass) && nonAmmoClass == string.Empty,
            "unknown non-ammo template remains unmarked", ref assertions);

        string root = FindRepositoryRoot();
        string module = Path.Combine(root, "mods", "SPT-Item-Intelligence");
        string runtime = File.ReadAllText(Path.Combine(module, "src", "AmmoPenetrationClass.cs"));
        string view = File.ReadAllText(Path.Combine(module, "src", "AmmoPenetrationBadgeView.cs"));
        string resolver = File.ReadAllText(Path.Combine(module, "src", "EftHoverIntegration.cs"));
        string map = File.ReadAllText(Path.Combine(module, "docs", "stable-beta-product-map.md"));
        Expect(runtime.Contains("ShotSharedMethods") && runtime.Contains("RealResistance") && runtime.Contains("GetPenetrationChance") &&
               runtime.Contains("EFT.InventoryLogic.AmmoTemplate") && runtime.Contains("PenetrationPower"),
            "the runtime reads the native EFT AmmoTemplate and BSG armor penetration calculation", ref assertions);
        Expect(view.Contains("SPTItemIntelligenceAmmoPenetration") && view.Contains("preserveAspect") &&
               view.Contains("new Color(0.005f, 0.008f, 0.01f, 1f)") && view.Contains("new Color(0.94f, 0.96f, 0.97f, 1f)"),
            "the compact original badge uses a black outline, a light Roman numeral and a shared static sprite per class", ref assertions);
        Expect(resolver.Contains("public static object ResolveItem(object itemViewOrItem)") &&
               map.Contains("`AmmoPenetrationClass.cs`") && map.Contains("`AmmoPenetrationBadgeView.cs`"),
            "visible pooled ItemViews can resolve their EFT item and the product map records both runtime units", ref assertions);
        return assertions;
    }

    static string Resolve(params float[] chances) => AmmoPenetrationClassPolicy.Resolve(armorClass => chances[armorClass - 1]);

    static string ResolveAmmo(int power, bool modded = false)
    {
        var template = modded ? (EFT.InventoryLogic.AmmoTemplate)new ModdedAmmoTemplate() : new EFT.InventoryLogic.AmmoTemplate();
        template.PenetrationPower = power;
        if (!AmmoPenetrationClassResolver.TryResolve(new TestItem { Template = template }, out string result))
            throw new InvalidOperationException("Native ammo bridge failed to resolve in test runtime.");
        return result;
    }

    sealed class TestItem { public object Template { get; set; } }
    sealed class ModdedAmmoTemplate : EFT.InventoryLogic.AmmoTemplate { }

    static string FindRepositoryRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "mods", "SPT-Item-Intelligence"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 40 assertion failed: " + message);
    }
}

namespace EFT.InventoryLogic
{
    public class AmmoTemplate { public int PenetrationPower { get; set; } }
}

public static class ShotSharedMethods
{
    public static ArmorResistanceData RealResistance(float armorMax, float armorCurrent, int armorClass, float penetrationPower)
        => new ArmorResistanceData(armorClass);
}

public sealed class ArmorResistanceData
{
    readonly int armorClass;
    public ArmorResistanceData(int armorClass) { this.armorClass = armorClass; }
    public float GetPenetrationChance(float penetrationPower) => penetrationPower - armorClass * 10f + 50f;
}
