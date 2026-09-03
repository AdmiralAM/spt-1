import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]; MANIFESTS=ROOT/'manifests'
def load(name): return json.loads((MANIFESTS/name).read_text(encoding='utf-8'))

def test_post_010_campaign_progression_is_complete_reachable_and_acyclic():
    graph=load('post-010-campaign-progression.json'); rewards=load('post-010-operation-reward-envelope.json')
    assert graph['implementationAllowed'] is False and graph['runtimeMaterialize'] is False
    assert graph['frozenBoundary']=={'questCount':31,'rootOfferCount':11,'relationshipRuntimeOffers':0}
    phase_ops=[op for phase in graph['phases'] for op in phase['operations']]; expected=set(rewards['operationBands'])
    assert len(phase_ops)==15 and len(set(phase_ops))==15 and set(phase_ops)==expected
    assert set(graph['prerequisites'])==expected and 'armored-transit' not in expected
    for deferred in ('expedition-discipline','field-medicine-under-pressure','controlled-chemical-support','high-value-target-window','night-signal-disruption'): assert deferred not in expected
    assert graph['prerequisites']['heavy-assault-loadout']==['ballistic-head-test','protection-calibration']
    assert graph['prerequisites']['labs-security-disruption']==['precision-observation-window','ballistic-head-test']
    phase_index={op:i for i,phase in enumerate(graph['phases']) for op in phase['operations']}
    for operation, prerequisites in graph['prerequisites'].items():
        assert len(prerequisites)<=graph['designRules']['maximumDirectPrerequisitesPerOperation'] and operation not in prerequisites
        for prerequisite in prerequisites: assert prerequisite in expected and phase_index[prerequisite]<=phase_index[operation]
    roots={op for op,p in graph['prerequisites'].items() if not p}; assert roots
    visiting=set(); visited=set()
    def visit(op):
        if op in visiting: raise AssertionError(f'cycle detected at {op}')
        if op in visited: return
        visiting.add(op)
        for p in graph['prerequisites'][op]: visit(p)
        visiting.remove(op); visited.add(op)
    for op in expected: visit(op)
    assert visited==expected
    assert set(graph['phases'][-1]['operations'])=={'labs-security-disruption'}
    assert len(graph['prerequisites']['labs-security-disruption'])==2

def test_post_010_campaign_has_concrete_non_regressing_player_level_placement():
    graph=load('post-010-campaign-progression.json'); rewards=load('post-010-operation-reward-envelope.json'); trajectory=load('relationship-standing-trajectory.json')
    levels=graph['operationLevelPlacement']; expected=set(rewards['operationBands']); assert set(levels)==expected and len(levels)==15
    mins={p['key']:p['minimumPlayerLevel'] for p in graph['phases']}; assert mins=={'field-readiness':15,'operational-integration':20,'specialist-operations':28,'command-grade-operations':35}
    for phase in graph['phases']: assert all(levels[op]>=phase['minimumPlayerLevel'] for op in phase['operations'])
    for op,ps in graph['prerequisites'].items():
        for p in ps: assert levels[p]<=levels[op]
    loyalty={t['loyaltyLevel']:t['minimumPlayerLevel'] for t in trajectory['tiers']}; assert loyalty[2]==15 and loyalty[3]==25 and loyalty[4]==35
    assert min(levels.values())==15 and levels['labs-security-disruption']>35
    assert graph['progressionContracts']['operationCount']==15

def test_post_010_progression_does_not_turn_loyalty_or_sales_into_quest_gates():
    graph=load('post-010-campaign-progression.json'); rules=graph['designRules']; gate=graph['materializationGate']
    assert rules['salesVolumeGateAllowed'] is False and rules['repeatableGateAllowed'] is False and rules['legacyQuestCountGateAllowed'] is False
    assert rules['mandatoryAllBranchesBeforeNextPhaseAllowed'] is False and rules['storefrontUnlockRequiredForProgression'] is False
    assert gate['requiresExactSpt413ConditionProofPerOperation'] is True and gate['requiresOverlapAuditPerOperation'] is True and gate['requiresEconomyAdmiralRewardReview'] is True
