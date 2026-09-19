using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimeLifeStatistics
    {
        LifeStatisticsSnapshot firstLife;
        LifeStatisticsSnapshot secondLifeStart;
        bool finalReportStarted;
        bool finalReportCompleted;

        internal bool FinalReportPending => finalReportStarted && !finalReportCompleted;

        internal void Reset()
        {
            firstLife = null;
            secondLifeStart = null;
            finalReportStarted = false;
            finalReportCompleted = false;
        }

        internal void CaptureSecondLifeStart(object player, Action<string> trace)
        {
            if (firstLife == null || secondLifeStart != null || player == null) return;
            secondLifeStart = Capture(player);
            trace?.Invoke($"Recovery statistics: captured second-life baseline, xp={secondLifeStart.Experience}, kills={secondLifeStart.Kills.Count}.");
        }

        internal void CaptureFirstLife(object player, Action<string> trace)
        {
            if (firstLife != null || player == null) return;
            firstLife = Capture(player);
            trace?.Invoke($"Recovery statistics: captured first life, xp={firstLife.Experience}, kills={firstLife.Kills.Count}.");
        }

        internal bool TryBeginFinalReport()
        {
            if (firstLife == null || secondLifeStart == null || finalReportStarted) return false;
            finalReportStarted = true;
            return true;
        }

        internal bool TryShow(object player, Action continueNative, out string failure)
        {
            failure = null;
            try
            {
                if (firstLife == null || secondLifeStart == null) return Fail("life-statistics snapshots are unavailable", out failure);
                LifeStatisticsReport report = LifeStatisticsReport.Separate(firstLife, secondLifeStart, Capture(player));
                Type contextType = FindType("EFT.UI.ItemUiContext");
                object context = contextType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
                MethodInfo show = contextType?.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .SingleOrDefault(method => method.Name == "ShowMessageWindow" && method.GetParameters().Length == 7);
                if (context == null || show == null) return Fail("native statistics window is unavailable", out failure);

                bool resolved = false;
                Action close = () =>
                {
                    if (resolved) return;
                    resolved = true;
                    finalReportCompleted = true;
                    continueNative?.Invoke();
                };
                show.Invoke(context, new object[]
                {
                    Format(report), close, close, "Second Life — две жизни", 0f, true,
                    Enum.ToObject(show.GetParameters()[6].ParameterType, 514)
                });
                return true;
            }
            catch (Exception exception)
            {
                return Fail("two-life statistics window failed: " + Unwrap(exception).Message, out failure);
            }
        }

        internal void CompleteFinalReport() => finalReportCompleted = true;

        static LifeStatisticsSnapshot Capture(object player)
        {
            object profile = ReadProperty(player, "Profile");
            object stats = ReadProperty(profile, "EftStats") ?? ReadField(ReadField(profile, "Stats"), "Eft");
            if (stats == null) return new LifeStatisticsSnapshot(0, Array.Empty<LifeKill>());

            int experience = ReadExperience(stats);
            var kills = new List<LifeKill>();
            if (ReadField(stats, "Victims") is IEnumerable victims)
            {
                foreach (object victim in victims.Cast<object>().Take(512))
                {
                    string profileId = ReadField(victim, "ProfileId")?.ToString() ?? string.Empty;
                    string name = ReadField(victim, "Name")?.ToString() ?? "Неизвестная цель";
                    string weapon = ReadField(victim, "Weapon")?.ToString() ?? "неизвестное оружие";
                    string bodyPart = ReadField(victim, "BodyPart")?.ToString() ?? "неизвестно";
                    float distance = Convert.ToSingle(ReadField(victim, "Distance") ?? 0f, CultureInfo.InvariantCulture);
                    object time = ReadField(victim, "Time");
                    string identity = string.Join("|", profileId, name, time, weapon, distance.ToString("R", CultureInfo.InvariantCulture));
                    kills.Add(new LifeKill(identity, name, weapon, bodyPart, distance));
                }
            }
            return new LifeStatisticsSnapshot(experience, kills);
        }

        static int ReadExperience(object stats)
        {
            try
            {
                object counters = ReadField(stats, "SessionCounters");
                Type counterTag = FindType("EFT.Counters.CounterTag");
                MethodInfo getAllInt = counters?.GetType().GetMethod(
                    "GetAllInt", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object[]) }, null);
                if (counterTag != null && getAllInt != null)
                {
                    object exp = Enum.Parse(counterTag, "Exp");
                    int value = Convert.ToInt32(getAllInt.Invoke(counters, new object[] { new object[] { exp } }));
                    if (value > 0) return value;
                }
            }
            catch { }
            return Math.Max(0, Convert.ToInt32(ReadField(stats, "TotalSessionExperience") ?? 0));
        }

        static string Format(LifeStatisticsReport report)
        {
            var text = new StringBuilder();
            AppendLife(text, "ПЕРВАЯ ЖИЗНЬ", report.FirstLife);
            text.AppendLine();
            AppendLife(text, "ВТОРАЯ ЖИЗНЬ", report.SecondLife);
            text.AppendLine().Append("Закройте окно, чтобы перейти к обычным итогам рейда.");
            return text.ToString();
        }

        static void AppendLife(StringBuilder text, string title, LifeStatisticsSnapshot life)
        {
            text.Append(title).Append(": опыт ").Append(life.Experience)
                .Append(", убийств ").Append(life.Kills.Count).AppendLine();
            foreach (LifeKill kill in life.Kills.Take(8))
            {
                text.Append("• ").Append(kill.Name)
                    .Append(" — ").Append(kill.Weapon)
                    .Append(", ").Append(kill.BodyPart)
                    .Append(", ").Append(kill.Distance.ToString("0.#", CultureInfo.CurrentCulture)).Append(" м")
                    .AppendLine();
            }
            if (life.Kills.Count > 8) text.Append("• ещё ").Append(life.Kills.Count - 8).AppendLine(" целей");
            if (life.Kills.Count == 0) text.AppendLine("• убийств нет");
        }

        static object ReadProperty(object instance, string name) => instance?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance, null);
        static object ReadField(object instance, string name) => instance == null ? null : FindField(instance.GetType(), name)?.GetValue(instance);
        static FieldInfo FindField(Type type, string name) { while (type != null) { FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) return field; type = type.BaseType; } return null; }
        static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null);
        static Exception Unwrap(Exception exception) => exception is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : exception;
        static bool Fail(string message, out string failure) { failure = message; return false; }
    }
}
