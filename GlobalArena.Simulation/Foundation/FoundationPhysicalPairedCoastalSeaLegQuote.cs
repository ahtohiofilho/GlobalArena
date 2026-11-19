using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R54 RESEARCH-ONLY quote: the maritime component of ONE route leg is
/// discounted once using the two COAST CROSSINGS ACTUALLY USED at its ends.
///
/// This is not a new property of any sea-sea edge, cannot mutate its owner,
/// and does not yet replace the route search/settlement cost. The authoritative
/// router must become terminal-pair-aware before consuming this function:
/// applying the resulting quote individually to sea edges would give a
/// maritime leg an unjustified multiplicative benefit.
///
/// Capital is an immutable, already-debited opening-cycle snapshot input;
/// no cargo, routes or current-cycle revenues enter this calculation.
/// </summary>
public static class FoundationPhysicalPairedCoastalSeaLegQuote
{
    // Precision of normalized port efficiency; independent of money units.
    private static readonly BigInteger Scale = new(1_000_000_000UL);

    /// <summary>
    /// Returns a positive per-whole-cargo-unit physical COST for exactly one
    /// entire contiguous sea leg. The base argument is the SUM of natural
    /// sea-sea crossing costs (never coast transfer costs). Coast crossings
    /// are separately billed once as ordinary physical transport edges.
    ///
    /// q_i = I_i/B_i; E = sqrt((1+q_A)(1+q_B));
    /// C_sea = ceil(C0 / (1 + beta*sqrt(E-1))).
    /// beta = numerator / denominator, provisional QA policy.
    ///
    /// All roots and operations use exact integer arithmetic on a scaled
    /// representation. Increasing either settled investment cannot increase
    /// the sea quote; zero funding returns exactly C0.
    /// </summary>
    public static ulong Quote(
        ulong entireSeaLegNaturalRate,
        PhysicalTransportEdgeRuntimeRecord embark,
        PhysicalTransportEdgeRuntimeRecord disembark,
        ulong strengthNumerator = 1UL,
        ulong strengthDenominator = 4UL)
    {
        if (entireSeaLegNaturalRate == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(entireSeaLegNaturalRate));
        if (strengthDenominator == 0UL)
            throw new ArgumentOutOfRangeException(nameof(strengthDenominator));
        ArgumentNullException.ThrowIfNull(embark);
        ArgumentNullException.ThrowIfNull(disembark);

        BigInteger efficiencyA = EndpointEfficiency(embark);
        BigInteger efficiencyB = EndpointEfficiency(disembark);
        // Because each efficiency is scaled, their product is scaled^2.
        BigInteger geometricMean = IntegerSquareRoot(
            efficiencyA * efficiencyB);
        BigInteger margin = geometricMean - Scale;
        if (margin < 0)
            throw new InvalidOperationException(
                "Infrastructure cannot diminish natural maritime efficiency.");
        // sqrt(margin/Scale), normalized to Scale units.
        BigInteger improvement = IntegerSquareRoot(margin * Scale);
        BigInteger effectiveDenominator = Scale +
            improvement * strengthNumerator / strengthDenominator;
        BigInteger numerator = new BigInteger(entireSeaLegNaturalRate) * Scale;
        // Ceil: a positive physical sea cost never rounds to free passage.
        BigInteger quote =
            (numerator + effectiveDenominator - BigInteger.One) /
            effectiveDenominator;
        return checked((ulong)quote);
    }

