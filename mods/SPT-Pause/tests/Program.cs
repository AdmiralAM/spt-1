using System;
using SPTPause;

static class Program
{
    static int assertions;

    static void Main()
    {
        StateMachineIsTransactional();
        DisabledSettingNeverTrapsAnActivePause();
        PausedInputIsSuppressedUntilToggle();
        NamedTimerAnchorsShiftExactlyOnce();
        TimerPanelDateFieldShifts();
        TimeOfDayRealtimeAnchorShifts();
        Console.WriteLine("Pause Admiral v1: " + assertions + " assertions passed.");
    }

    static void DisabledSettingNeverTrapsAnActivePause()
    {
        Expect(!PauseInputPolicy.AcceptToggle(false, true, false), "no shortcut means no toggle");
        Expect(!PauseInputPolicy.AcceptToggle(true, false, false), "disabled setting blocks a new pause");
        Expect(PauseInputPolicy.AcceptToggle(true, true, false), "enabled setting allows a new pause");
        Expect(PauseInputPolicy.AcceptToggle(true, false, true), "disabled setting still allows resume");
        Expect(PauseInputPolicy.AcceptToggle(true, true, true), "enabled setting allows resume");
    }

    static void PausedInputIsSuppressedUntilToggle()
    {
        Expect(!PauseInputPolicy.SuppressGameplayInput(false, false), "gameplay input is untouched while running");
        Expect(PauseInputPolicy.SuppressGameplayInput(true, false), "gameplay input is suppressed while paused");
        Expect(!PauseInputPolicy.SuppressGameplayInput(true, true), "pause toggle is allowed through to resume");
    }

    static void StateMachineIsTransactional()
    {
        long ticks = 100;
        int enters = 0;
        int restores = 0;
        TimeSpan restoredDuration = TimeSpan.Zero;
        PauseStateMachine state = new PauseStateMachine(() => ticks, 10d);

        Expect(state.TryPause(() => enters++), "first pause succeeds");
        Expect(state.IsPaused, "state becomes paused");
        Expect(!state.TryPause(() => enters++), "duplicate pause is ignored");
        ticks = 135;
        Expect(state.TryResume(duration => { restores++; restoredDuration = duration; }), "resume succeeds");
        Expect(!state.IsPaused, "state becomes resumed");
        Expect(!state.TryResume(duration => restores++), "duplicate resume is ignored");
        Expect(enters == 1 && restores == 1, "enter and restore run once");
        Expect(restoredDuration == TimeSpan.FromSeconds(3.5d), "monotonic duration is preserved");
    }

    static void NamedTimerAnchorsShiftExactlyOnce()
    {
        DateTime start = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        FakeRaidTimer timer = new FakeRaidTimer(start, start.AddMinutes(40));
        ReflectionClockAnchors anchors = ReflectionClockAnchors.CaptureNamedProperties(timer, "StartDateTime", "EscapeDateTime");
        Expect(anchors.Count == 2, "renamed timer fields are resolved through semantic properties");
        anchors.Shift(TimeSpan.FromSeconds(12));
        anchors.Shift(TimeSpan.FromSeconds(12));
        Expect(timer.StartDateTime == start.AddSeconds(12), "raid start anchor shifts once");
        Expect(timer.EscapeDateTime == start.AddMinutes(40).AddSeconds(12), "raid escape anchor shifts once");
    }

    static void TimerPanelDateFieldShifts()
    {
        DateTime exit = new DateTime(2026, 8, 23, 11, 0, 0, DateTimeKind.Utc);
        FakeTimerPanel panel = new FakeTimerPanel(exit);
        ReflectionClockAnchors anchors = ReflectionClockAnchors.CaptureDateTimeFields(panel, typeof(FakeTimerPanel));
        Expect(anchors.Count == 1, "timer panel date anchor is captured");
        anchors.Shift(TimeSpan.FromSeconds(7));
        Expect(panel.Value == exit.AddSeconds(7), "timer panel deadline excludes paused time");
    }

    static void TimeOfDayRealtimeAnchorShifts()
    {
        FakeGameDateTime gameDateTime = new FakeGameDateTime(42f);
        ReflectionClockAnchors anchors = ReflectionClockAnchors.CaptureFloatField(gameDateTime, "_realtimeSinceStartup");
        Expect(anchors.Count == 1, "time-of-day realtime anchor is captured");
        anchors.Shift(TimeSpan.FromSeconds(5.25d));
        Expect(Math.Abs(gameDateTime.Value - 47.25f) < 0.001f, "time of day excludes paused seconds");
    }

    static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
    }

    sealed class FakeRaidTimer
    {
        DateTime? alpha;
        DateTime? beta;

        internal FakeRaidTimer(DateTime start, DateTime escape)
        {
            alpha = start;
            beta = escape;
        }

        public DateTime? StartDateTime { get { return alpha; } }
        public DateTime? EscapeDateTime { get { return beta; } }
    }

    sealed class FakeTimerPanel
    {
        DateTime deadline;

        internal FakeTimerPanel(DateTime deadline)
        {
            this.deadline = deadline;
        }

        internal DateTime Value { get { return deadline; } }
    }

    sealed class FakeGameDateTime
    {
        float _realtimeSinceStartup;

        internal FakeGameDateTime(float value) { _realtimeSinceStartup = value; }
        internal float Value { get { return _realtimeSinceStartup; } }
    }
}
