namespace SPTBeltArmbandInventory
{
    internal static class WearableProtectionContract
    {
        internal const string Route = "/b-and-hb/protection";

        internal static string Encode(bool armBandProtected, bool beltProtected, bool headBandProtected)
        {
            return "{\"armBandProtected\":" + JsonBool(armBandProtected)
                + ",\"beltProtected\":" + JsonBool(beltProtected)
                + ",\"headBandProtected\":" + JsonBool(headBandProtected) + "}";
        }

        internal static bool IsAcknowledgement(string response, string expectedPayload)
        {
            if (response == null || expectedPayload == null) return false;
            if (string.Equals(response.Trim(), expectedPayload, System.StringComparison.Ordinal)) return true;
            string trimmed = response.Trim();

            // SPT's StaticRouter transport can JSON-encode a route's string result.
            // Accept only the exact expected payload wrapped as one JSON string; do
            // not accept a partial/mismatched policy response.
            string quoted = "\"" + expectedPayload.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            return string.Equals(trimmed, quoted, System.StringComparison.Ordinal);
        }

        static string JsonBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
