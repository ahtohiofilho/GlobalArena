using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation.Foundation;

/// <summary>One passage quote for the embarkation service, not a commercial
/// toll, a provider credit, an extra market or an immediate treasury debit.</summary>
internal static class FoundationNavalFareQuote
{
    internal const ulong CargoMicroPerWholeCargo = 1_000_000UL;

    internal static BigInteger CalculateScaled(FoundationNavalVoyage voyage, ulong cargoMicroPerUnit)
    {
        ArgumentNullException.ThrowIfNull(voyage);
        if (cargoMicroPerUnit == 0UL || voyage.EmbarkationFreightMicroPerWholeCargo is not { } rate ||
            rate == 0UL || voyage.PaidDenominator == 0UL || voyage.PaidNumerator > voyage.PaidDenominator)
            throw new InvalidOperationException("Passage requires a priced voyage and valid explicit cargo equivalence.");
        // Single floor at the treasury's exact scaled-money resolution.
        // BigInteger avoids overflow even for large production cost scales.
        return (BigInteger)rate * cargoMicroPerUnit * voyage.PaidNumerator *
            GlobalArenaMoneyScale.ExactScaledPerMoneyMicro /
            ((BigInteger)CargoMicroPerWholeCargo * voyage.PaidDenominator);
    }
}
