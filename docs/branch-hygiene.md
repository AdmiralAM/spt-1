# Branch hygiene

This repository keeps branches only when they have a clear current role. Task branches are working state, not long-term archives.

## Permanent branches

- `main` — authoritative integrated source.
- `stable` — deliberately promoted validated source snapshot.
- `runtime` — Tactical HUD install-only publication channel.
- `runtime-item-intelligence` — Item Intelligence install-only publication channel.
- `runtime-pause` — Pause install-only publication channel.
- `runtime-belt-armband` — Belt/Armband install-only publication channel.
- `runtime-artem-revival` — Artem Revival install-only core-overlay publication channel; external Bundles are documented by the module.
- `archive/v1.13.0` — intentional frozen Tactical HUD historical reserve.

Additional permanent/runtime branches require an explicit repository-level reason and documentation update.

## Task branches

Normal feature, fix, diagnostic, performance, compatibility, CI, and housekeeping work uses short-lived branches. Recommended namespaces include `feature/`, `fix/`, `diagnostic/`, `perf/`, `compat/`, `ci/`, and `chore/`.

A task branch exists only while its work is active, under review, awaiting required runtime validation, or still contains unique useful work that has not been deliberately preserved elsewhere.

## Deletion rule

Delete a task branch when one of the following is proven:

- its useful result has been merged into `main`;
- its temporary build/diagnostic purpose has expired;
- it has been superseded by a later accepted implementation;
- it is redundant with an intentional retained archive/publication channel;
- its PR is closed and the branch carries no unique work that still needs preservation.

Do not require `ahead_by=0` as the only proof. Squash merges, rebases, superseded experiments, and diagnostic branches can legitimately retain unique commit SHAs after their useful content has already been preserved or intentionally discarded. Review content/PR history before deleting when ancestry alone is inconclusive.

## Automatic cleanup

Repository setting **Automatically delete head branches** is enabled. After a normal merged PR, GitHub should remove its head branch automatically. This is the default cleanup mechanism for future task branches.

Repository cleanup should therefore focus on exceptions: old pre-policy branches, closed-unmerged experiments, abandoned diagnostics, intentional archives, and branches whose PR lifecycle did not trigger automatic deletion.

## Archive policy

Do not keep ordinary task branches as historical souvenirs. Git and merged PR history already preserve normal development history.

If a branch must be retained as a deliberate historical/recovery point:

1. move/use the `archive/` namespace;
2. document why it exists;
3. keep the number of archive branches minimal;
4. remove it when its recovery/historical purpose no longer justifies permanent retention.

## Runtime/publication branches

`stable` and `runtime-*` are controlled publication channels, not development branches. Do not make normal feature commits on them, open development work from them, or treat their force-updated history as source history.

`runtime-artem-revival` is intentionally retained because the validated Artem core overlay is small enough for an install channel while its approximately 1.5 GB authored Unity Bundles remain external. The branch must remain install-only and be updated only from a runtime-validated candidate.

## Safety rule

Never delete an active workstream branch or unique unmerged work merely for cosmetic cleanliness. When evidence is ambiguous, classify first; delete only after the branch's useful state is understood.

One-time historical deletion queues belong in GitHub Issues, not in this durable policy document. See the active repository-cleanup Issue when a manual branch retirement pass is in progress.
