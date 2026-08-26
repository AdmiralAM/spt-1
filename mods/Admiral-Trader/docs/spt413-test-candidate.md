# Admiral Trader — SPT 4.1.3 Test Candidate Gate

This document is the physical-runtime acceptance contract for Issue #146. It is not a release checklist and does not approve merge by itself.

## Source state before handoff

The PR head must satisfy all Admiral Trader workflows. Source `runtime-manifest.json` must remain fail-closed (`registrationEnabled=false`, `publicationMode=test-candidate-source`). The exact-runtime builder is the only supported path that produces an enabled candidate.

Final trader portrait/character art is intentionally outside this gate. The candidate uses the explicit built-in placeholder route declared in `base.json` and the runtime manifest.

The physical candidate must be built from the exact tested PR head against SPT 4.1.3 runtime assemblies. Build provenance must record the source head and exact runtime binary hashes so runtime evidence cannot describe a different source/runtime pair than the one accepted by CI.

## Mandatory artifact boundary

A user runtime handoff is **not valid** from a source checkout, PR diff, branch name, or green source workflow alone.

Before asking for any physical SPT test, the exact PR head must have a downloadable GitHub Actions artifact (or deliberately promoted runtime package) that contains the installable Admiral Trader candidate produced from the exact SPT 4.1.3 runtime build path. The handoff must name:

- PR number and branch;
- exact 40-character source SHA;
- successful workflow/run ID;
- exact artifact name;
- expected install layout;
- candidate provenance file contained in the package.

If source CI is green but no installable artifact exists, the gate remains **repo-side incomplete** and no user test is requested.

## Exact-runtime build contract

The maintained builder is `tools/build_spt413_test_candidate.ps1`. When executed in the controlled packaging environment it must:

1. verify the checkout is a clean Git working tree;
2. verify the current commit matches the expected PR head SHA;
3. locate the real `SPTarkov.Server.Core.dll`;
4. reject any assembly whose version is not `4.1.3.x`;
5. compile `Admiral Trader Server.dll` against those exact runtime assemblies, not the 4.1.2 NuGet baseline;
6. create `build/admiral-trader-test-candidate/SPT_Runtime/user/mods/Admiral-Trader`;
7. enable registration only in the staged manifest;
8. write `candidate-provenance.json` with the full source SHA, Server.Core version/SHA-256 and built Admiral server DLL SHA-256;
9. reject temporary/debug artifacts in the staged package.

The published artifact must preserve that staged layout. `candidate-provenance.json` must travel with the runtime evidence and must not be committed back to the source branch.

## Server-only smoke gate

Start the SPT 4.1.3 server before launching the client. The server must reach normal ready state without an Admiral exception. Evidence must show:

- Admiral exact-runtime item gate verifies all referenced TPLs;
- Admiral trader registers once under ID `d5c27bb3169f8dfbc13f6b69`;
- 31 authored Admiral quests register;
- no missing item/weapon TPL is reported;
- no duplicate trader or quest ID is reported;
- no locale load exception occurs.

Any failure here blocks client testing and merge.

## New-profile client gate

Use a disposable/new profile for the first functional pass.

Verify:

- exactly one custom trader named `Admiral` / `Адмирал` is present;
- the test placeholder image resolves (visual quality is not under test);
- no legacy Andrudis six-trader zoo appears;
- no legacy QuestManiac quest mass is exposed;
- current available quests belong to the curated Access/Arsenal campaign only and respect their level/prerequisite gates;
- quest text renders as authored text, not raw `<quest id> name/description/...` locale keys;
- Admiral assort is not an unrestricted shop: current offers are quest-gated and finite.

## Quest / assort spot checks

A full playthrough is not required for the first gate. Profile state may be advanced only in a disposable test profile when needed to verify unlock mechanics.

Required mechanics:

1. `Access Protocol: Clearance` success exposes the finite Labs access-card offer.
2. Each of the six Arsenal `Munitions` quest successes exposes only its mapped ammunition offer.
3. Each ammunition offer retains its configured finite stock/buy restriction.
4. `Special Weapons - Munitions` awards exactly one green RSP-30 sample (`6217726288ed9f0845317459`) and creates no permanent assort offer.
5. Completing one family does not unlock another family's ammunition.

## Existing-profile migration smoke gate

Use a copied profile only; never risk the user's primary profile for the first migration check.

Confirm that installing Admiral Trader does not expose unstarted legacy QuestManiac content. If an existing legacy active quest is present, capture its current status before and after server start; no direct profile mutation is permitted by the current source implementation.

The completion-bridge behavior remains a separate migration acceptance item if a representative active legacy profile is available.

## Evidence required for Issue #146

Retain only the evidence needed to make a merge decision:

- `candidate-provenance.json` from the exact staged/installed package;
- SPT server log from the candidate startup/test session;
- screenshot or concise observation confirming one Admiral trader and readable quest UI;
- any exception stack trace in full if the server/client fails;
- for unlock checks, the quest ID and observed offer result.

The server log and provenance must describe the same test run and source head. If the candidate is rebuilt, discard evidence from the older candidate rather than mixing runs.

Do not commit runtime logs, profile copies, generated build folders, screenshots, provenance files or ZIP packages to the source branch.

## Merge rule

PR #151 must remain Draft/unmerged until an exact-head installable SPT 4.1.3 artifact exists and its physical build/start/UI evidence is accepted. Source CI alone proves schema/tooling/package structure, not the final server assembly version or live quest/trader behavior.
