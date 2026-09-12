using System;
using System.Collections;
using System.Reflection;
using BepInEx.Configuration;

namespace SPTBeltArmbandInventory
{
    internal sealed class UseItemsAnywhereCompatibility : IDisposable
    {
        internal const string PluginGuid = "com.cj.useFromAnywhere";
        static readonly string[] SlotListFields =
        {
            "_weaponSlots", "GrenadeThrowSlots", "_meleeSlots", "FlareSlots",
            "ReloadSlots", "MedsSlots", "FoodDrinkSlots", "AllOtherItems"
        };

        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object armBand;
        object belt;

        internal UseItemsAnywhereCompatibility(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall(Type equipmentSlotType)
        {
            try
            {
                Type configuration = ReflectionTools.FindType("UseItemsAnywhere.Configuration");
                if (configuration == null) return false;
                armBand = Enum.Parse(equipmentSlotType, BeltSlotPlan.ArmBand, false);
                belt = Enum.ToObject(equipmentSlotType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);

                int extended = 0;
                foreach (string fieldName in SlotListFields)
                {
                    FieldInfo field = configuration.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    ConfigEntryBase entry = field?.GetValue(null) as ConfigEntryBase;
                    if (entry == null) continue;
                    if (EnsureBeltFollowsArmBand(entry)) extended++;
                }

                if (extended == 0) throw new InvalidOperationException("Use Items Anywhere has no ArmBand-enabled access list to extend.");
                logInfo?.Invoke("B&A&HB Use Items Anywhere compatibility installed: pseudo-slot15 follows ArmBand in " + extended + " configured access lists; binding/reload use the dedicated Belt slot without editing the foreign DLL.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                logWarning?.Invoke("B&A&HB Use Items Anywhere compatibility failed safely: " + exception.GetType().FullName + ": " + exception.Message);
                return false;
            }
        }

        bool EnsureBeltFollowsArmBand(ConfigEntryBase entry)
        {
            IList list = entry.BoxedValue as IList;
            if (list == null || !Contains(list, armBand) || Contains(list, belt)) return false;
            list.Add(belt);
            entry.BoxedValue = list;
            return true;
        }

        static bool Contains(IList list, object value)
        {
            for (int i = 0; i < list.Count; i++) if (Equals(list[i], value)) return true;
            return false;
        }

        public void Dispose()
        {
            armBand = null;
            belt = null;
        }
    }
}
