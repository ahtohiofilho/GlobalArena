using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// R70: CLOSE genuine economic cycles via the existing trusted turn phase,
/// not by calling internal treasury settlement or injecting a fake history.
/// The R69 proof remains read-only: it does not choose/settle the sea route.
/// </summary>
public sealed class FoundationRealSettledMaritimeFreightBridgeTests
{
    private readonly ITestOutputHelper _output;

    public FoundationRealSettledMaritimeFreightBridgeTests(ITestOutputHelper output)
        => _output = output;

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

    private static Scenario Create(bool fundCoastalInfrastructure = false,
        bool seedOwnedTariff = false)
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
            new[] { (Red, fundCoastalInfrastructure ? 1_000_000_000_000_000UL : 0UL), (Blue, 0UL) });
        // An explicitly pre-owned and tolled initial crossing is valid in
        // the canonical inventory. This is a TEST-ONLY tariff policy input,
        // not a claim that investment automatically chooses a tariff.
        PhysicalTransportInfrastructureRuntimeState transport = seeded.Transport;
        if (seedOwnedTariff)
        {
            var coastSeed = transport.Edges.First(row =>
                row.CargoMicroAtReferenceLoad == 500_000UL);
            transport = PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                transport.Edges.Select(row => row.Edge == coastSeed.Edge
                    ? row with
                    {
                        OwnershipKind = PhysicalTransportOwnershipKind.Owned,
                        InfrastructureOwner = Red,
                        ExternalTariffMicroPerWholeCargoUnit = 2_000UL
                    }
                    : row));
        }
        var snapshot = initial.Snapshot.DeployInitialPhysicalTransport(
                transport,
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

        if (fundCoastalInfrastructure)
        {
            // Test-owned, monetary investment in one REAL crossing.
            // Red owns the infrastructure; Blue must pay its actual tariff.
            var selected = host.Snapshot.Transport.Edges.Single(
                edge => edge.Edge == chosen.From.Edge);
            var invested = host.Snapshot.RecordMonetaryInfrastructureInvestment(
                Red, selected.Edge,
                checked(selected.NaturalConstructionCostMoneyMicro!.Value * 32UL));
            var financed = invested.Transport.Edges.Single(
                edge => edge.Edge == chosen.From.Edge);
            Assert.Equal(Red, financed.InfrastructureOwner);
            // Money funding establishes ownership/capacity, not an
            // automatic positive commercial tariff. Do not invent one.
            host.Session.AcceptSnapshot(invested);
            host = new FoundationTacticalHumanAiTurnHost(
                host.Session, host.Incidence, new[] { Red },
                new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
                {
                    [Blue] = new FoundationTacticalAiHoldPolicy()
                }, enableMonetaryInfrastructure: true,
                settlementPolicy: policy);
        }

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
        foreach (var (funded, initialTariff) in new[]
        {
            (Funded: false, InitialTariff: false),
            (Funded: true, InitialTariff: false),
            (Funded: false, InitialTariff: true)
        })
        {
        var scene = Create(funded, initialTariff);
        if (funded)
            Assert.Contains(scene.Host.Snapshot.Transport.Edges,
                edge => edge.InfrastructureOwner == Red);
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

            // Capture what the real settled economy ACTUALLY exercised.
            // R112 requires zero carry-over congestion after every real close.
            // A positive third-party tariff remains a separately audited case.
            var projected = FoundationPhysicalAuthoritativeTransportProjector
                .FromWorld(snapshot, incidence);
            var projectedByEdge = projected.Roads.ToDictionary(e => e.Edge);
            var projectedScales = projected.Scales.ToDictionary(
                e => e.Edge, e => e.CargoMicroAtReferenceLoad);
            var positiveThirdPartyTariffs = projectedByEdge.Values.Count(
                row => row.InfrastructureOwner.HasValue &&
                    row.ExternalTariffMicroPerWholeCargoUnit > 0UL);
            Assert.All(snapshot.Treasury.LastSettledFreightQuotes,
                previous =>
                {
                    Assert.True(projectedByEdge.TryGetValue(
                        previous.Edge, out var row));
                    Assert.Equal(projectedScales[previous.Edge],
                        previous.ReferenceCargoMicro);
                    Assert.Equal(row!.PhysicalCostMicroPerWholeCargoUnit,
                        previous.FreePhysicalCostMicroPerWholeCargoUnit);
                    Assert.Equal(0UL,
                        previous.QuotedCongestionSurchargeMicroPerWholeCargoUnit);
                });
            if (initialTariff)
            {
                Assert.True(positiveThirdPartyTariffs > 0,
                    "Explicit test-only tariff must survive a real settlement.");
                var tolled = projectedByEdge.Values.Single(row =>
                    row.InfrastructureOwner == Red &&
                    row.ExternalTariffMicroPerWholeCargoUnit == 2_000UL);
                Assert.Equal(2_000UL, tolled.ExternalTariffMicroPerWholeCargoUnit);
                // The same physical traversal charges its owner zero
                // external toll, but another civilization the exact
                // canonical tariff; neither is exempt from physical freight.
                UInt128 ownerRate =
                    tolled.PhysicalCostMicroPerWholeCargoUnit;
                UInt128 outsiderRate = ownerRate +
                    tolled.ExternalTariffMicroPerWholeCargoUnit;
                Assert.Equal((UInt128)2_000UL, outsiderRate - ownerRate);
            }
            _output.WriteLine(
                $"GA_R74_SETTLED_COVERAGE financed={funded} seeded_tariff={initialTariff} cycle={settled} " +
                $"positive_tariff_edges={positiveThirdPartyTariffs} " +
                $"settled_fixed_freight_edges={snapshot.Treasury.LastSettledFreightQuotes.Count} " +
                $"positive_tariff_gate={(positiveThirdPartyTariffs > 0 ? "COVERED" : "OPEN")} " +
                $"zero_surcharge_gate=COVERED");

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
}
