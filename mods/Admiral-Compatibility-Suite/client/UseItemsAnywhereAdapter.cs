using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using HarmonyLib;

namespace AdmiralCompatibilitySuite;

internal sealed class UseItemsAnywhereAdapter : IDisposable
{
    internal const string UpstreamPluginGuid = "com.cj.useFromAnywhere";
    private const int DedicatedBeltSlotValue = 15;
    private const string HarmonyOwner = "com.admiralam.compatibility-suite.use-items-anywhere-belt";
    private static readonly string[] SlotListFields =
    {
        "_weaponSlots", "GrenadeThrowSlots", "_meleeSlots", "FlareSlots",
        "ReloadSlots", "MedsSlots", "FoodDrinkSlots", "AllOtherItems"
    };

    private readonly Action<string> logInfo;
    private readonly Action<string> logWarning;
    private object belt;
    private Harmony harmony;
    private static MethodInfo enumerateBeltSources;

    internal UseItemsAnywhereAdapter(Action<string> logInfo, Action<string> logWarning)
    {
        this.logInfo = logInfo;
        this.logWarning = logWarning;
    }

    internal bool TryInstall()
    {
        try
        {
            Type configuration = FindUniqueType("UseItemsAnywhere.Configuration");
            Type equipmentSlot = FindUniqueType("EFT.InventoryLogic.EquipmentSlot");
            if (configuration == null || equipmentSlot == null)
            {
                return false;
            }

            belt = Enum.ToObject(equipmentSlot, DedicatedBeltSlotValue);
            Type beltApi = FindUniqueType("SPTBeltArmbandInventory.BeltAccessApi")
                ?? throw new TypeLoadException("BeltAccessApi is unavailable.");
            enumerateBeltSources = beltApi.GetMethod(
                "TryEnumerateBeltSources",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(object), typeof(object[]).MakeByRefType() },
                null) ?? throw new MissingMethodException(beltApi.FullName, "TryEnumerateBeltSources");
            int eligible = 0;
            int extended = 0;
            int alreadyExtended = 0;
            foreach (string fieldName in SlotListFields)
            {
                FieldInfo field = configuration.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field?.GetValue(null) is not ConfigEntryBase entry)
                {
                    continue;
                }

                if (entry.BoxedValue is not IList list)
                {
                    continue;
                }

                eligible++;
                if (!list.Contains(belt))
                {
                    list.Add(belt);
                    entry.BoxedValue = list;
                    extended++;
                }
                else
                {
                    alreadyExtended++;
                }
            }

            if (eligible == 0)
            {
                throw new InvalidOperationException("Use Items Anywhere exposes no configurable access list.");
            }

            harmony = new Harmony(HarmonyOwner);
            var postfix = new HarmonyMethod(typeof(UseItemsAnywhereAdapter), nameof(IncludeBeltItem))
            {
                priority = Priority.Last
            };
            harmony.Patch(
                AccessTools.Method(typeof(InventoryController), nameof(InventoryController.IsAtBindablePlace)),
                postfix: postfix);
            harmony.Patch(
                AccessTools.Method(typeof(InventoryController), nameof(InventoryController.IsAtReachablePlace)),
                postfix: postfix);

            logInfo?.Invoke($"Admiral Compatibility Suite verified {eligible} Use Items Anywhere access lists: enabled Belt slot15 in {extended}, already enabled in {alreadyExtended}.");
            return true;
        }
        catch (Exception exception)
        {
            Dispose();
            logWarning?.Invoke($"Admiral Compatibility Suite Use Items Anywhere adapter failed safely: {exception.GetType().FullName}: {exception.Message}");
            return false;
        }
    }

    private static void IncludeBeltItem(InventoryController __instance, Item item, ref bool __result)
    {
        if (__result || __instance?.Inventory == null || item == null || enumerateBeltSources == null)
            return;

        object[] arguments = { __instance.Inventory, null };
        if (enumerateBeltSources.Invoke(null, arguments) is true
            && arguments[1] is object[] sources
            && Array.IndexOf(sources, item) >= 0)
        {
            __result = true;
        }
    }

    private static Type FindUniqueType(string fullName)
    {
        Type[] matches = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .Where(type => type != null)
            .Distinct()
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public void Dispose()
    {
        harmony?.UnpatchSelf();
        harmony = null;
        enumerateBeltSources = null;
        belt = null;
    }
}
