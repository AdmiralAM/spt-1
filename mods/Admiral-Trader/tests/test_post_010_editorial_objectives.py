import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / 'manifests'


def load(name):
    return json.loads((MANIFESTS / name).read_text(encoding='utf-8'))


def test_post_010_editorial_objectives_match_active_campaign_exactly():
    editorial = load('post-010-editorial-objectives.json')
    campaign = load('post-010-campaign-progression.json')

    campaign_ops = [op for phase in campaign['phases'] for op in phase['operations']]
    editorial_ops = [entry['operationKey'] for entry in editorial['objectives']]

    assert editorial['runtimeMaterialize'] is False
    assert editorial['campaignAuthority'] == 'post-010-campaign-progression.json'
    assert editorial['acceptance']['operationCount'] == 15
    assert len(editorial_ops) == len(set(editorial_ops)) == 15
    assert set(editorial_ops) == set(campaign_ops)


def test_post_010_active_editorial_excludes_deferred_and_rejected_concepts():
    editorial = load('post-010-editorial-objectives.json')
    editorial_ops = {entry['operationKey'] for entry in editorial['objectives']}
    excluded = editorial['excludedConcepts']

    expected_deferred = {
        'expedition-discipline',
        'field-medicine-under-pressure',
        'high-value-target-window',
        'night-signal-disruption',
    }
    expected_rejected = {'armored-transit', 'controlled-chemical-support'}

    assert set(excluded['deferred']) == expected_deferred
    assert set(excluded['rejected']) == expected_rejected
    assert editorial_ops.isdisjoint(expected_deferred | expected_rejected)
    assert editorial['editorialRules']['deferredOrRejectedConceptCopyMustNotAppearInActiveWave'] is True


def test_post_010_editorial_copy_is_bilingual_and_nonempty():
    editorial = load('post-010-editorial-objectives.json')

    for entry in editorial['objectives']:
        assert set(entry['title']) == {'en', 'ru'}
        assert set(entry['steps']) == {'en', 'ru'}
        assert entry['title']['en'].strip() and entry['title']['ru'].strip()
        assert entry['steps']['en'] and entry['steps']['ru']
        assert all(step.strip() for step in entry['steps']['en'])
        assert all(step.strip() for step in entry['steps']['ru'])
        assert entry['editorialConstraint'].strip()
