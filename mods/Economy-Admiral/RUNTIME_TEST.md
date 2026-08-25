# Economy Admiral runtime gate

This document is for the first physical SPT 4.1.x runtime acceptance of the read-only Economy Admiral MVP.

## Candidate contract

Use the exact GitHub Actions artifact produced from the current PR head. The package is intended to be extracted at the SPT root so the module lands under:

`SPT_Runtime/user/mods/Economy Admiral/`

The runtime test must use the packaged `config/config.json` unless a test case explicitly says otherwise.

## Test A — Audit mode

1. Confirm `mode` is `Audit` and `preset` is `Normal`.
2. Start the SPT server normally with the target mod stack enabled.
3. Allow server startup/PostLoad to finish without closing the process early.
4. Confirm there is no Economy Admiral startup exception or fatal load error.
5. Run from PowerShell:

```powershell
& '.\SPT_Runtime\user\mods\Economy Admiral\Validate-Runtime.ps1'
```

Expected result: exit code `0` and a green Economy Admiral PASS line.

The validator requires:

- `economy-admiral-runtime-evidence.json`;
- all 8 analysis/planning reports;
- valid JSON in every report;
- identical before/after DB SHA-256 fingerprints;
- `DatabaseUnchangedAcrossPipeline = true`;
- `RuntimeGatePassed = true`;
- `ApplyMutations = false`;
- zero declared mutations;
- no selected composite candidate;
- no automatic target/enforcement mutation candidate.

## Test B — Off mode

After Test A succeeds:

1. Stop the SPT server.
2. Set `mode` to `Off`.
3. Delete or move the existing `reports` directory so stale files cannot satisfy the test.
4. Start the SPT server again and allow startup to finish.
5. Confirm Economy Admiral exits before analysis and does not recreate its report set.

`Off` is accepted only if no Economy Admiral analysis/planning/runtime-evidence reports are newly generated during that startup.

## Evidence to return for analysis

For Audit-mode acceptance, preserve:

- the full `reports` directory (9 JSON files total);
- the SPT server log from the same startup;
- the exact candidate artifact/head SHA used.

Those files are the gate for choosing/rejecting composite policy candidates and designing the first real enforcement transaction. No mutation path should be enabled before this evidence is reviewed.
