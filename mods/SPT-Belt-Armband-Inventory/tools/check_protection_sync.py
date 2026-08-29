from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
client = (ROOT / "src" / "ProtectionSettingsSync.cs").read_text(encoding="utf-8-sig")
contract = (ROOT / "src" / "WearableProtectionContract.cs").read_text(encoding="utf-8-sig")
server = (ROOT / "server" / "WearableProtectionRuntime.cs").read_text(encoding="utf-8-sig")
violations = []

for token in [
    "string response = postJson(WearableProtectionContract.Route, payload);",
    "if (!WearableProtectionContract.IsAcknowledgement(response, payload))",
    "server acknowledgement did not match the applied protection snapshot",
    "synced and acknowledged",
]:
    if token not in client:
        violations.append(f"client protection sync missing acknowledgement token {token!r}")

for token in [
    "internal static bool IsAcknowledgement(string response, string expectedPayload)",
    "string.Equals(response.Trim(), expectedPayload, System.StringComparison.Ordinal)",
]:
    if token not in contract:
        violations.append(f"protection wire contract missing token {token!r}")

for token in [
    "WearableProtectionSnapshot snapshot = WearableProtectionRuntime.Apply(info);",
    "string response = WearableProtectionContract.Encode(",
    "snapshot.ArmBandProtected",
    "snapshot.BeltProtected",
    "snapshot.HeadBandProtected",
    "return ValueTask.FromResult(response);",
]:
    if token not in server:
        violations.append(f"server protection acknowledgement missing token {token!r}")

if "jsonUtil.Serialize(snapshot)" in server:
    violations.append("server protection acknowledgement must use the shared deterministic wire contract, not serializer-dependent property casing")
if "postJson(WearableProtectionContract.Route, payload);" in client and "string response = postJson" not in client:
    violations.append("client must not discard the protection POST response")

if violations:
    raise SystemExit("B&A&HB protection-sync gate failed:\n" + "\n".join(violations))

print("B&A&HB protection-sync gate: OK (client reports sync only after exact server acknowledgement of the applied per-family snapshot)")
