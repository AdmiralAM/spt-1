using System;
using System.Reflection;

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
            Type serializationRegistry = Resolve("GClass3381");

            LogType("searchableTemplate", searchableTemplate);
            LogType("searchableItem", searchableItem);
            LogType("gridLayoutComponent", gridLayoutComponent);
            LogType("layoutInterface", layoutInterface);
            LogType("itemTypeMapping", jsonTypes);
            LogType("serializationRegistry", serializationRegistry);

            FieldInfo typeTable = jsonTypes?.GetField("TypeTable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo templateTypeTable = jsonTypes?.GetField("TemplateTypeTable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo itemConstructors = jsonTypes?.GetField("ItemConstructors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo init = serializationRegistry?.GetMethod("Init", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo serializationTypes = serializationRegistry?.GetField("List_0", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            logInfo?.Invoke("B&A&HB DISCOVERY: JsonTypes.TypeTable=" + Describe(typeTable));
            logInfo?.Invoke("B&A&HB DISCOVERY: JsonTypes.TemplateTypeTable=" + Describe(templateTypeTable));
            logInfo?.Invoke("B&A&HB DISCOVERY: JsonTypes.ItemConstructors=" + Describe(itemConstructors));
            logInfo?.Invoke("B&A&HB DISCOVERY: itemTypeInit=" + Describe(init));
            logInfo?.Invoke("B&A&HB DISCOVERY: serializationTypeList=" + Describe(serializationTypes));

            bool resolved = searchableTemplate != null
                && searchableItem != null
                && gridLayoutComponent != null
                && layoutInterface != null
                && jsonTypes != null
                && typeTable != null
                && templateTypeTable != null
                && itemConstructors != null
                && serializationRegistry != null
                && init != null;

            if (resolved)
            {
                logInfo?.Invoke("B&A&HB DISCOVERY PASS: SPT 4.1.3 searchable/container runtime boundary resolved. Custom taxonomy remains intentionally disabled for this load-safe artifact.");
            }
            else
            {
                logWarning?.Invoke("B&A&HB DISCOVERY FAIL-CLOSED: one or more SPT 4.1.3 inventory boundaries were unresolved. Custom taxonomy/type registration remains disabled; profile loading is not intentionally exposed to unknown taxonomy.");
            }

            return resolved;
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

        void LogType(string label, Type type)
        {
            logInfo?.Invoke("B&A&HB DISCOVERY: " + label + "=" + (type == null ? "<unresolved>" : type.AssemblyQualifiedName));
        }

        static string Describe(MemberInfo member)
        {
            if (member == null) return "<unresolved>";
            if (member is FieldInfo field) return field.DeclaringType?.FullName + "." + field.Name + " : " + field.FieldType.FullName;
            if (member is MethodInfo method) return method.DeclaringType?.FullName + "." + method;
            return member.DeclaringType?.FullName + "." + member.Name;
        }
    }
}
