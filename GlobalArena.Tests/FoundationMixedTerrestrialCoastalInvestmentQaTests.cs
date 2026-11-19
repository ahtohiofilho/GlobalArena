using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// R53: verify operating effects of SAME-rule land and coastal capital.
/// These are REAL canonical edges and treasury-debited money, with an
/// unchanged workforce and market. No fictitious port, commodity inventory,
/// worker, route memory, world age or unapproved operating-cost discount.
/// </summary>
public sealed class FoundationMixedTerrestrialCoastalInvestmentQaTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private readonly ITestOutputHelper _output;

    public FoundationMixedTerrestrialCoastalInvestmentQaTests(
        ITestOutputHelper output) => _output = output;

    [Fact]
    public void RealLandAndCoastCapitalABPreservesPositiveNaturalFreightAndConservedSales()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var baseHash = host.StateHash;
        var baseSnapshot = host.Snapshot;
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var rows = seeded.Transport.Edges.ToDictionary(x => x.Edge.FineStrategicEdgeId);
        var top = host.Incidence.FineTopology.Edges;
        // Choose a genuine coastal edge whose LAND endpoint has a land
        // crossing. Only these two REAL adjacent edges form this local
        // controlled corridor. The full graph remains in the world state.
        var pair = (
            from coast in top
            let coastRecord = rows[coast.Id]
            where coastRecord.CargoMicroAtReferenceLoad == 500_000UL
            from land in top
            let landRecord = rows[land.Id]
            where landRecord.CargoMicroAtReferenceLoad == 1_000_000UL
            where coast.IncidentCellIds.Any(id =>
                land.IncidentCellIds.Contains(id))
            select new { Coast = coast, Land = land }).First();
        var middle = pair.Coast.IncidentCellIds.Single(id =>
            pair.Land.IncidentCellIds.Contains(id));
        var landEnd = pair.Land.IncidentCellIds.Single(id => id != middle);
        var waterEnd = pair.Coast.IncidentCellIds.Single(id => id != middle);
        var fine = host.Incidence.Refinement.FineParameters;
        PhysicalTacticalTileId fineTile(StrategicCellId id) => new(fine, id);
        StrategicCellId coarse(StrategicCellId id) =>
            host.Incidence.TileIncidences[checked((int)id.Value - 1)]
                .IncidentCoarseCellIds[0];
        var red = host.Snapshot.Workers.Workers.First(x => x.Owner == Red);
        var blue = host.Snapshot.Workers.Workers.First(x => x.Owner == Blue);
        var census = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(red.Id, Red, coarse(landEnd),
                One, red.OutputMicroPerTurn, PhysicalWorkerActivity.Producing,
                red.DemandMultiplierMicro, fineTile(landEnd), red.Occupation),
            new PhysicalWorkerRuntimeRecord(blue.Id, Blue, coarse(waterEnd),
                Two, blue.OutputMicroPerTurn, PhysicalWorkerActivity.Producing,
                blue.DemandMultiplierMicro, fineTile(waterEnd), blue.Occupation)
        }, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = new FoundationActivityCommodityDemandPolicy(
            new[] { One, Two },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => new[] { One, Two }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "mixed-land-coast-real-edges", new[]
            {
                new FoundationCommodityCargoConversion(One, 1_000_000UL),
                new FoundationCommodityCargoConversion(Two, 1_000_000UL)
            });
        var noConstruction = host.Snapshot.WithWorkers(census)
            .DeployInitialPhysicalTransport(seeded.Transport,
                host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (Red, 1_000_000_000UL),
                    (Blue, 1_000_000_000UL)
                }));
        var landEdge = rows[pair.Land.Id].Edge;
        var coastEdge = rows[pair.Coast.Id].Edge;
        var seaEdge = seeded.Transport.Edges.First(x =>
            x.CargoMicroAtReferenceLoad == ulong.MaxValue);
        var originalCash = noConstruction.Treasury.Get(Red).CashScaled;
        var signatures = new List<string>();
        foreach (var (label, terrestrial, coastal) in new[]
        {
            ("natural", false, false),
            ("land_only", true, false),
            ("coast_only", false, true),
            ("both_improved", true, true)
        })
        {
            var snapshot = noConstruction;
            if (terrestrial)
            {
                var record = rows[pair.Land.Id];
                snapshot = snapshot.RecordMonetaryInfrastructureInvestment(
                    Red, landEdge,
                    checked(record.NaturalConstructionCostMoneyMicro!.Value * 4UL));
            }
            if (coastal)
            {
                var record = rows[pair.Coast.Id];
                snapshot = snapshot.RecordMonetaryInfrastructureInvestment(
                    Red, coastEdge,
                    checked(record.NaturalConstructionCostMoneyMicro!.Value * 4UL));
            }
            var (fullRoads, fullScales) =
                FoundationPhysicalAuthoritativeTransportProjector
                    .FromWorld(snapshot, host.Incidence);
            // The controlled corridor contains exactly the 2 real adjacent
            // edges; unprovided edges are NOT treated as free shortcuts.
            var roads = fullRoads.Where(x =>
                x.Edge == landEdge || x.Edge == coastEdge).ToArray();
            var scales = fullScales.Where(x =>
                x.Edge == landEdge || x.Edge == coastEdge).ToArray();
            Assert.Equal(2, roads.Length);
            Assert.Equal(2, scales.Length);
            ulong kLand = scales.Single(x => x.Edge == landEdge)
                .CargoMicroAtReferenceLoad;
            ulong kCoast = scales.Single(x => x.Edge == coastEdge)
                .CargoMicroAtReferenceLoad;
            Assert.Equal(terrestrial, kLand > rows[pair.Land.Id].CargoMicroAtReferenceLoad);
            Assert.Equal(coastal, kCoast > rows[pair.Coast.Id].CargoMicroAtReferenceLoad);
            Assert.All(roads, road => Assert.True(
                road.PhysicalCostMicroPerWholeCargoUnit > 0UL));
            Assert.Equal(ulong.MaxValue,
                fullScales.Single(x => x.Edge == seaEdge.Edge)
                    .CargoMicroAtReferenceLoad);
            var actualSpent = originalCash - snapshot.Treasury.Get(Red).CashScaled;
            Assert.Equal(terrestrial || coastal, actualSpent > 0);
            FoundationPhysicalLaggedFreightQuoteState? previous = null;
            for (int cycle = 1; cycle <= 4; cycle++)
            {
                var plan = FoundationPhysicalLaggedFreightCyclePlanner
                    .CoordinateUniversalCandidate(census, host.Incidence,
                        demand, cargo, roads, scales,
                        previousClosedCycleQuotes: previous,
                        maxProducerPasses: 64);
                var audit = plan.Distribution.AllocationAudit;
                Assert.Equal(audit.TotalAvailableGoodsMicro,
                    audit.TotalAllocatedGoodsMicro);
                ulong landCargo = plan.EdgeReadings.Single(x =>
                    x.Edge == landEdge).CurrentCargoMicro;
                ulong coastCargo = plan.EdgeReadings.Single(x =>
                    x.Edge == coastEdge).CurrentCargoMicro;
                _output.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "GA_R53 profile={0} cycle={1} K_land={2} K_coast={3} " +
                    "V_land={4} V_coast={5} land_quote={6} coast_quote={7} " +
                    "deliveries={8} spent_scaled={9}",
                    label, cycle, kLand, kCoast, landCargo, coastCargo,
                    plan.EdgeReadings.Single(x =>
                        x.Edge == landEdge)
                        .CurrentPhysicalQuoteMicroPerWholeCargoUnit,
                    plan.EdgeReadings.Single(x =>
                        x.Edge == coastEdge)
                        .CurrentPhysicalQuoteMicroPerWholeCargoUnit,
                    audit.DeliveryCount, actualSpent));
                signatures.Add(label + ":" + cycle + ":" +
                    landCargo + ":" + coastCargo);
                previous = plan.NextCycleQuotes;
            }
        }
        Assert.Equal(16, signatures.Count);
        Assert.Same(baseSnapshot, host.Snapshot);
        Assert.Equal(baseHash, host.StateHash);
    }
}
