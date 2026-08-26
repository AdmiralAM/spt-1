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
        GridWindowSizingPatches gridWindowSizingPatches;
        LootPriorityPatches lootPatches;
        UnloadPriorityPatches unloadPatches;
        ScavBeltPatches scavPatches;
        FastAccessSlotPatches fastAccessSlotPatches;
        SlotMergePatches slotMergePatches;
        PickupSlotPatches pickupPatches;
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
                Logger.LogWarning("B&A&HB #2 runtime type registration failed; client belt behavior is disabled for this session.");
                return;
            }

            Logger.LogInfo("B&A&HB #2 ArmBand presentation uses the native searchable-item GridWindow and GeneratedGridsView; legacy ContainersPanel BELT-row projection is disabled.");

            gridWindowSizingPatches = new GridWindowSizingPatches(Logger.LogInfo, Logger.LogWarning);
            if (!gridWindowSizingPatches.TryInstall())
            {
                gridWindowSizingPatches.Dispose();
                gridWindowSizingPatches = null;
                Logger.LogWarning("Belt storage remains active, but the ArmBand GridWindow may keep vanilla minimum window dimensions.");
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
                Logger.LogWarning("Belt storage remains active, but automatic loot placement will use vanilla container priorities.");
            }

            unloadPatches = new UnloadPriorityPatches(Logger.LogInfo, Logger.LogWarning);
            if (!unloadPatches.TryInstall())
            {
                unloadPatches.Dispose();
                unloadPatches = null;
                Logger.LogWarning("Belt storage remains active, but unload placement will use vanilla grid priorities.");
            }

            scavPatches = new ScavBeltPatches(Logger.LogInfo, Logger.LogWarning);
            if (!scavPatches.TryInstall())
            {
                scavPatches.Dispose();
                scavPatches = null;
                Logger.LogWarning("PMC belt behavior remains active, but a Scav spawned with a container belt may retain vanilla ArmBand deletion behavior.");
            }

            fastAccessSlotPatches = new FastAccessSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!fastAccessSlotPatches.TryInstall())
            {
                fastAccessSlotPatches.Dispose();
                fastAccessSlotPatches = null;
                Logger.LogWarning("Belt storage remains active, but magazines inside the belt may not participate in vanilla reachable-container reload logic.");
            }

            slotMergePatches = new SlotMergePatches(Logger.LogInfo, Logger.LogWarning);
            if (!slotMergePatches.TryInstall())
            {
                slotMergePatches.Dispose();
                slotMergePatches = null;
                Logger.LogWarning("Belt storage remains active, but ArmBand parent/child merge semantics remain vanilla.");
            }

            pickupPatches = new PickupSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!pickupPatches.TryInstall())
            {
                pickupPatches.Dispose();
                pickupPatches = null;
                Logger.LogWarning("Belt storage remains active, but compatible container belts may not auto-equip into an empty ArmBand slot on pickup.");
            }

            buildValidationPatches = new EquipmentBuildValidationPatches(Logger.LogInfo, Logger.LogWarning);
            if (!buildValidationPatches.TryInstall())
            {
                buildValidationPatches.Dispose();
                buildValidationPatches = null;
                Logger.LogWarning("Belt build/apply remains active, but missing belt contents may be classified under the Slots tab instead of Containers in Equipment Builds.");
            }

            Logger.LogInfo("B&A&HB #2 Phase 1 magazine-belt core initialized without idle polling.");
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
            if (runtimeTypePatches != null) runtimeTypePatches.Dispose();
            runtimeTypePatches = null;
            ReflectionTools.ResetDiagnostics();
        }
    }
}
