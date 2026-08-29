from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLIENT_PROJECT = ROOT / "src" / "SPT-Belt-Armband-Inventory.csproj"
PLUGIN = ROOT / "src" / "Plugin.cs"
SERVER_PROJECT = ROOT / "server" / "SPT-Belt-Armband-Inventory.Server.csproj"
WORKFLOW = ROOT.parents[1] / ".github" / "workflows" / "belt-armband-validate.yml"

violations = []


def require(path: Path, tokens, label: str):
    if not path.exists():
        violations.append(f"{label}: missing {path}")
        return
    text = path.read_text(encoding="utf-8-sig")
    for token in tokens:
        if token not in text:
            violations.append(f"{label}: missing contract token {token!r}")


require(
    CLIENT_PROJECT,
    [
        "<AssemblyName>SPT Belt Armband Inventory v0.2.0</AssemblyName>",
        "<Version>0.2.0</Version>",
        "<AssemblyVersion>0.2.0.0</AssemblyVersion>",
        "<FileVersion>0.2.0.0</FileVersion>",
    ],
    "client v0.2 identity",
)

require(
    SERVER_PROJECT,
    [
        "<Version>0.2.0</Version>",
        "<AssemblyVersion>0.2.0.0</AssemblyVersion>",
        "<FileVersion>0.2.0.0</FileVersion>",
    ],
    "server v0.2 identity",
)

require(
    PLUGIN,
    [
        'public const string PluginGuid = "com.admiralam.spt.belt-armband-inventory";',
        'public const string PluginName = "B&A&HB #2 MOD SPT";',
        'public const string PluginVersion = "0.2.0";',
    ],
    "BepInEx v0.2 identity",
)

require(
    WORKFLOW,
    [
        "SPT Belt Armband Inventory v0.2.0.dll",
        "CandidateLine=v0.2.0",
        "StableBaseline=v0.1.0",
        "SPTTarget=4.1.3",
        "BUILD-INFO.txt",
    ],
    "artifact v0.2 identity",
)

if violations:
    raise SystemExit("B&A&HB version-contract gate failed:\n" + "\n".join(violations))

print("B&A&HB version-contract gate: OK (development candidate=0.2.0; stable baseline=0.1.0; GUID/name preserved; artifact identity stamped)")
