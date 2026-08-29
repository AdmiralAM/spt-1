from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
violations = []


def require(path: Path, tokens, label: str):
    if not path.exists():
        violations.append(f"{label}: missing file {path}")
        return
    text = path.read_text(encoding="utf-8-sig")
    for token in tokens:
        if token not in text:
            violations.append(f"{label}: missing authority token {token!r}")


require(
    ROOT / "README.md",
    [
        "Development candidate **v0.2.0**",
        "Issue **#285**",
        "PR **#286**",
        "Candidate install / upgrade",
        "physical DLL filename intentionally remains",
        "Magazine operational integration",
        "BUILD-INFO.txt",
    ],
    "README current authority",
)

require(
    ROOT / "DESIGN-SPT-4.1.3-BELT.md",
    [
        "Issue #285 / PR #286",
        "Issue #287",
        "Magazine reload reachability",
        "Candidate identity / upgrade boundary",
        "Physical runtime acceptance is one combined gate",
    ],
    "DESIGN current authority",
)

require(
    DOCS / "product-concept.md",
    [
        "Development candidate **v0.2.0**",
        "low-priority reload fallback",
        "Magazine reload role",
        "Current gate",
    ],
    "product concept current authority",
)

require(
    DOCS / "RC1-runtime-checklist.md",
    [
        "Issue #285 / PR #286",
        "Issue #287",
        "Candidate runtime version is **0.2.0**",
        "Reload — vanilla source remains first",
        "Reload — wearable fallback",
        "1 PASS / 2 PASS / ... / 7 PASS",
    ],
    "runtime checklist current authority",
)

require(
    ROOT / "profile-safety" / "README.md",
    [
        "development v0.2.0",
        "68ac00000000000000000012",
        "Utility HeadBand `cigarettes`",
        "BAndHBHeadBandSplitGridV1",
        "do not keep the migration permanently pending",
        "pre-v0.2.0 profile backup",
    ],
    "profile-safety v0.2 recovery authority",
)

require(
    DOCS / "phase1-runtime-contract.md",
    [
        "# Phase 1 runtime contract — archived snapshot",
        "**Historical evidence only.**",
        "Current development authority is Issue #285 / PR #286.",
    ],
    "Phase 1 archive marker",
)

require(
    DOCS / "architecture-audit.md",
    [
        "archived Phase 1 snapshot",
        "**Historical evidence only.**",
        "Current v0.2.0 acceptance is defined only by `RC1-runtime-checklist.md`.",
    ],
    "architecture audit archive marker",
)

if violations:
    raise SystemExit("B&A&HB doc-authority gate failed:\n" + "\n".join(violations))

print("B&A&HB doc-authority gate: OK (v0.2.0 current/recovery authority aligned; Issue #285/PR #286/#287 explicit; Phase 1 records archived, not current)")
