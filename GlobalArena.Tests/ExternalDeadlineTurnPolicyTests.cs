using GlobalArena.Kernel;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class ExternalDeadlineTurnPolicyTests
{
    [Fact]
    public void NullInputIsRejected()
    {
        var policy =
            new ExternalDeadlineTurnPolicy();

        Assert.Throws<ArgumentNullException>(
            () => policy.ShouldClose(
                null!));
    }

    [Fact]
    public void DeadlineNotReachedKeepsTurnOpenRegardlessOfReadiness()
    {
        var policy =
            new ExternalDeadlineTurnPolicy();

        var notReady =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(1UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: false));

        var ready =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(1UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false));

        Assert.False(
            notReady);

        Assert.False(
            ready);
    }

    [Fact]
    public void DeadlineReachedClosesTurnRegardlessOfReadiness()
    {
        var policy =
            new ExternalDeadlineTurnPolicy();

        var notReady =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(2UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true));

        var ready =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(2UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: true));

        Assert.True(
            notReady);

        Assert.True(
            ready);
    }
}
