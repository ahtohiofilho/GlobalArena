using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Real coastal crossings and sea-only path from the Goldberg world, with
/// capital settled into an immutable authoritative treasury. These checks
/// apply the new sea benefit ONCE to the total sea component, not per edge;
/// the kernel and live pathfinder remain unchanged until a terminal-aware
/// candidate search is integrated and separately homologated.
/// </summary>
public sealed class FoundationPhysicalPairedCoastalSeaLegQuoteTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalPairedCoastalSeaLegQuoteTests(
        ITestOutputHelper output) => _output = output;

    [Fact]
    public void RealTwoPortSeaLegRespondsMonotonicallyToExactlySettledCapital()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var immutableHash = host.StateHash;
        var immutableSnapshot = host.Snapshot;
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(
                    1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(
                    5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(
                    25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var physical = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var land = host.Incidence.TileIncidences.Select(t =>
        {
            long sum = t.IncidentCoarseCellIds.Aggregate(0L,
                (total, id) => checked(total +
                    physical.Elevation.GetRawValue(id)));
            return sum / t.IncidentCoarseCellIds.Count >
                physical.SeaLevel.RawValue;
        }).ToArray();
        var all = seed.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        var adjacency = new Dictionary<StrategicCellId,
            List<(StrategicCellId Other, StrategicEdgeId Edge)>>();
        foreach (var edge in host.Incidence.FineTopology.Edges)
        {
            if (all[edge.Id].CargoMicroAtReferenceLoad != ulong.MaxValue)
                continue;
            var a = edge.IncidentCellIds[0];
            var b = edge.IncidentCellIds[1];
            Assert.False(land[checked((int)a.Value - 1)]);
            Assert.False(land[checked((int)b.Value - 1)]);
            if (!adjacency.TryGetValue(a, out var ar))
                adjacency[a] = ar = new();
            if (!adjacency.TryGetValue(b, out var br))
                adjacency[b] = br = new();
            ar.Add((b, edge.Id));
            br.Add((a, edge.Id));
        }
        var coastal = host.Incidence.FineTopology.Edges
            .Where(x => all[x.Id].CargoMicroAtReferenceLoad == 500_000UL)
            .Select(x => new
            {
                Crossing = all[x.Id],
                SeaTile = x.IncidentCellIds.Single(tile =>
                    !land[checked((int)tile.Value - 1)])
            }).ToArray();
        Assert.True(coastal.Length > 1 && adjacency.Count > 0);

        var first = coastal.First(x => adjacency.ContainsKey(x.SeaTile));
        // A real sea-sea segment, with two distinct actual coastal transfer
        // edges at its ends, is the smallest fully verified maritime journey.
        var pair = (
            from via in adjacency[first.SeaTile]
            from destination in coastal
            where destination.SeaTile == via.Other &&
                destination.Crossing.Edge != first.Crossing.Edge
            select new { Destination = destination, SeaEdge = via.Edge })
            .FirstOrDefault();
        Assert.NotNull(pair);
        var other = pair!.Destination.Crossing;
        ulong seaLeg = all[pair.SeaEdge]
            .FreePhysicalCostMicroPerWholeCargoUnit;
        Assert.True(seaLeg > 0UL);
        var world = immutableSnapshot
            .DeployInitialPhysicalTransport(
                seed.Transport, host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (Red, 1_000_000_000UL), (Blue, 1_000_000_000UL)
                }));
        var start = world.Transport.Edges.Single(x =>
            x.Edge == first.Crossing.Edge);
        var end = world.Transport.Edges.Single(x => x.Edge == other.Edge);
        ulong quote0 = FoundationPhysicalPairedCoastalSeaLegQuote
            .Quote(seaLeg, start, end);
        Assert.Equal(seaLeg, quote0);

        ulong aMoney = checked(start.NaturalConstructionCostMoneyMicro!.Value * 4UL);
        ulong bMoney = checked(end.NaturalConstructionCostMoneyMicro!.Value * 4UL);
        var fundedOne = world.RecordMonetaryInfrastructureInvestment(
            Red, start.Edge, aMoney);
        var fundedTwo = fundedOne.RecordMonetaryInfrastructureInvestment(
            Blue, end.Edge, bMoney);
        var fundedA = fundedOne.Transport.Edges.Single(x =>
            x.Edge == start.Edge);
        var fundedB = fundedTwo.Transport.Edges.Single(x =>
            x.Edge == end.Edge);
        ulong quoteOne = FoundationPhysicalPairedCoastalSeaLegQuote
            .Quote(seaLeg, fundedA, end);
        ulong quoteBoth = FoundationPhysicalPairedCoastalSeaLegQuote
            .Quote(seaLeg, fundedA, fundedB);
        Assert.True(quoteBoth < quoteOne && quoteOne < quote0);
        Assert.Equal(quoteBoth,
            FoundationPhysicalPairedCoastalSeaLegQuote
                .Quote(seaLeg, fundedB, fundedA));
        Assert.Equal(seaLeg, FoundationPhysicalPairedCoastalSeaLegQuote
            .Quote(seaLeg, start, end, strengthNumerator: 0UL));
        Assert.Equal(world.Treasury.Get(Red).CashScaled -
            GlobalArenaMoneyScale.FromMoneyMicro(aMoney),
            fundedTwo.Treasury.Get(Red).CashScaled);
        Assert.Equal(world.Treasury.Get(Blue).CashScaled -
            GlobalArenaMoneyScale.FromMoneyMicro(bMoney),
            fundedTwo.Treasury.Get(Blue).CashScaled);
        Assert.Equal(ulong.MaxValue, seed.Transport.Edges.First(x =>
            x.CargoMicroAtReferenceLoad == ulong.MaxValue)
            .CargoMicroAtReferenceLoad);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalPairedCoastalSeaLegQuote.Quote(
                seaLeg, all[pair.SeaEdge], start));
        Assert.Equal(immutableHash, host.StateHash);
        Assert.Same(immutableSnapshot, host.Snapshot);

        _output.WriteLine(
            $"GA_R54 paired_coast=A,B real_sea_edge={pair.SeaEdge.Value} " +
            $"natural_sea_rate={quote0} one_terminal={quoteOne} " +
            $"both_terminals={quoteBoth} natural_coast_total=" +
            $"{start.FreePhysicalCostMicroPerWholeCargoUnit +
              end.FreePhysicalCostMicroPerWholeCargoUnit} " +
            $"title_A={fundedA.InfrastructureOwner} " +
            $"title_B={fundedB.InfrastructureOwner} " +
            "sea_infrastructure=false dynamic_turn_feedback=false");
    }

    [Fact]
    public void EntireMaritimeLegIsDiscountedOnceWithoutCompoundingPerSeaEdge()
    {
        var example = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            example.Session.GeneratedWorld, example.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(
                    1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(
                    5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(
                    25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var port = seed.Transport.Edges.First(x =>
            x.CargoMicroAtReferenceLoad == 500_000UL);
        var built = port with
        {
            CommittedInvestmentMoneyMicro =
                port.NaturalConstructionCostMoneyMicro!.Value * 4UL,
            OwnershipKind = PhysicalTransportOwnershipKind.Owned,
            InfrastructureOwner = Red
        };
        // The rule takes the cumulative sea-base cost (five edges, once)
        // and doesn't apply a per-edge multiplier on top of it. Splitting
        // a geometric leg into more tiles must not multiply a port bonus.
        ulong whole = FoundationPhysicalPairedCoastalSeaLegQuote.Quote(
            125_000UL, built, built);
        ulong half = FoundationPhysicalPairedCoastalSeaLegQuote.Quote(
            50_000UL, built, built);
        ulong rest = FoundationPhysicalPairedCoastalSeaLegQuote.Quote(
            75_000UL, built, built);
        Assert.True(Math.Abs((long)whole - (long)(half + rest)) <= 1L,
            "Only one rounded total-leg quote should be used, not per-edge rounding.");
        Assert.True(whole > 0UL && whole < 125_000UL);
        Assert.Equal(whole,
            FoundationPhysicalPairedCoastalSeaLegQuote.Quote(
                125_000UL, built, built));
    }
}
