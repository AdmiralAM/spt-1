using System;

namespace SPTItemIntelligence
{
    public static class Phase3Tests
    {
        public static int Run()
        {
            int assertions = 0;
            Expect(RequirementDataContract.SchemaVersion == 1, "schema version is stable", ref assertions);
            Expect(RequirementDataContract.SnapshotRoute == "/spt-item-intelligence/v1/snapshot", "snapshot route is stable", ref assertions);

            object profile = new object();
            object quests = new object();
            object hideout = new object();
            RequirementDataEnvelope ready = new RequirementDataEnvelope(123, profile, quests, hideout);
            Expect(ready.schemaVersion == 1, "envelope carries schema version", ref assertions);
            Expect(ready.generatedAtUnixSeconds == 123, "envelope carries generation time", ref assertions);
            Expect(ready.profileReady, "profile readiness is explicit", ref assertions);
            Expect(object.ReferenceEquals(ready.profile, profile), "profile payload is preserved", ref assertions);
            Expect(object.ReferenceEquals(ready.quests, quests), "quest payload is preserved", ref assertions);
            Expect(object.ReferenceEquals(ready.hideout, hideout), "hideout payload is preserved", ref assertions);

            RequirementDataEnvelope waiting = new RequirementDataEnvelope(-1, null, quests, hideout);
            Expect(waiting.generatedAtUnixSeconds == 0, "negative timestamps are clamped", ref assertions);
            Expect(!waiting.profileReady && waiting.profile == null, "missing profile is retryable state", ref assertions);

            bool questsRejected = false;
            try { new RequirementDataEnvelope(0, null, null, hideout); }
            catch (ArgumentNullException) { questsRejected = true; }
            Expect(questsRejected, "missing quest table is rejected", ref assertions);

            bool hideoutRejected = false;
            try { new RequirementDataEnvelope(0, null, quests, null); }
            catch (ArgumentNullException) { hideoutRejected = true; }
            Expect(hideoutRejected, "missing hideout table is rejected", ref assertions);

            return assertions;
        }

        static void Expect(bool condition, string message, ref int assertions)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
