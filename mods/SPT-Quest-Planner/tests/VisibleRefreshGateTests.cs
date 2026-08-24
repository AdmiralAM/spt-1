using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class VisibleRefreshGateTests
{
    [Fact]
    public void OpenDefersCadenceUntilIntervalElapses()
    {
        VisibleRefreshGate gate = new(15f);
        gate.Open(100f);

        Assert.False(gate.ShouldRefresh(100f));
        Assert.False(gate.ShouldRefresh(114.999f));
        Assert.True(gate.ShouldRefresh(115f));
    }

    [Fact]
    public void SuccessfulCadenceCheckRearmsFromCurrentTime()
    {
        VisibleRefreshGate gate = new(15f);
        gate.Open(10f);

        Assert.True(gate.ShouldRefresh(25f));
        Assert.False(gate.ShouldRefresh(39.999f));
        Assert.True(gate.ShouldRefresh(40f));
    }

    [Fact]
    public void CloseSuppressesRefreshUntilReopened()
    {
        VisibleRefreshGate gate = new(15f);
        gate.Open(0f);
        gate.Close();

        Assert.False(gate.ShouldRefresh(1000f));

        gate.Open(1000f);
        Assert.False(gate.ShouldRefresh(1014.999f));
        Assert.True(gate.ShouldRefresh(1015f));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void InvalidIntervalIsRejected(float interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisibleRefreshGate(interval));
    }
}
