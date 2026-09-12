# Branch hygiene

This repository keeps branches only when they have a clear current role. Task branches are working state, not long-term archives.

## Permanent branches

- `main` — authoritative integrated source.
- `stable` — deliberately promoted validated source snapshot.
- `runtime` — Admiral Tactical HUD install-only publication channel; historical Tactical HUD naming may remain inside older published artifacts until deliberate replacement.
- `runtime-item-intelligence` — Item Intelligence Admiral install-only publication channel; retained compatibility branch name.
- `runtime-pause` — Pause Admiral install-only publication channel.
- `runtime-belt-armband` — Belt/Armband Inventory install-only publication channel.
- `runtime-item-valuation` — Item Valuation MOD SPT install-only publication channel.
- `runtime-economy-admiral` — Economy Admiral install-only publication channel.
- `runtime-artem-revival` — Admiral Artyom Revival stable publication identity; retained compatibility branch name, with authored core data/Bundles external and reproducible from the module contract.

`archive/v1.13.0` is **not permanent product authority**. It is a temporary Tactical HUD recovery reserve retained only while the Admiral Tactical HUD 1.13.3 line is not yet final. The final 1.13.3 stable cleanup should remove it unless the user explicitly decides to preserve that archive.

Additional permanent/runtime branches require an explicit repository-level reason and documentation update.

## Task branches

Normal feature, fix, diagnostic, performance, compatibility, CI, research, and housekeeping work uses short-lived branches. Recommended namespaces include `feature/`, `fix/`, `diagnostic/`, `perf/`, `compat/`, `ci/`, `research/`, and `chore/`.

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

Repository cleanup should therefore focus on exceptions: old pre-policy branches, closed-unmerged experiments, abandoned diagnostics, temporary recovery archives, and branches whose PR lifecycle did not trigger automatic deletion.

## Archive policy

Do not keep ordinary task branches as historical souvenirs. Git and merged PR history already preserve normal development history.

If a branch must be retained as a deliberate historical/recovery point:

1. move/use the `archive/` namespace;
2. document why it exists;
3. keep the number of archive branches minimal;
4. define when it stops being necessary;
5. remove it when its recovery/historical purpose no longer justifies retention.

## Runtime/publication branches

`stable` and `runtime-*` are controlled publication channels, not development branches. Do not make normal feature commits on them, open development work from them, or treat their force-updated history as source history.

For Admiral Tactical HUD, `runtime` is the publication channel while the single live implementation PR/branch is temporary development state. A legacy integrated path, retired task branch, or temporary recovery archive is never a second active development authority.

`runtime-item-intelligence`, `runtime-item-valuation`, `runtime-pause`, `runtime-belt-armband`, and `runtime-economy-admiral` are install-only product channels. An active successor roadmap does not make the previous accepted runtime package a development authority; it remains the published baseline until deliberate replacement.

`runtime-artem-revival` is intentionally retained as the publication compatibility identifier for **Admiral Artyom Revival**. It pins the accepted server identity plus manifest/hashes for `r5-RU-compat`; authored upstream runtime data and approximately 1.5 GB Unity Bundles remain external source material. The branch is not a standalone full-install archive. It must remain publication-only and be updated only from a runtime-validated candidate.

## Safety rule

Never delete an active workstream branch or unique unmerged work merely for cosmetic cleanliness. When evidence is ambiguous, classify first; delete only after the branch's useful state is understood.

One-time historical deletion queues belong in GitHub Issues or an explicit manual cleanup report, not in this durable policy document.
