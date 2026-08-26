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
PLUGIN = ROOT / "client" / "Plugin.cs"
VISUAL = ROOT / "client" / "VisualLayer.cs"


def method_body(source: str, name: str) -> str:
    """Return one balanced C# method body instead of matching into later methods."""
    match = re.search(rf"\b(?:void|float|string|bool)\s+{re.escape(name)}\s*\([^)]*\)\s*\{{", source)
    if not match:
        raise SystemExit(f"missing method: {name}")
    start = match.end() - 1
    depth = 0
    in_string = False
    escaped = False
    for index in range(start, len(source)):
        char = source[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[start:index + 1]
    raise SystemExit(f"unterminated method: {name}")


def main() -> int:
    if not PLUGIN.exists() or not VISUAL.exists():
        raise SystemExit("missing Tactical HUD source files")
    plugin = PLUGIN.read_text(encoding="utf-8")
    visual = VISUAL.read_text(encoding="utf-8")
    bodies = {
        "Refresh": method_body(plugin, "Refresh"),
        "Update": method_body(plugin, "Update"),
        "OnGUI": method_body(plugin, "OnGUI"),
        "Render": method_body(visual, "Render"),
        "Text": method_body(visual, "Text"),
        "DrawPopulation": method_body(visual, "DrawPopulation"),
        "DrawStatus": method_body(visual, "DrawStatus"),
        "DrawKillFeed": method_body(visual, "DrawKillFeed"),
        "HitKey": method_body(visual, "HitKey"),
        "WeaponKey": method_body(visual, "WeaponKey"),
    }
    rules = [
        ("refresh-list-allocation", "Refresh", re.compile(r"new\s+List<"), True,
         "Refresh allocates a List; reuse a field buffer instead."),
        ("refresh-hashset-allocation", "Refresh", re.compile(r"new\s+HashSet<"), True,
         "Refresh allocates a HashSet; reuse a field buffer instead."),
        ("refresh-linq-materialization", "Refresh", re.compile(r"\.(?:Where|Select)\s*\(.*?\)\s*\.ToList\s*\(", re.S), True,
         "Refresh materializes LINQ; use an indexed/reusable removal buffer."),
        ("ongui-resource-scan", "OnGUI", re.compile(r"Resources\.FindObjectsOfTypeAll"), True,
         "OnGUI performs a global Resources scan."),
        ("update-resource-scan", "Update", re.compile(r"Resources\.FindObjectsOfTypeAll"), True,
         "Update performs a global Resources scan."),
        ("ongui-texture-allocation", "OnGUI", re.compile(r"new\s+Texture2D"), True,
         "OnGUI allocates Texture2D objects."),
        ("update-texture-allocation", "Update", re.compile(r"new\s+Texture2D"), True,
         "Update allocates Texture2D objects."),
        ("removeall-lambda-update", "Update", re.compile(r"RemoveAll\s*\([^;]*=>"), False,
         "Update uses RemoveAll(lambda); use an allocation-free reverse loop."),
        ("text-guicontent-allocation", "Text", re.compile(r"new\s+GUIContent"), True,
         "Text allocates GUIContent objects instead of reusing the renderer cache."),
        ("population-number-formatting", "DrawPopulation", re.compile(r"\.ToString\s*\("), True,
         "DrawPopulation formats numbers on every repaint."),
        ("status-number-formatting", "DrawStatus", re.compile(r"\.ToString\s*\("), True,
         "DrawStatus formats numbers on every repaint."),
        ("killfeed-reclassification", "DrawKillFeed", re.compile(r"\b(?:WeaponKey|HitKey|CleanWeapon)\s*\("), True,
         "DrawKillFeed reclassifies immutable kill data on every repaint."),
        ("hitkey-lowercase", "HitKey", re.compile(r"ToLowerInvariant\s*\("), True,
         "HitKey allocates a lowercase copy."),
        ("weaponkey-lowercase", "WeaponKey", re.compile(r"ToLowerInvariant\s*\("), True,
         "WeaponKey allocates a lowercase copy."),
    ]
    failures = 0
    hits = 0
    for name, method, pattern, fatal, message in rules:
        if pattern.search(bodies[method]):
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
