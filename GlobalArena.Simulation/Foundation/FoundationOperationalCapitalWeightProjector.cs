using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R151 canonical operational WEIGHT calibration, not an expenditure.
/// Every eligible natural crossing begins with the same positive baseline.
/// Committed financial capital is divided by that crossing's explicit
/// original construction quote; high construction COST is not incorrectly
/// treated as high operating INFRASTRUCTURE.
/// </summary>
public static class FoundationOperationalCapitalWeightProjector
{
    /// <summary>
    /// A unit of effective natural infrastructure is represented in micro
    /// weights. One complete construction-quote-equivalent of committed
    /// investment adds one further unit. The quote converts MONEY to a
    /// dimensionless infrastructure ratio; it is never itself a weight.
    /// </summary>
    public const ulong OneNaturalInfrastructureMicro = 1_000_000UL;

    public static FoundationOperationalWorkEdge Project(
        PhysicalTransportEdgeRuntimeRecord physical,
        FoundationOperationalWorkKind kind)
    {
        ArgumentNullException.ThrowIfNull(physical);
        if (!physical.Edge.IsValid ||
            (kind != FoundationOperationalWorkKind.Land &&
             kind != FoundationOperationalWorkKind.Port) ||
            physical.NaturalConstructionCostMoneyMicro is not > 0UL ||
            physical.CargoMicroAtReferenceLoad == ulong.MaxValue)
            throw new ArgumentException(
                "Operational weights require a finite physical land/port " +
                "crossing with an explicit positive original construction quote.",
                nameof(physical));

        ulong quote = physical.NaturalConstructionCostMoneyMicro.Value;
        ulong confirmed = physical.CommittedInvestmentMoneyMicro.GetValueOrDefault();
        // BigInteger prevents multiplication overflow, and flooring is
        // deliberate: no hidden upward subsidy to microscopic investments.
        BigInteger normalized = (BigInteger)confirmed *
            OneNaturalInfrastructureMicro / quote;
        if (normalized > ulong.MaxValue)
            throw new OverflowException(
                "Operational capital weight exceeds the supported micro range.");
        return new FoundationOperationalWorkEdge(
            physical.Edge, kind, OneNaturalInfrastructureMicro,
            checked((ulong)normalized));
    }
}
