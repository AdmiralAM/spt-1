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

        internal static void Apply(object item, object itemIcon)
        {
            if (item == null || itemIcon == null) return;
            object tpl = ReflectionTools.ReadMember(item, "StringTemplateId") ?? ReflectionTools.ReadMember(item, "TemplateId");
            if (!string.Equals(tpl?.ToString(), RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal)) return;

            try
            {
                if (sprite == null) sprite = LoadSprite();
                PropertyInfo property = ReflectionTools.FindInstanceProperty(itemIcon.GetType(), "Sprite", typeof(Sprite));
                if (property != null && property.CanWrite) property.SetValue(itemIcon, sprite, null);
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB Utility HeadBand item icon override failed safely: " + exception.Message);
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
                foreach (MethodInfo method in factoryType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
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
                object postfix = harmonyMethodCtor.Invoke(new object[] { CreatePostfix(target) });
                patch.Invoke(harmony, new object[] { target, null, postfix, null, null });
                logInfo?.Invoke("B&A&HB owned Utility HeadBand inventory icon installed on ItemViewFactory.LoadItemIcon.");
                return true;
            }
            catch (Exception exception) { Dispose(); logWarning?.Invoke("B&A&HB HeadBand icon patch failed safely: " + exception.Message); return false; }
        }

        static MethodInfo CreatePostfix(MethodInfo original)
        {
            DynamicMethod method = new DynamicMethod("BAndHBHeadBandIconPostfix", typeof(void), new[] { original.GetParameters()[0].ParameterType, original.ReturnType }, typeof(HeadBandItemIconPatches), true);
            method.DefineParameter(1, ParameterAttributes.None, "item");
            method.DefineParameter(2, ParameterAttributes.None, "__result");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(HeadBandItemIconRuntime).GetMethod(nameof(HeadBandItemIconRuntime.Apply), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
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
