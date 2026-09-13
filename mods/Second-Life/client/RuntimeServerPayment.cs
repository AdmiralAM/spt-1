using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Admiral.SecondLife.Client
{
    internal static class RuntimeServerPayment
    {
        const string Route = "/second-life/v1/payment";

        internal static bool TryReserve(int cost, out string token, out bool reserved, out int available, out string failure)
        {
            token = Guid.NewGuid().ToString("N");
            return Send("reserve", token, cost, out reserved, out available, out failure);
        }

        internal static bool Commit(string token, out string failure) =>
            Send("commit", token, 0, out bool reserved, out _, out failure) && reserved;

        internal static bool Finalize(string token, out string failure) =>
            Send("finalize", token, 0, out bool reserved, out _, out failure) && reserved;

        internal static void Release(string token)
        {
            if (!string.IsNullOrWhiteSpace(token)) Send("release", token, 0, out _, out _, out _);
        }

        internal static bool Refund(string token, out string failure)
        {
            failure = null;
            return string.IsNullOrWhiteSpace(token) ||
                (Send("refund", token, 0, out bool reserved, out _, out failure) && reserved);
        }

        static bool Send(string action, string token, int cost, out bool reserved, out int available, out string failure)
        {
            reserved = false;
            available = 0;
            failure = null;
            try
            {
                Type handler = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SPT.Common.Http.RequestHandler", false)).FirstOrDefault(t => t != null);
                MethodInfo post = handler?.GetMethod("PostJson", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
                if (post == null) return Fail("SPT server transport is unavailable", out failure);
                string body = $"{{\"Action\":\"{action}\",\"Token\":\"{token}\",\"Cost\":{cost}}}";
                string response = post.Invoke(null, new object[] { Route, body }) as string;
                if (string.IsNullOrWhiteSpace(response)) return Fail("SPT payment server returned no response", out failure);
                bool ok = ReadBool(response, "Ok");
                reserved = ReadBool(response, "Reserved");
                available = ReadInt(response, "Available");
                if (!ok) return Fail(ReadString(response, "Message") ?? "SPT payment server rejected the request", out failure);
                return true;
            }
            catch (Exception exception) { return Fail("SPT payment request failed: " + (exception.InnerException?.Message ?? exception.Message), out failure); }
        }

        static bool ReadBool(string json, string name) => Regex.IsMatch(json, $"[\\\"']{name}[\\\"']\\s*:\\s*true", RegexOptions.IgnoreCase);
        static int ReadInt(string json, string name) { Match match = Regex.Match(json, $"[\\\"']{name}[\\\"']\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase); return match.Success ? int.Parse(match.Groups[1].Value) : 0; }
        static string ReadString(string json, string name) { Match match = Regex.Match(json, $"[\\\"']{name}[\\\"']\\s*:\\s*[\\\"']([^\\\"']*)", RegexOptions.IgnoreCase); return match.Success ? match.Groups[1].Value : null; }
        static bool Fail(string message, out string failure) { failure = message; return false; }
    }
}
