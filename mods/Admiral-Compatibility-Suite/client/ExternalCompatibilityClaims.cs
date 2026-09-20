using System;
using System.Linq;
using System.Reflection;

namespace AdmiralCompatibilitySuite;

internal static class ExternalCompatibilityClaims
{
    internal const int RequiredContractVersion = 1;
    internal const string PackNStrapPluginGuid = "com.wtt.packnstrap";
    private static readonly object OwnerToken = new object();

    internal static bool TryClaimClient(bool packNStrapPresent, Action<string> info, Action<string> warning)
    {
        try
        {
            Type api = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("SPTBeltArmbandInventory.ExternalCompatibilityApi", false))
                .Where(type => type != null)
                .SingleOrDefault();
            if (api == null) return false;

            FieldInfo version = api.GetField("ContractVersion", BindingFlags.Public | BindingFlags.Static);
            if (version == null || (int)version.GetRawConstantValue() != RequiredContractVersion)
                return false;

            MethodInfo claimTgc = api.GetMethod("TryClaimTgc300", BindingFlags.Public | BindingFlags.Static);
            MethodInfo claimPack = api.GetMethod("TryClaimPackNStrap211", BindingFlags.Public | BindingFlags.Static);
            if (claimTgc == null || claimPack == null) return false;

            bool tgcClaimed = (bool)claimTgc.Invoke(null, new object[] { RequiredContractVersion, OwnerToken });
            bool packClaimed = !packNStrapPresent
                || (bool)claimPack.Invoke(null, new object[] { RequiredContractVersion, OwnerToken });
            if (!tgcClaimed || !packClaimed) return false;

            info?.Invoke(packNStrapPresent
                ? "Admiral Compatibility Suite claimed Belt TGC 3.0.0 and Pack 'n' Strap 2.1.1 compatibility ownership."
                : "Admiral Compatibility Suite claimed Belt TGC 3.0.0 compatibility ownership; Pack 'n' Strap is absent.");
            return true;
        }
        catch (Exception exception)
        {
            warning?.Invoke($"Admiral Compatibility Suite external Belt ownership claim failed safely: {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }
}
