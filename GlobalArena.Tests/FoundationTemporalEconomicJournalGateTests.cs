using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
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
        var second = FoundationTemporalEconomicJournalGate.CloseAndReplay(
            receipt.Next, new[] { secondEvent }, movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
            incidence, policy, nextContext, fine);
        Assert.Empty(second.Next.EdgeTravels.Pending);
        Assert.Equal(2UL, second.Next.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Equal(destination,
            second.Next.Workers.Workers.Single(w => w.Id == worker.Id)
                .TacticalTileId);
        Assert.Equal(400_000U,
            secondEvent.Receipt.TurnResult!.TimeSlices[0].PresenceMicro);
        Assert.Equal(600_000U,
            secondEvent.Receipt.TurnResult.TimeSlices[1].ProductiveEffortMicro);
        Assert.Equal(second.Hash, new CanonicalWorldStateHasher().Compute(
            FoundationTemporalEconomicJournalGate.ReplayJournal(
                receipt.Next, second.Journal, movement,
                new FoundationGroundEdgeTravelTimePolicy(1_400_000UL),
                incidence, policy, nextContext, fine)));
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
        var signed = FoundationFineFirstPhysicalGeography.Generate(
            demo.Session.GeneratedWorld, demo.Incidence);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            demo.Session.GeneratedWorld, demo.Incidence, signed);
        var investmentExecutor = new FoundationMonetaryInfrastructureEventExecutor(
            demo.Session.GeneratedWorld, incidence, signed, signature);
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
}
