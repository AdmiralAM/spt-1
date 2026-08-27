using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.admiralam.spt.belt-armband-inventory";
        public const string PluginName = "B&A&HB #2 MOD SPT";
        public const string PluginVersion = "0.1.0";

        ConfigEntry<bool> modEnabled;
        RuntimeCustomBeltTypePatches runtimeTypePatches;
        RuntimeCustomHeadBandTypePatches runtimeHeadBandTypePatches;
        GridWindowSizingPatches gridWindowSizingPatches;
        LootPriorityPatches lootPatches;
        UnloadPriorityPatches unloadPatches;
        ScavBeltPatches scavPatches;
        FastAccessSlotPatches fastAccessSlotPatches;
        SlotMergePatches slotMergePatches;
        PickupSlotPatches pickupPatches;
        PaymentSlotPatches paymentPatches;
        EquipmentBuildValidationPatches buildValidationPatches;
        Coroutine deferredRuntimePump;

        void Awake()
        {
            ReflectionTools.LogWarning = Logger.LogWarning;
            modEnabled = Config.Bind("General", "Enabled", true, "Enable B&A&HB #2 MOD SPT. Runtime-candidate builds force this on at startup.");

            if (!modEnabled.Value)
            {
                modEnabled.Value = true;
                Config.Save();
                Logger.LogInfo("B&A&HB #2 MOD SPT migrated stale Enabled=false config to Enabled=true for runtime validation.");
            }

            // One-shot implementation-boundary discovery only. Product identities and
            // placement are fixed by DedicatedWearableSlotContract and are never chosen here.
            HostBoundaryDiscovery.Log(Logger.LogInfo, Logger.LogWarning);

            if (LegacyBeltSlotDetected())
            {
                Logger.LogWarning("Trenchfoot-BeltSlot is already loaded. Remove/disable that DLL before enabling B&A&HB #2 MOD SPT; no duplicate patch was installed.");
                return;
            }

            runtimeTypePatches = new RuntimeCustomBeltTypePatches(Logger.LogInfo, Logger.LogWarning);
            if (!runtimeTypePatches.TryInstall())
            {
                runtimeTypePatches.Dispose();
                runtimeTypePatches = null;
                Logger.LogWarning("B&A&HB #2 shared searchable runtime type registration failed; wearable-container behavior is disabled for this session.");
                return;
            }

            runtimeHeadBandTypePatches = new RuntimeCustomHeadBandTypePatches(Logger.LogInfo, Logger.LogWarning);
            if (!runtimeHeadBandTypePatches.TryInstall())
            {
                runtimeHeadBandTypePatches.Dispose();
                runtimeHeadBandTypePatches = null;
                if (runtimeTypePatches != null) runtimeTypePatches.Dispose();
                runtimeTypePatches = null;
                Logger.LogWarning("B&A&HB #2 dedicated HeadBand runtime mapping failed; wearable runtime registration rolled back for this session.");
                return;
            }

            Logger.LogInfo("B&A&HB #2 wearable presentation uses the native searchable-item GridWindow and GeneratedGridsView; legacy ContainersPanel ArmBand projection remains disabled.");

            gridWindowSizingPatches = new GridWindowSizingPatches(Logger.LogInfo, Logger.LogWarning);
            if (!gridWindowSizingPatches.TryInstall())
            {
                gridWindowSizingPatches.Dispose();
                gridWindowSizingPatches = null;
                Logger.LogWarning("Wearable storage remains active, but GridWindow sizing may keep vanilla minimum dimensions.");
            }
            else
            {
                GridWindowSizingRuntime.RequestFlush = EnsureDeferredRuntimePump;
            }

            lootPatches = new LootPriorityPatches(Logger.LogInfo, Logger.LogWarning);
            if (!lootPatches.TryInstall())
            {
                lootPatches.Dispose();
                lootPatches = null;
                Logger.LogWarning("Wearable storage remains active, but automatic loot placement will use vanilla container priorities.");
            }

            unloadPatches = new UnloadPriorityPatches(Logger.LogInfo, Logger.LogWarning);
            if (!unloadPatches.TryInstall())
            {
                unloadPatches.Dispose();
                unloadPatches = null;
                Logger.LogWarning("Wearable storage remains active, but unload placement will use vanilla grid priorities.");
            }

            scavPatches = new ScavBeltPatches(Logger.LogInfo, Logger.LogWarning);
            if (!scavPatches.TryInstall())
            {
                scavPatches.Dispose();
                scavPatches = null;
                Logger.LogWarning("PMC wearable behavior remains active, but a Scav spawned with a container ArmBand may retain vanilla ArmBand deletion behavior.");
            }

            fastAccessSlotPatches = new FastAccessSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!fastAccessSlotPatches.TryInstall())
            {
                fastAccessSlotPatches.Dispose();
                fastAccessSlotPatches = null;
                Logger.LogWarning("Wearable storage remains active, but magazines inside compatible wearable containers may not participate in vanilla reachable-container reload logic.");
            }

            slotMergePatches = new SlotMergePatches(Logger.LogInfo, Logger.LogWarning);
            if (!slotMergePatches.TryInstall())
            {
                slotMergePatches.Dispose();
                slotMergePatches = null;
                Logger.LogWarning("Wearable storage remains active, but wearable parent/child merge semantics remain vanilla.");
            }

            pickupPatches = new PickupSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!pickupPatches.TryInstall())
            {
                pickupPatches.Dispose();
                pickupPatches = null;
                Logger.LogWarning("Wearable storage remains active, but compatible wearable items may not auto-equip through the optional pickup integration.");
            }

            paymentPatches = new PaymentSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!paymentPatches.TryInstall())
            {
                paymentPatches.Dispose();
                paymentPatches = null;
                Logger.LogWarning("Wearable storage remains active, but payment-capable wearable contents may not participate in vanilla payment-source enumeration.");
            }

            buildValidationPatches = new EquipmentBuildValidationPatches(Logger.LogInfo, Logger.LogWarning);
            if (!buildValidationPatches.TryInstall())
            {
                buildValidationPatches.Dispose();
                buildValidationPatches = null;
                Logger.LogWarning("Wearable build/apply remains active, but missing wearable contents may be classified under Slots instead of Containers in Equipment Builds.");
            }

            Logger.LogInfo("B&A&HB #2 MOD SPT wearable-container core initialized without idle polling.");
        }

        void EnsureDeferredRuntimePump()
        {
            if (deferredRuntimePump == null) deferredRuntimePump = StartCoroutine(FlushDeferredRuntimeWork());
        }

        IEnumerator FlushDeferredRuntimeWork()
        {
            yield return null;
            while (gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending)
            {
                GridWindowSizingRuntime.Flush();
                if (gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending) yield return null;
            }
            deferredRuntimePump = null;
        }

        bool LegacyBeltSlotDetected()
        {
            try
            {
                Type chainloader = Type.GetType("BepInEx.Bootstrap.Chainloader, BepInEx", false);
                PropertyInfo pluginInfos = ReflectionTools.FindInstanceProperty(chainloader, "PluginInfos");
                if (pluginInfos == null && chainloader != null)
                {
                    PropertyInfo[] properties = chainloader.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    for (int i = 0; i < properties.Length; i++)
                    {
                        if (string.Equals(properties[i].Name, "PluginInfos", StringComparison.Ordinal))
                        {
                            pluginInfos = properties[i];
                            break;
                        }
                    }
                }
                IDictionary dictionary = pluginInfos == null ? null : pluginInfos.GetValue(null, null) as IDictionary;
                return dictionary != null && (dictionary.Contains("com.trenchfoot.beltslot") || dictionary.Contains("BeltSlot"));
            }
            catch (Exception exception)
            {
                Logger.LogWarning("B&A&HB legacy-plugin discovery failed closed: " + exception.GetType().FullName + ": " + exception.Message);
                return false;
            }
        }

        void OnDestroy()
        {
            if (deferredRuntimePump != null)
            {
                StopCoroutine(deferredRuntimePump);
                deferredRuntimePump = null;
            }
            if (buildValidationPatches != null) buildValidationPatches.Dispose();
            buildValidationPatches = null;
            if (paymentPatches != null) paymentPatches.Dispose();
            paymentPatches = null;
            if (pickupPatches != null) pickupPatches.Dispose();
            pickupPatches = null;
            if (slotMergePatches != null) slotMergePatches.Dispose();
            slotMergePatches = null;
            if (fastAccessSlotPatches != null) fastAccessSlotPatches.Dispose();
            fastAccessSlotPatches = null;
            if (scavPatches != null) scavPatches.Dispose();
            scavPatches = null;
            if (unloadPatches != null) unloadPatches.Dispose();
            unloadPatches = null;
            if (lootPatches != null) lootPatches.Dispose();
            lootPatches = null;
            if (gridWindowSizingPatches != null) gridWindowSizingPatches.Dispose();
            gridWindowSizingPatches = null;
            if (runtimeHeadBandTypePatches != null) runtimeHeadBandTypePatches.Dispose();
            runtimeHeadBandTypePatches = null;
            if (runtimeTypePatches != null) runtimeTypePatches.Dispose();
            runtimeTypePatches = null;
            ReflectionTools.ResetDiagnostics();
        }
    }
}
