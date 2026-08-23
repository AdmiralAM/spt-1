using System;

namespace SPTPause
{
    public sealed class PauseStateMachine
    {
        readonly Func<long> timestamp;
        readonly double timestampFrequency;
        long pausedAt;

        public PauseStateMachine(Func<long> timestamp, double timestampFrequency)
        {
            this.timestamp = timestamp ?? throw new ArgumentNullException(nameof(timestamp));
            if (timestampFrequency <= 0d) throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
            this.timestampFrequency = timestampFrequency;
        }

        public bool IsPaused { get; private set; }

        public bool TryPause(Action enter)
        {
            if (IsPaused) return false;
            if (enter == null) throw new ArgumentNullException(nameof(enter));

            enter();
            pausedAt = timestamp();
            IsPaused = true;
            return true;
        }

        public bool TryResume(Action<TimeSpan> restore)
        {
            if (!IsPaused) return false;
            if (restore == null) throw new ArgumentNullException(nameof(restore));

            long endedAt = timestamp();
            double seconds = Math.Max(0d, (endedAt - pausedAt) / timestampFrequency);
            IsPaused = false;
            restore(TimeSpan.FromSeconds(seconds));
            return true;
        }
    }
}
