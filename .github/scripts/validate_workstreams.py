#!/usr/bin/env python3
"""Validate the canonical user-authorized, self-advancing SPT workstreams."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = ROOT / ".github" / "workstreams.json"
RESUME_POLICY = "first-phase-without-recorded-acceptance-evidence"
REQUIRED = {
    "workstreamName", "productName", "modulePath", "version",
    "resumePolicy", "phasePlan", "frozen", "stableAcceptance",
}
DEPRECATED_DYNAMIC_POINTERS = {
    "activeIssue", "activePr", "activeBranch", "activeDevelopment",
    "currentPhase", "activePackage", "roadmap", "successor", "state", "userGate",
}


def fail(message: str) -> None:
    print(f"workstream registry invalid: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> None:
    try:
        data = json.loads(REGISTRY.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        fail(str(error))

    if data.get("schemaVersion") != 4:
        fail("schemaVersion must be 4")
    if "controller" in data:
        fail("controller is forbidden; no worker may gate another worker")

    authority = data.get("authority", {})
    if authority.get("productAuthority") != "user":
        fail("the user must be the sole product authority")
    if authority.get("coordinationWorker") != "GitHub Work SPT":
        fail("GitHub Work SPT must remain the coordination worker")
    if authority.get("coordinationWorkerMayGateExecution") is not False:
        fail("the coordination worker must not gate execution")
    if authority.get("workerRequiresAnotherWorkerPermission") is not False:
        fail("workers must not require another worker's permission")
    if authority.get("workerMayEncodeExplicitUserGovernanceInstruction") is not True:
        fail("workers must be able to encode an explicit user instruction")
    if authority.get("governanceBranchPrefix") != "governance/":
        fail("governanceBranchPrefix must be governance/")

    branch_contract = authority.get("branchSelectionContract", {})
    if branch_contract.get("rule") != "discover-single-live-implementation-pr-from-github":
        fail("live implementation authority must be discovered from GitHub")
    if branch_contract.get("registryStoresTemporaryPointers") is not False:
        fail("registry must not store temporary implementation pointers")
    if branch_contract.get("exactHeadSource") != "single-live-pr-head":
        fail("exact live head must come from the discovered live PR")
    if branch_contract.get("noLivePr") != "create-from-current-main-when-coherent-implementation-exists":
        fail("no-live-PR behavior is invalid")
    if branch_contract.get("multipleLivePrs") != "reconcile-to-one-from-current-issue-pr-evidence":
        fail("multiple-live-PR behavior is invalid")

    execution = data.get("execution", {})
    if execution.get("roadmapAuthorization") != "user-standing-authorization-all-recorded-phases":
        fail("the entire recorded roadmap must carry the user's standing authorization")
    if execution.get("resumePolicy") != RESUME_POLICY:
        fail("execution resumePolicy is invalid")
    if execution.get("workerMayAdvanceWithinRecordedRoadmap") is not True:
        fail("workers must advance within the recorded roadmap")
    if execution.get("registryUpdateRequiredForRecordedPhaseTransition") is not False:
        fail("ordinary recorded phase transitions must not require registry edits")
    if execution.get("liveImplementationAuthority") != "discover-single-live-pr-from-github":
        fail("execution must discover live implementation authority from GitHub")
    if execution.get("registryStoresDynamicImplementationPointers") is not False:
        fail("execution must forbid dynamic implementation pointers in the registry")
    if "maxActiveRuntimeGates" in execution:
        fail("global runtime-gate counters require forbidden inter-worker coordination")
    if execution.get("runtimeGateActivation") != "worker-direct-to-user-at-recorded-runtime-phase":
        fail("runtime handoff must go directly from the worker to the user")
    if execution.get("workerMayRequestUserRuntimeDirectly") is not True:
        fail("workers must be able to request the recorded runtime gate directly")
    if execution.get("maxActivePullRequestsPerModule") != 1:
        fail("at most one active PR per module is permitted")
    if execution.get("workerMayChangeRoadmapWithoutExplicitUserInstruction") is not False:
        fail("workers must not change roadmaps without an explicit user instruction")
    if execution.get("workerMayPerformGovernanceWithoutExplicitUserInstruction") is not False:
        fail("workers must not perform governance without an explicit user instruction")

    workstreams = data.get("workstreams")
    if not isinstance(workstreams, dict) or not workstreams:
        fail("workstreams must be a non-empty object")

    phase_count = 0
    for key, stream in workstreams.items():
        if not isinstance(stream, dict):
            fail(f"{key} must be an object")
        missing = REQUIRED - stream.keys()
        if missing:
            fail(f"{key} is missing {sorted(missing)}")
        deprecated = DEPRECATED_DYNAMIC_POINTERS & stream.keys()
        if deprecated:
            fail(f"{key} contains dynamic/controller pointers {sorted(deprecated)}")
        if stream["resumePolicy"] != RESUME_POLICY:
            fail(f"{key}.resumePolicy is invalid")
        if not stream["stableAcceptance"]:
            fail(f"{key}.stableAcceptance must not be empty")

        phases = stream["phasePlan"]
        if not isinstance(phases, list) or not phases:
            fail(f"{key}.phasePlan must be a non-empty list")
        keys: list[str] = []
        for index, phase in enumerate(phases):
            if not isinstance(phase, dict):
                fail(f"{key}.phasePlan[{index}] must be an object")
            for field in ("key", "issue", "objective", "acceptance", "requiresUserRuntime"):
                if field not in phase:
                    fail(f"{key}.phasePlan[{index}] is missing {field}")
            if not isinstance(phase["key"], str) or not phase["key"]:
                fail(f"{key}.phasePlan[{index}].key is invalid")
            if phase["issue"] is not None and (
                not isinstance(phase["issue"], int) or phase["issue"] <= 0
            ):
                fail(f"{key}.phasePlan[{index}].issue is invalid")
            if not phase["objective"] or not phase["acceptance"]:
                fail(f"{key}.phasePlan[{index}] needs objective and acceptance")
            if not isinstance(phase["requiresUserRuntime"], bool):
                fail(f"{key}.phasePlan[{index}].requiresUserRuntime must be boolean")
            keys.append(phase["key"])
        if len(keys) != len(set(keys)):
            fail(f"{key}.phasePlan contains duplicate keys")
        if keys[-1] != "stable-release":
            fail(f"{key}.phasePlan must end with stable-release")
        if not any(phase["requiresUserRuntime"] for phase in phases):
            fail(f"{key}.phasePlan must contain a physical runtime phase")
        phase_count += len(phases)

    print(
        f"workstream registry valid: {len(workstreams)} modules, "
        f"{phase_count} user-authorized phases, live PR discovery, direct user runtime handoff"
    )


if __name__ == "__main__":
    main()
