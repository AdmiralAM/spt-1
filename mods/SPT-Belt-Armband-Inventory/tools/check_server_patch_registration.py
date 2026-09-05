from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "server" / "ServerMod.cs"
INSURANCE = ROOT / "server" / "Patches" / "HandleInsuredItemLostEventPatch.cs"
text = SOURCE.read_text(encoding="utf-8-sig")
insurance = INSURANCE.read_text(encoding="utf-8-sig")
violations = []

required = [
    "IRuntimePatch[] deathMatches = patches.Where(patch => patch is IsItemKeptAfterDeathPatch).Take(2).ToArray();",
    "IRuntimePatch[] insuranceMatches = patches.Where(patch => patch is HandleInsuredItemLostEventPatch).Take(2).ToArray();",
    "if (deathMatches.Length != 1 || insuranceMatches.Length != 1)",
    "death/insurance protection remains disabled as one atomic feature",
    "IRuntimePatch deathPatch = deathMatches[0];",
    "IRuntimePatch insurancePatch = insuranceMatches[0];",
    "deathPatch.Enable();",
    "insurancePatch.Enable();",
    "enabled[i].Disable();",
]
for token in required:
    if token not in text:
        violations.append(f"missing server protection registration token: {token!r}")

if "SingleOrDefault(patch => patch is IsItemKeptAfterDeathPatch)" in text:
    violations.append("death patch resolution must not throw on duplicate DI bindings")
if "SingleOrDefault(patch => patch is HandleInsuredItemLostEventPatch)" in text:
    violations.append("insurance patch resolution must not throw on duplicate DI bindings")

death_resolve = text.find("IRuntimePatch[] deathMatches")
insurance_resolve = text.find("IRuntimePatch[] insuranceMatches")
unique_gate = text.find("if (deathMatches.Length != 1 || insuranceMatches.Length != 1)")
first_enable = text.find("deathPatch.Enable();")
if min(death_resolve, insurance_resolve, unique_gate, first_enable) < 0 or not (
    death_resolve < unique_gate < first_enable and insurance_resolve < unique_gate < first_enable
):
    violations.append("both protection patch types must be bounded and uniquely proven before any Enable call")

for token in [
    "BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly",
    "method.ReturnType != typeof(void)",
    "parameters.Length != 4",
    "parameters[0].ParameterType != typeof(MongoId)",
    "parameters[1].ParameterType != typeof(PmcData)",
    "parameters[2].ParameterType != typeof(EndLocalRaidRequestData)",
    "parameters[3].ParameterType != typeof(string)",
    "Multiple exact LocationLifecycleService.HandleInsuredItemLostEvent(MongoId, PmcData, EndLocalRaidRequestData, string) methods found",
]:
    if token not in insurance:
        violations.append(f"insurance target binding missing exact SPT 4.1.3 signature token: {token!r}")

if violations:
    raise SystemExit("B&A&HB server-patch registration gate failed:\n" + "\n".join(violations))

print("B&A&HB server-patch registration gate: OK (death/insurance DI bindings bounded-unique before enable; rollback atomic; insurance target exact to SPT 4.1.3 four-parameter signature)")
