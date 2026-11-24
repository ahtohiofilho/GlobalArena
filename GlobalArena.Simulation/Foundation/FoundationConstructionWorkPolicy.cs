using System.Numerics;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Money and effective work are independent requirements. Both are expressed
/// in micro units. The explicit rate is money micro per whole unit of Work.
/// No civilization productivity multiplier, terrain multiplier or default
/// calibration is introduced here.
/// </summary>
public static class FoundationConstructionWorkPolicy
{
    public const ulong WorkMicroPerWholeUnit = 1_000_000UL;
    public const ulong NeutralIndividualProductivityMicro = 1_000_000UL;

    public static ulong RequiredWorkMicro(ulong investmentMoneyMicro,
        ulong moneyMicroPerWholeWork)
    {
        if (investmentMoneyMicro == 0)
            throw new ArgumentOutOfRangeException(nameof(investmentMoneyMicro));
        if (moneyMicroPerWholeWork == 0)
            throw new ArgumentOutOfRangeException(nameof(moneyMicroPerWholeWork));

        // Round UP once per project: positive funding never yields a free
        // zero-work project. BigInteger prevents intermediate overflow.
        BigInteger numerator = (BigInteger)investmentMoneyMicro * WorkMicroPerWholeUnit;
        BigInteger quotient = BigInteger.DivRem(numerator, moneyMicroPerWholeWork, out var remainder);
        if (!remainder.IsZero) quotient++;
        return checked((ulong)quotient);
    }

    public static FoundationConstructionProgress Quote(ulong investmentMoneyMicro,
        ulong moneyMicroPerWholeWork) =>
        new(investmentMoneyMicro, RequiredWorkMicro(investmentMoneyMicro, moneyMicroPerWholeWork));
}
