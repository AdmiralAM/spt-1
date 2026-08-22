#!/usr/bin/env python3
"""Static guardrail for obvious Tactical HUD hot-path allocation regressions.

This is intentionally conservative: it does not try to replace a profiler.
It catches patterns that are easy to reintroduce into Refresh/Update/OnGUI and
that are disproportionately expensive in a continuously-running Unity HUD.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGETS = [
    ROOT / "SPT-PopCounter" / "Plugin.cs",
    ROOT / "SPT-PopCounter" / "VisualLayer.cs",
]

# Patterns are reported as warnings unless explicitly marked fatal. The script
# returns non-zero only for known high-confidence regressions in recurring paths.
RULES = [
    ("refresh-list-allocation", re.compile(r"void\s+Refresh\s*\(\).*?new\s+List<", re.S), True,
     "Refresh allocates a List; reuse a field buffer instead."),
    ("refresh-hashset-allocation", re.compile(r"void\s+Refresh\s*\(\).*?new\s+HashSet<", re.S), True,
     "Refresh allocates a HashSet; reuse a field buffer instead."),
    ("refresh-linq-materialization", re.compile(r"void\s+Refresh\s*\(\).*?\.(?:Where|Select)\s*\(.*?\)\s*\.ToList\s*\(", re.S), True,
     "Refresh materializes LINQ; use an indexed/reusable removal buffer."),
    ("ongui-resource-scan", re.compile(r"void\s+OnGUI\s*\(\).*?Resources\.FindObjectsOfTypeAll", re.S), True,
     "OnGUI performs a global Resources scan."),
    ("update-resource-scan", re.compile(r"void\s+Update\s*\(\).*?Resources\.FindObjectsOfTypeAll", re.S), True,
     "Update performs a global Resources scan."),
    ("ongui-texture-allocation", re.compile(r"void\s+OnGUI\s*\(\).*?new\s+Texture2D", re.S), True,
     "OnGUI allocates Texture2D objects."),
    ("update-texture-allocation", re.compile(r"void\s+Update\s*\(\).*?new\s+Texture2D", re.S), True,
     "Update allocates Texture2D objects."),
    ("removeall-lambda-update", re.compile(r"void\s+Update\s*\(\).*?RemoveAll\s*\([^;]*=>", re.S), False,
     "Update uses RemoveAll(lambda); consider an allocation-free reverse loop if profiling shows GC pressure."),
]


def read_sources() -> str:
    chunks = []
    for path in TARGETS:
        if not path.exists():
            raise SystemExit(f"missing source: {path.relative_to(ROOT)}")
        chunks.append(f"\n// FILE: {path.name}\n" + path.read_text(encoding="utf-8"))
    return "".join(chunks)


def main() -> int:
    source = read_sources()
    failures = 0
    hits = 0
    for name, pattern, fatal, message in RULES:
        if pattern.search(source):
            hits += 1
            level = "FAIL" if fatal else "WARN"
            print(f"[{level}] {name}: {message}")
            failures += int(fatal)

    if hits == 0:
        print("[OK] no guarded hot-path allocation patterns detected")
    elif failures == 0:
        print("[OK] warnings only; no guarded regressions detected")

    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
