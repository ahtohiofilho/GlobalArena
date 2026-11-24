namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Experimental construction accounting, not a world mutation or an order.
/// Monetary funding and effective infrastructure remain separate concerns.
/// Required effort is an explicit input until the construction policy is agreed.
/// </summary>
public readonly record struct FoundationConstructionProgress
{
    public ulong FundedMoneyMicro { get; }
    public ulong RequiredEffortMicro { get; }
    public ulong CompletedEffortMicro { get; }
    public bool IsComplete => CompletedEffortMicro == RequiredEffortMicro;
    public ulong RemainingEffortMicro => RequiredEffortMicro - CompletedEffortMicro;

    public FoundationConstructionProgress(ulong fundedMoneyMicro,
        ulong requiredEffortMicro, ulong completedEffortMicro = 0)
    {
        if (fundedMoneyMicro == 0)
            throw new ArgumentOutOfRangeException(nameof(fundedMoneyMicro));
        if (requiredEffortMicro == 0)
            throw new ArgumentOutOfRangeException(nameof(requiredEffortMicro));
        if (completedEffortMicro > requiredEffortMicro)
            throw new ArgumentOutOfRangeException(nameof(completedEffortMicro));
        FundedMoneyMicro = fundedMoneyMicro;
        RequiredEffortMicro = requiredEffortMicro;
        CompletedEffortMicro = completedEffortMicro;
    }

    public FoundationConstructionEffortResult Apply(ulong offeredEffortMicro)
    {
        if (FundedMoneyMicro == 0 || RequiredEffortMicro == 0)
            throw new InvalidOperationException("Construction progress must be initialized.");
        ulong used = Math.Min(RemainingEffortMicro, offeredEffortMicro);
        return new FoundationConstructionEffortResult(
            new FoundationConstructionProgress(FundedMoneyMicro,
                RequiredEffortMicro, CompletedEffortMicro + used),
            used, offeredEffortMicro - used);
    }
}

public readonly record struct FoundationConstructionEffortResult(
    FoundationConstructionProgress Progress,
    ulong UsedEffortMicro,
    ulong UnusedEffortMicro);
