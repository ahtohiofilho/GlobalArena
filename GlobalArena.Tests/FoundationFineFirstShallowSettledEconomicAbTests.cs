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

    [Fact]
    public void RealSettledCashCanFinanceShallowPortWithoutBuilderOrCongestion()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var specialized = new PhysicalWorkerRuntimeState(
            demo.Snapshot.Workers.Workers.Select(w =>
                new PhysicalWorkerRuntimeRecord(
                    w.Id, w.Owner, w.CellId,
                    w.Owner == Red ? Grain : Wood,
                    w.OutputMicroPerTurn, w.Activity,
                    w.DemandMultiplierMicro, w.TacticalTileId,
                    w.Occupation)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        demo.Session.AcceptSnapshot(demo.Snapshot.WithWorkers(specialized));
        var world = demo.Session.GeneratedWorld;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            world, demo.Incidence);
        var identity = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            world, demo.Incidence, fine);
        var policy = Policy();
        long[] depths = fine.FineElevationRaw
            .Where(h => h <= fine.SeaLevelRaw)
            .Select(h => fine.SeaLevelRaw - h).OrderBy(x => x).ToArray();
        var shallow = new FoundationExperimentalShallowFreightPolicy(
            depths[(depths.Length - 1) / 4], 10U, 3U, 20U);
        var opened = FoundationFineFirstEconomicCycleGate.OpenNewEconomy(
            demo.Snapshot, world, demo.Incidence, fine, identity,
            policy, shallow);
        var baseline = FoundationPhysicalNaturalTransportSeeder
            .PreviewFineFirst(world, demo.Incidence, fine,
                policy.OperatingTransport, policy.MonetaryConstruction);
        var old = baseline.Transport.Edges.ToDictionary(x => x.Edge);
        var portIds = demo.Incidence.FineTopology.Edges
            .Where(e =>
            {
                int a = checked((int)e.IncidentCellIds[0].Value - 1);
                int b = checked((int)e.IncidentCellIds[1].Value - 1);
                return fine.CrossingAtIndices(a, b) ==
                    FoundationFineTransportCrossingKind.Port;
            }).Select(e => new PhysicalTacticalEdgeId(
                demo.Incidence.Refinement.FineParameters, e.Id)).ToHashSet();
        var port = opened.Transport.Edges.First(x =>
            portIds.Contains(x.Edge) &&
            x.NaturalConstructionCostMoneyMicro >
                old[x.Edge].NaturalConstructionCostMoneyMicro);
        Assert.True(port.NaturalConstructionCostMoneyMicro >
            policy.MonetaryConstruction.CoastBaseCostMoneyMicro);
        demo.Session.AcceptFineFirstOpening(
            opened, demo.Incidence, fine, identity);
        var host = new FoundationTacticalHumanAiTurnHost(
            demo.Session, demo.Incidence,
            new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiResumeProductionPolicy()
            },
            enableMonetaryInfrastructure: true,
            settlementPolicy: policy.Settlement,
            fineGeography: fine, fineSignature: identity);

        host.SetHumanReady(Red);
        var first = host.TryCloseTurn();
        Assert.NotNull(first);
        Assert.True(first!.ReplayMatched);
        Assert.Equal(1UL, first.EconomicSettlement!.EconomicCycleOrdinal);
        const ulong spend = 100_000UL;
        BigInteger cash = host.Snapshot.Treasury.Get(Red).CashScaled;
        Assert.True(cash > GlobalArenaMoneyScale.FromMoneyMicro(spend));
        var before = host.Snapshot.Transport.Edges.Single(x =>
            x.Edge == port.Edge);
        ulong originalEffective =
            FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(before);
        var demandBefore = host.Snapshot.Workers;
        host.QueueHumanMonetaryInvestment(Red, port.Edge, spend);
        host.SetHumanReady(Red);
        var second = host.TryCloseTurn();
        Assert.NotNull(second);
        Assert.True(second!.ReplayMatched);
        Assert.Equal(2UL, second.EconomicSettlement!.EconomicCycleOrdinal);
        Assert.Single(second.InfrastructureInvestments);
        Assert.Equal(spend, second.InfrastructureInvestments[0].MoneyMicro);

        var invested = host.Snapshot.Transport.Edges.Single(x =>
            x.Edge == port.Edge);
        Assert.Equal(port.NaturalConstructionCostMoneyMicro,
            invested.NaturalConstructionCostMoneyMicro);
        Assert.Equal(port.FreePhysicalCostMicroPerWholeCargoUnit,
            invested.FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(port.CargoMicroAtReferenceLoad,
            invested.CargoMicroAtReferenceLoad);
        Assert.Equal(spend, invested.CommittedInvestmentMoneyMicro);
        Assert.Equal(Red, invested.InfrastructureOwner);
        Assert.True(FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(invested) < originalEffective);
        Assert.Equal(demandBefore.Workers.Select(w => w.Id),
            host.Snapshot.Workers.Workers.Select(w => w.Id));
        Assert.Equal(cash - GlobalArenaMoneyScale.FromMoneyMicro(spend) +
            second.EconomicSettlement.Credits.Single(x =>
                x.Civilization == Red).CashCreditScaled,
            host.Snapshot.Treasury.Get(Red).CashScaled);
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
        Assert.True(prior.Count > 0 && newer.Count > 0);
        var allUsed = prior.Keys.Concat(newer.Keys).Distinct().ToArray();
        int changedLoads = allUsed.Count(edge =>
            prior.GetValueOrDefault(edge) != newer.GetValueOrDefault(edge));
        // The visual benchmark in R120 used this seed with real workers.
        // Other seeds are measured, not forced to manufacture a route change.
        if (seed == 20261012UL)
            Assert.True(changedLoads > 0,
                "R120 depth must change at least one real settled trade load.");
        Assert.True(candidate.First.Event.PhysicalFreightScaled >= BigInteger.Zero);
        Assert.True(baseCycle.First.Event.PhysicalFreightScaled >= BigInteger.Zero);
        int changedPortQuotes = 0, changedSeaQuotes = 0;
        var fineEdges = baseCycle.Incidence.FineTopology.Edges.ToDictionary(
            x => x.Id);
        foreach (var (edgeId, original) in original)
        {
            var modified = all[edgeId];
            var topology = fineEdges[edgeId.FineStrategicEdgeId];
            int a = checked((int)topology.IncidentCellIds[0].Value - 1);
            int b = checked((int)topology.IncidentCellIds[1].Value - 1);
            var kind = baseCycle.Geography.CrossingAtIndices(a, b);
            if (kind is FoundationFineTransportCrossingKind.Ground or
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
            {
                Assert.Equal(original, modified);
            }
            else if (kind == FoundationFineTransportCrossingKind.Port)
            {
                Assert.True(modified.NaturalConstructionCostMoneyMicro >=
                    original.NaturalConstructionCostMoneyMicro);
                if (modified.NaturalConstructionCostMoneyMicro >
                    original.NaturalConstructionCostMoneyMicro)
                    changedPortQuotes++;
            }
            else
            {
                Assert.Equal(FoundationFineTransportCrossingKind.OpenSea, kind);
                Assert.Equal(original.NaturalConstructionCostMoneyMicro,
                    modified.NaturalConstructionCostMoneyMicro);
                if (modified.FreePhysicalCostMicroPerWholeCargoUnit >
                    original.FreePhysicalCostMicroPerWholeCargoUnit)
                    changedSeaQuotes++;
            }
        }
        Assert.True(changedPortQuotes > 0 && changedSeaQuotes > 0);
        Assert.Equal(baseCycle.Second.Event.CreditedToTreasuriesScaled,
            baseCycle.First.Event.CreditedToTreasuriesScaled);
        Assert.Equal(candidate.Second.Event.CreditedToTreasuriesScaled,
            candidate.First.Event.CreditedToTreasuriesScaled);
    }
}
