# Admiral Trader — SPT 4.1.3 release-candidate gate

This document is the physical-runtime handoff contract for Issue #192. `origin/main:AGENTS.md` and `origin/main:.github/workstreams.json` are the authority if any text here becomes stale.

## Authority and gate state

The user is the sole product authority. The recorded Trader phase plan authorizes the module worker to continue automatically through Gameplay Alpha, Beta stabilization and release hardening, then enter the single batched `combined-release-candidate` runtime phase directly once all feasible automated work is exhausted. No controller activation, registry edit or another worker acknowledgement is required.

Source `runtime-manifest.json` remains fail-closed (`registrationEnabled=false`, `publicationMode=test-candidate-source`). An enabled runtime tree is produced only by the maintained staging/package path.

## Frozen candidate contract

The candidate must preserve all current frozen product identity and counts:

- product: **Admiral Trader**, version `0.1.0`;
- trader ID: `d5c27bb3169f8dfbc13f6b69`;
- portrait route: `/files/trader/avatar/d5c27bb3169f8dfbc13f6b69.jpg`;
- exact final portrait identity from `manifests/identity-assets.json`;
- exactly 31 canonical quests;
- exactly 4 Baseline plus 7 Milestone offers;
- initial Admiral standing `0`;
- unaccepted quests remain unaccepted;
- persistent IDs remain governed by `manifests/persistent-identities.json`.

The current stock model is mixed by design: four finite Baseline offers are available without quest gates, while seven finite Milestone offers are unlocked by their authored quest mappings. The candidate must never reinterpret all eleven offers as quest-gated.

## Mandatory automated boundary

Before any physical SPT test is requested, all feasible module validation must be green for the exact source head and the exact-head package must be available as a GitHub artifact or deliberately promoted package. The handoff must identify:

- PR and branch;
- full 40-character source SHA;
- successful workflow/run URL;
- artifact name, artifact ID and direct GitHub-hosted artifact URL;
- artifact digest/checksum;
- install layout `SPT_Runtime/user/mods/Admiral-Trader`;
- `candidate-provenance.json` from that package.

A source commit, PR, CI result, local ZIP or published-API preflight artifact is not by itself a valid physical-runtime handoff.

## Exact-runtime build and package contract

`tools/build_spt413_test_candidate.ps1` builds the exact clean source head against the real SPT 4.1.3 runtime assemblies supplied through the SPT installation root and records their hashes. `tools/package_spt413_exact_candidate.ps1` then validates and packages that staged tree.

The exact package must contain at least:

- `Admiral Trader Server.dll`;
- `candidate-provenance.json`;
- `db/base.json`, `db/assort.json`, `db/questassort.json` and all 31 quest templates;
- all required EN/RU locale layers;
- `manifests/runtime-manifest.json`, `identity-assets.json` and `persistent-identities.json`;
- the exact approved portrait asset;
- `tools/Reset-AdmiralTraderProfile.ps1`.

Packaging must reject build/debug junk, wrong source HEAD, wrong runtime identity, wrong portrait identity, wrong questassort casing/counts, or an incomplete recovery tree. Optional installation uses prepare-then-swap semantics with rollback on activation failure.

The published `SPTushonka.* 4.1.3` API preflight is useful compile/API evidence only. It does not substitute for the exact-runtime provenance above and its artifact is not physical-runtime evidence eligible.

## Profile safety and recovery

New profiles must obtain Admiral standing `0` from the profile template. The module must not pre-accept or pre-complete its quests.

For existing-profile reset/update/disable/uninstall testing, use the packaged recovery tool rather than manual JSON editing:

```powershell
.\tools\Reset-AdmiralTraderProfile.ps1 -ProfilePath <profile.json>
.\tools\Reset-AdmiralTraderProfile.ps1 -ProfilePath <profile.json> -Apply
```

The first command is preview-only. `-Apply` creates and SHA-256 verifies a timestamped backup **before** mutation, removes only state owned by current or retired Admiral identities from `persistent-identities.json`, validates the rewritten JSON, and restores the backup on write failure. The retained backup is not deleted automatically.

## One batched runtime gate

When all earlier phase acceptance is proven and the exact-runtime GitHub artifact exists, use one copied/disposable profile session to cover the complete candidate rather than per-patch testing.

The minimum PASS set is:

1. Server reaches normal ready state with one Admiral trader, no duplicate IDs, missing TPLs, locale exceptions, or load/save errors.
2. Trader portrait resolves from the frozen route and renders with the approved proportions.
3. A fresh profile shows Admiral standing exactly `0`; no Admiral quest is already accepted or completed.
4. Exactly 31 Admiral quests are published with readable EN/RU text and correct level/prerequisite lifecycle.
5. Quest completion delivers XP, Admiral standing, RUB/item rewards and any authored sample reward through the normal SPT reward/mail path.
6. Exactly four finite Baseline offers are present without quest gates; exactly seven finite Milestone offers follow `questassort.success` and preserve stock/buy limits.
7. `Access Protocol: Clearance` unlocks only the Labs access offer; each of the six normal Arsenal Munitions milestones unlocks only its mapped ammunition offer; Special Weapons remains sample-only with no permanent offer.
8. A copied existing profile survives preview/apply recovery, retains unrelated profile state, and recreates current Admiral TraderInfo at standing `0` on subsequent trader access.
9. Update/disable/uninstall recovery does not strand current or retired Admiral-owned quest/trader state or corrupt profile load/save.

On any FAIL, preserve the minimal exact evidence (source SHA/provenance, relevant log/stack trace, quest or offer ID, and concise observed result) and return to remediation automatically. Do not mix evidence from different source heads.

## Stable promotion boundary

Only an exact candidate that passes the batched runtime gate can proceed to the recorded stable-release phase. Source validation, packaging, a preflight artifact, or an untested exact-runtime archive is not stable acceptance.
