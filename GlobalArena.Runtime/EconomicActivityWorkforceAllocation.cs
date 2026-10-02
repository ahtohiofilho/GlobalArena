namespace GlobalArena.Runtime;

public sealed class EconomicActivityWorkforceAllocation
{
    public EconomicActivityKind Kind { get; }

    public ulong Workforce { get; }

    public EconomicActivityWorkforceAllocation(
        EconomicActivityKind kind,
        ulong workforce)
    {
        if (!Enum.IsDefined(
            typeof(EconomicActivityKind),
            kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "Economic activity kind must be Agriculture, Mining or TradeLogistics.");
        }

        if (workforce == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workforce),
                "Economic activity workforce allocation must be positive.");
        }

        Kind = kind;
        Workforce = workforce;
    }
}