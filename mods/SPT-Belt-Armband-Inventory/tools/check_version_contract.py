from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLIENT_PROJECT = ROOT / "src" / "SPT-Belt-Armband-Inventory.csproj"
PLUGIN = ROOT / "src" / "Plugin.cs"
SERVER_PROJECT = ROOT / "server" / "SPT-Belt-Armband-Inventory.Server.csproj"
SERVER_MOD = ROOT / "server" / "ServerMod.cs"
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
        # Keep the v0.1.0 physical filename for in-place overwrite compatibility.
        # Runtime/assembly identity is v0.3.0; changing the filename would leave
        # the stable DLL beside the candidate and create duplicate BepInEx GUIDs.
        "<AssemblyName>SPT Belt Armband Inventory v0.1.0</AssemblyName>",
        "<Version>0.3.0</Version>",
        "<AssemblyVersion>0.3.0.0</AssemblyVersion>",
        "<FileVersion>0.3.0.0</FileVersion>",
    ],
    "client v0.3 identity with upgrade-safe legacy path",
)

require(
    SERVER_PROJECT,
    [
        "<Version>0.3.0</Version>",
        "<AssemblyVersion>0.3.0.0</AssemblyVersion>",
        "<FileVersion>0.3.0.0</FileVersion>",
    ],
    "server assembly v0.3 identity",
)

require(
    SERVER_MOD,
    [
        'public string ModGuid { get; init; } = "com.admiralam.spt.belt-armband-inventory.server";',
        'public string Name { get; init; } = "B&A&HB #2 MOD SPT Server";',
        "public SemanticVersioning.Version Version { get; init; } = new(0, 3, 0);",
        'public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");',
    ],
    "SPT server mod v0.3 identity",
)

require(
    PLUGIN,
    [
        'public const string PluginGuid = "com.admiralam.spt.belt-armband-inventory";',
        'public const string PluginName = "B&A&HB #2 MOD SPT";',
        'public const string PluginVersion = "0.3.0";',
    ],
    "BepInEx v0.3 identity",
)

require(
    WORKFLOW,
    [
        "SPT Belt Armband Inventory v0.1.0.dll",
        "ReleaseLine=v0.3.0",
        "ClientRuntimeVersion=0.3.0",
        "ClientFilenameCompatibility=legacy-v0.1.0-path",
        "StableBaseline=v0.2.0",
        "SPTTarget=4.1.3",
        "BUILD-INFO.txt",
        "SPT Belt Armband Inventory v0.3.0.dll",
        'Copy-Item -LiteralPath "$root/BUILD-INFO.txt" -Destination "$modRoot/BUILD-INFO.txt" -Force',
        '$installedBuildInfo = Get-Content -LiteralPath "$modRoot/BUILD-INFO.txt" -Raw',
        "if ($installedBuildInfo -ne $buildInfo)",
    ],
    "stable artifact v0.3 identity",
)

if violations:
    raise SystemExit("B&A&HB version-contract gate failed:\n" + "\n".join(violations))

print("B&A&HB version-contract gate: OK (client assembly/BepInEx + server assembly/SPT mod metadata all v0.3.0; upgrade-safe client path overwrites stable DLL; duplicate v0.3 filename forbidden; root+installed provenance frozen)")
