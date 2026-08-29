# SPT automated work charter

This is the single repository-wide execution policy for automated workers.

## Canonical authority

Before every work session, fetch `origin/main` and read these exact files from that ref:

1. `origin/main:AGENTS.md` — immutable worker policy;
2. `origin/main:.github/workstreams.json` — current workstream state;
3. the technical Issues recorded in its phase plan and the module's live GitHub PR/evidence;
4. the affected module README and relevant technical docs.

Policy copied into a feature branch, old PR body, chat memory, artifact, or historical Issue is not authority. Do not merge `main` merely to read control state.

## Authority and roles

The user is the sole product authority. The complete recorded `phasePlan` is the user's standing authorization to execute every listed phase through its recorded acceptance, including a direct runtime handoff and the recorded stable/publication transition. No worker grants permission to another worker.

`GitHub Work SPT` is a coordination and audit worker, not a controller or approval gate. It may reconcile repository facts, maintain shared mechanics when the user asks, and report cross-module status. Module workers do not wait for it, ask it to activate a phase, or require its acknowledgement.

Module workers (Belt, Trader, Economy, HUD, and future module chats) inspect, implement, test, repair CI, package, integrate, and attach evidence. They must not:

- edit this charter or `.github/workstreams.json` without an explicit user instruction;
- rewrite their recorded phase plan or stable acceptance without an explicit user instruction;
- create a new product scope or declare an unfinished product cancelled/parked without an explicit user instruction;
- change frozen names, versions, persistent IDs, routes, or cross-module ownership;
- treat a branch, commit, PR, CI run, document, validator, or artifact as the product finish line;
- ask for `next step` when the registry already records the continuation;
- modify another module or invent governance changes as a substitute for product work.

Any worker may faithfully encode an explicit user governance instruction through a `governance/*` PR. It must not wait for `GitHub Work SPT` to do so. Without an explicit user instruction, control files remain unchanged. CODEOWNERS and the control guard keep those changes visible to the user.

## Worker execution loop

The complete ordered `phasePlan` is pre-authorized. At the start of every run, inspect `main`, the recorded Issues, and the module's live PR evidence; resume at the first phase whose acceptance is not already proven. Continue within every available run:

`ACTIVE -> IMPLEMENT -> VALIDATE -> FIX UNTIL GREEN -> CONTINUE NEXT RECORDED PACKAGE -> RELEASE CANDIDATE -> ONE BATCHED RUNTIME TEST -> FAIL: REMEDIATE / PASS: STABLE RELEASE`

- Complete each phase's technical work without requiring approval between internal steps.
- Fix scoped CI failures and continue; do not end a run merely because CI started or passed.
- When a phase completes, record evidence in its technical Issue/PR and immediately continue to the next `phasePlan` entry.
- Ordinary recorded phase transitions never require a registry edit, another worker's acknowledgement, or a new user message.
- Create at most one implementation PR for the module, and only when coherent implementation exists.
- Discover the module's single live implementation PR from GitHub; PR numbers and temporary branches are deliberately not stored as control pointers.
- New branch/PR mechanics for an already recorded phase are not a new product decision.
- Do not expand beyond the registry and linked Issue/PR.

## Registry update boundaries

Do not update the registry merely because a phase, commit, CI run, PR, merge, artifact, or recorded successor completed. The evidence itself determines the resume point.

Only an explicit user instruction may add, remove, or reorder product scope; change a phase contract or frozen identity; cancel/park work; or change an undefined publication decision. The worker receiving that instruction may encode it directly. Runtime readiness, technical blockers, phase completion, and recorded stable/publication transitions live in Issue/PR evidence and never require a registry update.

## Valid stop conditions

A worker may stop only at the exact boundary of:

1. a coherent physical SPT/EFT runtime gate that cannot be resolved from source, references, logs, artifacts, or automated validation;
2. missing permission/access or a proven external dependency after all unblocked work is exhausted;
3. an explicit product decision absent from the registered roadmap, asked directly of the user;
4. completed stable/release acceptance.

PR creation, branch synchronization, commits, documentation, CI, packaging, and an internal artifact are never stop conditions.

## Runtime-test budget and handoff

