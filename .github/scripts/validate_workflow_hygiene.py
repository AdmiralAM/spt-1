#!/usr/bin/env python3
"""Reject recurring workflow and artifact churn before it reaches main."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS = ROOT / ".github" / "workflows"
TEMPORARY_PREFIXES = ("diagnostic-", "temp-", "tmp-")
UPLOAD_ACTION = "uses: actions/upload-artifact@"


def fail(messages: list[str]) -> None:
    for message in messages:
        print(f"workflow hygiene invalid: {message}", file=sys.stderr)
    raise SystemExit(1)


def upload_steps(lines: list[str]) -> list[tuple[int, list[str]]]:
    """Return (line number, full step lines) for upload-artifact steps."""
    steps: list[tuple[int, list[str]]] = []
    start = 0
    for index, line in enumerate(lines):
        if re.match(r"^\s{6}-\s", line):
            start = index
        if UPLOAD_ACTION in line:
            end = len(lines)
            for candidate in range(index + 1, len(lines)):
                if re.match(r"^\s{6}-\s", lines[candidate]):
                    end = candidate
                    break
            steps.append((index + 1, lines[start:end]))
    return steps


def main() -> None:
    errors: list[str] = []
    checked_uploads = 0

    for path in sorted(WORKFLOWS.glob("*.yml")):
        if path.name.startswith(TEMPORARY_PREFIXES):
            errors.append(f"{path.name}: temporary/diagnostic workflows are forbidden")

        text = path.read_text(encoding="utf-8")
        lines = text.splitlines()
        pull_request_enabled = bool(re.search(r"^\s{2}pull_request:\s*$", text, re.MULTILINE))

        for line_number, step in upload_steps(lines):
            checked_uploads += 1
            step_text = "\n".join(step)
            if not re.search(r"^\s+retention-days:\s+\d+\s*$", step_text, re.MULTILINE):
                errors.append(
                    f"{path.name}:{line_number}: upload-artifact needs bounded retention-days"
                )
            if pull_request_enabled and "github.event_name == 'workflow_dispatch'" not in step_text:
                errors.append(
                    f"{path.name}:{line_number}: pull-request runs must not upload artifacts; "
                    "gate the upload to workflow_dispatch"
                )

        if re.search(r"^\s+-\s+['\"]?README\.md['\"]?\s*$", text, re.MULTILINE):
            errors.append(
                f"{path.name}: module workflow path filters must not include the root README"
            )

    if errors:
        fail(errors)

    print(
        f"workflow hygiene valid: {len(list(WORKFLOWS.glob('*.yml')))} workflows, "
        f"{checked_uploads} bounded manual/publication artifact uploads"
    )


if __name__ == "__main__":
    main()
