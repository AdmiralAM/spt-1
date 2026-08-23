using System;
using System.Collections;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class ReflectionTools
    {
        internal static Type FindType(string fullName)
        {
            Type direct = Type.GetType(fullName + ", Assembly-CSharp", false);
            if (direct != null) return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(fullName, false);
                if (found != null) return found;
            }
            return null;
        }

        internal static object ReadMember(object instance, string preferredName)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(preferredName, flags);
            if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(instance, null);
            FieldInfo field = type.GetField(preferredName, flags);
            return field == null ? null : field.GetValue(instance);
        }

        internal static bool ReadBoolean(object instance, string preferredName)
        {
            object value = ReadMember(instance, preferredName);
            return value is bool && (bool)value;
        }

        internal static bool HasContainers(object item)
        {
            if (item == null) return false;
            if (ReadBoolean(item, "IsContainer")) return true;
            if (HasAny(ReadMember(item, "Containers"))) return true;
            if (HasAny(ReadMember(item, "Grids"))) return true;

            // EFT container items commonly expose their usable grids on Template rather than
            // directly on the runtime Item instance. Pack 'n' Strap belts are grid-backed
            // armband items, so checking only Item.Containers incorrectly classified them
            // as plain armbands and prevented the belt row from ever appearing.
            object template = ReadMember(item, "Template");
            if (template != null)
            {
                if (ReadBoolean(template, "IsContainer")) return true;
                if (HasAny(ReadMember(template, "Containers"))) return true;
                if (HasAny(ReadMember(template, "Grids"))) return true;
            }

            return false;
        }

        static bool HasAny(object value)
        {
            if (value == null || value is string) return false;
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null) return false;
            IEnumerator enumerator = enumerable.GetEnumerator();
            try { return enumerator.MoveNext(); }
            finally
            {
                IDisposable disposable = enumerator as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
        }
    }
}
