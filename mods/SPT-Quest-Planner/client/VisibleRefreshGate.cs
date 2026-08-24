using System;

namespace SPTQuestPlanner.Client
{
    internal sealed class VisibleRefreshGate
    {
        private readonly float intervalSeconds;
        private float nextRefreshAt;
        private bool armed;

        public VisibleRefreshGate(float intervalSeconds)
        {
            if (intervalSeconds <= 0f || float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds))
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));

            this.intervalSeconds = intervalSeconds;
        }

        public void Open(float now)
        {
            armed = true;
            nextRefreshAt = now + intervalSeconds;
        }

        public void Close()
        {
            armed = false;
            nextRefreshAt = 0f;
        }

        public bool ShouldRefresh(float now)
        {
            if (!armed || now < nextRefreshAt) return false;

            nextRefreshAt = now + intervalSeconds;
            return true;
        }
    }
}
