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
        ProtectionSettingsSync protectionSettings;
        RuntimeCustomBeltTypePatches runtimeTypePatches;
        RuntimeCustomHeadBandTypePatches runtimeHeadBandTypePatches;
        DedicatedEquipmentSlotPatches dedicatedEquipmentSlotPatches;
        DedicatedSlotPresentationPatches dedicatedSlotPresentationPatches;
        CompactFaceHeadBandPresentationPatches compactFaceHeadBandPresentationPatches;
        FirstOpenHeadBandLayoutPatches firstOpenHeadBandLayoutPatches;
        DedicatedSlotLocalizationPatches dedicatedSlotLocalizationPatches;
        HeadwearCompatibilityPatches headwearCompatibilityPatches;
        BeltContainersPanelProjectionPatches beltContainersPanelProjectionPatches;
        GridWindowSizingPatches gridWindowSizingPatches;
        LootPriorityPatches lootPatches;
        UnloadPriorityPatches unloadPatches;
        ScavBeltPatches scavPatches;
        FastAccessSlotPatches fastAccessSlotPatches;
        SlotMergePatches slotMergePatches;
        PickupSlotPatches pickupPatches;
        DedicatedWearablePickupPatches dedicatedPickupPatches;
        PaymentSlotPatches paymentPatches;
        EquipmentBuildValidationPatches buildValidationPatches;
        Coroutine deferredRuntimePump;
        Coroutine protectionSyncPump;

        void Awake()
        {
            ReflectionTools.LogWarning = Logger.LogWarning;
            modEnabled = Config.Bind("General", "Enabled", true, "Enable B&A&HB #2 MOD SPT. Runtime-candidate builds force this on at startup.");
            protectionSettings = new ProtectionSettingsSync(Config, Logger.LogInfo, Logger.LogWarning);

            if (!modEnabled.Value)
            {
                modEnabled.Value = true;
                Config.Save();
                Logger.LogInfo("B&A&HB #2 MOD SPT migrated stale Enabled=false config to Enabled=true for runtime validation.");
            }

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
                runtimeTypePatches.Dispose();
                runtimeTypePatches = null;
                Logger.LogWarning("B&A&HB #2 dedicated HeadBand runtime mapping failed; wearable runtime registration rolled back for this session.");
                return;
            }

            dedicatedEquipmentSlotPatches = new DedicatedEquipmentSlotPatches(Logger.LogInfo, Logger.LogWarning);
            if (!dedicatedEquipmentSlotPatches.TryInstall())
            {
                dedicatedEquipmentSlotPatches.Dispose();
                dedicatedEquipmentSlotPatches = null;
                runtimeHeadBandTypePatches.Dispose();
                runtimeHeadBandTypePatches = null;
                runtimeTypePatches.Dispose();
                runtimeTypePatches = null;
                Logger.LogWarning("B&A&HB #2 dedicated Belt/HeadBand equipment-slot client projection failed; dedicated runtime mappings rolled back for this session.");
                return;
            }

            dedicatedSlotPresentationPatches = new DedicatedSlotPresentationPatches(Logger.LogInfo, Logger.LogWarning);
            if (!dedicatedSlotPresentationPatches.TryInstall())
            {
                dedicatedSlotPresentationPatches.Dispose();
                dedicatedSlotPresentationPatches = null;
                Logger.LogWarning("Dedicated Belt/HeadBand equipment data remains active, but visible captions/HeadBand placement could not bind to SlotView.Show for this session.");
            }

            compactFaceHeadBandPresentationPatches = new CompactFaceHeadBandPresentationPatches(Logger.LogInfo, Logger.LogWarning);
            if (!compactFaceHeadBandPresentationPatches.TryInstall())
            {
                compactFaceHeadBandPresentationPatches.Dispose();
                compactFaceHeadBandPresentationPatches = null;
                Logger.LogWarning("Accepted stable HeadBand presentation remains active; compact Face/HeadBand layout could not bind for this session.");
            }

            firstOpenHeadBandLayoutPatches = new FirstOpenHeadBandLayoutPatches(Logger.LogInfo, Logger.LogWarning);
            if (!firstOpenHeadBandLayoutPatches.TryInstall())
            {
                firstOpenHeadBandLayoutPatches.Dispose();
                firstOpenHeadBandLayoutPatches = null;
                Logger.LogWarning("HeadBand remains visible, but first Items-tab layout may require a later native refresh for this session.");
            }
            else
            {
                FirstOpenHeadBandLayoutRuntime.RequestFlush = EnsureDeferredRuntimePump;
            }

            dedicatedSlotLocalizationPatches = new DedicatedSlotLocalizationPatches(Logger.LogInfo, Logger.LogWarning);
            if (!dedicatedSlotLocalizationPatches.TryInstall())
            {
                dedicatedSlotLocalizationPatches.Dispose();
                dedicatedSlotLocalizationPatches = null;
                Logger.LogWarning("Dedicated wearable slots remain active, but Belt/HeadBand captions may use the English fallback for this session.");
            }

            headwearCompatibilityPatches = new HeadwearCompatibilityPatches(Logger.LogInfo, Logger.LogWarning);
            if (!headwearCompatibilityPatches.TryInstall())
            {
                headwearCompatibilityPatches.Dispose();
                headwearCompatibilityPatches = null;
                Logger.LogWarning("Dedicated HeadBand remains active, but vanilla Headwear may still show a misleading compatibility highlight for Emergency HeadBand.");
            }

            beltContainersPanelProjectionPatches = new BeltContainersPanelProjectionPatches(Logger.LogInfo, Logger.LogWarning);
            if (!beltContainersPanelProjectionPatches.TryInstall())
            {
                beltContainersPanelProjectionPatches.Dispose();
                beltContainersPanelProjectionPatches = null;
                Logger.LogWarning("Dedicated Belt equipment data remains active, but the Belt row could not be projected into EFT ContainersPanel for this session.");
            }

            Logger.LogInfo("B&A&HB #2 wearable presentation uses native SlotView/GridWindow paths with fixed dedicated Belt and HeadBand locations.");

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
            else
            {
                dedicatedPickupPatches = new DedicatedWearablePickupPatches(Logger.LogInfo, Logger.LogWarning);
                if (!dedicatedPickupPatches.TryInstall())
                {
                    dedicatedPickupPatches.Dispose();
                    dedicatedPickupPatches = null;
                    Logger.LogWarning("Core ArmBand pickup remains active, but exact Magazine Belt/Emergency HeadBand auto-placement is disabled for this session.");
                }
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

            protectionSyncPump = StartCoroutine(SyncProtectionSettingsBounded());
            Logger.LogInfo("B&A&HB #2 MOD SPT wearable-container core initialized without idle polling.");
        }

        IEnumerator SyncProtectionSettingsBounded()
        {
            const int maxAttempts = 30;
            for (int i = 0; i < maxAttempts; i++)
            {
                if (protectionSettings != null && protectionSettings.TryBindAndSync())
                {
                    protectionSyncPump = null;
                    yield break;
                }
                yield return null;
            }
            Logger.LogWarning("B&A&HB protection F12 settings could not reach the server during bounded startup sync; all three categories remain Protected until a later setting change succeeds.");
            protectionSyncPump = null;
        }

        void EnsureDeferredRuntimePump()
        {
            if (deferredRuntimePump == null) deferredRuntimePump = StartCoroutine(FlushDeferredRuntimeWork());
        }

        IEnumerator FlushDeferredRuntimeWork()
        {
            yield return null;
            while ((gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending)
                || (firstOpenHeadBandLayoutPatches != null && FirstOpenHeadBandLayoutRuntime.HasPending))
            {
                if (gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending)
                    GridWindowSizingRuntime.Flush();
                if (firstOpenHeadBandLayoutPatches != null && FirstOpenHeadBandLayoutRuntime.HasPending)
                    FirstOpenHeadBandLayoutRuntime.Flush();

                if ((gridWindowSizingPatches != null && GridWindowSizingRuntime.HasPending)
                    || (firstOpenHeadBandLayoutPatches != null && FirstOpenHeadBandLayoutRuntime.HasPending))
                    yield return null;
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
            if (protectionSyncPump != null)
            {
                StopCoroutine(protectionSyncPump);
                protectionSyncPump = null;
            }
            if (deferredRuntimePump != null)
            {
                StopCoroutine(deferredRuntimePump);
                deferredRuntimePump = null;
            }
            if (buildValidationPatches != null) buildValidationPatches.Dispose();
            buildValidationPatches = null;
            if (paymentPatches != null) paymentPatches.Dispose();
            paymentPatches = null;
            if (dedicatedPickupPatches != null) dedicatedPickupPatches.Dispose();
            dedicatedPickupPatches = null;
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
            if (beltContainersPanelProjectionPatches != null) beltContainersPanelProjectionPatches.Dispose();
            beltContainersPanelProjectionPatches = null;
            if (headwearCompatibilityPatches != null) headwearCompatibilityPatches.Dispose();
            headwearCompatibilityPatches = null;
            if (dedicatedSlotLocalizationPatches != null) dedicatedSlotLocalizationPatches.Dispose();
            dedicatedSlotLocalizationPatches = null;
            if (firstOpenHeadBandLayoutPatches != null) firstOpenHeadBandLayoutPatches.Dispose();
            firstOpenHeadBandLayoutPatches = null;
            if (compactFaceHeadBandPresentationPatches != null) compactFaceHeadBandPresentationPatches.Dispose();
            compactFaceHeadBandPresentationPatches = null;
            if (dedicatedSlotPresentationPatches != null) dedicatedSlotPresentationPatches.Dispose();
            dedicatedSlotPresentationPatches = null;
            if (dedicatedEquipmentSlotPatches != null) dedicatedEquipmentSlotPatches.Dispose();
            dedicatedEquipmentSlotPatches = null;
            if (runtimeHeadBandTypePatches != null) runtimeHeadBandTypePatches.Dispose();
            runtimeHeadBandTypePatches = null;
            if (runtimeTypePatches != null) runtimeTypePatches.Dispose();
            runtimeTypePatches = null;
            if (protectionSettings != null) protectionSettings.Dispose();
            protectionSettings = null;
            ReflectionTools.ResetDiagnostics();
        }
    }
}
