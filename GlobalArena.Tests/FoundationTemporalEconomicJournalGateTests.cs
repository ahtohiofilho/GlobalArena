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
    private static FoundationProductiveReference[] ProductiveReferences() => new[]
    {
        new FoundationProductiveReference(FoundationProductiveActivity.Agriculture, 2_000_000UL),
        new FoundationProductiveReference(FoundationProductiveActivity.Forestry, 3_000_000UL),
        new FoundationProductiveReference(FoundationProductiveActivity.Mining, 4_000_000UL)
    };

    [Fact]
    public void R235CDetachedProductiveLedgerReservesExclusiveTitleAndActivatesOnlyCompletedCapital()
    {
        var (world, worker, destination, _, _, _) = Fixture();
        var owner = worker.Owner;
        var foreign = world.Civilizations.Civilizations.Single(c => c != owner);
        var target = new FoundationProductiveTarget(worker.TacticalTileId!.Value, FoundationProductiveActivity.Agriculture);
        var wood = target with { Activity = FoundationProductiveActivity.Forestry };
        var remote = target with { Node = destination };
        var references = ProductiveReferences();
        var empty = FoundationProductiveInfrastructureRuntimeState.CreateEmpty(references.Reverse());
        Assert.Equal(references, empty.References.ToArray());
        Assert.Equal(0UL, empty.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Null(empty.Read(target).ReservedOwner);
        Assert.Throws<ArgumentException>(() =>
            FoundationProductiveInfrastructureRuntimeState.CreateEmpty(references.Take(2)));
        Assert.Throws<ArgumentException>(() =>
            FoundationProductiveInfrastructureRuntimeState.CreateEmpty(references.Select(r =>
                r with { ConstructionMoneyMicro = 0UL })));
        var reserved = empty.Reserve(target, owner, 6_000_000UL, 1_000_000UL);
        var first = Assert.Single(reserved.Projects);
        Assert.Equal(owner, reserved.Read(target).ReservedOwner);
        Assert.Equal(6_000_000UL, reserved.Read(target).PendingCapitalMoneyMicro);
        Assert.Equal(0UL, reserved.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Empty(empty.Projects);
        Assert.Equal(0UL, reserved.Read(wood).PendingCapitalMoneyMicro);
        Assert.Equal(0UL, reserved.Read(remote).PendingCapitalMoneyMicro);
        Assert.Throws<InvalidOperationException>(() => reserved.Reserve(target, owner, 1UL, 1UL));
        Assert.Throws<InvalidOperationException>(() => reserved.Reserve(target, foreign, 1UL, 1UL));
        Assert.Throws<InvalidOperationException>(() => reserved.Activate(first.Id));
        var partial = reserved.RecordWork(first.Id, 500_000UL);
        Assert.Equal(0UL, partial.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Throws<InvalidOperationException>(() => partial.RecordWork(first.Id, 500_001UL));
        Assert.Throws<InvalidOperationException>(() => partial.Activate(first.Id));
        var completed = partial.RecordWork(first.Id, 500_000UL);
        Assert.Equal(0UL, completed.Read(target).ActivatedCapitalMoneyMicro);
        var active = completed.Activate(first.Id);
        Assert.Equal(6_000_000UL, active.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Equal(0UL, active.Read(target).PendingCapitalMoneyMicro);
        Assert.Throws<InvalidOperationException>(() => active.Activate(first.Id));
        Assert.Throws<InvalidOperationException>(() => active.Reserve(target, foreign, 1UL, 1UL));
        var next = active.Reserve(target, owner, 2_000_000UL, 1UL)
            .Reserve(wood, owner, 3_000_000UL, 1UL).Reserve(remote, owner, 1UL, 1UL);
        Assert.Equal(6_000_000UL, next.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Equal(2_000_000UL, next.Read(target).PendingCapitalMoneyMicro);
        Assert.Equal(3_000_000UL, next.Read(wood).PendingCapitalMoneyMicro);
        Assert.Equal(1UL, next.Read(remote).PendingCapitalMoneyMicro);
        var max = empty.Reserve(target, owner, ulong.MaxValue, 1UL).RecordWork(1UL, 1UL).Activate(1UL);
        Assert.Throws<OverflowException>(() => max.Reserve(target, owner, 1UL, 1UL));
    }

    [Fact]
    public void R235CEmptyProductiveDeploymentHasCanonicalHashAndSurvivesWorldCopies()
    {
        var (legacy, _, _, _, incidence, fine) = Fixture();
        var references = ProductiveReferences();
        Assert.Throws<InvalidOperationException>(() => legacy.DeployInitialProductiveInfrastructure(references));
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(legacy.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var hasher = new CanonicalWorldStateHasher();
        var oldHash = hasher.Compute(opening);
        var deployed = opening.DeployInitialProductiveInfrastructure(references);
        Assert.Equal(31U, hasher.Compute(deployed).FormatVersion);
        Assert.Equal(oldHash, hasher.Compute(opening));
        Assert.NotEqual(oldHash, hasher.Compute(deployed));
        Assert.Equal(hasher.Compute(deployed), hasher.Compute(opening.DeployInitialProductiveInfrastructure(references.Reverse())));
        Assert.NotEqual(hasher.Compute(deployed), hasher.Compute(opening.DeployInitialProductiveInfrastructure(
            references.Select(r => r with { ConstructionMoneyMicro = r.ConstructionMoneyMicro + 1UL }))));
        Assert.Throws<InvalidOperationException>(() => deployed.DeployInitialProductiveInfrastructure(references));
        var worker = deployed.Workers.Workers.First();
        var copied = deployed.AdvanceRevision().WithWorkers(deployed.Workers)
            .SetWorkerBaseActivity(worker.Owner, worker.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining))
            .WithPhysicalEdgeTravel(deployed.Workers, deployed.EdgeTravels)
            .WithConstructions(deployed.Constructions);
        Assert.Same(deployed.ProductiveInfrastructure, copied.ProductiveInfrastructure);
        Assert.Equal(31U, hasher.Compute(copied).FormatVersion);
        Assert.Empty(copied.ProductiveInfrastructure.Projects);
        Assert.Equal(deployed.Treasury.Get(worker.Owner).CashScaled, copied.Treasury.Get(worker.Owner).CashScaled);
    }

    [Fact]
    public void R235D1ProductiveFundingDebitsOpeningCashAndReplaysWithoutActivation()
    {
        var (legacy, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(legacy.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders().DeployInitialProductiveInfrastructure(ProductiveReferences());
        var worker = opening.Workers.Workers.First();
        var owner = worker.Owner;
        var foreign = opening.Civilizations.Civilizations.Single(c => c != owner);
        var node = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            incidence.FineTopology.Cells.First(c =>
                fine.SurfaceAtIndex((int)c.Id.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)c.Id.Value - 1)).Id);
        var target = new FoundationProductiveTarget(node, FoundationProductiveActivity.Agriculture);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), Policy(opening.Civilizations.Civilizations),
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var hasher = new CanonicalWorldStateHasher();
        var before = hasher.Compute(opening);
        const ulong money = 1_600_000UL;
        var command = new FoundationProductiveFundingCommand(new CommandId(context.Turn, 1UL), owner, target, money);
        var result = phase.Resolve(new TurnResolutionInput(opening, new ISimulationCommand[] { command }, context), opening);
        var control = phase.Resolve(new TurnResolutionInput(opening, Array.Empty<ISimulationCommand>(), context), opening);
        var funded = Assert.Single(result.Events.OfType<FoundationProductiveFundingEvent>());
        var sale = Assert.Single(result.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var controlSale = Assert.Single(control.Events.OfType<FoundationTemporalSalesSettledEvent>());
        var next = result.ResultingWorldState;
        var project = Assert.Single(next.ProductiveInfrastructure.Projects);
        Assert.Equal(funded.ProjectId, project.Id);
        Assert.Equal(money, project.RequiredWorkMicro);
        Assert.Equal(0UL, project.CompletedWorkMicro);
        Assert.False(project.IsActivated);
        Assert.Equal(money, next.ProductiveInfrastructure.Read(target).PendingCapitalMoneyMicro);
        Assert.Equal(0UL, next.ProductiveInfrastructure.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Empty(next.Constructions.Projects);
        Assert.Equal(controlSale.ProducedGoodsMicro, sale.ProducedGoodsMicro);
        Assert.Equal(controlSale.GrossSalesScaled, sale.GrossSalesScaled);
        Assert.Equal(controlSale.Credits.ToArray(), sale.Credits.ToArray());
        foreach (var account in next.Treasury.Accounts)
            Assert.Equal(opening.Treasury.Get(account.Civilization).CashScaled +
                sale.Credits.Single(c => c.Civilization == account.Civilization).CashCreditScaled -
                (account.Civilization == owner ? GlobalArenaMoneyScale.FromMoneyMicro(money) : BigInteger.Zero),
                account.CashScaled);
        Assert.Equal(before, hasher.Compute(opening));
        Assert.Equal(31U, hasher.Compute(next).FormatVersion);
        Assert.Equal(hasher.Compute(next), hasher.Compute(phase.ReplayPersistentJournal(opening, result.EventLog, context)));
        var forged = funded with { RequiredWorkMicro = funded.RequiredWorkMicro + 1UL };
        var tampered = new SimulationEventLog(result.EventLog.Entries.Select((e, index) =>
            new SimulationEventLogEntry(checked((ulong)index + 1UL), e.Event is FoundationProductiveFundingEvent ? forged : e.Event,
                wasEligible: true, wasExecuted: true)).ToArray());
        Assert.Throws<InvalidOperationException>(() => phase.ReplayPersistentJournal(opening, tampered, context));
        var competing = new FoundationProductiveFundingCommand(new CommandId(context.Turn, 2UL), foreign, target, 1UL);
        Assert.Throws<InvalidOperationException>(() => phase.Resolve(new TurnResolutionInput(opening,
            new ISimulationCommand[] { command, competing }, context), opening));
        Assert.Throws<InvalidOperationException>(() => phase.Resolve(new TurnResolutionInput(opening,
            new ISimulationCommand[] { command with { MoneyMicro = 5_000_001UL } }, context), opening));
        var water = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            incidence.FineTopology.Cells.First(c => fine.SurfaceAtIndex((int)c.Id.Value - 1) != FoundationFineSurfaceKind.Land).Id);
        Assert.Throws<InvalidOperationException>(() => phase.Resolve(new TurnResolutionInput(opening,
            new ISimulationCommand[] { command with { Target = target with { Node = water } } }, context), opening));
        Assert.Equal(before, hasher.Compute(opening));
    }

    [Theory]
    [InlineData(1_200_000UL, true)]
    [InlineData(1_600_000UL, true)]
    [InlineData(1_600_000UL, false)]
    public void R235D3RemoteProductiveBuildUsesTypedQueueAndSharedCalendar(ulong investment, bool progressive)
    {
        var (legacy, traveler, destination, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(legacy.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var source = traveler.TacticalTileId!.Value;
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound
            .WithWorkers(new PhysicalWorkerRuntimeState(bound.Workers.Workers.Select(w =>
                w.Id == traveler.Id ? w.WithTacticalAssignment(source, traveler.CellId,
                    PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned) : w),
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true))
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders().DeployInitialProductiveInfrastructure(ProductiveReferences());
        var worker = opening.Workers.Workers.Single(w => w.Id == traveler.Id);
        var target = new FoundationProductiveTarget(destination, FoundationProductiveActivity.Agriculture);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters,
            incidence.FineTopology.Edges.Single(e => e.IncidentCellIds.Contains(source.FineStrategicCellId) &&
                e.IncidentCellIds.Contains(destination.FineStrategicCellId)).Id);
        var travel = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        // Choose the distribution contract before any turn. The legacy
        // fractional-production nonconvergence is tracked separately (#729).
        // Never retry a rejected market with a different settlement policy.
        var legacyPolicy = Policy(opening.Civilizations.Civilizations);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = progressive ? new FoundationAgriculturalProductionPolicy(fine, incidence,
            crops, 2_000_000UL, new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL)) : null;
        var items = crops.Values.Append(wood).Append(stone)
            .Concat(legacyPolicy.Demand.Commodities).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(item =>
                new FoundationActivityCommodityDemandCoefficient(profile, item, 1_000_000UL))));
        var settlement = progressive ? new FoundationAuthoritativeEconomicSettlementPolicy(
            demand, new FoundationCommodityCargoConversionPolicy("temporal-cargo", items.Select(item =>
                new FoundationCommodityCargoConversion(item, 1_000_000UL))), legacyPolicy.TaxRates,
            maxProducerPasses: legacyPolicy.MaxProducerPasses,
            useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true) : legacyPolicy;
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            travel, settlement, fine, signature,
            persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
            enableExperimentalAutomaticBase: progressive, enableLocalPriceAllocation: progressive);
        var router = new FoundationConstructionApproachRouter(demo.Session.GeneratedWorld, incidence, fine, signature, travel);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var hasher = new CanonicalWorldStateHasher();
        var before = hasher.Compute(opening);
        var command = new FoundationProductiveFundingCommand(new CommandId(context.Turn, 2UL),
            worker.Owner, target, investment)
            { Assignments = new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) } };
        var road = new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL),
            worker.Owner, edge, 100_000UL, Array.Empty<FoundationFundedWorkerInsertion>());
        var foreign = opening.Workers.Workers.First(w => w.Owner != worker.Owner);
        Assert.Throws<InvalidOperationException>(() => phase.Resolve(new TurnResolutionInput(opening,
            new ISimulationCommand[] { command with { Assignments = new[] { new FoundationFundedWorkerInsertion(foreign.Id, 0) } } },
            context), opening));
        Assert.Throws<InvalidOperationException>(() => phase.Resolve(new TurnResolutionInput(opening,
            new ISimulationCommand[] { command with { Assignments = new[] {
                new FoundationFundedWorkerInsertion(worker.Id, 0), new FoundationFundedWorkerInsertion(worker.Id, 0) } } },
            context), opening));
        Assert.Throws<InvalidOperationException>(() => opening.InsertWorkerOrder(worker.Owner, worker.Id, 0,
            FoundationWorkerOrderKind.BuildProductive, projectId: 1UL));
        var result = phase.Resolve(new TurnResolutionInput(opening, new ISimulationCommand[] { road, command }, context), opening);
        var next = result.ResultingWorldState;
        var order = Assert.Single(next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(FoundationWorkerOrderKind.BuildProductive, order.Kind);
        Assert.Equal(Assert.Single(next.Constructions.Projects).Id, order.ProjectId);
        Assert.Equal(Assert.Single(next.ProductiveInfrastructure.Projects).Id, order.ProjectId);
        Assert.Equal(400_000UL, Assert.Single(next.EdgeTravels.Pending, p => p.Worker == worker.Id).RemainingMicro);
        Assert.Equal(0UL, Assert.Single(next.ProductiveInfrastructure.Projects).CompletedWorkMicro);
        var funding = Assert.Single(result.Events.OfType<FoundationProductiveFundingEvent>());
        Assert.Equal(worker.Id, Assert.Single(funding.Assignments).Worker);
        Assert.Equal(hasher.Compute(next), hasher.Compute(phase.ReplayPersistentJournal(opening, result.EventLog, context)));
        var forged = funding with { Assignments = Array.Empty<FoundationFundedWorkerInsertion>() };
        var tampered = new SimulationEventLog(result.EventLog.Entries.Select((e, index) =>
            new SimulationEventLogEntry(checked((ulong)index + 1UL), e.Event is FoundationProductiveFundingEvent ? forged : e.Event,
                wasEligible: true, wasExecuted: true)).ToArray());
        Assert.Throws<InvalidOperationException>(() => phase.ReplayPersistentJournal(opening, tampered, context));
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var second = phase.Resolve(new TurnResolutionInput(next, Array.Empty<ISimulationCommand>(), secondContext), next);
        var arrived = second.ResultingWorldState;
        Assert.DoesNotContain(arrived.EdgeTravels.Pending, p => p.Worker == worker.Id);
        Assert.Equal(destination, arrived.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
        Assert.Equal(FoundationConstructionApproachStatus.AtSite, router.Plan(arrived, worker.Id).Status);
        Assert.Equal(order, Assert.Single(arrived.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders));
        Assert.Equal(600_000UL, Assert.Single(arrived.ProductiveInfrastructure.Projects).CompletedWorkMicro);
        Assert.Equal(0UL, arrived.ProductiveInfrastructure.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Equal(investment, arrived.ProductiveInfrastructure.Read(target).PendingCapitalMoneyMicro);
        Assert.Equal(0UL, Assert.Single(arrived.Constructions.Projects).CompletedWorkMicro);
        var slices = Assert.Single(second.Events.OfType<FoundationPersistentWorkerTurnEvent>()).Slices
            .Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(400_000L, slices.Where(s => s.Activity == PhysicalWorkerActivity.Traveling).Sum(s => (long)s.DurationMicro));
        Assert.Equal(600_000L, slices.Where(s => s.Activity == PhysicalWorkerActivity.Other).Sum(s => (long)s.DurationMicro));
        Assert.All(slices, s => Assert.Null(s.ProjectId));
        Assert.Equal(600_000L, slices.Where(s => s.ProductiveProjectId == order.ProjectId).Sum(s => (long)s.DurationMicro));
        Assert.Equal(before, hasher.Compute(opening));
        Assert.Equal(hasher.Compute(arrived), hasher.Compute(phase.ReplayPersistentJournal(next, second.EventLog, secondContext)));
        var thirdContext = new SimulationContext(new TurnNumber(3UL), new SimulationSeed(712UL));
        var ready = arrived.InsertWorkerOrder(worker.Owner, worker.Id, 1,
            FoundationWorkerOrderKind.Occupation, occupation: PhysicalWorkerOccupation.Agriculture);
        var third = phase.Resolve(new TurnResolutionInput(ready, Array.Empty<ISimulationCommand>(), thirdContext), ready);
        var completed = third.ResultingWorldState;
        var project = Assert.Single(completed.ProductiveInfrastructure.Projects);
        Assert.True(project.IsActivated);
        Assert.Equal(project.RequiredWorkMicro, project.CompletedWorkMicro);
        Assert.Equal(investment, completed.ProductiveInfrastructure.Read(target).ActivatedCapitalMoneyMicro);
        Assert.Equal(0UL, completed.ProductiveInfrastructure.Read(target).PendingCapitalMoneyMicro);
        Assert.Equal(0UL, Assert.Single(completed.Constructions.Projects).CompletedWorkMicro);
        var remainingOrder = Assert.Single(completed.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(FoundationWorkerOrderKind.Occupation, remainingOrder.Kind);
        var timeline = Assert.Single(third.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        var work = checked((long)(investment - 600_000UL));
        var workerSlices = timeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.Equal(work, workerSlices.Where(s => s.ProductiveProjectId == project.Id).Sum(s => (long)s.DurationMicro));
        Assert.Equal(1_000_000L - work, workerSlices.Where(s => s.Activity == PhysicalWorkerActivity.Producing).Sum(s => (long)s.DurationMicro));
        Assert.Equal(hasher.Compute(completed), hasher.Compute(phase.ReplayPersistentJournal(ready, third.EventLog, thirdContext)));
        var forgedTimeline = timeline with { Slices = timeline.Slices.Select(s => s.ProductiveProjectId.HasValue
            ? s with { ProductiveProjectId = null } : s).ToArray() };
        var forgedLog = new SimulationEventLog(third.EventLog.Entries.Select((e, index) =>
            new SimulationEventLogEntry(checked((ulong)index + 1UL), e.Event is FoundationPersistentWorkerTurnEvent ? forgedTimeline : e.Event,
                wasEligible: true, wasExecuted: true)).ToArray());
        Assert.Throws<InvalidOperationException>(() => phase.ReplayPersistentJournal(ready, forgedLog, thirdContext));
        // An independent scheduler replay does not debit construction again.
        var scheduler = new FoundationPersistentWorkerTurnScheduler(router, agriculture,
            progressive ? settlement : null, enableLocalPriceAllocation: progressive);
        var scheduled = scheduler.Record(ready, thirdContext);
        Assert.Same(ready.Treasury, scheduled.Next.Treasury);

        // Concurrent road and productive teams share a calendar, not an ID namespace.
        var crew = opening.Workers.Workers.Where(w => w.Owner == worker.Owner)
            .OrderBy(w => w.Id.Value).ToArray();
        Assert.Equal(3, crew.Length);
        var destinationRegion = arrived.Workers.Workers.Single(w => w.Id == worker.Id).CellId;
        var teamOpening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.Id == crew[0].Id || w.Id == crew[1].Id
                ? w.WithTacticalAssignment(destination, destinationRegion, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned)
                : w.Id == crew[2].Id
                    ? w.WithTacticalAssignment(source, traveler.CellId, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned)
                    : w), tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var teamCommand = command with { MoneyMicro = investment - 1UL, Assignments = new[] {
            new FoundationFundedWorkerInsertion(crew[0].Id, 0), new FoundationFundedWorkerInsertion(crew[1].Id, 0) } };
        var roadCommand = new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL),
            worker.Owner, edge, 100_000UL, new[] { new FoundationFundedWorkerInsertion(crew[2].Id, 0) });
        var teamResult = phase.Resolve(new TurnResolutionInput(teamOpening,
            new ISimulationCommand[] { roadCommand, teamCommand }, context), teamOpening);
        var teamState = teamResult.ResultingWorldState;
        var productiveTeam = Assert.Single(teamState.ProductiveInfrastructure.Projects);
        var roadTeam = Assert.Single(teamState.Constructions.Projects);
        Assert.Equal(roadTeam.Id, productiveTeam.Id);
        Assert.True(productiveTeam.IsActivated);
        Assert.True(roadTeam.IsActivated);
        var teamSlices = Assert.Single(teamResult.Events.OfType<FoundationPersistentWorkerTurnEvent>()).Slices;
        Assert.Equal(checked((long)(investment - 1UL)), teamSlices.Where(s => s.ProductiveProjectId == productiveTeam.Id)
            .Sum(s => (long)s.DurationMicro));
        Assert.Equal(100_000L, teamSlices.Where(s => s.ProjectId == roadTeam.Id).Sum(s => (long)s.DurationMicro));
        Assert.Equal(checked((long)(investment / 2UL)), teamSlices.Where(s => s.Worker == crew[0].Id &&
            s.ProductiveProjectId == productiveTeam.Id).Sum(s => (long)s.DurationMicro));
        Assert.Equal(checked((long)(investment / 2UL - 1UL)), teamSlices.Where(s => s.Worker == crew[1].Id &&
            s.ProductiveProjectId == productiveTeam.Id).Sum(s => (long)s.DurationMicro));
        Assert.Equal(hasher.Compute(teamState), hasher.Compute(phase.ReplayPersistentJournal(teamOpening, teamResult.EventLog, context)));
    }

    [Fact]
    public void R235EOpeningCapitalChangesActualOutputAndLocalRankingOnlyForOwnerAndActivity()
    {
        var (legacy, traveler, remote, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(legacy.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var node = traveler.TacticalTileId!.Value;
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound
            .WithWorkers(new PhysicalWorkerRuntimeState(bound.Workers.Workers.Select(w =>
                w.WithTacticalAssignment(node, traveler.CellId, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned)),
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true))
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 30_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders().DeployInitialProductiveInfrastructure(ProductiveReferences());
        var worker = opening.Workers.Workers.Single(w => w.Id == traveler.Id);
        var peer = opening.Workers.Workers.First(w => w.Owner == worker.Owner && w.Id != worker.Id);
        var foreign = opening.Workers.Workers.First(w => w.Owner != worker.Owner);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 1_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone)
            .Concat(opening.Workers.Workers.Select(w => w.Commodity)).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(item =>
                new FoundationActivityCommodityDemandCoefficient(profile, item, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r235e", items.Select(item =>
                new FoundationCommodityCargoConversion(item, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64, useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true);
        var target = new FoundationProductiveTarget(node, FoundationProductiveActivity.Mining);
        var funded = opening.FundProductiveConstruction(worker.Owner, target, 12_000_000UL, 600_000UL);
        var project = Assert.Single(funded.ProductiveInfrastructure.Projects);
        var complete = funded.RecordProductiveConstructionWork(project.Id, 600_000UL);
        var active = complete.ActivateCompletedProductiveConstruction(project.Id);
        ulong Output(WorldState state, PhysicalWorkerRuntimeRecord person, PhysicalTacticalTileId tile,
            PhysicalWorkerOccupation role, byte? crop = null) =>
            FoundationTemporalEconomicTurnProjector.ResolveOpeningOutput(state, person, tile, role, crop, agriculture).WholeTurnOutput;
        Assert.Equal(5_000_000UL, Output(opening, worker, node, PhysicalWorkerOccupation.Mining));
        Assert.Equal(5_000_000UL, Output(funded, worker, node, PhysicalWorkerOccupation.Mining));
        Assert.Equal(5_000_000UL, Output(complete, worker, node, PhysicalWorkerOccupation.Mining));
        Assert.Equal(10_000_000UL, Output(active, worker, node, PhysicalWorkerOccupation.Mining));
        Assert.Equal(5_000_000UL, Output(active, foreign, node, PhysicalWorkerOccupation.Mining));
        Assert.Equal(6_000_000UL, Output(active, worker, node, PhysicalWorkerOccupation.Forestry));
        Assert.Equal(Output(opening, worker, remote, PhysicalWorkerOccupation.Mining),
            Output(active, worker, remote, PhysicalWorkerOccupation.Mining));
        var farm = active.FundProductiveConstruction(worker.Owner,
            target with { Activity = FoundationProductiveActivity.Agriculture }, 6_000_000UL, 1UL);
        var farmId = farm.ProductiveInfrastructure.Projects.Single(p => p.Target.Activity == FoundationProductiveActivity.Agriculture).Id;
        farm = farm.RecordProductiveConstructionWork(farmId, 1UL).ActivateCompletedProductiveConstruction(farmId);
        foreach (var crop in crops.Keys)
        {
            ulong natural = agriculture.Resolve(node, crop).WholeTurnOutput;
            Assert.Equal(checked(natural * 2UL), Output(farm, worker, node, PhysicalWorkerOccupation.Agriculture, (byte)crop));
            Assert.Equal(natural, Output(farm, foreign, node, PhysicalWorkerOccupation.Agriculture, (byte)crop));
        }
        var calendar = opening.Workers.Workers.Select(w => new FoundationPersistentWorkerSlice(w.Id,
            node, 0U, 1_000_000U, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var requests = new[] { new FoundationBaseMarginalInterval(worker.Id, 0U) };
        var baseline = FoundationLocalBaseCalendarPlanner.Plan(opening, incidence, fine, policy, calendar, requests, agriculture);
        var improved = FoundationLocalBaseCalendarPlanner.Plan(active, incidence, fine, policy, calendar, requests, agriculture);
        Assert.Equal(PhysicalWorkerOccupation.Forestry, baseline.Single(s => s.Worker == worker.Id).Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Mining, improved.Single(s => s.Worker == worker.Id).Occupation);
        var production = FoundationTemporalEconomicTurnProjector.ProjectPersistent(active, improved, demand, agriculture);
        Assert.Equal(10_000_000UL, Assert.Single(production.Production).ProducedMicro);

        // Actual commissioning at the end of a collective turn cannot boost
        // that turn's residual production, even though the staged ledger is active.
        var ready = funded.InsertWorkerOrder(worker.Owner, worker.Id, 0, FoundationWorkerOrderKind.BuildProductive, projectId: project.Id)
            .InsertWorkerOrder(worker.Owner, worker.Id, 1, FoundationWorkerOrderKind.Occupation, occupation: PhysicalWorkerOccupation.Mining)
            .InsertWorkerOrder(peer.Owner, peer.Id, 0, FoundationWorkerOrderKind.Occupation, occupation: PhysicalWorkerOccupation.Mining);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy, fine, signature,
            persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
            enableExperimentalAutomaticBase: true, enableLocalPriceAllocation: true);
        var hasher = new CanonicalWorldStateHasher();
        var hash = hasher.Compute(ready);
        var firstContext = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var first = phase.Resolve(new TurnResolutionInput(ready, Array.Empty<ISimulationCommand>(), firstContext), ready);
        var next = first.ResultingWorldState;
        Assert.True(Assert.Single(next.ProductiveInfrastructure.Projects).IsActivated);
        Assert.Equal(7_000_000UL, Assert.Single(first.Events.OfType<FoundationTemporalSalesSettledEvent>()).ProducedGoodsMicro);
        Assert.Equal(hasher.Compute(next), hasher.Compute(phase.ReplayPersistentJournal(ready, first.EventLog, firstContext)));
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var second = phase.Resolve(new TurnResolutionInput(next, Array.Empty<ISimulationCommand>(), secondContext), next);
        Assert.Equal(20_000_000UL, Assert.Single(second.Events.OfType<FoundationTemporalSalesSettledEvent>()).ProducedGoodsMicro);
        Assert.Equal(hasher.Compute(second.ResultingWorldState),
            hasher.Compute(phase.ReplayPersistentJournal(next, second.EventLog, secondContext)));
        Assert.Equal(hash, hasher.Compute(ready));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R235F1HostAdmitsPrivateProductivePlansAndArbitratesWithoutForeignDebit(bool reverse)
    {
        var (legacy, traveler, remote, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(legacy.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders().DeployInitialProductiveInfrastructure(ProductiveReferences());
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        var node = traveler.TacticalTileId!.Value;
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.WithTacticalAssignment(node, traveler.CellId, w.Activity, w.Occupation!.Value)),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        demo.Session.AcceptSnapshot(opening);
        var civs = opening.Civilizations.Civilizations.OrderBy(c => c.Value).ToArray();
        var scopes = civs.ToDictionary(c => c, _ => new FoundationCivilizationKnowledgeScope(
            new[] { node }, Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>()));
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence, civs,
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(), enableMonetaryInfrastructure: true,
            settlementPolicy: Policy(civs), fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, knowledgeScopes: scopes);
        Assert.True(host.ProductiveInfrastructureEnabled);
        var target = new FoundationProductiveTarget(node, FoundationProductiveActivity.Mining);
        var winner = civs[0]; var loser = civs[1];
        var builder = opening.Workers.Workers.First(w => w.Owner == winner);
        var foreign = opening.Workers.Workers.First(w => w.Owner == loser);
        var hash = host.StateHash;
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanProductiveFunding(winner,
            target with { Node = remote }, 1UL, Array.Empty<FoundationFundedWorkerInsertion>()));
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanProductiveFunding(winner, target,
            600_000UL, new[] { new FoundationFundedWorkerInsertion(foreign.Id, 0) }));
        host.QueueHumanProductiveFunding(winner, target, 1UL, Array.Empty<FoundationFundedWorkerInsertion>());
        Assert.True(host.CancelHumanProductiveFunding(winner, target));
        var sequence = reverse ? civs.Reverse().ToArray() : civs;
        foreach (var civ in sequence)
        {
            var person = civ == winner ? builder : foreign;
            host.QueueHumanProductiveFunding(civ, target, civ == winner ? 600_000UL : 900_000UL,
                new[] { new FoundationFundedWorkerInsertion(person.Id, 0) });
            Assert.Single(host.GetHumanPendingProductiveFunding(civ));
        }
        Assert.Empty(host.GetHumanProductiveProjects(winner, node));
        Assert.Empty(host.GetHumanProductiveProjects(loser, node));
        Assert.Equal(hash, host.StateHash);
        host.SetHumanReady(sequence[0]);
        Assert.Null(host.TryCloseTurn());
        host.SetHumanReady(sequence[1]);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(winner, Assert.Single(receipt.ProductiveFunding).Investor);
        Assert.Equal(600_000UL, Assert.Single(host.GetHumanProductiveProjects(winner, node)).FundedMoneyMicro);
        Assert.Empty(host.GetHumanProductiveProjects(loser, node));
        Assert.Null(host.GetHumanProductiveCapital(loser, node).Single(r => r.Target == target).ReservedOwner);
        Assert.Equal(600_000UL, host.GetHumanProductiveCapital(winner, node).Single(r => r.Target == target).ActivatedCapitalMoneyMicro);
        Assert.Empty(host.GetHumanPendingProductiveFunding(winner));
        Assert.Empty(host.GetHumanPendingProductiveFunding(loser));
        var sale = receipt.TemporalEconomicSettlement!;
        Assert.Equal(opening.Treasury.Get(loser).CashScaled +
            sale.Credits.Single(c => c.Civilization == loser).CashCreditScaled,
            host.Snapshot.Treasury.Get(loser).CashScaled);
    }

    [Fact]
    public void R235BGenericCapitalResponseIsExactMonotoneAndScaleIndependent()
    {
        const ulong natural = 1_000_000UL, reference = 10_000_000UL;
        Assert.Equal(natural, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, 0, reference));
        Assert.Equal(1_414_213UL, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, reference, reference));
        Assert.Equal(2_000_000UL, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, 3 * reference, reference));
        Assert.Equal(500_000UL, FoundationInfrastructureCapitalResponse.ProjectUnitCost(natural, 3 * reference, reference));
        Assert.Equal(0UL, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(0, ulong.MaxValue, reference));
        Assert.Equal(1UL, FoundationInfrastructureCapitalResponse.ProjectUnitCost(1, ulong.MaxValue, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationInfrastructureCapitalResponse.ProjectUnitCost(0, 1, reference));
        Assert.Throws<OverflowException>(() =>
            FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(ulong.MaxValue, ulong.MaxValue, 1));
        Assert.Equal(ulong.MaxValue, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(ulong.MaxValue, 0, ulong.MaxValue));
        ulong lastOutput = natural, lastCost = natural;
        for (ulong i = 0; i <= 100; i++)
        {
            ulong capital = i * reference / 10;
            ulong output = FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, capital, reference);
            ulong cost = FoundationInfrastructureCapitalResponse.ProjectUnitCost(natural, capital, reference);
            Assert.True(output >= lastOutput);
            Assert.True(cost <= lastCost);
            Assert.Equal(output, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, capital * 100, reference * 100));
            Assert.Equal(cost, FoundationInfrastructureCapitalResponse.ProjectUnitCost(natural, capital * 100, reference * 100));
            // Compare against the original exact R112 expression, independently
            // of the refactored root implementation.
            var radicand = (BigInteger)natural * natural * reference / ((BigInteger)reference + capital);
            Assert.True((BigInteger)cost * cost <= radicand);
            Assert.True(((BigInteger)cost + 1) * (cost + 1) > radicand);
            lastOutput = output;
            lastCost = cost;
        }
        var firstGain = FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, reference, reference) - natural;
        var laterGain = FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, 2 * reference, reference)
            - FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(natural, reference, reference);
        Assert.True(firstGain > laterGain);
    }

    [Fact]
    public void R235BSharedAgriculturalCapitalPreservesAllSixNaturalCropYields()
    {
        var (_, worker, _, _, incidence, fine) = Fixture();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var policy = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var node = worker.TacticalTileId!.Value;
        const ulong reference = 10_000_000UL;
        Assert.Equal(6, crops.Count);
        foreach (var crop in crops.Keys)
        {
            var (commodity, naturalOutput) = policy.Resolve(node, crop);
            Assert.Equal(crops[crop], commodity);
            // Same node-level agricultural capital, no crop-specific bonus.
            Assert.Equal(checked(naturalOutput * 2),
                FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(naturalOutput, 3 * reference, reference));
        }
        Assert.Equal(12_000_000UL, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(6_000_000UL, 3 * reference, reference));
        Assert.Equal(10_000_000UL, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(5_000_000UL, 3 * reference, reference));
    }

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



    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R2271TemporalHostPublishesActualLocalOrRemoteTrade(bool remote)
    {
        var (world, worker, destination, _, incidence, _) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        // Explicit opening fixture: supply at one node, consumers either
        // at that node or across a real adjacent crossing. No fabricated routes.
        var node = worker.TacticalTileId!.Value;
        var foreign = world.Civilizations.Civilizations.Single(c => c != worker.Owner);
        var remoteRegion = incidence.TileIncidences[
            (int)destination.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
        var census = world.Workers.Workers.Select(w =>
            w.WithTacticalLocationAndActivity(
                remote && w.Owner == foreign ? destination : node,
                remote && w.Owner == foreign ? remoteRegion : worker.CellId,
                w.Owner == foreign ? PhysicalWorkerActivity.Other : PhysicalWorkerActivity.Producing));
        var prepared = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            world.WithWorkers(new PhysicalWorkerRuntimeState(census,
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true))
                .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                    world.Civilizations.Civilizations.Select(c => (c, 0UL)))));
        demo.Session.AcceptSnapshot(prepared);
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
            world.Civilizations.Civilizations,
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
            enableMonetaryInfrastructure: true, settlementPolicy: Policy(world.Civilizations.Civilizations),
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(worker.Owner).Availability);
        Assert.Null(host.GetLastClosedLocalEconomics(worker.Owner, node));
        Assert.Null(host.GetLastClosedLocalEconomics(foreign, remote ? destination : node));
        if (remote) Assert.Throws<InvalidOperationException>(() => host.GetLastClosedLocalEconomics(worker.Owner, destination));
        host.SetHumanReady(worker.Owner);
        Assert.Null(host.TryCloseTurn());
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(worker.Owner).Availability);
        Assert.Null(host.GetLastClosedLocalEconomics(worker.Owner, node));
        host.SetHumanReady(foreign);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        var sale = Assert.IsType<FoundationTemporalSalesSettledEvent>(receipt.TemporalEconomicSettlement);
        Assert.True(sale.GrossSalesScaled > BigInteger.Zero);
        var prices = Assert.IsType<FoundationSettledLocalPriceSnapshot>(sale.AuditedLocalPrices);
        Assert.Equal(1UL, prices.CycleOrdinal);
        Assert.NotEmpty(prices.Prices);
        Assert.All(prices.Prices, p =>
        {
            Assert.Equal((ulong?)p.PriceMicro, prices.ObservedPrice(p.Node, p.Commodity));
            Assert.True(p.PriceMicro > 0UL);
        });
        if (remote) Assert.Contains(prices.Prices, p => p.Node == destination);
        else Assert.All(prices.Prices, p => Assert.Equal(node, p.Node));
        Assert.Null(prices.ObservedPrice(node, new CommodityId(uint.MaxValue)));
        foreach (var civ in world.Civilizations.Civilizations)
        {
            var view = host.GetLastClosedEconomicTradeView(civ);
            Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset, view.Availability);
            Assert.Equal((ulong?)1UL, view.EconomicCycleOrdinal);
            Assert.Equal((WorldStateHash?)receipt.Hash, view.PublishedHash);
            Assert.Equal(sale.AuditedParticipantEdges![civ].ToArray(), view.UsedEdges.ToArray());
            if (remote) Assert.NotEmpty(view.UsedEdges);
            else Assert.Empty(view.UsedEdges);
        }
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedEconomicTradeView(new CivilizationId(uint.MaxValue)));
        var mine = Assert.IsType<FoundationViewerLocalEconomicClose>(host.GetLastClosedLocalEconomics(worker.Owner, node));
        var theirs = Assert.IsType<FoundationViewerLocalEconomicClose>(host.GetLastClosedLocalEconomics(foreign, remote ? destination : node));
        Assert.Equal(receipt.Hash, mine.CommittedWorldHash);
        Assert.Equal(1UL, mine.EconomicCycleOrdinal);
        Assert.Equal(worker.Owner, mine.Viewer);
        Assert.Equal(foreign, theirs.Viewer);
        Assert.Equal(sale.ProducedGoodsMicro, mine.Commodities.Aggregate(0UL, (sum, row) => sum + row.ProducedMicro));
        Assert.All(theirs.Commodities, row => Assert.Equal(0UL, row.ProducedMicro));
        Assert.All(mine.Commodities, row => Assert.Equal(row.ProducedMicro, row.SoldMicro));
        Assert.Equal(sale.SellerNetScaled, mine.Commodities.Aggregate(BigInteger.Zero, (sum, row) => sum + row.SellerNetScaled));
        Assert.Equal(sale.ProducedGoodsMicro, mine.Commodities.Concat(theirs.Commodities).Aggregate(0UL, (sum, row) => sum + row.ReceivedMicro));
        Assert.Equal(mine.Commodities.Aggregate(0UL, (sum, row) => sum + row.ExportedMicro),
            theirs.Commodities.Aggregate(0UL, (sum, row) => sum + row.ImportedMicro));
        if (remote) Assert.Contains(mine.Commodities, row => row.ExportedMicro > 0UL);
        else Assert.All(mine.Commodities.Concat(theirs.Commodities), row => { Assert.Equal(0UL, row.ExportedMicro); Assert.Equal(0UL, row.ImportedMicro); });
        Assert.All(mine.Commodities, row => Assert.Equal(prices.ObservedPrice(node, row.Commodity), row.LocalPriceMicro));
        Assert.Throws<InvalidOperationException>(() => host.GetLastClosedLocalEconomics(new CivilizationId(uint.MaxValue), node));
        Assert.Equal(receipt.Hash, host.StateHash);
    }

    [Fact]
    public void R2289SettledLocalPricesDeduplicateSharedMarketsAndPreserveMissingVersusZero()
    {
        var (_, worker, destination, _, _, _) = Fixture();
        var node = worker.TacticalTileId!.Value;
        var commodity = worker.Commodity;
        FoundationPhysicalEconomicAccountingLine Line(ulong ordinal, PhysicalTacticalTileId buyer, ulong price) =>
            new(ordinal, worker.Owner, worker.Owner, node, buyer, commodity, 1UL, price,
                BigInteger.Zero, BigInteger.Zero, BigInteger.Zero, BigInteger.Zero, BigInteger.Zero);
        var lines = new[] { Line(1, node, 2_000_000UL), Line(2, node, 2_000_000UL), Line(3, destination, 0UL) };
        var snapshot = FoundationSettledLocalPriceSnapshot.FromAccounting(7UL, lines);
        Assert.Equal(2, snapshot.Prices.Count);
        Assert.Equal((ulong?)2_000_000UL, snapshot.ObservedPrice(node, commodity));
        Assert.Equal((ulong?)0UL, snapshot.ObservedPrice(destination, commodity));
        Assert.Null(snapshot.ObservedPrice(node, new CommodityId(uint.MaxValue)));
        Assert.Equal(1_000_000UL, FoundationSettledLocalPriceSnapshot.InitialReferencePriceMicro);
        Assert.True(snapshot.Identical(FoundationSettledLocalPriceSnapshot.FromAccounting(7UL, lines.Reverse())));
        Assert.False(snapshot.Identical(FoundationSettledLocalPriceSnapshot.FromAccounting(8UL, lines)));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationSettledLocalPriceSnapshot.FromAccounting(7UL, lines.Append(Line(4, node, 3_000_000UL))));
        Assert.Empty(FoundationSettledLocalPriceSnapshot.FromAccounting(7UL,
            Array.Empty<FoundationPhysicalEconomicAccountingLine>()).Prices);
    }

    [Fact]
    public void R2281MarginalRevenueUsesCanonicalMarketAndPreservesOpening()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var items = crops.Values.Concat(opening.Workers.Workers.Select(w => w.Commodity)).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2281", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var calendar = opening.Workers.Workers.Select(w => new FoundationPersistentWorkerSlice(
            w.Id, w.TacticalTileId!.Value, 0U, 1_000_000U,
            PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var hasher = new CanonicalWorldStateHasher();
        var before = hasher.Compute(opening);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var measured = FoundationBaseMarginalRevenueEvaluator.EvaluateWithDiagnostics(
            opening, incidence, fine, policy, calendar, worker.Id, 0U, agriculture);
        timer.Stop();
        var rows = measured.Candidates;
        Assert.NotEmpty(rows);
        Assert.Equal(rows.Count + 1, measured.FinancialEvaluations);
        Console.WriteLine($"R2282_REFERENCE crops/native workers={calendar.Length} candidates={rows.Count} financial_evaluations={measured.FinancialEvaluations} elapsed_ms={timer.ElapsedMilliseconds}");
        Assert.Equal(rows.OrderByDescending(x => x.MarginalCreditScaled).ThenBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray(), rows.ToArray());
        var repeated = FoundationBaseMarginalRevenueEvaluator.Evaluate(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), worker.Id, 0U, agriculture);
        Assert.Equal(rows.ToArray(), repeated.ToArray());
        var eligible = crops.Keys.Where(c => agriculture.Resolve(worker.TacticalTileId!.Value, c).WholeTurnOutput > 0)
            .Select(c => (byte)c).OrderBy(c => c).ToArray();
        Assert.Equal(eligible, rows.Where(x => x.Crop.HasValue).Select(x => x.Crop!.Value).OrderBy(c => c).ToArray());
        foreach (var row in rows)
        {
            Assert.Equal(1_000_000U, row.EffortMicro);
            Assert.Equal(BigInteger.Zero, row.BaselineCreditScaled);
            Assert.True(row.MarginalCreditScaled > 0);
        }
        Assert.Equal(before, hasher.Compute(opening));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.All(calendar, x => Assert.Equal(PhysicalWorkerOccupation.Unassigned, x.Occupation));
        var busy = calendar.Select(x => x.Worker == worker.Id ? x with
            { Activity = PhysicalWorkerActivity.Producing, Occupation = PhysicalWorkerOccupation.Agriculture, Crop = eligible[0] } : x).ToArray();
        Assert.Throws<InvalidOperationException>(() => FoundationBaseMarginalRevenueEvaluator.Evaluate(
            opening, incidence, fine, policy, busy, worker.Id, 0U, agriculture));

        // A real producer and the remaining idle workers define one complete
        // reference calendar. Split the target's interval to verify that
        // operational effort is fractional rather than a whole worker.
        var producer = opening.Workers.Workers.First(w => w.Id != worker.Id && w.Owner == worker.Owner);
        var partial = calendar.Where(s => s.Worker != worker.Id)
            .Select(s => s.Worker == producer.Id ? s with
            { Activity = PhysicalWorkerActivity.Producing, Occupation = producer.Occupation!.Value } : s)
            .Concat(new[]
            {
                new FoundationPersistentWorkerSlice(worker.Id, worker.TacticalTileId!.Value, 0U, 600_000U,
                    PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, 1UL),
                new FoundationPersistentWorkerSlice(worker.Id, worker.TacticalTileId!.Value, 600_000U, 400_000U,
                    PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)
            }).ToArray();
        var operations = new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL);
        timer.Restart();
        var reference = FoundationBaseMarginalRevenueEvaluator.EvaluateWithDiagnostics(
            opening, incidence, fine, policy, partial, worker.Id, 600_000U, agriculture, operations);
        timer.Stop();
        Assert.Equal(reference.Candidates.Count + 1, reference.FinancialEvaluations);
        Console.WriteLine($"R2282_REFERENCE operational workers={calendar.Length} candidates={reference.Candidates.Count} financial_evaluations={reference.FinancialEvaluations} elapsed_ms={timer.ElapsedMilliseconds}");
        var land = Assert.Single(reference.Candidates, x => x.Occupation == PhysicalWorkerOccupation.LandTransport);
        Assert.Equal(400_000U, land.EffortMicro);
        Assert.Null(land.Crop);
        int nodeIndex = (int)worker.TacticalTileId!.Value.FineStrategicCellId.Value - 1;
        bool portEligible = fine.FineElevationRaw[nodeIndex] > fine.SeaLevelRaw &&
            incidence.FineTopology.Edges.Any(e =>
                e.IncidentCellIds.Contains(worker.TacticalTileId.Value.FineStrategicCellId) &&
                fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                    (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Port);
        Assert.Equal(portEligible, reference.Candidates.Any(x => x.Occupation == PhysicalWorkerOccupation.PortOperations));

        // Independent direct use of the canonical reconciler, not an invented
        // per-edge tariff or a benefit estimated from global freight savings.
        FoundationTemporalFinancialReconciliation Reconcile(IReadOnlyList<FoundationPersistentWorkerSlice> input) =>
            FoundationTemporalFinancialReconciler.Evaluate(opening, incidence,
                FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, input, policy.Demand, agriculture),
                policy, fine, opening, operations);
        BigInteger OwnerCredit(FoundationTemporalFinancialReconciliation f)
        {
            var account = f.Accounting.Accounts.Single(a => a.Civilization == worker.Owner);
            return account.SellerNetScaled + account.RevenueTaxScaled + account.TollIncomeScaled;
        }
        var baseline = Reconcile(partial);
        Assert.Equal(OwnerCredit(baseline), land.BaselineCreditScaled);
        var frozen = FoundationFrozenMarginalRevenueEstimator.Evaluate(opening, incidence, fine, policy,
            partial, new[] { new FoundationBaseMarginalInterval(worker.Id, 600_000U) }, agriculture, operations);
        Assert.Equal(1, frozen.FinancialEvaluations);
        Assert.Equal(reference.Candidates.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray(),
            frozen.Intervals.Single().Candidates.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray());
        Assert.All(frozen.Intervals.Single().Candidates, x => Assert.Equal(400_000U, x.EffortMicro));
        foreach (var candidate in reference.Candidates.Where(x =>
            x.Occupation is PhysicalWorkerOccupation.LandTransport or PhysicalWorkerOccupation.PortOperations))
        {
            var modified = partial.Select(s => s.Worker == worker.Id && s.StartMicro == 600_000U
                ? s with { Occupation = candidate.Occupation } : s).ToArray();
            var direct = Reconcile(modified);
            Assert.Equal(OwnerCredit(direct), candidate.CandidateCreditScaled);
            var quotes = Assert.IsAssignableFrom<IReadOnlyList<FoundationOperationalFreightShadowEdge>>(direct.OperationalQuotes);
            Assert.NotEmpty(quotes);
            Assert.Equal(400_000UL, quotes.Aggregate(0UL, (sum, q) => checked(sum + q.OperationalServiceMicro)));
            Assert.All(quotes, q => Assert.True(q.ShadowPhysicalCostMicroPerWholeCargoUnit <= q.OriginalPhysicalCostMicroPerWholeCargoUnit));
        }
        Assert.Equal(before, hasher.Compute(opening));
        Assert.All(partial.Where(s => s.Worker == worker.Id), s => Assert.Equal(PhysicalWorkerOccupation.Unassigned, s.Occupation));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    public void R2283FrozenBatchMatchesReferenceAndReusesOnlyEquivalentFullIdleWorkers(int workersPerCivilization)
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: workersPerCivilization);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        // Explicit homogeneous opening within each owner, not between owners.
        // Keep individual IDs and complete queue census. This exposes the
        // concentrated-opening reuse opportunity without changing live rules.
        var people = opening.Workers.Workers;
        var representatives = people.GroupBy(w => w.Owner).ToDictionary(g => g.Key, g => g.First());
        var homogeneous = people.Select(w =>
        {
            var template = representatives[w.Owner];
            return new PhysicalWorkerRuntimeRecord(w.Id, w.Owner, template.CellId,
                template.Commodity, template.OutputMicroPerTurn, template.Activity,
                template.DemandMultiplierMicro, template.TacticalTileId, template.Occupation);
        }).ToArray();
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(homogeneous,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var items = crops.Values.Concat(homogeneous.Select(w => w.Commodity)).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c,
                    profile == FoundationPhysicalDemandProfile.AgricultureProduction ? 1_300_000UL : 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2283", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var operations = new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL);
        var calendar = homogeneous.Select(w => new FoundationPersistentWorkerSlice(
            w.Id, w.TacticalTileId!.Value, 0U, 1_000_000U,
            PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var requests = calendar.Select(s => new FoundationBaseMarginalInterval(s.Worker, s.StartMicro)).ToArray();
        var before = new CanonicalWorldStateHasher().Compute(opening);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var batch = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), requests.Reverse().ToArray(), agriculture, operations);
        timer.Stop();
        Assert.Equal(requests, batch.Intervals.Select(x => x.Interval).ToArray());
        Assert.True(batch.EquivalentIntervalsReused > 0);
        var directEvaluations = 0;
        var separateTimer = System.Diagnostics.Stopwatch.StartNew();
        foreach (var entry in batch.Intervals)
        {
            var direct = FoundationBaseMarginalRevenueEvaluator.EvaluateWithDiagnostics(opening, incidence, fine, policy,
                calendar, entry.Interval.Worker, entry.Interval.StartMicro, agriculture, operations);
            Assert.Equal(direct.Candidates.ToArray(), entry.Candidates.ToArray());
            directEvaluations += direct.FinancialEvaluations;
        }
        separateTimer.Stop();
        Assert.True(batch.FinancialEvaluations < directEvaluations);
        Console.WriteLine($"R2283_BATCH workers={homogeneous.Length} reused={batch.EquivalentIntervalsReused} batch_evaluations={batch.FinancialEvaluations} separate_evaluations={directEvaluations} batch_ms={timer.ElapsedMilliseconds} separate_ms={separateTimer.ElapsedMilliseconds}");
        // Split two equivalent workers into partial intervals; they must
        // remain independently evaluated even if the economic rows match.
        var twins = homogeneous.Where(w => w.Owner == homogeneous[0].Owner).Take(2).Select(w => w.Id).ToHashSet();
        var partial = calendar.SelectMany(s => twins.Contains(s.Worker)
            ? new[] { s with { DurationMicro = 600_000U }, s with { StartMicro = 600_000U, DurationMicro = 400_000U } }
            : new[] { s }).ToArray();
        var partialRequests = twins.OrderBy(id => id.Value)
            .Select(id => new FoundationBaseMarginalInterval(id, 600_000U)).ToArray();
        var partialBatch = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            partial, partialRequests, agriculture, operations);
        Assert.Equal(0, partialBatch.EquivalentIntervalsReused);
        foreach (var entry in partialBatch.Intervals)
        {
            var direct = FoundationBaseMarginalRevenueEvaluator.Evaluate(opening, incidence, fine, policy,
                partial, entry.Interval.Worker, entry.Interval.StartMicro, agriculture, operations);
            Assert.Equal(direct.ToArray(), entry.Candidates.ToArray());
        }
        Assert.Throws<ArgumentException>(() => FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(
            opening, incidence, fine, policy, calendar, new[] { requests[0], requests[0] }, agriculture, operations));
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(opening));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    public void R2284BoundedReferenceMeasuresHeterogeneousWorkersWithoutPartialRankings(int workersPerCivilization)
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: workersPerCivilization);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        // Deliberately unique economic facts even at the same node.
        // Native roles/commodities remain authentic to the test scenario.
        var workers = opening.Workers.Workers.Select(w => new PhysicalWorkerRuntimeRecord(
            w.Id, w.Owner, w.CellId, w.Commodity, checked(1_000_000UL + 10_000UL * w.Id.Value),
            w.Activity, w.DemandMultiplierMicro, w.TacticalTileId, w.Occupation)).ToArray();
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var items = crops.Values.Concat(workers.Select(w => w.Commodity)).Distinct().ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2284", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var operations = new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL);
        var calendar = workers.Select(w => new FoundationPersistentWorkerSlice(
            w.Id, w.TacticalTileId!.Value, 0U, 1_000_000U,
            PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var requests = calendar.Select(s => new FoundationBaseMarginalInterval(s.Worker, s.StartMicro)).ToArray();
        var before = new CanonicalWorldStateHasher().Compute(opening);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var exact = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar, requests, agriculture, operations);
        watch.Stop();
        long exactMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var estimate = FoundationFrozenMarginalRevenueEstimator.Evaluate(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), requests.Reverse().ToArray(), agriculture, operations);
        watch.Stop();
        long estimateMs = watch.ElapsedMilliseconds;
        Assert.False(estimate.IsCommitted);
        Assert.Equal(1, estimate.FinancialEvaluations);
        Assert.Equal(requests, estimate.Intervals.Select(x => x.Interval).ToArray());
        int comparable = 0, agreement = 0;
        BigInteger regret = BigInteger.Zero, opportunity = BigInteger.Zero;
        foreach (var interval in estimate.Intervals)
        {
            var reference = exact.Intervals.Single(x => x.Interval == interval.Interval).Candidates;
            Assert.Equal(reference.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray(),
                interval.Candidates.Select(x => (x.Occupation, x.Crop)).OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray());
            if (reference.Count == 0) continue;
            comparable++;
            var first = interval.Candidates[0];
            var selected = reference.Single(x => x.Occupation == first.Occupation && x.Crop == first.Crop);
            var best = BigInteger.Max(BigInteger.Zero, reference[0].MarginalCreditScaled);
            var achieved = first.EstimatedCreditDeltaScaled > 0 ? selected.MarginalCreditScaled : BigInteger.Zero;
            if (achieved == best) agreement++;
            regret += best - achieved;
            opportunity += best;
        }
        Console.WriteLine($"R2285_FROZEN workers={workers.Length} solves={estimate.FinancialEvaluations} trees={estimate.RouteTreesBuilt} elapsed_ms={estimateMs} comparable={comparable} exact_best_matches={agreement} regret_scaled={regret} opportunity_scaled={opportunity}");
        var repeated = FoundationFrozenMarginalRevenueEstimator.Evaluate(opening, incidence, fine, policy,
            calendar, requests, agriculture, operations);
        foreach (var row in estimate.Intervals)
            Assert.Equal(row.Candidates.ToArray(), repeated.Intervals.Single(x => x.Interval == row.Interval).Candidates.ToArray());
        Assert.Equal(0, exact.EquivalentIntervalsReused);
        Assert.Equal(0, exact.DeferredIntervals);
        Assert.Equal(1 + exact.Intervals.Sum(x => x.Candidates.Count), exact.FinancialEvaluations);
        const int budget = 18;
        watch.Restart();
        var limited = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), requests.Reverse().ToArray(), agriculture, operations, budget);
        watch.Stop();
        Assert.InRange(limited.FinancialEvaluations, 1, budget);
        Assert.InRange(limited.DeferredIntervals, 1, requests.Length - 1);
        Assert.Equal(requests, limited.Intervals.Select(x => x.Interval).ToArray());
        foreach (var row in limited.Intervals)
        {
            if (row.DeferredByBudget) Assert.Empty(row.Candidates);
            else Assert.Equal(exact.Intervals.Single(x => x.Interval == row.Interval).Candidates.ToArray(),
                row.Candidates.ToArray());
        }
        Console.WriteLine($"R2284_HETEROGENEOUS workers={workers.Length} exact_solves={exact.FinancialEvaluations} exact_ms={exactMs} budget={budget} bounded_solves={limited.FinancialEvaluations} bounded_ms={watch.ElapsedMilliseconds} deferred={limited.DeferredIntervals}");
        var zero = FoundationBaseMarginalRevenueEvaluator.EvaluateBatch(opening, incidence, fine, policy,
            calendar, requests, agriculture, operations, 0);
        Assert.Equal(0, zero.FinancialEvaluations);
        Assert.Equal(exact.Intervals.Count(x => x.Candidates.Count > 0), zero.DeferredIntervals);
        Assert.All(zero.Intervals, x => Assert.Empty(x.Candidates));
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(opening));
        Assert.Equal(0UL, opening.Treasury.LastSettledEconomicCycleOrdinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R2286ExperimentalAutomaticBaseUsesOnlyAvailableSuffixAndReplaysCollectively(bool localPriceAllocation)
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(worker.TacticalTileId!.Value.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        var own = opening.Workers.Workers.Where(w => w.Owner == worker.Owner).ToArray();
        var second = own.First(w => w.Id != worker.Id);
        var manual = own.Single(w => w.Id != worker.Id && w.Id != second.Id);
        // Other workers provide real demand but no independent native output.
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.WithTacticalLocationAndActivity(w.TacticalTileId!.Value, w.CellId, PhysicalWorkerActivity.Other)),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2286", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64);
        var movement = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            movement, policy, fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL,
            agriculture: agriculture, enableExperimentalAutomaticBase: true,
            enableLocalPriceAllocation: localPriceAllocation);
        var disabled = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            movement, policy, fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL,
            agriculture: agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var automatic = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic);
        var manualCrop = (byte)4;
        var manualBase = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, manualCrop);
        var commands = new ISimulationCommand[]
        {
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 1UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id, FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity: automatic)),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(second.Owner, second.Id, FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity: automatic)),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 3UL),
                new FoundationPersistentOrderEdit(manual.Owner, manual.Id, FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity: manualBase)),
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 4UL), worker.Owner, edge,
                600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) })
        };
        var before = new CanonicalWorldStateHasher().Compute(opening);
        var input = new TurnResolutionInput(opening, commands, context);
        Assert.Throws<InvalidOperationException>(() => disabled.Resolve(input, opening));
        var resolved = phase.Resolve(input, opening);
        var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        var first = timeline.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(2, first.Length);
        Assert.Equal(600_000U, first[0].DurationMicro);
        Assert.NotNull(first[0].ProjectId);
        Assert.Equal(600_000U, first[1].StartMicro);
        Assert.Equal(400_000U, first[1].DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, first[1].Activity);
        Assert.Null(first[1].ProjectId);
        var full = Assert.Single(timeline.Slices, x => x.Worker == second.Id);
        Assert.Equal(1_000_000U, full.DurationMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing, full.Activity);
        Assert.All(timeline.Slices.Where(x => x.Worker == manual.Id), x =>
        {
            Assert.Equal(PhysicalWorkerActivity.Producing, x.Activity);
            Assert.Equal(manualCrop, x.Crop);
        });
        Assert.All(timeline.Slices.GroupBy(x => x.Worker), g => Assert.Equal(1_000_000UL,
            g.Aggregate(0UL, (sum, x) => sum + x.DurationMicro)));
        Assert.Equal(automatic, resolved.ResultingWorldState.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).BaseActivity);
        Assert.Equal(1UL, resolved.ResultingWorldState.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.True(resolved.ResultingWorldState.Constructions.Projects.Single().IsActivated);
        var replay = phase.ReplayPersistentJournal(opening, resolved.EventLog, context);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(resolved.ResultingWorldState), hasher.Compute(replay));
        Assert.Equal(before, hasher.Compute(opening));
        Assert.Null(FoundationLocalBaseCalendarPlanner.ReadHistory(opening));
        var firstState = resolved.ResultingWorldState;
        var history = FoundationLocalBaseCalendarPlanner.ReadHistory(firstState);
        if (!localPriceAllocation)
        {
            Assert.Null(history);
            Assert.Equal(CanonicalWorldStateHasher.BaseActivityFormatVersion, hasher.Compute(firstState).FormatVersion);
            return;
        }
        Assert.Equal(CanonicalWorldStateHasher.LocalPriceHistoryFormatVersion, hasher.Compute(firstState).FormatVersion);
        Assert.NotNull(history);
        var firstPrices = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>()).AuditedLocalPrices!;
        Assert.NotEmpty(firstPrices.Prices);
        Assert.Equal(firstPrices.Prices.Select(p => new CivilizationObservedMarketPrice(p.Node, p.Commodity, p.PriceMicro, 1UL)).ToArray(),
            history!.ToArray());
        Assert.Equal(history.ToArray(), FoundationLocalBaseCalendarPlanner.ReadHistory(replay)!.ToArray());

        // The next close uses the previous immutable state as the input.
        // Verify retained and overwritten observations, not just the ordinal.
        var nextContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var nextInput = new TurnResolutionInput(firstState, Array.Empty<ISimulationCommand>(), nextContext);
        var next = phase.Resolve(nextInput, firstState);
        var nextPrices = Assert.Single(next.Events.OfType<FoundationTemporalSalesSettledEvent>()).AuditedLocalPrices!;
        var expectedHistory = history.ToDictionary(p => (p.Node, p.Commodity));
        foreach (var p in nextPrices.Prices)
            expectedHistory[(p.Node, p.Commodity)] = new CivilizationObservedMarketPrice(p.Node, p.Commodity, p.PriceMicro, 2UL);
        var nextHistory = FoundationLocalBaseCalendarPlanner.ReadHistory(next.ResultingWorldState)!;
        Assert.Equal(expectedHistory.Count, nextHistory.Count);
        Assert.All(nextHistory, p => Assert.Equal(expectedHistory[(p.Node, p.Commodity)], p));
        Assert.All(history, p => Assert.Equal(1UL, p.ObservedCycleOrdinal));
        Assert.Equal(hasher.Compute(next.ResultingWorldState),
            hasher.Compute(phase.ReplayPersistentJournal(firstState, next.EventLog, nextContext)));

    }

    [Theory]
    [InlineData("legacy", 3)]
    [InlineData("local-fixed-point", 2)]
    [InlineData("manual-fixed-point", 2)]
    [InlineData("local-progressive", 6)]
    [InlineData("manual-progressive", 6)]
    public void R22811ACompareDistributionContractsAfterTravel(string mode, int turns)
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        // Other workers provide real demand but no independent native output.
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.WithTacticalLocationAndActivity(w.TacticalTileId!.Value, w.CellId, PhysicalWorkerActivity.Other)),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        bool progressive = mode.EndsWith("-progressive", StringComparison.Ordinal);
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2287", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64, useTemporalProgressiveDistribution: progressive);
        Assert.Equal(64, policy.MaxProducerPasses);
        var movement = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        // Same physical prices, demand, freight and accounting; the
        // distribution contract is selected before the turn, never on failure.
        FoundationTemporalCollectiveTurnPhase NewPhase(bool? alternate = null) => new(demo.Session.GeneratedWorld, incidence,
            movement, alternate.HasValue ? new FoundationAuthoritativeEconomicSettlementPolicy(demand, policy.Cargo,
                policy.TaxRates, maxProducerPasses: 64, useTemporalProgressiveDistribution: alternate.Value) : policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL,
            agriculture: agriculture, enableExperimentalAutomaticBase: true,
            enableLocalPriceAllocation: mode != "legacy");
        // Select a real traversable land arrival with positive agricultural
        // yield, not an arbitrary tile or a preselected market outcome.
        var candidate = (from w in opening.Workers.Workers
            let source = w.TacticalTileId!.Value
            from crossing in incidence.FineTopology.Edges
            where crossing.IncidentCellIds.Contains(source.FineStrategicCellId) &&
                fine.CrossingAtIndices((int)crossing.IncidentCellIds[0].Value - 1,
                    (int)crossing.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground
            let destination = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
                crossing.IncidentCellIds.Single(n => n != source.FineStrategicCellId))
            from crop in crops.Keys
            where agriculture.Resolve(source, crop).WholeTurnOutput > 0 &&
                agriculture.Resolve(destination, crop).WholeTurnOutput > 0
            orderby w.Id != worker.Id, w.Id.Value, destination.FineStrategicCellId.Value, (byte)crop
            select (Worker: w, Source: source, Destination: destination, Crop: crop)).First();
        worker = candidate.Worker;
        var own = opening.Workers.Workers.Where(w => w.Owner == worker.Owner).ToArray();
        var automatic = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic);
        var fixedManual = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual,
            PhysicalWorkerOccupation.Agriculture, (byte)candidate.Crop);
        var hash = new CanonicalWorldStateHasher();
        var openingHash = hash.Compute(opening);

        BigInteger Run()
        {
            var state = opening;
            BigInteger cumulative = BigInteger.Zero;
            bool auto = !mode.StartsWith("manual", StringComparison.Ordinal);
            for (ulong turn = 1; turn <= (ulong)turns; turn++)
            {
                var context = new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL));
                var commands = new List<ISimulationCommand>();
                if (turn == 1UL)
                {
                    foreach (var person in own)
                        commands.Add(new FoundationPersistentOrderEditCommand(
                            new CommandId(context.Turn, (ulong)commands.Count + 1UL),
                            new FoundationPersistentOrderEdit(person.Owner, person.Id,
                                FoundationPersistentOrderEditKind.SetBaseActivity,
                                BaseActivity: auto ? automatic : fixedManual)));
                    commands.Add(new FoundationPersistentOrderEditCommand(
                        new CommandId(context.Turn, (ulong)commands.Count + 1UL),
                        new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                            FoundationPersistentOrderEditKind.Insert, Index: 0,
                            OrderKind: FoundationWorkerOrderKind.Move, Destination: candidate.Destination)));
                }
                var before = hash.Compute(state);
                var input = new TurnResolutionInput(state, commands.ToArray(), context);
                var activePhase = NewPhase();
                if (!progressive && turn == (ulong)turns)
                {
                    var error = Assert.Throws<InvalidOperationException>(() => activePhase.Resolve(input, state));
                    Assert.Contains("stable full-pass fixed point", error.Message);
                    Assert.Equal(before, hash.Compute(state));
                    Assert.Equal(turn - 1UL, state.Treasury.LastSettledEconomicCycleOrdinal);
                    Console.WriteLine($"R22811A_FIXED_POINT_REJECTED mode={mode} turn={turn} {error.Message}");
                    return cumulative;
                }
                var timer = System.Diagnostics.Stopwatch.StartNew();
                TurnResolutionResult resolved;
                try { resolved = activePhase.Resolve(input, state); }
                catch (InvalidOperationException ex)
                {
                    Assert.Equal(before, hash.Compute(state));
                    Assert.Equal(turn - 1UL, state.Treasury.LastSettledEconomicCycleOrdinal);
                    Console.WriteLine($"R22811A_CLOSE_REJECTED mode={mode} turn={turn} {ex.Message}");
                    throw;
                }
                timer.Stop();
                var next = resolved.ResultingWorldState;
                var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
                var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
                Assert.Equal(progressive, sale.UsedProgressiveDistribution);
                Assert.Equal(turn, sale.EconomicCycleOrdinal);
                Assert.Equal(turn, next.Treasury.LastSettledEconomicCycleOrdinal);
                Assert.True(sale.ProducedGoodsMicro > 0UL);
                Assert.Equal(sale.GrossSalesScaled, sale.TotalCivilizationCreditsScaled + sale.PhysicalFreightScaled);
                Assert.Equal(state.Workers.Workers.Count, timeline.Slices.Select(x => x.Worker).Distinct().Count());
                foreach (var group in timeline.Slices.GroupBy(x => x.Worker))
                {
                    uint cursor = 0;
                    foreach (var slice in group.OrderBy(x => x.StartMicro))
                    {
                        Assert.Equal(cursor, slice.StartMicro);
                        cursor += slice.DurationMicro;
                    }
                    Assert.Equal(1_000_000U, cursor);
                }
                foreach (var credit in sale.Credits)
                    Assert.Equal(state.Treasury.Get(credit.Civilization).CashScaled + credit.CashCreditScaled,
                        next.Treasury.Get(credit.Civilization).CashScaled);
                var slices = timeline.Slices.Where(x => x.Worker == worker.Id).OrderBy(x => x.StartMicro).ToArray();
                if (turn == 1UL)
                {
                    Assert.All(slices, x =>
                    {
                        Assert.Equal(candidate.Source, x.Node);
                        Assert.Equal(PhysicalWorkerActivity.Traveling, x.Activity);
                    });
                    Assert.Equal(400_000UL, Assert.Single(next.EdgeTravels.Pending, x => x.Worker == worker.Id).RemainingMicro);
                }
                else
                {
                    Assert.DoesNotContain(next.EdgeTravels.Pending, x => x.Worker == worker.Id);
                    Assert.Empty(next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
                    Assert.Equal(candidate.Destination, next.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
                    if (turn == 2UL)
                    {
                        Assert.Equal(400_000L, slices.Where(x => x.Activity == PhysicalWorkerActivity.Traveling).Sum(x => (long)x.DurationMicro));
                        Assert.Equal(600_000L, slices.Where(x => x.Node == candidate.Destination).Sum(x => (long)x.DurationMicro));
                    }
                    else Assert.All(slices, x => Assert.Equal(candidate.Destination, x.Node));
                    Assert.Contains(slices, x => x.Node == candidate.Destination && x.Activity == PhysicalWorkerActivity.Producing);
                }
                Assert.All(next.WorkerOrders.Queues.Where(q => q.Owner == worker.Owner),
                    q => Assert.Equal(auto ? automatic : fixedManual, q.BaseActivity));
                if (!auto)
                    Assert.All(timeline.Slices.Where(x => own.Any(w => w.Id == x.Worker) && x.Activity == PhysicalWorkerActivity.Producing),
                        x => Assert.Equal((byte)candidate.Crop, x.Crop));
                Assert.Equal(hash.Compute(next), hash.Compute(NewPhase().ReplayPersistentJournal(state, resolved.EventLog, context)));
                Assert.Equal(before, hash.Compute(state));
                if (progressive && turn == 1UL)
                {
                    // A different policy cannot replay this sale, even if its
                    // accounting happens to match on this opening.
                    Assert.Throws<InvalidOperationException>(() => NewPhase(false).ReplayPersistentJournal(state, resolved.EventLog, context));
                    // Direct test-only check of the certificate. The production
                    // path and per-turn measurements never perform this solve.
                    var projected = FoundationTemporalEconomicTurnProjector.ProjectPersistent(state, timeline.Slices, demand, agriculture);
                    var diagnostic = FoundationTemporalFinancialReconciler.Evaluate(state, incidence, projected, policy, fine, state);
                    Assert.False(diagnostic.Distribution.StableUnderOneFullCanonicalPass);
                    Assert.Equal(0, diagnostic.Distribution.Passes);
                    Assert.Equal(sale.ProducedGoodsMicro, diagnostic.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
                    Assert.Equal(sale.GrossSalesScaled, diagnostic.Accounting.GrossSalesScaled);
                }
                if (auto && turn == 2UL)
                {
                    // Forged available effort must fail independent replay,
                    // even with unchanged event IDs and stated final hash.
                    var forged = timeline.Slices.ToArray();
                    int index = Array.FindIndex(forged, x => x.Worker == worker.Id && x.Node == candidate.Destination);
                    forged[index] = forged[index] with { DurationMicro = forged[index].DurationMicro - 1U };
                    var events = resolved.Events.Select(e => e is FoundationPersistentWorkerTurnEvent ? timeline with { Slices = forged } : e).ToArray();
                    var log = new SimulationEventLog(events.Select((e, i) =>
                        new SimulationEventLogEntry((ulong)i + 1UL, e, true, true)).ToArray());
                    Assert.ThrowsAny<Exception>(() => activePhase.ReplayPersistentJournal(state, log, context));
                }
                if (mode != "legacy")
                {
                    var evidence = sale.AuditedLocalPrices!;
                    var oldHistory = FoundationLocalBaseCalendarPlanner.ReadHistory(state)
                        ?? Array.Empty<CivilizationObservedMarketPrice>();
                    var expected = oldHistory.ToDictionary(p => (p.Node, p.Commodity));
                    foreach (var p in evidence.Prices)
                        expected[(p.Node, p.Commodity)] = new CivilizationObservedMarketPrice(
                            p.Node, p.Commodity, p.PriceMicro, turn);
                    var history = FoundationLocalBaseCalendarPlanner.ReadHistory(next)!;
                    Assert.Equal(expected.Count, history.Count);
                    Assert.All(history, p => Assert.Equal(expected[(p.Node, p.Commodity)], p));
                    Assert.Equal(CanonicalWorldStateHasher.LocalPriceHistoryFormatVersion,
                        hash.Compute(next).FormatVersion);
                    Assert.All(oldHistory, p => Assert.True(p.ObservedCycleOrdinal < turn));
                    Console.WriteLine($"R22811A_PRICES mode={mode} turn={turn} observed={evidence.Prices.Count} retained={history.Count - evidence.Prices.Count}");
                }
                // No new financial reconciliation for diagnostics. Record
                // actual committed results and wall time of the real close.
                var ownerCredit = sale.Credits.Single(x => x.Civilization == worker.Owner).CashCreditScaled;
                cumulative += ownerCredit;
                var roles = timeline.Slices.Where(x => own.Any(w => w.Id == x.Worker))
                    .GroupBy(x => (x.Occupation, x.Crop))
                    .Select(g => $"{g.Key.Occupation}/{g.Key.Crop}:{g.Sum(x => (long)x.DurationMicro)}");
                Console.WriteLine($"R22811A_COLLECTIVE mode={mode} turn={turn} owner_credit_scaled={ownerCredit} goods_micro={sale.ProducedGoodsMicro} roles={string.Join(",", roles)} elapsed_ms={timer.ElapsedMilliseconds} cumulative_credit_scaled={cumulative}");
                state = next;
            }
            return cumulative;
        }
        var total = Run();
        Console.WriteLine($"R22811A_TOTAL mode={mode} requested_turns={turns} owner_credit_scaled={total}");
        Assert.Equal(openingHash, hash.Compute(opening));
    }

    [Fact]
    public void R22813C1SafeRouteBoundIncludesFinalSupplyAndTaxWithoutChangingPrice()
    {
        // Final worst case P=1/2 when four units are delivered against one.
        // A 0.45 route is just affordable after 10% tax, a more expensive one is not.
        Assert.True(FoundationPhysicalMarginalMarketDistributor.IsSafeAtFinalSupplyBound(
            1_000_000UL, 4_000_000UL, new BigInteger(450_000UL) * 1_000_000UL, 1_000U));
        Assert.False(FoundationPhysicalMarginalMarketDistributor.IsSafeAtFinalSupplyBound(
            1_000_000UL, 4_000_000UL, new BigInteger(450_001UL) * 1_000_000UL, 1_000U));
        Assert.True(FoundationPhysicalMarginalMarketDistributor.IsSafeAtFinalSupplyBound(
            1_000_000UL, 1_000_000UL, new BigInteger(450_001UL) * 1_000_000UL, 1_000U));
        Assert.True(FoundationPhysicalMarginalMarketDistributor.IsSafeAtFinalSupplyBound(
            1_000_000UL, 4_000_000UL, BigInteger.Zero, 10_000U));
    }

    [Fact]
    public void R2317BoundedCapacityIsExactAndReservesLocalSupply()
    {
        var freight = new BigInteger(2_351_599UL) * 1_000_000UL;
        ulong cap = FoundationPhysicalMarginalMarketDistributor.SafeDestinationCapacity(500_001UL, freight, 1_000U);
        Assert.True(cap > 50_000UL && cap < 1_618_192UL);
        Assert.True(FoundationPhysicalMarginalMarketDistributor.IsSafeAtFinalSupplyBound(500_001UL, cap, freight, 1_000U));
        Assert.False(FoundationPhysicalMarginalMarketDistributor.IsSafeAtFinalSupplyBound(500_001UL, cap + 1UL, freight, 1_000U));
        Assert.Equal(0UL, FoundationPhysicalMarginalMarketDistributor.RemainingSafeCapacity(cap, 0UL, cap));
        Assert.Equal(cap - 30_000UL, FoundationPhysicalMarginalMarketDistributor.RemainingSafeCapacity(cap, 10_000UL, 20_000UL));
        Assert.Equal(0UL, FoundationPhysicalMarginalMarketDistributor.SafeDestinationCapacity(500_001UL, freight, 10_000U));
        Assert.Equal(ulong.MaxValue, FoundationPhysicalMarginalMarketDistributor.SafeDestinationCapacity(500_001UL, BigInteger.Zero, 10_000U));
    }

    [Fact]
    public void R22813LocalBasicResourcesUseDistinctCommoditiesAndActualEffort()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var basic = new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 1_000_000UL, basic);
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r22813", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            world.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)));
        var node = worker.TacticalTileId!.Value;
        Assert.Equal(FoundationFineSurfaceKind.Land, fine.SurfaceAtIndex((int)node.FineStrategicCellId.Value - 1));
        var pair = world.Workers.Workers.Where(w => w.Owner == worker.Owner).OrderBy(w => w.Id.Value).Take(2).ToArray();
        Assert.Equal(2, pair.Length);
        var roster = world.Workers.Workers.Select(w => pair.Any(p => p.Id == w.Id)
            ? new PhysicalWorkerRuntimeRecord(w.Id, w.Owner, worker.CellId, w.Commodity, w.OutputMicroPerTurn,
                PhysicalWorkerActivity.Other, w.DemandMultiplierMicro, node, PhysicalWorkerOccupation.Unassigned) : w).ToArray();
        var opening = world.WithWorkers(new PhysicalWorkerRuntimeState(roster,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        var calendar = roster.Select(w => new FoundationPersistentWorkerSlice(w.Id, w.TacticalTileId!.Value, 0U,
            1_000_000U, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null)).ToArray();
        var requests = pair.Select(w => new FoundationBaseMarginalInterval(w.Id, 0U)).ToArray();
        var planned = FoundationLocalBaseCalendarPlanner.Plan(opening, incidence, fine, policy, calendar, requests, agriculture);
        Assert.Equal(PhysicalWorkerOccupation.Forestry, planned.Single(s => s.Worker == pair[0].Id).Occupation);
        Assert.Equal(PhysicalWorkerOccupation.Mining, planned.Single(s => s.Worker == pair[1].Id).Occupation);
        Assert.Equal(planned, FoundationLocalBaseCalendarPlanner.Plan(opening, incidence, fine, policy,
            calendar.Reverse().ToArray(), requests.Reverse().ToArray(), agriculture));
        var actual = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, planned, demand, agriculture);
        Assert.Equal(2, actual.Production.Count);
        Assert.Equal(6_000_000UL, Assert.Single(actual.Production, p => p.Commodity == wood).ProducedMicro);
        Assert.Equal(5_000_000UL, Assert.Single(actual.Production, p => p.Commodity == stone).ProducedMicro);
        Assert.DoesNotContain(wood, agriculture.Commodities);
        Assert.DoesNotContain(stone, agriculture.Commodities);
        var selected = planned.Single(s => s.Worker == pair[0].Id);
        var residual = planned.Where(s => s.Worker != selected.Worker).Append(selected with { StartMicro = 400_000U, DurationMicro = 600_000U })
            .Append(selected with { DurationMicro = 400_000U, Activity = PhysicalWorkerActivity.Other, Occupation = PhysicalWorkerOccupation.Unassigned }).ToArray();
        var partial = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, residual, demand, agriculture);
        Assert.Equal(3_600_000UL, Assert.Single(partial.Production, p => p.Commodity == wood).ProducedMicro);
        Assert.Equal(600_000U, Assert.Single(partial.Production, p => p.Commodity == wood).EffortMicro);
    }

    [Fact]
    public void R22812ExperimentalHostPublishesSixCollectiveTurnsAndPreservesPrivateOrders()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        // Adopt the signed fine world while the original worker census
        // still has the same identity as the session's opening census.
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        // Other workers provide real demand but no independent native output.
        // This fixture-only activity change is a same-world snapshot update,
        // not part of the signed world adoption.
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.WithTacticalLocationAndActivity(w.TacticalTileId!.Value, w.CellId, PhysicalWorkerActivity.Other)),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        demo.Session.AcceptSnapshot(opening);
        Assert.Same(opening, demo.Session.Snapshot);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        const bool progressive = true;
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r2287", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64, useTemporalProgressiveDistribution: progressive);
        Assert.Equal(64, policy.MaxProducerPasses);
        var movement = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        // Select a real traversable land arrival with positive agricultural
        // yield, not an arbitrary tile or a preselected market outcome.
        var candidate = (from w in opening.Workers.Workers
            let source = w.TacticalTileId!.Value
            from crossing in incidence.FineTopology.Edges
            where crossing.IncidentCellIds.Contains(source.FineStrategicCellId) &&
                fine.CrossingAtIndices((int)crossing.IncidentCellIds[0].Value - 1,
                    (int)crossing.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground
            let destination = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
                crossing.IncidentCellIds.Single(n => n != source.FineStrategicCellId))
            from crop in crops.Keys
            where agriculture.Resolve(source, crop).WholeTurnOutput > 0 &&
                agriculture.Resolve(destination, crop).WholeTurnOutput > 0
            orderby w.Id != worker.Id, w.Id.Value, destination.FineStrategicCellId.Value, (byte)crop
            select (Worker: w, Source: source, Destination: destination, Crop: crop)).First();
        worker = candidate.Worker;
        var own = opening.Workers.Workers.Where(w => w.Owner == worker.Owner).ToArray();
        var automatic = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic);
        var foreign = opening.Civilizations.Civilizations.Single(c => c != worker.Owner);
        var fixedPolicy = new FoundationAuthoritativeEconomicSettlementPolicy(demand, policy.Cargo,
            policy.TaxRates, maxProducerPasses: 64);
        FoundationTacticalHumanAiTurnHost CreateHost(FoundationAuthoritativeEconomicSettlementPolicy selected, bool enabled = false) =>
            new(demo.Session, incidence, opening.Civilizations.Civilizations,
                new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
                enableMonetaryInfrastructure: true, settlementPolicy: selected,
                fineGeography: fine, fineSignature: signature, physicalTravelPolicy: movement,
                constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
                enableExperimentalLocalEconomy: enabled);
        Assert.Throws<InvalidOperationException>(() => CreateHost(policy));
        Assert.Throws<InvalidOperationException>(() => CreateHost(fixedPolicy, true));
        var disabled = CreateHost(fixedPolicy);
        Assert.False(disabled.ExperimentalLocalEconomyEnabled);
        Assert.Throws<InvalidOperationException>(() => disabled.QueueHumanBaseActivity(worker.Owner, worker.Id, automatic));

        var host = CreateHost(policy, true);
        Assert.True(host.ExperimentalLocalEconomyEnabled);
        Assert.True(host.PersistentOrdersEnabled);
        var initialHash = host.StateHash;
        foreach (var person in own)
            host.QueueHumanBaseActivity(person.Owner, person.Id, automatic);
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanBaseActivity(foreign, worker.Id, automatic));
        host.QueueHumanPersistentEdit(worker.Owner, new FoundationPersistentOrderEdit(
            worker.Owner, worker.Id, FoundationPersistentOrderEditKind.Insert, Index: 0,
            OrderKind: FoundationWorkerOrderKind.Move, Destination: candidate.Destination));
        Assert.All(host.GetHumanPersistentQueues(worker.Owner), q => Assert.Equal(automatic, q.BaseActivity));
        Assert.All(host.GetHumanPersistentQueues(foreign), q => Assert.Null(q.BaseActivity));
        Assert.All(host.Snapshot.WorkerOrders.Queues, q => Assert.Null(q.BaseActivity));
        Assert.Equal(initialHash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(worker.Owner).Availability);

        for (ulong turn = 1; turn <= 6; turn++)
        {
            var before = host.Snapshot;
            var beforeHash = host.StateHash;
            var previousView = host.GetLastClosedEconomicTradeView(worker.Owner);
            host.SetHumanReady(worker.Owner);
            Assert.Null(host.TryCloseTurn());
            Assert.Same(before, host.Snapshot);
            Assert.Equal(beforeHash, host.StateHash);
            Assert.Equal(previousView.EconomicCycleOrdinal, host.GetLastClosedEconomicTradeView(worker.Owner).EconomicCycleOrdinal);
            host.SetHumanReady(foreign);
            var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
            var sale = Assert.IsType<FoundationTemporalSalesSettledEvent>(receipt.TemporalEconomicSettlement);
            Assert.True(receipt.ReplayMatched);
            Assert.True(sale.UsedProgressiveDistribution);
            Assert.Equal(turn, sale.EconomicCycleOrdinal);
            Assert.Equal(turn, host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
            Assert.Equal(receipt.Hash, host.StateHash);
            Assert.Equal(0, host.PendingOrderCount);
            Assert.Equal(CanonicalWorldStateHasher.LocalPriceHistoryFormatVersion, host.StateHash.FormatVersion);
            Assert.NotEmpty(FoundationLocalBaseCalendarPlanner.ReadHistory(host.Snapshot)!);
            var timeline = Assert.IsType<FoundationPersistentWorkerTurnEvent>(receipt.PersistentTimeline);
            Assert.All(timeline.Slices.GroupBy(x => x.Worker), g =>
                Assert.Equal(1_000_000L, g.Sum(x => (long)x.DurationMicro)));
            var traveler = timeline.Slices.Where(x => x.Worker == worker.Id).ToArray();
            if (turn == 1UL)
            {
                Assert.All(traveler, x => Assert.Equal(PhysicalWorkerActivity.Traveling, x.Activity));
                Assert.Equal(400_000UL, Assert.Single(host.Snapshot.EdgeTravels.Pending, p => p.Worker == worker.Id).RemainingMicro);
            }
            else
            {
                Assert.DoesNotContain(host.Snapshot.EdgeTravels.Pending, p => p.Worker == worker.Id);
                Assert.Contains(traveler, x => x.Node == candidate.Destination && x.Activity == PhysicalWorkerActivity.Producing);
                if (turn == 2UL)
                    Assert.Equal(600_000L, traveler.Where(x => x.Activity == PhysicalWorkerActivity.Producing).Sum(x => (long)x.DurationMicro));
            }
            foreach (var civ in opening.Civilizations.Civilizations)
            {
                var view = host.GetLastClosedEconomicTradeView(civ);
                Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset, view.Availability);
                Assert.Equal((ulong?)turn, view.EconomicCycleOrdinal);
                Assert.Equal((WorldStateHash?)receipt.Hash, view.PublishedHash);
                Assert.Equal(sale.AuditedParticipantEdges![civ].ToArray(), view.UsedEdges.ToArray());
                var credit = sale.Credits.Single(x => x.Civilization == civ);
                Assert.Equal(before.Treasury.Get(civ).CashScaled + credit.CashCreditScaled,
                    host.Snapshot.Treasury.Get(civ).CashScaled);
            }
            Console.WriteLine($"R22812_HOST turn={turn} goods_micro={sale.ProducedGoodsMicro} hash={receipt.Hash.HexDigest}");
        }
        Assert.Throws<InvalidOperationException>(() => host.GetLastClosedEconomicTradeView(new CivilizationId(uint.MaxValue)));
    }

    [Fact]
    public void R22813BDefaultAutomaticHostPreservesExplicitOrdersAndEightCommoditySettlement()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(
            demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
            bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        // Adopt the signed fine world while the original worker census
        // still has the same identity as the session's opening census.
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        // Other workers provide real demand but no independent native output.
        // This fixture-only activity change is a same-world snapshot update,
        // not part of the signed world adoption.
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.WithTacticalLocationAndActivity(w.TacticalTileId!.Value, w.CellId, PhysicalWorkerActivity.Other)),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        demo.Session.AcceptSnapshot(opening);
        Assert.Same(opening, demo.Session.Snapshot);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        const bool progressive = true;
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r22813b", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)), maxProducerPasses: 64, useTemporalProgressiveDistribution: progressive, requireSafeProgressiveRoutes: true);
        Assert.Equal(64, policy.MaxProducerPasses);
        var movement = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        // Select a real traversable land arrival with positive agricultural
        // yield, not an arbitrary tile or a preselected market outcome.
        var candidate = (from w in opening.Workers.Workers
            let source = w.TacticalTileId!.Value
            from crossing in incidence.FineTopology.Edges
            where crossing.IncidentCellIds.Contains(source.FineStrategicCellId) &&
                fine.CrossingAtIndices((int)crossing.IncidentCellIds[0].Value - 1,
                    (int)crossing.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground
            let destination = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
                crossing.IncidentCellIds.Single(n => n != source.FineStrategicCellId))
            from crop in crops.Keys
            where agriculture.Resolve(source, crop).WholeTurnOutput > 0 &&
                agriculture.Resolve(destination, crop).WholeTurnOutput > 0
            orderby w.Id != worker.Id, w.Id.Value, destination.FineStrategicCellId.Value, (byte)crop
            select (Worker: w, Source: source, Destination: destination, Crop: crop)).First();
        worker = candidate.Worker;
        var own = opening.Workers.Workers.Where(w => w.Owner == worker.Owner).ToArray();
        var automatic = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic);
        var foreign = opening.Civilizations.Civilizations.Single(c => c != worker.Owner);
        var fixedPolicy = new FoundationAuthoritativeEconomicSettlementPolicy(demand, policy.Cargo,
            policy.TaxRates, maxProducerPasses: 64);
        FoundationTacticalHumanAiTurnHost CreateHost(FoundationAuthoritativeEconomicSettlementPolicy selected, bool enabled = false, bool defaultBase = false) =>
            new(demo.Session, incidence, opening.Civilizations.Civilizations,
                new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
                enableMonetaryInfrastructure: true, settlementPolicy: selected,
                fineGeography: fine, fineSignature: signature, physicalTravelPolicy: movement,
                constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
                enableExperimentalLocalEconomy: enabled, enableDefaultAutomaticBase: defaultBase);
        Assert.Throws<InvalidOperationException>(() => CreateHost(policy));
        Assert.Throws<InvalidOperationException>(() => CreateHost(fixedPolicy, true));
        Assert.Throws<InvalidOperationException>(() => CreateHost(fixedPolicy, false, true));
        var explicitOnly = CreateHost(policy, true);
        Assert.False(explicitOnly.DefaultAutomaticBaseEnabled);

        var host = CreateHost(policy, true, true);
        Assert.True(host.DefaultAutomaticBaseEnabled);
        Assert.True(host.ExperimentalLocalEconomyEnabled);
        Assert.True(host.PersistentOrdersEnabled);
        var initialHash = host.StateHash;
        var manualWorker = own.First(w => w.Id != worker.Id);
        var manual = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining);
        host.QueueHumanBaseActivity(manualWorker.Owner, manualWorker.Id, manual);
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanBaseActivity(foreign, worker.Id, automatic));
        host.QueueHumanPersistentEdit(worker.Owner, new FoundationPersistentOrderEdit(
            worker.Owner, worker.Id, FoundationPersistentOrderEditKind.Insert, Index: 0,
            OrderKind: FoundationWorkerOrderKind.Move, Destination: candidate.Destination));
        Assert.All(host.GetHumanPersistentQueues(worker.Owner), q =>
        {
            if (q.Worker == manualWorker.Id) Assert.Equal(manual, q.BaseActivity);
            else Assert.Null(q.BaseActivity);
        });
        Assert.All(host.GetHumanPersistentQueues(foreign), q => Assert.Null(q.BaseActivity));
        Assert.All(host.Snapshot.WorkerOrders.Queues, q => Assert.Null(q.BaseActivity));
        Assert.Equal(initialHash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(worker.Owner).Availability);

        for (ulong turn = 1; turn <= 3; turn++)
        {
            var before = host.Snapshot;
            var beforeHash = host.StateHash;
            var previousView = host.GetLastClosedEconomicTradeView(worker.Owner);
            host.SetHumanReady(worker.Owner);
            Assert.Null(host.TryCloseTurn());
            Assert.Same(before, host.Snapshot);
            Assert.Equal(beforeHash, host.StateHash);
            Assert.Equal(previousView.EconomicCycleOrdinal, host.GetLastClosedEconomicTradeView(worker.Owner).EconomicCycleOrdinal);
            host.SetHumanReady(foreign);
            var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
            var sale = Assert.IsType<FoundationTemporalSalesSettledEvent>(receipt.TemporalEconomicSettlement);
            Assert.True(receipt.ReplayMatched);
            Assert.True(sale.UsedProgressiveDistribution);
            Assert.True(sale.UsedSafeProgressiveRoutes);
            Assert.Equal(turn, sale.EconomicCycleOrdinal);
            Assert.Equal(turn, host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
            Assert.Equal(receipt.Hash, host.StateHash);
            Assert.Equal(0, host.PendingOrderCount);
            Assert.Equal(CanonicalWorldStateHasher.LocalPriceHistoryFormatVersion, host.StateHash.FormatVersion);
            Assert.NotEmpty(FoundationLocalBaseCalendarPlanner.ReadHistory(host.Snapshot)!);
            var timeline = Assert.IsType<FoundationPersistentWorkerTurnEvent>(receipt.PersistentTimeline);
            Assert.All(timeline.Slices.GroupBy(x => x.Worker), g =>
                Assert.Equal(1_000_000L, g.Sum(x => (long)x.DurationMicro)));
            Assert.All(host.Snapshot.WorkerOrders.Queues, q =>
            {
                if (q.Worker == manualWorker.Id) Assert.Equal(manual, q.BaseActivity);
                else Assert.Null(q.BaseActivity);
            });
            Assert.All(timeline.Slices.Where(s => s.Worker == manualWorker.Id), s =>
            {
                Assert.Equal(PhysicalWorkerActivity.Producing, s.Activity);
                Assert.Equal(PhysicalWorkerOccupation.Mining, s.Occupation);
            });
            if (turn == 1UL)
            {
                var projected = FoundationTemporalEconomicTurnProjector.ProjectPersistent(before, timeline.Slices, demand, agriculture);
                Assert.Contains(projected.Production, p => p.Commodity == wood && p.Worker != manualWorker.Id && p.ProducedMicro > 0);
                Assert.Contains(projected.Production, p => p.Commodity == stone && p.Worker == manualWorker.Id && p.ProducedMicro == 5_000_000UL);
                Assert.All(timeline.Slices.Where(s => s.Worker != worker.Id &&
                    fine.SurfaceAtIndex((int)s.Node.FineStrategicCellId.Value - 1) == FoundationFineSurfaceKind.Land), s =>
                    Assert.Equal(PhysicalWorkerActivity.Producing, s.Activity));
            }
            var traveler = timeline.Slices.Where(x => x.Worker == worker.Id).ToArray();
            if (turn == 1UL)
            {
                Assert.All(traveler, x => Assert.Equal(PhysicalWorkerActivity.Traveling, x.Activity));
                Assert.Equal(400_000UL, Assert.Single(host.Snapshot.EdgeTravels.Pending, p => p.Worker == worker.Id).RemainingMicro);
            }
            else
            {
                Assert.DoesNotContain(host.Snapshot.EdgeTravels.Pending, p => p.Worker == worker.Id);
                Assert.Contains(traveler, x => x.Node == candidate.Destination && x.Activity == PhysicalWorkerActivity.Producing);
                if (turn == 2UL)
                    Assert.Equal(600_000L, traveler.Where(x => x.Activity == PhysicalWorkerActivity.Producing).Sum(x => (long)x.DurationMicro));
            }
            foreach (var civ in opening.Civilizations.Civilizations)
            {
                var view = host.GetLastClosedEconomicTradeView(civ);
                Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset, view.Availability);
                Assert.Equal((ulong?)turn, view.EconomicCycleOrdinal);
                Assert.Equal((WorldStateHash?)receipt.Hash, view.PublishedHash);
                Assert.Equal(sale.AuditedParticipantEdges![civ].ToArray(), view.UsedEdges.ToArray());
                var credit = sale.Credits.Single(x => x.Civilization == civ);
                Assert.Equal(before.Treasury.Get(civ).CashScaled + credit.CashCreditScaled,
                    host.Snapshot.Treasury.Get(civ).CashScaled);
            }
            Console.WriteLine($"R22813B_HOST turn={turn} goods_micro={sale.ProducedGoodsMicro} hash={receipt.Hash.HexDigest}");
        }
        Assert.Throws<InvalidOperationException>(() => host.GetLastClosedEconomicTradeView(new CivilizationId(uint.MaxValue)));
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




    private sealed class R233AProbePolicy : FoundationPersistentEconomicAiPolicy
    {
        public int Calls { get; private set; }
        public FoundationPersistentAiView? View { get; private set; }
        public Func<FoundationPersistentAiView, IReadOnlyList<FoundationPersistentOrderEdit>> Decide { get; set; } =
            _ => Array.Empty<FoundationPersistentOrderEdit>();

        public Func<FoundationPersistentAiView, FoundationAiFundingIntent?> Fund { get; set; } = _ => null;

        public override FoundationAiFundingIntent? PlanFunding(FoundationPersistentAiView view) => Fund(view);

        public override IReadOnlyList<FoundationPersistentOrderEdit> PlanPersistent(FoundationPersistentAiView view)
        {
            Calls++;
            View = view;
            return Decide(view);
        }
    }

    [Fact]
    public void R233APersistentAiUsesPrivateOrdinaryOrdersAndExactHumanEquivalentReplay()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var aiOwner = worker.Owner;
        var human = world.Civilizations.Civilizations.Single(c => c != aiOwner);
        FoundationTacticalHumanAiTurnHost CreateHost(R233AProbePolicy? policy)
        {
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
            var policies = new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>();
            if (policy is not null) policies.Add(aiOwner, policy);
            return new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
                policy is null ? beginning.Civilizations.Civilizations.ToArray() : new[] { human },
                policies, enableMonetaryInfrastructure: true,
                settlementPolicy: Policy(beginning.Civilizations.Civilizations),
                fineGeography: fine, fineSignature: signature,
                physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                constructionMoneyMicroPerWholeWork: 1_000_000UL);
        }

        var edit = new FoundationPersistentOrderEdit(aiOwner, worker.Id,
            FoundationPersistentOrderEditKind.Insert, OrderKind: FoundationWorkerOrderKind.Occupation,
            Occupation: PhysicalWorkerOccupation.Agriculture);
        var probe = new R233AProbePolicy();
        var host = CreateHost(probe);
        var before = host.Snapshot;
        var hash = host.StateHash;
        var foreignWorker = before.Workers.Workers.First(w => w.Owner == human);
        // A valid first edit followed by a forged foreign worker must stage nothing.
        probe.Decide = _ => new[] { edit, edit with { Worker = foreignWorker.Id } };
        Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
        Assert.Equal(0, host.PendingOrderCount);
        Assert.Empty(host.ReadyCivilizations);
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);

        // A separately forged issuer must also fail before admission.
        probe.Decide = _ => new[] { edit with { Issuer = human, Worker = foreignWorker.Id } };
        Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
        Assert.Equal(0, host.PendingOrderCount);
        Assert.Empty(host.ReadyCivilizations);
        Assert.Same(before, host.Snapshot);

        probe.Decide = view =>
        {
            Assert.Equal(aiOwner, view.Owner);
            Assert.Equal(host.NextTurn, view.Turn);
            Assert.Empty(view.LocalEconomics);
            Assert.Equal(before.Workers.Workers.Where(w => w.Owner == aiOwner)
                .Select(w => w.Id.Value).OrderBy(id => id), view.Workers.Select(w => w.Id.Value));
            Assert.All(view.Workers, w =>
            {
                Assert.Equal(aiOwner, w.Queue.Owner);
                Assert.Equal(w.Id, w.Queue.Worker);
                Assert.Empty(w.Queue.Orders);
                Assert.False(w.Crossing);
            });
            return new[] { edit };
        };
        host.PrepareAiCivilizations();
        int calls = probe.Calls;
        host.PrepareAiCivilizations();
        Assert.Equal(calls, probe.Calls);
        Assert.Equal(1, host.PendingOrderCount);
        Assert.Single(host.ReadyCivilizations);
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Throws<InvalidOperationException>(() => host.GetHumanPersistentQueues(aiOwner));
        Assert.All(host.GetHumanPersistentQueues(human), q => Assert.Empty(q.Orders));
        Assert.Null(host.TryCloseTurn());
        Assert.Equal(calls, probe.Calls);
        host.SetHumanReady(human);
        var actual = host.TryCloseTurn();
        Assert.NotNull(actual);
        Assert.True(actual!.ReplayMatched);
        Assert.Equal(edit, Assert.Single(actual.PersistentEdits).Edit);
        Assert.Equal(host.StateHash, actual.Hash);
        Assert.Equal(0, host.PendingOrderCount);

        // Same starting world and ordinary human-issued command must close
        // to exactly the same authoritative state, including the journal.
        var manual = CreateHost(null);
        Assert.Equal(hash, manual.StateHash);
        manual.QueueHumanPersistentEdit(aiOwner, edit);
        manual.SetHumanReady(aiOwner);
        manual.SetHumanReady(human);
        var expected = manual.TryCloseTurn();
        Assert.NotNull(expected);
        Assert.Equal(expected!.Hash, actual.Hash);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(Assert.Single(expected.PersistentEdits), Assert.Single(actual.PersistentEdits));

        // The next AI plan receives exactly the same own-node read available
        // to an equivalent human, not the foreign or global evidence object.
        var closedHash = host.StateHash;
        probe.Decide = view =>
        {
            Assert.False(view.AutomaticBaseAvailable);
            Assert.False(view.DefaultAutomaticBase);
            var reads = view.Workers.Select(w => w.Node).Distinct()
                .OrderBy(node => node.FineStrategicCellId.Value)
                .Select(node => manual.GetLastClosedLocalEconomics(aiOwner, node))
                .OfType<FoundationViewerLocalEconomicClose>().ToArray();
            Assert.NotEmpty(reads);
            Assert.Equal(reads.Length, view.LocalEconomics.Count);
            for (int i = 0; i < reads.Length; i++)
            {
                var observed = view.LocalEconomics[i];
                Assert.Equal(aiOwner, observed.Viewer);
                Assert.Equal(reads[i].Node, observed.Node);
                Assert.Equal(1UL, observed.EconomicCycleOrdinal);
                Assert.Equal(closedHash, observed.CommittedWorldHash);
                Assert.Equal(reads[i].Commodities.ToArray(), observed.Commodities.ToArray());
            }
            return Array.Empty<FoundationPersistentOrderEdit>();
        };
        host.PrepareAiCivilizations();
        Assert.Equal(closedHash, host.StateHash);
        Assert.Throws<InvalidOperationException>(() =>
            host.GetLastClosedLocalEconomics(aiOwner, worker.TacticalTileId!.Value));
    }

    [Theory]
    [InlineData(0)] // exact human-equivalent funding
    [InlineData(1)] // human reserves first
    [InlineData(2)] // AI reserves first
    [InlineData(3)] // foreign assignment
    [InlineData(4)] // insufficient cash
    [InlineData(5)] // mixed dependent plan rejected
    [InlineData(6)] // unknown link
    public void R233D1PrivateFundingUsesOrdinaryJournalAndCollectiveConflictGate(int mode)
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var owner = worker.Owner;
        var human = world.Civilizations.Civilizations.Single(c => c != owner);
        var source = worker.TacticalTileId!.Value;
        var crossing = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(source.FineStrategicCellId) &&
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var target = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            crossing.IncidentCellIds.Single(n => n != source.FineStrategicCellId));
        FoundationTacticalHumanAiTurnHost CreateHost(R233AProbePolicy? ai)
        {
            var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
                new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                    new GoldbergParameters(3, 0)), workersPerCivilization: 3);
            var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
            var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
                .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
            var beginning = FoundationPersistedEdgeTravelTransitions.DeployEmpty(
                bound.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                    bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
                .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
                .DeployInitialWorkerOrders();
            demo.Session.AcceptFineFirstOpening(beginning, incidence, fine, signature);
            var scopes = new Dictionary<CivilizationId, FoundationCivilizationKnowledgeScope>
            {
                [owner] = new(new[] { source, target }, mode == 6
                    ? Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>() : new[] { (source, target) }),
                [human] = new(new[] { source, target }, new[] { (source, target) })
            };
            return new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
                ai is null ? beginning.Civilizations.Civilizations.ToArray() : new[] { human },
                ai is null ? new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>() :
                    new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy> { [owner] = ai },
                enableMonetaryInfrastructure: true, settlementPolicy: Policy(beginning.Civilizations.Civilizations),
                fineGeography: fine, fineSignature: signature,
                physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                constructionMoneyMicroPerWholeWork: 1_000_000UL, knowledgeScopes: scopes);
        }
        var probe = new R233AProbePolicy();
        var host = CreateHost(probe);
        var before = host.Snapshot;
        var hash = host.StateHash;
        var foreign = before.Workers.Workers.First(w => w.Owner == human);
        var assignments = new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) };
        if (mode == 0)
        {
            // Exclusive construction slot before activation; exclusive
            // infrastructure title afterwards. No foreign debit or takeover.
            var fundedWorld = before.FundConstruction(owner, edge, 600_000UL, 600_000UL);
            Assert.Throws<InvalidOperationException>(() =>
                fundedWorld.FundConstruction(human, edge, 300_000UL, 300_000UL));
            Assert.Throws<InvalidOperationException>(() =>
                fundedWorld.FundConstruction(owner, edge, 300_000UL, 300_000UL));
            var project = Assert.Single(fundedWorld.Constructions.Projects);
            Assert.Equal(owner, project.Investor);
            var complete = fundedWorld.WithConstructions(new FoundationConstructionRuntimeState(
                new[] { project with { CompletedWorkMicro = project.RequiredWorkMicro } }));
            var activated = complete.ActivateCompletedConstruction(project.Id);
            Assert.Equal(owner, activated.Transport.Edges.Single(e => e.Edge == edge).InfrastructureOwner);
            Assert.Throws<InvalidOperationException>(() =>
                activated.FundConstruction(human, edge, 300_000UL, 300_000UL));
            Assert.Equal(BigInteger.Zero, activated.Treasury.Get(human).TotalInfrastructureInvestmentScaled);
            var reinvested = activated.FundConstruction(owner, edge, 300_000UL, 300_000UL);
            Assert.Equal(owner, Assert.Single(reinvested.Constructions.Projects, p => !p.IsActivated).Investor);
            Assert.Equal(hash, host.StateHash);
        }
        probe.Fund = view =>
        {
            Assert.Equal(owner, view.Owner);
            Assert.True(view.FundingAvailable);
            Assert.Equal((BigInteger?)GlobalArenaMoneyScale.FromMoneyMicro(5_000_000UL), view.OwnCashScaled);
            return new FoundationAiFundingIntent(edge, mode == 4 ? 5_000_001UL : 600_000UL,
                mode == 3 ? new[] { new FoundationFundedWorkerInsertion(foreign.Id, 0) } : assignments);
        };
        if (mode == 5)
            probe.Decide = _ => new[] { new FoundationPersistentOrderEdit(owner, worker.Id,
                FoundationPersistentOrderEditKind.Insert, OrderKind: FoundationWorkerOrderKind.Occupation,
                Occupation: PhysicalWorkerOccupation.Agriculture) };
        if (mode is 3 or 4 or 5 or 6)
        {
            Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
            Assert.Same(before, host.Snapshot);
            Assert.Equal(hash, host.StateHash);
            Assert.Equal(0, host.PendingOrderCount);
            Assert.Empty(host.ReadyCivilizations);
            return;
        }
        if (mode == 1)
            host.QueueHumanFundedConstruction(human, edge, 300_000UL, Array.Empty<FoundationFundedWorkerInsertion>());
        host.PrepareAiCivilizations();
        if (mode == 2)
            host.QueueHumanFundedConstruction(human, edge, 300_000UL, Array.Empty<FoundationFundedWorkerInsertion>());
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(mode == 0 ? 1 : 2, host.PendingOrderCount);
        Assert.Throws<InvalidOperationException>(() => host.GetHumanPersistentFunding(owner));
        Assert.Equal(mode == 0 ? 0 : 1, host.GetHumanPersistentFunding(human).Count);
        Assert.Null(host.TryCloseTurn());
        host.SetHumanReady(human);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        var funded = Assert.Single(receipt.PersistentFunding);
        Assert.Equal(edge, funded.Edge);
        // First turn starts with the lowest canonical civilization ID,
        // irrespective of controller, submission order or funding amount.
        var winner = mode == 0 || owner.Value < human.Value ? owner : human;
        Assert.Equal(winner, funded.Investor);
        Assert.Equal(winner == owner ? 600_000UL : 300_000UL, funded.MoneyMicro);
        Assert.Equal(winner == owner ? GlobalArenaMoneyScale.FromMoneyMicro(600_000UL) : BigInteger.Zero,
            host.Snapshot.Treasury.Get(owner).TotalInfrastructureInvestmentScaled);
        Assert.Equal(winner == human ? GlobalArenaMoneyScale.FromMoneyMicro(300_000UL) : BigInteger.Zero,
            host.Snapshot.Treasury.Get(human).TotalInfrastructureInvestmentScaled);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(0, host.PendingOrderCount);

        var manual = CreateHost(null);
        Assert.Equal(hash, manual.StateHash);
        if (winner == owner)
            manual.QueueHumanFundedConstruction(owner, edge, 600_000UL, assignments);
        else
            manual.QueueHumanFundedConstruction(human, edge, 300_000UL, Array.Empty<FoundationFundedWorkerInsertion>());
        manual.SetHumanReady(owner);
        manual.SetHumanReady(human);
        var expected = Assert.IsType<FoundationTacticalCollectiveReceipt>(manual.TryCloseTurn());
        Assert.Equal(expected.Hash, receipt.Hash);
        Assert.Equal(expected.Revision, receipt.Revision);

        if (mode is 1 or 2)
        {
            // Both humans may privately reserve the same edge. Reverse the
            // admission/readiness order and attach a dependent edit to the loser.
            var contested = CreateHost(null);
            var loser = winner == owner ? human : owner;
            var losingWorker = loser == owner ? worker.Id : foreign.Id;
            var order = mode == 1 ? new[] { human, owner } : new[] { owner, human };
            foreach (var civ in order)
                contested.QueueHumanFundedConstruction(civ, edge, civ == owner ? 600_000UL : 300_000UL,
                    civ == loser ? new[] { new FoundationFundedWorkerInsertion(losingWorker, 0) } :
                    civ == owner ? assignments : Array.Empty<FoundationFundedWorkerInsertion>());
            Assert.Single(contested.GetHumanPersistentFunding(owner));
            Assert.Single(contested.GetHumanPersistentFunding(human));
            Assert.Throws<InvalidOperationException>(() => contested.QueueHumanFundedConstruction(
                loser, edge, 1UL, Array.Empty<FoundationFundedWorkerInsertion>()));
            contested.QueueHumanPersistentEdit(loser, new FoundationPersistentOrderEdit(loser, losingWorker,
                FoundationPersistentOrderEditKind.Remove, OrderId: 1UL));
            Assert.Equal(hash, contested.StateHash);
            foreach (var civ in order.Reverse()) contested.SetHumanReady(civ);
            var resolved = Assert.IsType<FoundationTacticalCollectiveReceipt>(contested.TryCloseTurn());
            Assert.True(resolved.ReplayMatched);
            Assert.Equal(winner, Assert.Single(resolved.PersistentFunding).Investor);
            Assert.Empty(resolved.PersistentEdits);
            Assert.Equal(expected.Hash, resolved.Hash);
            Assert.Equal(expected.Revision, resolved.Revision);
            Assert.Equal(0, contested.PendingOrderCount);
        }
        if (mode == 1)
        {
            // No project in turn one; the same contest in turn two rotates
            // priority to the other civilization without changing controller.
            var rotated = CreateHost(null);
            rotated.SetHumanReady(owner);
            rotated.SetHumanReady(human);
            var openingClose = Assert.IsType<FoundationTacticalCollectiveReceipt>(rotated.TryCloseTurn());
            Assert.True(openingClose.ReplayMatched);
            Assert.Equal(2UL, rotated.NextTurn.Value);
            rotated.QueueHumanFundedConstruction(owner, edge, 600_000UL, assignments);
            rotated.QueueHumanFundedConstruction(human, edge, 300_000UL, Array.Empty<FoundationFundedWorkerInsertion>());
            rotated.SetHumanReady(owner);
            rotated.SetHumanReady(human);
            var second = Assert.IsType<FoundationTacticalCollectiveReceipt>(rotated.TryCloseTurn());
            Assert.True(second.ReplayMatched);
            Assert.Equal(winner == owner ? human : owner, Assert.Single(second.PersistentFunding).Investor);
        }
    }

    [Fact]
    public void R233D2InvestmentPolicyRequiresPositivePaybackBudgetAndAvailableBuilder()
    {
        var (world, worker, _, _, incidence, _) = Fixture();
        var node = worker.TacticalTileId!.Value;
        var ids = incidence.FineTopology.Edges.Where(e => e.IncidentCellIds.Contains(node.FineStrategicCellId))
            .OrderBy(e => e.Id.Value).Take(2)
            .Select(e => new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, e.Id)).ToArray();
        var workers = world.Workers.Workers.Where(w => w.Owner == worker.Owner)
            .OrderBy(w => w.Id.Value).Select(w => new FoundationPersistentAiWorker(w.Id, node,
                PhysicalWorkerActivity.Producing, false, FoundationWorkerOrderQueue.Empty(w.Id, w.Owner))).ToArray();
        Assert.Equal(3, workers.Length);
        var good = new FoundationAiInvestmentOpportunity(ids[0], node, 1_000_000UL,
            100_000UL, 100_000UL, 80_000UL);
        var bad = good with { Edge = ids[1], CargoMicro = 100_000UL };
        var view = new FoundationPersistentAiView(worker.Owner, new TurnNumber(1UL), workers)
        {
            FundingAvailable = true, AutomaticBaseAvailable = true, DefaultAutomaticBase = true,
            OwnCashScaled = GlobalArenaMoneyScale.FromMoneyMicro(1_000_000UL),
            InvestmentOpportunities = new[] { bad, good }
        };
        var policy = new FoundationPersistentInvestmentAiPolicy();
        Assert.Empty(policy.PlanPersistent(view));
        var chosen = Assert.IsType<FoundationAiFundingIntent>(policy.PlanFunding(view));
        Assert.Equal(good.Edge, chosen.Edge);
        Assert.Equal(good.MoneyMicro, chosen.MoneyMicro);
        Assert.Equal(new FoundationFundedWorkerInsertion(workers[^1].Id, 0), Assert.Single(chosen.Assignments));
        var tie = good with { Edge = ids[1] };
        Assert.Equal(ids[0], policy.PlanFunding(view with { InvestmentOpportunities = new[] { tie, good } })!.Edge);
        Assert.Null(policy.PlanFunding(view with { OwnCashScaled = view.OwnCashScaled - 1 }));
        Assert.Null(policy.PlanFunding(view with { InvestmentOpportunities = new[] { bad } }));
        Assert.NotNull(policy.PlanFunding(view with { InvestmentOpportunities = new[] { bad with
            { UnitCostBeforeMicro = 1_000_000UL, UnitCostAfterMicro = 800_000UL } } }));
        Assert.Null(policy.PlanFunding(view with { InvestmentOpportunities = new[] { good with
            { CargoMicro = 0UL, UnitCostBeforeMicro = 100_000_000UL, UnitCostAfterMicro = 1UL } } }));
        Assert.Null(policy.PlanFunding(view with { InvestmentOpportunities = new[] { good with { UnitCostAfterMicro = good.UnitCostBeforeMicro } } }));
        Assert.Null(policy.PlanFunding(view with { Workers = workers.Take(2).ToArray() }));
        Assert.Null(policy.PlanFunding(view with { Workers = new[] { workers[0], workers[1], workers[2] with { Crossing = true } } }));
        Assert.Null(policy.PlanFunding(view with { FundingAvailable = false }));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void R234D2CCollectiveCloseReplaysFreeVoyageWithSeaDemandAndLandBirths(bool priced, bool settle, bool paidTrip)
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var worker = bound.Workers.Workers.First();
        var router = new FoundationConstructionApproachRouter(demo.Session.GeneratedWorld,
            incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var naval = router.NavalTransitions;
        var parameters = incidence.Refinement.FineParameters;
        var land = incidence.FineTopology.Cells.Where(c =>
            fine.SurfaceAtIndex((int)c.Id.Value - 1) == FoundationFineSurfaceKind.Land &&
            fine.CanTraverseOnFoot((int)c.Id.Value - 1)).ToArray();
        var candidate = (from a in land
            let origin = new PhysicalTacticalTileId(parameters, a.Id)
            from b in a.AdjacentCellIds
            where fine.CrossingAtIndices((int)a.Id.Value - 1, (int)b.Value - 1) == FoundationFineTransportCrossingKind.Port
            let water = new PhysicalTacticalTileId(parameters, b)
            from c in land
            where c.Id != a.Id
            let target = new PhysicalTacticalTileId(parameters, c.Id)
            where naval.FindNextLandingStep(water, target).HasValue
            select (Origin: origin, Water: water, Target: target)).First();
        var foreignNode = new PhysicalTacticalTileId(parameters, land.First(c => c.Id != candidate.Origin.FineStrategicCellId &&
            c.Id != candidate.Target.FineStrategicCellId).Id);
        var census = bound.Workers.Workers.Select(w =>
        {
            var node = w.Owner == worker.Owner ? candidate.Origin : foreignNode;
            var region = incidence.TileIncidences[(int)node.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
            return w.WithTacticalLocationAndActivity(node, region, PhysicalWorkerActivity.Other).WithUniformCityDemography();
        });
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.WithWorkers(
            new PhysicalWorkerRuntimeState(census, tacticalLocationsAuthoritative: true, occupationsAuthoritative: true))
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))));
        if (paidTrip)
        {
            var edge = incidence.FineTopology.Edges.Single(e =>
                e.IncidentCellIds.Contains(candidate.Origin.FineStrategicCellId) &&
                e.IncidentCellIds.Contains(candidate.Water.FineStrategicCellId));
            var foreign = opening.Civilizations.Civilizations.Single(c => c != worker.Owner);
            opening = opening.RecordMonetaryInfrastructureInvestment(foreign,
                new PhysicalTacticalEdgeId(parameters, edge.Id), 1UL);
        }
        opening = opening.WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        foreach (var w in opening.Workers.Workers)
            opening = opening.SetWorkerBaseActivity(w.Owner, w.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        opening = naval.Deploy(opening, priced ? (paidTrip ? 1_000_000_000_000_000UL : 2_000_000UL) : null);
        var nodes = new List<PhysicalTacticalTileId> { candidate.Origin, candidate.Water };
        var links = new List<(PhysicalTacticalTileId, PhysicalTacticalTileId)> { (candidate.Origin, candidate.Water) };
        var at = candidate.Water;
        while (at != candidate.Target)
        {
            var next = naval.FindNextLandingStep(at, candidate.Target)!.Value;
            Assert.DoesNotContain(next, nodes);
            nodes.Add(next);
            links.Add((at, next));
            at = next;
        }
        var scope = new FoundationCivilizationKnowledgeScope(nodes, links);
        var scopes = opening.Civilizations.Civilizations.ToDictionary(c => c, _ => scope);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>().ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var economy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r234d2c", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64, useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), economy, fine, signature,
            persistentMoneyMicroPerWholeWork: 1_000_000UL, enableDemographicGrowth: true,
            agriculture: agriculture, enableExperimentalAutomaticBase: true, enableLocalPriceAllocation: true,
            enableDefaultAutomaticBase: true, enableExclusiveNodeLabor: true,
            navalDepartureTimeMicro: 1_400_000UL, navalScopes: scopes, enableNavalSettlement: settle);
        var hasher = new CanonicalWorldStateHasher();
        var state = opening;
        bool sawSeaDemand = false, sawLanding = false;
        ulong lastTurn = checked(1UL + ((ulong)links.Count * 1_400_000UL + 999_999UL) / 1_000_000UL);
        for (ulong turn = 1; turn <= lastTurn; turn++)
        {
            var context = new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL));
            ISimulationCommand[] commands = turn == 2 ? new ISimulationCommand[]
            {
                new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 1UL),
                    new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                        FoundationPersistentOrderEditKind.Insert, Index: 0,
                        OrderKind: FoundationWorkerOrderKind.Move, Destination: candidate.Target))
            } : Array.Empty<ISimulationCommand>();
            if (turn == 2 && !settle)
            {
                // A payable vessel cannot enter the collective close before
                // the financial settlement contract exists.
                var paid = naval.Embark(state, worker.Id, candidate.Water, turn, 1_400_000UL,
                    _ => new FoundationClosedNodeWorkEvidence(1_000_000U, 0UL, 1_000_000UL));
                var paidHash = hasher.Compute(paid);
                Assert.Throws<InvalidOperationException>(() => phase.Resolve(
                    new TurnResolutionInput(paid, commands, context), paid));
                Assert.Equal(paidHash, hasher.Compute(paid));
            }
            var resolved = phase.Resolve(new TurnResolutionInput(state, commands, context), state);
            var next = resolved.ResultingWorldState;
            var timeline = Assert.Single(resolved.Events.OfType<FoundationPersistentWorkerTurnEvent>());
            var sale = Assert.Single(resolved.Events.OfType<FoundationTemporalSalesSettledEvent>());
            var births = Assert.Single(resolved.Events.OfType<FoundationPhysicalBirthBatchEvent>());
            Assert.Equal(turn, next.Treasury.LastSettledEconomicCycleOrdinal);
            Assert.Equal(turn, next.EdgeTravels.PriorWorkTurn);
            Assert.Equal(settle ? 30U : priced ? 29U : 28U, hasher.Compute(next).FormatVersion);
            Assert.Equal(hasher.Compute(next), hasher.Compute(phase.ReplayPersistentJournal(state, resolved.EventLog, context)));
            Assert.Equal(sale.GrossSalesScaled, sale.TotalCivilizationCreditsScaled + sale.PhysicalFreightScaled);
            if (settle)
            {
                var debit = Assert.IsType<FoundationNavalSettlementEvent>(resolved.Events.Last());
                Assert.Equal(turn, next.Treasury.LastNavalSettlementCycleOrdinal);
                Assert.Equal(timeline.NavalLandings.Count, debit.Debits.Count);
                foreach (var account in next.Treasury.Accounts)
                {
                    var amount = debit.Debits.Where(d => d.Landing.Voyage.Owner == account.Civilization)
                        .Aggregate(BigInteger.Zero, (sum, d) => sum + d.AmountScaled);
                    var credit = sale.Credits.Single(c => c.Civilization == account.Civilization).CashCreditScaled;
                    var previous = state.Treasury.Get(account.Civilization);
                    Assert.Equal(previous.CashScaled + credit - amount, account.CashScaled);
                    Assert.Equal(previous.TotalNavalFareDebitsScaled + amount, account.TotalNavalFareDebitsScaled);
                }
                var omitted = new SimulationEventLog(resolved.EventLog.Entries.Take(resolved.EventLog.Entries.Count - 1).ToArray());
                Assert.Throws<InvalidOperationException>(() => phase.ReplayPersistentJournal(state, omitted, context));
                if (debit.Debits.Count > 0)
                {
                    var line = Assert.Single(debit.Debits);
                    var forged = debit with { Debits = new[] { line with { AmountScaled = line.AmountScaled + 1 } } };
                    var entries = resolved.EventLog.Entries.Take(resolved.EventLog.Entries.Count - 1).Append(
                        new SimulationEventLogEntry((ulong)resolved.EventLog.Entries.Count, forged, wasEligible: true, wasExecuted: true)).ToArray();
                    Assert.Throws<InvalidOperationException>(() => phase.ReplayPersistentJournal(state, new SimulationEventLog(entries), context));
                }
            }
            Assert.All(births.Newborns, b => Assert.Equal(FoundationFineSurfaceKind.Land,
                fine.SurfaceAtIndex((int)b.TacticalTileId!.Value.FineStrategicCellId.Value - 1)));
            var waterSlices = timeline.Slices.Where(s => s.Worker == worker.Id &&
                fine.SurfaceAtIndex((int)s.Node.FineStrategicCellId.Value - 1) != FoundationFineSurfaceKind.Land).ToArray();
            foreach (var slice in waterSlices)
            {
                Assert.Equal(PhysicalWorkerActivity.Traveling, slice.Activity);
                var rows = sale.AuditedLocalEconomics!.Read(worker.Owner, slice.Node)!;
                Assert.Contains(rows, r => r.DemandMicro > 0UL);
                Assert.All(rows, r => Assert.Equal(0UL, r.ProducedMicro));
                sawSeaDemand = true;
            }
            if (timeline.NavalLandings.Count > 0)
            {
                var landing = Assert.Single(timeline.NavalLandings);
                Assert.Equal(candidate.Target, landing.Destination);
                if (paidTrip)
                {
                    Assert.Equal(landing.Voyage.PaidDenominator, landing.Voyage.PaidNumerator);
                    Assert.True(next.Treasury.Get(worker.Owner).CashScaled.Sign < 0);
                    Assert.Throws<InvalidOperationException>(() => next.Treasury.DebitConstruction(worker.Owner, 1UL));
                    Assert.Throws<InvalidOperationException>(() =>
                        FoundationNavalSettlement.Record(state, next, timeline, context));
                }
                else
                {
                    Assert.Equal(0UL, landing.Voyage.PaidNumerator);
                    if (priced) Assert.Equal(BigInteger.Zero, FoundationNavalFareQuote.CalculateScaled(landing.Voyage,
                        next.EdgeTravels.NavalCargoMicroPerWorker!.Value));
                }
                sawLanding = true;
                Assert.Empty(next.EdgeTravels.NavalVoyages!);
            }
            state = next;
        }
        Assert.True(sawSeaDemand);
        Assert.True(sawLanding);
        Assert.Equal(candidate.Target, state.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
        if (paidTrip)
        {
            var debtor = state.Treasury.Get(worker.Owner);
            Assert.True(debtor.CashScaled.Sign < 0);
            var context = new SimulationContext(new TurnNumber(lastTurn + 1UL), new SimulationSeed(712UL));
            var following = phase.Resolve(new TurnResolutionInput(state, Array.Empty<ISimulationCommand>(), context), state);
            var followingDebit = Assert.IsType<FoundationNavalSettlementEvent>(following.Events.Last());
            Assert.Empty(followingDebit.Debits);
            var followingSale = Assert.Single(following.Events.OfType<FoundationTemporalSalesSettledEvent>());
            var cash = following.ResultingWorldState.Treasury.Get(worker.Owner);
            Assert.Equal(debtor.CashScaled + followingSale.Credits.Single(c => c.Civilization == worker.Owner).CashCreditScaled, cash.CashScaled);
            Assert.Equal(debtor.TotalNavalFareDebitsScaled, cash.TotalNavalFareDebitsScaled);
            Assert.Equal(hasher.Compute(following.ResultingWorldState),
                hasher.Compute(phase.ReplayPersistentJournal(state, following.EventLog, context)));
            // Use only actual verified market receipts, not the runtime's
            // private credit capability. Investment eligibility follows
            // the resulting balance, including partial debt amortization.
            if (cash.CashScaled < GlobalArenaMoneyScale.FromMoneyMicro(1UL))
                Assert.Throws<InvalidOperationException>(() =>
                    following.ResultingWorldState.Treasury.DebitConstruction(worker.Owner, 1UL));
            else
                Assert.Equal(cash.CashScaled - GlobalArenaMoneyScale.FromMoneyMicro(1UL),
                    following.ResultingWorldState.Treasury.DebitConstruction(worker.Owner, 1UL).Get(worker.Owner).CashScaled);
        }
    }

    [Fact]
    public void R234D2BPreviousWorkIsCanonicalAndNavalDepartureRespectsKnowledge()
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var worker = bound.Workers.Workers.First();
        var router = new FoundationConstructionApproachRouter(demo.Session.GeneratedWorld,
            incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var naval = router.NavalTransitions;
        var parameters = incidence.Refinement.FineParameters;
        var land = incidence.FineTopology.Cells.Where(c =>
            fine.SurfaceAtIndex((int)c.Id.Value - 1) == FoundationFineSurfaceKind.Land &&
            fine.CanTraverseOnFoot((int)c.Id.Value - 1)).ToArray();
        var candidate = (from a in land
            let origin = new PhysicalTacticalTileId(parameters, a.Id)
            from b in a.AdjacentCellIds
            where fine.CrossingAtIndices((int)a.Id.Value - 1, (int)b.Value - 1) == FoundationFineTransportCrossingKind.Port
            let water = new PhysicalTacticalTileId(parameters, b)
            from c in land
            where c.Id != a.Id
            let target = new PhysicalTacticalTileId(parameters, c.Id)
            where naval.FindNextLandingStep(water, target).HasValue
            select (Origin: origin, Water: water, Target: target)).First();
        var region = incidence.TileIncidences[(int)candidate.Origin.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.WithWorkers(
            new PhysicalWorkerRuntimeState(bound.Workers.Workers.Select(w => w.Id == worker.Id ?
                w.WithTacticalLocationAndActivity(candidate.Origin, region, PhysicalWorkerActivity.Other) : w),
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true))
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders()
            .SetWorkerBaseActivity(worker.Owner, worker.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        opening = naval.Deploy(opening);
        var scheduler = new FoundationPersistentWorkerTurnScheduler(router,
            enableNavalContinuation: true, navalDepartureTimeMicro: 1_400_000UL);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Null(naval.TryDepart(opening, opening, worker.Id, candidate.Target, 1, 1_400_000UL, null));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var (closed, ev) = scheduler.Record(opening, context);
        Assert.Equal(28U, hasher.Compute(closed).FormatVersion);
        Assert.Equal(1UL, closed.EdgeTravels.PriorWorkTurn);
        Assert.Equal(hasher.Compute(closed), hasher.Compute(scheduler.Execute(opening, ev, context)));
        Assert.Contains(closed.EdgeTravels.PriorWork!, w => w.Node == candidate.Origin && w.Owner == worker.Owner);
        var noLinks = new FoundationCivilizationKnowledgeScope(
            new[] { candidate.Origin, candidate.Water, candidate.Target },
            Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>());
        Assert.Null(naval.TryDepart(closed, closed, worker.Id, candidate.Target, 2, 1_400_000UL, noLinks));
        Assert.Null(naval.TryDepart(closed, closed, worker.Id, candidate.Target, 3, 1_400_000UL, null));
        var departure = Assert.IsType<WorldState>(naval.TryDepart(closed, closed,
            worker.Id, candidate.Target, 2, 1_400_000UL, null));
        Assert.Equal(2UL, Assert.Single(departure.EdgeTravels.NavalVoyages!).EmbarkationTurn);
        Assert.Equal(1UL, departure.EdgeTravels.PriorWorkTurn);
        Assert.Equal(28U, hasher.Compute(departure).FormatVersion);
        Assert.Equal(hasher.Compute(departure), hasher.Compute(naval.TryDepart(closed, closed,
            worker.Id, candidate.Target, 2, 1_400_000UL, null)!));
        // Only the actual sea route is known. Even if the destination is
        // on the same continent, no unknown ground shortcut is authorized.
        var nodes = new List<PhysicalTacticalTileId> { candidate.Origin, candidate.Water };
        var links = new List<(PhysicalTacticalTileId, PhysicalTacticalTileId)> { (candidate.Origin, candidate.Water) };
        var at = candidate.Water;
        while (at != candidate.Target)
        {
            var next = naval.FindNextLandingStep(at, candidate.Target)!.Value;
            Assert.DoesNotContain(next, nodes);
            nodes.Add(next);
            links.Add((at, next));
            at = next;
        }
        var scope = new FoundationCivilizationKnowledgeScope(nodes, links);
        var scopes = closed.Civilizations.Civilizations.ToDictionary(c => c, _ => scope);
        var scoped = new FoundationPersistentWorkerTurnScheduler(router,
            enableNavalContinuation: true, navalDepartureTimeMicro: 1_400_000UL, navalScopes: scopes);
        var queued = closed.InsertWorkerOrder(worker.Owner, worker.Id, 0,
            FoundationWorkerOrderKind.Move, candidate.Target);
        var second = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var (sailed, timeline) = scoped.Record(queued, second);
        Assert.Equal(400_000UL, Assert.Single(sailed.EdgeTravels.Pending, p => p.Worker == worker.Id).RemainingMicro);
        Assert.Equal(2UL, Assert.Single(sailed.EdgeTravels.NavalVoyages!).EmbarkationTurn);
        Assert.All(timeline.Slices.Where(s => s.Worker == worker.Id),
            s => Assert.Equal(PhysicalWorkerActivity.Traveling, s.Activity));
        Assert.Equal(hasher.Compute(sailed), hasher.Compute(scoped.Execute(queued, timeline, second)));
        Assert.Equal(2UL, sailed.EdgeTravels.PriorWorkTurn);

    }

    [Fact]
    public void R234D2ANavalCalendarContinuesAndReplaysLandingWithResidualWork()
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var worker = bound.Workers.Workers.First();
        var coast = incidence.FineTopology.Edges.First(e =>
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Port &&
            e.IncidentCellIds.Any(n => fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)n.Value - 1)));
        var landId = coast.IncidentCellIds.Single(n =>
            fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land);
        var origin = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, landId);
        var water = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            coast.IncidentCellIds.Single(n => n != landId));
        var region = incidence.TileIncidences[(int)landId.Value - 1].IncidentCoarseCellIds[0];
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.WithWorkers(
            new PhysicalWorkerRuntimeState(bound.Workers.Workers.Select(w => w.Id == worker.Id ?
                w.WithTacticalLocationAndActivity(origin, region, PhysicalWorkerActivity.Other) : w),
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true))
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders()
            .SetWorkerBaseActivity(worker.Owner, worker.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining))
            .InsertWorkerOrder(worker.Owner, worker.Id, 0, FoundationWorkerOrderKind.Move, origin);
        var router = new FoundationConstructionApproachRouter(demo.Session.GeneratedWorld,
            incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var naval = router.NavalTransitions;
        var started = naval.Embark(naval.Deploy(opening), worker.Id, water, 1, 1_400_000UL,
            _ => new FoundationClosedNodeWorkEvidence(1_000_000U, 1_000_000UL, 1_000_000UL));
        var scheduler = new FoundationPersistentWorkerTurnScheduler(router, enableNavalContinuation: true);
        var legacy = new FoundationPersistentWorkerTurnScheduler(router);
        var hasher = new CanonicalWorldStateHasher();
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        Assert.Throws<InvalidOperationException>(() => legacy.Record(started, context));
        var afloat = naval.Advance(naval.Advance(started, worker.Id, 1_000_000UL).State, worker.Id, 400_000UL).State;
        var order = Assert.Single(afloat.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        var waiting = afloat.RemoveWorkerOrder(worker.Owner, worker.Id, order.Id);
        var (waited, waitEvent) = scheduler.Record(waiting, new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL)));
        Assert.Empty(waited.EdgeTravels.Pending);
        Assert.Single(waited.EdgeTravels.NavalVoyages!);
        Assert.All(waitEvent.Slices.Where(s => s.Worker == worker.Id), s =>
            Assert.Equal(PhysicalWorkerActivity.Traveling, s.Activity));
        Assert.Empty(waitEvent.NavalLandings);
        var state = started;
        for (ulong turn = 1; turn <= 3; turn++)
        {
            context = new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL));
            var (next, ev) = scheduler.Record(state, context);
            Assert.Equal(hasher.Compute(next), hasher.Compute(scheduler.Execute(state, ev, context)));
            Assert.All(ev.Slices.GroupBy(s => s.Worker), g => Assert.Equal(1_000_000L, g.Sum(s => (long)s.DurationMicro)));
            var slices = ev.Slices.Where(s => s.Worker == worker.Id).ToArray();
            if (turn < 3)
            {
                Assert.All(slices, s => Assert.Equal(PhysicalWorkerActivity.Traveling, s.Activity));
                Assert.Empty(ev.NavalLandings);
                Assert.Single(next.EdgeTravels.NavalVoyages!);
                Assert.Equal(turn == 1 ? 400_000UL : 800_000UL,
                    Assert.Single(next.EdgeTravels.Pending).RemainingMicro);
            }
            else
            {
                var landing = Assert.Single(ev.NavalLandings);
                Assert.Equal(origin, landing.Destination);
                Assert.Equal(800_000U, landing.ArrivalMicro);
                Assert.Equal(800_000U, slices[0].DurationMicro);
                Assert.Equal(PhysicalWorkerActivity.Traveling, slices[0].Activity);
                Assert.Equal(200_000U, slices[1].DurationMicro);
                Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
                Assert.Equal(PhysicalWorkerOccupation.Mining, slices[1].Occupation);
                Assert.Empty(next.EdgeTravels.NavalVoyages!);
                Assert.Empty(next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
                Assert.Throws<InvalidOperationException>(() => scheduler.Execute(state,
                    ev with { NavalLandings = Array.Empty<FoundationNavalLanding>() }, context));
            }
            state = next;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R234D1NavalVesselPersistsAcrossEdgesAndDisappearsOnLanding(bool priced)
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var worker = bound.Workers.Workers.First();
        var coast = incidence.FineTopology.Edges.First(e =>
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Port &&
            e.IncidentCellIds.Any(n => fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)n.Value - 1)));
        var landId = coast.IncidentCellIds.Single(n =>
            fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land);
        var origin = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, landId);
        var water = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            coast.IncidentCellIds.Single(n => n != landId));
        var region = incidence.TileIncidences[(int)landId.Value - 1].IncidentCoarseCellIds[0];
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.WithWorkers(
            new PhysicalWorkerRuntimeState(bound.Workers.Workers.Select(w => w.Id == worker.Id ?
                w.WithTacticalLocationAndActivity(origin, region, PhysicalWorkerActivity.Other) : w),
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true)));
        var hasher = new CanonicalWorldStateHasher();
        var legacyHash = hasher.Compute(opening);
        var naval = new FoundationNavalVoyageTransitions(demo.Session.GeneratedWorld, incidence, fine, signature);
        const ulong cargo = 2_000_000UL; // explicit QA equivalence, not balance
        Assert.Throws<InvalidOperationException>(() => naval.Deploy(opening, 0UL));
        var deployed = naval.Deploy(opening, priced ? cargo : null);
        Assert.Equal(priced ? 29U : 27U, hasher.Compute(deployed).FormatVersion);
        Assert.NotEqual(legacyHash, hasher.Compute(deployed));
        Assert.Throws<InvalidOperationException>(() => naval.Embark(deployed, worker.Id, water, 1, 1_400_000UL, _ => null));
        FoundationClosedNodeWorkEvidence Evidence(IReadOnlySet<CivilizationId> _) =>
            new(1_000_000U, 250_000UL, 1_000_000UL);
        var started = naval.Embark(deployed, worker.Id, water, 1, 1_400_000UL, Evidence);
        var voyage = Assert.Single(started.EdgeTravels.NavalVoyages!);
        Assert.Equal(750_000UL, voyage.PaidNumerator);
        if (priced)
        {
            var port = opening.Transport.Edges.Single(e => e.Edge == voyage.EmbarkationEdge);
            var rate = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(port);
            Assert.Equal(rate, voyage.EmbarkationFreightMicroPerWholeCargo);
            var other = opening.Civilizations.Civilizations.Single(c => c != worker.Owner);
            var funding = opening.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                opening.Civilizations.Civilizations.Select(c => (c, 100_000_000UL))));
            var invested = funding.RecordMonetaryInfrastructureInvestment(other, port.Edge,
                port.NaturalConstructionCostMoneyMicro!.Value);
            var improved = invested.Transport.Edges.Single(e => e.Edge == port.Edge);
            var improvedRate = FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(improved);
            Assert.True(improvedRate < rate);
            var foreignTrip = naval.Embark(naval.Deploy(invested, cargo), worker.Id, water, 1, 1_400_000UL,
                _ => throw new InvalidOperationException("Activated port must bypass work."));
            var foreignVoyage = Assert.Single(foreignTrip.EdgeTravels.NavalVoyages!);
            Assert.Equal(improvedRate, foreignVoyage.EmbarkationFreightMicroPerWholeCargo);
            Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(improvedRate) * 2,
                FoundationNavalFareQuote.CalculateScaled(foreignVoyage, cargo));
            var expected = GlobalArenaMoneyScale.FromMoneyMicro(rate) * 3 / 2;
            Assert.Equal(expected, FoundationNavalFareQuote.CalculateScaled(voyage, cargo));
            Assert.Equal(BigInteger.Zero, FoundationNavalFareQuote.CalculateScaled(voyage with { PaidNumerator = 0UL }, cargo));
            Assert.Equal(GlobalArenaMoneyScale.FromMoneyMicro(rate) * 2,
                FoundationNavalFareQuote.CalculateScaled(voyage with { PaidNumerator = voyage.PaidDenominator }, cargo));
            var altered = started.WithPhysicalEdgeTravel(started.Workers,
                started.EdgeTravels.ReplaceNavalVoyage(worker.Id, voyage with { EmbarkationFreightMicroPerWholeCargo = checked(rate + 1UL) }));
            Assert.NotEqual(hasher.Compute(started), hasher.Compute(altered));
            Assert.NotEqual(hasher.Compute(deployed), hasher.Compute(naval.Deploy(opening, cargo + 1UL)));
            Assert.Throws<InvalidOperationException>(() => started.EdgeTravels.ReplaceNavalVoyage(worker.Id,
                voyage with { EmbarkationFreightMicroPerWholeCargo = null }));
            Assert.Equal(BigInteger.Zero, FoundationNavalFareQuote.CalculateScaled(voyage with
                { PaidNumerator = 0UL, EmbarkationFreightMicroPerWholeCargo = ulong.MaxValue }, ulong.MaxValue));
            var large = voyage with { PaidNumerator = 1UL, PaidDenominator = 3UL, EmbarkationFreightMicroPerWholeCargo = ulong.MaxValue };
            Assert.Equal((BigInteger)ulong.MaxValue * ulong.MaxValue * GlobalArenaMoneyScale.ExactScaledPerMoneyMicro / 3_000_000UL,
                FoundationNavalFareQuote.CalculateScaled(large, ulong.MaxValue));
        }
        else
        {
            Assert.Null(voyage.EmbarkationFreightMicroPerWholeCargo);
            Assert.Throws<InvalidOperationException>(() => FoundationNavalFareQuote.CalculateScaled(voyage, cargo));
        }
        Assert.Equal(1_400_000UL, voyage.CrossingTimeMicro);
        Assert.Equal(hasher.Compute(started), hasher.Compute(naval.Embark(deployed, worker.Id, water, 1, 1_400_000UL, Evidence)));
        Assert.Throws<InvalidOperationException>(() => started.EdgeTravels.ReplaceNavalVoyage(worker.Id,
            voyage with { PaidDenominator = 0UL }));
        var changed = started.WithPhysicalEdgeTravel(started.Workers,
            started.EdgeTravels.ReplaceNavalVoyage(worker.Id, voyage with { PaidNumerator = 0UL }));
        Assert.NotEqual(hasher.Compute(started), hasher.Compute(changed));
        var first = naval.Advance(started, worker.Id, 1_000_000UL);
        Assert.Equal(400_000UL, Assert.Single(first.State.EdgeTravels.Pending).RemainingMicro);
        Assert.Equal(0UL, first.ResidualMicro);
        var sea = naval.Advance(first.State, worker.Id, 1_000_000UL);
        Assert.Equal(600_000UL, sea.ResidualMicro);
        Assert.Null(sea.Landed);
        Assert.Empty(sea.State.EdgeTravels.Pending);
        Assert.Equal(voyage, Assert.Single(sea.State.EdgeTravels.NavalVoyages!));
        Assert.Equal(water, sea.State.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
        var returnCrossing = naval.StartNext(sea.State, worker.Id, origin);
        Assert.Equal(voyage.CrossingTimeMicro, Assert.Single(returnCrossing.EdgeTravels.Pending).OriginalDurationMicro);
        var returning = naval.Advance(returnCrossing, worker.Id, 600_000UL);
        var landed = naval.Advance(returning.State, worker.Id, 1_000_000UL);
        Assert.Equal(200_000UL, landed.ResidualMicro);
        Assert.Equal(voyage, landed.Landed);
        Assert.Empty(landed.State.EdgeTravels.NavalVoyages!);
        Assert.Empty(landed.State.EdgeTravels.Pending);
        Assert.Equal(origin, landed.State.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
        Assert.Equal(priced ? 29U : 27U, hasher.Compute(landed.State).FormatVersion);
        if (priced)
            Assert.Equal(FoundationNavalFareQuote.CalculateScaled(voyage, cargo),
                FoundationNavalFareQuote.CalculateScaled(landed.Landed!, landed.State.EdgeTravels.NavalCargoMicroPerWorker!.Value));
        Assert.Equal(legacyHash, hasher.Compute(opening));
    }

    [Fact]
    public void R234CBoardingUsesCoastalTitleBeforeCalendarAndExactWorkShare()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var own = worker.Owner;
        var foreign = world.Civilizations.Civilizations.Single(c => c != own);
        var coast = incidence.FineTopology.Edges.First(e =>
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Port);
        var landId = coast.IncidentCellIds.Single(n =>
            fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land);
        var origin = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, landId);
        var water = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            coast.IncidentCellIds.Single(n => n != landId));
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, coast.Id);
        world = world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 5_000_000UL))));
        FoundationNavalBoardingDecision Read(WorldState w,
            Func<IReadOnlySet<CivilizationId>, FoundationClosedNodeWorkEvidence?> evidence) =>
            FoundationNavalBoardingPolicy.Evaluate(w, incidence, fine, own, origin, water, evidence);
        Assert.Equal(FoundationNavalBoardingStatus.NoPriorCalendar, Read(world, _ => null).Status);
        Assert.Equal(FoundationNavalBoardingStatus.InsufficientCoverage,
            Read(world, _ => new(999_999U, 1_000_000UL, 1_000_000UL)).Status);
        var shared = Read(world, eligible =>
        {
            Assert.Contains(foreign, eligible);
            return new(1_000_000U, 1_000_000UL, 100_000_000UL);
        });
        Assert.True(shared.IsAllowed);
        Assert.Equal(99_000_000UL, shared.PaidNumerator);
        Assert.Equal(100_000_000UL, shared.PaidDenominator);
        Assert.Equal(0UL, Read(world, _ => new(1_000_000U, 1_000_000UL, 1_000_000UL)).PaidNumerator);
        var enemy = world.WithDiplomacy(world.Diplomacy.WithRelation(own, foreign, BaselineDiplomacyRelationKind.Enemy));
        Assert.Equal(FoundationNavalBoardingStatus.InsufficientCoverage, Read(enemy, eligible =>
        {
            Assert.DoesNotContain(foreign, eligible);
            return new(0U, 0UL, 0UL);
        }).Status);
        var funded = world.RecordMonetaryInfrastructureInvestment(own, edge, 1UL);
        FoundationClosedNodeWorkEvidence? NeverRead(IReadOnlySet<CivilizationId> _) =>
            throw new InvalidOperationException("Infrastructure must bypass calendar.");
        Assert.Equal(new FoundationNavalBoardingDecision(FoundationNavalBoardingStatus.Allowed),
            Read(funded, NeverRead));
        var otherTitle = world.RecordMonetaryInfrastructureInvestment(foreign, edge, 1UL);
        var fullCharge = new FoundationNavalBoardingDecision(FoundationNavalBoardingStatus.Allowed, 1, 1);
        Assert.Equal(fullCharge, Read(otherTitle, NeverRead));
        Assert.Equal(fullCharge, Read(otherTitle.WithDiplomacy(otherTitle.Diplomacy.WithRelation(
            own, foreign, BaselineDiplomacyRelationKind.Ally)), NeverRead));
        Assert.Equal(FoundationNavalBoardingStatus.HostileInfrastructure,
            Read(otherTitle.WithDiplomacy(enemy.Diplomacy), NeverRead).Status);
        Assert.Equal(FoundationNavalBoardingStatus.InvalidCoastalCrossing,
            FoundationNavalBoardingPolicy.Evaluate(world, incidence, fine, own, water, origin, NeverRead).Status);
    }

    [Fact]
    public void R234CActualHostNaturalBoardingRequiresPreviousCommittedWork()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var owner = worker.Owner;
        var foreign = world.Civilizations.Civilizations.Single(c => c != owner);
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.DeployInitialTreasury(
            CivilizationTreasuryRuntimeState.CreateOpening(bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        var crossing = incidence.FineTopology.Edges.First(e =>
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Port &&
            e.IncidentCellIds.Any(n => fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)n.Value - 1)));
        var landId = crossing.IncidentCellIds.Single(n =>
            fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land);
        var origin = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, landId);
        var water = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            crossing.IncidentCellIds.Single(n => n != landId));
        var otherLand = incidence.FineTopology.Cells.First(c => c.Id != landId &&
            fine.SurfaceAtIndex((int)c.Id.Value - 1) == FoundationFineSurfaceKind.Land &&
            fine.CanTraverseOnFoot((int)c.Id.Value - 1)).Id;
        var elsewhere = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, otherLand);
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
        {
            var node = w.Owner == owner ? origin : elsewhere;
            var coarse = incidence.TileIncidences[(int)node.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
            return w.WithTacticalLocationAndActivity(node, coarse, PhysicalWorkerActivity.Other);
        }), tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        demo.Session.AcceptSnapshot(opening);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>().ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var settlement = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r234c", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64, useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true);
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
            opening.Civilizations.Civilizations, new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
            enableMonetaryInfrastructure: true, settlementPolicy: settlement, fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
            enableExperimentalLocalEconomy: true, enableDefaultAutomaticBase: true, enableExclusiveNodeLabor: true);
        Assert.Equal(FoundationNavalBoardingStatus.NoPriorCalendar, host.ReadNavalBoarding(owner, origin, water).Status);
        foreach (var w in opening.Workers.Workers)
            host.QueueHumanBaseActivity(w.Owner, w.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        Assert.Equal(FoundationNavalBoardingStatus.NoPriorCalendar, host.ReadNavalBoarding(owner, origin, water).Status);
        host.SetHumanReady(owner);
        Assert.Null(host.TryCloseTurn());
        host.SetHumanReady(foreign);
        var close = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(close.ReplayMatched);
        var ownDecision = host.ReadNavalBoarding(owner, origin, water);
        Assert.True(ownDecision.IsAllowed);
        Assert.Equal(0UL, ownDecision.PaidNumerator);
        var foreignDecision = host.ReadNavalBoarding(foreign, origin, water);
        Assert.True(foreignDecision.IsAllowed);
        Assert.Equal(foreignDecision.PaidDenominator, foreignDecision.PaidNumerator);
        Assert.Equal(close.Hash, host.StateHash);
    }

    [Fact]
    public void R234BWorkCoverageAndWorkShareAreIndependent()
    {
        var (world, worker, destination, _, _, _) = Fixture();
        var own = worker.Owner;
        var foreign = world.Workers.Workers.First(w => w.Owner != own);
        var node = worker.TacticalTileId!.Value;
        var owners = world.Workers.Workers.ToDictionary(w => w.Id, w => w.Owner);
        var eligible = new HashSet<CivilizationId> { own, foreign.Owner };
        FoundationPersistentWorkerSlice Work(PhysicalWorkerId id, uint start, uint duration) =>
            new(id, node, start, duration, PhysicalWorkerActivity.Producing,
                PhysicalWorkerOccupation.Mining, null);
        var both = new[] { Work(worker.Id, 0, 1_000_000), Work(foreign.Id, 0, 1_000_000) };
        var equal = FoundationClosedNodeWorkProjector.Read(both, owners, node, own, eligible);
        Assert.True(equal.HasFullCoverage);
        Assert.Equal(1_000_000UL, equal.OwnWorkMicro);
        Assert.Equal(2_000_000UL, equal.EligibleWorkMicro);
        Assert.Equal(1_000_000UL, equal.ForeignWorkMicro);
        var sequential = FoundationClosedNodeWorkProjector.Read(
            new[] { Work(worker.Id, 0, 400_000), Work(foreign.Id, 400_000, 600_000) },
            owners, node, own, eligible);
        Assert.True(sequential.HasFullCoverage);
        Assert.Equal(400_000UL, sequential.OwnWorkMicro);
        Assert.Equal(1_000_000UL, sequential.EligibleWorkMicro);
        var overlap = FoundationClosedNodeWorkProjector.Read(
            new[] { Work(worker.Id, 0, 600_000), Work(foreign.Id, 0, 600_000) },
            owners, node, own, eligible);
        Assert.False(overlap.HasFullCoverage);
        Assert.Equal(600_000U, overlap.CoverageMicro);
        Assert.Equal(1_200_000UL, overlap.EligibleWorkMicro);
        var excluded = FoundationClosedNodeWorkProjector.Read(
            new[] { Work(worker.Id, 0, 400_000), Work(foreign.Id, 400_000, 600_000) },
            owners, node, own, new HashSet<CivilizationId> { own });
        Assert.Equal(400_000U, excluded.CoverageMicro);
        Assert.Equal(400_000UL, excluded.EligibleWorkMicro);
        var nonWork = FoundationClosedNodeWorkProjector.Read(new[]
        {
            Work(worker.Id, 0, 1_000_000) with { Activity = PhysicalWorkerActivity.Traveling },
            Work(foreign.Id, 0, 1_000_000) with { Activity = PhysicalWorkerActivity.Military },
            Work(worker.Id, 0, 1_000_000) with { Activity = PhysicalWorkerActivity.Other, Occupation = PhysicalWorkerOccupation.Unassigned },
            Work(worker.Id, 0, 1_000_000) with { Node = destination }
        }, owners, node, own, eligible);
        Assert.Equal(0U, nonWork.CoverageMicro);
        Assert.Equal(0UL, nonWork.EligibleWorkMicro);
        var constructionAndOperation = FoundationClosedNodeWorkProjector.Read(new[]
        {
            Work(worker.Id, 0, 500_000) with { Activity = PhysicalWorkerActivity.Other, Occupation = PhysicalWorkerOccupation.Unassigned, ProjectId = 1UL },
            Work(foreign.Id, 500_000, 500_000) with { Activity = PhysicalWorkerActivity.Other, Occupation = PhysicalWorkerOccupation.PortOperations }
        }, owners, node, own, eligible);
        Assert.True(constructionAndOperation.HasFullCoverage);
        Assert.Equal(500_000UL, constructionAndOperation.OwnWorkMicro);
    }

    [Fact]
    public void R233ECoordinatorAlternatesAndFallsBackWithoutMixedPlans()
    {
        var (world, worker, destination, _, incidence, _) = Fixture();
        var source = worker.TacticalTileId!.Value;
        var workers = world.Workers.Workers.Where(w => w.Owner == worker.Owner).OrderBy(w => w.Id.Value)
            .Select(w => new FoundationPersistentAiWorker(w.Id, source, PhysicalWorkerActivity.Producing,
                false, FoundationWorkerOrderQueue.Empty(w.Id, w.Owner))).ToArray();
        var id = incidence.FineTopology.Edges.First(e => e.IncidentCellIds.Contains(source.FineStrategicCellId)).Id;
        var investment = new FoundationAiInvestmentOpportunity(new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, id),
            source, 1_000_000UL, 100_000UL, 100_000UL, 80_000UL);
        var view = new FoundationPersistentAiView(worker.Owner, new TurnNumber(1UL), workers)
        {
            FundingAvailable = true, AutomaticBaseAvailable = true, DefaultAutomaticBase = true,
            OwnCashScaled = GlobalArenaMoneyScale.FromMoneyMicro(1_000_000UL),
            InvestmentOpportunities = new[] { investment },
            AdjacentOpportunities = new[] { new FoundationAiAdjacentOpportunity(source, destination, 100, 100) }
        };
        var coordinator = new FoundationPersistentAutonomousEconomicAiPolicy();
        var odd = coordinator.PlanTurn(view);
        Assert.Equal(FoundationWorkerOrderKind.Move, Assert.Single(odd.Edits).OrderKind);
        Assert.Null(odd.Funding);
        var evenView = view with { Turn = new TurnNumber(2UL) };
        var even = coordinator.PlanTurn(evenView);
        Assert.Empty(even.Edits);
        Assert.Equal(investment.Edge, Assert.IsType<FoundationAiFundingIntent>(even.Funding).Edge);
        var noExpansion = coordinator.PlanTurn(view with { AdjacentOpportunities = Array.Empty<FoundationAiAdjacentOpportunity>() });
        Assert.Empty(noExpansion.Edits);
        Assert.Equal(investment.Edge, Assert.IsType<FoundationAiFundingIntent>(noExpansion.Funding).Edge);
        var noFunding = coordinator.PlanTurn(evenView with { FundingAvailable = false });
        Assert.Equal(Assert.Single(odd.Edits), Assert.Single(noFunding.Edits));
        Assert.Null(noFunding.Funding);
        var tooFew = coordinator.PlanTurn(evenView with { Workers = workers.Take(2).ToArray() });
        Assert.Empty(tooFew.Edits);
        Assert.Null(tooFew.Funding);
        var localOnly = coordinator.PlanTurn(view with { DefaultAutomaticBase = false });
        Assert.Equal(3, localOnly.Edits.Count);
        Assert.All(localOnly.Edits, e => Assert.Equal(FoundationPersistentOrderEditKind.SetBaseActivity, e.Kind));
        Assert.Null(localOnly.Funding);
    }

    [Fact]
    public void R235GProductivePolicyGuardsCashWorkAndCoordinatorExclusivity()
    {
        var (world, worker, destination, _, incidence, _) = Fixture();
        var source = worker.TacticalTileId!.Value;
        var workers = world.Workers.Workers.Where(w => w.Owner == worker.Owner).OrderBy(w => w.Id.Value)
            .Select(w => new FoundationPersistentAiWorker(w.Id, source, PhysicalWorkerActivity.Producing,
                false, FoundationWorkerOrderQueue.Empty(w.Id, w.Owner))).ToArray();
        var quote = new FoundationAiProductiveOpportunity(new FoundationProductiveTarget(source,
            FoundationProductiveActivity.Mining), 100_000UL, 100_000UL, 400_000_000_000L, 600_000_000_000L);
        var view = new FoundationPersistentAiView(worker.Owner, new TurnNumber(2UL), workers)
        {
            FundingAvailable = true, AutomaticBaseAvailable = true, DefaultAutomaticBase = true,
            OwnCashScaled = GlobalArenaMoneyScale.FromMoneyMicro(1_000_000UL),
            ProductiveOpportunities = new[] { quote },
            AdjacentOpportunities = new[] { new FoundationAiAdjacentOpportunity(source, destination, 100, 100) }
        };
        var policy = new FoundationPersistentProductiveInvestmentAiPolicy();
        var selected = Assert.IsType<FoundationAiProductiveFundingIntent>(policy.PlanProductiveFunding(view));
        Assert.Equal(quote.Target, selected.Target);
        Assert.Equal(workers[^1].Id, Assert.Single(selected.Assignments).Worker);
        Assert.Null(policy.PlanProductiveFunding(view with { OwnCashScaled = GlobalArenaMoneyScale.FromMoneyMicro(999_999UL) }));
        Assert.Null(policy.PlanProductiveFunding(view with { Workers = workers.Take(2).ToArray() }));
        Assert.Null(policy.PlanProductiveFunding(view with { ProductiveOpportunities = new[] { quote with { RequiredWorkMicro = 9_000_000UL } } }));
        Assert.Null(policy.PlanProductiveFunding(view with { ProductiveOpportunities = new[] { quote with { IncrementalRevenueNumerator = BigInteger.Zero } } }));
        var coordinator = new FoundationPersistentAutonomousEconomicAiPolicy();
        var even = coordinator.PlanStrategicTurn(view);
        Assert.Empty(even.Edits);
        Assert.Null(even.Funding);
        Assert.Equal(quote.Target, Assert.IsType<FoundationAiProductiveFundingIntent>(even.Productive).Target);
        var odd = coordinator.PlanStrategicTurn(view with { Turn = new TurnNumber(1UL) });
        Assert.Single(odd.Edits);
        Assert.Null(odd.Productive);
        Assert.Null(odd.Funding);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R235GActualProductiveAiUsesKnownOwnNodeEvidenceAndCanonicalFunding(bool known)
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var owner = worker.Owner;
        var human = world.Civilizations.Civilizations.Single(c => c != owner);
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.DeployInitialTreasury(
            CivilizationTreasuryRuntimeState.CreateOpening(bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders().DeployInitialProductiveInfrastructure(Enum.GetValues<FoundationProductiveActivity>()
                .Select(a => new FoundationProductiveReference(a, 100_000UL)));
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        var crossing = incidence.FineTopology.Edges.First(e =>
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground &&
            e.IncidentCellIds.All(n => fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)n.Value - 1)));
        var source = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, crossing.IncidentCellIds[0]);
        var target = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, crossing.IncidentCellIds[1]);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var census = opening.Workers.Workers.Select(w =>
        {
            var node = w.Owner == owner ? source : target;
            var coarse = incidence.TileIncidences[(int)node.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
            return w.WithTacticalLocationAndActivity(node, coarse, PhysicalWorkerActivity.Other);
        });
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(census,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        demo.Session.AcceptSnapshot(opening);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>().ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var settlement = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r233d2", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64, useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true);
        var scopes = new Dictionary<CivilizationId, FoundationCivilizationKnowledgeScope>
        {
            [owner] = new(known ? new[] { source } : new[] { target },
                Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>()),
            [human] = new(new[] { source, target }, new[] { (source, target) })
        };
        var policy = new FoundationPersistentProductiveInvestmentAiPolicy();
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence, new[] { human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy> { [owner] = policy },
            enableMonetaryInfrastructure: true, settlementPolicy: settlement, fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
            enableExperimentalLocalEconomy: true, enableDefaultAutomaticBase: true, knowledgeScopes: scopes);
        foreach (var w in opening.Workers.Workers.Where(w => w.Owner == human))
            host.QueueHumanBaseActivity(human, w.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        var hash = host.StateHash;
        host.PrepareAiCivilizations();
        Assert.Equal(hash, host.StateHash);
        host.SetHumanReady(human);
        var first = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.Empty(first.ProductiveFunding);
        Assert.True(first.ReplayMatched);
        hash = host.StateHash;
        host.PrepareAiCivilizations();
        Assert.Equal(hash, host.StateHash);
        host.SetHumanReady(human);
        var second = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(second.ReplayMatched);
        if (!known)
        {
            Assert.Empty(second.ProductiveFunding);
            Assert.Empty(host.Snapshot.ProductiveInfrastructure.Projects);
            return;
        }
        var funding = Assert.Single(second.ProductiveFunding);
        Assert.Equal(owner, funding.Investor);
        Assert.Equal(source, funding.Target.Node);
        Assert.Equal(100_000UL, funding.MoneyMicro);
        Assert.Equal(100_000UL, funding.RequiredWorkMicro);
        Assert.Single(funding.Assignments);
        var project = Assert.Single(host.Snapshot.ProductiveInfrastructure.Projects);
        Assert.Equal(funding.ProjectId, project.Id);
        Assert.True(project.IsActivated);
        Assert.Equal(funding.MoneyMicro, host.Snapshot.ProductiveInfrastructure.Read(funding.Target).ActivatedCapitalMoneyMicro);
    }

    private sealed class R233D2EvidenceProbe : FoundationPersistentEconomicAiPolicy
    {
        internal override bool RequiresInvestmentCandidates => true;
        public FoundationPersistentAiView? View { get; private set; }
        public override IReadOnlyList<FoundationPersistentOrderEdit> PlanPersistent(FoundationPersistentAiView view)
        {
            View = view;
            return Array.Empty<FoundationPersistentOrderEdit>();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R233D2ActualParticipantTradeProjectsOnlyKnownIncidentInvestment(bool known)
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var owner = worker.Owner;
        var human = world.Civilizations.Civilizations.Single(c => c != owner);
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.DeployInitialTreasury(
            CivilizationTreasuryRuntimeState.CreateOpening(bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        var crossing = incidence.FineTopology.Edges.First(e =>
            fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground &&
            e.IncidentCellIds.All(n => fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)n.Value - 1)));
        var source = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, crossing.IncidentCellIds[0]);
        var target = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, crossing.IncidentCellIds[1]);
        var edge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, crossing.Id);
        var census = opening.Workers.Workers.Select(w =>
        {
            var node = w.Owner == owner ? source : target;
            var coarse = incidence.TileIncidences[(int)node.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
            return w.WithTacticalLocationAndActivity(node, coarse, PhysicalWorkerActivity.Other);
        });
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(census,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        demo.Session.AcceptSnapshot(opening);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>().ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var settlement = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r233d2", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64, useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true);
        var scopes = new Dictionary<CivilizationId, FoundationCivilizationKnowledgeScope>
        {
            [owner] = new(new[] { source, target }, known ? new[] { (source, target) } :
                Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>()),
            [human] = new(new[] { source, target }, new[] { (source, target) })
        };
        var probe = new R233D2EvidenceProbe();
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence, new[] { human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy> { [owner] = probe },
            enableMonetaryInfrastructure: true, settlementPolicy: settlement, fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
            enableExperimentalLocalEconomy: true, enableDefaultAutomaticBase: true, knowledgeScopes: scopes);
        foreach (var w in opening.Workers.Workers.Where(w => w.Owner == human))
            host.QueueHumanBaseActivity(human, w.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        var eligibleWorkOwners = new HashSet<CivilizationId> { owner, human };
        Assert.Null(host.ReadLastClosedNodeWork(target, human, eligibleWorkOwners));
        host.PrepareAiCivilizations();
        Assert.Empty(probe.View!.InvestmentOpportunities);
        host.SetHumanReady(human);
        var closed = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(closed.ReplayMatched);
        var work = Assert.IsType<FoundationClosedNodeWorkEvidence>(
            host.ReadLastClosedNodeWork(target, human, eligibleWorkOwners));
        Assert.True(work.HasFullCoverage);
        Assert.Equal(3_000_000UL, work.OwnWorkMicro);
        Assert.Equal(work.OwnWorkMicro, work.EligibleWorkMicro);
        Assert.Equal(closed.Hash, host.StateHash);
        var load = Assert.Single(closed.TemporalEconomicSettlement!.AuditedParticipantEdges![owner], e => e.Edge == edge);
        Assert.True(load.TotalCargoMicro > 0);
        var hash = host.StateHash;
        host.PrepareAiCivilizations();
        Assert.Equal(hash, host.StateHash);
        if (!known) Assert.Empty(probe.View!.InvestmentOpportunities);
        else
        {
            var candidate = Assert.Single(probe.View!.InvestmentOpportunities);
            Assert.Equal(edge, candidate.Edge);
            Assert.Equal(source, candidate.Source);
            Assert.Equal(load.TotalCargoMicro, candidate.CargoMicro);
            var actual = host.Snapshot.Transport.Edges.Single(e => e.Edge == edge);
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(actual), candidate.UnitCostBeforeMicro);
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.ProjectUnitFreightCost(actual, candidate.MoneyMicro), candidate.UnitCostAfterMicro);
            Assert.True(GlobalArenaMoneyScale.FromMoneyMicro(candidate.MoneyMicro) * 10 <= probe.View.OwnCashScaled);
            Assert.True(candidate.SavingNumerator > 0);
        }
        host.SetHumanReady(human);
        var next = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(next.ReplayMatched);
        Assert.Empty(next.PersistentFunding);
    }

    [Fact]
    public void R233BLocalProductionAiDelegatesOnlyUncommandedWorkers()
    {
        var (world, worker, _, _, _, _) = Fixture();
        var queue = FoundationWorkerOrderQueue.Empty(worker.Id, worker.Owner);
        var row = new FoundationPersistentAiWorker(worker.Id, worker.TacticalTileId!.Value,
            worker.Activity, false, queue);
        var view = new FoundationPersistentAiView(worker.Owner, new TurnNumber(1UL), new[] { row });
        var policy = new FoundationPersistentLocalProductionAiPolicy();
        Assert.Empty(policy.PlanPersistent(view));
        var enabled = view with { AutomaticBaseAvailable = true };
        var decision = Assert.Single(policy.PlanPersistent(enabled));
        Assert.Equal(worker.Owner, decision.Issuer);
        Assert.Equal(worker.Id, decision.Worker);
        Assert.Equal(FoundationPersistentOrderEditKind.SetBaseActivity, decision.Kind);
        Assert.Equal(FoundationWorkerBaseMode.Automatic, decision.BaseActivity!.Mode);
        Assert.Empty(policy.PlanPersistent(enabled with { DefaultAutomaticBase = true }));
        Assert.Empty(policy.PlanPersistent(enabled with { Workers = new[] { row with { Crossing = true } } }));
        var manual = queue.SetBaseActivity(worker.Owner,
            new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        Assert.Empty(policy.PlanPersistent(enabled with { Workers = new[] { row with { Queue = manual } } }));
        var busy = queue.Insert(worker.Owner, 0, FoundationWorkerOrderKind.Occupation,
            occupation: PhysicalWorkerOccupation.Agriculture);
        Assert.Empty(policy.PlanPersistent(enabled with { Workers = new[] { row with { Queue = busy } } }));
        var automatic = queue.SetBaseActivity(worker.Owner,
            new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic));
        Assert.Empty(policy.PlanPersistent(enabled with { Workers = new[] { row with { Queue = automatic } } }));
    }

    [Fact]
    public void R233CExpansionPolicyPreservesAnchorsAndReservations()
    {
        var (world, worker, destination, _, _, _) = Fixture();
        var own = world.Workers.Workers.Where(w => w.Owner == worker.Owner).OrderBy(w => w.Id.Value)
            .Select(w => new FoundationPersistentAiWorker(w.Id, worker.TacticalTileId!.Value,
                PhysicalWorkerActivity.Producing, false, FoundationWorkerOrderQueue.Empty(w.Id, w.Owner))).ToArray();
        Assert.Equal(3, own.Length);
        var edge = new FoundationAiAdjacentOpportunity(own[0].Node, destination, 100, 100);
        var view = new FoundationPersistentAiView(worker.Owner, new TurnNumber(1UL), own)
        {
            DefaultAutomaticBase = true, AutomaticBaseAvailable = true,
            AdjacentOpportunities = new[] { edge }
        };
        var ai = new FoundationPersistentExpansionAiPolicy();
        var decision = Assert.Single(ai.PlanPersistent(view));
        Assert.Equal(own[^1].Id, decision.Worker);
        Assert.Equal((PhysicalTacticalTileId?)destination, decision.Destination);
        Assert.Equal(FoundationWorkerOrderKind.Move, decision.OrderKind);
        Assert.Equal(decision, Assert.Single(ai.PlanPersistent(view with { Workers = own.Reverse().ToArray() })));
        Assert.Empty(ai.PlanPersistent(view with { Workers = own.Take(2).ToArray() }));
        // Equal productivity: the same opportunity becomes unattractive
        // as physical travel takes a greater share of the planning horizon.
        Assert.Empty(ai.PlanPersistent(view with { AdjacentOpportunities = new[] { edge with { CrossingTimeMicro = 5_000_000UL } } }));
        Assert.Empty(ai.PlanPersistent(view with { AdjacentOpportunities = new[] { edge with { CrossingTimeMicro = 100_000_000UL } } }));
        Assert.Equal(decision, Assert.Single(ai.PlanPersistent(view with
        {
            AdjacentOpportunities = new[] { edge with { CrossingTimeMicro = 2_000_000UL } }
        })));
        Assert.Empty(ai.PlanPersistent(view with { AdjacentOpportunities = new[] { edge with { CrossingTimeMicro = 0UL } } }));
        Assert.Equal(decision, Assert.Single(ai.PlanPersistent(view with
        {
            AdjacentOpportunities = new[] { edge with { SourcePotential = 1_000_000, DestinationPotential = 1_000_000 } }
        })));
        Assert.Empty(ai.PlanPersistent(view with { AdjacentOpportunities = new[] { edge with { DestinationPotential = 60 } } }));
        Assert.Empty(ai.PlanPersistent(view with { Workers = new[] { own[0], own[1], own[2] with { Crossing = true } } }));
        var reserved = own[2].Queue.Insert(worker.Owner, 0, FoundationWorkerOrderKind.Move, destination: destination);
        Assert.Empty(ai.PlanPersistent(view with { Workers = new[] { own[0], own[1], own[2] with { Queue = reserved } } }));
        Assert.Empty(ai.PlanPersistent(view with { Workers = new[] { own[0], own[1], own[2] with { Node = destination } } }));
    }

    [Fact]
    public void R234GExpansionRejectsUnaffordableOrUnprofitableNavalFare()
    {
        var (world, worker, destination, _, _, _) = Fixture();
        var own = world.Workers.Workers.Where(w => w.Owner == worker.Owner).OrderBy(w => w.Id.Value)
            .Select(w => new FoundationPersistentAiWorker(w.Id, worker.TacticalTileId!.Value,
                PhysicalWorkerActivity.Producing, false, FoundationWorkerOrderQueue.Empty(w.Id, w.Owner))).ToArray();
        var opportunity = new FoundationAiAdjacentOpportunity(own[0].Node, destination,
            1_000_000_000_000L, 1_000_000_000_000L, 2_000_000UL) { IsNaval = true };
        var view = new FoundationPersistentAiView(worker.Owner, new TurnNumber(1UL), own)
        {
            DefaultAutomaticBase = true, AutomaticBaseAvailable = true,
            OwnCashScaled = GlobalArenaMoneyScale.FromMoneyMicro(10_000_000UL),
            AdjacentOpportunities = new[] { opportunity }
        };
        var ai = new FoundationPersistentExpansionAiPolicy();
        Assert.Single(ai.PlanPersistent(view));
        var small = opportunity with { FareScaled = GlobalArenaMoneyScale.FromMoneyMicro(100_000UL) };
        Assert.Single(ai.PlanPersistent(view with { AdjacentOpportunities = new[] { small } }));
        Assert.Empty(ai.PlanPersistent(view with { OwnCashScaled = BigInteger.Zero,
            AdjacentOpportunities = new[] { small } }));
        var large = opportunity with { FareScaled = GlobalArenaMoneyScale.FromMoneyMicro(5_000_000UL) };
        Assert.Empty(ai.PlanPersistent(view with { AdjacentOpportunities = new[] { large } }));
        Assert.Empty(ai.PlanPersistent(view with { AdjacentOpportunities = new[] {
            opportunity with { CrossingTimeMicro = 100_000_000UL } } }));
    }

    [Theory]
    [InlineData(0)] // authorized natural trip after real work
    [InlineData(1)] // endpoints known, links unknown
    [InlineData(2)] // prohibitive travel time
    [InlineData(3)] // prohibitive foreign fare
    public void R234GActualAiHostUsesKnownAffordableNavalRoute(int mode)
    {
        var (world, _, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var worker = bound.Workers.Workers.First();
        var router = new FoundationConstructionApproachRouter(demo.Session.GeneratedWorld,
            incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var naval = router.NavalTransitions;
        var parameters = incidence.Refinement.FineParameters;
        var land = incidence.FineTopology.Cells.Where(c =>
            fine.SurfaceAtIndex((int)c.Id.Value - 1) == FoundationFineSurfaceKind.Land &&
            fine.CanTraverseOnFoot((int)c.Id.Value - 1)).ToArray();
        var candidate = (from a in land
            let origin = new PhysicalTacticalTileId(parameters, a.Id)
            from b in a.AdjacentCellIds
            where fine.CrossingAtIndices((int)a.Id.Value - 1, (int)b.Value - 1) == FoundationFineTransportCrossingKind.Port
            let water = new PhysicalTacticalTileId(parameters, b)
            from c in land
            where c.Id != a.Id
            let target = new PhysicalTacticalTileId(parameters, c.Id)
            where naval.FindNextLandingStep(water, target).HasValue
            select (Origin: origin, Water: water, Target: target)).First();
        var foreignNode = new PhysicalTacticalTileId(parameters, land.First(c => c.Id != candidate.Origin.FineStrategicCellId &&
            c.Id != candidate.Target.FineStrategicCellId).Id);
        var census = bound.Workers.Workers.Select(w =>
        {
            var node = w.Owner == worker.Owner ? candidate.Origin : foreignNode;
            var region = incidence.TileIncidences[(int)node.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
            return w.WithTacticalLocationAndActivity(node, region, PhysicalWorkerActivity.Other).WithUniformCityDemography();
        });
        // Adopt signed geography with the original census reference.
        // Relocate the QA workers only after this one-time boundary.
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))));
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(census,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        if (mode == 3)
        {
            var edge = incidence.FineTopology.Edges.Single(e =>
                e.IncidentCellIds.Contains(candidate.Origin.FineStrategicCellId) &&
                e.IncidentCellIds.Contains(candidate.Water.FineStrategicCellId));
            var foreign = opening.Civilizations.Civilizations.Single(c => c != worker.Owner);
            opening = opening.RecordMonetaryInfrastructureInvestment(foreign,
                new PhysicalTacticalEdgeId(parameters, edge.Id), 1UL);
        }
        opening = opening.WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        opening = naval.Deploy(opening, mode == 3 ? 1_000_000_000_000_000UL : 2_000_000UL);
        var nodes = new List<PhysicalTacticalTileId> { candidate.Origin, candidate.Water };
        var links = new List<(PhysicalTacticalTileId, PhysicalTacticalTileId)> { (candidate.Origin, candidate.Water) };
        var at = candidate.Water;
        while (at != candidate.Target)
        {
            var next = naval.FindNextLandingStep(at, candidate.Target)!.Value;
            Assert.DoesNotContain(next, nodes);
            nodes.Add(next);
            links.Add((at, next));
            at = next;
        }
        var scope = new FoundationCivilizationKnowledgeScope(nodes, mode == 1 ?
            Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>() : links);
        var scopes = opening.Civilizations.Civilizations.ToDictionary(c => c, _ => scope);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>().ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var economy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r234d2c", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64, useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true);
        demo.Session.AcceptSnapshot(opening);
        var human = opening.Civilizations.Civilizations.Single(c => c != worker.Owner);
        ulong naturalTime = mode == 2 ? 100_000_000UL : 2_000_000UL / (ulong)links.Count;
        if (mode == 3)
        {
            var quote = Assert.Single(naval.ReadExpansionQuotes(opening, worker.Id, 1UL, naturalTime,
                scope, FoundationPersistentExpansionAiPolicy.ExpansionHorizonMicro));
            var departed = Assert.IsType<WorldState>(naval.TryDepart(opening, opening, worker.Id,
                candidate.Target, 1UL, naturalTime, scope));
            var voyage = Assert.Single(departed.EdgeTravels.NavalVoyages!);
            Assert.Equal(candidate.Target, quote.Target);
            Assert.Equal(voyage.CrossingTimeMicro * (ulong)links.Count, quote.TimeMicro);
            Assert.Equal(FoundationNavalFareQuote.CalculateScaled(voyage, opening.EdgeTravels.NavalCargoMicroPerWorker!.Value),
                quote.FareScaled);
            Assert.True(quote.FareScaled > opening.Treasury.Get(worker.Owner).CashScaled);
        }
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence, new[] { human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            { [worker.Owner] = new FoundationPersistentAutonomousEconomicAiPolicy() },
            enableMonetaryInfrastructure: true, settlementPolicy: economy, fineGeography: fine,
            fineSignature: signature, physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
            enableExperimentalLocalEconomy: true, enableDefaultAutomaticBase: true,
            knowledgeScopes: scopes, enableExclusiveNodeLabor: true, navalDepartureTimeMicro: naturalTime);
        foreach (var w in opening.Workers.Workers.Where(w => w.Owner == human))
            host.QueueHumanBaseActivity(human, w.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        for (int turn = 1; turn <= 2; turn++)
        {
            var hash = host.StateHash;
            host.PrepareAiCivilizations();
            Assert.Equal(hash, host.StateHash);
            host.SetHumanReady(human);
            var close = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
            Assert.True(close.ReplayMatched);
            Assert.Equal(close.Hash, host.StateHash);
            var moves = close.PersistentEdits.Where(e => e.Edit.Issuer == worker.Owner &&
                e.Edit.OrderKind == FoundationWorkerOrderKind.Move).ToArray();
            if (mode == 0 && turn == 2)
            {
                var move = Assert.Single(moves).Edit;
                Assert.Equal(candidate.Target, move.Destination);
                Assert.Equal(move.Worker, Assert.Single(host.Snapshot.EdgeTravels.NavalVoyages!).Worker);
                Assert.Contains(close.PersistentTimeline!.Slices, s => s.Worker == move.Worker &&
                    s.Activity == PhysicalWorkerActivity.Traveling);
            }
            else Assert.Empty(moves);
        }
        if (mode == 0)
        {
            host.PrepareAiCivilizations();
            host.SetHumanReady(human);
            var close = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
            Assert.True(close.ReplayMatched);
            var landing = Assert.Single(close.PersistentTimeline!.NavalLandings);
            Assert.Equal(candidate.Target, landing.Destination);
            Assert.Equal(0UL, landing.Voyage.PaidNumerator);
            Assert.Equal(close.Hash, host.StateHash);
        }
        else Assert.Empty(host.Snapshot.EdgeTravels.NavalVoyages!);
    }

    [Fact]
    public void R233C1KnownSubgraphRequiresKnownEndpointsAndUndirectedEdges()
    {
        var (_, worker, destination, _, _, _) = Fixture();
        var source = worker.TacticalTileId!.Value;
        Assert.Throws<ArgumentException>(() => new FoundationCivilizationKnowledgeScope(
            new[] { source }, new[] { (source, destination) }));
        var nodes = new List<PhysicalTacticalTileId> { source, destination };
        var links = new List<(PhysicalTacticalTileId, PhysicalTacticalTileId)> { (source, destination) };
        var scope = new FoundationCivilizationKnowledgeScope(nodes, links);
        nodes.Clear();
        links.Clear();
        Assert.True(scope.KnowsNode(source));
        Assert.True(scope.KnowsNode(destination));
        Assert.True(scope.KnowsLink(source, destination));
        Assert.True(scope.KnowsLink(destination, source));
        var other = new FoundationCivilizationKnowledgeScope(new[] { source, destination },
            Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>());
        Assert.False(other.KnowsLink(source, destination));
    }

    [Theory]
    [InlineData(0)] // legacy full-map QA
    [InlineData(1)] // endpoints known to AI, edge known only to human
    [InlineData(2)] // one edge known to AI
    [InlineData(3)] // destination unknown to AI
    [InlineData(4)] // policy attempts to bypass the known-edge boundary
    [InlineData(5)] // autonomous coordinator with one known link
    [InlineData(6)] // autonomous coordinator with no known link
    [InlineData(7)] // exclusive labor, initially mixed node
    [InlineData(8)] // exclusive labor, arrival at foreign working node
    [InlineData(9)] // known ground link but prohibitive natural crossing time
    public void R233CActualAiExpansionCrossesGroundAndResumesProduction(int knowledgeMode)
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
        var bound = demo.Session.Snapshot.PromoteFineFirstBeforeTransport(demo.Session.GeneratedWorld, incidence, fine, signature)
            .DeployInitialFineFirstPhysicalTransport(world.Transport, demo.Session.GeneratedWorld, incidence, fine, signature);
        var opening = FoundationPersistedEdgeTravelTransitions.DeployEmpty(bound.DeployInitialTreasury(
            CivilizationTreasuryRuntimeState.CreateOpening(bound.Civilizations.Civilizations.Select(c => (c, 5_000_000UL)))))
            .WithConstructions(new FoundationConstructionRuntimeState(Array.Empty<FoundationConstructionProject>()))
            .DeployInitialWorkerOrders();
        demo.Session.AcceptFineFirstOpening(opening, incidence, fine, signature);
        var owner = worker.Owner;
        var human = opening.Civilizations.Civilizations.Single(c => c != owner);
        // Select a signed ground site with a ground neighbor; relocate only
        // in this fixture before the host is created, not during gameplay.
        var cell = incidence.FineTopology.Cells.First(c =>
            fine.SurfaceAtIndex((int)c.Id.Value - 1) == FoundationFineSurfaceKind.Land &&
            fine.CanTraverseOnFoot((int)c.Id.Value - 1) &&
            c.AdjacentCellIds.Any(n => fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)n.Value - 1) &&
                fine.CrossingAtIndices((int)c.Id.Value - 1, (int)n.Value - 1) == FoundationFineTransportCrossingKind.Ground));
        var source = new PhysicalTacticalTileId(incidence.Refinement.FineParameters, cell.Id);
        var coarse = incidence.TileIncidences[(int)cell.Id.Value - 1].IncidentCoarseCellIds[0];
        opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
            w.Owner == owner ? w.WithTacticalLocationAndActivity(source, coarse, PhysicalWorkerActivity.Other)
                : w.WithTacticalLocationAndActivity(w.TacticalTileId!.Value, w.CellId, PhysicalWorkerActivity.Other)),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        demo.Session.AcceptSnapshot(opening);
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>().ToDictionary(c => c, c => new CommodityId((uint)c));
        var wood = new CommodityId(100U);
        var stone = new CommodityId(101U);
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL,
            new FoundationBasicResourceProductionPolicy(wood, stone, 6_000_000UL, 5_000_000UL));
        var items = crops.Values.Append(wood).Append(stone).ToArray();
        var demand = new FoundationActivityCommodityDemandPolicy(items,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => items.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var settlement = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("r233c", items.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            opening.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64, useTemporalProgressiveDistribution: true, requireSafeProgressiveRoutes: true);
        var allowedTarget = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
            cell.AdjacentCellIds.First(n =>
                fine.SurfaceAtIndex((int)n.Value - 1) == FoundationFineSurfaceKind.Land &&
                fine.CanTraverseOnFoot((int)n.Value - 1) &&
                fine.CrossingAtIndices((int)cell.Id.Value - 1, (int)n.Value - 1) == FoundationFineTransportCrossingKind.Ground));
        if (knowledgeMode is 7 or 8)
        {
            var foreignNode = knowledgeMode == 7 ? source : allowedTarget;
            var region = incidence.TileIncidences[(int)foreignNode.FineStrategicCellId.Value - 1].IncidentCoarseCellIds[0];
            opening = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
                w.Owner == human ? w.WithTacticalLocationAndActivity(foreignNode, region, PhysicalWorkerActivity.Other) : w),
                tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
            demo.Session.AcceptSnapshot(opening);
        }
        var knownNodes = knowledgeMode == 3 ? new[] { source } : new[] { source, allowedTarget };
        var knownLinks = knowledgeMode is 2 or 5 or 8 or 9 ? new[] { (source, allowedTarget) } :
            Array.Empty<(PhysicalTacticalTileId, PhysicalTacticalTileId)>();
        var scopes = knowledgeMode == 0 ? null :
            new Dictionary<CivilizationId, FoundationCivilizationKnowledgeScope>
            {
                [owner] = new(knownNodes, knownLinks),
                [human] = new(new[] { source, allowedTarget }, new[] { (source, allowedTarget) })
            };
        var probe = new R233AProbePolicy();
        probe.Decide = view => new[] { new FoundationPersistentOrderEdit(owner, view.Workers[0].Id,
            FoundationPersistentOrderEditKind.Insert, Index: 0,
            OrderKind: FoundationWorkerOrderKind.Move, Destination: allowedTarget) };
        IFoundationTacticalAiTurnPolicy ai = knowledgeMode == 4 ? probe :
            knowledgeMode == 7 ? new FoundationTacticalAiHoldPolicy() :
            knowledgeMode is 5 or 6 or 9 ? new FoundationPersistentAutonomousEconomicAiPolicy() : new FoundationPersistentExpansionAiPolicy();
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence, new[] { human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy> { [owner] = ai },
            enableMonetaryInfrastructure: true, settlementPolicy: settlement, fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(knowledgeMode == 9 ? 100_000_000UL : 1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture,
            enableExperimentalLocalEconomy: true, enableDefaultAutomaticBase: true, knowledgeScopes: scopes,
            enableExclusiveNodeLabor: knowledgeMode is 7 or 8);
        if (knowledgeMode is 7 or 8)
            foreach (var w in opening.Workers.Workers.Where(w => w.Owner == human))
                host.QueueHumanBaseActivity(human, w.Id,
                    new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Mining));
        var hash = host.StateHash;
        if (knowledgeMode == 7)
        {
            var owners = opening.Workers.Workers.ToDictionary(w => w.Id, w => w.Owner);
            var expected = opening.Civilizations.Civilizations.OrderBy(c => c.Value).ToArray();
            foreach (var winner in expected)
            {
                host.SetHumanReady(human);
                var close = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
                Assert.True(close.ReplayMatched);
                var work = close.PersistentTimeline!.Slices.Where(s => s.Node == source &&
                    s.Activity == PhysicalWorkerActivity.Producing).ToArray();
                Assert.NotEmpty(work);
                Assert.All(work, s => Assert.Equal(winner, owners[s.Worker]));
                Assert.Equal(close.Hash, host.StateHash);
            }
            return;
        }
        if (knowledgeMode == 4)
        {
            Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
            Assert.Empty(host.ReadyCivilizations);
            Assert.Equal(0, host.PendingOrderCount);
            Assert.Equal(hash, host.StateHash);
            probe.Decide = _ => Array.Empty<FoundationPersistentOrderEdit>();
        }
        host.PrepareAiCivilizations();
        Assert.Equal(hash, host.StateHash);
        if (knowledgeMode is 1 or 3 or 4 or 6 or 9)
        {
            Assert.Equal(0, host.PendingOrderCount);
            Assert.Null(host.TryCloseTurn());
            host.SetHumanReady(human);
            var restricted = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
            Assert.True(restricted.ReplayMatched);
            Assert.Empty(restricted.PersistentEdits);
            Assert.Equal(restricted.Hash, host.StateHash);
            return;
        }
        Assert.Equal(knowledgeMode == 8 ? 4 : 1, host.PendingOrderCount);
        Assert.Null(host.TryCloseTurn());
        host.SetHumanReady(human);
        var first = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(first.ReplayMatched);
        var edit = Assert.Single(first.PersistentEdits, e => e.Edit.Issuer == owner).Edit;
        Assert.Equal(owner, edit.Issuer);
        Assert.Equal(FoundationWorkerOrderKind.Move, edit.OrderKind);
        var target = edit.Destination!.Value;
        if (knowledgeMode is 2 or 5 or 8) Assert.Equal(allowedTarget, target);
        Assert.Contains(target.FineStrategicCellId, cell.AdjacentCellIds);
        Assert.Equal(FoundationFineTransportCrossingKind.Ground,
            fine.CrossingAtIndices((int)cell.Id.Value - 1, (int)target.FineStrategicCellId.Value - 1));
        Assert.Contains(first.PersistentTimeline!.Slices, s => s.Worker == edit.Worker && s.Activity == PhysicalWorkerActivity.Traveling);
        Assert.Equal(2, first.PersistentTimeline.Slices.Where(s => s.Node == source &&
            s.Worker != edit.Worker && opening.Workers.Workers.Any(w => w.Id == s.Worker && w.Owner == owner))
            .Select(s => s.Worker).Distinct().Count());
        host.SetHumanReady(human);
        var second = Assert.IsType<FoundationTacticalCollectiveReceipt>(host.TryCloseTurn());
        Assert.True(second.ReplayMatched);
        Assert.Empty(second.PersistentEdits);
        Assert.Equal(target, host.Snapshot.Workers.Workers.Single(w => w.Id == edit.Worker).TacticalTileId!.Value);
        if (knowledgeMode == 8)
        {
            Assert.DoesNotContain(second.PersistentTimeline!.Slices, s => s.Worker == edit.Worker &&
                s.Node == target && s.Activity == PhysicalWorkerActivity.Producing);
            Assert.Contains(second.PersistentTimeline.Slices, s => s.Worker == edit.Worker &&
                s.Node == target && s.Activity == PhysicalWorkerActivity.Other &&
                s.Occupation == PhysicalWorkerOccupation.Unassigned);
        }
        else
            Assert.Contains(second.PersistentTimeline!.Slices, s => s.Worker == edit.Worker &&
                s.Node == target && s.Activity == PhysicalWorkerActivity.Producing);
        Assert.Equal(second.Hash, host.StateHash);
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
        Assert.IsType<FoundationTemporalSalesSettledEvent>(resolved.Events.Reverse().Skip(1).First());
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
        foreach (var events in new[] { resolved.Events.Take(resolved.Events.Count() - 1).ToArray(),
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
    public void R219AExplicitCropUsesArrivalRemainderAndCanonicalMarketReplay()
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
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.Agriculture, Crop: 4))
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
        Assert.Equal((byte)4, slices[1].Crop);
        Assert.Equal((byte)4, next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders.Single().Crop);
        var expected = agriculture.Resolve(slices[1].Node, GlobalArena.Foundation.Agriculture.CropId.Rice);
        Assert.Equal(crops[GlobalArena.Foundation.Agriculture.CropId.Rice], expected.Commodity);
        ulong total = timeline.Slices.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
            .GroupBy(s => (s.Worker, s.Node, s.Occupation, s.Crop))
            .Aggregate(0UL, (sum, group) => checked(sum + (ulong)((UInt128)
                (group.Key.Crop.HasValue ? expected.WholeTurnOutput : beginning.Workers.Workers.Single(w => w.Id == group.Key.Worker).OutputMicroPerTurn)
                * (ulong)group.Sum(s => (long)s.DurationMicro) / 1_000_000UL)));
        Assert.Equal(total, sale.ProducedGoodsMicro);
        var intake = Assert.IsAssignableFrom<IReadOnlyList<FoundationWorkerFoodIntake>>(sale.FoodIntake);
        Assert.Equal(beginning.Workers.Workers.Count * 6, intake.Count);
        Assert.Equal(total, intake.Aggregate(0UL, (sum, row) => checked(sum + row.ConsumedMicro)));
        foreach (var cropItem in crops.Values)
        {
            ulong cropProduction = timeline.Slices.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
                .GroupBy(s => (s.Worker, s.Node, s.Occupation, s.Crop))
                .Where(g => (g.Key.Crop.HasValue ? agriculture.Resolve(g.Key.Node, (GlobalArena.Foundation.Agriculture.CropId)g.Key.Crop.Value).Commodity
                    : beginning.Workers.Workers.Single(w => w.Id == g.Key.Worker).Commodity) == cropItem)
                .Aggregate(0UL, (sum, g) => checked(sum + (ulong)((UInt128)
                    (g.Key.Crop.HasValue ? agriculture.Resolve(g.Key.Node, (GlobalArena.Foundation.Agriculture.CropId)g.Key.Crop.Value).WholeTurnOutput
                    : beginning.Workers.Workers.Single(w => w.Id == g.Key.Worker).OutputMicroPerTurn)
                    * (ulong)g.Sum(s => (long)s.DurationMicro) / 1_000_000UL)));
            Assert.Equal(cropProduction, intake.Where(x => x.Commodity == cropItem)
                .Aggregate(0UL, (sum, x) => checked(sum + x.ConsumedMicro)));
        }
        var conversions = crops.Values.ToDictionary(c => c, _ => 0.000001);
        var nutrition = FoundationSettledFoodIntake.Evaluate(worker.Id, intake, conversions,
            new CanonicalNutritionCandidateParameters(0.5, 2.8));
        Assert.Null(nutrition.CandidateQuality);
        Assert.False(nutrition.AuthorizesProductivityChange);

        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(22U, hash.Compute(next).FormatVersion);
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        var water = Enumerable.Range(0, fine.FineElevationRaw.Count).First(i => fine.SurfaceAtIndex(i) != FoundationFineSurfaceKind.Land);
        var waterTile = new PhysicalTacticalTileId(fine.FineParameters, new StrategicCellId((ulong)water + 1UL));
        foreach (var crop in crops.Keys) Assert.Equal(0UL, agriculture.Resolve(waterTile, crop).WholeTurnOutput);
        Assert.Throws<ArgumentException>(() => beginning.InsertWorkerOrder(worker.Owner, worker.Id, 0,
            FoundationWorkerOrderKind.Occupation, occupation: PhysicalWorkerOccupation.Mining, crop: 4));
    }

    [Fact]
    public void R219BConstructionRemainderOperatesOpeningInfrastructureAndReplays()
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
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture, operationalPolicy: new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.Insert, Index: 1,
                    OrderKind: FoundationWorkerOrderKind.Occupation, Occupation: PhysicalWorkerOccupation.LandTransport))
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
        Assert.Equal(PhysicalWorkerActivity.Other, slices[1].Activity);
        Assert.Equal(PhysicalWorkerOccupation.LandTransport, slices[1].Occupation);
        var quotes = Assert.IsAssignableFrom<IReadOnlyList<FoundationOperationalFreightShadowEdge>>(sale.OperationalQuotes);
        Assert.NotEmpty(quotes);
        Assert.Equal(400_000UL, quotes.Aggregate(0UL, (sum, q) => checked(sum + q.OperationalServiceMicro)));
        foreach (var quote in quotes)
        {
            var openingEdge = beginning.Transport.Edges.Single(e => e.Edge == quote.Edge);
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(openingEdge), quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
            Assert.True(quote.ShadowPhysicalCostMicroPerWholeCargoUnit < quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(FoundationOperationalWorkKind.Land, quote.WorkKind);
        }
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
    }

    [Fact]
    public void R2232ManualBaseResumesAfterConstructionAndReplays()
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
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture, operationalPolicy: new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity:
                    new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 4)))
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
        Assert.Equal(PhysicalWorkerOccupation.Agriculture, slices[1].Occupation);
        Assert.Equal((byte)4, slices[1].Crop);
        var queue = next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id);
        Assert.Empty(queue.Orders);
        Assert.Equal((byte)4, queue.BaseActivity!.Crop);
        var input = FoundationTemporalEconomicTurnProjector.ProjectPersistent(beginning, timeline.Slices, demand, agriculture);
        var output = Assert.Single(input.Production, p => p.Worker == worker.Id);
        var expected = agriculture.Resolve(tile, GlobalArena.Foundation.Agriculture.CropId.Rice);
        Assert.Equal(expected.Commodity, output.Commodity);
        Assert.Equal(400_000U, output.EffortMicro);
        Assert.Equal(expected.WholeTurnOutput * 400_000UL / 1_000_000UL, output.ProducedMicro);
        Assert.Equal(1_000_000L, slices.Sum(s => (long)s.DurationMicro));
        Assert.NotNull(sale.FoodIntake);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var second = phase.Resolve(new TurnResolutionInput(next, Array.Empty<ISimulationCommand>(), secondContext), next);
        var secondTimeline = Assert.Single(second.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        var baseSlices = secondTimeline.Slices.Where(s => s.Worker == worker.Id).ToArray();
        Assert.All(baseSlices, s => { Assert.Equal((byte)4, s.Crop); Assert.Equal(PhysicalWorkerActivity.Producing, s.Activity); });
        Assert.Equal(1_000_000L, baseSlices.Sum(s => (long)s.DurationMicro));
        Assert.Equal(hash.Compute(second.ResultingWorldState), hash.Compute(phase.ReplayPersistentJournal(next, second.EventLog, secondContext)));
    }

    [Fact]
    public void R2233BaseResumesAtArrivalAndExactBoundary()
    {
        var (world, worker, _, _, incidence, fine) = Fixture();
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)), workersPerCivilization: 3);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(demo.Session.GeneratedWorld, incidence, fine);
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
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        // Select a real ground crossing whose destination supports the chosen crop.
        // A ground crossing alone does not guarantee agricultural suitability.
        var candidate = (from w in beginning.Workers.Workers
            where w.Activity != PhysicalWorkerActivity.Traveling && w.TacticalTileId.HasValue
            let source = w.TacticalTileId!.Value
            from edge in incidence.FineTopology.Edges
            where edge.IncidentCellIds.Contains(source.FineStrategicCellId)
                && fine.CrossingAtIndices((int)edge.IncidentCellIds[0].Value - 1,
                    (int)edge.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground
            let target = new PhysicalTacticalTileId(incidence.Refinement.FineParameters,
                edge.IncidentCellIds.Single(n => n != source.FineStrategicCellId))
            from crop in crops.Keys
            where agriculture.Resolve(target, crop).WholeTurnOutput > 0
            orderby w.Id != worker.Id, crop != GlobalArena.Foundation.Agriculture.CropId.Rice,
                w.Id.Value, target.FineStrategicCellId.Value, (byte)crop
            select (Worker: w, Source: source, Destination: target, Crop: crop)).First();
        worker = candidate.Worker;
        Assert.NotEqual(PhysicalWorkerActivity.Traveling, worker.Activity);
        Assert.DoesNotContain(beginning.EdgeTravels.Pending, p => p.Worker == worker.Id);
        var tile = candidate.Source;
        var destination = candidate.Destination;
        var selectedCrop = (byte)candidate.Crop;
        Assert.True(agriculture.Resolve(destination, candidate.Crop).WholeTurnOutput > 0);
        var baseActivity = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual,
            PhysicalWorkerOccupation.Agriculture, selectedCrop);
        var moving = beginning.SetWorkerBaseActivity(worker.Owner, worker.Id, baseActivity)
            .InsertWorkerOrder(worker.Owner, worker.Id, 0, FoundationWorkerOrderKind.Move, destination);
        var scheduler = new FoundationPersistentWorkerTurnScheduler(new FoundationConstructionApproachRouter(
            demo.Session.GeneratedWorld, incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)), agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var (first, travel) = scheduler.Record(moving, context);
        var firstSlices = travel.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.All(firstSlices, x => Assert.Equal(PhysicalWorkerActivity.Traveling, x.Activity));
        Assert.Equal(1_000_000L, firstSlices.Sum(x => (long)x.DurationMicro));
        var pending = first.EdgeTravels.Pending.Single(x => x.Worker == worker.Id);
        Assert.Equal(400_000UL, pending.RemainingMicro);
        var secondContext = new SimulationContext(new TurnNumber(2UL), new SimulationSeed(712UL));
        var (second, arrival) = scheduler.Record(first, secondContext);
        var slices = arrival.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(400_000U, slices[0].DurationMicro);
        Assert.Equal(tile, slices[0].Node);
        Assert.Equal(600_000U, slices[1].DurationMicro);
        Assert.Equal(destination, slices[1].Node);
        Assert.Equal(PhysicalWorkerActivity.Producing, slices[1].Activity);
        Assert.Equal(selectedCrop, slices[1].Crop);
        Assert.Empty(second.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        Assert.Equal(baseActivity, second.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).BaseActivity);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(second), hash.Compute(scheduler.Execute(first, arrival, secondContext)));
        var exact = new FoundationPersistentWorkerTurnScheduler(new FoundationConstructionApproachRouter(
            demo.Session.GeneratedWorld, incidence, fine, signature, new FoundationGroundEdgeTravelTimePolicy(1_000_000UL)), agriculture);
        var (boundary, boundaryEvent) = exact.Record(moving, context);
        Assert.All(boundaryEvent.Slices.Where(x => x.Worker == worker.Id), x => Assert.Null(x.Crop));
        Assert.Equal(destination, boundary.Workers.Workers.Single(x => x.Id == worker.Id).TacticalTileId);
        var (following, followingEvent) = exact.Record(boundary, secondContext);
        Assert.Empty(following.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        var followingSlices = followingEvent.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(1_000_000L, followingSlices.Sum(x => (long)x.DurationMicro));
        Assert.All(followingSlices, x => { Assert.Equal(PhysicalWorkerActivity.Producing, x.Activity); Assert.Equal(selectedCrop, x.Crop); });
    }

    [Fact]
    public void R2233PrivateManualBasePublishesOnlyAllReady()
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
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        demo.Session.AcceptFineFirstOpening(beginning, incidence, fine, signature);
        var host = new FoundationTacticalHumanAiTurnHost(demo.Session, incidence,
            beginning.Civilizations.Civilizations, new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>(),
            enableMonetaryInfrastructure: true, settlementPolicy: policy, fineGeography: fine, fineSignature: signature,
            physicalTravelPolicy: new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            constructionMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture);
        var owner = worker.Owner;
        var foreign = beginning.Civilizations.Civilizations.First(c => c != owner);
        var rice = new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 4);
        var hash = host.StateHash;
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanBaseActivity(foreign, worker.Id, rice));
        Assert.Throws<InvalidOperationException>(() => host.QueueHumanBaseActivity(owner, worker.Id,
            new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Automatic)));
        Assert.Equal(0, host.PendingOrderCount);
        host.QueueHumanBaseActivity(owner, worker.Id, rice);
        host.QueueHumanFundedConstruction(owner, edge, 600_000UL,
            new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) });
        var planned = host.GetHumanPersistentQueues(owner).Single(q => q.Worker == worker.Id);
        Assert.Equal(rice, planned.BaseActivity);
        Assert.Single(planned.Orders);
        Assert.DoesNotContain(host.GetHumanPersistentQueues(foreign), q => q.Worker == worker.Id);
        Assert.Null(host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).BaseActivity);
        Assert.Equal(hash, host.StateHash);
        host.SetHumanReady(owner);
        Assert.Null(host.TryCloseTurn());
        Assert.Same(beginning, host.Snapshot);
        host.SetHumanReady(foreign);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        Assert.Single(receipt.PersistentEdits);
        Assert.Single(receipt.PersistentFunding);
        Assert.Equal(1UL, host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        var committed = host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == worker.Id);
        Assert.Equal(rice, committed.BaseActivity);
        Assert.Empty(committed.Orders);
        var slices = receipt.PersistentTimeline!.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(600_000U, slices[0].DurationMicro);
        Assert.Equal(400_000U, slices[1].DurationMicro);
        Assert.Equal((byte)4, slices[1].Crop);
        Assert.Equal(0, host.PendingOrderCount);
        var trade = host.GetLastClosedEconomicTradeView(owner);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset, trade.Availability);
        Assert.Equal((ulong?)1UL, trade.EconomicCycleOrdinal);
        Assert.Equal((WorldStateHash?)receipt.Hash, trade.PublishedHash);
        Assert.Equal(receipt.TemporalEconomicSettlement!.AuditedParticipantEdges![owner].ToArray(),
            trade.UsedEdges.ToArray());
    }

    [Fact]
    public void R2233TwoFiniteBuildsThenBase()
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
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var otherEdge = new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters,
            incidence.FineTopology.Edges.First(e => e.Id != crossing.Id &&
                e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
                fine.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                    (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground).Id);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture);
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 1UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity:
                    new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Agriculture, 4))),
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 2UL), worker.Owner,
                edge, 300_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 3UL), worker.Owner,
                otherEdge, 500_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 1) })
        };
        var result = phase.Resolve(new TurnResolutionInput(beginning, commands, context), beginning);
        var timeline = Assert.Single(result.Events.OfType<FoundationPersistentWorkerTurnEvent>());
        var slices = timeline.Slices.Where(x => x.Worker == worker.Id).ToArray();
        Assert.Equal(3, slices.Length);
        Assert.Equal(300_000U, slices[0].DurationMicro);
        Assert.Equal(1UL, slices[0].ProjectId);
        Assert.Equal(500_000U, slices[1].DurationMicro);
        Assert.Equal(2UL, slices[1].ProjectId);
        Assert.Equal(200_000U, slices[2].DurationMicro);
        Assert.Equal((byte)4, slices[2].Crop);
        Assert.All(result.ResultingWorldState.Constructions.Projects, p => Assert.True(p.IsActivated));
        Assert.Empty(result.ResultingWorldState.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(result.ResultingWorldState),
            hash.Compute(phase.ReplayPersistentJournal(beginning, result.EventLog, context)));
    }

    [Fact]
    public void R2232ManualBaseLogisticsUsesOnlyResidualTimeAndReplays()
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
        var crops = Enum.GetValues<GlobalArena.Foundation.Agriculture.CropId>()
            .ToDictionary(c => c, c => new CommodityId((uint)c));
        var agriculture = new FoundationAgriculturalProductionPolicy(fine, incidence, crops, 2_000_000UL);
        var demand = new FoundationActivityCommodityDemandPolicy(crops.Values.ToArray(),
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(profile => crops.Values.Select(c =>
                new FoundationActivityCommodityDemandCoefficient(profile, c, 1_000_000UL))));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(demand,
            new FoundationCommodityCargoConversionPolicy("crop-test", crops.Values.Select(c =>
                new FoundationCommodityCargoConversion(c, 1_000_000UL))),
            beginning.Civilizations.Civilizations.Select(c => new FoundationPhysicalEconomicTaxRate(c, 0U)),
            maxProducerPasses: 64);
        var phase = new FoundationTemporalCollectiveTurnPhase(demo.Session.GeneratedWorld, incidence,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), policy,
            fine, signature, persistentMoneyMicroPerWholeWork: 1_000_000UL, agriculture: agriculture, operationalPolicy: new FoundationOperationalFreightShadowPolicy(1_000_000UL, 1_000_000UL));
        var context = new SimulationContext(new TurnNumber(1UL), new SimulationSeed(712UL));
        var commands = new ISimulationCommand[]
        {
            new FoundationFundedWorkerIntentCommand(new CommandId(context.Turn, 1UL), worker.Owner,
                edge, 600_000UL, new[] { new FoundationFundedWorkerInsertion(worker.Id, 0) }),
            new FoundationPersistentOrderEditCommand(new CommandId(context.Turn, 2UL),
                new FoundationPersistentOrderEdit(worker.Owner, worker.Id,
                    FoundationPersistentOrderEditKind.SetBaseActivity, BaseActivity:
                    new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.LandTransport)))
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
        Assert.Equal(PhysicalWorkerActivity.Other, slices[1].Activity);
        Assert.Equal(PhysicalWorkerOccupation.LandTransport, slices[1].Occupation);
        Assert.Empty(next.WorkerOrders.Queues.Single(q => q.Worker == worker.Id).Orders);
        var quotes = Assert.IsAssignableFrom<IReadOnlyList<FoundationOperationalFreightShadowEdge>>(sale.OperationalQuotes);
        Assert.NotEmpty(quotes);
        Assert.Equal(400_000UL, quotes.Aggregate(0UL, (sum, q) => checked(sum + q.OperationalServiceMicro)));
        foreach (var quote in quotes)
        {
            var openingEdge = beginning.Transport.Edges.Single(e => e.Edge == quote.Edge);
            Assert.Equal(FoundationPhysicalAuthoritativeTransportProjector.EffectiveUnitFreightCost(openingEdge), quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
            Assert.True(quote.ShadowPhysicalCostMicroPerWholeCargoUnit < quote.OriginalPhysicalCostMicroPerWholeCargoUnit);
        }
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(next), hash.Compute(phase.ReplayPersistentJournal(beginning, resolved.EventLog, context)));
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
        var forgedRead = new FoundationTemporalSalesSettledEvent(
            settled.Id, settled.BeginningHash, settled.PreSettlementHash,
            settled.PreSettlementRevision, settled.EconomicCycleOrdinal,
            settled.ScheduleDigest, settled.CargoUnitCode, settled.ProducedGoodsMicro,
            settled.GrossSalesScaled, settled.PhysicalFreightScaled, settled.SellerNetScaled,
            settled.TaxesScaled, settled.ExternalTollsScaled, settled.Credits,
            settled.FoodIntake, settled.OperationalQuotes, settled.UsedProgressiveDistribution, settled.UsedSafeProgressiveRoutes);
        forgedRead.AttachAuditedParticipantEdges(settled.AuditedParticipantEdges!);
        forgedRead.AttachAuditedLocalPrices(settled.AuditedLocalPrices!);
        var localRows = settled.AuditedLocalEconomics!.Rows.ToArray();
        localRows[0] = localRows[0] with { Commodity = localRows[0].Commodity with { DemandMicro = localRows[0].Commodity.DemandMicro + 1UL } };
        forgedRead.AttachAuditedLocalEconomics(new FoundationSettledLocalEconomics(settled.EconomicCycleOrdinal, localRows));
        var forgedReadJournal = new SimulationEventLog(new[]
        {
            new SimulationEventLogEntry(1UL, movementEvent, wasEligible: true, wasExecuted: true),
            new SimulationEventLogEntry(2UL, forgedRead, wasEligible: true, wasExecuted: true)
        });
        Assert.Throws<InvalidOperationException>(() => FoundationTemporalEconomicJournalGate.ReplayJournal(
            beginning, forgedReadJournal, movement, new FoundationGroundEdgeTravelTimePolicy(1_400_000UL), incidence, policy, context, fine));
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

    [Fact]
    public void R219A2PresenceSharesConserveAndResolveTiesByWorker()
    {
        var a = new PhysicalWorkerId(1UL);
        var b = new PhysicalWorkerId(2UL);
        var c = new PhysicalWorkerId(3UL);
        var shares = FoundationSettledFoodIntake.Split(101UL,
            new[] { (c, 400_000UL), (b, 600_000UL), (a, 1_000_000UL) });
        Assert.Equal(new[] { (a, 51UL), (b, 30UL), (c, 20UL) }, shares);
        Assert.Equal(FoundationSettledFoodIntake.Split(101UL,
            new[] { (a, 1_000_000UL), (b, 600_000UL), (c, 400_000UL) }), shares);
        Assert.Equal(new[] { (a, 1UL), (b, 0UL) },
            FoundationSettledFoodIntake.Split(1UL, new[] { (b, 1UL), (a, 1UL) }));
        Assert.Throws<ArgumentException>(() => FoundationSettledFoodIntake.Split(1UL,
            new[] { (a, 0UL) }));
    }

    [Fact]
    public void R219A2NutritionScalarRequiresExplicitSufficiencyAndSixFoods()
    {
        var worker = new PhysicalWorkerId(1UL);
        var items = Enumerable.Range(1, 6).Select(i => new CommodityId((uint)i)).ToArray();
        var conversions = items.ToDictionary(c => c, _ => 0.000001);
        var mono = items.Select((c, i) => new FoundationWorkerFoodIntake(worker, c, i == 0 ? 6_000_000UL : 0UL)).ToArray();
        var mixed = items.Select(c => new FoundationWorkerFoodIntake(worker, c, 1_000_000UL)).ToArray();
        var p = new CanonicalNutritionCandidateParameters(0.5, 2.8);
        var m = FoundationSettledFoodIntake.Evaluate(worker, mono, conversions, p);
        var v = FoundationSettledFoodIntake.Evaluate(worker, mixed, conversions, p, f => f / (1 + f));
        Assert.Equal(m.FoodPerPerson, v.FoodPerPerson);
        Assert.Equal(1.0, m.CandidateVarietyFactor);
        Assert.True(v.CandidateVarietyFactor > m.CandidateVarietyFactor);
        Assert.Null(m.CandidateQuality);
        Assert.True(v.CandidateQuality > 0);
        Assert.Throws<ArgumentException>(() => FoundationSettledFoodIntake.Evaluate(worker, mixed.Skip(1).ToArray(), conversions, p));
    }

}
