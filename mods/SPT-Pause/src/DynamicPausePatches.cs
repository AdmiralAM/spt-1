using System;
using System.Reflection;

namespace SPTPause
{
    internal static class PauseRuntime
    {
        internal static bool IsPaused;
        internal static bool ShowPausedText = true;
    }

    internal sealed class DynamicPausePatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.pause.runtime";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal DynamicPausePatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                if (harmonyType == null || harmonyMethodType == null) return Fail("Harmony is unavailable; Pause Admiral remains disabled.");

                Type gameWorld = ReflectionTools.FindType("EFT.GameWorld");
                Type timerPanel = ReflectionTools.FindType("EFT.UI.BattleTimer.TimerPanel");
                if (gameWorld == null || timerPanel == null) return Fail("SPT 4.1 world/timer types were not found; Pause Admiral remains disabled.");

                MethodInfo worldTick = gameWorld.GetMethod("DoWorldTick", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(float) }, null);
                MethodInfo otherWorldTick = gameWorld.GetMethod("DoOtherWorldTick", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(float) }, null);
                MethodInfo updateTimer = timerPanel.GetMethod("UpdateTimer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (worldTick == null || otherWorldTick == null || updateTimer == null) return Fail("SPT 4.1 patch targets changed; pause was not installed.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmony == null || patchMethod == null || harmonyMethodConstructor == null) return Fail("Harmony patch API is incompatible; pause was not installed.");

                MethodInfo worldPrefix = typeof(DynamicPausePatches).GetMethod(nameof(WorldTickPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo timerPrefix = typeof(DynamicPausePatches).GetMethod(nameof(TimerPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                object worldPatch = harmonyMethodConstructor.Invoke(new object[] { worldPrefix });
                object timerPatch = harmonyMethodConstructor.Invoke(new object[] { timerPrefix });
                Patch(patchMethod, harmonyMethodType, worldTick, worldPatch);
                Patch(patchMethod, harmonyMethodType, otherWorldTick, worldPatch);
                Patch(patchMethod, harmonyMethodType, updateTimer, timerPatch);

                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (logInfo != null) logInfo("Pause Admiral installed on SPT 4.1 world ticks and raid timer UI.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Pause Admiral patch installation failed safely: " + exception.Message);
            }
        }

        static bool WorldTickPrefix()
        {
            return !PauseRuntime.IsPaused;
        }

        static bool TimerPrefix(object __instance)
        {
            if (!PauseRuntime.IsPaused) return true;
            if (PauseRuntime.ShowPausedText) SetTimerText(__instance, "PAUSED");
            return false;
        }

        static void SetTimerText(object panel, string value)
        {
            if (panel == null) return;
            try
            {
                FieldInfo textField = ReflectionTools.FindField(panel.GetType(), "_timerText");
                object text = textField == null ? null : textField.GetValue(panel);
                if (text == null) return;
                PropertyInfo property = text.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.CanWrite) property.SetValue(text, value, null);
            }
            catch
            {
                // Visual fallback is optional; world and clock pause stay active.
            }
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int p = 1; p < parameters.Length; p++)
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object prefix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase)) arguments[i] = prefix;
            patchMethod.Invoke(harmony, arguments);
        }

        bool Fail(string message)
        {
            if (logWarning != null) logWarning(message);
            return false;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
        }
    }
}
