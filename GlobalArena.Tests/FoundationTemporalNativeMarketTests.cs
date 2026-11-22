using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationTemporalNativeMarketTests
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
            workersPerCivilization: 1);
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
        var (world, worker, destination, movement) = Fixture();
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
    public void RealTravelTurnUsesExistingGlobalMarginalSolverAndAuditsGoods()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var before = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var turn = new TurnNumber(1UL);
        var phase = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var resolved = phase.Resolve(
            new TurnResolutionInput(before, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, new SimulationContext(turn, new SimulationSeed(712UL))), before);
        var travel = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(resolved.Events));
        var demand = Demand();
        var temporal = FoundationTemporalEconomicTurnProjector.Project(
            before, resolved.ResultingWorldState, new[] { travel }, demand);
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(
            before, temporal, demand);
        Assert.Equal(temporal.TotalProductionMicro,
            market.TotalActiveCapacityMicroPerTurn);
        Assert.Equal(temporal.Presence.Demand.Aggregate(0UL,
            (sum, row) => checked(sum + row.DemandMicro)),
            market.TotalCommodityDemandMicro);
        Assert.DoesNotContain(market.SupplyLots,
            lot => lot.Owner == worker.Owner);
        Assert.Contains(market.DemandByCivilization,
            row => row.Owner == worker.Owner &&
                row.Tile == worker.TacticalTileId!.Value);

        var cargo = new FoundationCommodityCargoConversionPolicy(
            "test-cargo", demand.Commodities.Select(id =>
                new FoundationCommodityCargoConversion(id, 1_000_000UL)));
        var plan = FoundationTemporalNativeMarginalMarketPlanner.Coordinate(
            before, incidence, temporal, demand, cargo, 64, fine);
        Assert.Equal(temporal.TotalProductionMicro,
            plan.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(temporal.TotalProductionMicro,
            plan.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.False(temporal.IsCommitted);
    }

    [Fact]
    public void NativeTemporalMarketRejectsWrongSourceWorld()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var before = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var turn = new TurnNumber(1UL);
        var phase = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var resolved = phase.Resolve(
            new TurnResolutionInput(before, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, new SimulationContext(turn, new SimulationSeed(712UL))), before);
        var demand = Demand();
        var temporal = FoundationTemporalEconomicTurnProjector.Project(
            before, resolved.ResultingWorldState,
            new[] { Assert.IsType<FoundationEdgeTravelTurnEvent>(
                Assert.Single(resolved.Events)) }, demand);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalNativeMarketInputs.FromTemporal(
                resolved.ResultingWorldState, temporal, demand));
    }
}
