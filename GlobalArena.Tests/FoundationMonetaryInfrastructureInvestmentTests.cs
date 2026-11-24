using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Monetarily financed construction from actual CIVILIZATION treasury.
/// Physical workers produce/demand at their node but are NEVER required
/// or consumed to make an infrastructure investment; no commodity stock.
/// </summary>
public sealed class FoundationMonetaryInfrastructureInvestmentTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalTacticalEdgeId RemoteEdge,
        PhysicalWorkerId Producer);

    private static FoundationNaturalTransportSeedingPolicy Operation() =>
        new(new FoundationNaturalTransportCostBand(1_000_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(20_000_000UL, 100_000UL),
            new FoundationNaturalTransportCostBand(100_000_000UL, 10_000UL),
            4_000_000UL);

    private static FoundationMonetaryConstructionPolicy Money() =>
        new(2_000_000UL, 4_000_000UL, 6_000_000UL, 8_000_000UL);

    private static Scenario Create(
        ulong redOpeningMoneyMicro = 25_000_000UL,
        ulong blueOpeningMoneyMicro = 0UL)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence,
            Operation(), Money());
        var deployed = fixture.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, fixture.Session.GeneratedWorld, fixture.Incidence);
        var opening = CivilizationTreasuryRuntimeState.CreateOpening(
            new[] { (Red, redOpeningMoneyMicro), (Blue, blueOpeningMoneyMicro) });
        fixture.Session.AcceptSnapshot(deployed.DeployInitialTreasury(opening));
        var person = fixture.Snapshot.Workers.Workers.First(w =>
            w.Owner == Red && w.Activity == PhysicalWorkerActivity.Producing);
        // The edge is NOT adjacent to the investing civilization's workers.
        // No contrived builder or demand market is materialized on the edge.
        var finiteEdges = seeded.Transport.Edges
            .Where(row => row.CargoMicroAtReferenceLoad != ulong.MaxValue)
            .Select(row => row.Edge.FineStrategicEdgeId).ToHashSet();
        var crossing = fixture.Incidence.FineTopology.Edges.First(edge =>
            finiteEdges.Contains(edge.Id) &&
            !edge.IncidentCellIds.Contains(
                person.TacticalTileId!.Value.FineStrategicCellId));
        var edgeId = new PhysicalTacticalEdgeId(
            fixture.Incidence.Refinement.FineParameters, crossing.Id);
        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence,
            new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            },
            enableMonetaryInfrastructure: true);
        return new Scenario(host, edgeId, person.Id);
    }

    private static PhysicalTransportEdgeRuntimeRecord Edge(Scenario s) =>
        s.Host.Snapshot.Transport.Edges.Single(x =>
            x.Edge == s.RemoteEdge);

    [Fact]
    public void R208FundsAndBindsRemoteWorkersAtomicallyAndReplays()
    {
        var s = Create();
        var before = s.Host.Snapshot.WithConstructions(
            new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var admission = new FoundationMonetaryInfrastructureEventExecutor(
            s.Host.Session.GeneratedWorld, s.Host.Incidence);
        var executor = new FoundationFundedWorkerIntentJournal(
            new FoundationConstructionFundingExecutor(admission, 2_000_000UL));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var id = new EventId(new CommandId(turn, 1UL), 1UL);
        var workers = before.Workers.Workers.Where(w => w.Owner == Red).ToArray();
        var assignments = workers.Select(w => new FoundationFundedWorkerInsertion(w.Id, 0)).ToArray();
        var (after, ev) = executor.Record(before, id, Red, s.RemoteEdge, 2_000_000UL,
            assignments.Reverse(), context);
        var project = Assert.Single(after.Constructions.Projects);
        Assert.Equal(project.Id, ev.ProjectId);
        Assert.Equal(0UL, project.CompletedWorkMicro);
        Assert.False(project.IsActivated);
        Assert.Equal(1_000_000UL, project.RequiredWorkMicro);
        Assert.Equal(before.Treasury.Get(Red).CashScaled -
            GlobalArenaMoneyScale.FromMoneyMicro(2_000_000UL), after.Treasury.Get(Red).CashScaled);
        Assert.Same(before.Transport, after.Transport);
        Assert.Same(before.Workers, after.Workers);
        Assert.Equal(before.Revision + 1, after.Revision);
        foreach (var worker in workers)
        {
            var order = Assert.Single(after.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
            Assert.Equal(FoundationWorkerOrderKind.Build, order.Kind);
            Assert.Equal(project.Id, order.ProjectId);
        }
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(after), hash.Compute(executor.Execute(before, ev, context)));
        var log = new SimulationEventLog(new[] { new SimulationEventLogEntry(1UL, ev, true, true) });
        Assert.Equal(hash.Compute(after), hash.Compute(new SimulationEventLogReplayer(executor)
            .Replay(new SimulationReplayInput(before, log, context))));
        Assert.Equal(hash.Compute(after), hash.Compute(executor.Record(before, id, Red,
            s.RemoteEdge, 2_000_000UL, assignments, context).Next));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(after, ev, context));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(before,
            ev with { ProjectId = project.Id + 1 }, context));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(before,
            ev with { Assignments = Array.Empty<FoundationFundedWorkerInsertion>() }, context));
        var foreign = before.Workers.Workers.First(w => w.Owner == Blue);
        Assert.Throws<InvalidOperationException>(() => executor.Record(before, id, Red,
            s.RemoteEdge, 2_000_000UL, new[] { assignments[0], new FoundationFundedWorkerInsertion(foreign.Id, 0) }, context));
        Assert.Throws<InvalidOperationException>(() => executor.Record(before, id, Red,
            s.RemoteEdge, 2_000_000UL, new[] { assignments[0], assignments[0] }, context));
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.Record(before, id, Red,
            s.RemoteEdge, 2_000_000UL, new[] { new FoundationFundedWorkerInsertion(s.Producer, 9) }, context));
        Assert.Empty(before.Constructions.Projects);
        Assert.All(before.WorkerOrders.Queues, q => Assert.Empty(q.Orders));
        var alone = executor.Record(before, id, Red, s.RemoteEdge, 2_000_000UL,
            Array.Empty<FoundationFundedWorkerInsertion>(), context).Next;
        Assert.Single(alone.Constructions.Projects);
        Assert.All(alone.WorkerOrders.Queues, q => Assert.Empty(q.Orders));
        Assert.Equal(after.Treasury.Get(Red).CashScaled, alone.Treasury.Get(Red).CashScaled);
    }

    [Fact]
    public void ConstructionFundingDebitsOnceAndReplaysWithoutActivatingInfrastructure()
    {
        var s = Create();
        var before = s.Host.Snapshot.WithConstructions(
            new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()));
        var admission = new FoundationMonetaryInfrastructureEventExecutor(
            s.Host.Session.GeneratedWorld, s.Host.Incidence);
        var executor = new FoundationConstructionFundingExecutor(admission, 2_000_000UL);
        // Obtain the existing host's real command identity without committing it
        // to the opt-in construction snapshot.
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 2_000_000UL);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        var legacy = Assert.Single(receipt!.InfrastructureInvestments);
        var context = new SimulationContext(legacy.Id.Turn, new SimulationSeed(712UL));
        var ev = new FoundationConstructionFundedEvent(legacy.Id, Red, s.RemoteEdge,
            2_000_000UL, 1_000_000UL, before.Revision);
        var after = executor.Execute(before, ev, context);
        var project = Assert.Single(after.Constructions.Projects);
        Assert.Equal(1UL, project.Id);
        Assert.Equal(0UL, project.CompletedWorkMicro);
        Assert.Equal(1_000_000UL, project.RequiredWorkMicro);
        Assert.Same(before.Transport, after.Transport);
        Assert.Same(before.Workers, after.Workers);
        Assert.Equal(before.Treasury.Get(Red).CashScaled -
            GlobalArenaMoneyScale.FromMoneyMicro(2_000_000UL), after.Treasury.Get(Red).CashScaled);
        Assert.Equal(before.Treasury.Get(Red).TotalInfrastructureInvestmentScaled +
            GlobalArenaMoneyScale.FromMoneyMicro(2_000_000UL),
            after.Treasury.Get(Red).TotalInfrastructureInvestmentScaled);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(after), hasher.Compute(executor.Execute(before, ev, context)));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(after, ev, context));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(before,
            ev with { RequiredWorkMicro = 1UL }, context));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(after,
            ev with { ExpectedRevision = after.Revision }, context));
        Assert.Throws<InvalidOperationException>(() => executor.Execute(before,
            ev with { Investor = Blue }, context));
        Assert.Throws<InvalidOperationException>(() => before.RecordMonetaryInfrastructureInvestment(
            Red, s.RemoteEdge, 1UL));
    }

    [Fact]
    public void ConstructionSnapshotRetainsProgressAndHashesWithoutActivatingCapital()
    {
        var scenario = Create();
        var before = scenario.Host.Snapshot;
        var project = new FoundationConstructionProject(1UL, scenario.RemoteEdge, Red,
            2_000_000UL, 800UL, 500UL);
        var ledger = new FoundationConstructionRuntimeState(new[] { project });
        var next = before.WithConstructions(ledger);
        var advanced = next.AdvanceRevision().WithWorkers(next.Workers);
        Assert.Same(ledger, advanced.Constructions);
        Assert.Equal(300UL, advanced.Constructions.Projects.Single().RemainingWorkMicro);
        Assert.Same(before.Transport, advanced.Transport);
        Assert.Same(before.Treasury, advanced.Treasury);
        Assert.False(before.Constructions.IsInitialized);
        var hasher = new CanonicalWorldStateHasher();
        Assert.NotEqual(hasher.Compute(before), hasher.Compute(next));
        var completed = next.WithConstructions(new FoundationConstructionRuntimeState(
            new[] { project with { CompletedWorkMicro = 800UL } }));
        Assert.True(completed.Constructions.Projects.Single().IsComplete);
        Assert.NotEqual(hasher.Compute(next), hasher.Compute(completed));
        Assert.Same(before.Transport, completed.Transport);
        Assert.Same(before.Treasury, completed.Treasury);
        Assert.Throws<ArgumentException>(() => new FoundationConstructionRuntimeState(
            new[] { project, project with { Id = 2UL } }));
        Assert.Throws<ArgumentException>(() => new FoundationConstructionRuntimeState(
            new[] { project with { CompletedWorkMicro = 801UL } }));
        Assert.Throws<InvalidOperationException>(() => next.WithConstructions(
            FoundationConstructionRuntimeState.Uninitialized));
    }

    [Fact]
    public void FixedUnitFreightIsPositiveMonotoneAndDeterministicAcrossExtremeCapital()
    {
        var s = Create();
        var edge = Edge(s);
        ulong natural = edge.FreePhysicalCostMicroPerWholeCargoUnit;
        ulong quote = edge.NaturalConstructionCostMoneyMicro!.Value;
        var investments = new[]
        {
            0UL, 1UL, quote / 2UL, quote,
            checked(quote * 2UL), checked(quote * 10UL),
            ulong.MaxValue
        }.Distinct().OrderBy(amount => amount).ToArray();

        ulong previous = natural;
        foreach (ulong amount in investments)
        {
            var row = edge with { CommittedInvestmentMoneyMicro = amount };
            ulong actual = FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(row);
            Assert.InRange(actual, 1UL, previous);
            Assert.Equal(actual,
                FoundationPhysicalAuthoritativeTransportProjector
                    .EffectiveUnitFreightCost(row));
            previous = actual;
        }
        Assert.Equal(natural,
            FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(edge with
                {
                    CommittedInvestmentMoneyMicro = 0UL
                }));
        Assert.Equal(1UL,
            FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(edge with
                {
                    CommittedInvestmentMoneyMicro = ulong.MaxValue,
                    FreePhysicalCostMicroPerWholeCargoUnit = 1UL
                }));
    }

    [Fact]
    public void FixedUnitFreightNeverDependsOnHistoricalCargoCapacityOrOwnerToll()
    {
        var s = Create();
        var edge = Edge(s);
        ulong quote = edge.NaturalConstructionCostMoneyMicro!.Value;
        var invested = edge with { CommittedInvestmentMoneyMicro = quote };
        ulong expected = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveUnitFreightCost(invested);

        var differentHistoricalCapacity = invested with
        {
            CargoMicroAtReferenceLoad = 1UL
        };
        var differentOwnerTariff = invested with
        {
            ExternalTariffMicroPerWholeCargoUnit = 987_654_321UL
        };
        Assert.Equal(expected,
            FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(differentHistoricalCapacity));
        Assert.Equal(expected,
            FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(differentOwnerTariff));
        Assert.True(expected < edge.FreePhysicalCostMicroPerWholeCargoUnit);
    }

    [Fact]
    public void MonetaryConstructionQuotesAreQuadraticWhileOperatingIsLinear()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var physical = fixture.Session.GeneratedWorld;
        var incidence = fixture.Incidence;
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            physical, incidence, Operation(), Money());
        Assert.True(seeded.Transport.HasMonetaryConstructionCosts);
        Assert.False(seeded.Transport.HasCommittedInvestments);
        Assert.All(seeded.Transport.Edges, edge =>
        {
            Assert.NotNull(edge.NaturalConstructionCostMoneyMicro);
            Assert.Null(edge.CommittedInvestmentMoneyMicro);
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                edge.OwnershipKind);
            Assert.Equal(0UL, edge.ExternalTariffMicroPerWholeCargoUnit);
        });
        var elevation = physical.StrategicPhysicalFields.Elevation;
        long sea = physical.StrategicPhysicalFields.SeaLevel.RawValue;
        var tileElevation = incidence.TileIncidences.Select(tile =>
            tile.IncidentCoarseCellIds.Aggregate(0L,
                (sum, coarse) => checked(sum +
                    elevation.GetRawValue(coarse))) /
                tile.IncidentCoarseCellIds.Count).ToArray();
        var byEdge = seeded.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        UInt128 scale = (UInt128)StrategicScalarField.Denominator;
        int nonFlatLand = 0;
        foreach (var crossing in incidence.FineTopology.Edges)
        {
            int a = checked((int)crossing.IncidentCellIds[0].Value - 1);
            int b = checked((int)crossing.IncidentCellIds[1].Value - 1);
            bool la = tileElevation[a] > sea;
            bool lb = tileElevation[b] > sea;
            var item = byEdge[crossing.Id];
            if (la && lb)
            {
                Int128 deltaSigned =
                    (Int128)tileElevation[a] - tileElevation[b];
                if (deltaSigned < 0) deltaSigned = -deltaSigned;
                UInt128 d = (UInt128)deltaSigned;
                if (d > 0) nonFlatLand++;
                Assert.Equal(checked((ulong)(1_000_000UL +
                    (UInt128)4_000_000UL * d / scale)),
                    item.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(checked((ulong)(2_000_000UL +
                    (UInt128)8_000_000UL * d * d / (scale * scale))),
                    item.NaturalConstructionCostMoneyMicro);
            }
            else if (la || lb)
            {
                Assert.Equal(20_000_000UL,
                    item.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(4_000_000UL,
                    item.NaturalConstructionCostMoneyMicro);
            }
            else
            {
                Assert.Equal(100_000_000UL,
                    item.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(6_000_000UL,
                    item.NaturalConstructionCostMoneyMicro);
            }
        }
        Assert.True(nonFlatLand > 0);
    }

    [Fact]
    public void AllReadyTurnDebitsCashAndCreditsRemoteEdgeWithoutWorkersOrMaterials()
    {
        var s = Create();
        var before = s.Host.Snapshot;
        var previousHash = s.Host.StateHash;
        var previousDemand =
            FoundationPhysicalWorkforceEconomicProjector.Project(before.Workers);
        BigInteger oldCash = before.Treasury.Get(Red).CashScaled;
        var original = Edge(s);
        var initialProjected = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(before, s.Host.Incidence);
        ulong initialCapacity = initialProjected.Scales
            .Single(row => row.Edge == s.RemoteEdge).CargoMicroAtReferenceLoad;
        ulong initialCost = initialProjected.Roads
            .Single(row => row.Edge == s.RemoteEdge)
            .PhysicalCostMicroPerWholeCargoUnit;
        Assert.Equal(original.CargoMicroAtReferenceLoad, initialCapacity);
        const ulong spend = 175_000UL;

        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, spend);
        Assert.Equal(1, s.Host.PendingOrderCount);
        Assert.Equal(previousHash, s.Host.StateHash);
        Assert.Null(s.Host.TryCloseTurn()); // Human not ready.
        Assert.Same(before, s.Host.Snapshot);

        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        Assert.Empty(receipt.Events);
        var ev = Assert.Single(receipt.InfrastructureInvestments);
        Assert.Equal(Red, ev.Investor);
        Assert.Equal(s.RemoteEdge, ev.Edge);
        Assert.Equal(spend, ev.MoneyMicro);
        Assert.Equal(14U, receipt.Hash.FormatVersion);
        Assert.Equal(before.Revision + 1UL, s.Host.Snapshot.Revision);
        Assert.Equal(receipt.Hash, s.Host.StateHash);
        Assert.Equal(spend, Edge(s).CommittedInvestmentMoneyMicro);
        Assert.Equal(original.NaturalConstructionCostMoneyMicro,
            Edge(s).NaturalConstructionCostMoneyMicro);
        Assert.Equal(original.FreePhysicalCostMicroPerWholeCargoUnit,
            Edge(s).FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(original.CargoMicroAtReferenceLoad,
            Edge(s).CargoMicroAtReferenceLoad); // immutable natural baseline
        var investedProjection = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(s.Host.Snapshot, s.Host.Incidence);
        Assert.Equal(initialCapacity, investedProjection.Scales
            .Single(row => row.Edge == s.RemoteEdge).CargoMicroAtReferenceLoad);
        ulong effectiveCost = investedProjection.Roads
            .Single(row => row.Edge == s.RemoteEdge)
            .PhysicalCostMicroPerWholeCargoUnit;
        Assert.True(effectiveCost < initialCost,
            "Capital already debited from the real treasury must lower unit freight.");
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            Edge(s).OwnershipKind);
        Assert.Equal(Red, Edge(s).InfrastructureOwner);
        Assert.Equal(0UL, Edge(s).ExternalTariffMicroPerWholeCargoUnit);

        var amount = GlobalArenaMoneyScale.FromMoneyMicro(spend);
        Assert.Equal(oldCash - amount,
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(amount,
            s.Host.Snapshot.Treasury.Get(Red)
                .TotalInfrastructureInvestmentScaled);
        Assert.Equal(BigInteger.Zero,
            s.Host.Snapshot.Treasury.Get(Blue)
                .TotalInfrastructureInvestmentScaled);
        Assert.Same(before.Workers, s.Host.Snapshot.Workers);
        Assert.Equal(previousDemand.DemandSites.ToArray(),
            FoundationPhysicalWorkforceEconomicProjector
                .Project(s.Host.Snapshot.Workers).DemandSites.ToArray());
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(0, s.Host.PendingOrderCount);
    }

    [Fact]
    public void RealCashFundingCanBeSpreadAcrossTurnsToReduceUnitFreight()
    {
        var s = Create();
        ulong cost = Edge(s).NaturalConstructionCostMoneyMicro!.Value;
        Assert.True(cost > 100_000UL);
        BigInteger initial = s.Host.Snapshot.Treasury.Get(Red).CashScaled;
        for (int turn = 1; turn <= 2; turn++)
        {
            ulong part = turn == 1 ? 100_000UL : cost - 100_000UL;
            s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, part);
            s.Host.SetHumanReady(Red);
            var receipt = s.Host.TryCloseTurn();
            Assert.NotNull(receipt);
            Assert.True(receipt!.ReplayMatched);
            Assert.Equal(14U, receipt.Hash.FormatVersion);
        }
        Assert.Equal(cost, Edge(s).CommittedInvestmentMoneyMicro);
        var fundedProjection = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(s.Host.Snapshot, s.Host.Incidence);
        ulong funded = fundedProjection.Roads
            .Single(row => row.Edge == s.RemoteEdge)
            .PhysicalCostMicroPerWholeCargoUnit;
        Assert.Equal(Edge(s).CargoMicroAtReferenceLoad,
            fundedProjection.Scales.Single(row => row.Edge == s.RemoteEdge)
                .CargoMicroAtReferenceLoad);
        Assert.True(funded <
            Edge(s).FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(initial - GlobalArenaMoneyScale.FromMoneyMicro(cost),
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(cost),
            s.Host.Snapshot.Treasury.Get(Red)
                .TotalInfrastructureInvestmentScaled);
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            Edge(s).OwnershipKind);
        Assert.Equal(Red, Edge(s).InfrastructureOwner);
        // Once the original construction quote is fully funded, further
        // upgrades remain possible on the SAME physical edge.
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, cost);
        s.Host.SetHumanReady(Red);
        var upgraded = s.Host.TryCloseTurn();
        Assert.NotNull(upgraded);
        Assert.True(upgraded!.ReplayMatched);
        Assert.Single(upgraded.InfrastructureInvestments);
        Assert.Equal(checked(cost * 2UL),
            Edge(s).CommittedInvestmentMoneyMicro);
        var upgradedProjection = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(s.Host.Snapshot, s.Host.Incidence);
        ulong afterUpgrade = upgradedProjection.Roads
            .Single(row => row.Edge == s.RemoteEdge)
            .PhysicalCostMicroPerWholeCargoUnit;
        Assert.True(afterUpgrade < funded);
        Assert.Equal(Edge(s).CargoMicroAtReferenceLoad,
            upgradedProjection.Scales.Single(row => row.Edge == s.RemoteEdge)
                .CargoMicroAtReferenceLoad);
        Assert.Equal(initial - GlobalArenaMoneyScale.FromMoneyMicro(cost * 2UL),
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            Edge(s).OwnershipKind);
        Assert.Equal(Red, Edge(s).InfrastructureOwner);
    }

    [Fact]
    public void ProducingWorkerCanChangeOccupationWhileCivilizationFundsRemoteEdge()
    {
        var s = Create();
        var before = s.Host.Snapshot;
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 50_000UL);
        s.Host.QueueHumanActivity(Red, s.Producer,
            PhysicalWorkerActivity.Other);
        Assert.Equal(2, s.Host.PendingOrderCount);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Single(receipt!.Events);
        Assert.Single(receipt.InfrastructureInvestments);
        Assert.Equal(before.Revision + 2UL, s.Host.Snapshot.Revision);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(14U, s.Host.StateHash.FormatVersion);
        Assert.Equal(PhysicalWorkerActivity.Other,
            s.Host.Snapshot.Workers.Workers.Single(w =>
                w.Id == s.Producer).Activity);
        Assert.Equal(50_000UL, Edge(s).CommittedInvestmentMoneyMicro);
    }

    [Fact]
    public void MultipleEdgeCapitalOrdersRespectBudgetCancellationAndSingleTurnJournal()
    {
        var s = Create(redOpeningMoneyMicro: 250_000UL);
        var second = s.Host.Snapshot.Transport.Edges.First(row =>
            row.Edge != s.RemoteEdge &&
            row.CargoMicroAtReferenceLoad != ulong.MaxValue &&
            row.NaturalConstructionCostMoneyMicro is > 0UL &&
            row.OwnershipKind == PhysicalTransportOwnershipKind.NoInfrastructure).Edge;
        var before = s.Host.Snapshot;
        var initialHash = s.Host.StateHash;
        var initialCash = before.Treasury.Get(Red).CashScaled;
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 100_000UL);
        s.Host.QueueHumanMonetaryInvestment(Red, second, 100_000UL);
        var pending = s.Host.GetHumanMonetaryInvestments(Red);
        Assert.Equal(2, pending.Count);
        Assert.Equal(200_000UL, pending.Aggregate(0UL, (total, order) => checked(total + order.MoneyMicro)));
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(Red,
                before.Transport.Edges.First(row =>
                    row.Edge != s.RemoteEdge && row.Edge != second &&
                    row.CargoMicroAtReferenceLoad != ulong.MaxValue &&
                    row.NaturalConstructionCostMoneyMicro is > 0UL &&
                    row.OwnershipKind == PhysicalTransportOwnershipKind.NoInfrastructure).Edge,
                100_000UL));
        Assert.Same(before, s.Host.Snapshot);
        Assert.Equal(initialHash, s.Host.StateHash);
        Assert.True(s.Host.CancelHumanMonetaryInvestment(Red, second));
        Assert.Single(s.Host.GetHumanMonetaryInvestments(Red));
        s.Host.QueueHumanMonetaryInvestment(Red, second, 100_000UL);
        Assert.Equal(2, s.Host.PendingOrderCount);

        s.Host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            s.Host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(2, receipt.InfrastructureInvestments.Count);
        Assert.Contains(receipt.InfrastructureInvestments,
            ev => ev.Edge == s.RemoteEdge && ev.MoneyMicro == 100_000UL);
        Assert.Contains(receipt.InfrastructureInvestments,
            ev => ev.Edge == second && ev.MoneyMicro == 100_000UL);
        Assert.Equal(initialCash - GlobalArenaMoneyScale.FromMoneyMicro(200_000UL),
            s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(100_000UL, s.Host.Snapshot.Transport.Edges
            .Single(row => row.Edge == second).CommittedInvestmentMoneyMicro);
        Assert.Empty(s.Host.GetHumanMonetaryInvestments(Red));
    }

    [Fact]
    public void CancelledCapitalLeavesTreasuryAndEdgeIntactWhenTurnCloses()
    {
        var s = Create(redOpeningMoneyMicro: 300_000UL);
        var original = s.Host.Snapshot;
        var hash = s.Host.StateHash;
        var cash = original.Treasury.Get(Red).CashScaled;
        var originalCapital = Edge(s).CommittedInvestmentMoneyMicro;

        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 100_000UL);
        Assert.Equal(1, s.Host.PendingOrderCount);
        Assert.Same(original, s.Host.Snapshot);
        Assert.True(s.Host.CancelHumanMonetaryInvestment(Red, s.RemoteEdge));
        Assert.False(s.Host.CancelHumanMonetaryInvestment(Red, s.RemoteEdge));
        Assert.Equal(0, s.Host.PendingOrderCount);
        Assert.Same(original, s.Host.Snapshot);
        Assert.Equal(hash, s.Host.StateHash);

        s.Host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            s.Host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Empty(receipt.InfrastructureInvestments);
        Assert.Equal(cash, s.Host.Snapshot.Treasury.Get(Red).CashScaled);
        Assert.Equal(originalCapital, Edge(s).CommittedInvestmentMoneyMicro);
        Assert.Same(original.Workers, s.Host.Snapshot.Workers);
    }

    [Fact]
    public void NoOverdraftForgedFundsOrDuplicateInvestmentCanBePublished()
    {
        var s = Create(redOpeningMoneyMicro: 300_000UL);
        var snapshot = s.Host.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(
                Red, s.RemoteEdge, 300_001UL));
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(
                Blue, s.RemoteEdge, 1UL));
        Assert.Equal(0, s.Host.PendingOrderCount);
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 150_000UL);
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(
                Red, s.RemoteEdge, 1UL));
        Assert.True(s.Host.CancelHumanMonetaryInvestment(
            Red, s.RemoteEdge));
        Assert.Same(snapshot, s.Host.Snapshot);
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 150_000UL);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        var ev = Assert.Single(receipt!.InfrastructureInvestments);
        var executor = new FoundationMonetaryInfrastructureEventExecutor(
            s.Host.Session.GeneratedWorld, s.Host.Incidence);
        var context = new SimulationContext(
            ev.Id.Turn, new SimulationSeed(712UL));
        var independent = executor.Execute(snapshot, ev, context);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(independent));
        var forged = new FoundationMonetaryInfrastructureInvestedEvent(
            ev.Id, ev.Investor, ev.Edge, 300_001UL,
            ev.ExpectedRevision);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(snapshot, forged, context));
        var foreign = new FoundationMonetaryInfrastructureInvestedEvent(
            ev.Id, Blue, ev.Edge, ev.MoneyMicro, ev.ExpectedRevision);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(snapshot, foreign, context));
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(independent, ev, context));
    }

    [Fact]
    public void V12QuotesStayUnmodifiedAndOpeningIsExplicitNotMarketRevenue()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var previous = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence, Operation());
        var v12 = fixture.Snapshot.DeployInitialPhysicalTransport(
            previous.Transport, fixture.Session.GeneratedWorld,
            fixture.Incidence);
        Assert.Equal(12U, new CanonicalWorldStateHasher()
            .Compute(v12).FormatVersion);
        Assert.False(v12.Transport.HasMonetaryConstructionCosts);
        Assert.All(v12.Transport.Edges, row =>
            Assert.Null(row.NaturalConstructionCostMoneyMicro));
        Assert.False(v12.Treasury.IsInitialized);
        var v13seed = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence,
            Operation(), Money());
        var v13 = fixture.Snapshot.DeployInitialPhysicalTransport(
            v13seed.Transport, fixture.Session.GeneratedWorld,
            fixture.Incidence);
        Assert.Equal(13U, new CanonicalWorldStateHasher()
            .Compute(v13).FormatVersion);
        Assert.False(v13.Treasury.IsInitialized);
        Assert.Throws<InvalidOperationException>(() =>
            v13.RecordMonetaryInfrastructureInvestment(
                Red, v13.Transport.Edges[0].Edge, 1UL));
        Assert.Equal(GlobalArenaMoneyScale.ExactScaledPerUnit,
            FoundationPhysicalEconomicAccountingProposal.MoneyScale);
        Assert.Equal(GlobalArenaMoneyScale.ExactScaledPerUnit,
            GlobalArenaMoneyScale.FromMoneyMicro(1_000_000UL));
        Assert.Throws<InvalidOperationException>(() =>
            v12.DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(
                    new[] { (Red, 0UL), (Blue, 0UL) })));
        Assert.Throws<ArgumentException>(() =>
            CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 0UL), (Red, 0UL) }));
        var zero = v13.DeployInitialTreasury(
            CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 0UL), (Blue, 0UL) }));
        Assert.Equal(13U, new CanonicalWorldStateHasher()
            .Compute(zero).FormatVersion);
        Assert.Equal(BigInteger.Zero, zero.Treasury.Get(Red).CashScaled);
        Assert.Equal(BigInteger.Zero,
            zero.Treasury.Get(Blue).CashScaled);
    }
    [Fact]
    public void NaturalCoastIsAnInvestableGargleneckButOpenSeaIsNeverConstructible()
    {
        var s = Create();
        var start = s.Host.Snapshot;
        var sea = start.Transport.Edges.First(row =>
            row.CargoMicroAtReferenceLoad == ulong.MaxValue);
        var coast = start.Transport.Edges.First(row =>
            row.FreePhysicalCostMicroPerWholeCargoUnit == 20_000_000UL &&
            row.CargoMicroAtReferenceLoad == 100_000UL);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            coast.OwnershipKind);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            sea.OwnershipKind);
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(Red, sea.Edge, 1UL));
        Assert.Equal(0, s.Host.PendingOrderCount);
        Assert.Same(start, s.Host.Snapshot);
        Assert.Equal(ulong.MaxValue,
            FoundationPhysicalAuthoritativeTransportProjector
                .FromWorld(start, s.Host.Incidence).Scales
                .Single(x => x.Edge == sea.Edge).CargoMicroAtReferenceLoad);

        // No port entity or construction mode: invest in the actual coast
        // edge using the same monetary command as every terrestrial edge.
        const ulong spend = 1_000_000UL;
        s.Host.QueueHumanMonetaryInvestment(Red, coast.Edge, spend);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        Assert.Single(receipt.InfrastructureInvestments);
        var after = s.Host.Snapshot;
        Assert.Equal(start.Transport.Edges.Count, after.Transport.Edges.Count);
        var built = after.Transport.Edges.Single(x => x.Edge == coast.Edge);
        Assert.Equal(spend, built.CommittedInvestmentMoneyMicro);
        Assert.Equal(coast.CargoMicroAtReferenceLoad,
            built.CargoMicroAtReferenceLoad);
        var projected = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(after, s.Host.Incidence);
        Assert.Equal(coast.CargoMicroAtReferenceLoad,
            projected.Scales.Single(x => x.Edge == coast.Edge)
                .CargoMicroAtReferenceLoad);
        ulong effectiveCost = projected.Roads.Single(x => x.Edge == coast.Edge)
            .PhysicalCostMicroPerWholeCargoUnit;
        Assert.True(effectiveCost < coast.FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            built.OwnershipKind);
        Assert.Equal(Red, built.InfrastructureOwner);
        Assert.Equal(0UL, built.ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(ulong.MaxValue,
            FoundationPhysicalAuthoritativeTransportProjector
                .FromWorld(after, s.Host.Incidence).Scales
                .Single(x => x.Edge == sea.Edge).CargoMicroAtReferenceLoad);
        Assert.Throws<InvalidOperationException>(() =>
            after.Transport.RecordMoneyInvestment(Red, sea.Edge, 1UL));
    }

    [Fact]
    public void TheSameInvestmentFunctionHasDiminishingMarginalCapacityReturns()
    {
        var s = Create();
        var natural = s.Host.Snapshot.Transport.Edges.First(x =>
            x.CargoMicroAtReferenceLoad != ulong.MaxValue &&
            x.NaturalConstructionCostMoneyMicro is > 0UL);
        ulong cost = natural.NaturalConstructionCostMoneyMicro!.Value;
        ulong quarter = cost / 4UL;
        Assert.True(quarter > 0UL);
        ulong k0 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural);
        ulong k1 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = quarter });
        ulong k2 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = quarter * 2UL });
        ulong k4 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = cost });
        Assert.True(k0 < k1 && k1 < k2 && k2 < k4);
        // For equal-sized investment increments the earlier increment
        // must return more additional capacity than the later increment.
        Assert.True(k1 - k0 > (k2 - k1));
        Assert.True(k2 - k0 > k4 - k2);
        Assert.Equal(k0 + k0 / 2UL, k4);
        // Unlike R48, the curve continues past the original construction
        // quote rather than approaching a 2x hard ceiling.
        ulong k8 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = cost * 8UL });
        ulong k16 = FoundationPhysicalAuthoritativeTransportProjector
            .EffectiveReferenceCargo(natural with
            { CommittedInvestmentMoneyMicro = cost * 16UL });
        Assert.True(k16 > k8 && k8 > k4);
        Assert.True(k16 > k0 * 2UL);
        Assert.True(k8 - k4 > 0UL);
        // The growth is sublinear, not an artificial cap.
        Assert.True(k16 - k8 < k8 - k0);
    }

    [Fact]
    public void ExactIntegerInvestmentCurveNeverLosesCapacityAtRoundingBoundaries()
    {
        var s = Create();
        var natural = s.Host.Snapshot.Transport.Edges.First(e =>
            e.CargoMicroAtReferenceLoad != ulong.MaxValue);
        var edge = natural with
        {
            CargoMicroAtReferenceLoad = 10_000UL,
            NaturalConstructionCostMoneyMicro = 7UL
        };
        ulong previous = 0UL;
        for (ulong investment = 0UL; investment <= 500UL; investment++)
        {
            ulong current = FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveReferenceCargo(edge with
                { CommittedInvestmentMoneyMicro = investment });
            if (investment > 0UL)
                Assert.True(current >= previous,
                    "More verified capital may never lower transport capacity.");
            previous = current;
        }
        Assert.True(previous > 20_000UL,
            "Repeated improvements must overcome two times natural throughput.");
        // An independent arbitrary-integer sanity check for the square root.
        for (int i = 0; i < 100; i++)
        {
            BigInteger n = (BigInteger)i * i + i;
            BigInteger root = FoundationPhysicalAuthoritativeTransportProjector
                .IntegerSquareRoot(n);
            Assert.True(root * root <= n &&
                (root + 1) * (root + 1) > n);
        }
    }

    [Fact]
    public void InvestedEdgeHasOneOwnerAndForeignReinvestmentCannotSeizeTitle()
    {
        var s = Create(redOpeningMoneyMicro: 25_000_000UL,
            blueOpeningMoneyMicro: 25_000_000UL);
        var natural = Edge(s);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            natural.OwnershipKind);
        const ulong first = 120_000UL;
        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, first);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);

        var owned = Edge(s);
        Assert.Equal(PhysicalTransportOwnershipKind.Owned,
            owned.OwnershipKind);
        Assert.Equal(Red, owned.InfrastructureOwner);
        Assert.Equal(0UL, owned.ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(first, owned.CommittedInvestmentMoneyMicro);
        var projected = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(s.Host.Snapshot, s.Host.Incidence).Roads
            .Single(r => r.Edge == s.RemoteEdge);
        Assert.Equal(FoundationInfrastructureOwnershipKind.Owned,
            projected.OwnershipKind);
        Assert.Equal(Red, projected.InfrastructureOwner);
        Assert.Equal(0UL, projected.ExternalTariffMicroPerWholeCargoUnit);
        var originalHash = s.Host.StateHash;
        var originalSnapshot = s.Host.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanMonetaryInvestment(Blue, s.RemoteEdge, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            originalSnapshot.RecordMonetaryInfrastructureInvestment(
                Blue, s.RemoteEdge, 1UL));
        Assert.Equal(originalHash, s.Host.StateHash);
        Assert.Same(originalSnapshot, s.Host.Snapshot);
        Assert.Equal(0, s.Host.PendingOrderCount);

        s.Host.QueueHumanMonetaryInvestment(Red, s.RemoteEdge, 2UL);
        s.Host.SetHumanReady(Red);
        var reinvested = s.Host.TryCloseTurn();
        Assert.NotNull(reinvested);
        Assert.True(reinvested!.ReplayMatched);
        Assert.Equal(Red, Edge(s).InfrastructureOwner);
        Assert.Equal(first + 2UL, Edge(s).CommittedInvestmentMoneyMicro);
        Assert.Equal(0UL, Edge(s).ExternalTariffMicroPerWholeCargoUnit);
    }

}
