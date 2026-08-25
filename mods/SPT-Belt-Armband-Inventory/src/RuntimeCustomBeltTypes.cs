using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal sealed class RuntimeCustomBeltTypePatches : IDisposable
    {
        internal const string CustomTemplateParentId = "68ac00000000000000000004";
        internal const string CustomBeltParentId = "68ac00000000000000000005";
        internal const string LayoutName = "B&A&HB-RC-1x2";

        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.runtime-types";

        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

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

                logInfo?.Invoke("B&A&HB RUNTIME TYPE: custom searchable belt item/template mappings registered directly in SPT 4.1.3 JsonTypes for RC parent " + CustomBeltParentId + ".");
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

        bool InstallItemTypeInitPostfix()
        {
            Type registryType = ReflectionTools.FindType("GClass3381");
            Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
            Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
            if (registryType == null || harmonyType == null || harmonyMethodType == null)
            {
                logWarning?.Invoke("B&A&HB RUNTIME TYPE: SPT 4.1.3 item-type initialization registry or Harmony not found.");
                return false;
            }

            MethodInfo init = registryType.GetMethod("Init", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo patch = FindPatchMethod(harmonyType, harmonyMethodType);
            ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
            if (init == null || patch == null || hmCtor == null)
            {
                logWarning?.Invoke("B&A&HB RUNTIME TYPE: GClass3381.Init/Harmony patch boundary changed.");
                return false;
            }

            harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
            unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
            object postfix = hmCtor.Invoke(new object[] { typeof(RuntimeCustomBeltTypes).GetMethod(nameof(RuntimeCustomBeltTypes.AfterItemTypeInit), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) });
            patch.Invoke(harmony, new[] { (MethodBase)init, null, postfix, null, null });
            return true;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Patch") continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length == 5 && p[0].ParameterType == typeof(MethodBase) && p[1].ParameterType == harmonyMethodType) return method;
            }
            return null;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
        }
    }

    internal static class RuntimeCustomBeltTypes
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type CustomTemplateType;
        internal static Type CustomBeltItemType;

        static ConstructorInfo customItemConstructor;

        internal static bool BuildAndRegister()
        {
            if (CustomTemplateType == null || CustomBeltItemType == null)
            {
                Type searchableTemplate = ReflectionTools.FindType("EFT.InventoryLogic.SearchableItemTemplate");
                Type searchableItem = ReflectionTools.FindType("EFT.InventoryLogic.SearchableItem");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                Type gridLayoutComponent = ReflectionTools.FindType("EFT.InventoryLogic.GridLayoutComponent");
                Type layoutInterface = ReflectionTools.FindType("EFT.InventoryLogic.IGridLayoutComponentTemplate");
                if (searchableTemplate == null || searchableItem == null || itemType == null || gridLayoutComponent == null || layoutInterface == null)
                {
                    LogWarning?.Invoke("B&A&HB RUNTIME TYPE: searchable item/template/grid-layout contract types were not found.");
                    return false;
                }

                AssemblyName assemblyName = new AssemblyName("SPTBeltArmbandInventory.RuntimeTypes");
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name);

                CustomTemplateType = BuildTemplateType(module, searchableTemplate, layoutInterface);
                CustomBeltItemType = BuildItemType(module, searchableItem, itemType, gridLayoutComponent, CustomTemplateType);
                customItemConstructor = CustomBeltItemType.GetConstructor(new[] { typeof(string), CustomTemplateType });
                if (customItemConstructor == null) throw new InvalidOperationException("generated custom belt constructor missing");
            }

            RegisterJsonMappings();
            EnsureSerializationTypes();
            return true;
        }

        static Type BuildTemplateType(ModuleBuilder module, Type searchableTemplate, Type layoutInterface)
        {
            TypeBuilder builder = module.DefineType(
                "SPTBeltArmbandInventory.Runtime.CustomBeltTemplate",
                TypeAttributes.Public | TypeAttributes.Class,
                searchableTemplate,
                new[] { layoutInterface });

            FieldBuilder layout = builder.DefineField("_runtimeLayoutName", typeof(string), FieldAttributes.Private);
            ConstructorInfo baseCtor = searchableTemplate.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (baseCtor == null) throw new InvalidOperationException("SearchableItemTemplate parameterless constructor not found");

            ConstructorBuilder ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            ILGenerator cil = ctor.GetILGenerator();
            cil.Emit(OpCodes.Ldarg_0);
            cil.Emit(OpCodes.Call, baseCtor);
            cil.Emit(OpCodes.Ldarg_0);
            cil.Emit(OpCodes.Ldstr, RuntimeCustomBeltTypePatches.LayoutName);
            cil.Emit(OpCodes.Stfld, layout);
            cil.Emit(OpCodes.Ret);

            PropertyBuilder property = builder.DefineProperty("LayoutName", PropertyAttributes.None, typeof(string), Type.EmptyTypes);
            MethodBuilder getter = builder.DefineMethod("get_LayoutName", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName, typeof(string), Type.EmptyTypes);
            ILGenerator gil = getter.GetILGenerator();
            gil.Emit(OpCodes.Ldarg_0);
            gil.Emit(OpCodes.Ldfld, layout);
            gil.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);

            MethodBuilder setter = builder.DefineMethod("set_LayoutName", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, typeof(void), new[] { typeof(string) });
            ILGenerator sil = setter.GetILGenerator();
            sil.Emit(OpCodes.Ldarg_0);
            sil.Emit(OpCodes.Ldarg_1);
            sil.Emit(OpCodes.Stfld, layout);
            sil.Emit(OpCodes.Ret);
            property.SetSetMethod(setter);

            PropertyInfo interfaceLayout = layoutInterface.GetProperty("LayoutName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo interfaceGetter = interfaceLayout == null ? null : interfaceLayout.GetGetMethod(true);
            if (interfaceGetter != null) builder.DefineMethodOverride(getter, interfaceGetter);

            return builder.CreateType();
        }

        static Type BuildItemType(ModuleBuilder module, Type searchableItem, Type itemType, Type gridLayoutComponent, Type customTemplate)
        {
            TypeBuilder builder = module.DefineType(
                "SPTBeltArmbandInventory.Runtime.CustomBeltSearchableContainer",
                TypeAttributes.Public | TypeAttributes.Class,
                searchableItem);

            ConstructorInfo baseCtor = FindItemBaseConstructor(searchableItem, customTemplate);
            if (baseCtor == null) throw new InvalidOperationException("SearchableItem(string, searchable template) constructor not found");

            ConstructorInfo gridCtor = FindGridLayoutConstructor(gridLayoutComponent, searchableItem, customTemplate);
            if (gridCtor == null) throw new InvalidOperationException("GridLayoutComponent(item, template) constructor not found");

            MemberInfo components = FindComponentsMember(searchableItem);
            if (components == null) throw new InvalidOperationException("SearchableItemItemClass Components collection not found");
            Type componentsType = components is PropertyInfo cp ? cp.PropertyType : ((FieldInfo)components).FieldType;
            MethodInfo add = FindAddMethod(componentsType, gridLayoutComponent);
            if (add == null) throw new InvalidOperationException("Components.Add(GridLayoutComponent) boundary not found");

            ConstructorBuilder ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(string), customTemplate });
            ILGenerator il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, baseCtor);

            il.Emit(OpCodes.Ldarg_0);
            if (components is PropertyInfo property) il.Emit(OpCodes.Call, property.GetGetMethod(true));
            else il.Emit(OpCodes.Ldfld, (FieldInfo)components);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Newobj, gridCtor);
            il.Emit(OpCodes.Callvirt, add);
            if (add.ReturnType != typeof(void)) il.Emit(OpCodes.Pop);
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

        static ConstructorInfo FindGridLayoutConstructor(Type componentType, Type searchableItem, Type customTemplate)
        {
            foreach (ConstructorInfo ctor in componentType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] p = ctor.GetParameters();
                if (p.Length != 2) continue;
                if (!p[0].ParameterType.IsAssignableFrom(searchableItem)) continue;
                if (!p[1].ParameterType.IsAssignableFrom(customTemplate)) continue;
                return ctor;
            }
            return null;
        }

        static MemberInfo FindComponentsMember(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo p = current.GetProperty("Components", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (p != null && p.GetGetMethod(true) != null) return p;
                FieldInfo f = current.GetField("Components", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        static MethodInfo FindAddMethod(Type collectionType, Type componentType)
        {
            foreach (MethodInfo method in collectionType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "Add") continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length == 1 && p[0].ParameterType.IsAssignableFrom(componentType)) return method;
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

            typeTable[RuntimeCustomBeltTypePatches.CustomBeltParentId] = CustomBeltItemType;
            templateTable[RuntimeCustomBeltTypePatches.CustomTemplateParentId] = CustomTemplateType;
            templateTable[RuntimeCustomBeltTypePatches.CustomBeltParentId] = CustomTemplateType;

            Type delegateType = constructors.GetType().GetGenericArguments()[1];
            DynamicMethod factory = new DynamicMethod("CreateBAndHBBelt", itemType, new[] { typeof(string), typeof(object) }, typeof(RuntimeCustomBeltTypes), true);
            ILGenerator il = factory.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, CustomTemplateType);
            il.Emit(OpCodes.Newobj, customItemConstructor);
            il.Emit(OpCodes.Ret);
            constructors[RuntimeCustomBeltTypePatches.CustomBeltParentId] = factory.CreateDelegate(delegateType);
        }

        internal static void AfterItemTypeInit()
        {
            try
            {
                RegisterJsonMappings();
                EnsureSerializationTypes();
                LogInfo?.Invoke("B&A&HB RUNTIME TYPE: SPT 4.1.3 item-type initialization retained custom belt/template registration.");
            }
            catch (Exception exception)
            {
                Exception root = exception;
                while (root is TargetInvocationException invocation && invocation.InnerException != null) root = invocation.InnerException;
                LogWarning?.Invoke("B&A&HB RUNTIME TYPE INIT FAIL: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static void EnsureSerializationTypes()
        {
            Type registry = ReflectionTools.FindType("GClass3381");
            FieldInfo listField = registry == null ? null : registry.GetField("List_0", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            IList list = listField == null ? null : listField.GetValue(null) as IList;
            if (list == null) return;
            if (!list.Contains(CustomTemplateType)) list.Add(CustomTemplateType);
            if (!list.Contains(CustomBeltItemType)) list.Add(CustomBeltItemType);
        }
    }
}
