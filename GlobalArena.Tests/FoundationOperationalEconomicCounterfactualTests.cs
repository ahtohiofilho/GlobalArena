using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationOperationalEconomicCounterfactualTests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationAuthoritativeEconomicSettlementPolicy MarketPolicy() =>
        new(
            new FoundationActivityCommodityDemandPolicy(
                new[] { Grain, Wood },
                Enum.GetValues<FoundationPhysicalDemandProfile>()
                    .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                        new FoundationActivityCommodityDemandCoefficient(
                            profile, item, 1_000_000UL)))),
            new FoundationCommodityCargoConversionPolicy(
                "r154-physical-cargo", new[]
                {
                    new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
                    new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
                }),
            new[]
            {
                new FoundationPhysicalEconomicTaxRate(Human, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Ai, 500U)
            }, maxProducerPasses: 64);

    private static (
        WorldState Before, WorldState After,
        PhysicalTacticalIncidenceMap Incidence,
        FoundationFineFirstPhysicalGeography Geography,
        PhysicalWorkerRuntimeRecord Reassigned) Scenario()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var geometry = FoundationFineFirstPhysicalGeography.Generate(
            fixture.Session.GeneratedWorld, fixture.Incidence);
        var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            fixture.Session.GeneratedWorld, fixture.Incidence, geometry,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var before = fixture.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, fixture.Session.GeneratedWorld, fixture.Incidence);
        var person = before.Workers.Workers.First(worker =>
            worker.Owner == Human &&
            worker.Activity == PhysicalWorkerActivity.Producing);
        var workers = before.Workers.Workers.Select(worker =>
            worker.Id == person.Id
                ? worker.WithTacticalAssignment(
                    worker.TacticalTileId!.Value, worker.CellId,
                    PhysicalWorkerActivity.Other,
                    PhysicalWorkerOccupation.LandTransport)
                : worker).ToArray();
        var after = before.WithWorkers(new PhysicalWorkerRuntimeState(
            workers, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        return (before, after, fixture.Incidence, geometry, person);
    }

    private static FoundationOperationalEconomicCounterfactual Compare(
        WorldState before, WorldState after,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography) =>
        FoundationOperationalEconomicCounterfactualProjector.Compare(
            before, after, incidence, geography, MarketPolicy(),
            new FoundationOperationalProductivityPolicy(
                new FoundationOperationalProductivityBand(
                    1_000_000UL, 10_000U),
                new FoundationOperationalProductivityBand(
                    1_000_000UL, 10_000U)),
            new FoundationOperationalFreightShadowPolicy(
                1_000_000UL, 1_000_000UL));

    [Fact]
    public void RealWorkerTransferCreatesThreeReconciledUncommittedEconomies()
    {
        var s = Scenario();
        var initialRevision = s.Before.Revision;
        var reassignedRevision = s.After.Revision;
        var result = Compare(s.Before, s.After, s.Incidence, s.Geography);

        Assert.False(result.IsCommitted);
        Assert.Equal(new BigInteger(s.Reassigned.OutputMicroPerTurn),
            result.LostProductionGoodsMicro);
        Assert.Equal(result.AllProducingBaseline.ProducedAndSoldGoodsMicro -
            s.Reassigned.OutputMicroPerTurn,
            result.ReassignedWithoutServiceDiscount.ProducedAndSoldGoodsMicro);
        Assert.Equal(result.ReassignedWithoutServiceDiscount.ProducedAndSoldGoodsMicro,
            result.ReassignedWithServiceDiscount.ProducedAndSoldGoodsMicro);
        Assert.True(result.CommittedOperationalService.TotalAllocatedEffortMicro > 0UL);
        Assert.True(result.CommittedOperationalService.TotalServiceMicro > BigInteger.Zero);
        Assert.Contains(result.FreightShadow.EdgeReadings, row =>
            row.OperationalServiceMicro > 0UL &&
            row.ShadowPhysicalCostMicroPerWholeCargoUnit <=
                row.OriginalPhysicalCostMicroPerWholeCargoUnit);
        Assert.True(result.AllProducingBaseline.GrossSalesScaled > BigInteger.Zero);
        Assert.True(result.ReassignedWithoutServiceDiscount.GrossSalesScaled > BigInteger.Zero);
        Assert.True(result.ReassignedWithServiceDiscount.GrossSalesScaled > BigInteger.Zero);
        Assert.Equal(result.ReassignedWithServiceDiscount.GrossSalesScaled -
            result.ReassignedWithoutServiceDiscount.GrossSalesScaled,
            result.OperationalGrossSalesDeltaScaled);
        Assert.Equal(result.ReassignedWithServiceDiscount.NetSellerReceiptsScaled -
            result.AllProducingBaseline.NetSellerReceiptsScaled,
            result.OverallSellerNetDeltaScaled);
        Assert.Equal(initialRevision, s.Before.Revision);
        Assert.Equal(reassignedRevision, s.After.Revision);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            s.Before.Workers.Workers.Single(w =>
                w.Id == s.Reassigned.Id).Activity);
        Assert.Equal(PhysicalWorkerActivity.Other,
            s.After.Workers.Workers.Single(w =>
                w.Id == s.Reassigned.Id).Activity);
    }

    [Fact]
    public void CounterfactualRejectsUnchangedWorkersAndTamperedLocations()
    {
        var s = Scenario();
        Assert.Throws<ArgumentException>(() =>
            Compare(s.Before, s.Before, s.Incidence, s.Geography));

        var relocated = s.After.Workers.Workers.Select(worker =>
            worker.Id == s.Reassigned.Id
                ? worker.WithTacticalLocationAndActivity(
                    new PhysicalTacticalTileId(
                        worker.TacticalTileId!.Value.FineGoldbergParameters,
                        new StrategicCellId(251UL)),
                    worker.CellId, PhysicalWorkerActivity.Other)
                : worker).ToArray();
        var altered = s.After.WithWorkers(new PhysicalWorkerRuntimeState(
            relocated, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        Assert.Throws<ArgumentException>(() =>
            Compare(s.Before, altered, s.Incidence, s.Geography));
    }
}