- A module worker enters its recorded `requiresUserRuntime` phase automatically after all prior acceptance is proven. No controller activation, registry edit, or inter-chat coordination is required.
- Ask the user directly only at that coherent physical boundary. The user alone decides when to run the candidate and may choose the order if several modules become ready.
- Batch related checks into one release-candidate session per module; do not use the user as a per-patch debugger.
- Ask only after all feasible source inspection, automated tests, builds, packaging, integration, and CI repair are complete.
- Provide the exact GitHub Actions/release URL, PR, branch, commit SHA, artifact name/ID, digest, install layout, and a short numbered table of action / PASS / FAIL / minimal evidence.
- Chat attachments, local files, source ZIPs, CI success without an artifact, and vague `test everything` requests are invalid handoffs.
- On FAIL, consume the evidence and resume remediation automatically. On PASS, follow the next recorded phase or release transition under the standing authorization; do not ask another worker for permission.

### Mandatory user-facing test request

A runtime-test request is valid only when the same message gives the user one complete, immediately actionable handoff. It must begin with this compact block (translated to the conversation language when needed):

```text
Скачать: <one clickable GitHub download URL>
Скачивать именно: <exact artifact/asset/ZIP name>
Кандидат: <module name, version, target SPT, PR and exact commit SHA>
Установка: <exact replacement/copy steps and destination paths>
Проверить:
1. <user action> — PASS: <observable result>; FAIL: <observable result>
2. ...
Вернуть: <numbered PASS/FAIL plus the smallest requested screenshot or log excerpt>
Общий PASS: <exact rule>
```

Hard rules:

- The first URL is the **primary candidate download**, not merely a repository home page, source tree, PR, commit, workflow list, or CI-status page. Provenance links may follow it.
- State exactly which named file/artifact the user downloads. Never make the user search an Actions run, choose among builds, infer a filename, build source, or guess which ZIP is installable.
- Verify that the linked GitHub artifact, Release asset, or install-ready `runtime-*` package actually exists and matches the named exact commit before asking.
- Give exact install/replace/remove instructions for that candidate, including both client and server paths when applicable.
- Test points are short, numbered, behavior-specific, and include observable PASS and FAIL results. `Test everything`, `try it`, free exploration, and an unbounded full-log request are invalid.
- Ask for only the minimum evidence needed to decide the numbered gate.
- **No working GitHub download link + no exact filename + no numbered checklist = no user test request.** Continue packaging/publication work or report the concrete blocker without assigning the user a test.

Detailed handoff mechanics live in `docs/runtime-artifact-gate.md`; that document cannot override this charter or the registry.

## Safety and isolation

- Work in the registered module branch/PR and change only that module plus narrowly required shared infrastructure.
- `main` is integration-only; `runtime-*` and `stable` are deliberate publication channels, not development workspaces.
- Never weaken tests, force-update another workstream, share unrelated concurrency groups, or invoke suite publication for ordinary validation.
- Persistent profile identities require an immutable manifest covering current and retired distributed IDs, backup-first ownership-scoped recovery, and deterministic regression coverage.
- Never rename, reuse, or silently drop a distributed persistent ID.
- A profile-load/save incident freezes feature expansion for that module until recovery and prevention are proven.
- Performance-sensitive code must avoid permanent polling, scene-wide scans, hot-path reflection/allocations, and global UI mutation unless explicitly proven necessary and bounded.

## Communication

Use only:

1. one short start acknowledgement;
2. a material root cause or plan-changing CI failure;
3. a genuine blocker/runtime gate;
4. one coherent package/RC/stable result.

Do not narrate file edits, branch creation, commits, CI polling, documentation, or every internal gate. Intermediate updates are non-terminal and require no user response.

## Repository lifecycle

`Issue -> short-lived branch -> implementation -> module CI -> PR -> runtime gate when required -> merge -> verify main -> close/update Issue -> delete temporary branch`

Detailed repository mechanics live in `CONTRIBUTING.md`, `docs/development-workflow.md`, `docs/github-stable-runtime.md`, and `docs/branch-hygiene.md`. If any text conflicts, this charter and `.github/workstreams.json` win.
