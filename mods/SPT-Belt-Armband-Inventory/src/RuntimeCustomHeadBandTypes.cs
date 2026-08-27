using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal sealed class RuntimeCustomHeadBandTypePatches : IDisposable
    {
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;

        internal RuntimeCustomHeadBandTypePatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                if (RuntimeCustomBeltTypes.CustomTemplateType == null || RuntimeCustomBeltTypes.CustomBeltItemType == null)
                    throw new InvalidOperationException("shared searchable runtime type is not initialized");

                RuntimeCustomHeadBandTypes.Register();
                logInfo?.Invoke("B&A&HB RUNTIME TYPE: dedicated HeadBand parent mapped to the proven searchable container runtime type.");
                return true;
            }
            catch (Exception exception)
            {
                Exception root = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                logWarning?.Invoke("B&A&HB HEADBAND RUNTIME TYPE INSTALL FAIL: " + root.GetType().FullName + ": " + root.Message);
                Dispose();
                return false;
            }
        }

        public void Dispose()
        {
            RuntimeCustomHeadBandTypes.Rollback();
        }
    }

    internal static class RuntimeCustomHeadBandTypes
    {
        static IDictionary ownedTypeTable;
        static IDictionary ownedTemplateTable;
        static IDictionary ownedConstructors;
        static object previousItemType;
        static object previousTemplateType;
        static object previousConstructor;
        static object installedConstructor;
        static bool hadItemType;
        static bool hadTemplateType;
        static bool hadConstructor;

        internal static void Register()
        {
            Type jsonTypes = ReflectionTools.FindType("EFT.InventoryLogic.JsonTypes");
            Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
            if (jsonTypes == null || itemType == null)
                throw new InvalidOperationException("EFT.InventoryLogic.JsonTypes/Item not found");

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

            string key = RuntimeIdentity.HeadBandItemParentId;
            RequireAvailable(typeTable, key, RuntimeCustomBeltTypes.CustomBeltItemType, "item type");
            RequireAvailable(templateTable, key, RuntimeCustomBeltTypes.CustomTemplateType, "template type");
            if (constructors.Contains(key))
                throw new InvalidOperationException("JsonTypes item-constructor id collision for " + key);

            ConstructorInfo customCtor = RuntimeCustomBeltTypes.CustomBeltItemType.GetConstructor(
                new[] { typeof(string), RuntimeCustomBeltTypes.CustomTemplateType });
            if (customCtor == null)
                throw new InvalidOperationException("shared searchable item constructor unavailable for HeadBand mapping");

            Type delegateType = constructors.GetType().GetGenericArguments()[1];
            DynamicMethod factory = new DynamicMethod(
                "CreateBAndHBHeadBand",
                itemType,
                new[] { typeof(string), typeof(object) },
                typeof(RuntimeCustomHeadBandTypes),
                true);
            ILGenerator il = factory.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, RuntimeCustomBeltTypes.CustomTemplateType);
            il.Emit(OpCodes.Newobj, customCtor);
            il.Emit(OpCodes.Ret);
            object constructorDelegate = factory.CreateDelegate(delegateType);

            ownedTypeTable = typeTable;
            ownedTemplateTable = templateTable;
            ownedConstructors = constructors;
            hadItemType = typeTable.Contains(key);
            hadTemplateType = templateTable.Contains(key);
            hadConstructor = constructors.Contains(key);
            previousItemType = hadItemType ? typeTable[key] : null;
            previousTemplateType = hadTemplateType ? templateTable[key] : null;
            previousConstructor = hadConstructor ? constructors[key] : null;
            installedConstructor = constructorDelegate;

            try
            {
                typeTable[key] = RuntimeCustomBeltTypes.CustomBeltItemType;
                templateTable[key] = RuntimeCustomBeltTypes.CustomTemplateType;
                constructors[key] = constructorDelegate;
            }
            catch
            {
                Rollback();
                throw;
            }
        }

        static void RequireAvailable(IDictionary table, string key, object expected, string label)
        {
            if (!table.Contains(key)) return;
            if (ReferenceEquals(table[key], expected)) return;
            throw new InvalidOperationException("JsonTypes HeadBand " + label + " id collision for " + key);
        }

        internal static void Rollback()
        {
            string key = RuntimeIdentity.HeadBandItemParentId;
            RestoreOwned(ownedConstructors, key, installedConstructor, hadConstructor, previousConstructor);
            RestoreOwned(ownedTemplateTable, key, RuntimeCustomBeltTypes.CustomTemplateType, hadTemplateType, previousTemplateType);
            RestoreOwned(ownedTypeTable, key, RuntimeCustomBeltTypes.CustomBeltItemType, hadItemType, previousItemType);

            ownedTypeTable = null;
            ownedTemplateTable = null;
            ownedConstructors = null;
            previousItemType = null;
            previousTemplateType = null;
            previousConstructor = null;
            installedConstructor = null;
            hadItemType = false;
            hadTemplateType = false;
            hadConstructor = false;
        }

        static void RestoreOwned(IDictionary table, string key, object installed, bool hadPrevious, object previous)
        {
            if (table == null || installed == null || !table.Contains(key)) return;
            if (!ReferenceEquals(table[key], installed)) return;
            if (hadPrevious) table[key] = previous;
            else table.Remove(key);
        }
    }
}
