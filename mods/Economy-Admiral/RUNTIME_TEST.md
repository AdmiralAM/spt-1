# Economy Admiral runtime gate

This document is for the first physical SPT 4.1.3 runtime acceptance of the read-only Economy Admiral MVP.

## Candidate contract

Use the exact `economy-admiral-candidate` GitHub Actions artifact produced from the current PR head.

Create this directory if it does not exist:

`SPT_Runtime/user/mods/Economy Admiral/`

Extract the **contents of the candidate ZIP into that directory**. Do not extract the files directly into the SPT root.

The installed module directory must contain at least:

- `Economy-Admiral.dll`;
- `BUILD_INFO.json`;
- `config/config.json`;
- `README.md`;
- `RUNTIME_TEST.md`;
- `Validate-Runtime.ps1`.

`BUILD_INFO.json` binds the package to the exact PR head SHA and workflow run. The runtime validator rejects a manifest without this packaged build identity.

The first runtime test must use the packaged `config/config.json` unchanged unless a test case below explicitly says otherwise.

## Test A — Audit mode

1. Confirm `mode` is `Audit` and `preset` is `Normal`.
2. Start the SPT 4.1.3 server normally with the target mod stack enabled.
3. Allow server startup/PostLoad to finish without closing the process early.
4. Confirm there is no Economy Admiral startup exception or fatal load error.
5. Run from the SPT parent directory in PowerShell:

```powershell
& '.\SPT_Runtime\user\mods\Economy Admiral\Validate-Runtime.ps1'
```

Expected result: exit code `0` and a green Economy Admiral PASS line.

The validator requires:

- runtime evidence schema v2;
- exact packaged `BuildIdentity` for Economy Admiral / SPT 4.1.3;
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

Run this only after Test A succeeds.

1. Stop the SPT server.
2. Set `mode` to `Off` in `config/config.json`.
3. Delete or move the existing `reports` directory so stale files cannot satisfy the test.
4. Start the SPT server again and allow startup to finish.
5. Confirm Economy Admiral exits before analysis and does not recreate its report set.

`Off` is accepted only if no Economy Admiral analysis/planning/runtime-evidence reports are newly generated during that startup.

## Evidence to return for analysis

For Audit-mode acceptance, preserve and return:

- the full `reports` directory (9 JSON files total);
- the SPT server log from the same startup;
- `BUILD_INFO.json` from the installed module directory.

For Off-mode acceptance, preserve the SPT server log from that second startup and confirm that the reports directory was not recreated by Economy Admiral.

These artifacts are the gate for choosing/rejecting composite policy candidates and designing the first real enforcement transaction. No mutation path should be enabled before this evidence is reviewed.
