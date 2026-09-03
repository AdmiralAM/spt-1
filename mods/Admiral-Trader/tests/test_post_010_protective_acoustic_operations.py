import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]

class Post010ProtectiveAcousticOperationsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest=json.loads((ROOT/'manifests'/'post-010-protective-acoustic-operations.json').read_text(encoding='utf-8'))
        cls.equipment_proof=json.loads((ROOT/'manifests'/'post-010-player-equipment-proof.json').read_text(encoding='utf-8'))
        cls.allowlist_proof=json.loads((ROOT/'manifests'/'post-010-protective-acoustic-equipment-allowlist-proof.json').read_text(encoding='utf-8'))
        cls.by_key={op['key']:op for op in cls.manifest['operations']}

    def test_specs_are_non_materialized_and_armored_transit_is_rejected(self):
        self.assertEqual(self.manifest['schemaVersion'],8)
        self.assertFalse(self.manifest['implementationAllowed'])
        self.assertEqual(set(self.by_key),{'acoustic-contact'})
        rejected=self.manifest['rejectedOperations']['armored-transit']
        self.assertEqual(rejected['decision'],'reject-from-active-authored-wave')
        self.assertFalse(rejected['replacementQuestRequired']); self.assertFalse(rejected['rewardRedistributionAllowed'])
        self.assertTrue(all(op['runtimeMaterialize'] is False for op in self.manifest['operations']))

    def test_surviving_acoustic_copy_and_shape_are_bounded(self):
        op=self.by_key['acoustic-contact']
        for locale in ('en','ru'):
            for field in ('description','started','success'): self.assertGreater(len(op['playerText'][locale][field].strip()),30)
        bounded=op['boundedOperation']
        self.assertEqual(bounded['location'],'woods'); self.assertEqual(bounded['contact'],{'target':'Savage','count':2})
        self.assertEqual(bounded['extraction'],{'location':'woods','exitStatus':'Survived'})
        self.assertEqual(bounded['selectionStatus'],'admitted-pending-same-raid-proof')

    def test_explicit_headset_allowlist_remains_exact_and_fail_closed(self):
        authority=self.manifest['equipmentConditionAuthority']; op=self.by_key['acoustic-contact']; plan=op['equipmentPlan']; proof=self.allowlist_proof['operations']['acoustic-contact']
        self.assertEqual(authority['conditionType'],'Equipment'); self.assertFalse(authority['includeNotEquippedItems'])
        self.assertEqual(plan['equipmentInclusive'],proof['equipmentInclusive']); self.assertEqual(plan['explicitTplCount'],15)
        self.assertFalse(plan['materializationReady']); self.assertNotIn('tpl allowlist',plan['remainingBlocker'].lower())
        self.assertIn('economy admiral numeric reward review is complete',' '.join(op['proofGates']).lower())

    def test_vanilla_overlap_is_closed_as_bounded_differentiation(self):
        op=self.by_key['acoustic-contact']; disposition=op['vanillaOverlapDisposition']
        self.assertEqual(disposition['decision'],'admit-bounded-differentiation')
        self.assertEqual(disposition['reviewStatus'],'closed-static-semantic-review')
        self.assertEqual(disposition['definingTuple'],['active qualifying headset','woods','exactly 2 Savage','Survived extraction'])
        self.assertTrue(disposition['noCosmeticDifferentiation'])
        self.assertGreaterEqual(len(disposition['knownComponentOverlaps']),4)
        self.assertGreaterEqual(len(disposition['reopenConditions']),2)
        self.assertEqual(op['conditionReadiness']['vanillaSemanticOverlap'],'admitted-bounded-differentiation')

    def test_same_raid_coupling_is_the_only_materialization_blocker(self):
        op=self.by_key['acoustic-contact']; blockers=op['materializationBlockedBy']
        self.assertEqual(len(blockers),1)
        self.assertIn('same-raid',blockers[0].lower())
        self.assertNotIn('vanilla overlap',blockers[0].lower()); self.assertNotIn('economy',blockers[0].lower())
        readiness=op['conditionReadiness']; self.assertEqual(readiness['equipmentToExtractionSameRaidCoupling'],'unproven-required-before-runtime-materialization')
        self.assertFalse(self.manifest['survivedExtractionAuthority']['sameRaidEquipmentCouplingProven'])
        self.assertIn('same-raid',op['equipmentPlan']['remainingBlocker'].lower())
        self.assertIn('vanilla semantic-overlap disposition',op['equipmentPlan']['remainingBlocker'].lower())

    def test_acoustic_contact_rejects_storefront_and_legend_grid(self):
        anti=self.by_key['acoustic-contact']['antiGrind']
        self.assertEqual(anti['maximumRequiredSuccessfulRaids'],1); self.assertEqual(anti['maximumTargetCount'],2)
        self.assertTrue(anti['noHeadsetByHeadsetLadder']); self.assertTrue(anti['noLegendGearKillExpansion']); self.assertTrue(anti['noSequentialStorefrontUnlocks']); self.assertFalse(anti['repeatable'])

    def test_frozen_runtime_counts_are_unchanged(self):
        quests=sorted((ROOT/'db'/'quests').glob('*.json')); assort=json.loads((ROOT/'db'/'assort.json').read_text(encoding='utf-8')); roots=[i for i in assort['items'] if i.get('parentId')=='hideout']
        self.assertEqual(len(quests),31); self.assertEqual(len(roots),11)

if __name__=='__main__': unittest.main()
