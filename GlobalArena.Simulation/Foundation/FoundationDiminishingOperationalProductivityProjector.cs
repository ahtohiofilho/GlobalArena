using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit operational parameters, separate for land and port. The base
/// specifies physical service per whole unit of effort WITHOUT investments.
/// ExtraProductivityBasisPoints specifies the limiting incremental service
/// productivity at extreme capital, not a default freight or cargo limit.
/// </summary>
public sealed record FoundationOperationalProductivityBand(
    ulong NaturalServiceMicroPerWholeEffort,
    uint ExtraProductivityBasisPoints);

/// <summary>
/// Caller-owned experimental calibration. No historical constants, market
/// demand or cargo traffic enter this policy. LAND and PORT cannot silently
/// use the same rate without the caller explicitly supplying both bands.
/// </summary>
public sealed class FoundationOperationalProductivityPolicy
{
    public FoundationOperationalProductivityBand Land { get; }
    public FoundationOperationalProductivityBand Port { get; }

    public FoundationOperationalProductivityPolicy(
        FoundationOperationalProductivityBand land,
        FoundationOperationalProductivityBand port)
    {
        if (land is null || port is null ||
            land.NaturalServiceMicroPerWholeEffort == 0UL ||
            port.NaturalServiceMicroPerWholeEffort == 0UL ||
            land.ExtraProductivityBasisPoints == 0U ||
            port.ExtraProductivityBasisPoints == 0U)
            throw new ArgumentException(
                "Each operational class requires an explicitly positive " +
                "baseline and marginal-capital productivity headroom.");
        Land = land;
        Port = port;
    }
}

/// <summary>
/// R152 conversion from SETTLED monetary investment to an operational service
/// factor. The monetary quote is a denominator (difficulty), never productive
/// natural capital. Work distribution (R151) and service PER UNIT of work
/// are intentionally distinct effects; do not multiply a third time.
/// 
/// Factor = Base * [1 + (GainBps / 10000) * K / (Q + K)].
/// This is increasing, bounded and strictly concave before integer rounding.
/// Its coefficients are externally supplied; zero work still yields zero
/// service when this factor is consumed by R150.
/// </summary>
public static class FoundationDiminishingOperationalProductivityProjector
{
    public static FoundationOperationalEdgeProductivity Project(
        PhysicalTransportEdgeRuntimeRecord physical,
        FoundationOperationalWorkKind kind,
        FoundationOperationalProductivityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(physical);
        ArgumentNullException.ThrowIfNull(policy);
        // Reuse the physical eligibility and quote safeguards of R151.
        _ = FoundationOperationalCapitalWeightProjector.Project(physical, kind);
        var band = kind switch
        {
            FoundationOperationalWorkKind.Land => policy.Land,
            FoundationOperationalWorkKind.Port => policy.Port,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        BigInteger invested = physical.CommittedInvestmentMoneyMicro.GetValueOrDefault();
        BigInteger quote = physical.NaturalConstructionCostMoneyMicro!.Value;
        BigInteger gain = (BigInteger)band.NaturalServiceMicroPerWholeEffort *
            band.ExtraProductivityBasisPoints * invested /
            (10_000UL * (quote + invested));
        BigInteger effective = band.NaturalServiceMicroPerWholeEffort + gain;
        if (effective > ulong.MaxValue)
            throw new OverflowException(
                "Effective operational productivity exceeds micro-unit range.");
        return new FoundationOperationalEdgeProductivity(
            physical.Edge, kind, checked((ulong)effective));
    }
}
