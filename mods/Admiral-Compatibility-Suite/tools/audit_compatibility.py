#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path

SKIP = {"bin", "obj", ".git", ".sources", "artifacts", "build"}
TEXT_EXT = {".cs", ".csproj", ".json", ".jsonc", ".ps1", ".py", ".md", ".yml", ".yaml"}
RULES = {
    "bepin_dependency": re.compile(r"BepInDependency\s*\("),
    "harmony_patch": re.compile(r"HarmonyPatch|\.Patch\s*\("),
    "reflection": re.compile(r"AccessTools|Type\.GetType|\.GetType\s*\(|GetAssemblies\s*\(|Assembly\.Load"),
    "template_mutation": re.compile(r"TemplateTable|ItemTemplates|\.Filters?\b|\.Add\s*\(|\.Remove\s*\("),
    "file_or_config_replacement": re.compile(r"Copy-Item|Move-Item|Remove-Item|File\.(Write|Move|Copy|Delete)|user[\\/]mods|BepInEx[\\/]plugins"),
}
FOREIGN = {
    "amands-sense": re.compile(r"AmandsSense|xyz\.drakia\.Sense", re.I),
    "foldables": re.compile(r"com\.ozen\.foldables|\bFoldables\b", re.I),
    "merge-consumables": re.compile(r"com\.lacyway\.mc|MergeConsumables", re.I),
    "packnstrap": re.compile(r"Pack.?n.?Strap|com\.wtt\.packnstrap", re.I),
    "tgc": re.compile(r"TgcCompatibility|TGC 3\.0|com\.emilanderss0n\.tgc", re.I),
    "ui-fixes": re.compile(r"UIFixes|com\.tyfon\.uifixes", re.I),
    "use-items-anywhere": re.compile(r"UseItemsAnywhere|Use Items Anywhere|com\.cj\.useFromAnywhere", re.I),
}

def files(root: Path):
    scan_base = root / "mods" if (root / "mods").is_dir() else root
    for path in sorted(scan_base.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in TEXT_EXT:
            continue
        if any(part in SKIP for part in path.parts):
            continue
        yield path

def audit(root: Path, source_label: str = "repository") -> dict:
    findings = []
    totals = Counter()
    modules = Counter()
    for path in files(root):
        rel = path.relative_to(root).as_posix()
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        kinds = sorted(name for name, pattern in RULES.items() if pattern.search(text))
        foreign = sorted(name for name, pattern in FOREIGN.items() if pattern.search(text))
        if not kinds and not foreign:
            continue
        parts = rel.split("/")
        module = parts[1] if parts[0] == "mods" and len(parts) > 1 else parts[0]
        for kind in kinds: totals[kind] += 1
        modules[module] += 1
        findings.append({"source": source_label, "path": rel, "module": module, "signals": kinds, "foreignTargets": foreign})
    return {
        "schemaVersion": 1,
        "filesWithSignals": len(findings),
        "signalFileCounts": dict(sorted(totals.items())),
        "moduleFileCounts": dict(sorted(modules.items())),
        "findings": findings,
    }

def audit_many(roots: list[tuple[str, Path]]) -> dict:
    combined = []
    totals = Counter()
    modules = Counter()
    for label, root in roots:
        result = audit(root, label)
        combined.extend(result["findings"])
        totals.update(result["signalFileCounts"])
        modules.update({f"{label}:{name}": count for name, count in result["moduleFileCounts"].items()})
    return {
        "schemaVersion": 1,
        "scannedRoots": [{"label": label, "path": str(root)} for label, root in roots],
        "filesWithSignals": len(combined),
        "signalFileCounts": dict(sorted(totals.items())),
        "moduleFileCounts": dict(sorted(modules.items())),
        "findings": combined,
    }

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--scan-root", action="append", default=[], metavar="LABEL=PATH")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    roots = [("repository", args.root.resolve())]
    for value in args.scan_root:
        label, separator, path = value.partition("=")
        if not separator or not label or not path:
            parser.error("--scan-root must use LABEL=PATH")
        roots.append((label, Path(path).resolve()))
    result = audit_many(roots)
    rendered = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")

if __name__ == "__main__":
    main()
