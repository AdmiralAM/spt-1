using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal sealed class RuntimeCustomBeltTypePatches : IDisposable
    {
        internal const string CustomTemplateParentId = RuntimeIdentity.SearchableTemplateParentId;
        internal const string CustomBeltParentId = RuntimeIdentity.BeltItemParentId;

        readonly Action<string> logInfo;
        readonly Action<string> logWarning;

        internal RuntimeCustomBeltTypePatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                RuntimeCustomBeltTypes.LogInfo = logInfo;
                RuntimeCustomBeltTypes.LogWarning = logWarning;
                if (!RuntimeCustomBeltTypes.BuildAndRegister()) return false;

                logInfo?.Invoke("B&A&HB RUNTIME TYPE: custom searchable belt item/template mappings registered directly in SPT 4.1.4 JsonTypes for RC parent " + CustomBeltParentId + ".");
                return true;
            }
            catch (Exception exception)
            {
                Exception root = Unwrap(exception);
                logWarning?.Invoke("B&A&HB RUNTIME TYPE INSTALL FAIL: " + root.GetType().FullName + ": " + root.Message + (string.IsNullOrEmpty(root.StackTrace) ? "" : "\n" + root.StackTrace));
                Dispose();
                return false;
            }
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        public void Dispose()
        {
            RuntimeCustomBeltTypes.RollbackJsonMappings();
            RuntimeCustomBeltTypes.ReleaseLogging(logInfo, logWarning);
        }
    }

    internal static class RuntimeCustomBeltTypes
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type CustomTemplateType;
        internal static Type CustomBeltItemType;

        static ConstructorInfo customItemConstructor;
        static IDictionary ownedTypeTable;
        static IDictionary ownedTemplateTable;
        static IDictionary ownedConstructors;
        static object previousItemType;
        static object previousTemplateParentType;
        static object previousBeltTemplateType;
        static object previousConstructor;
        static object installedConstructor;
        static bool hadItemType;
        static bool hadTemplateParentType;
        static bool hadBeltTemplateType;
        static bool hadConstructor;

        internal static bool BuildAndRegister()
        {
            if (CustomTemplateType == null || CustomBeltItemType == null)
            {
                Type searchableTemplate = ReflectionTools.FindType("EFT.InventoryLogic.SearchableItemTemplate");
                Type searchableItem = ReflectionTools.FindType("EFT.InventoryLogic.SearchableItem");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (searchableTemplate == null || searchableItem == null || itemType == null)
                {
                    LogWarning?.Invoke("B&A&HB RUNTIME TYPE: searchable item/template contract types were not found.");
                    return false;
                }

                AssemblyName assemblyName = new AssemblyName("SPTBeltArmbandInventory.RuntimeTypes");
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name);

                CustomTemplateType = BuildTemplateType(module, searchableTemplate);
                CustomBeltItemType = BuildItemType(module, searchableItem, CustomTemplateType);
                customItemConstructor = CustomBeltItemType.GetConstructor(new[] { typeof(string), CustomTemplateType });
                if (customItemConstructor == null) throw new InvalidOperationException("generated custom belt constructor missing");
            }

            RegisterJsonMappings();
            return true;
        }

        static Type BuildTemplateType(ModuleBuilder module, Type searchableTemplate)
        {
            TypeBuilder builder = module.DefineType(
                "SPTBeltArmbandInventory.Runtime.CustomBeltTemplate",
                TypeAttributes.Public | TypeAttributes.Class,
                searchableTemplate);

            ConstructorInfo baseCtor = searchableTemplate.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (baseCtor == null) throw new InvalidOperationException("SearchableItemTemplate parameterless constructor not found");

            ConstructorBuilder ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            ILGenerator cil = ctor.GetILGenerator();
            cil.Emit(OpCodes.Ldarg_0);
            cil.Emit(OpCodes.Call, baseCtor);
            cil.Emit(OpCodes.Ret);

            return builder.CreateType();
        }

        static Type BuildItemType(ModuleBuilder module, Type searchableItem, Type customTemplate)
        {
            TypeBuilder builder = module.DefineType(
                "SPTBeltArmbandInventory.Runtime.CustomBeltSearchableContainer",
                TypeAttributes.Public | TypeAttributes.Class,
                searchableItem);

            ConstructorInfo baseCtor = FindItemBaseConstructor(searchableItem, customTemplate);
            if (baseCtor == null) throw new InvalidOperationException("SearchableItem(string, searchable template) constructor not found");

            ConstructorBuilder ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(string), customTemplate });
            ILGenerator il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ret);

            return builder.CreateType();
        }

        static ConstructorInfo FindItemBaseConstructor(Type searchableItem, Type customTemplate)
        {
            foreach (ConstructorInfo ctor in searchableItem.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] p = ctor.GetParameters();
                if (p.Length != 2 || p[0].ParameterType != typeof(string)) continue;
                if (p[1].ParameterType.IsAssignableFrom(customTemplate)) return ctor;
            }
            return null;
        }

        static void RegisterJsonMappings()
        {
            Type jsonTypes = ReflectionTools.FindType("EFT.InventoryLogic.JsonTypes");
            Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
            if (jsonTypes == null || itemType == null) throw new InvalidOperationException("EFT.InventoryLogic.JsonTypes/Item not found");

            FieldInfo typeTableField = jsonTypes.GetField("TypeTable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo templateTableField = jsonTypes.GetField("TemplateTypeTable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo constructorsField = jsonTypes.GetField("ItemConstructors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (typeTableField == null || templateTableField == null || constructorsField == null)
                throw new InvalidOperationException("JsonTypes mapping tables changed");

            IDictionary typeTable = typeTableField.GetValue(null) as IDictionary;
            IDictionary templateTable = templateTableField.GetValue(null) as IDictionary;
            IDictionary constructors = constructorsField.GetValue(null) as IDictionary;
            if (typeTable == null || templateTable == null || constructors == null)
                throw new InvalidOperationException("JsonTypes mapping tables unavailable");

            Type delegateType = constructors.GetType().GetGenericArguments()[1];
            DynamicMethod factory = new DynamicMethod("CreateBAndHBBelt", itemType, new[] { typeof(string), typeof(object) }, typeof(RuntimeCustomBeltTypes), true);
            ILGenerator il = factory.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, CustomTemplateType);
            il.Emit(OpCodes.Newobj, customItemConstructor);
            il.Emit(OpCodes.Ret);
            object constructorDelegate = factory.CreateDelegate(delegateType);

            RequireAvailable(typeTable, RuntimeCustomBeltTypePatches.CustomBeltParentId, CustomBeltItemType, "item type");
            RequireAvailable(templateTable, RuntimeCustomBeltTypePatches.CustomTemplateParentId, CustomTemplateType, "template parent type");
            RequireAvailable(templateTable, RuntimeCustomBeltTypePatches.CustomBeltParentId, CustomTemplateType, "belt template type");
            if (constructors.Contains(RuntimeCustomBeltTypePatches.CustomBeltParentId))
                throw new InvalidOperationException("JsonTypes item-constructor id collision for " + RuntimeCustomBeltTypePatches.CustomBeltParentId);

            ownedTypeTable = typeTable;
            ownedTemplateTable = templateTable;
            ownedConstructors = constructors;
            hadItemType = typeTable.Contains(RuntimeCustomBeltTypePatches.CustomBeltParentId);
            hadTemplateParentType = templateTable.Contains(RuntimeCustomBeltTypePatches.CustomTemplateParentId);
            hadBeltTemplateType = templateTable.Contains(RuntimeCustomBeltTypePatches.CustomBeltParentId);
            hadConstructor = constructors.Contains(RuntimeCustomBeltTypePatches.CustomBeltParentId);
            previousItemType = hadItemType ? typeTable[RuntimeCustomBeltTypePatches.CustomBeltParentId] : null;
            previousTemplateParentType = hadTemplateParentType ? templateTable[RuntimeCustomBeltTypePatches.CustomTemplateParentId] : null;
            previousBeltTemplateType = hadBeltTemplateType ? templateTable[RuntimeCustomBeltTypePatches.CustomBeltParentId] : null;
            previousConstructor = hadConstructor ? constructors[RuntimeCustomBeltTypePatches.CustomBeltParentId] : null;
            installedConstructor = constructorDelegate;

            try
            {
                typeTable[RuntimeCustomBeltTypePatches.CustomBeltParentId] = CustomBeltItemType;
                templateTable[RuntimeCustomBeltTypePatches.CustomTemplateParentId] = CustomTemplateType;
                templateTable[RuntimeCustomBeltTypePatches.CustomBeltParentId] = CustomTemplateType;
                constructors[RuntimeCustomBeltTypePatches.CustomBeltParentId] = constructorDelegate;
            }
            catch
            {
                RollbackJsonMappings();
                throw;
            }
        }

        static void RequireAvailable(IDictionary table, string key, object expected, string label)
        {
            if (!table.Contains(key)) return;
            object current = table[key];
            if (ReferenceEquals(current, expected)) return;
            throw new InvalidOperationException("JsonTypes " + label + " id collision for " + key);
        }

        internal static void RollbackJsonMappings()
        {
            RestoreOwned(ownedConstructors, RuntimeCustomBeltTypePatches.CustomBeltParentId, installedConstructor, hadConstructor, previousConstructor);
            RestoreOwned(ownedTemplateTable, RuntimeCustomBeltTypePatches.CustomBeltParentId, CustomTemplateType, hadBeltTemplateType, previousBeltTemplateType);
            RestoreOwned(ownedTemplateTable, RuntimeCustomBeltTypePatches.CustomTemplateParentId, CustomTemplateType, hadTemplateParentType, previousTemplateParentType);
            RestoreOwned(ownedTypeTable, RuntimeCustomBeltTypePatches.CustomBeltParentId, CustomBeltItemType, hadItemType, previousItemType);

            ownedTypeTable = null;
            ownedTemplateTable = null;
            ownedConstructors = null;
            previousItemType = null;
            previousTemplateParentType = null;
            previousBeltTemplateType = null;
            previousConstructor = null;
            installedConstructor = null;
            hadItemType = false;
            hadTemplateParentType = false;
            hadBeltTemplateType = false;
            hadConstructor = false;
        }

        static void RestoreOwned(IDictionary table, string key, object installed, bool hadPrevious, object previous)
        {
            if (table == null || installed == null || !table.Contains(key)) return;
            object current = table[key];
            if (!ReferenceEquals(current, installed)) return;
            if (hadPrevious) table[key] = previous;
            else table.Remove(key);
        }

        internal static void ReleaseLogging(Action<string> logInfo, Action<string> logWarning)
        {
            if (Equals(LogInfo, logInfo)) LogInfo = null;
            if (Equals(LogWarning, logWarning)) LogWarning = null;
        }
    }
}
