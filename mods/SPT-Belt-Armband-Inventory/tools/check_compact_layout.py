from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
PATCH = SRC / "CompactFaceHeadBandPresentationPatches.cs"
PLUGIN = SRC / "Plugin.cs"
SETTLE = SRC / "HeadBandRenderSettle.cs"

violations = []
patch = PATCH.read_text(encoding="utf-8-sig") if PATCH.exists() else ""
plugin = PLUGIN.read_text(encoding="utf-8-sig") if PLUGIN.exists() else ""
settle = SETTLE.read_text(encoding="utf-8-sig") if SETTLE.exists() else ""

required_patch = [
    'const float HeadBandHeight = 44f;',
    'const float Gap = 4f;',
    'Enum.Parse(equipmentSlotType, "FaceCover", false)',
    'Mathf.Floor((originalHeight - Gap) * 0.5f)',
    'faceRect.anchoredPosition = state.FaceAnchoredPosition - new Vector2(0f, (HeadBandHeight + Gap) * 0.5f);',
    'headBandRect.anchoredPosition = state.FaceAnchoredPosition + new Vector2(0f, (faceHeight + Gap) * 0.5f);',
    'HeadBandRenderSettle.Suppressed = true;',
    'HeadBandRenderSettle.Suppressed = false;',
    'hostPanelMutation=false',
]
for token in required_patch:
    if token not in patch:
        violations.append(f"compact layout: missing invariant {token!r}")

for forbidden in [
    'LayoutElement',
    'preferredHeight',
    'Canvas.ForceUpdateCanvases',
    'StartCoroutine',
    'Gear Panel',
]:
    if forbidden in patch:
        violations.append(f"compact layout: forbidden global/deferred mechanism returned: {forbidden}")

for token in [
    'internal static bool Suppressed;',
    'if (Suppressed || headwearView == null || headwearView.transform == null) return;',
]:
    if token not in settle:
        violations.append(f"stable fallback suppression contract missing {token!r}")

required_plugin = [
    'CompactFaceHeadBandPresentationPatches compactFaceHeadBandPresentationPatches;',
    'new CompactFaceHeadBandPresentationPatches(Logger.LogInfo, Logger.LogWarning);',
    'compactFaceHeadBandPresentationPatches.Dispose();',
]
for token in required_plugin:
    if token not in plugin:
        violations.append(f"compact layout wiring: missing {token!r}")

if violations:
    raise SystemExit("B&A&HB compact Face/HeadBand gate failed:\n" + "\n".join(violations))

print("B&A&HB compact Face/HeadBand gate: OK (stable fallback suppressed only after compact owner install; local FaceCover footprint; no host-panel mutation/deferred refresh)")
