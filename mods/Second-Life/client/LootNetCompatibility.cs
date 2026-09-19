using System;
using System.Linq;
using System.Reflection;

namespace Admiral.SecondLife.Client
{
    internal static class LootNetCompatibility
    {
        const string AssemblyName = "LootNet";
        const string TrackerTypeName = "LootNet.Services.RaidTracker";

        internal static void MarkSuccessfulRecovery(Action<string> logInfo, Action<string> logWarning)
        {
            try
            {
                Assembly lootNet = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase));
                if (lootNet == null) return;

                Type tracker = lootNet.GetType(TrackerTypeName, throwOnError: false);
                PropertyInfo playerDied = tracker?.GetProperty(
                    "PlayerDied",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo isInRaid = tracker?.GetProperty(
                    "IsInRaid",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo getPlayerDied = playerDied?.GetGetMethod(nonPublic: true);
                MethodInfo setPlayerDied = playerDied?.GetSetMethod(nonPublic: true);
                MethodInfo getIsInRaid = isInRaid?.GetGetMethod(nonPublic: true);

                if (playerDied?.PropertyType != typeof(bool) || getPlayerDied == null || setPlayerDied == null)
                {
                    logWarning?.Invoke("LootNet is installed, but its PlayerDied compatibility contract is unavailable; leaving LootNet state unchanged.");
                    return;
                }

                if (isInRaid?.PropertyType == typeof(bool) && getIsInRaid != null && !(bool)getIsInRaid.Invoke(null, null))
                {
                    logWarning?.Invoke("LootNet is installed, but it is not tracking the active raid; leaving LootNet state unchanged.");
                    return;
                }

                bool wasMarkedDead = (bool)getPlayerDied.Invoke(null, null);
                if (!wasMarkedDead)
                {
                    logInfo?.Invoke("LootNet compatibility: successful recovery required no provisional-death correction.");
                    return;
                }

                setPlayerDied.Invoke(null, new object[] { false });
                if ((bool)getPlayerDied.Invoke(null, null))
                {
                    logWarning?.Invoke("LootNet compatibility could not clear the provisional first-death result.");
                    return;
                }

                logInfo?.Invoke("LootNet compatibility: cleared the provisional first-death result after successful recovery; tracked loot and kills were retained.");
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("LootNet compatibility failed safely; recovery remains active and LootNet state was left unchanged: " + Unwrap(exception).Message);
            }
        }

        static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                exception = invocation.InnerException;
            return exception;
        }
    }
}
