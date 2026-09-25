using System;
using System.IO;
using System.Linq;
using SPTItemIntelligence;

static class Phase40AmmoPenetrationTests
{
    public static int Run()
    {
        int assertions = 0;
        Expect(Resolve(19.99f, 39.99f, 59.99f, 10f, 0f, 0f) == "III",
            "a medium chance can identify the highest useful penetration tier", ref assertions);
        Expect(Resolve(60f, 59.99f, 0f, 0f, 0f, 0f) == "II",
            "the higher useful class wins while its own chance drives the color", ref assertions);
        Expect(Resolve(20f, 60f, 59.99f, 0f, 0f, 0f) == "III",
            "the highest qualifying armor class wins", ref assertions);
        Expect(Resolve(0f, 0f, 60f, 79.99f, 0f, 0f) == "IV",
            "the highest chance of at least 20% selects its armor tier", ref assertions);
        Expect(Resolve(80f, 80f, 80f, 80f, 80f, 80f) == "VI",
            "a Very High rating on class VI resolves to VI", ref assertions);
        float selectedChance;
        Expect(AmmoPenetrationClassPolicy.Resolve(i => i == 4 ? 32f : i < 4 ? 85f : 0f, out selectedChance) == "IV" && selectedChance == 32f,
            "the shield color uses the chosen class chance, not an easier class", ref assertions);
        Expect(AmmoPenetrationClassPolicy.Resolve(null) == string.Empty,
            "missing runtime rating data hides the badge cleanly", ref assertions);
        Expect(AmmoPenetrationClassPolicy.Resolve(_ => float.NaN) == string.Empty,
            "invalid native chance values never invent a class", ref assertions);
        Expect(Resolve(0f, 0f, 0f, 0f, 0f, 0f) == string.Empty,
            "zero penetration chance hides the badge", ref assertions);
        Expect(Resolve(9f, 0f, 0f, 0f, 0f, 0f) == "I",
            "a real very-low chance on class I remains visible", ref assertions);
        Expect(ResolveAmmo(3) == "III", "lower-penetration ammo uses its own native rating", ref assertions);
        Expect(ResolveAmmo(20) == "V", "the highest 20-percent tier is selected", ref assertions);
        Expect(ResolveAmmo(31) == "VI", "class VI can carry a low-chance color", ref assertions);
        Expect(ResolveAmmo(44) == "VI", "class VI can carry a medium-chance color", ref assertions);
        Expect(ResolveAmmo(62) == "VI", "high-penetration ammo reaches class VI", ref assertions);
        Expect(ResolveAmmo(44, modded: true) == "VI", "subclassed modded ammo uses the same native rating path", ref assertions);
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
        Expect(view.Contains("SPTItemIntelligenceAmmoPenetration") &&
               view.Contains("EFT.Utilities.ResourcesCache") &&
               view.Contains("Mod Types/icon_type_mod_armor_plate_") &&
               view.Contains("ShieldColor(chance)") && !view.Contains("GetShieldSprite") &&
               view.Contains("sprite != null") && view.Contains("badgeObject.activeSelf != visible"),
            "the compact badge uses EFT's native class sprite and hides cleanly if it is unavailable", ref assertions);
        Expect(view.Contains("new Color(0.72f, 0.79f, 0.50f, 1f)") &&
               view.Contains("new Color(0.43f, 0.78f, 0.62f, 1f)"),
            "high penetration stays yellow-green while very high penetration is visibly more emerald", ref assertions);
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
