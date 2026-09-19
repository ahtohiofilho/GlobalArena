using GlobalArena.Kernel;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class ManualReadyTurnPolicyTests
{
    [Fact]
    public void NullInputIsRejected()
    {
        var policy =
            new ManualReadyTurnPolicy();

        Assert.Throws<ArgumentNullException>(
            () => policy.ShouldClose(
                null!));
    }

    [Fact]
    public void NotReadyKeepsTurnOpenRegardlessOfDeadlineSignal()
    {
        var policy =
            new ManualReadyTurnPolicy();

        var withoutDeadline =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(1UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: false));

        var withDeadline =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(1UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true));

        Assert.False(
            withoutDeadline);

        Assert.False(
            withDeadline);
    }

    [Fact]
    public void AllRequiredParticipantsReadyClosesTurnRegardlessOfDeadlineSignal()
    {
        var policy =
            new ManualReadyTurnPolicy();

        var withoutDeadline =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(2UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false));

        var withDeadline =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(2UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: true));

        Assert.True(
            withoutDeadline);

        Assert.True(
            withDeadline);
    }
}
