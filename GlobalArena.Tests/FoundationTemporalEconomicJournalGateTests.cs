using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationTemporalEconomicJournalGateTests
{
    private static (WorldState World, PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Destination,
        PhysicalTacticalWorkerMoveEventExecutor Validator,
        PhysicalTacticalIncidenceMap Incidence,
        FoundationFineFirstPhysicalGeography Fine) Fixture()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var worker = demo.Session.Snapshot.Workers.Workers.First(w =>
            w.Activity == PhysicalWorkerActivity.Producing);
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            demo.Session.GeneratedWorld, demo.Incidence);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            demo.Session.GeneratedWorld, demo.Incidence, fine);
        var validator = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, demo.Incidence, fine, signature);
        var location = worker.TacticalTileId!.Value;
        var adjacent = demo.Incidence.FineTopology.Edges
            .Where(edge => edge.IncidentCellIds.Contains(
                location.FineStrategicCellId))
            .Select(edge => edge.IncidentCellIds.Single(
                id => id != location.FineStrategicCellId))
            .Where(id => fine.CanTraverseOnFoot(checked((int)id.Value - 1)))
            .Select(id => new PhysicalTacticalTileId(
                demo.Incidence.Refinement.FineParameters, id))
            .First(tile =>
            {
                try { return validator.CheckEdge(location, tile).HasValue; }
                catch (InvalidOperationException) { return false; }
            });
        var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            demo.Session.GeneratedWorld, demo.Incidence, fine,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var bound = demo.Session.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, demo.Session.GeneratedWorld, demo.Incidence);
        return (bound, worker, adjacent, validator, demo.Incidence, fine);
    }



    private static FoundationActivityCommodityDemandPolicy Demand()
    {
        var items = new[] { new CommodityId(1U), new CommodityId(2U) };
        return new FoundationActivityCommodityDemandPolicy(
            items, Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => items.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
    }

    private static (
        WorldState Before, WorldState After,
        FoundationEdgeTravelTurnEvent Event,
        PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Destination)
        FirstTurn()
    {
        var (world, worker, destination, movement, _, _) = Fixture();
        var before = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var phase = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var command = new FoundationEdgeTravelTurnCommand(
            new CommandId(turn, 1UL), worker.Owner, worker.Id,
            destination, isStart: true);
        var resolved = phase.Resolve(
            new TurnResolutionInput(before, new ISimulationCommand[] { command },
                context), before);
        return (before, resolved.ResultingWorldState,
            Assert.IsType<FoundationEdgeTravelTurnEvent>(
                Assert.Single(resolved.Events)), worker, destination);
    }




    [Fact]
    public void R212HostPublishesOnlyAllReadyAndInterruptsActiveOccupation()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        demo.Session.AcceptFineFirstOpening(beginning, incidence, fine, signature);
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
            beginning.Civilizations.Civilizations,
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
            enableMonetaryInfrastructure: true, settlementPolicy: Policy(beginning.Civilizations.Civilizations),
            fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL);
        Assert.True(host.PersistentOrdersEnabled);
        var owner = worker.Owner;
        var foreign = beginning.Civilizations.Civilizations.First(c => c != owner);
        host.QueueHumanPersistentEdit(owner, new FoundationPersistentOrderEdit(owner, worker.Id,
            FoundationPersistentOrderEditKind.Insert, OrderKind: FoundationWorkerOrderKind.Occupation,
            Occupation: PhysicalWorkerOccupation.Agriculture));
        Assert.Single(host.GetHumanPersistentQueues(owner).Single(q => q.Worker == worker.Id).Orders);
        Assert.Empty(host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanPersistentEdit(foreign,
            new FoundationPersistentOrderEdit(foreign, worker.Id, FoundationPersistentOrderEditKind.Insert,
                OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Mining)));
        host.SetHumanReady(owner);
        Assert.Null(host.TryCloseTurn());
        Assert.Same(beginning, host.Snapshot);
        host.SetHumanReady(foreign);
        var first = host.TryCloseTurn();
        Assert.NotNull(first);
        Assert.NotNull(first!.PersistentTimeline);
        Assert.Single(first.PersistentEdits);
        Assert.Equal(1UL, host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).ActiveOrderId);
        Assert.Equal(0, host.PendingOrderCount);
        // Interruption is staged privately; it removes the active head, not
        // the worker, and leaves no implicit productive activity.
        var interrupted = host.Snapshot.InterruptWorkerOrder(owner, worker.Id, 1UL);
        Assert.Empty(interrupted.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(PhysicalWorkerActivity.Other, interrupted.Workers.Workers.Single(w => w.Id == worker.Id).Activity);
        host.QueueHumanPersistentEdit(owner, new FoundationPersistentOrderEdit(owner, worker.Id,
            FoundationPersistentOrderEditKind.Interrupt, OrderId: 1UL));
        Assert.Empty(host.GetHumanPersistentQueues(owner).Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(1UL, host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).ActiveOrderId);
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanPersistentEdit(owner,
            new FoundationPersistentOrderEdit(owner, worker.Id, FoundationPersistentOrderEditKind.Interrupt, OrderId: 1UL)));
    }

    [Fact]
    public void R217BirthsAfterEconomicCloseAreAtomicAndReplayable()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), Policy(beginning.Civilizations.Civilizations),
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, enableDemographicGrowth: true);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Agriculture))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        var batch = Assert.IsType<FoundationPhysicalBirthBatchEvent>(resolved.Events.Last());
        Assert.IsType<FoundationTemporalSalesSettledEvent>(resolved.Events[^2]);
        Assert.Equal(beginning.Civilizations.Civilizations.Count, batch.Newborns.Count);
        Assert.Equal(beginning.Workers.Workers.Count + batch.Newborns.Count, next.Workers.Workers.Count);
        Assert.Equal(next.Workers.Workers.Count, next.WorkerOrders.Queues.Count);
        Assert.Equal(21U, new CanonicalWorldStateHasher().Compute(next).FormatVersion);
        var presence = FoundationPhysicalPresenceDemographicProjector.Project(beginning, timeline.Slices.Select(s => new FoundationWorkerTemporalNodeSlice(s.Worker, s.Node, s.DurationMicro, s.Activity == PhysicalWorkerActivity.Producing ? s.DurationMicro : 0U, s.Activity, s.Occupation)).ToArray(), Demand());
        var expected = FoundationPhysicalPresenceDemographicProjector.PlanBirths(beginning, presence,
            beginning.Civilizations.Civilizations, context.Turn.Value);
        Assert.Equal(expected.Births.Select(b => b.NatalNode), batch.Newborns.Select(w => w.NatalNode!.Value));
        foreach (var child in batch.Newborns)
        {
            Assert.Equal(child.NatalNode, child.TacticalTileId);
            Assert.Equal(PhysicalWorkerOccupation.Unassigned, child.Occupation);
            Assert.Empty(next.WorkerOrders.Queues.Single(q => q.Worker == child.Id).Orders);
            Assert.DoesNotContain(timeline.Slices, s => s.Worker == child.Id);
            Assert.Equal(child.NatalNode, child.WithTacticalAssignment(child.TacticalTileId!.Value,
                child.CellId, PhysicalWorkerActivity.Producing, PhysicalWorkerOccupation.Agriculture).NatalNode);
        }
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        foreach (var events in new[] { resolved.Events.Take(resolved.Events.Count - 1).ToArray(),
            resolved.Events.Append(batch).ToArray(),
            resolved.Events.Take(resolved.Events.Count - 1).Append(batch with { ScheduleDigest = "forged" }).ToArray() })
        {
            var log = new SimulationEventLog(events.Select((e, i) => new SimulationEventLogEntry((ulong)i + 1, e, true, true)).ToArray());
            Assert.ThrowsAny<Exception>(() => phase.ReplayPersistentJournal(beginning, log, context));
        }
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var second = phase.Resolve(new TurnResolutionInput(next, Array.Empty<ISimulationCommand>(), secondContext), next);
        var secondTimeline = Assert.Single(second.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        foreach (var child in batch.Newborns)
            Assert.Equal(1_000_000L, secondTimeline.Slices.Where(s => s.Worker == child.Id).Sum(s => (long)s.DurationMicro));
        Assert.Equal(hash.Compute(second.ResultingWorldState), hash.Compute(phase.ReplayPersistentJournal(next, second.EventLog, secondContext)));
    }

    [Fact]
    public void R211CollectiveFundingAndTimelineSettleOneMarketAndReplay()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var tile = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        // Use a real signed fine-first monetary world for collective admission.
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld,
                incidence, fine, signature);
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), Policy(beginning.Civilizations.Civilizations),
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Agriculture))
        };
        var resolved = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var next = resolved.ResultingWorldState;
        var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        Assert.Single(resolved.Events.OfType<FoundationFundedWorkerIntentEvent>());
        Assert.Equal(1UL, next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(next.Constructions.Projects.Single().IsActivated);
        var slices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
        ulong expectedGoods = timeline.Slices.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
            .GroupBy(s => (s.Worker, s.Node, s.Occupation))
            .Aggregate(0UL, (sum, g) => checked(sum + (ulong)((UInt128)beginning.Workers.Workers
                .Single(w => w.Id == g.Key.Worker).OutputMicroPerTurn *
                (ulong)g.Sum(s => (long)s.DurationMicro) / 1_000_000UL)));
        Assert.Equal(expectedGoods, sale.ProducedGoodsMicro);
        Assert.Equal(sale.GrossSalesScaled, sale.TotalCivilizationCreditsScaled + sale.PhysicalFreightScaled);
        foreach (var credit in sale.Credits)
            Assert.Equal(beginning.Treasury.Get(credit.Civilization).CashScaled + credit.CashCreditScaled -
                (credit.Civilization == worker.Owner ? GlobalArenaMoneyScale.FromMoneyMicro(600_000UL) : BigInteger.Zero),
                next.Treasury.Get(credit.Civilization).CashScaled);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        var duplicate = new SimulationEventLog(resolved.Events.Take(3).Append(timeline).Append(sale)
            .Select((e, i) => new SimulationEventLogEntry((ulong)i + 1, e, true, true)).ToArray());
        Assert.ThrowsAny<Exception>(() => phase.ReplayPersistentJournal(beginning, duplicate, context));
        Assert.Empty(beginning.Constructions.Projects);
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Fact]
    public void R210PersistentBuildUsesArrivalRemainderAndContinuesWithoutNewOrder()
    {
        var (world, worker, adjacent, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(adjacent.FineStrategicCellId) &&
            !e.IncidentCellIds.Contains(worker.TacticalTileId!.Value.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var initial = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                world.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var funding = new FoundationFundedWorkerIntentJournal(new FoundationConstructionFundingExecutor(
            new FoundationMonetaryInfrastructureEventExecutor(demo.Session.GeneratedWorld, incidence), 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var (funded, _) = funding.Record(initial, new EventId(new CommandId(context.Turn, 1UL), 1UL),
            worker.Owner, edge, 1_200_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }, context);
        funded = funded.InsertWorkerOrder(worker.Owner, worker.Id, 1, FoundationWorkerOrderKind.Occupation,
            occupation: PhysicalWorkerOccupation.Agriculture);
        var scheduler = new FoundationPersistentWorkerTurnScheduler(new FoundationConstructionApproachRouter(
            demo.Session.GeneratedWorld, incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)));
        var hash = new CanonicalWorldStateHasher();
        var state = funded;
        for (ulong turn = 1; turn <= 3; turn++)
        {
            context = new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL));
            var (next, ev) = scheduler.Record(state, context);
            Assert.Equal(hash.Compute(next), hash.Compute(scheduler.Execute(state, ev, context)));
            Assert.Throws<InvalidOperationException>(() => scheduler.Execute(next, ev, context));
            Assert.All(ev.Slices.GroupBy(x => x.Worker), g => Assert.Equal(1_000_000L, g.Sum(x => (long)x.DurationMicro)));
            var slices = ev.Slices.Where(x => x.Worker == worker.Id).ToArray();
            Assert.Equal(turn == 1 ? 0UL : turn == 2 ? 600_000UL : 1_200_000UL,
                next.Constructions.Projects.Single().CompletedWorkMicro);
            Assert.Equal(turn == 3, next.Constructions.Projects.Single().IsActivated);
            if (turn == 2)
            {
                Assert.Equal(400_000U, slices[0].DurationMicro);
                Assert.Equal(600_000U, slices[1].DurationMicro);
                Assert.Equal(1UL, slices[1].ProjectId);
            }
            if (turn == 3)
            {
                Assert.Equal(600_000U, slices[0].DurationMicro);
                Assert.Equal(400_000U, slices[1].DurationMicro);
                Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
                Assert.Equal(2UL, next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).ActiveOrderId);
            }
            Assert.Equal(funded.Treasury.Get(worker.Owner).CashScaled, next.Treasury.Get(worker.Owner).CashScaled);
            state = next;
        }
    }

    [Fact]
    public void R209RemoteBuildHeadAutomaticallyApproachesSiteAcrossTurns()
    {
        var (world, worker, adjacent, movement, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            demo.Session.GeneratedWorld, incidence, fine);
        var source = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(adjacent.FineStrategicCellId) &&
            !e.IncidentCellIds.Contains(source.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var initial = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                world.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var funding = new FoundationFundedWorkerIntentJournal(new FoundationConstructionFundingExecutor(
            new FoundationMonetaryInfrastructureEventExecutor(demo.Session.GeneratedWorld, incidence), 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var (funded, _) = funding.Record(initial, new EventId(new CommandId(context.Turn, 1UL), 1UL),
            worker.Owner, edge, 1_600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }, context);
        var policy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var router = new FoundationConstructionApproachRouter(demo.Session.GeneratedWorld, incidence, fine, signature, policy);
        var plan = router.Plan(funded, worker.Id);
        Assert.Equal(FoundationConstructionApproachStatus.RouteAvailable, plan.Status);
        Assert.Equal(source, plan.Path[0]);
        Assert.Contains(plan.Path.Last().FineStrategicCellId, crossing.IncidentCellIds);
        Assert.Equal(new BigInteger(1_400_000UL), plan.RemainingTravelMicro);
        var hasher = new CanonicalWorldStateHasher();
        var (first, receipt) = router.Advance(funded, worker.Id);
        Assert.NotNull(receipt);
        Assert.Equal(400_000UL, first.EdgeTravels.Pending.Single().RemainingMicro);
        Assert.Equal(FoundationConstructionApproachStatus.CrossingInProgress, router.Plan(first, worker.Id).Status);
        Assert.Equal(hasher.Compute(first), hasher.Compute(router.Advance(funded, worker.Id).State));
        var (arrived, second) = router.Advance(first, worker.Id);
        Assert.NotNull(second);
        Assert.Empty(arrived.EdgeTravels.Pending);
        Assert.Equal(FoundationConstructionApproachStatus.AtSite, router.Plan(arrived, worker.Id).Status);
        Assert.Same(arrived, router.Advance(arrived, worker.Id).State);
        Assert.Equal(0UL, arrived.Constructions.Projects.Single().CompletedWorkMicro);
        Assert.Same(funded.Treasury, arrived.Treasury);
        Assert.Same(funded.WorkerOrders, arrived.WorkerOrders);
        Assert.Equal(FoundationConstructionApproachStatus.NoBuildHead,
            router.Plan(initial, worker.Id).Status);
    }

    [Fact]
    public void FundWorkAndCommissionAcrossCyclesUsingOnlyOpeningInfrastructure()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        // Retain the proven legacy fixture binding and signed surface for
        // routing. One stationary worker is reserved exclusively for work.
        var movement = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var admission = new FoundationMonetaryInfrastructureEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var builder = world.Workers.Workers.First(w => w.Owner == worker.Owner &&
            w.Occupation == PhysicalWorkerOccupation.Forestry);
        var tile = builder.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        world = world.WithWorkers(new PhysicalWorkerRuntimeState(
            world.Workers.Workers.Select(w => w.Id == builder.Id
                ? w.WithTacticalAssignment(tile, w.CellId, PhysicalWorkerActivity.Other,
                    PhysicalWorkerOccupation.Unassigned) : w),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                world.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()));
        var policy = Policy(beginning.Civilizations.Civilizations);
        var travelPolicy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var construction = new FoundationConstructionCollectiveTurnPhase(
            new FoundationConstructionFundingExecutor(admission, 1_000_000UL),
            incidence, fine, temporal: true);
        var hasher = new CanonicalWorldStateHasher();
        var natural = beginning.Transport.Edges.Single(e => e.Edge == edge);
        ulong originalFreight = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(natural);
        var state = beginning;
        const ulong money = 1_600_000UL;
        FoundationTemporalSalesSettledEvent? completionSale = null;
        for (ulong turn = 1; turn <= 4; turn++)
        {
            var context = new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL));
            ISimulationCommand[] commands = turn == 1
                ? new ISimulationCommand[] { new FoundationMonetaryInfrastructureCommand(
                    new CommandId(context.Turn, 1UL), builder.Owner, edge, money) }
                : turn <= 3
                    ? new ISimulationCommand[] { new FoundationConstructionWorkCommand(
                        new CommandId(context.Turn, 1UL), builder.Owner, builder.Id, 1UL) }
                    : Array.Empty<ISimulationCommand>();
            var opening = state;
            var staged = construction.Resolve(new TurnResolutionInput(opening, commands, context), opening);
            var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
                opening, staged.Events, movement, travelPolicy, incidence, policy, context,
                admission, fine, construction);
            state = receipt.Next;
            Assert.True(receipt.ReplayMatched);
            Assert.Equal(turn, state.Treasury.LastSettledEconomicCycleOrdinal);
            Assert.Equal(receipt.Hash, hasher.Compute(FoundationTemporalEconomicJournalGate.ReplayJournal(
                opening, receipt.Journal, movement, travelPolicy, incidence, policy, context,
                fine, admission, construction)));
            Assert.Equal(receipt.EconomicEvent.GrossSalesScaled,
                receipt.EconomicEvent.TotalCivilizationCreditsScaled + receipt.EconomicEvent.PhysicalFreightScaled);
            foreach (var credit in receipt.EconomicEvent.Credits)
            {
                var debit = turn == 1 && credit.Civilization == builder.Owner
                    ? GlobalArenaMoneyScale.FromMoneyMicro(money) : BigInteger.Zero;
                Assert.Equal(opening.Treasury.Get(credit.Civilization).CashScaled - debit + credit.CashCreditScaled,
                    state.Treasury.Get(credit.Civilization).CashScaled);
            }
            var calendar = FoundationTemporalEconomicTurnProjector.Project(opening, staged.ResultingWorldState,
                Array.Empty<FoundationEdgeTravelTurnEvent>(), policy.Demand);
            var expected = FoundationTemporalFinancialReconciler.Evaluate(opening, incidence, calendar, policy, fine, opening);
            Assert.Equal(expected.Accounting.PhysicalFreightScaled, receipt.EconomicEvent.PhysicalFreightScaled);
            Assert.Equal(expected.Accounting.GrossSalesScaled, receipt.EconomicEvent.GrossSalesScaled);
            Assert.Equal(expected.Accounting.ExternalTollsScaled, receipt.EconomicEvent.ExternalTollsScaled);
            var project = Assert.Single(state.Constructions.Projects);
            Assert.Equal(turn == 1 ? 0UL : turn == 2 ? 1_000_000UL : money, project.CompletedWorkMicro);
            Assert.Equal(turn >= 3, project.IsActivated);
            var physical = state.Transport.Edges.Single(e => e.Edge == edge);
            Assert.Equal(turn < 3 ? originalFreight :
                FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(natural, money),
                FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(physical));
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(physical),
                state.Treasury.LastSettledFreightQuotes.Single(q => q.Edge == edge).FreePhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(money),
                state.Treasury.Get(builder.Owner).TotalInfrastructureInvestmentScaled);
            Assert.DoesNotContain(calendar.Production, p => p.Worker == builder.Id);
            if (turn == 3)
            {
                var batch = Assert.IsType<FoundationConstructionWorkBatchEvent>(Assert.Single(staged.Events));
                Assert.Equal(400_000UL, Assert.Single(batch.Receipts).ResidualWorkMicro);
                completionSale = receipt.EconomicEvent;
            }
            if (turn == 4)
            {
                Assert.NotNull(completionSale);
                Assert.Equal(3UL, completionSale!.EconomicCycleOrdinal);
                Assert.Equal(money, physical.CommittedInvestmentMoneyMicro.GetValueOrDefault());
            }
        }
    }

    [Fact]
    public void OneFullPhysicalTravelTurnCreditsRealTreasuryOnceAndReplays()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var beforeHash = new CanonicalWorldStateHasher().Compute(beginning);
        var travel = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(beginning, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, context), beginning);
        var movementEvent = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travel.Events));
        var policy = Policy(beginning.Civilizations.Civilizations);
        var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplay(
            beginning, new[] { movementEvent }, movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            incidence, policy, context, fine);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(receipt.Hash, new CanonicalWorldStateHasher().Compute(receipt.Next));
        Assert.Equal(beforeHash, new CanonicalWorldStateHasher().Compute(beginning));
        Assert.Single(receipt.Next.EdgeTravels.Pending);
        Assert.Equal(400_000UL,
            receipt.Next.EdgeTravels.Pending[0].RemainingMicro);
        Assert.Equal(1UL,
            receipt.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(receipt.EconomicEvent.ProducedGoodsMicro > 0UL);
        Assert.Equal(receipt.EconomicEvent.GrossSalesScaled,
            receipt.EconomicEvent.TotalCivilizationCreditsScaled +
            receipt.EconomicEvent.PhysicalFreightScaled);
        Assert.Equal(receipt.EconomicEvent.GrossSalesScaled,
            receipt.EconomicEvent.SellerNetScaled +
            receipt.EconomicEvent.TaxesScaled +
            receipt.EconomicEvent.ExternalTollsScaled +
            receipt.EconomicEvent.PhysicalFreightScaled);
        Assert.Equal(receipt.EconomicEvent.TotalCivilizationCreditsScaled,
            receipt.EconomicEvent.Credits.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.CashCreditScaled));
        foreach (var credited in receipt.EconomicEvent.Credits)
            Assert.Equal(credited.CashCreditScaled,
                receipt.Next.Treasury.Get(credited.Civilization).CashScaled);
        Assert.Equal(2, receipt.Journal.Entries.Count);
        // Turn 2 must consume the remaining 40% in transit, work 60%
        // at the destination and publish exactly one additional cycle.
        var nextContext = new SimulationContext(
            new TurnNumber(2UL), new SimulationSeed(712UL));
        var nextMove = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(receipt.Next, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(new TurnNumber(2UL), 1UL),
                    worker.Owner, worker.Id, destination, isStart: false)
            }, nextContext), receipt.Next);
        var secondEvent = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(nextMove.Events));
        // The second-cycle market in this deliberately small scenario
        // cannot certify a fixed point under its explicit 64-pass budget.
        // The gate MUST refuse treasury credit, while the physical 40/60
        // timeline remains fully deterministic and auditable.
        var timeline = FoundationTemporalEconomicTurnProjector.Project(
            receipt.Next, nextMove.ResultingWorldState,
            new[] { secondEvent }, policy.Demand);
        Assert.Equal(400_000U,
            secondEvent.Receipt.TurnResult!.TimeSlices[0].PresenceMicro);
        Assert.Equal(600_000U,
            secondEvent.Receipt.TurnResult.TimeSlices[1].ProductiveEffortMicro);
        Assert.Contains(timeline.Production,
            p => p.Worker == worker.Id && p.Node == destination &&
                p.EffortMicro == 600_000U);
        Assert.Empty(nextMove.ResultingWorldState.EdgeTravels.Pending);
        Assert.Equal(destination,
            nextMove.ResultingWorldState.Workers.Workers.Single(w => w.Id == worker.Id)
                .TacticalTileId);
        var hashBeforeRejectedClose = new CanonicalWorldStateHasher()
            .Compute(receipt.Next);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplay(
                receipt.Next, new[] { secondEvent }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, nextContext, fine));
        Assert.Equal(1UL, receipt.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(hashBeforeRejectedClose,
            new CanonicalWorldStateHasher().Compute(receipt.Next));
        var authenticatedReplay =
            FoundationTemporalEconomicJournalGate.ReplayJournal(
                beginning, receipt.Journal, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(authenticatedReplay));
        var settled = receipt.EconomicEvent;
        var forgedMoney = new FoundationTemporalSalesSettledEvent(
            settled.Id, settled.BeginningHash, settled.PreSettlementHash,
            settled.PreSettlementRevision, settled.EconomicCycleOrdinal,
            settled.ScheduleDigest + "FAKE", settled.CargoUnitCode,
            settled.ProducedGoodsMicro, settled.GrossSalesScaled,
            settled.PhysicalFreightScaled, settled.SellerNetScaled,
            settled.TaxesScaled, settled.ExternalTollsScaled,
            settled.Credits);
        var tamperedJournal = new SimulationEventLog(new[]
        {
            new SimulationEventLogEntry(1UL, movementEvent,
                wasEligible: true, wasExecuted: true),
            new SimulationEventLogEntry(2UL, forgedMoney,
                wasEligible: true, wasExecuted: true)
        });
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.ReplayJournal(
                beginning, tamperedJournal, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplay(
                receipt.Next, new[] { movementEvent }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine));
    }

    [Fact]
    public void RejectsTamperedTravelReceiptBeforeAnyFinancialCommit()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var travel = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(beginning, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, context), beginning);
        var actual = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travel.Events));
        var forged = new FoundationEdgeTravelTurnEvent(
            actual.Id, actual.Issuer, actual.WorkerId, actual.Destination,
            actual.Receipt with { Revision = actual.Receipt.Revision + 1UL });
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplay(
                beginning, new[] { forged }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, Policy(beginning.Civilizations.Civilizations), context,
                fine));
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
    }

    private static FoundationAuthoritativeEconomicSettlementPolicy Policy(
        IEnumerable<CivilizationId> roster)
    {
        var demand = Demand();
        return new FoundationAuthoritativeEconomicSettlementPolicy(
            demand,
            new FoundationCommodityCargoConversionPolicy(
                "temporal-cargo",
                demand.Commodities.Select(c =>
                    new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            roster.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
    }

    [Fact]
    public void PreFundedCapitalInvestmentPrecedesTemporalSalesAndReplaysAsOneJournal()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c =>
                (c, c == worker.Owner ? 5_000_000UL : 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var travel = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(beginning, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, context), beginning);
        var movementEvent = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travel.Events));
        var edge = movement.CheckEdge(worker.TacticalTileId!.Value, destination)!.Value;
        var capital = new FoundationMonetaryInfrastructureInvestedEvent(
            new EventId(new CommandId(turn, 2UL), 1UL),
            worker.Owner, edge, 100_000UL,
            travel.ResultingWorldState.Revision);

        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        // The fixture's WorldBinding is the original generated-world binding,
        // not the promoted fine-first binding. The investment executor must
        // match that exact identity; a forged binding must remain rejected.
        var investmentExecutor = new FoundationMonetaryInfrastructureEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var policy = Policy(beginning.Civilizations.Civilizations);
        var journal = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
            beginning, new ISimulationEvent[] { movementEvent, capital },
            movement, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            incidence, policy, context, investmentExecutor, fine);
        Assert.True(journal.ReplayMatched);
        Assert.Equal(3, journal.Journal.Entries.Count);
        Assert.Equal(1UL, journal.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(journal.Hash, new CanonicalWorldStateHasher().Compute(
            FoundationTemporalEconomicJournalGate.ReplayJournal(
                beginning, journal.Journal, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, fine, investmentExecutor)));
        Assert.Equal(100_000UL,
            journal.Next.Transport.Edges.Single(e => e.Edge == edge)
                .CommittedInvestmentMoneyMicro);
        Assert.Equal(journal.EconomicEvent.GrossSalesScaled,
            journal.EconomicEvent.TotalCivilizationCreditsScaled +
            journal.EconomicEvent.PhysicalFreightScaled);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
                beginning, new ISimulationEvent[] { capital, movementEvent },
                movement, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, context, investmentExecutor, fine));
        Assert.Equal(0UL, beginning.Treasury.LastSettledEconomicCycleOrdinal);
    }
    [Fact]
    public void LocalOccupationChangeAffectsFullTurnDemandAndCanonicalSupplyLot()
    {
        var (world, worker, _, _, incidence, _) = Fixture();
        Assert.Equal(PhysicalWorkerActivity.Producing, worker.Activity);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, worker.Occupation);
        var begin = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var localExecutor = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var atNode = worker.TacticalTileId!.Value;
        var ev = new PhysicalTacticalWorkerMovedEvent(
            new EventId(new CommandId(turn, 1UL), 1UL),
            worker.Owner, worker.Id, atNode, atNode, null,
            localExecutor.PresentationRegion(atNode, worker.CellId),
            PhysicalWorkerActivity.Producing, PhysicalWorkerOccupation.Forestry);
        var after = localExecutor.Execute(begin, ev, context);
        Assert.Equal(PhysicalWorkerOccupation.Forestry,
            after.Workers.Workers.Single(w => w.Id == worker.Id).Occupation);
        var input = FoundationTemporalEconomicTurnProjector.Project(
            begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
            new[] { ev }, Demand());
        var row = Assert.Single(input.Slices, s => s.Worker == worker.Id);
        Assert.Equal(1_000_000U, row.PresenceMicro);
        Assert.Equal(1_000_000U, row.ProductiveEffortMicro);
        Assert.Equal(PhysicalWorkerOccupation.Forestry,
            input.EffectiveOccupations[worker.Id]);
        // Distinct activity coefficients prove the journaled occupation,
        // rather than the stale census occupation, drives consumer demand.
        var goods = new[] { new CommodityId(1U), new CommodityId(2U) };
        var profilePolicy = new FoundationActivityCommodityDemandPolicy(
            goods, Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => goods.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item,
                        profile == FoundationPhysicalDemandProfile.ForestryProduction
                            ? 2_000_000UL : 1_000_000UL))));
        var historical = FoundationTemporalEconomicTurnProjector.Project(
            begin, begin, Array.Empty<FoundationEdgeTravelTurnEvent>(),
            profilePolicy);
        var reassigned = FoundationTemporalEconomicTurnProjector.Project(
            begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
            new[] { ev }, profilePolicy);
        foreach (var item in goods)
        {
            var oldDemand = historical.Presence.Demand.Single(d =>
                d.Owner == worker.Owner && d.Node == atNode &&
                d.Commodity == item).DemandMicro;
            var newDemand = reassigned.Presence.Demand.Single(d =>
                d.Owner == worker.Owner && d.Node == atNode &&
                d.Commodity == item).DemandMicro;
            Assert.Equal(1_000_000UL, newDemand - oldDemand);
        }
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(
            begin, input, Demand());
        Assert.Contains(market.SupplyLots, lot =>
            lot.Owner == worker.Owner &&
            lot.Occupation == PhysicalWorkerOccupation.Forestry &&
            lot.Tile == atNode);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicTurnProjector.Project(
                begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
                Demand()));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicTurnProjector.Project(
                begin, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
                new[] { ev, ev }, Demand()));
    }

    [Fact]
    public void CollectiveHostCommitsOneAtomicTemporalSaleAfterAnOrderedPhysicalTurn()
    {
        var (world, worker, destination, _, incidence, _) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var prepared = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        demo.Session.AcceptSnapshot(prepared);
        var host = new FoundationTacticalHumanAiTurnHost(
            demo.Session, incidence, new[] { worker.Owner },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [world.Civilizations.Civilizations.Single(c => c != worker.Owner)] =
                    new FoundationTacticalAiHoldPolicy()
            }, enableMonetaryInfrastructure: true,
            settlementPolicy: Policy(world.Civilizations.Civilizations),
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var initial = host.StateHash;
        var snapshotBefore = host.Snapshot;
        Assert.Null(host.GetLastClosedTemporalEconomics(worker.Owner));
        var aiCivilization = world.Civilizations.Civilizations.Single(c => c != worker.Owner);
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedTemporalEconomics(aiCivilization));
        host.QueueHumanMove(worker.Owner, worker.Id, destination);
        Assert.Equal(initial, host.StateHash);
        host.SetHumanReady(worker.Owner);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.NotNull(receipt.TemporalEconomicSettlement);
        Assert.Null(receipt.EconomicSettlement);
        Assert.Single(receipt.EdgeTravels);
        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.Equal(1UL, host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(400_000UL,
            host.Snapshot.EdgeTravels.Pending.Single().RemainingMicro);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.NotEqual(initial, receipt.Hash);
        var publicForViewer =
            FoundationViewerTemporalEconomicCloseProjector.Read(
                receipt, host.Snapshot, worker.Owner);
        var expected = receipt.TemporalEconomicSettlement!.Credits.Single(
            credit => credit.Civilization == worker.Owner);
        Assert.Equal(expected.CashCreditScaled, publicForViewer.TotalCashCreditScaled);
        Assert.Equal(expected.SellerNetScaled, publicForViewer.SellerNetScaled);
        Assert.Equal(expected.SellerTaxScaled, publicForViewer.TaxReceiptsScaled);
        Assert.Equal(expected.OwnerTollIncomeScaled,
            publicForViewer.OwnerTollIncomeScaled);
        Assert.Equal(host.StateHash, publicForViewer.CommittedWorldHash);
        Assert.Equal(publicForViewer,
            host.GetLastClosedTemporalEconomics(worker.Owner));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationViewerTemporalEconomicCloseProjector.Read(
                receipt, snapshotBefore, worker.Owner));
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedTemporalEconomics(aiCivilization));
        var outsider = new CivilizationId(uint.MaxValue);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationViewerTemporalEconomicCloseProjector.Read(
                receipt, host.Snapshot, outsider));

    }

    [Fact]
    public void AtomicJournalReplaysLocalOccupationAndTravelInTheSameTurn()
    {
        var (world, traveler, destination, _, incidence, fine) = Fixture();
        // This fixture uses the generated-world binding, not the promoted
        // fine-first world binding. Both the local and travel events must be
        // validated by the same matching canonical executor.
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var movement = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, incidence);
        var localWorker = world.Workers.Workers.Single(w =>
            w.Owner == traveler.Owner &&
            w.Occupation == PhysicalWorkerOccupation.Forestry);
        Assert.NotEqual(traveler.Id, localWorker.Id);
        var funding = CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 0UL)));
        var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.DeployInitialTreasury(funding));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var location = localWorker.TacticalTileId!.Value;
        var local = new PhysicalTacticalWorkerMovedEvent(
            new EventId(new CommandId(turn, 1UL), 1UL),
            localWorker.Owner, localWorker.Id, location, location, null,
            movement.PresentationRegion(location, localWorker.CellId),
            PhysicalWorkerActivity.Producing,
            PhysicalWorkerOccupation.Agriculture);
        var afterLocal = movement.Execute(beginning, local, context);
        var travelResult = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(afterLocal, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 2UL), traveler.Owner, traveler.Id,
                    destination, isStart: true)
            }, context), afterLocal);
        var travel = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(travelResult.Events));
        var policy = Policy(beginning.Civilizations.Civilizations);
        var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
            beginning, new ISimulationEvent[] { local, travel }, movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), incidence,
            policy, context, investmentExecutor: null, signedFine: fine);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(3, receipt.Journal.Entries.Count);
        Assert.Equal(1UL, receipt.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(PhysicalWorkerOccupation.Agriculture,
            receipt.Next.Workers.Workers.Single(w => w.Id == localWorker.Id).Occupation);
        Assert.Equal(400_000UL, receipt.Next.EdgeTravels.Pending.Single()
            .RemainingMicro);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(
                FoundationTemporalEconomicJournalGate.ReplayJournal(
                    beginning, receipt.Journal, movement,
                    new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                    incidence, policy, context, fine)));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
                beginning, new ISimulationEvent[] { travel, local }, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), incidence,
                policy, context, investmentExecutor: null, signedFine: fine));
    }

}
