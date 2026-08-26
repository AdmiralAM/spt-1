using System;
using System.Collections;
using System.Collections.Concurrent;
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

        static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberAccessor>> MemberCache =
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberAccessor>>();
        static readonly ConcurrentDictionary<string, Type> TypeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<Assembly, Type[]> AssemblyTypesCache = new ConcurrentDictionary<Assembly, Type[]>();

        internal static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            Type cached;
            if (TypeCache.TryGetValue(fullName, out cached)) return cached;

            Type direct = Type.GetType(fullName + ", Assembly-CSharp", false);
            if (direct != null) return CacheType(fullName, direct);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(fullName, false);
                if (found != null) return CacheType(fullName, found);
            }
            return null;
        }

        static Type CacheType(string fullName, Type type)
        {
            return TypeCache.GetOrAdd(fullName, type);
        }

        internal static object ReadMember(object instance, string preferredName)
        {
            if (instance == null || string.IsNullOrEmpty(preferredName)) return null;
            MemberAccessor accessor = GetAccessor(instance.GetType(), preferredName);
            return accessor.Read(instance);
        }

        static MemberAccessor GetAccessor(Type type, string preferredName)
        {
            ConcurrentDictionary<string, MemberAccessor> members;
            if (!MemberCache.TryGetValue(type, out members))
            {
                var createdMembers = new ConcurrentDictionary<string, MemberAccessor>(StringComparer.Ordinal);
                members = MemberCache.GetOrAdd(type, createdMembers);
            }

            MemberAccessor accessor;
            if (members.TryGetValue(preferredName, out accessor)) return accessor;
            return members.GetOrAdd(preferredName, CreateAccessor(type, preferredName));
        }

        static MemberAccessor CreateAccessor(Type type, string preferredName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(preferredName, flags);
            if (property != null && property.GetIndexParameters().Length != 0) property = null;
            FieldInfo field = property == null ? type.GetField(preferredName, flags) : null;
            return new MemberAccessor(property, field);
        }

        internal static Type[] GetTypes(Assembly assembly)
        {
            if (assembly == null) return Array.Empty<Type>();
            Type[] cached;
            if (AssemblyTypesCache.TryGetValue(assembly, out cached)) return cached;

            Type[] discovered;
            try { discovered = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { discovered = exception.Types ?? Array.Empty<Type>(); }
            catch { discovered = Array.Empty<Type>(); }
            return AssemblyTypesCache.GetOrAdd(assembly, discovered);
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
            ICollection collection = value as ICollection;
            if (collection != null) return collection.Count > 0;
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