    /// <summary>
    /// Audit a proposed EXACT single sea leg before quoting it. The first
    /// tile must be LAND, the last LAND and all internal tiles WATER under
    /// the same V1 elevation/sea-level rule as the freight seeder.
    /// The ordered edges must form this real fine-graph path: one coast
    /// crossing, one or more all-natural sea-sea edges, one coast crossing.
    /// This is intended for a future terminal-aware pathfinder; it does not
    /// change candidate selection or authoritative settlement in R54.
    /// </summary>
    public static ulong QuoteVerifiedSeaLeg(
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        IReadOnlyList<PhysicalTacticalTileId> routeTiles,
        IReadOnlyList<PhysicalTransportEdgeRuntimeRecord> routeEdges,
        ulong strengthNumerator = 1UL,
        ulong strengthDenominator = 4UL)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(routeTiles);
        ArgumentNullException.ThrowIfNull(routeEdges);
        if (incidence.Refinement.CoarseParameters !=
                world.Request.StrategicParameters ||
            routeTiles.Count < 4 ||
            routeEdges.Count != routeTiles.Count - 1)
            throw new ArgumentException(
                "A single maritime leg needs one real land-to-water transfer, " +
                "at least one sea-sea segment and one water-to-land transfer.");
        var graph = incidence.FineTopology.Edges.ToDictionary(
            edge => edge.Id);
        bool IsLand(PhysicalTacticalTileId tile)
        {
            if (!tile.IsValid ||
                tile.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters)
                throw new ArgumentException(
                    "Foreign or invalid physical tile in maritime route.");
            var index = checked((int)tile.FineStrategicCellId.Value - 1);
            var incident = incidence.TileIncidences[index]
                .IncidentCoarseCellIds;
            long elevation = 0L;
            foreach (var coarse in incident)
                elevation = checked(elevation +
                    world.StrategicPhysicalFields.Elevation.GetRawValue(coarse));
            return elevation / incident.Count >
                world.StrategicPhysicalFields.SeaLevel.RawValue;
        }

        if (!IsLand(routeTiles[0]) ||
            !IsLand(routeTiles[^1]))
            throw new ArgumentException(
                "Maritime terminal endpoints must be on land.");
        for (int i = 1; i < routeTiles.Count - 1; i++)
            if (IsLand(routeTiles[i]))
                throw new ArgumentException(
                    "An internal land tile splits the journey into separate maritime legs.");

        ulong totalNaturalSeaCost = 0UL;
        for (int i = 0; i < routeEdges.Count; i++)
        {
            var record = routeEdges[i];
            if (record is null ||
                !graph.TryGetValue(record.Edge.FineStrategicEdgeId,
                    out var edge) ||
                record.Edge.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                !edge.IncidentCellIds.Contains(
                    routeTiles[i].FineStrategicCellId) ||
                !edge.IncidentCellIds.Contains(
                    routeTiles[i + 1].FineStrategicCellId) ||
                routeTiles[i] == routeTiles[i + 1])
                throw new ArgumentException(
                    "A maritime route must consist of contiguous genuine physical edges.");
            bool sea = i > 0 && i < routeEdges.Count - 1;
            if (sea)
            {
                if (record.CargoMicroAtReferenceLoad != ulong.MaxValue ||
                    record.OwnershipKind !=
                        PhysicalTransportOwnershipKind.NoInfrastructure ||
                    record.InfrastructureOwner.HasValue ||
                    record.ExternalTariffMicroPerWholeCargoUnit != 0UL ||
                    record.CommittedInvestmentMoneyMicro.GetValueOrDefault() != 0UL)
                    throw new ArgumentException(
                        "Open sea cannot contain constructed infrastructure or tolls.");
                totalNaturalSeaCost = checked(
                    totalNaturalSeaCost +
                    record.FreePhysicalCostMicroPerWholeCargoUnit);
            }
            else if (record.CargoMicroAtReferenceLoad == ulong.MaxValue)
                throw new ArgumentException(
                    "A coast transfer must have finite natural capacity.");
        }
        return Quote(totalNaturalSeaCost,
            routeEdges[0], routeEdges[^1],
            strengthNumerator, strengthDenominator);
    }

    private static BigInteger EndpointEfficiency(
        PhysicalTransportEdgeRuntimeRecord edge)
    {
        if (edge.CargoMicroAtReferenceLoad == ulong.MaxValue ||
            edge.NaturalConstructionCostMoneyMicro is not > 0UL)
            throw new InvalidOperationException(
                "A sea-leg endpoint must be a financeable coast crossing, " +
                "not an open sea or an unquoted natural edge.");
        // The incidence of these edges on the actual sea route must be
        // verified separately by the pathfinder before invoking Quote.
        ulong invested = edge.CommittedInvestmentMoneyMicro ?? 0UL;
        return Scale + new BigInteger(invested) * Scale /
            edge.NaturalConstructionCostMoneyMicro.Value;
    }

    private static BigInteger IntegerSquareRoot(BigInteger value)
    {
        if (value.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value.IsZero)
            return BigInteger.Zero;
        BigInteger x = BigInteger.One <<
            ((checked((int)value.GetBitLength()) + 1) / 2);
        while (true)
        {
            BigInteger next = (x + value / x) >> 1;
            if (next >= x)
                return x;
            x = next;
        }
    }
}
