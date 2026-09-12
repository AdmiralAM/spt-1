# SPT automated work charter

This is the single repository-wide execution policy for automated workers.

## Canonical authority

Before every work session, fetch `origin/main` and read these exact files from that ref:

1. `origin/main:AGENTS.md` — immutable worker policy;
2. `origin/main:.github/workstreams.json` — durable workstream roadmap and contracts;
3. the technical Issues recorded in its phase plan and the module's live GitHub PR/evidence;
4. the affected module README and relevant technical docs.

Policy copied into a feature branch, old PR body, chat memory, artifact, or historical Issue is not authority. Do not merge `main` merely to read control state.

The registry deliberately does **not** store temporary implementation PR numbers, branch names, exact live heads, mutable current-phase pointers, or runtime-gate state. Discover those from GitHub evidence at the start of each run.

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
- When a phase completes, ensure its evidence is available through the commit, Checks/Actions, artifact metadata, or the PR's current summary, then immediately continue to the next `phasePlan` entry. Do not append a routine phase-completion comment merely to restate machine-recorded evidence.
- Ordinary recorded phase transitions never require a registry edit, another worker's acknowledgement, or a new user message.
- Create at most one implementation PR for the module, and only when coherent implementation exists.
- Discover the module's single live implementation PR from GitHub; PR numbers, temporary branches, exact live heads, mutable phase state, and runtime-gate state are deliberately not stored as registry control pointers.
- If exactly one live implementation PR exists, its current head branch and exact head SHA are the implementation authority for that run.
- If no live implementation PR exists, create a short-lived branch from current `main` only when coherent implementation for the first incomplete recorded phase exists.
- If multiple live implementation PRs exist for one module, reconcile them to one authority from current Issue/PR evidence before implementation; never choose using stale registry data, chat memory, an old artifact, or a retired branch.
- New branch/PR mechanics for an already recorded phase are not a new product decision.
- Do not expand beyond the registry and linked Issue/PR.

## Registry update boundaries

The registry stores durable product scope, ordered phase authorization, frozen contracts, stable acceptance, and durable historical evidence only. It must not be used as a mutable execution dashboard.

Do not update the registry merely because a phase, commit, CI run, PR, merge, artifact, branch, exact head, runtime gate, or recorded successor changed. The live GitHub evidence itself determines the resume point.

Only an explicit user instruction may add, remove, or reorder product scope; change a phase contract or frozen identity; cancel/park work; or change an undefined publication decision. The worker receiving that instruction may encode it directly. Runtime readiness, technical blockers, phase completion, exact heads, active PR/branch identity, and recorded stable/publication transitions live in Issue/PR evidence and never require a registry update.

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
- Ask only after all feasible source inspection, automated tests, builds, integration, and CI repair are complete. Packaging is required only for a release/distribution boundary or when direct deployment is unavailable.
- When the user has granted a module worker direct access to the local SPT installation, prefer verified direct deployment for development/runtime iterations. Provide the PR, branch, exact source commit, deployed paths and hashes, validation result, and a short numbered table of action / PASS / FAIL / minimal evidence.
- When direct deployment is unavailable or the candidate is a release/distribution boundary, provide the exact GitHub Actions/release URL, artifact name/ID, digest, install layout, PR, branch, exact commit, and the same bounded checklist.
- Chat attachments, source-only ZIPs, unverified local builds, and vague `test everything` requests are invalid handoffs.
- On FAIL, consume the evidence and resume remediation automatically. On PASS, follow the next recorded phase or release transition under the standing authorization; do not ask another worker for permission.

### User-authorized direct SPT deployment

- Direct deployment is a runtime delivery path, not permission to modify another module, third-party mod, profile, save, or unrelated SPT file. Each worker may replace only its own owned runtime paths and directly required integration files.
- Before replacement, verify the target SPT installation and exact owned destination paths, preserve user configuration and profile data, and make a recoverable backup outside active mod/plugin load paths when rollback would otherwise be difficult.
- Build and validate from the live PR exact head, deploy atomically where practical, verify the hashes of the files actually installed, and run the feasible local server/client smoke before asking the user to test behavior.
- Do not build an intermediate ZIP merely to copy the same files into the authorized local installation. Produce an install-ready ZIP only for stable release, external distribution, deliberate rollback evidence, or an explicit user request.
- A direct-deployment report must state what was replaced, where it was installed, which source head produced it, deployed file hashes, smoke outcome, and rollback location. Never claim deployment from build-output alone.

