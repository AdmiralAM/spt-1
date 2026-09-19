using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory;

public static class SecureContainerCompatibilityPolicy
{
    public const string PackNStrapContainerParent = "680fd1dae5044e670a092e16";
    public const string PackNStrapPlateContainer = "6a3c0e9643138b61c8739586";

    public static readonly IReadOnlyCollection<string> GammaTemplateIds = new[]
    {
        "5857a8bc2459772bad15db29",
        "665ee77ccf2d642e98220bca",
        "68f117b8121d878a2303eee0",
        "68f8e04eae031982b00e7aaf"
    };

    public static bool IsSupportedPackNStrapContainer(string templateId, IEnumerable<string> ancestorIds)
    {
        if (string.Equals(templateId, PackNStrapPlateContainer, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (string ancestorId in ancestorIds)
            if (string.Equals(ancestorId, PackNStrapContainerParent, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
