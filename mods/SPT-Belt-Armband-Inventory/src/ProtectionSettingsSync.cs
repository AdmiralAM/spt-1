using System;
using System.Reflection;
using BepInEx.Configuration;

namespace SPTBeltArmbandInventory
{
    internal enum DeathLossMode
    {
        Protected,
        LostOnDeath
    }

    internal sealed class ProtectionSettingsSync : IDisposable
    {
        readonly ConfigEntry<DeathLossMode> armBand;
        readonly ConfigEntry<DeathLossMode> belt;
        readonly ConfigEntry<DeathLossMode> headBand;
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        Func<string, string, string> postJson;
        bool transportWarningLogged;

        internal ProtectionSettingsSync(ConfigFile config, Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;

            armBand = config.Bind(
                "Protection",
                "ArmBand",
                DeathLossMode.Protected,
                "ArmBand container family death behavior: Protected or LostOnDeath.");
            belt = config.Bind(
                "Protection",
                "Belt",
                DeathLossMode.Protected,
                "Belt container family death behavior: Protected or LostOnDeath.");
            headBand = config.Bind(
                "Protection",
                "HeadBand",
                DeathLossMode.Protected,
                "HeadBand container family death behavior: Protected or LostOnDeath.");

            armBand.SettingChanged += OnSettingChanged;
            belt.SettingChanged += OnSettingChanged;
            headBand.SettingChanged += OnSettingChanged;
        }

        internal bool TryBindAndSync()
        {
            if (postJson == null && !TryBindTransport()) return false;
            return Sync();
        }

        bool TryBindTransport()
        {
            Type requestHandler = ReflectionTools.FindType("SPT.Common.Http.RequestHandler");
            if (requestHandler == null) return false;

            MethodInfo selected = null;
            MethodInfo[] methods = requestHandler.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (!string.Equals(method.Name, "PostJson", StringComparison.Ordinal)
                    || method.ReturnType != typeof(string)
                    || parameters.Length != 2
                    || parameters[0].ParameterType != typeof(string)
                    || parameters[1].ParameterType != typeof(string))
                    continue;
                if (selected != null) return false;
                selected = method;
            }
            if (selected == null) return false;

            try
            {
                postJson = (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>), selected);
                return true;
            }
            catch
            {
                postJson = null;
                return false;
            }
        }

        bool Sync()
        {
            if (postJson == null) return false;
            try
            {
                string payload = WearableProtectionContract.Encode(
                    armBand.Value == DeathLossMode.Protected,
                    belt.Value == DeathLossMode.Protected,
                    headBand.Value == DeathLossMode.Protected);
                string response = postJson(WearableProtectionContract.Route, payload);
                if (!WearableProtectionContract.IsAcknowledgement(response, payload))
                    throw new InvalidOperationException("server acknowledgement did not match the applied protection snapshot");

                logInfo?.Invoke("B&A&HB protection settings synced and acknowledged: ArmBand=" + armBand.Value
                    + ", Belt=" + belt.Value + ", HeadBand=" + headBand.Value + ".");
                transportWarningLogged = false;
                return true;
            }
            catch (Exception exception)
            {
                if (!transportWarningLogged)
                {
                    transportWarningLogged = true;
                    logWarning?.Invoke("B&A&HB protection settings sync failed safely; server defaults/current acknowledged policy remain authoritative: "
                        + Unwrap(exception).GetType().Name + ": " + Unwrap(exception).Message);
                }
                return false;
            }
        }

        void OnSettingChanged(object sender, EventArgs args)
        {
            if (postJson == null)
            {
                if (!TryBindTransport()) return;
            }
            Sync();
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        public void Dispose()
        {
            armBand.SettingChanged -= OnSettingChanged;
            belt.SettingChanged -= OnSettingChanged;
            headBand.SettingChanged -= OnSettingChanged;
            postJson = null;
        }
    }
}
