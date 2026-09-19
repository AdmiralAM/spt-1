using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace AdmiralCompatibilitySuite;

internal sealed class UseItemsAnywhereAdapter : IDisposable
{
    internal const string UpstreamPluginGuid = "com.cj.useFromAnywhere";
    private const int DedicatedBeltSlotValue = 15;
    private static readonly string[] SlotListFields =
    {
        "_weaponSlots", "GrenadeThrowSlots", "_meleeSlots", "FlareSlots",
        "ReloadSlots", "MedsSlots", "FoodDrinkSlots", "AllOtherItems"
    };

    private readonly Action<string> logInfo;
    private readonly Action<string> logWarning;
    private object armBand;
    private object belt;

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

            armBand = Enum.Parse(equipmentSlot, "ArmBand", false);
            belt = Enum.ToObject(equipmentSlot, DedicatedBeltSlotValue);
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

                SlotAccessSyncResult result = SlotAccessSynchronizer.EnsureFollower(list, armBand, belt);
                if (result == SlotAccessSyncResult.Ineligible)
                {
                    continue;
                }

                eligible++;
                if (result == SlotAccessSyncResult.Added)
                {
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
                throw new InvalidOperationException("Use Items Anywhere exposes no ArmBand-enabled access list.");
            }

            logInfo?.Invoke($"Admiral Compatibility Suite verified {eligible} Use Items Anywhere access lists: added Belt slot15 to {extended}, already present in {alreadyExtended}.");
            return true;
        }
        catch (Exception exception)
        {
            Dispose();
            logWarning?.Invoke($"Admiral Compatibility Suite Use Items Anywhere adapter failed safely: {exception.GetType().FullName}: {exception.Message}");
            return false;
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
        armBand = null;
        belt = null;
    }
}
