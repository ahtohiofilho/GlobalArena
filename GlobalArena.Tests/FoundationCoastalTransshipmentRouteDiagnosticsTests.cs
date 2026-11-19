using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// R51: compare the ACTUAL physical paths of each real worker-backed
/// delivery against the aggregate edges rendered by Godot. A collection
/// of 48 used edges is not a single 48-edge route. A coast crossing is
/// already an explicit billed edge, so adding another transfer charge
/// before this audit risks charging the same event twice.
/// </summary>
public sealed class FoundationCoastalTransshipmentRouteDiagnosticsTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private readonly ITestOutputHelper _output;

    public FoundationCoastalTransshipmentRouteDiagnosticsTests(
        ITestOutputHelper output) => _output = output;

    private sealed record RouteSummary(
        ulong Delivery, ulong Goods, int LandEdges, int CoastEdges,
        int SeaEdges, BigInteger LandBaseCost, BigInteger CoastBaseCost,
        BigInteger SeaBaseCost, string Path);

    private static (
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationActivityCommodityDemandPolicy Demand,
        FoundationCommodityCargoConversionPolicy Cargo) Build()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var workers = new PhysicalWorkerRuntimeState(
            host.Snapshot.Workers.Workers.Select(w =>
                new PhysicalWorkerRuntimeRecord(
                    w.Id, w.Owner, w.CellId,
                    w.Owner == Red ? One : Two,
                    w.OutputMicroPerTurn, w.Activity,
                    w.DemandMultiplierMicro, w.TacticalTileId,
                    w.Occupation)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = new FoundationActivityCommodityDemandPolicy(
            new[] { One, Two },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => new[] { One, Two }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "explicit-godot-economic-qa-only", new[]
            {
                new FoundationCommodityCargoConversion(One, 1_000_000UL),
                new FoundationCommodityCargoConversion(Two, 1_000_000UL)
            });
        return (host, workers, demand, cargo);
    }

    private static bool[] PhysicalLand(
        FoundationTacticalHumanAiTurnHost host)
    {
        var world = host.Session.GeneratedWorld;
        var height = world.StrategicPhysicalFields.Elevation;
        long water = world.StrategicPhysicalFields.SeaLevel.RawValue;
        return host.Incidence.TileIncidences.Select(tile =>
        {
            long sum = tile.IncidentCoarseCellIds.Aggregate(0L,
                (total, id) => checked(total + height.GetRawValue(id)));
            return sum / tile.IncidentCoarseCellIds.Count > water;
        }).ToArray();
    }

    private static IReadOnlyList<RouteSummary> Analyze(
        FoundationPhysicalLaggedFreightCyclePlan plan,
        FoundationTacticalHumanAiTurnHost host,
        FoundationNaturalTransportSeedEvidence seeded)
    {
        var land = PhysicalLand(host);
        var fine = host.Incidence.FineTopology.Edges;
        var natural = seeded.Transport.Edges.ToDictionary(e =>
            e.Edge.FineStrategicEdgeId);
        var lookup = new Dictionary<(ulong, ulong),
            (int Kind, ulong Cost)>();
        foreach (var edge in fine)
        {
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            bool la = land[checked((int)a - 1)];
            bool lb = land[checked((int)b - 1)];
            int kind = la && lb ? 0 : la || lb ? 1 : 2;
            var rate = natural[edge.Id].FreePhysicalCostMicroPerWholeCargoUnit;
            lookup.Add((Math.Min(a, b), Math.Max(a, b)), (kind, rate));
        }
        var summaries = new List<RouteSummary>();
        foreach (var delivery in plan.Distribution.Candidates)
        {
            var tiles = delivery.RouteTiles;
            int terrestrial = 0, coast = 0, sea = 0;
            BigInteger landCost = 0, coastCost = 0, seaCost = 0;
            Assert.True(tiles.Count >= 1);
            Assert.Equal(delivery.SourceTile, tiles[0]);
            Assert.Equal(delivery.BuyerTile, tiles[^1]);
            for (int i = 1; i < tiles.Count; i++)
            {
                ulong a = tiles[i - 1].FineStrategicCellId.Value;
                ulong b = tiles[i].FineStrategicCellId.Value;
                Assert.True(lookup.TryGetValue(
                    (Math.Min(a, b), Math.Max(a, b)), out var crossing),
                    "Every path segment must be a real fine crossing.");
                if (crossing.Kind == 0)
                {
                    terrestrial++;
                    landCost += crossing.Cost;
                }
                else if (crossing.Kind == 1)
                {
                    coast++;
                    coastCost += crossing.Cost;
                }
                else
                {
                    sea++;
                    seaCost += crossing.Cost;
                }
            }
            Assert.Equal(tiles.Count - 1, terrestrial + coast + sea);
            // Because each coast crossing already charges its edge cost,
            // each transition is charged exactly once at this layer.
            summaries.Add(new RouteSummary(
                delivery.DeliveryOrdinal, delivery.GoodsMicro,
                terrestrial, coast, sea, landCost, coastCost, seaCost,
                string.Join(">", tiles.Select(t =>
                    t.FineStrategicCellId.Value))));
        }
        return summaries;
    }

    [Fact]
    public void FrozenSixWorkerFreightRevealsActualModalTransitionsAndCoastCostAB()
    {
        var s = Build();
        var original = s.Host.Snapshot;
        var hash = s.Host.StateHash;
        var quotes = FoundationPhysicalUniversalMarketPricing
            .QuotesFromRealWorkers(s.Workers, s.Demand);
        var signatures = new List<string>();
        foreach (ulong coastalBase in new[]
        {
            5_000UL, 25_000UL, 100_000UL
        })
        {
            // All except the NATURAL coast C0 are unchanged: same graph,
            // six workers, demand, prices, positive open-sea costs and K.
            var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
                s.Host.Session.GeneratedWorld, s.Host.Incidence,
                new FoundationNaturalTransportSeedingPolicy(
                    new FoundationNaturalTransportCostBand(
                        1_000UL, 1_000_000UL),
                    new FoundationNaturalTransportCostBand(
                        coastalBase, 500_000UL),
                    new FoundationNaturalTransportCostBand(
                        25_000UL, 100_000UL),
                    10_000UL));
            var roads = seeded.Transport.Edges.Select(e =>
                new FoundationPhysicalRoutingEdgeCost(
                    e.Edge, FoundationInfrastructureOwnershipKind.NoInfrastructure,
                    null, e.FreePhysicalCostMicroPerWholeCargoUnit, 0UL))
                .ToArray();
            var scales = seeded.Transport.Edges.Select(e =>
                new FoundationPhysicalEdgeCarryingScale(
                    e.Edge, e.CargoMicroAtReferenceLoad)).ToArray();
            FoundationPhysicalLaggedFreightQuoteState? prior = null;
            for (int cycle = 1; cycle <= 4; cycle++)
            {
                var plan = FoundationPhysicalLaggedFreightCyclePlanner
                    .CoordinateCandidate(
                        s.Workers, s.Host.Incidence, s.Demand, s.Cargo,
                        quotes, roads, scales,
                        previousClosedCycleQuotes: prior,
                        maxProducerPasses: 64);
                var deliveries = Analyze(plan, s.Host, seeded);
                var audit = plan.Distribution.AllocationAudit;
                Assert.Equal(audit.TotalAvailableGoodsMicro,
                    audit.TotalAllocatedGoodsMicro);
                Assert.Equal(audit.DeliveryCount, deliveries.Count);
                var used = audit.Freight.UsedEdges;
                // This is the exact distinction previously obscured by the
                // visible 'PARTICIPANT ROUTES' label (edge count, not routes).
                int crossings = deliveries.Sum(x => x.CoastEdges);
                int individualRoutesWithMultipleTransfers =
                    deliveries.Count(x => x.CoastEdges >= 3);
                _output.WriteLine(
                    $"GA_R51_SUMMARY coast_base={coastalBase} cycle={cycle} " +
                    $"deliveries={deliveries.Count} unique_edges={used.Count} " +
                    $"total_coast_crossings={crossings} " +
                    $"multi_transfer_deliveries={individualRoutesWithMultipleTransfers} " +
                    $"max_coast_per_delivery={deliveries.Select(x => x.CoastEdges).DefaultIfEmpty().Max()}");
                foreach (var route in deliveries.OrderByDescending(d =>
                    d.CoastEdges).ThenBy(d => d.Delivery).Take(12))
                {
                    _output.WriteLine(
                        $"GA_R51_PATH coast_base={coastalBase} cycle={cycle} " +
                        $"delivery={route.Delivery} goods={route.Goods} " +
                        $"land_edges={route.LandEdges} " +
                        $"coast_transfers={route.CoastEdges} sea_edges={route.SeaEdges} " +
                        $"land_C0={route.LandBaseCost} " +
                        $"coast_C0={route.CoastBaseCost} sea_C0={route.SeaBaseCost} " +
                        $"tiles={route.Path}");
                }
                signatures.Add(coastalBase + ":" + cycle + ":" +
                    string.Join(";", deliveries.OrderBy(x => x.Delivery)
                        .Select(x => x.Delivery + "=" + x.Path)));
                prior = plan.NextCycleQuotes;
            }
        }
        Assert.Equal(12, signatures.Count);
        Assert.Same(original, s.Host.Snapshot);
        Assert.Equal(hash, s.Host.StateHash);
        Assert.Equal(1UL, s.Host.NextTurn.Value);
    }
}