### Mandatory user-facing test request

A runtime-test request is valid only when the same message gives the user one complete, immediately actionable handoff. In artifact mode it must begin with this compact block (translated to the conversation language when needed):

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

- In artifact mode, the first URL is the **primary candidate download**, not merely a repository home page, source tree, PR, commit, workflow list, or CI-status page. Provenance links may follow it.
- State exactly which named file/artifact the user downloads. Never make the user search an Actions run, choose among builds, infer a filename, build source, or guess which ZIP is installable.
- Verify that the linked GitHub artifact, Release asset, or install-ready `runtime-*` package actually exists and matches the named exact commit before asking.
- Give exact install/replace/remove instructions for that candidate, including both client and server paths when applicable.
- Test points are short, numbered, behavior-specific, and include observable PASS and FAIL results. `Test everything`, `try it`, free exploration, and an unbounded full-log request are invalid.
- Ask for only the minimum evidence needed to decide the numbered gate.
- In authorized direct-deployment mode, replace the download/install lines with `Installed automatically`, exact deployed paths, exact source head, deployed hashes, smoke result, and rollback location. The user must not repeat an installation the worker already completed.
- **No verified direct deployment and no working GitHub candidate download = no user test request.** Continue deployment/packaging work or report the concrete blocker without assigning the user a test.

Detailed handoff mechanics live in `docs/runtime-artifact-gate.md`; that document cannot override this charter or the registry.

## Safety and isolation

- Work in the module's single live implementation branch/PR discovered from GitHub evidence; if none exists, start from current `main` only as allowed by the worker execution loop.
- `main` is integration-only; `runtime-*` and `stable` are deliberate publication channels, not development workspaces.
- Never weaken tests, force-update another workstream, share unrelated concurrency groups, or invoke suite publication for ordinary validation.
- Persistent profile identities require an immutable manifest covering current and retired distributed IDs, backup-first ownership-scoped recovery, and deterministic regression coverage.
- Never rename, reuse, or silently drop a distributed persistent ID.
- A profile-load/save incident freezes feature expansion for that module until recovery and prevention are proven.
- Performance-sensitive code must avoid permanent polling, scene-wide scans, hot-path reflection/allocations, and global UI mutation unless explicitly proven necessary and bounded.

## Model routing and usage economy

The user selects the model; workers only recommend it. At the end of each
coherent result, state the recommended model for the next substantive step in
one short line with a concrete reason. Before beginning a new substantive
phase, warn first only when the currently selected model is materially
unsuitable; otherwise continue without pausing for approval.

- Use **GPT-5.3-Codex-Spark** for bounded, well-specified work: localized code
  changes, tests, configuration, manifests, documentation, mechanical cleanup,
  reruns, and verification against an already accepted design.
- Use **GPT-5.6 Terra** for routine production implementation that still needs
  sound engineering judgment but has a clear scope and accepted architecture.
- Use **GPT-5.6 Luna** for high-volume, low-risk mechanical work such as narrow
  extraction, classification, formatting, simple configuration edits, and
  focused checks where small quality differences are not material.
- Use **GPT-5.5** only as a low-risk fallback for small, clearly specified work
  when Spark or the appropriate GPT-5.6 model is unavailable or offers no
  practical advantage.
- Use **GPT-5.6 Sol** for architecture, ambiguous implementation, runtime-sensitive
  integration, profile migrations, cross-module compatibility, unfamiliar
  failures, and substantial debugging or refactoring.
- Consider **GPT-6 Astra** only for a narrowly identified, exceptionally hard
  blocker where Sol has failed to resolve the problem or the decision has
  unusually high correctness impact. Return to Sol or Spark after that bounded
  problem is resolved.
- Reassess the recommendation when evidence turns a routine task into an
  architectural or runtime problem. Do not spend Sol or Astra on mechanical
  work already determined by the recorded plan.

Model routing is advisory only. It is not a governance gate, a valid stop
condition, permission to change scope, or a reason to interrupt suitable work.
Do not encode quotas or assume model availability; use the models actually
offered to the user at that time.

## Communication

Use only:

