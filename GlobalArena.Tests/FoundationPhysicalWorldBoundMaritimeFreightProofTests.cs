using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// World-bound R67 integration evidence. No alternative tariff vector,
/// invented ports, currency conversions or manually supplied land route.
/// </summary>
public sealed class FoundationPhysicalWorldBoundMaritimeFreightProofTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private sealed record Fixture(
        FoundationTacticalHumanAiTurnHost Host,
        WorldState Snapshot, PhysicalTacticalTileId Source,
        PhysicalTacticalTileId Destination,
        PhysicalTacticalEdgeId Embark, PhysicalTacticalEdgeId Disembark);

    private static HashSet<ulong> Flood(
        ulong start, Dictionary<ulong, List<ulong>> graph)
    {
        var seen = new HashSet<ulong> { start };
        var queue = new Queue<ulong>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var at))
            if (graph.TryGetValue(at, out var links))
                foreach (var next in links)
                    if (seen.Add(next))
                        queue.Enqueue(next);
        return seen;
    }

    private static Fixture Prepare()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)),
            workersPerCivilization: 3);
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
        var fields = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var isLand = host.Incidence.TileIncidences.Select(tile =>
        {
            long total = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                total = checked(total + fields.Elevation.GetRawValue(coarse));
            return total / tile.IncidentCoarseCellIds.Count >
                fields.SeaLevel.RawValue;
        }).ToArray();
        var roads = new Dictionary<ulong, List<ulong>>();
        var sea = new Dictionary<ulong, List<ulong>>();
        var coasts = new List<(PhysicalTacticalEdgeId Edge,
            ulong Land, ulong Water)>();
        static void Add(Dictionary<ulong, List<ulong>> graph,
            ulong a, ulong b)
        {
            if (!graph.TryGetValue(a, out var left))
                graph[a] = left = new();
            if (!graph.TryGetValue(b, out var right))
                graph[b] = right = new();
            left.Add(b);
            right.Add(a);
        }
        foreach (var edge in host.Incidence.FineTopology.Edges)
        {
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            bool la = isLand[checked((int)a - 1)];
            bool lb = isLand[checked((int)b - 1)];
            if (la && lb) Add(roads, a, b);
            else if (!la && !lb) Add(sea, a, b);
            else coasts.Add((new PhysicalTacticalEdgeId(
                host.Incidence.Refinement.FineParameters, edge.Id),
                la ? a : b, la ? b : a));
        }
        (PhysicalTacticalEdgeId Edge, ulong Land, ulong Water) first =
            default, last = default;
        bool found = false;
        foreach (var a in coasts)
        {
            var landReach = Flood(a.Land, roads);
            var seaReach = Flood(a.Water, sea);
            foreach (var b in coasts)
                if (a.Edge != b.Edge && a.Land != b.Land &&
                    a.Water != b.Water && landReach.Contains(b.Land) &&
                    seaReach.Contains(b.Water))
                {
                    first = a;
                    last = b;
                    found = true;
                    break;
                }
            if (found) break;
        }
        Assert.True(found);
        var fine = host.Incidence.Refinement.FineParameters;
        var snapshot = host.Snapshot.DeployInitialPhysicalTransport(
                seed.Transport, host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (Red, 10_000_000_000UL),
                    (Blue, 10_000_000_000UL)
                }));
        return new Fixture(host, snapshot,
            new PhysicalTacticalTileId(fine, new StrategicCellId(first.Land)),
            new PhysicalTacticalTileId(fine, new StrategicCellId(last.Land)),
            first.Edge, last.Edge);
    }

    [Fact]
    public void WorldSnapshotQuotesAndRealCoastsProduceDeterministicEvidence()
    {
        var f = Prepare();
        var hash = f.Host.StateHash;
        var original = f.Host.Snapshot;
        var world = f.Host.Session.GeneratedWorld;
        var a = FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
            f.Snapshot, world, f.Host.Incidence, Red,
            f.Source, f.Destination, f.Embark, f.Disembark);
        var repeated = FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
            f.Snapshot, world, f.Host.Incidence, Red,
            f.Source, f.Destination, f.Embark, f.Disembark);
        Assert.Equal(a, repeated);
        Assert.True(a.HasRealConnectedSeaLeg);
        Assert.True(a.UsesAuthoritativeWorldTransport);
        Assert.Equal(f.Embark, a.EmbarkEdge);
        Assert.Equal(f.Disembark, a.DisembarkEdge);
        var blue = FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
            f.Snapshot, world, f.Host.Incidence, Blue,
            f.Source, f.Destination, f.Embark, f.Disembark);
        Assert.Equal(a.Bound, blue.Bound); // Natural infrastructure: no tolls.
        Assert.Same(original, f.Host.Snapshot);
        Assert.Equal(hash, f.Host.StateHash);
    }

    [Fact]
    public void FundedCoastUsesAuthoritativeDebitedTransportNotResearchQuote()
    {
        var f = Prepare();
        var world = f.Host.Session.GeneratedWorld;
        var baseline = FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(f.Snapshot, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark);
        var record = f.Snapshot.Transport.Edges.Single(
            x => x.Edge == f.Embark);
        var funded = f.Snapshot.RecordMonetaryInfrastructureInvestment(
            Red, f.Embark,
            checked(record.NaturalConstructionCostMoneyMicro!.Value * 4UL));
        var after = FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(funded, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark);
        Assert.True(after.HasRealConnectedSeaLeg);
        Assert.True(after.Bound.EmbarkCoastRateMicroPerWholeCargoUnit > 0UL);
        Assert.NotEqual(
            f.Snapshot.Transport.Edges.Single(x => x.Edge == f.Embark)
                .CommittedInvestmentMoneyMicro,
            funded.Transport.Edges.Single(x => x.Edge == f.Embark)
                .CommittedInvestmentMoneyMicro);
        // The original research snapshot does not mutate.
        Assert.Equal(baseline, FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(f.Snapshot, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark));
    }

    private static FoundationPhysicalLaggedFreightQuoteState History(
        Fixture fixture, ulong embarkSurcharge = 0UL,
        bool settled = true, bool missingLastEdge = false)
    {
        var (roads, scales) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                fixture.Snapshot, fixture.Host.Incidence);
        var capacity = scales.ToDictionary(x => x.Edge,
            x => x.CargoMicroAtReferenceLoad);
        var history = roads.Select(road =>
            new FoundationPhysicalLaggedFreightEdgeState(
                road.Edge, road.PhysicalCostMicroPerWholeCargoUnit,
                capacity[road.Edge],
                road.Edge == fixture.Embark ? embarkSurcharge : 0UL))
            .ToArray();
        if (missingLastEdge)
            history = history[..^1];
        return new FoundationPhysicalLaggedFreightQuoteState(
            1UL, history, hasSettledProvenance: settled);
    }

    [Fact]
    public void ClosedCycleCongestionIsAppliedExactlyOnceForSameSeller()
    {
        var f = Prepare();
        var world = f.Host.Session.GeneratedWorld;
        var baseline = FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(f.Snapshot, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark);
        var closed = History(f, embarkSurcharge: 17UL);
        var after = FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(f.Snapshot, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark,
                previousClosedCycleQuotes: closed);
        Assert.Equal((UInt128)17UL +
            baseline.Bound.MaritimeLowerBoundMicroPerWholeCargoUnit,
            after.Bound.MaritimeLowerBoundMicroPerWholeCargoUnit);
        Assert.Equal(
            baseline.Bound.EmbarkCoastRateMicroPerWholeCargoUnit + 17UL,
            after.Bound.EmbarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal(baseline.Bound.DisembarkCoastRateMicroPerWholeCargoUnit,
            after.Bound.DisembarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal(after, FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(f.Snapshot, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark, closed));
    }

    [Fact]
    public void UnsettledPreviewAndIncompleteHistoryFailClosed()
    {
        var f = Prepare();
        var world = f.Host.Session.GeneratedWorld;
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
                f.Snapshot, world, f.Host.Incidence, Red, f.Source,
                f.Destination, f.Embark, f.Disembark,
                History(f, embarkSurcharge: 17UL, settled: false)));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
                f.Snapshot, world, f.Host.Incidence, Red, f.Source,
                f.Destination, f.Embark, f.Disembark,
                History(f, missingLastEdge: true)));
    }

    [Fact]
    public void CapitalFinancingResetsHistoricalCongestionOnChangedCapacity()
    {
        var f = Prepare();
        var world = f.Host.Session.GeneratedWorld;
        var closed = History(f, embarkSurcharge: 17UL);
        var edge = f.Snapshot.Transport.Edges.Single(
            x => x.Edge == f.Embark);
        var invested = f.Snapshot.RecordMonetaryInfrastructureInvestment(
            Red, f.Embark,
            checked(edge.NaturalConstructionCostMoneyMicro!.Value * 4UL));
        var withoutMemory = FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(invested, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark);
        var withOldMemory = FoundationPhysicalWorldBoundMaritimeFreightProof
            .Evaluate(invested, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark, closed);
        Assert.Equal(withoutMemory.Bound, withOldMemory.Bound);
    }

    [Fact]
    public void RejectsLandEdgesAndForeignWorldGenerationSignature()
    {
        var f = Prepare();
        var world = f.Host.Session.GeneratedWorld;
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
                f.Snapshot, world, f.Host.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Embark));
        var foreign = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261013UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)),
            workersPerCivilization: 3);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalWorldBoundMaritimeFreightProof.Evaluate(
                f.Snapshot, foreign.Session.GeneratedWorld,
                foreign.Incidence, Red,
                f.Source, f.Destination, f.Embark, f.Disembark));
    }
}
