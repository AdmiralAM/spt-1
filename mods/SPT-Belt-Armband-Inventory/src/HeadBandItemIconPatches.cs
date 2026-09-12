using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class HeadBandItemIconRuntime
    {
        const string ResourceName = "SPTBeltArmbandInventory.HeadBandIcon.png";
        static Sprite sprite;
        internal static Action<string> LogWarning;

        internal static bool Apply(object item, ref object itemIcon)
        {
            if (item == null) return true;
            object tpl = ReflectionTools.ReadMember(item, "StringTemplateId") ?? ReflectionTools.ReadMember(item, "TemplateId");
            if (!string.Equals(tpl?.ToString(), RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal)) return true;

            try
            {
                if (sprite == null) sprite = LoadSprite();
                itemIcon = Activator.CreateInstance(ReflectionTools.FindType("ItemIcon"), new object[] { 0 });
                PropertyInfo property = ReflectionTools.FindInstanceProperty(itemIcon.GetType(), "Sprite", typeof(Sprite));
                if (property != null && property.CanWrite) property.SetValue(itemIcon, sprite, null);
                return property == null || !property.CanWrite;
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB Utility HeadBand item icon override failed safely: " + exception.Message);
                return true;
            }
        }

        static Sprite LoadSprite()
        {
            using Stream stream = typeof(HeadBandItemIconRuntime).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new FileNotFoundException("Embedded Utility HeadBand icon missing", ResourceName);
            byte[] bytes = new byte[stream.Length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read <= 0) throw new EndOfStreamException(ResourceName);
                offset += read;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!ImageConversion.LoadImage(texture, bytes, false)) throw new InvalidDataException("Utility HeadBand PNG could not be decoded");
            texture.name = "BAndHB_UtilityHeadBand_Icon";
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        internal static void Reset() { LogWarning = null; }
    }

    internal sealed class HeadBandItemIconPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.headband-icon";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal HeadBandItemIconPatches(Action<string> logInfo, Action<string> logWarning)
        { this.logInfo = logInfo; this.logWarning = logWarning; }

        internal bool TryInstall()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type factoryType = ReflectionTools.FindType("EFT.UI.DragAndDrop.ItemViewFactory");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (harmonyType == null || harmonyMethodType == null || factoryType == null || itemType == null) return false;
                MethodInfo target = null;
                foreach (MethodInfo method in factoryType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.Name == "LoadItemIcon" && parameters.Length == 3 && parameters[0].ParameterType == itemType) { target = method; break; }
                }
                MethodInfo patch = harmonyType.GetMethod("Patch", new[] { typeof(MethodBase), harmonyMethodType, harmonyMethodType, harmonyMethodType, harmonyMethodType });
                ConstructorInfo harmonyMethodCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", Type.EmptyTypes);
                if (target == null || patch == null || harmonyMethodCtor == null || unpatchSelf == null) return false;

                HeadBandItemIconRuntime.LogWarning = logWarning;
                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object prefix = harmonyMethodCtor.Invoke(new object[] { typeof(HeadBandItemIconPatches).GetMethod(nameof(CreatePrefix), BindingFlags.Static | BindingFlags.NonPublic) });
                patch.Invoke(harmony, new object[] { target, prefix, null, null, null });
                logInfo?.Invoke("B&A&HB owned Utility HeadBand inventory icon installed on ItemViewFactory.LoadItemIcon.");
                return true;
            }
            catch (Exception exception) { Dispose(); logWarning?.Invoke("B&A&HB HeadBand icon patch failed safely: " + exception.ToString()); return false; }
        }

        static MethodInfo CreatePrefix(MethodBase originalMethod)
        {
            MethodInfo original = (MethodInfo)originalMethod;
            DynamicMethod method = new DynamicMethod("BAndHBHeadBandIconPrefix", typeof(bool), new[] { original.GetParameters()[0].ParameterType, original.ReturnType.MakeByRefType() }, typeof(HeadBandItemIconPatches), true);
            method.DefineParameter(1, ParameterAttributes.None, "__0");
            method.DefineParameter(2, ParameterAttributes.None, "__result");
            ILGenerator il = method.GetILGenerator();
            LocalBuilder result = il.DeclareLocal(typeof(object));
            LocalBuilder runOriginal = il.DeclareLocal(typeof(bool));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloca, result);
            il.Emit(OpCodes.Call, typeof(HeadBandItemIconRuntime).GetMethod(nameof(HeadBandItemIconRuntime.Apply), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Stloc, runOriginal);
            Label finish = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, runOriginal);
            il.Emit(OpCodes.Brtrue, finish);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Castclass, original.ReturnType);
            il.Emit(OpCodes.Stind_Ref);
            il.MarkLabel(finish);
            il.Emit(OpCodes.Ldloc, runOriginal);
            il.Emit(OpCodes.Ret);
            return method;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null; unpatchSelf = null; HeadBandItemIconRuntime.Reset();
        }
    }
}