1. one short start acknowledgement;
2. a material root cause or plan-changing CI failure;
3. a genuine blocker/runtime gate;
4. one coherent package/RC/stable result.

Do not narrate file edits, branch creation, commits, CI polling, documentation, or every internal gate. Intermediate updates are non-terminal and require no user response.

### PR evidence economy

A PR timeline is not an execution log. Preserve technical proof while minimizing worker runs, comments, commits, and duplicated text:

- Use commits, Checks, Actions logs, artifact metadata, and test outputs as the primary evidence. Do not copy them into PR comments unless a human decision or runtime handoff depends on the result.
- Keep the PR body as one concise current summary. Update it when the authoritative state materially changes instead of appending repetitive status comments.
- Do not post comments for individual commits, pushes, checkpoint saves, CI starts, routine failures/retries, green reruns, documentation-only updates, model recommendations, or ordinary phase transitions.
- A new PR/Issue comment is justified only by a material root cause or plan-changing failure, a genuine blocker, an actionable runtime-test handoff, or one coherent RC/stable result.
- Batch implementation and validation into coherent logical commits. Amend or fix up unpublished intermediate work when safe; never force-rewrite shared published history merely to make an old timeline look cleaner.
- Do not run a worker solely to write status, duplicate evidence already recorded by GitHub, perform cosmetic reporting, or create documentation with no product, contract, operational, or maintenance value.
- Documentation required by the product, compatibility contract, installation, recovery, or future maintenance remains part of the same implementation run; it is not a separate reporting phase.
- Prefer the least expensive suitable model and the fewest runs that preserve correctness. Evidence economy must never remove required tests, exact-head verification, runtime smoke, artifact validation, or safety gates.

### CI and artifact economy

Repository automation must keep validation cheap and release evidence deliberate:

- A normal GitHub PR push runs only the smallest affected deterministic validation. Expensive hosted exact-runtime downloads, hosted server-start smoke, package assembly, publication, and install-ready artifact upload run only at a coherent RC/release boundary or explicit `workflow_dispatch`. This does not prevent an authorized local worker from exact-runtime building, smoking, and directly deploying its owned module without creating a ZIP.
- Validation output already preserved in the Actions log is not uploaded again as a transient JSON/report artifact. Upload only an actionable install-ready candidate or a file genuinely required to diagnose a non-reproducible failure.
- Do not create a new workflow for a one-off diagnostic, source transport, checkpoint, or model handoff. Use local tooling, an existing manual diagnostic entry point, or a temporary uncommitted script instead.
- A temporary workflow that is exceptionally required must be removed from the branch immediately after use and disabled in GitHub when retired. It must never remain registered as active after its source file is gone.
- Workflow path filters stay inside the owning module plus the workflow itself. Root README or unrelated module documentation must not trigger a module build.
- Keep fast validation separate from expensive RC/publication work. Prefer one coherent module validation workflow over several workflows that repeatedly provision the same SDKs and dependencies for the same change.
- Every uploaded artifact declares a bounded retention period. Stable distribution belongs in a deliberate runtime/release channel, not in indefinitely retained CI artifacts.

### Scope and compatibility hygiene

- A module PR changes only its owned module, its directly required integration boundary, and the minimum shared workflow/docs needed for that implementation. Unrelated suite-wide version edits, documentation refreshes, and neighboring-module cleanup do not hitchhike in a feature PR.
- Express supported runtime compatibility as a range (for example `~4.1.0`) separately from the exact SPT version used as the current validation baseline. Do not turn one successfully tested patch into an unnecessary hard product lock.
- Historical evidence may keep the exact SPT version it proved, but active requirements, package metadata, and user-facing compatibility text must clearly distinguish compatibility range from validation baseline.
- Before adding a validator, manifest, planning document, or regression file, reuse or extend an existing authority when practical. Do not create a new file solely to restate a fact already enforced elsewhere.

## Repository lifecycle

`Issue -> short-lived branch -> implementation -> module CI -> PR -> runtime gate when required -> merge -> verify main -> close/update Issue -> delete temporary branch`

Detailed repository mechanics live in `CONTRIBUTING.md`, `docs/development-workflow.md`, `docs/github-stable-runtime.md`, and `docs/branch-hygiene.md`. If any text conflicts, this charter and `.github/workstreams.json` win.
