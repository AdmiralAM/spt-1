using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class ReflectionTools
    {
        sealed class MemberAccessor
        {
            internal readonly PropertyInfo Property;
            internal readonly FieldInfo Field;

            internal MemberAccessor(PropertyInfo property, FieldInfo field)
            {
                Property = property;
                Field = field;
            }

            internal object Read(object instance)
            {
                if (Property != null) return Property.GetValue(instance, null);
                return Field == null ? null : Field.GetValue(instance);
            }
        }

        static readonly object MemberCacheLock = new object();
        static readonly Dictionary<Tuple<Type, string>, MemberAccessor> MemberCache = new Dictionary<Tuple<Type, string>, MemberAccessor>();

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
            if (instance == null || string.IsNullOrEmpty(preferredName)) return null;
            MemberAccessor accessor = GetAccessor(instance.GetType(), preferredName);
            return accessor.Read(instance);
        }

        static MemberAccessor GetAccessor(Type type, string preferredName)
        {
            var key = Tuple.Create(type, preferredName);
            lock (MemberCacheLock)
            {
                MemberAccessor cached;
                if (MemberCache.TryGetValue(key, out cached)) return cached;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo property = type.GetProperty(preferredName, flags);
                if (property != null && property.GetIndexParameters().Length != 0) property = null;
                FieldInfo field = property == null ? type.GetField(preferredName, flags) : null;
                cached = new MemberAccessor(property, field);
                MemberCache.Add(key, cached);
                return cached;
            }
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
