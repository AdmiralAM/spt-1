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
                try
                {
                    if (Property != null) return Property.GetValue(instance, null);
                    return Field == null ? null : Field.GetValue(instance);
                }
                catch (Exception exception)
                {
                    ReportFailureOnce(instance?.GetType(), Property?.Name ?? Field?.Name, "read", exception);
                    return null;
                }
            }
        }

        internal static Action<string> LogWarning;

        static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberAccessor>> MemberCache =
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberAccessor>>();
        static readonly ConcurrentDictionary<string, Type> TypeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<Assembly, Type[]> AssemblyTypesCache = new ConcurrentDictionary<Assembly, Type[]>();
        static readonly ConcurrentDictionary<string, byte> ReportedDiagnostics = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

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
            try
            {
                MemberAccessor accessor = GetAccessor(instance.GetType(), preferredName);
                return accessor.Read(instance);
            }
            catch (Exception exception)
            {
                ReportFailureOnce(instance.GetType(), preferredName, "resolve", exception);
                return null;
            }
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
            PropertyInfo selectedProperty = null;
            FieldInfo selectedField = null;
            int sameNameMembers = 0;

            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo[] properties;
                try { properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { properties = Array.Empty<PropertyInfo>(); }

                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!string.Equals(property.Name, preferredName, StringComparison.Ordinal)) continue;
                    if (property.GetIndexParameters().Length != 0) continue;
                    sameNameMembers++;
                    if (selectedProperty == null && selectedField == null) selectedProperty = property;
                }

                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { fields = Array.Empty<FieldInfo>(); }

                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!string.Equals(field.Name, preferredName, StringComparison.Ordinal)) continue;
                    sameNameMembers++;
                    if (selectedProperty == null && selectedField == null) selectedField = field;
                }
            }

            if (sameNameMembers > 1)
                ReportAmbiguityPreventedOnce(type, preferredName, sameNameMembers, selectedProperty as MemberInfo ?? selectedField);

            return new MemberAccessor(selectedProperty, selectedField);
        }

        internal static PropertyInfo FindInstanceProperty(Type type, string name, Type returnType = null)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            PropertyInfo selected = null;
            int matches = 0;
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo[] properties;
                try { properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!string.Equals(property.Name, name, StringComparison.Ordinal)) continue;
                    if (property.GetIndexParameters().Length != 0) continue;
                    if (returnType != null && property.PropertyType != returnType) continue;
                    matches++;
                    if (selected == null) selected = property;
                }
            }
            if (matches > 1) ReportAmbiguityPreventedOnce(type, name, matches, selected);
            return selected;
        }

        internal static MethodInfo FindInstanceMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            Type[] expected = parameterTypes ?? Type.EmptyTypes;
            MethodInfo selected = null;
            int matches = 0;
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods;
                try { methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal) || method.ContainsGenericParameters) continue;
                    if (returnType != null && method.ReturnType != returnType) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != expected.Length) continue;
                    bool match = true;
                    for (int p = 0; p < parameters.Length; p++)
                    {
                        if (parameters[p].ParameterType == expected[p]) continue;
                        match = false;
                        break;
                    }
                    if (!match) continue;
                    matches++;
                    if (selected == null) selected = method;
                }
            }
            if (matches > 1) ReportAmbiguityPreventedOnce(type, name, matches, selected);
            return selected;
        }

        static void ReportAmbiguityPreventedOnce(Type type, string memberName, int matches, MemberInfo selected)
        {
            if (LogWarning == null) return;
            string typeName = type?.FullName ?? "<null>";
            string key = "ambiguous|" + typeName + "|" + memberName;
            if (!ReportedDiagnostics.TryAdd(key, 0)) return;
            string selectedName = selected == null ? "<none>" : (selected.DeclaringType?.FullName + "." + selected.Name);
            LogWarning("B&A&HB REFLECTION AMBIGUITY PREVENTED: type=" + typeName
                + ", member=" + memberName
                + ", matches=" + matches
                + ", selected=" + selectedName
                + ". Caller stack:\n" + Environment.StackTrace);
        }

        static void ReportFailureOnce(Type type, string memberName, string stage, Exception exception)
        {
            if (LogWarning == null || exception == null) return;
            Exception root = exception;
            while (root is TargetInvocationException invocation && invocation.InnerException != null) root = invocation.InnerException;
            string typeName = type?.FullName ?? "<null>";
            string key = "failure|" + stage + "|" + typeName + "|" + memberName + "|" + root.GetType().FullName;
            if (!ReportedDiagnostics.TryAdd(key, 0)) return;
            LogWarning("B&A&HB REFLECTION FAIL-CLOSED: stage=" + stage
                + ", type=" + typeName
                + ", member=" + (memberName ?? "<null>")
                + ", exception=" + root.GetType().FullName + ": " + root.Message
                + (string.IsNullOrEmpty(root.StackTrace) ? "" : "\n" + root.StackTrace));
        }

        internal static void ResetDiagnostics()
        {
            LogWarning = null;
            ReportedDiagnostics.Clear();
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
            IEnumerator enumerator = null;
            try
            {
                enumerator = enumerable.GetEnumerator();
                return enumerator.MoveNext();
            }
            catch { return false; }
            finally
            {
                IDisposable disposable = enumerator as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
        }
    }
}
