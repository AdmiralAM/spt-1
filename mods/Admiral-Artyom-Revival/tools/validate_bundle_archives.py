#!/usr/bin/env python3
"""Validate the transported Artem bundle ZIP set against bundles.json.

The six uploaded ZIP archives together represent one logical Bundles directory.
This tool is read-only: it checks exact manifest paths, duplicate logical paths,
and physical files outside the manifest without extracting Unity bundles.
"""

from __future__ import annotations

import argparse
import json
import zipfile
from collections import defaultdict
from pathlib import Path, PurePosixPath


def load_manifest(path: Path) -> set[str]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if isinstance(payload, dict):
        entries = payload.get("manifest") or payload.get("bundles") or payload
        if isinstance(entries, dict):
            candidates = entries.keys()
        elif isinstance(entries, list):
            candidates = [entry.get("key") or entry.get("path") for entry in entries if isinstance(entry, dict)]
        else:
            raise SystemExit("unsupported bundles.json structure")
    elif isinstance(payload, list):
        candidates = [entry.get("key") or entry.get("path") for entry in payload if isinstance(entry, dict)]
    else:
        raise SystemExit("unsupported bundles.json structure")

    result = set()
    for value in candidates:
        if not value:
            continue
        value = str(value).replace("\\", "/").lstrip("/")
        if value.lower().startswith("bundles/"):
            value = value[len("bundles/"):]
        result.add(value.lower())
    return result


def archive_bundle_members(archive: Path) -> list[str]:
    result = []
    with zipfile.ZipFile(archive) as zf:
        for info in zf.infolist():
            if info.is_dir() or not info.filename.lower().endswith(".bundle"):
                continue
            name = info.filename.replace("\\", "/").lstrip("/")
            parts = list(PurePosixPath(name).parts)
            if parts and parts[0].lower() == "bundles":
                parts = parts[1:]
            result.append("/".join(parts).lower())
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path, help="Path to Artem bundles.json")
    parser.add_argument("archives", nargs="+", type=Path, help="The six transported bundle ZIP archives")
    args = parser.parse_args()

    expected = load_manifest(args.manifest.resolve())
    locations: dict[str, list[str]] = defaultdict(list)
    for archive in args.archives:
        archive = archive.resolve()
        if not archive.is_file():
            raise SystemExit(f"archive does not exist: {archive}")
        for member in archive_bundle_members(archive):
            locations[member].append(archive.name)

    physical = set(locations)
    missing = sorted(expected - physical)
    orphan = sorted(physical - expected)
    duplicate_paths = {path: sources for path, sources in sorted(locations.items()) if len(sources) > 1}

    print(f"manifest paths: {len(expected)}")
    print(f"physical logical bundle paths: {len(physical)}")
    print(f"missing required paths: {len(missing)}")
    print(f"physical paths outside manifest: {len(orphan)}")
    print(f"duplicate logical paths: {len(duplicate_paths)}")

    if missing:
        print("\nMISSING:")
        for path in missing:
            print(f"  {path}")

    if orphan:
        print("\nOUTSIDE MANIFEST:")
        for path in orphan:
            print(f"  {path}")

    if duplicate_paths:
        print("\nDUPLICATE LOGICAL PATHS:")
        for path, sources in duplicate_paths.items():
            print(f"  {path}: {', '.join(sources)}")

    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
