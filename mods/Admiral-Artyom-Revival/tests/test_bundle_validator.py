#!/usr/bin/env python3
"""Regression tests for the Artem transported bundle ZIP validator."""

from __future__ import annotations

import importlib.util
import json
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "tools" / "validate_bundle_archives.py"

spec = importlib.util.spec_from_file_location("validate_bundle_archives", TOOL)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


def write_zip(path: Path, members: list[str]) -> None:
    with zipfile.ZipFile(path, "w") as zf:
        for member in members:
            zf.writestr(member, b"bundle")


def main() -> int:
    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        manifest = root / "bundles.json"
        manifest.write_text(
            json.dumps(
                {
                    "manifest": {
                        "Bundles/weapons/test-a.bundle": {},
                        "clothes/test-b.bundle": {},
                        "BUNDLES/hand/test-c.bundle": {},
                    }
                }
            ),
            encoding="utf-8",
        )

        first = root / "first.zip"
        second = root / "second.zip"
        write_zip(first, ["Bundles/weapons/test-a.bundle", "clothes/test-b.bundle", "readme.txt"])
        write_zip(second, ["BUNDLES/hand/test-c.bundle", "Bundles/extra/orphan.bundle"])

        expected = module.load_manifest(manifest)
        assert expected == {
            "weapons/test-a.bundle",
            "clothes/test-b.bundle",
            "hand/test-c.bundle",
        }

        physical = set(module.archive_bundle_members(first)) | set(module.archive_bundle_members(second))
        assert expected <= physical
        assert "extra/orphan.bundle" in physical - expected

        duplicate = root / "duplicate.zip"
        write_zip(duplicate, ["Bundles/weapons/test-a.bundle"])
        members = module.archive_bundle_members(first) + module.archive_bundle_members(duplicate)
        assert members.count("weapons/test-a.bundle") == 2

    print("OK: bundle validator normalization/split/archive regression checks")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
