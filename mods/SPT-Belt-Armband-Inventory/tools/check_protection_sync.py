from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
client = (ROOT / "src" / "ProtectionSettingsSync.cs").read_text(encoding="utf-8-sig")
contract = (ROOT / "src" / "WearableProtectionContract.cs").read_text(encoding="utf-8-sig")
server = (ROOT / "server" / "WearableProtectionRuntime.cs").read_text(encoding="utf-8-sig")
plugin = (ROOT / "src" / "Plugin.cs").read_text(encoding="utf-8-sig")
violations = []

for token in [
    "bool subscribed;",
    "EnsureSubscribed();",
    "void EnsureSubscribed()",
    "if (subscribed) return;",
    "subscribed = true;",
    "if (subscribed)",
    "subscribed = false;",
    "string response = postJson(WearableProtectionContract.Route, payload);",
    "if (!WearableProtectionContract.IsAcknowledgement(response, payload))",
    "server acknowledgement did not match the applied protection snapshot",
    "synced and acknowledged",
]:
    if token not in client:
        violations.append(f"client protection sync missing lifecycle/acknowledgement token {token!r}")

constructor_start = client.find("internal ProtectionSettingsSync(")
try_sync_start = client.find("internal bool TryBindAndSync()", constructor_start)
constructor = client[constructor_start:try_sync_start] if constructor_start >= 0 and try_sync_start > constructor_start else ""
for token in ("SettingChanged += OnSettingChanged", "TryBindTransport()", "Sync()"):
    if token in constructor:
        violations.append(f"ProtectionSettingsSync constructor must remain passive before mandatory client core is live ({token})")

try_sync_end = client.find("void EnsureSubscribed()", try_sync_start)
try_sync = client[try_sync_start:try_sync_end] if try_sync_start >= 0 and try_sync_end > try_sync_start else ""
if try_sync.find("EnsureSubscribed();") < 0:
    violations.append("TryBindAndSync must activate F12 subscriptions before live-core synchronization")

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

# Plugin may construct the passive settings owner early, but the only startup
# activation path must remain the bounded sync coroutine after mandatory core
# installation. Early fatal returns therefore cannot subscribe or POST.
if "protectionSyncPump = StartCoroutine(SyncProtectionSettingsBounded());" not in plugin:
    violations.append("Plugin startup must retain the bounded live-core protection sync activation")
if plugin.count("TryBindAndSync()") != 1:
    violations.append("Plugin must have exactly one startup TryBindAndSync activation site")

if violations:
    raise SystemExit("B&A&HB protection-sync gate failed:\n" + "\n".join(violations))

print("B&A&HB protection-sync gate: OK (settings owner is passive on fatal early startup; subscriptions activate only with bounded live-core sync; client success requires exact server acknowledgement)")
