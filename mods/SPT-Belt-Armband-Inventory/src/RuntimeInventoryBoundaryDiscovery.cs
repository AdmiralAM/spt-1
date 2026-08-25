using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Load-safe SPT 4.1.3 inventory contract discovery pass.
    /// This class deliberately performs no custom taxonomy/type registration.
    /// </summary>
    internal sealed class RuntimeInventoryBoundaryDiscovery
    {
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpcodeMap();

        internal RuntimeInventoryBoundaryDiscovery(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool Run()
        {
            Type searchableTemplate = Resolve("EFT.InventoryLogic.SearchableItemTemplate");
            Type searchableItem = Resolve("EFT.InventoryLogic.SearchableItem");
            Type gridLayoutComponent = Resolve("EFT.InventoryLogic.GridLayoutComponent");
            Type layoutInterface = Resolve("EFT.InventoryLogic.IGridLayoutComponentTemplate");
            Type jsonTypes = Resolve("EFT.InventoryLogic.JsonTypes");

            LogType("searchableTemplate", searchableTemplate);
            LogType("searchableItem", searchableItem);
            LogType("gridLayoutComponent", gridLayoutComponent);
            LogType("layoutInterface", layoutInterface);
            LogType("itemTypeMapping", jsonTypes);

            FieldInfo typeTable = jsonTypes?.GetField("TypeTable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo templateTypeTable = jsonTypes?.GetField("TemplateTypeTable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo itemConstructors = jsonTypes?.GetField("ItemConstructors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            logInfo?.Invoke("B&A&HB DISCOVERY: JsonTypes.TypeTable=" + Describe(typeTable));
            logInfo?.Invoke("B&A&HB DISCOVERY: JsonTypes.TemplateTypeTable=" + Describe(templateTypeTable));
            logInfo?.Invoke("B&A&HB DISCOVERY: JsonTypes.ItemConstructors=" + Describe(itemConstructors));

            var targetFields = new HashSet<int>();
            if (typeTable != null) targetFields.Add(typeTable.MetadataToken);
            if (templateTypeTable != null) targetFields.Add(templateTypeTable.MetadataToken);
            if (itemConstructors != null) targetFields.Add(itemConstructors.MetadataToken);

            List<MethodBase> registrationMethods = FindMethodsReferencingFields(jsonTypes?.Assembly, targetFields);
            if (registrationMethods.Count == 0)
            {
                logWarning?.Invoke("B&A&HB DISCOVERY: itemTypeInit=<unresolved>; no Assembly-CSharp method referencing JsonTypes registration tables was found.");
            }
            else
            {
                for (int i = 0; i < registrationMethods.Count && i < 12; i++)
                    logInfo?.Invoke("B&A&HB DISCOVERY: itemTypeInitCandidate[" + i + "]=" + Describe(registrationMethods[i]));
            }

            FieldInfo serializationTypes = FindSerializationTypeList(jsonTypes?.Assembly, searchableTemplate, searchableItem);
            logInfo?.Invoke("B&A&HB DISCOVERY: serializationTypeList=" + Describe(serializationTypes));

            MethodBase preferredInit = ChooseRegistrationBoundary(registrationMethods, jsonTypes);
            logInfo?.Invoke("B&A&HB DISCOVERY: itemTypeInit=" + Describe(preferredInit));

            bool resolved = searchableTemplate != null
                && searchableItem != null
                && gridLayoutComponent != null
                && layoutInterface != null
                && jsonTypes != null
                && typeTable != null
                && templateTypeTable != null
                && itemConstructors != null
                && preferredInit != null;

            if (resolved)
                logInfo?.Invoke("B&A&HB DISCOVERY PASS: SPT 4.1.3 searchable/container registration boundary resolved structurally. Custom taxonomy remains intentionally disabled for this load-safe artifact.");
            else
                logWarning?.Invoke("B&A&HB DISCOVERY FAIL-CLOSED: one or more SPT 4.1.3 inventory boundaries remain unresolved. Custom taxonomy/type registration remains disabled; profile loading is not intentionally exposed to unknown taxonomy.");

            return resolved;
        }

        List<MethodBase> FindMethodsReferencingFields(Assembly assembly, HashSet<int> targetTokens)
        {
            var result = new List<MethodBase>();
            if (assembly == null || targetTokens.Count == 0) return result;

            foreach (Type type in SafeGetTypes(assembly))
            {
                foreach (MethodBase method in SafeGetMethods(type))
                {
                    MethodBody body;
                    try { body = method.GetMethodBody(); } catch { continue; }
                    if (body == null) continue;
                    byte[] il;
                    try { il = body.GetILAsByteArray(); } catch { continue; }
                    if (il == null || il.Length == 0) continue;
                    if (ReferencesAnyTargetField(method.Module, il, targetTokens)) result.Add(method);
                }
            }

            return result.OrderByDescending(RegistrationScore).ThenBy(x => x.DeclaringType?.FullName).ThenBy(x => x.Name).ToList();
        }

        static bool ReferencesAnyTargetField(Module module, byte[] il, HashSet<int> targetTokens)
        {
            int p = 0;
            while (p < il.Length)
            {
                OpCode opcode;
                byte first = il[p++];
                if (first == 0xFE)
                {
                    if (p >= il.Length) return false;
                    short key = unchecked((short)(0xFE00 | il[p++]));
                    if (!OpCodesByValue.TryGetValue(key, out opcode)) return false;
                }
                else if (!OpCodesByValue.TryGetValue(first, out opcode)) return false;

                int operandSize = OperandSize(opcode.OperandType, il, p);
                if ((opcode.OperandType == OperandType.InlineField || opcode.OperandType == OperandType.InlineTok) && p + 4 <= il.Length)
                {
                    int token = BitConverter.ToInt32(il, p);
                    if (targetTokens.Contains(token)) return true;
                    try
                    {
                        FieldInfo resolved = module.ResolveField(token);
                        if (resolved != null && targetTokens.Contains(resolved.MetadataToken)) return true;
                    }
                    catch { }
                }
                p += operandSize;
            }
            return false;
        }

        static int OperandSize(OperandType type, byte[] il, int operandStart)
        {
            switch (type)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR: return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch:
                    if (operandStart + 4 > il.Length) return 0;
                    int count = BitConverter.ToInt32(il, operandStart);
                    return 4 + Math.Max(0, count) * 4;
                default: return 0;
            }
        }

        static int RegistrationScore(MethodBase method)
        {
            int score = 0;
            string name = method.Name ?? string.Empty;
            string type = method.DeclaringType?.FullName ?? string.Empty;
            if (name.IndexOf("Init", StringComparison.OrdinalIgnoreCase) >= 0) score += 8;
            if (name.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0) score += 4;
            if (name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0) score += 4;
            if (method.IsStatic) score += 2;
            if (type.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0) score += 2;
            return score;
        }

        static MethodBase ChooseRegistrationBoundary(List<MethodBase> methods, Type jsonTypes)
        {
            if (methods == null || methods.Count == 0) return null;
            MethodBase exact = methods.FirstOrDefault(m => m.DeclaringType == jsonTypes && m.IsStatic);
            return exact ?? methods[0];
        }

        FieldInfo FindSerializationTypeList(Assembly assembly, Type searchableTemplate, Type searchableItem)
        {
            if (assembly == null) return null;
            foreach (Type type in SafeGetTypes(assembly))
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!typeof(IList).IsAssignableFrom(field.FieldType)) continue;
                    IList list;
                    try { list = field.GetValue(null) as IList; } catch { continue; }
                    if (list == null || list.Count == 0) continue;

                    bool hasTemplate = false;
                    bool hasItem = false;
                    int inspected = Math.Min(list.Count, 256);
                    for (int i = 0; i < inspected; i++)
                    {
                        Type value = list[i] as Type;
                        if (value == null) continue;
                        if (searchableTemplate != null && (value == searchableTemplate || searchableTemplate.IsAssignableFrom(value))) hasTemplate = true;
                        if (searchableItem != null && (value == searchableItem || searchableItem.IsAssignableFrom(value))) hasItem = true;
                        if (hasTemplate && hasItem)
                        {
                            logInfo?.Invoke("B&A&HB DISCOVERY: serializationRegistry=" + type.AssemblyQualifiedName);
                            return field;
                        }
                    }
                }
            }
            logInfo?.Invoke("B&A&HB DISCOVERY: serializationRegistry=<unresolved>");
            return null;
        }

        Type Resolve(string fullName)
        {
            Type type = ReflectionTools.FindType(fullName);
            if (type != null) return type;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(fullName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types.Where(x => x != null); }
            catch { return Array.Empty<Type>(); }
        }

        static IEnumerable<MethodBase> SafeGetMethods(Type type)
        {
            try
            {
                return type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Cast<MethodBase>()
                    .Concat(type.GetConstructors(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            }
            catch { return Array.Empty<MethodBase>(); }
        }

        void LogType(string label, Type type)
        {
            logInfo?.Invoke("B&A&HB DISCOVERY: " + label + "=" + (type == null ? "<unresolved>" : type.AssemblyQualifiedName));
        }

        static string Describe(MemberInfo member)
        {
            if (member == null) return "<unresolved>";
            if (member is FieldInfo field) return field.DeclaringType?.FullName + "." + field.Name + " : " + field.FieldType.FullName;
            if (member is MethodBase method) return method.DeclaringType?.FullName + "." + method;
            return member.DeclaringType?.FullName + "." + member.Name;
        }

        static Dictionary<short, OpCode> BuildOpcodeMap()
        {
            var map = new Dictionary<short, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (field.FieldType != typeof(OpCode)) continue;
                OpCode opcode = (OpCode)field.GetValue(null);
                map[opcode.Value] = opcode;
            }
            return map;
        }
    }
}
