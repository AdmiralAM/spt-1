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
        public const string PluginName = "B&A&HB MOD SPT";
        public const string PluginVersion = "0.1.0";

        ConfigEntry<bool> modEnabled;
        RuntimeCustomBeltTypePatches runtimeTypePatches;
        GridWindowSizingPatches gridWindowSizingPatches;
        LootPriorityPatches lootPatches;
        UnloadPriorityPatches unloadPatches;
        ScavBeltPatches scavPatches;
        GrenadeSlotPatches grenadePatches;
        FastAccessBeltSyncPatches fastAccessSyncPatches;
        FastAccessSlotPatches fastAccessSlotPatches;
        SlotMergePatches slotMergePatches;
        PickupSlotPatches pickupPatches;
        PaymentSlotPatches paymentPatches;
        EquipmentBuildValidationPatches buildValidationPatches;
        Coroutine deferredRuntimePump;

        void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true, "Enable B&A&HB MOD SPT. Runtime-candidate builds force this on at startup.");

            if (!modEnabled.Value)
            {
                modEnabled.Value = true;
                Config.Save();
                Logger.LogInfo("B&A&HB MOD SPT migrated stale Enabled=false config to Enabled=true for runtime validation.");
            }

            if (LegacyBeltSlotDetected())
            {
                Logger.LogWarning("Trenchfoot-BeltSlot is already loaded. Remove/disable that DLL before enabling B&A&HB MOD SPT; no duplicate patch was installed.");
                return;
            }

            runtimeTypePatches = new RuntimeCustomBeltTypePatches(Logger.LogInfo, Logger.LogWarning);
            if (!runtimeTypePatches.TryInstall())
            {
                runtimeTypePatches.Dispose();
                runtimeTypePatches = null;
                Logger.LogWarning("B&A&HB runtime-type proof disabled: custom searchable belt item/template registration did not install.");
                return;
            }

            Logger.LogInfo("B&A&HB ArmBand presentation uses the native searchable-item GridWindow and GeneratedGridsView; legacy ContainersPanel BELT-row projection is disabled.");

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
                Logger.LogWarning("Belt UI remains active, but automatic loot placement will use vanilla container priorities.");
            }

            unloadPatches = new UnloadPriorityPatches(Logger.LogInfo, Logger.LogWarning);
            if (!unloadPatches.TryInstall())
            {
                unloadPatches.Dispose();
                unloadPatches = null;
                Logger.LogWarning("Belt UI remains active, but unload placement will use vanilla grid priorities.");
            }

            scavPatches = new ScavBeltPatches(Logger.LogInfo, Logger.LogWarning);
            if (!scavPatches.TryInstall())
            {
                scavPatches.Dispose();
                scavPatches = null;
                Logger.LogWarning("PMC belt behavior remains active, but a Scav spawned with a container belt may retain vanilla ArmBand deletion behavior.");
            }

            grenadePatches = new GrenadeSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!grenadePatches.TryInstall())
            {
                grenadePatches.Dispose();
                grenadePatches = null;
                Logger.LogWarning("Belt storage remains active, but grenades inside the belt may not participate in vanilla G/fast-access selection.");
            }

            fastAccessSyncPatches = new FastAccessBeltSyncPatches(Logger.LogInfo, Logger.LogWarning);
            if (!fastAccessSyncPatches.TryInstall())
            {
                fastAccessSyncPatches.Dispose();
                fastAccessSyncPatches = null;
                Logger.LogWarning("Belt grenade enumeration remains active, but equipping/removing a loaded belt may require the grenade fast-access view to reopen before it reflects the change.");
            }
            else
            {
                FastAccessBeltSyncRuntime.RequestFlush = EnsureDeferredRuntimePump;
            }

            fastAccessSlotPatches = new FastAccessSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!fastAccessSlotPatches.TryInstall())
            {
                fastAccessSlotPatches.Dispose();
                fastAccessSlotPatches = null;
                Logger.LogWarning("Belt storage remains active, but non-grenade consumables inside the belt may not participate in vanilla bind/reachable fast-access logic.");
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

            paymentPatches = new PaymentSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!paymentPatches.TryInstall())
            {
                paymentPatches.Dispose();
                paymentPatches = null;
                Logger.LogWarning("Belt storage remains active, but money/items inside the belt may not be considered by vanilla in-raid trader-service payments.");
            }

            buildValidationPatches = new EquipmentBuildValidationPatches(Logger.LogInfo, Logger.LogWarning);
            if (!buildValidationPatches.TryInstall())
            {
                buildValidationPatches.Dispose();
                buildValidationPatches = null;
                Logger.LogWarning("Belt build/apply remains active, but missing belt contents may be classified under the Slots tab instead of Containers in Equipment Builds.");
            }
        }

        void EnsureDeferredRuntimePump()
        {
            if (deferredRuntimePump == null)
                deferredRuntimePump = StartCoroutine(FlushDeferredRuntimeWork());
        }

        IEnumerator FlushDeferredRuntimeWork()
        {
            // Preserve the previous next-frame behavior without keeping an idle
            // MonoBehaviour.Update callback alive for the lifetime of the plugin.
            yield return null;

            while ((gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending)
                || (fastAccessSyncPatches != null && FastAccessBeltSyncRuntime.HasPending))
            {
                if (gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending)
                    GridWindowSizingRuntime.Flush();
                if (fastAccessSyncPatches != null && FastAccessBeltSyncRuntime.HasPending)
                    FastAccessBeltSyncRuntime.Flush();

                if ((gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending)
                    || (fastAccessSyncPatches != null && FastAccessBeltSyncRuntime.HasPending))
                    yield return null;
            }

            deferredRuntimePump = null;
        }

        bool LegacyBeltSlotDetected()
        {
            try
            {
                Type chainloader = Type.GetType("BepInEx.Bootstrap.Chainloader, BepInEx", false);
                PropertyInfo pluginInfos = chainloader == null ? null : chainloader.GetProperty("PluginInfos", BindingFlags.Static | BindingFlags.Public);
                IDictionary dictionary = pluginInfos == null ? null : pluginInfos.GetValue(null, null) as IDictionary;
                return dictionary != null && (dictionary.Contains("com.trenchfoot.beltslot") || dictionary.Contains("BeltSlot"));
            }
            catch { return false; }
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
            if (fastAccessSyncPatches != null) fastAccessSyncPatches.Dispose();
            fastAccessSyncPatches = null;
            if (grenadePatches != null) grenadePatches.Dispose();
            grenadePatches = null;
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
        }
    }
}
