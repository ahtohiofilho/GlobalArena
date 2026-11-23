using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationTemporalFinancialReconciliationTests
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
    public void CrossTurnPhysicalSalesReconcileExactMoneyWithoutTreasuryMutation()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var before = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var initialHash = new CanonicalWorldStateHasher().Compute(before);
        var turn = new TurnNumber(1UL);
        var result = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(before, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, new SimulationContext(turn, new SimulationSeed(712UL))), before);
        var travel = Assert.IsType<FoundationEdgeTravelTurnEvent>(
            Assert.Single(result.Events));
        var demand = Demand();
        var temporal = FoundationTemporalEconomicTurnProjector.Project(
            before, result.ResultingWorldState, new[] { travel }, demand);
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "test-cargo", demand.Commodities.Select(item =>
                new FoundationCommodityCargoConversion(item, 1_000_000UL)));
        var taxes = before.Civilizations.Civilizations.Select(c =>
            new FoundationPhysicalEconomicTaxRate(c,
                c == worker.Owner ? 1000U : 0U));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(
            demand, cargo, taxes, maxProducerPasses: 64);
        var financial = FoundationTemporalFinancialReconciler.Evaluate(
            before, incidence, temporal, policy, fine);
        Assert.False(financial.IsCommitted);
        Assert.False(financial.Accounting.IsCommitted);
        Assert.Equal(temporal.TotalProductionMicro, financial.GoodsProducedMicro);
        Assert.Equal(financial.GoodsProducedMicro,
            financial.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(financial.Accounting.GrossSalesScaled,
            financial.Accounting.PhysicalFreightScaled +
            financial.CashCreditsScaled);
        Assert.Equal(financial.Accounting.ExternalTollsScaled,
            financial.Accounting.Accounts.Aggregate(BigInteger.Zero,
                (sum, a) => sum + a.TollIncomeScaled));
        Assert.Equal(initialHash, new CanonicalWorldStateHasher().Compute(before));
        var replay = FoundationTemporalFinancialReconciler.Evaluate(
            before, incidence, temporal, policy, fine);
        Assert.Equal(financial.CashCreditsScaled, replay.CashCreditsScaled);
        Assert.Equal(financial.Accounting.GrossSalesScaled,
            replay.Accounting.GrossSalesScaled);
    }

    [Fact]
    public void IncompleteTaxPolicyCannotGenerateTemporalMoney()
    {
        var (world, worker, destination, movement, incidence, fine) = Fixture();
        var before = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var turn = new TurnNumber(1UL);
        var result = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL)).Resolve(
            new TurnResolutionInput(before, new ISimulationCommand[]
            {
                new FoundationEdgeTravelTurnCommand(
                    new CommandId(turn, 1UL), worker.Owner, worker.Id,
                    destination, isStart: true)
            }, new SimulationContext(turn, new SimulationSeed(712UL))), before);
        var demand = Demand();
        var temporal = FoundationTemporalEconomicTurnProjector.Project(
            before, result.ResultingWorldState,
            new[] { Assert.IsType<FoundationEdgeTravelTurnEvent>(
                Assert.Single(result.Events)) }, demand);
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "test-cargo", demand.Commodities.Select(item =>
                new FoundationCommodityCargoConversion(item, 1_000_000UL)));
        var policy = new FoundationAuthoritativeEconomicSettlementPolicy(
            demand, cargo, new[] {
                new FoundationPhysicalEconomicTaxRate(worker.Owner, 0U)
            });
        Assert.Throws<ArgumentException>(() =>
            FoundationTemporalFinancialReconciler.Evaluate(
                before, incidence, temporal, policy, fine));
    }
}
