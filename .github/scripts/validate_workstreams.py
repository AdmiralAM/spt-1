#!/usr/bin/env python3
"""Validate the canonical SPT workstream registry."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = ROOT / ".github" / "workstreams.json"
VALID_STATES = {"ACTIVE", "BLOCKED", "RUNTIME_GATE", "PARKED", "STABLE"}
VALID_GATES = {"none", "queued", "active", "passed", "failed"}
REQUIRED = {
    "workstreamName", "productName", "modulePath", "version", "state",
    "activeIssue", "activePr", "activeBranch", "currentPhase", "activePackage", "roadmap",
    "successor", "userGate", "frozen", "stableAcceptance",
}


def fail(message: str) -> None:
    print(f"workstream registry invalid: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> None:
    try:
        data = json.loads(REGISTRY.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        fail(str(error))

    if data.get("schemaVersion") != 1:
        fail("schemaVersion must be 1")
    controller = data.get("controller", {})
    if controller.get("name") != "GitHub Work SPT":
        fail("GitHub Work SPT must be the controller")
    if controller.get("workersMayEditControl") is not False:
        fail("workersMayEditControl must be false")
    if controller.get("governanceBranchPrefix") != "governance/":
        fail("governanceBranchPrefix must be governance/")

    execution = data.get("execution", {})
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

    active_prs: dict[int, str] = {}
    active_branches: dict[str, str] = {}
    active_gates = 0
    for key, stream in workstreams.items():
        if not isinstance(stream, dict):
            fail(f"{key} must be an object")
        missing = REQUIRED - stream.keys()
        if missing:
            fail(f"{key} is missing {sorted(missing)}")
        if stream["state"] not in VALID_STATES:
            fail(f"{key}.state is invalid")
        if stream["userGate"] not in VALID_GATES:
            fail(f"{key}.userGate is invalid")
        active_gates += stream["userGate"] == "active"

        roadmap = stream["roadmap"]
        if not isinstance(roadmap, list) or not roadmap:
            fail(f"{key}.roadmap must be a non-empty list")
        if len(roadmap) != len(set(roadmap)):
            fail(f"{key}.roadmap contains duplicates")
        if stream["currentPhase"] not in roadmap:
            fail(f"{key}.currentPhase is not in its roadmap")
        if stream["successor"] is not None and stream["successor"] not in roadmap:
            fail(f"{key}.successor is not in its roadmap")
        if not stream["stableAcceptance"]:
            fail(f"{key}.stableAcceptance must not be empty")
        package = stream["activePackage"]
        if not isinstance(package, dict):
            fail(f"{key}.activePackage must be an object")
        if not package.get("objective") or not package.get("acceptance"):
            fail(f"{key}.activePackage needs objective and acceptance")
        if package.get("continueWithoutUser") is not True:
            fail(f"{key}.activePackage.continueWithoutUser must be true")

        active_pr = stream["activePr"]
        if active_pr is not None:
            if not isinstance(active_pr, int) or active_pr <= 0:
                fail(f"{key}.activePr must be a positive integer or null")
            if active_pr in active_prs:
                fail(f"PR #{active_pr} belongs to both {active_prs[active_pr]} and {key}")
            active_prs[active_pr] = key

        branch = stream["activeBranch"]
        if branch is not None:
            if not isinstance(branch, str) or not branch:
                fail(f"{key}.activeBranch must be a non-empty string or null")
            if branch in active_branches:
                fail(f"branch {branch} belongs to both {active_branches[branch]} and {key}")
            active_branches[branch] = key

    if active_gates > execution["maxActiveRuntimeGates"]:
        fail("too many active user runtime gates")
    print(
        f"workstream registry valid: {len(workstreams)} modules, "
        f"{len(active_prs)} active PRs, {active_gates} active runtime gates"
    )


if __name__ == "__main__":
    main()
