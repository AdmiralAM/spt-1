using System;
using System.IO;

static class Phase35AmandsSenseRuntimeAdapterTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string module = Path.Combine(root, "mods", "SPT-Item-Intelligence");
        string adapter = File.ReadAllText(Path.Combine(module, "src", "AmandsSenseIntegration.cs"));
        string plugin = File.ReadAllText(Path.Combine(module, "src", "Plugin.cs"));
        string settings = File.ReadAllText(Path.Combine(module, "src", "UiSettings.cs"));
        string project = File.ReadAllText(Path.Combine(module, "src", "SPT-Item-Intelligence.csproj"));

        Expect(plugin.Contains("BepInDependency(\"xyz.drakia.Sense\", BepInDependency.DependencyFlags.SoftDependency)"),
            "Sense load ordering is optional and never becomes a mandatory plugin dependency", ref assertions);
        Expect(adapter.Contains("FindAssembly(\"AmandsSense\")") &&
               adapter.Contains("AmandsSense.Components.AmandsSenseItem") &&
               adapter.Contains("AmandsSense.Components.AmandsSenseContainer") &&
               adapter.Contains("AmandsSense.Components.AmandsSenseClass"),
            "the adapter discovers the installed Sense runtime without a compile-time type dependency", ref assertions);
        Expect(adapter.Contains("FindMethod(itemType, \"SetSense\", 1)") &&
               adapter.Contains("FindMethod(containerType, \"SetSense\", 1)") &&
               adapter.Contains("FindMethod(containerType, \"UpdateSense\", 0)") &&
               adapter.Contains("FindMethod(itemType, \"RemoveLootItem\", 1)") &&
               adapter.Contains("FindMethod(senseClass, \"Clear\", 0)"),
            "bounded lifecycle hooks cover presentation, pickup/drop and raid reset", ref assertions);
        Expect(settings.Contains("config.Bind(\"Amands Sense\", \"Container Name Scale\", 0.86f") &&
               adapter.Contains("ContainerUpdatePostfix") &&
               adapter.Contains("ApplyContainerNameScale(senseItem)") &&
               adapter.Contains("SetMember(nameText, \"fontSize\", settings.SenseContainerNameScale)"),
            "only the native Sense container-name line receives the bounded smaller type size", ref assertions);
        Expect(adapter.Contains("!settings.SenseIntegration || senseItem == null") &&
               adapter.Contains("!settings.SenseRequiredItems && !settings.SenseCategories && !wantsContainerValue") &&
               plugin.Contains("senseIntegration.Dispose();"),
            "disabled integration or all Sense presentation layers exit immediately, and disposal removes runtime patches", ref assertions);
        Expect(adapter.Contains("ledger.Evaluate(templateId, allocation, fir)") &&
               adapter.Contains("SenseContainerPolicyEngine.Select(candidates, fallbackCategory") &&
               adapter.Contains("!hasRequirementPolicy && !hasContainerValue"),
            "requirements override native visuals first, then native value marks, then contextual Sense categories", ref assertions);
        Expect(adapter.Contains("BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic"),
            "Sense 3.1 static Clear lifecycle method is discovered", ref assertions);
        Expect(adapter.Contains("icon_quest.png") && adapter.Contains("icon_barter_building.png") && adapter.Contains("icon_info.png") &&
               adapter.Contains("icon_provisions_food.png") && adapter.Contains("icon_provisions_drinks.png") &&
               adapter.Contains("icon_keys_mechanic.png") && adapter.Contains("icon_weapons_throw.png") &&
               adapter.Contains("icon_money.png") && adapter.Contains("icon_fav_checked.png"),
            "the adapter reuses Sense-owned runtime sprites instead of copying or shipping its assets", ref assertions);
        Expect(adapter.Contains("ledger.Observe(id, template, stack, fir)") &&
               adapter.Contains("pickedItemIds.Remove(itemId) && ledger.Remove(itemId)") &&
               adapter.Contains("ledger.Reset();") && adapter.Contains("pickedItemIds.Clear();"),
            "successive pickup, returned loot and raid reset update reservations without polling", ref assertions);
        Expect(adapter.Contains("if (!settings.SenseRequiredItems)") && adapter.Contains("evaluationCache.Clear();") &&
               adapter.Contains("if (raidChanged != null) raidChanged();"),
            "container category and value views refresh after successful loot removal even with requirement markers disabled", ref assertions);
        Expect(settings.Contains("config.Bind(\"Amands Sense\", \"Integration\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Required Items\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Category Markers\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Container Value Colors\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Secondary Reason Outline\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Remaining Count Text\", true"),
            "the required Sense layers are independently configurable in Item Intelligence F12", ref assertions);
        Expect(settings.Contains("new Color(33f / 255f, 168f / 255f, 1.00f)") &&
               settings.Contains("BrightnessEntry(config, \"50k\", 0.70f") &&
               settings.Contains("BrightnessEntry(config, \"100k\", 0.85f") &&
               settings.Contains("BrightnessEntry(config, \"200k\", 0.95f") &&
               settings.Contains("BrightnessEntry(config, \"500k Plus\", 1.00f"),
            "water keeps the installed Sense drink blue while container value settings control brightness, not category hue", ref assertions);
        Expect(settings.Contains("\"Amands Sense Colors\", \"Active Quest\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Hideout\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Future Quest\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Food\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Water\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Keys\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Grenades\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Currency\"") &&
               settings.Contains("\"Amands Sense Container Value Brightness\"") &&
               settings.Contains("\"Amands Sense Container Value Colors\"") &&
               settings.Contains("\"50k to 100k\"") && settings.Contains("\"100k to 200k\"") && settings.Contains("\"200k Plus\"") &&
               settings.Contains("new Color(0.20f, 0.52f, 1.00f)") &&
               settings.Contains("new Color(1.00f, 0.91f, 0.45f)") &&
               settings.Contains("new Color(1.00f, 0.86f, 0.12f)") &&
               settings.Contains("\"500k Plus\"") &&
               settings.Contains("\"Amands Sense Count Colors\", \"One Item\"") &&
               settings.Contains("\"Amands Sense Count Colors\", \"Two to Three\"") &&
               settings.Contains("\"Amands Sense Count Colors\", \"Four Plus\""),
            "Sense categories and container count ranges remain independently configurable", ref assertions);
        Expect(adapter.Contains("icon_provisions_food.png") && adapter.Contains("icon_provisions_drinks.png") &&
               adapter.Contains("icon_keys_mechanic.png") && adapter.Contains("icon_weapons_throw.png") &&
               adapter.Contains("icon_money.png") && adapter.Contains("settings.GetSenseContainerValueBrightness(containerTotalValue)") &&
               adapter.Contains("if (icon == ItemNeedIcon.Food) return \"icon_provisions_food.png\"") &&
               adapter.Contains("if (icon == ItemNeedIcon.Water) return \"icon_provisions_drinks.png\""),
            "Sense uses its owned category icons and applies the aggregate container-value color tier", ref assertions);
        Expect(adapter.Contains("IsContainerRoot(senseItem, item, contained)") &&
               adapter.Contains("state.Price.FleaUnitValue") && !adapter.Contains("settings.ValueMode == ItemValueMode.Flea") &&
               adapter.Contains("Color valueBase = policy.HasItemIntelligence ? primary : settings.GetSenseContainerValueColor(containerTotalValue)") &&
               adapter.Contains("ApplyContainerValueBrightness(valueBase, settings.GetSenseContainerValueBrightness(containerTotalValue))") &&
               adapter.Contains("hasRequirementPolicy ? primary : hasContainerValue ? valueColor : primary") &&
               !adapter.Contains("AppendNativeContainerValue") && !adapter.Contains(" ₽</color>"),
            "containers without a requirement/category receive value-band tint while semantic categories keep their hue and no price label is added", ref assertions);
        Expect(adapter.Contains("value.evaluationCache.Remove(__instance)") && adapter.Contains("value.Apply(__instance)") &&
               adapter.Contains("Color ApplyContainerValueBrightness(Color categoryColor, float brightness)"),
            "each Sense container rescan invalidates its old contents and reapplies category color plus value brightness", ref assertions);
        Expect(adapter.Contains("GameUiText.T(\"CURRENCY\", \"ВАЛЮТА\")") &&
               adapter.Contains("currencyCode == \"USD\" ? \"$\" : currencyCode == \"EUR\" ? \"€\" : currencyCode == \"RUB\" ? \"₽\"") &&
               adapter.Contains("if (icon == ItemNeedIcon.Currency) return \"icon_money.png\""),
            "money-only containers retain a dedicated localized currency label, denomination and money sprite", ref assertions);
        int protectedVisualMethod = adapter.IndexOf("static bool HasProtectedSenseVisual(object senseItem)", StringComparison.Ordinal);
        int containerProtection = adapter.IndexOf("if (IsContainer(senseItem)) return false;", protectedVisualMethod, StringComparison.Ordinal);
        int nativeProtection = adapter.IndexOf("if (type == \"Valuables\"", protectedVisualMethod, StringComparison.Ordinal);
        Expect(protectedVisualMethod >= 0 && containerProtection > protectedVisualMethod && containerProtection < nativeProtection,
            "container aggregation can show its category and flea value tint without inheriting a child QuestItems marker", ref assertions);
        Expect(adapter.Contains("type == \"ElectronicKeys\" || type == \"MechanicalKeys\"") &&
               adapter.Contains("type == \"KappaItems\" || type == \"RareItems\" || type == \"WishList\"") &&
               adapter.Contains("if (IsContainer(senseItem)) return false") &&
               adapter.Contains("if (type == \"QuestItems\") return !IsContainer(senseItem)") &&
               adapter.Contains("ItemNeedReason[] order = { ItemNeedReason.Food, ItemNeedReason.Water, ItemNeedReason.Grenade, ItemNeedReason.Key, ItemNeedReason.Currency }") &&
               adapter.Contains("if (icon == ItemNeedIcon.Currency) return \"icon_money.png\""),
            "II key markers can replace the native electronic/mechanical key category while rare/favorite visuals and text restore safely", ref assertions);
        Expect(adapter.Contains("Text(Member(senseItem, \"senseItemType\")) == \"ElectronicKeys\"") &&
               adapter.Contains("icon_keys_electronic.png") && adapter.Contains("icon_keys_mechanic.png"),
            "mechanical and electronic keys use their distinct native Sense key images", ref assertions);
        Expect(!project.Contains("AmandsSense", StringComparison.OrdinalIgnoreCase) &&
               !adapter.Contains("using AmandsSense", StringComparison.OrdinalIgnoreCase),
            "Amands Sense is not a build dependency and remains optional", ref assertions);
        Expect(!adapter.Contains("File.Write") && !adapter.Contains("Items.json") && !adapter.Contains("Sense.cfg"),
            "the integration never rewrites Sense files or user configuration", ref assertions);
        Expect(adapter.Contains("Member(item, \"Containers\")") && adapter.Contains("\"ContainedItems\"") &&
               adapter.Contains("foreach (object picked in EnumerateItemTree(item))"),
            "Sense integration traverses EFT containers for both markers and picked-item accounting", ref assertions);
        Expect(adapter.Contains("Member(senseItem, \"lootableContainer\")") &&
               adapter.Contains("Member(lootableContainer, \"ItemOwner\", \"Owner\")") &&
               adapter.Contains("Member(owner, \"RootItem\")") &&
               adapter.Contains("\"Items\", \"AllItems\", \"AllRealPlayerItems\""),
            "Sense world containers are evaluated from the lootable container item owner", ref assertions);
        Expect(adapter.Contains("SenseEvaluationCache") && adapter.Contains("evaluationCache.Count >= 512") &&
               adapter.Contains("cached.LedgerRevision == ledger.Revision") && adapter.Contains("ReferenceEquals(cached.Index, index)"),
            "repeated Sense rendering reuses bounded evaluations until requirements or raid inventory change", ref assertions);
        Expect(adapter.Contains("\"Succeed\", \"Succeeded\", \"Success\", \"IsSuccess\"") &&
               adapter.Contains("if (status == null) return true"),
            "pickup completion accepts the runtime result shapes used by Sense 3.1", ref assertions);
        Expect(adapter.Contains("CompactText(textPolicy, primary, stock, isContainer") &&
               adapter.Contains("completedContainer") && adapter.Contains("if (isContainer && policy.Stock == SenseStockState.Complete) return string.Empty") &&
               !adapter.Contains("ALL ✓") && !adapter.Contains("ВСЁ ✓") &&
               !adapter.Contains(">\\n<"),
            "completed containers collapse to a green check while other Sense status stays on one compact line", ref assertions);
        Expect(adapter.Contains("Member(senseItem, \"descriptionText\")") &&
               adapter.Contains("Member(senseItem, \"itemCount\")") &&
               adapter.Contains("SetMember(nativeCount, \"color\", countColor)") &&
               !adapter.Contains("policy.ItemCount + \"</color>\""),
            "count bands color Sense's native container counter without duplicating a category count", ref assertions);

        return assertions;
    }

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
        if (!condition) throw new InvalidOperationException("Phase 35 assertion failed: " + message);
    }
}
