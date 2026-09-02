import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipStockContractTests(unittest.TestCase):
    def load(self, name):
        return json.loads((ROOT / "manifests" / name).read_text(encoding="utf-8"))

    def test_relationship_contract_matches_gameplay_policy(self):
        policy = self.load("gameplay-policy.json")
        relationship = self.load("relationship-stock.json")

        self.assertEqual(relationship["schemaVersion"], 4)
        self.assertEqual(relationship["stockClass"], "Relationship")
        self.assertTrue(policy["traderStock"]["relationshipStockAllowed"])
        self.assertFalse(relationship["authority"]["salesSumGateAllowed"])
        self.assertFalse(relationship["authority"]["questGateAllowed"])
        self.assertFalse(relationship["authority"]["capabilityAuthority"])
        self.assertTrue(relationship["authority"]["finiteStockRequired"])
        self.assertEqual(
            relationship["authority"]["standingThresholds"],
            policy["loyalty"]["expectedStandingThresholds"][1:],
        )

        rules = relationship["designRules"]
        self.assertFalse(rules["tierFillQuotaAllowed"])
        self.assertTrue(rules["emptyTierAllowedWhenNoCandidatePasses"])

        tiers = relationship["tiers"]
        self.assertEqual([x["loyaltyLevel"] for x in tiers], [2, 3, 4])
        self.assertEqual([x["standing"] for x in tiers], policy["loyalty"]["expectedStandingThresholds"][1:])
        self.assertTrue(all(x["selection"]["minimumRequiredOffers"] == 0 for x in tiers))
        self.assertTrue(all(x["selection"]["maximumOffers"] > 0 for x in tiers))
        self.assertTrue(all(x["selection"]["qualityGateOverridesCount"] for x in tiers))

    def test_economy_relationship_adapter_integration_is_current_and_uplift_path_is_separate(self):
        relationship = self.load("relationship-stock.json")
        integration = relationship["integrationState"]
        self.assertEqual(integration["economyAdapterRelationshipParsing"], "implemented-on-origin-main")
        self.assertEqual(integration["verifiedOriginMain"], "4a31b134c70bdc7dbec3af036f3e6772c7dbbf5f")
        self.assertEqual(integration["staticCompatibilityProof"], "relationship-economy-static-compatibility-proof.json")
        self.assertEqual(
            set(integration["adapterFiles"]),
            {
                "mods/Economy-Admiral/server/AdmiralTraderRuntimeAdapterService.cs",
                "mods/Economy-Admiral/server/AdmiralTraderGameplayAlphaAdapter.cs",
            },
        )
        self.assertIn("RelationshipOfferCount is emitted by the Economy adapter", integration["provenContract"])
        self.assertTrue(any("immutable Baseline source assort" in x for x in integration["provenContract"]))

        uplift = relationship["standingUpliftState"]
        self.assertFalse(uplift["newRelationshipOffer"])
        self.assertTrue(uplift["economyEnvelopeApproved"])
        self.assertTrue(uplift["profileScopedProjectionImplemented"])
        self.assertTrue(uplift["profileLevelAndStandingResolverImplemented"])
        self.assertTrue(uplift["loyaltyThresholdContractLocked"])
        self.assertFalse(uplift["globalSourceMutationAllowed"])
        self.assertFalse(uplift["runtimeMaterializationEnabled"])
        self.assertFalse(uplift["physicalCheckpointRequestedNow"])

        materialization = relationship["materialization"]
        new_offer_gates = materialization["newOfferPathRequiredBeforeEnable"]
        uplift_gates = materialization["standingUpliftPathRequiredBeforeEnable"]
        self.assertTrue(any("Economy Admiral" in entry for entry in new_offer_gates))
        self.assertEqual(len(uplift_gates), 1)
        self.assertIn("physical runtime proof", uplift_gates[0])
        self.assertNotIn("Economy Admiral", uplift_gates[0])

    def test_frozen_candidate_is_not_materialized_by_relationship_design(self):
        relationship = self.load("relationship-stock.json")
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        baseline = self.load("baseline-stock.json")
        questassort = json.loads((ROOT / "db" / "questassort.json").read_text(encoding="utf-8"))

        self.assertFalse(relationship["materialization"]["enabled"])
        self.assertEqual(len(assort["items"]), 11)
        self.assertEqual(len(baseline["offers"]), 4)
        self.assertEqual(len(questassort["success"]), 7)
        classified = {x["offerId"] for x in baseline["offers"]} | set(questassort["success"])
        self.assertEqual(classified, {x["_id"] for x in assort["items"]})

    def test_relationship_cannot_bypass_capability_boundaries(self):
        relationship = self.load("relationship-stock.json")
        rules = relationship["designRules"]
        self.assertFalse(rules["permanentHighEndAmmoAllowed"])
        self.assertFalse(rules["labsAccessAllowed"])
        self.assertFalse(rules["specialWeaponsSupplyAllowed"])
        self.assertFalse(rules["questMilestoneSubstituteAllowed"])
        self.assertEqual(rules["directOverlapAuditRequired"], ["vanilla", "Scorpion", "Artem"])
        self.assertTrue(rules["runtimeMaterializationRequiresItemTplProof"])
        self.assertTrue(rules["runtimeMaterializationRequiresEconomyReview"])

    def test_candidate_evidence_is_review_only_and_rejections_are_explicit(self):
        candidate_doc = self.load("relationship-stock-candidates.json")
        self.assertFalse(candidate_doc["policy"]["materialize"])
        self.assertTrue(candidate_doc["policy"]["requiresPinnedTraderAssortOverlapProof"])
        self.assertTrue(candidate_doc["policy"]["requiresEconomyAdmiralReview"])
        self.assertTrue(candidate_doc["policy"]["rejectedCandidatesMustNeverMaterialize"])
        self.assertTrue(candidate_doc["policy"]["independentRecurringUtilityRequired"])
        self.assertTrue(candidate_doc["policy"]["rawBarterSupplyRejectedEvenWhenEconomyEnvelopeIsSafe"])
        self.assertTrue(candidate_doc["sourceBaseline"]["dynamicFenceExcludedFromDirectFixedAssortUniqueness"])
        self.assertGreaterEqual(len(candidate_doc["sourceBaseline"]["fixedTraderAssortsReviewed"]), 12)

        candidates = candidate_doc["candidates"]
        by_tpl = {x["tpl"]: x for x in candidates}
        self.assertEqual(len(by_tpl), len(candidates))
        military_cable = by_tpl["59e36c6f86f774176c10a2a7"]
        self.assertEqual(military_cable["decision"], "reject-raw-barter-supply-no-independent-utility")
        self.assertEqual(military_cable["overlap"]["vanillaPinnedFixedAssorts"], "no-direct-tpl-hit")
        self.assertEqual(military_cable["overlap"]["scorpionPinned"], "no-exact-tpl-hit-in-repository-code-search")
        self.assertEqual(military_cable["overlap"]["artemPinned"], "no-exact-tpl-hit-in-repository-code-search")
        self.assertFalse(military_cable["admissionFailure"]["independentRecurringUtility"])
        self.assertTrue(military_cable["admissionFailure"]["rawBarterSupply"])
        self.assertFalse(military_cable["admissionFailure"]["economyReviewCanOverride"])

        cable_review = self.load("relationship-military-cable-review.json")
        self.assertEqual(cable_review["decision"]["state"], military_cable["decision"])
        self.assertFalse(cable_review["decision"]["materialize"])
        self.assertFalse(cable_review["decision"]["economyReviewCanOverride"])
        self.assertFalse(cable_review["findings"]["independentRecurringFieldUtility"])
        self.assertTrue(cable_review["findings"]["rawMaterialComponent"])

        self.assertEqual(by_tpl["590c2e1186f77425357b6124"]["decision"], "reject-redundant-pinned-vanilla")
        self.assertEqual(by_tpl["5910968f86f77425cf569c32"]["decision"], "reject-redundant-pinned-vanilla")
        self.assertEqual(by_tpl["591094e086f7747caa7bb2ef"]["decision"], "reject-pinned-vanilla-and-economic-impact")
        self.assertEqual(by_tpl["544fb5454bdc2df8738b456a"]["decision"], "reject-redundant-pinned-vanilla")
        self.assertEqual(by_tpl["5ac78a9b86f7741cca0bbd8d"]["decision"], "reject-redundant-pinned-vanilla")
        self.assertTrue(all(x["spt413ReferencePriceRub"] > 0 for x in candidates))

        pinned_direct_overlap = {
            x["tpl"]
            for x in candidates
            if x.get("overlap", {}).get("vanillaPinnedAssort") in {"mechanic-direct-unlimited", "ref-direct-unlimited"}
        }
        self.assertIn("590c2e1186f77425357b6124", pinned_direct_overlap)
        self.assertIn("5910968f86f77425cf569c32", pinned_direct_overlap)
        self.assertIn("591094e086f7747caa7bb2ef", pinned_direct_overlap)
        self.assertIn("544fb5454bdc2df8738b456a", pinned_direct_overlap)
        self.assertIn("5ac78a9b86f7741cca0bbd8d", pinned_direct_overlap)

        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        live_tpls = {x["_tpl"] for x in assort["items"]}
        rejected_tpls = {x["tpl"] for x in candidates if x["decision"].startswith("reject-")}
        self.assertTrue(rejected_tpls.isdisjoint(live_tpls))

    def test_finished_equipment_review_rejects_unlimited_vanilla_duplicates(self):
        review = self.load("relationship-finished-equipment-review.json")
        self.assertFalse(review["policy"]["materialize"])
        self.assertTrue(review["policy"]["unlimitedVanillaDirectOfferIsDisqualifying"])
        self.assertFalse(review["policy"]["priceDiscountAsRelationshipBenefitAllowed"])
        self.assertFalse(review["policy"]["questOrCombatCapabilityBypassAllowed"])

        resolved = {x["tpl"]: x for x in review["resolved"]}
        expected = {
            "590c60fc86f77412b13fddcf": ("Therapist", "686e34716c2a18ed6b0eb451"),
            "59fafd4b86f7745ca07e1232": ("Therapist", "686e34706c2a18ed6b0eb427"),
            "5d235bb686f77443f4331278": ("Jaeger", "686e34256c2a18ed6b0e94b1"),
        }
        self.assertEqual(set(resolved), set(expected))
        for tpl, (trader, offer_id) in expected.items():
            candidate = resolved[tpl]
            self.assertEqual(candidate["decision"], "reject-redundant-pinned-vanilla")
            self.assertEqual(candidate["evidence"]["trader"], trader)
            self.assertEqual(candidate["evidence"]["offerId"], offer_id)
            self.assertTrue(candidate["evidence"]["unlimitedCount"])
            self.assertGreater(candidate["evidence"]["stackObjectsCount"], 1_000_000)

        self.assertEqual(review["conclusion"]["approvedOfferCount"], 0)
        self.assertTrue(review["conclusion"]["relationshipMaterializationStillDisabled"])


if __name__ == "__main__":
    unittest.main()
