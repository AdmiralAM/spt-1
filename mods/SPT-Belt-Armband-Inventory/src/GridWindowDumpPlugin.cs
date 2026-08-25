using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx;
using UnityEngine;

namespace SPTBeltArmbandInventory.Diagnostics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class GridWindowDumpPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.admiralam.spt.belt-grid-window-dump";
        public const string PluginName = "B&A&HB GridWindow Full Dump";
        public const string PluginVersion = "0.1.0";

        private const string TargetTypeName = "EFT.UI.GridWindow";
        private const string TargetObjectName = "Grid Window Template(Clone)";
        private bool dumped;
        private Type gridWindowType;
        private object harmony;
        private MethodInfo unpatchSelf;
        private static GridWindowDumpPlugin instance;

        private void Awake()
        {
            instance = this;
            try
            {
                gridWindowType = FindType(TargetTypeName);
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                if (gridWindowType == null || harmonyType == null || harmonyMethodType == null)
                    throw new InvalidOperationException("GridWindow or Harmony runtime type was not found");

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (patchMethod == null || harmonyMethodConstructor == null || unpatchSelf == null)
                    throw new InvalidOperationException("Harmony patch API was not found");

                harmony = Activator.CreateInstance(harmonyType, new object[] { PluginGuid });
                MethodInfo factory = typeof(GridWindowDumpPlugin).GetMethod(nameof(PostfixFactory), BindingFlags.Static | BindingFlags.NonPublic);
                int patched = 0;
                MethodInfo[] methods = gridWindowType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "Show", StringComparison.Ordinal) || method.IsAbstract || method.ContainsGenericParameters)
                        continue;
                    // Do not capture Window<T>.Show(): select the real GridWindow binding overload
                    // by its runtime signature, not by declaring type (the client may inherit it).
                    ParameterInfo[] showParameters = method.GetParameters();
                    if (showParameters.Length != 5
                        || showParameters[0].ParameterType.FullName != "EFT.InventoryLogic.CompoundItem")
                        continue;

                    object postfix = harmonyMethodConstructor.Invoke(new object[] { factory });
                    patchMethod.Invoke(harmony, new[] { method, null, postfix, null, null });
                    patched++;
                }

                if (patched == 0)
                    throw new InvalidOperationException("No GridWindow.Show overloads were found");

                Logger.LogInfo("[GRID-DUMP] Armed on " + patched + " GridWindow.Show overload(s). Open the empty Belt window once.");
                WriteMarker();
            }
            catch (Exception exception)
            {
                Exception root = Unwrap(exception);
                Logger.LogError("[GRID-DUMP] INSTALL FAILED: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        private void Update()
        {
            if (dumped || gridWindowType == null)
                return;

            try
            {
                UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(gridWindowType);
                for (int i = 0; i < objects.Length; i++)
                {
                    Component component = objects[i] as Component;
                    if (component == null || component.gameObject == null
                        || !string.Equals(component.gameObject.name, TargetObjectName, StringComparison.Ordinal)
                        || !component.gameObject.activeInHierarchy)
                        continue;

                    WriteDump(component, null, "UNITY_OBJECT_SCAN (Show hook was not reached)");
                    dumped = true;
                    break;
                }
            }
            catch (Exception exception)
            {
                Logger.LogError("[GRID-DUMP] OBJECT SCAN FAILED: " + exception);
            }
        }

        private void WriteMarker()
        {
            try
            {
                string path = Path.Combine(Paths.GameRootPath, "GridWindow_dump.txt");
                File.WriteAllText(path,
                    "B&A&HB GRIDWINDOW DUMP ARMED\r\nUTC=" + DateTime.UtcNow.ToString("O")
                    + "\r\nSPT_ROOT=" + Paths.GameRootPath + "\r\n",
                    new UTF8Encoding(false));
                Logger.LogInfo("[GRID-DUMP] MARKER: " + path);
            }
            catch (Exception exception)
            {
                Logger.LogError("[GRID-DUMP] MARKER FAILED: " + exception);
            }
        }

        private static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo originalMethod = original as MethodInfo;
            if (originalMethod == null || originalMethod.DeclaringType == null)
                return null;

            DynamicMethod postfix = new DynamicMethod(
                "GridWindowFullDumpPostfix",
                typeof(void),
                new[] { originalMethod.DeclaringType, typeof(object[]) },
                typeof(GridWindowDumpPlugin),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            postfix.DefineParameter(2, ParameterAttributes.None, "__args");
            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldstr, originalMethod.ToString());
            il.Emit(OpCodes.Call, typeof(GridWindowDumpPlugin).GetMethod(nameof(Probe), BindingFlags.Static | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        private static void Probe(object target, object[] args, string showSignature)
        {
            GridWindowDumpPlugin current = instance;
            if (current == null || current.dumped || target == null)
                return;

            Component component = target as Component;
            if (component == null || !string.Equals(component.gameObject.name, TargetObjectName, StringComparison.Ordinal))
                return;

            current.WriteDump(component, args, showSignature);
            current.dumped = true;
        }

        private static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 5 && parameters[0].ParameterType == typeof(MethodBase) && parameters[1].ParameterType == harmonyMethodType)
                    return method;
            }

            return null;
        }

        private static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException && current.InnerException != null)
                current = current.InnerException;
            return current;
        }

        private void OnDestroy()
        {
            try
            {
                if (harmony != null && unpatchSelf != null)
                    unpatchSelf.Invoke(harmony, null);
            }
            catch
            {
            }

            if (ReferenceEquals(instance, this))
                instance = null;
        }

        private void WriteDump(UnityEngine.Object target, object[] args, string showSignature)
        {
            try
            {
                string path = Path.Combine(Paths.GameRootPath, "GridWindow_dump.txt");
                StringBuilder output = new StringBuilder(256 * 1024);
                HashSet<object> visited = new HashSet<object>(ReferenceComparer.Instance);

                output.AppendLine("B&A&HB GRIDWINDOW FULL RUNTIME DUMP");
                output.AppendLine("UTC=" + DateTime.UtcNow.ToString("O"));
                output.AppendLine("SPT_ROOT=" + Paths.GameRootPath);
                output.AppendLine("TARGET_TYPE=" + target.GetType().AssemblyQualifiedName);
                output.AppendLine("TARGET_NAME=" + target.name);
                output.AppendLine("SHOW_SIGNATURE=" + showSignature);
                output.AppendLine("SHOW_ARGUMENT_COUNT=" + (args == null ? "<null>" : args.Length.ToString()));
                if (args != null)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        object argument = args[i];
                        output.AppendLine("SHOW_ARGUMENT[" + i + "]=" + DescribeArgument(argument));
                    }
                }
                output.AppendLine();

                DumpObject(output, "GRID_WINDOW", target, visited, 0, 3);

                Component component = target as Component;
                if (component != null)
                {
                    output.AppendLine();
                    output.AppendLine("===== COMPLETE UI HIERARCHY =====");
                    DumpHierarchy(output, component.transform, visited, 0);
                }

                File.WriteAllText(path, output.ToString(), new UTF8Encoding(false));
                Logger.LogInfo("[GRID-DUMP] COMPLETE: " + path);
            }
            catch (Exception exception)
            {
                Logger.LogError("[GRID-DUMP] FAILED: " + exception);
            }
        }

        private static string DescribeArgument(object value)
        {
            if (value == null)
                return "<null>";

            try
            {
                Type type = value.GetType();
                string text = "type=" + (type.FullName ?? type.Name);
                if (value is UnityEngine.Object unityObject)
                    text += ", unityName=\"" + unityObject.name + "\"";

                foreach (string memberName in new[] { "TemplateId", "Tpl", "Id", "Item", "Template", "Grid", "ItemContext", "Owner" })
                {
                    try
                    {
                        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (property != null && property.GetIndexParameters().Length == 0)
                        {
                            object nested = property.GetValue(value, null);
                            text += ", " + memberName + "=" + FormatValue(nested);
                            continue;
                        }

                        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field != null)
                            text += ", " + memberName + "=" + FormatValue(field.GetValue(value));
                    }
                    catch
                    {
                    }
                }

                return text;
            }
            catch
            {
                return "type=" + value.GetType().FullName;
            }
        }

        private static void DumpHierarchy(StringBuilder output, Transform transform, HashSet<object> visited, int depth)
        {
            string indent = new string(' ', depth * 2);
            GameObject gameObject = transform.gameObject;
            output.AppendLine(indent + "GAMEOBJECT name=\"" + gameObject.name + "\" path=\"" + GetPath(transform)
                + "\" activeSelf=" + gameObject.activeSelf + " activeInHierarchy=" + gameObject.activeInHierarchy
                + " layer=" + gameObject.layer + " tag=" + SafeTag(gameObject));

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    output.AppendLine(indent + "  COMPONENT <missing/null>");
                    continue;
                }

                output.AppendLine(indent + "  COMPONENT[" + i + "] " + component.GetType().AssemblyQualifiedName);
                DumpObject(output, indent + "  COMPONENT[" + i + "]", component, visited, 0, 1);
            }

            for (int i = 0; i < transform.childCount; i++)
                DumpHierarchy(output, transform.GetChild(i), visited, depth + 1);
        }

        private static void DumpObject(
            StringBuilder output,
            string label,
            object value,
            HashSet<object> visited,
            int depth,
            int maxDepth)
        {
            if (value == null)
            {
                output.AppendLine(label + "=<null>");
                return;
            }

            Type runtimeType = value.GetType();
            output.AppendLine(label + ".RUNTIME_TYPE=" + runtimeType.AssemblyQualifiedName);
            output.AppendLine(label + ".VALUE=" + FormatValue(value));

            if (IsTerminal(runtimeType) || depth >= maxDepth)
                return;

            if (!runtimeType.IsValueType && !visited.Add(value))
            {
                output.AppendLine(label + ".RECURSION=<already visited>");
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
                DumpEnumerable(output, label, enumerable, visited, depth, maxDepth);

            for (Type declaredType = runtimeType; declaredType != null; declaredType = declaredType.BaseType)
            {
                output.AppendLine(label + ".DECLARED_BY=" + declaredType.AssemblyQualifiedName);
                FieldInfo[] fields = declaredType.GetFields(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    string memberLabel = label + ".FIELD " + declaredType.FullName + "." + field.Name;
                    try
                    {
                        object fieldValue = field.GetValue(field.IsStatic ? null : value);
                        output.AppendLine(memberLabel + " TYPE=" + field.FieldType.AssemblyQualifiedName + " VALUE=" + FormatValue(fieldValue));
                        if (ShouldExpand(fieldValue, depth, maxDepth))
                            DumpObject(output, memberLabel, fieldValue, visited, depth + 1, maxDepth);
                    }
                    catch (Exception exception)
                    {
                        output.AppendLine(memberLabel + "=<ERROR " + Flatten(exception) + ">");
                    }
                }
            }

            PropertyInfo[] properties = runtimeType.GetProperties(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                string memberLabel = label + ".PROPERTY " + property.DeclaringType.FullName + "." + property.Name;
                try
                {
                    MethodInfo getter = property.GetGetMethod(true);
                    object propertyValue = property.GetValue(getter != null && getter.IsStatic ? null : value, null);
                    output.AppendLine(memberLabel + " TYPE=" + property.PropertyType.AssemblyQualifiedName + " VALUE=" + FormatValue(propertyValue));
                }
                catch (Exception exception)
                {
                    output.AppendLine(memberLabel + "=<ERROR " + Flatten(exception) + ">");
                }
            }
        }

        private static void DumpEnumerable(
            StringBuilder output,
            string label,
            IEnumerable enumerable,
            HashSet<object> visited,
            int depth,
            int maxDepth)
        {
            int index = 0;
            try
            {
                foreach (object item in enumerable)
                {
                    string itemLabel = label + ".ITEM[" + index + "]";
                    output.AppendLine(itemLabel + "=" + FormatValue(item));
                    if (index < 128 && ShouldExpand(item, depth, maxDepth))
                        DumpObject(output, itemLabel, item, visited, depth + 1, maxDepth);

                    index++;
                    if (index >= 2048)
                    {
                        output.AppendLine(label + ".ITEMS=<truncated after 2048 entries>");
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                output.AppendLine(label + ".ENUMERATION=<ERROR " + Flatten(exception) + ">");
            }

            output.AppendLine(label + ".ENUMERATED_COUNT=" + index);
        }

        private static bool ShouldExpand(object value, int depth, int maxDepth)
        {
            if (value == null || depth >= maxDepth)
                return false;

            Type type = value.GetType();
            if (IsTerminal(type))
                return false;

            UnityEngine.Object unityObject = value as UnityEngine.Object;
            if (unityObject != null)
                return !(unityObject is GameObject) && !(unityObject is Transform);

            return true;
        }

        private static bool IsTerminal(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(Guid)
                || type == typeof(Type);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "<null>";

            try
            {
                UnityEngine.Object unityObject = value as UnityEngine.Object;
                if (unityObject != null)
                {
                    Component component = unityObject as Component;
                    string path = component != null ? GetPath(component.transform) : string.Empty;
                    return "UnityObject(type=" + value.GetType().FullName + ", name=\"" + unityObject.name
                        + "\", instanceId=" + unityObject.GetInstanceID() + (path.Length > 0 ? ", path=\"" + path + "\"" : string.Empty) + ")";
                }

                string text = value.ToString();
                if (text == null)
                    return "<ToString returned null>";

                text = text.Replace("\r", "\\r").Replace("\n", "\\n");
                return text.Length <= 4096 ? text : text.Substring(0, 4096) + "<truncated>";
            }
            catch (Exception exception)
            {
                return "<unprintable " + Flatten(exception) + ">";
            }
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            StringBuilder path = new StringBuilder(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                path.Insert(0, parent.name + "/");
                parent = parent.parent;
            }

            return path.ToString();
        }

        private static string SafeTag(GameObject gameObject)
        {
            try
            {
                return gameObject.tag;
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private static string Flatten(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
                current = current.InnerException;

            return current.GetType().FullName + ": " + current.Message.Replace("\r", " ").Replace("\n", " ");
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
