using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// R70: CLOSE genuine economic cycles via the existing trusted turn phase,
/// not by calling internal treasury settlement or injecting a fake history.
/// The R69 proof remains read-only: it does not choose/settle the sea route.
/// </summary>
public sealed class FoundationRealSettledMaritimeFreightBridgeTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationAuthoritativeEconomicSettlementPolicy Policy() =>
        new(
            new FoundationActivityCommodityDemandPolicy(
                new[] { Grain, Wood },
                Enum.GetValues<FoundationPhysicalDemandProfile>()
                    .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                        new FoundationActivityCommodityDemandCoefficient(
                            profile, item, 1_000_000UL)))),
            new FoundationCommodityCargoConversionPolicy(
                "authoritative-sales-cargo", new[]
                {
                    new FoundationCommodityCargoConversion(
                        Grain, 1_000_000UL),
                    new FoundationCommodityCargoConversion(
                        Wood, 1_000_000UL)
                }),
            new[]
            {
                new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Blue, 500U)
            }, maxProducerPasses: 64);

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        FoundationAuthoritativeEconomicSettlementPolicy Policy,
        PhysicalTacticalTileId Source,
        PhysicalTacticalTileId Destination,
        PhysicalTacticalEdgeId Embark,
        PhysicalTacticalEdgeId Disembark);

    private static HashSet<ulong> Reach(
        ulong start, Dictionary<ulong, List<ulong>> graph)
    {
        var known = new HashSet<ulong> { start };
        var queue = new Queue<ulong>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out ulong at))
            if (graph.TryGetValue(at, out var neighbors))
                foreach (var id in neighbors)
                    if (known.Add(id)) queue.Enqueue(id);
        return known;
    }

    private static Scenario Create()
    {
        var initial = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            initial.Session.GeneratedWorld, initial.Incidence,
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
        var treasury = CivilizationTreasuryRuntimeState.CreateOpening(
            new[] { (Red, 0UL), (Blue, 0UL) });
        var snapshot = initial.Snapshot.DeployInitialPhysicalTransport(
                seeded.Transport,
                initial.Session.GeneratedWorld, initial.Incidence)
            .DeployInitialTreasury(treasury);
        initial.Session.AcceptSnapshot(snapshot);
        var policy = Policy();
        var host = new FoundationTacticalHumanAiTurnHost(
            initial.Session, initial.Incidence, new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            }, enableMonetaryInfrastructure: true,
            settlementPolicy: policy);

        var fields = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var isLand = host.Incidence.TileIncidences.Select(tile =>
        {
            long sum = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                sum = checked(sum + fields.Elevation.GetRawValue(coarse));
            return sum / tile.IncidentCoarseCellIds.Count >
                fields.SeaLevel.RawValue;
        }).ToArray();
        var land = new Dictionary<ulong, List<ulong>>();
        var sea = new Dictionary<ulong, List<ulong>>();
        var coast = new List<(PhysicalTacticalEdgeId Edge,
            ulong Land, ulong Sea)>();
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
            if (la && lb) Add(land, a, b);
            else if (!la && !lb) Add(sea, a, b);
            else coast.Add((
                new PhysicalTacticalEdgeId(
                    host.Incidence.Refinement.FineParameters, edge.Id),
                la ? a : b, la ? b : a));
        }

        var chosen = (From: coast[0], To: coast[0]);
        bool found = false;
        foreach (var a in coast)
        {
            var landReach = Reach(a.Land, land);
            var seaReach = Reach(a.Sea, sea);
            foreach (var b in coast)
            {
                if (a.Edge == b.Edge || a.Land == b.Land ||
                    a.Sea == b.Sea || !seaReach.Contains(b.Sea) ||
                    !landReach.Contains(b.Land))
                    continue;
                chosen = (a, b);
                found = true;
                break;
            }
            if (found) break;
        }
        Assert.True(found, "Need two actual coastline edges with " +
            "positive natural sea leg and reachable terrestrial endpoints.");

        var fine = host.Incidence.Refinement.FineParameters;
        return new Scenario(host, policy,
            new PhysicalTacticalTileId(
                fine, new StrategicCellId(chosen.From.Land)),
            new PhysicalTacticalTileId(
                fine, new StrategicCellId(chosen.To.Land)),
            chosen.From.Edge, chosen.To.Edge);
    }

    private static FoundationPhysicalTreasuryBoundMaritimeProof.Evidence Proof(
        Scenario scene, ulong nextOrdinal,
        FoundationCommodityCargoConversionPolicy? cargo = null)
        => FoundationPhysicalTreasuryBoundMaritimeProof.Evaluate(
            scene.Host.Snapshot, scene.Host.Session.GeneratedWorld,
            scene.Host.Incidence, cargo ?? scene.Policy.Cargo,
            Grain, 1_000_000UL, Red, scene.Source, scene.Destination,
            scene.Embark, scene.Disembark, nextOrdinal);

    private static FoundationEconomicSalesSettledEvent Close(
        Scenario scene, ulong ordinal)
    {
        scene.Host.SetHumanReady(Red);
        var receipt = scene.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        var sold = Assert.IsType<FoundationEconomicSalesSettledEvent>(
            receipt.EconomicSettlement);
        Assert.Equal(ordinal, sold.EconomicCycleOrdinal);
        Assert.Equal(sold.ProducedGoodsMicro,
            sold.SoldAndConsumedGoodsMicro);
        Assert.Equal(receipt.Hash, scene.Host.StateHash);
        Assert.Equal(ordinal,
            scene.Host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(scene.Policy.Cargo.CargoUnitCode,
            scene.Host.Snapshot.Treasury.LastSettledCargoUnitCode);
        return sold;
    }

    [Fact]
    public void RealConsecutiveSettlementsEnableReadOnlyMaritimeProofWithoutForgedHistory()
    {
        var s = Create();
        var opening = Proof(s, 1UL);
        Assert.False(opening.UsedVerifiedSettledFreight);
        Assert.Equal(0UL, opening.PreviousSettledEconomicCycleOrdinal);
        var first = Close(s, 1UL);
        Assert.True(first.CreditedToTreasuriesScaled.Sign > 0);
        var firstClosedHash = s.Host.StateHash;

        var secondCandidate = Proof(s, 2UL);
        Assert.True(secondCandidate.UsedVerifiedSettledFreight);
        Assert.Equal(1UL, secondCandidate.PreviousSettledEconomicCycleOrdinal);
        Assert.Equal(2UL, secondCandidate.PlanningEconomicCycleOrdinal);
        Assert.Equal(1_000_000UL, secondCandidate.CargoMicro);
        // Independently inspect exact carried quote for the chosen coast.
        var coast = s.Host.Snapshot.Transport.Edges.Single(
            e => e.Edge == s.Embark);
        var last = s.Host.Snapshot.Treasury.LastSettledFreightQuotes.Single(
            e => e.Edge == s.Embark);
        ulong expectedSurcharge =
            coast.FreePhysicalCostMicroPerWholeCargoUnit ==
                last.FreePhysicalCostMicroPerWholeCargoUnit &&
            FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveReferenceCargo(coast) == last.ReferenceCargoMicro
                ? last.QuotedCongestionSurchargeMicroPerWholeCargoUnit : 0UL;
        Assert.Equal(
            opening.Route.Bound.EmbarkCoastRateMicroPerWholeCargoUnit +
                expectedSurcharge,
            secondCandidate.Route.Bound.EmbarkCoastRateMicroPerWholeCargoUnit);
        Assert.Equal(firstClosedHash, s.Host.StateHash);
        Assert.Throws<InvalidOperationException>(() => Proof(s, 1UL));
        Assert.Throws<InvalidOperationException>(() => Proof(s, 3UL));

        var second = Close(s, 2UL);
        Assert.True(second.CreditedToTreasuriesScaled.Sign > 0);
        var thirdCandidate = Proof(s, 3UL);
        Assert.True(thirdCandidate.UsedVerifiedSettledFreight);
        Assert.Equal(2UL, thirdCandidate.PreviousSettledEconomicCycleOrdinal);
        Assert.Equal(3UL, thirdCandidate.PlanningEconomicCycleOrdinal);
        Assert.Equal(second.CargoUnitCode, thirdCandidate.CargoUnitCode);
        Assert.Equal(s.Host.StateHash,
            new CanonicalWorldStateHasher().Compute(s.Host.Snapshot));
    }

    [Fact]
    public void RealClosedTreasuryRejectsWrongCargoUnitWithoutChangingSettlement()
    {
        var s = Create();
        Close(s, 1UL);
        var committed = s.Host.Snapshot;
        var committedHash = s.Host.StateHash;
        var unrelatedCargo = new FoundationCommodityCargoConversionPolicy(
            "another-unit", new[]
            {
                new FoundationCommodityCargoConversion(
                    Grain, 1_000_000UL)
            });
        Assert.Throws<InvalidOperationException>(() =>
            Proof(s, 2UL, unrelatedCargo));
        Assert.Same(committed, s.Host.Snapshot);
        Assert.Equal(committedHash, s.Host.StateHash);
    }

    [Fact]
    public void R74RealSettledCyclesPreserveExhaustiveVersusBoundedMaritimeDecision()
    {
        var scene = Create();
        var world = scene.Host.Session.GeneratedWorld;
        var incidence = scene.Host.Incidence;

        // Economic-cycle history is generated only by the real public
        // settlement boundary (R70), never fabricated in the treasury.
        for (ulong settled = 1UL; settled <= 2UL; settled++)
        {
            var economic = Close(scene, settled);
            Assert.True(economic.CreditedToTreasuriesScaled.Sign > 0);
            var snapshot = scene.Host.Snapshot;
            var hash = scene.Host.StateHash;
            Assert.True(snapshot.Treasury.HasClosedEconomicCycle);
            Assert.Equal(settled,
                snapshot.Treasury.LastSettledEconomicCycleOrdinal);
            Assert.NotEmpty(snapshot.Treasury.LastSettledFreightQuotes);

            foreach (var seller in new[] { Red, Blue })
            {
                var exhaustive =
                    FoundationPhysicalFrozenEconomicMaritimeComparator.Compare(
                        snapshot, world, incidence, scene.Policy.Cargo,
                        Grain, 1_000_000UL, seller, scene.Source,
                        scene.Destination, settled + 1UL, proveBound: false);
                var bounded =
                    FoundationPhysicalFrozenEconomicMaritimeComparator.Compare(
                        snapshot, world, incidence, scene.Policy.Cargo,
                        Grain, 1_000_000UL, seller, scene.Source,
                        scene.Destination, settled + 1UL, proveBound: true);

                Assert.Equal(exhaustive.LandOnlyRateMicro,
                    bounded.LandOnlyRateMicro);
                Assert.Equal(exhaustive.SeaPreferred, bounded.SeaPreferred);
                Assert.Equal(exhaustive.PotentialPairs, bounded.PotentialPairs);
                Assert.True(bounded.ExcludedPairs >= 0);
                Assert.True(bounded.QuotedPairs <= exhaustive.QuotedPairs);
                Assert.True(exhaustive.QuotedPairs > 0);
                if (exhaustive.SeaPreferred)
                {
                    Assert.Equal(exhaustive.BestSeaRateMicro,
                        bounded.BestSeaRateMicro);
                    Assert.Equal(exhaustive.EmbarkEdge, bounded.EmbarkEdge);
                    Assert.Equal(exhaustive.DisembarkEdge,
                        bounded.DisembarkEdge);
                }
                Assert.Equal(exhaustive,
                    FoundationPhysicalFrozenEconomicMaritimeComparator.Compare(
                        snapshot, world, incidence, scene.Policy.Cargo,
                        Grain, 1_000_000UL, seller, scene.Source,
                        scene.Destination, settled + 1UL, proveBound: false));
                Assert.Same(snapshot, scene.Host.Snapshot);
                Assert.Equal(hash, scene.Host.StateHash);
            }
        }
    }
}
