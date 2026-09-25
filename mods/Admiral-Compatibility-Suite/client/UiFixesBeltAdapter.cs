using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EFT.InventoryLogic;
using HarmonyLib;

namespace AdmiralCompatibilitySuite;

internal sealed class UiFixesBeltAdapter : IDisposable
{
    internal const string UpstreamPluginGuid = "com.tyfon.uifixes";
    private const string OwnerId = "com.admiralam.compatibility-suite.uifixes-belt";
    private const int RequiredBeltContractVersion = 1;

    private static MethodInfo enumerateBeltSources;
    private static int rewrittenCalls;

    private readonly Action<string> logInfo;
    private readonly Action<string> logWarning;
    private Harmony harmony;

    internal UiFixesBeltAdapter(Action<string> logInfo, Action<string> logWarning)
    {
        this.logInfo = logInfo;
        this.logWarning = logWarning;
    }

    internal bool TryInstall()
    {
        try
        {
            Type beltApi = FindUniqueType("SPTBeltArmbandInventory.BeltAccessApi")
                ?? throw new TypeLoadException("BeltAccessApi is unavailable.");
            FieldInfo contract = beltApi.GetField("ContractVersion", BindingFlags.Public | BindingFlags.Static);
            if (contract?.GetRawConstantValue() is not int version || version != RequiredBeltContractVersion)
            {
                throw new InvalidOperationException($"BeltAccessApi contract mismatch; required {RequiredBeltContractVersion}.");
            }

            PropertyInfo available = beltApi.GetProperty("IsAvailable", BindingFlags.Public | BindingFlags.Static);
            if (available?.GetValue(null) is not true)
            {
                throw new InvalidOperationException("BeltAccessApi is not active.");
            }

            enumerateBeltSources = beltApi.GetMethod(
                "TryEnumerateBeltSources",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(object), typeof(object[]).MakeByRefType() },
                null) ?? throw new MissingMethodException(beltApi.FullName, "TryEnumerateBeltSources");

            Type slowLoading = FindUniqueType("UIFixes.LoadAmmoInRaidPatches+SlowLoadingPatch")
                ?? throw new TypeLoadException("UI Fixes SlowLoadingPatch is unavailable.");
            MethodInfo target = slowLoading.GetMethod("Prefix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(slowLoading.FullName, "Prefix");

            rewrittenCalls = 0;
            harmony = new Harmony(OwnerId);
            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(UiFixesBeltAdapter), nameof(Transpiler)));
            if (rewrittenCalls != 1 || Harmony.GetPatchInfo(target)?.Owners.Contains(OwnerId) != true)
            {
                throw new InvalidOperationException($"UI Fixes ammo-source coverage incomplete: {rewrittenCalls}/1.");
            }

            logInfo?.Invoke("Admiral Compatibility Suite bridged UI Fixes in-raid ammo loading to BeltAccessApi v1; verified call sites=1.");
            return true;
        }
        catch (Exception exception)
        {
            Dispose();
            logWarning?.Invoke($"Admiral Compatibility Suite UI Fixes/Belt adapter failed safely: {Unwrap(exception).Message}");
            return false;
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = new List<CodeInstruction>();
        MethodInfo append = AccessTools.Method(typeof(UiFixesBeltAdapter), nameof(AppendBeltAmmo));
        foreach (CodeInstruction instruction in instructions)
        {
            result.Add(instruction);
            if (instruction.opcode != OpCodes.Call
                || instruction.operand is not MethodInfo called
                || !called.IsGenericMethod
                || called.Name != "GetAllAssembledItems"
                || called.DeclaringType != typeof(ItemExtensions)
                || called.GetGenericArguments() is not [Type itemType]
                || itemType != typeof(Ammo))
            {
                continue;
            }

            result.Add(new CodeInstruction(OpCodes.Ldarg_3));
            result.Add(new CodeInstruction(OpCodes.Ldloc_1));
            result.Add(new CodeInstruction(OpCodes.Call, append));
            rewrittenCalls++;
        }

        return result;
    }

    private static void AppendBeltAmmo(InventoryController controller, List<Ammo> destination)
    {
        if (controller?.Inventory == null || destination == null || enumerateBeltSources == null)
        {
            return;
        }

        object[] arguments = { controller.Inventory, null };
        if (enumerateBeltSources.Invoke(null, arguments) is not true || arguments[1] is not object[] sources)
        {
            return;
        }

        foreach (Ammo ammo in sources.OfType<Ammo>())
        {
            if (!destination.Contains(ammo))
            {
                destination.Add(ammo);
            }
        }
    }

    private static Type FindUniqueType(string fullName)
    {
        Type[] matches = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .Where(type => type != null)
            .Distinct()
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: not null } target)
        {
            exception = target.InnerException;
        }

        return exception;
    }

    public void Dispose()
    {
        harmony?.UnpatchSelf();
        harmony = null;
        enumerateBeltSources = null;
        rewrittenCalls = 0;
    }
}
