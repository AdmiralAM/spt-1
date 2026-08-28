using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal static class HeadBandUtilityPolicy
    {
        internal const string Rouble = "5449016a4bdc2d6f028b456f";
        internal const string Dollar = "5696686a4bdc2da3298b456a";
        internal const string Euro = "569668774bdc2da2298b4568";

        internal const string ApolloSoyuz = "573475fb24597737fb1379e1";
        internal const string Malboro = "573476d324597737da2adc13";
        internal const string Wilston = "573476f124597737e04bf328";
        internal const string Strike = "5734770f24597738025ee254";

        internal const string VanillaWallet = "5783c43d2459774bbe137486";

        internal static readonly IReadOnlyList<string> AcceptedTemplateIds = new[]
        {
            Rouble,
            Dollar,
            Euro,
            ApolloSoyuz,
            Malboro,
            Wilston,
            Strike,
            VanillaWallet
        };

        internal static bool IsAccepted(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return false;
            for (int i = 0; i < AcceptedTemplateIds.Count; i++)
                if (string.Equals(AcceptedTemplateIds[i], templateId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
