using System;
using System.Collections.Generic;
using System.Reflection;

namespace SPTItemIntelligence
{
    public static class AmmoPenetrationClassPolicy
    {
        public static string Resolve(Func<int, float> penetrationChance)
        {
            if (penetrationChance == null) return string.Empty;
            int highest = 0;
            for (int armorClass = 1; armorClass <= 6; armorClass++)
            {
                float chance;
                try { chance = penetrationChance(armorClass); }
                catch { continue; }
                // EFT's MultiLineInfo labels 60-79.99% High and 80%+ Very High.
                if (!float.IsNaN(chance) && !float.IsInfinity(chance) && chance >= 60f) highest = armorClass;
            }
            switch (highest)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                case 6: return "VI";
                default: return string.Empty;
            }
        }
    }

    public static class AmmoPenetrationClassResolver
    {
        static readonly Dictionary<int, string> cache = new Dictionary<int, string>();
        static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static Type ammoTemplateType;
        static MethodInfo realResistance;
        static MethodInfo getPenetrationChance;
        static bool discoveryAttempted;

        public static bool TryResolve(object itemViewOrItem, out string romanClass)
        {
            romanClass = string.Empty;
            object item = EftItemTemplateIdResolver.ResolveItem(itemViewOrItem);
            object template = ReadMember(item, "Template");
            if (template == null) return false;
            if (!IsAmmoTemplate(template.GetType())) return true;
            object rawPower = ReadMember(template, "PenetrationPower");
            int penetrationPower;
            try { penetrationPower = Convert.ToInt32(rawPower, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return true; }
            if (penetrationPower <= 0) return true;
            string result;
            if (cache.TryGetValue(penetrationPower, out result)) { romanClass = result; return true; }
            if (!BindNativeMethods(template.GetType().Assembly)) return false;

            bool failed = false;
            result = AmmoPenetrationClassPolicy.Resolve(armorClass =>
            {
                try
                {
                    object resistance = realResistance.Invoke(null, new object[] { 100f, 100f, armorClass, (float)penetrationPower });
                    object chance = getPenetrationChance.Invoke(resistance, new object[] { (float)penetrationPower });
                    return Convert.ToSingle(chance, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch { failed = true; return float.NaN; }
            });
            if (failed) return false;
            if (cache.Count >= 512) cache.Clear();
            cache[penetrationPower] = result;
            romanClass = result;
            return true;
        }

        static bool IsAmmoTemplate(Type type)
        {
            if (ammoTemplateType == null) FindAmmoTemplateType(type.Assembly);
            if (ammoTemplateType != null) return ammoTemplateType.IsAssignableFrom(type);
            // The type check is only a compatibility fallback for EFT builds that move the
            // template class to another assembly; never infer ammo from localized names.
            for (Type current = type; current != null; current = current.BaseType)
                if (string.Equals(current.Name, "AmmoTemplate", StringComparison.Ordinal)) return true;
            return false;
        }

        static void FindAmmoTemplateType(Assembly preferred)
        {
            if (ammoTemplateType != null) return;
            if (preferred != null) ammoTemplateType = preferred.GetType("EFT.InventoryLogic.AmmoTemplate", false);
            if (ammoTemplateType != null) return;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && ammoTemplateType == null; i++)
                ammoTemplateType = assemblies[i].GetType("EFT.InventoryLogic.AmmoTemplate", false);
        }

        static bool BindNativeMethods(Assembly preferred)
        {
            if (realResistance != null && getPenetrationChance != null) return true;
            if (discoveryAttempted) return false;
            discoveryAttempted = true;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int pass = 0; pass < 2 && realResistance == null; pass++)
            {
                Assembly[] candidates = pass == 0 && preferred != null ? new[] { preferred } : assemblies;
                for (int i = 0; i < candidates.Length; i++)
                {
                    Type shotMethods = candidates[i].GetType("ShotSharedMethods", false);
                    Type resistanceType = candidates[i].GetType("ArmorResistanceData", false);
                    if (shotMethods == null || resistanceType == null) continue;
                    realResistance = shotMethods.GetMethod("RealResistance", Flags, null,
                        new[] { typeof(float), typeof(float), typeof(int), typeof(float) }, null);
                    getPenetrationChance = resistanceType.GetMethod("GetPenetrationChance", Flags, null,
                        new[] { typeof(float) }, null);
                    if (realResistance != null && getPenetrationChance != null) return true;
                    realResistance = null;
                    getPenetrationChance = null;
                }
            }
            return false;
        }

        static object ReadMember(object source, string name)
        {
            for (Type type = source == null ? null : source.GetType(); type != null; type = type.BaseType)
            {
                try
                {
                    PropertyInfo property = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                    if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(source, null);
                    FieldInfo field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
                    if (field != null) return field.GetValue(source);
                }
                catch { }
            }
            return null;
        }
    }
}
