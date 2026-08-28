#!/usr/bin/env python3
"""Validate the canonical self-advancing SPT workstream registry."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = ROOT / ".github" / "workstreams.json"
RESUME_POLICY = "first-phase-without-recorded-acceptance-evidence"
VALID_STATES = {"ACTIVE", "BLOCKED", "RUNTIME_GATE", "PARKED", "STABLE"}
VALID_GATES = {"none", "queued", "active", "passed", "failed"}
REQUIRED = {
    "workstreamName", "productName", "modulePath", "version", "state",
    "resumePolicy", "phasePlan", "userGate", "frozen", "stableAcceptance",
}
DEPRECATED_DYNAMIC_POINTERS = {
    "activeIssue", "activePr", "activeBranch", "currentPhase",
    "activePackage", "roadmap", "successor",
}


def fail(message: str) -> None:
    print(f"workstream registry invalid: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> None:
    try:
        data = json.loads(REGISTRY.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        fail(str(error))

    if data.get("schemaVersion") != 2:
        fail("schemaVersion must be 2")
    controller = data.get("controller", {})
    if controller.get("name") != "GitHub Work SPT":
        fail("GitHub Work SPT must be the controller")
    if controller.get("workersMayEditControl") is not False:
        fail("workersMayEditControl must be false")
    if controller.get("governanceBranchPrefix") != "governance/":
        fail("governanceBranchPrefix must be governance/")

    execution = data.get("execution", {})
    if execution.get("roadmapAuthorization") != "all-recorded-phases":
        fail("the entire recorded roadmap must be authorized")
    if execution.get("resumePolicy") != RESUME_POLICY:
        fail("execution resumePolicy is invalid")
    if execution.get("workerMayAdvanceWithinRecordedRoadmap") is not True:
        fail("workers must advance within the recorded roadmap")
    if execution.get("registryUpdateRequiredForRecordedPhaseTransition") is not False:
        fail("ordinary recorded phase transitions must not require registry edits")
    if execution.get("maxActiveRuntimeGates") != 1:
        fail("exactly one active runtime gate is permitted")
    if execution.get("maxActivePullRequestsPerModule") != 1:
        fail("at most one active PR per module is permitted")
    if execution.get("workerMayChangeRoadmap") is not False:
        fail("workerMayChangeRoadmap must be false")
    if execution.get("workerMayPerformGovernance") is not False:
        fail("workerMayPerformGovernance must be false")

    workstreams = data.get("workstreams")
    if not isinstance(workstreams, dict) or not workstreams:
        fail("workstreams must be a non-empty object")

    active_gates = 0
    phase_count = 0
    for key, stream in workstreams.items():
        if not isinstance(stream, dict):
            fail(f"{key} must be an object")
        missing = REQUIRED - stream.keys()
        if missing:
            fail(f"{key} is missing {sorted(missing)}")
        deprecated = DEPRECATED_DYNAMIC_POINTERS & stream.keys()
        if deprecated:
            fail(f"{key} contains controller-churn pointers {sorted(deprecated)}")
        if stream["state"] not in VALID_STATES:
            fail(f"{key}.state is invalid")
        if stream["userGate"] not in VALID_GATES:
            fail(f"{key}.userGate is invalid")
        active_gates += stream["userGate"] == "active"
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

    if active_gates > execution["maxActiveRuntimeGates"]:
        fail("too many active user runtime gates")
    print(
        f"workstream registry valid: {len(workstreams)} modules, "
        f"{phase_count} pre-authorized phases, {active_gates} active runtime gates"
    )


if __name__ == "__main__":
    main()
