using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class ReloadExecutionPublicationGuardRegression
    {
        class FakeItem { }
        sealed class FakeMagazine : FakeItem { }

        [ModuleInitializer]
        internal static void Run()
        {
            MethodInfo oldMethod = ReloadCandidateBridgeRuntime.GetItemsInSlots;
            object oldArgument = ReloadCandidateBridgeRuntime.BeltSlotsArgument;
            Type oldItemType = ReloadCandidateBridgeRuntime.ItemType;
            Type oldMagazineType = ReloadCandidateBridgeRuntime.MagazineType;
            Type oldReturnType = ReloadCandidateBridgeRuntime.ReturnType;
            Func<object, IEnumerable> oldParents = ReloadCandidateBridgeRuntime.GetAllParentItems;
            Func<object, string> oldTemplateId = ReloadCandidateBridgeRuntime.ReadTemplateId;

            try
            {
                MethodInfo query = typeof(ReloadExecutionPublicationGuardRegression).GetMethod(nameof(Query), BindingFlags.Static | BindingFlags.NonPublic);
                var argument = new List<int> { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
                Func<object, IEnumerable> parents = Parents;
                Func<object, string> templateId = TemplateId;

                ReloadCandidateBridgeRuntime.GetItemsInSlots = query;
                ReloadCandidateBridgeRuntime.BeltSlotsArgument = argument;
                ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
                ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
                ReloadCandidateBridgeRuntime.ReturnType = query.ReturnType;
                ReloadCandidateBridgeRuntime.GetAllParentItems = parents;
                ReloadCandidateBridgeRuntime.ReadTemplateId = templateId;

                ReloadExecutionPublicationGuard.Snapshot snapshot = ReloadExecutionPublicationGuard.CaptureForRegression();
                Require(snapshot.IsComplete, "complete bridge execution state is captured");
                Require(ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "unchanged captured execution state remains publishable");

                ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(ReloadExecutionPublicationGuardRegression).GetMethod(nameof(AlternateQuery), BindingFlags.Static | BindingFlags.NonPublic);
                Require(!ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "MethodInfo replacement fails closed");
                ReloadCandidateBridgeRuntime.GetItemsInSlots = query;

                ReloadCandidateBridgeRuntime.BeltSlotsArgument = new List<int> { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
                Require(!ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "value-identical pseudo-slot argument replacement fails closed");
                ReloadCandidateBridgeRuntime.BeltSlotsArgument = argument;

                ReloadCandidateBridgeRuntime.ItemType = typeof(object);
                Require(!ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "ItemType replacement fails closed");
                ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);

                ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeItem);
                Require(!ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "MagazineType replacement fails closed");
                ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);

                ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<object>);
                Require(!ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "ReturnType replacement fails closed");
                ReloadCandidateBridgeRuntime.ReturnType = query.ReturnType;

                ReloadCandidateBridgeRuntime.GetAllParentItems = AlternateParents;
                Require(!ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "parent-reader delegate replacement fails closed");
                ReloadCandidateBridgeRuntime.GetAllParentItems = parents;

                ReloadCandidateBridgeRuntime.ReadTemplateId = AlternateTemplateId;
                Require(!ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "template-id reader delegate replacement fails closed");
                ReloadCandidateBridgeRuntime.ReadTemplateId = templateId;

                Require(ReloadExecutionPublicationGuard.ShouldPublishForRegression(snapshot), "exact captured identities recover after fixture restoration");

                Require(!ReloadExecutionPublicationGuard.ShouldKeepAssemblyLoadSubscriptionForRegression(true, false), "successful post-subscription retry removes AssemblyLoad handler");
                Require(!ReloadExecutionPublicationGuard.ShouldKeepAssemblyLoadSubscriptionForRegression(true, true), "terminal state dominates successful retry cleanup");
                Require(!ReloadExecutionPublicationGuard.ShouldKeepAssemblyLoadSubscriptionForRegression(false, true), "terminal retry removes AssemblyLoad handler");
                Require(ReloadExecutionPublicationGuard.ShouldKeepAssemblyLoadSubscriptionForRegression(false, false), "unavailable non-terminal Harmony retry retains AssemblyLoad handler");
            }
            finally
            {
                ReloadCandidateBridgeRuntime.GetItemsInSlots = oldMethod;
                ReloadCandidateBridgeRuntime.BeltSlotsArgument = oldArgument;
                ReloadCandidateBridgeRuntime.ItemType = oldItemType;
                ReloadCandidateBridgeRuntime.MagazineType = oldMagazineType;
                ReloadCandidateBridgeRuntime.ReturnType = oldReturnType;
                ReloadCandidateBridgeRuntime.GetAllParentItems = oldParents;
                ReloadCandidateBridgeRuntime.ReadTemplateId = oldTemplateId;
            }
        }

        static IEnumerable<FakeItem> Query(IEnumerable<int> slots) { return Array.Empty<FakeItem>(); }
        static IEnumerable<FakeItem> AlternateQuery(IEnumerable<int> slots) { return Array.Empty<FakeItem>(); }
        static IEnumerable Parents(object item) { return Array.Empty<object>(); }
        static IEnumerable AlternateParents(object item) { return Array.Empty<object>(); }
        static string TemplateId(object item) { return "template"; }
        static string AlternateTemplateId(object item) { return "template"; }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Reload execution publication guard regression failed: " + message);
        }
    }
}
