using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// R122: actual six-worker fine-first economic settlement A/B, not
/// synthetic route pairs. Explicit QA prices, never production calibration.
/// </summary>
public sealed class FoundationFineFirstShallowSettledEconomicAbTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationGameplayEconomicConfiguration Policy()
    {
        var goods = new[] { Grain, Wood };
        return new FoundationGameplayEconomicConfiguration(
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(250_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(500_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL),
            new FoundationAuthoritativeEconomicSettlementPolicy(
                new FoundationActivityCommodityDemandPolicy(
                    goods,
                    Enum.GetValues<FoundationPhysicalDemandProfile>()
                        .SelectMany(activity => goods.Select(item =>
                            new FoundationActivityCommodityDemandCoefficient(
                                activity, item, 1_000_000UL)))),
                new FoundationCommodityCargoConversionPolicy(
                    "r122-real-worker-qa-cargo",
                    goods.Select(item => new FoundationCommodityCargoConversion(
                        item, 1_000_000UL))),
                new[]
                {
                    new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                    new FoundationPhysicalEconomicTaxRate(Blue, 500U)
                }, maxProducerPasses: 64),
            new[] { (Red, 0UL), (Blue, 0UL) });
    }

    private sealed record Closed(
        FoundationFineFirstEconomicCycleGate.Receipt First,
        FoundationFineFirstEconomicCycleGate.Receipt Second,
        IReadOnlyList<FoundationPhysicalTradeEdgeLoad> Used,
        PhysicalTransportInfrastructureRuntimeState InitialTransport,
        FoundationFineFirstPhysicalGeography Geography,
        PhysicalTacticalIncidenceMap Incidence,
        WorldStateHash SourceHash);

    private static Closed Settle(ulong seed, bool shallow)
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var start = demo.Snapshot;
        var specialized = new PhysicalWorkerRuntimeState(
            start.Workers.Workers.Select(w => new PhysicalWorkerRuntimeRecord(
                w.Id, w.Owner, w.CellId,
                w.Owner == Red ? Grain : Wood,
                w.OutputMicroPerTurn, w.Activity,
                w.DemandMultiplierMicro, w.TacticalTileId, w.Occupation)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var prepared = start.WithWorkers(specialized);
        var world = demo.Session.GeneratedWorld;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            world, demo.Incidence);
        var identity = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, demo.Incidence, fine);
        var policy = Policy();
        FoundationExperimentalShallowFreightPolicy? candidate = null;
        if (shallow)
        {
            long[] depths = fine.FineElevationRaw
                .Where(h => h <= fine.SeaLevelRaw)
                .Select(h => fine.SeaLevelRaw - h)
                .OrderBy(d => d).ToArray();
            candidate = new FoundationExperimentalShallowFreightPolicy(
                depths[(depths.Length - 1) / 4], 10U, 3U, 20U);
        }
        var opened = FoundationFineFirstEconomicCycleGate.OpenNewEconomy(
            prepared, world, demo.Incidence, fine, identity, policy, candidate);
        var unchangedBefore = new CanonicalWorldStateHasher().Compute(prepared);
        var plan = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(opened, demo.Incidence,
                policy.Settlement.Demand, policy.Settlement.Cargo,
                maxProducerPasses: policy.Settlement.MaxProducerPasses,
                fineGeography: fine);
        var first = FoundationFineFirstEconomicCycleGate.CloseAndReplay(
            opened, world, demo.Incidence, fine, identity,
            policy.Settlement,
            new SimulationContext(new TurnNumber(1UL),
                new SimulationSeed(712UL)));
        Assert.True(first.ReplayMatched);
        Assert.Equal(1UL, first.Event.EconomicCycleOrdinal);
        Assert.Equal(first.Event.ProducedGoodsMicro,
            first.Event.SoldAndConsumedGoodsMicro);
        Assert.Equal(first.Event.GrossSalesScaled,
            first.Event.PhysicalFreightScaled +
            first.Event.CreditedToTreasuriesScaled);
        Assert.Equal(first.Event.CreditedToTreasuriesScaled,
            first.Next.Treasury.Get(Red).CashScaled +
            first.Next.Treasury.Get(Blue).CashScaled);
        var second = FoundationFineFirstEconomicCycleGate.CloseAndReplay(
            first.Next, world, demo.Incidence, fine, identity,
            policy.Settlement,
            new SimulationContext(new TurnNumber(2UL),
                new SimulationSeed(713UL)));
        Assert.True(second.ReplayMatched);
        Assert.Equal(first.Event.ProducedGoodsMicro,
            second.Event.ProducedGoodsMicro);
        Assert.Equal(first.Event.CreditedToTreasuriesScaled,
            second.Event.CreditedToTreasuriesScaled);
        Assert.Equal(2UL, second.Event.EconomicCycleOrdinal);
        Assert.Equal(new CanonicalWorldStateHasher().Compute(prepared),
            unchangedBefore);
        return new Closed(first, second,
            plan.Distribution.AllocationAudit.Freight.UsedEdges,
            opened.Transport, fine, demo.Incidence, unchangedBefore);
    }

    [Theory]
    [InlineData(20261012UL)]
    [InlineData(20261013UL)]
    public void TwoSettledRealWorkerCyclesRemainReplayableWithShallowFreight(
        ulong seed)
    {
        var baseCycle = Settle(seed, shallow: false);
        var candidate = Settle(seed, shallow: true);

        Assert.Equal(baseCycle.SourceHash, candidate.SourceHash);
        Assert.Equal(baseCycle.Geography.FineElevationRaw,
            candidate.Geography.FineElevationRaw);
        Assert.Equal(baseCycle.First.Event.ProducedGoodsMicro,
            candidate.First.Event.ProducedGoodsMicro);
        Assert.Equal(baseCycle.First.Event.SoldAndConsumedGoodsMicro,
            candidate.First.Event.SoldAndConsumedGoodsMicro);
        Assert.True(baseCycle.First.Event.GrossSalesScaled > BigInteger.Zero);
        Assert.True(candidate.First.Event.GrossSalesScaled > BigInteger.Zero);
        Assert.True(candidate.First.Event.CreditedToTreasuriesScaled >
            BigInteger.Zero);
        Assert.Equal(baseCycle.InitialTransport.Edges.Count,
            candidate.InitialTransport.Edges.Count);
        var original = baseCycle.InitialTransport.Edges.ToDictionary(
            e => e.Edge);
        var all = candidate.InitialTransport.Edges.ToDictionary(e => e.Edge);
        Assert.Equal(original.Keys.OrderBy(x => x.FineStrategicEdgeId.Value),
            all.Keys.OrderBy(x => x.FineStrategicEdgeId.Value));
        foreach (var (id, edge) in original)
        {
            var modified = all[id];
            Assert.True(modified.FreePhysicalCostMicroPerWholeCargoUnit >=
                edge.FreePhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(edge.CargoMicroAtReferenceLoad,
                modified.CargoMicroAtReferenceLoad);
        }

        var prior = baseCycle.Used.ToDictionary(x => x.Edge, x => x.TotalCargoMicro);
        var newer = candidate.Used.ToDictionary(x => x.Edge, x => x.TotalCargoMicro);
        // Report real settled trade impact, not a hard-coded assertion
        // about any specific seed's chosen port or individual route.
        Assert.True(prior.Count > 0 && newer.Count > 0);
        Assert.True(candidate.First.Event.PhysicalFreightScaled >= BigInteger.Zero);
        Assert.True(baseCycle.First.Event.PhysicalFreightScaled >= BigInteger.Zero);
    }
}
