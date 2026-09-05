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
            return response != null
                && expectedPayload != null
                && string.Equals(response.Trim(), expectedPayload, System.StringComparison.Ordinal);
        }

        static string JsonBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
