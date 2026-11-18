using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// One real edge: no synthetic node slope/roughness, no owner-based price.
/// Operation uses |delta-height|, construction labor uses its SQUARE.
/// V13 is opt-in and v12 preserves its original canonical hash.
/// </summary>
public sealed class FoundationPhysicalAltitudeCostSeparationTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationNaturalTransportSeedingPolicy Operating(
        ulong landHeightLinear = 4_000_000UL) =>
        new(new FoundationNaturalTransportCostBand(1_000_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(20_000_000UL, 100_000UL),
            new FoundationNaturalTransportCostBand(100_000_000UL, 10_000UL),
            landHeightLinear);

    private static FoundationNaturalConstructionSeedingPolicy Construction(
        ulong landHeightSquared = 8_000_000UL) =>
        new(2_000_000UL, 6_000_000UL, 10_000_000UL,
            landHeightSquared);

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain, Wood },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("altitude-two-cost-conversion", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
            new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
        });

    private static long[] TileHeights(
        FoundationTacticalHumanAiTurnHost host) =>
        host.Incidence.TileIncidences.Select(tile =>
            tile.IncidentCoarseCellIds.Aggregate(0L, (sum, coarse) =>
                checked(sum + host.Session.GeneratedWorld.StrategicPhysicalFields
                    .Elevation.GetRawValue(coarse))) /
                tile.IncidentCoarseCellIds.Count).ToArray();

    [Fact]
    public void EveryRealEdgeUsesLinearOperatingAndQuadraticConstructionCosts()
    {
        var host = Host();
        var result = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence, Operating(),
            Construction());
        Assert.True(result.Transport.HasNaturalConstructionLabor);
        Assert.Equal(host.Incidence.FineTopology.Edges.Count,
            result.Transport.Edges.Count);
        var heights = TileHeights(host);
        long seaLevel = host.Session.GeneratedWorld.StrategicPhysicalFields
            .SeaLevel.RawValue;
        UInt128 scale = (UInt128)StrategicScalarField.Denominator;
        var byEdge = result.Transport.Edges.ToDictionary(row =>
            row.Edge.FineStrategicEdgeId);
        int nonFlatLandEdges = 0;
        foreach (var crossing in host.Incidence.FineTopology.Edges)
        {
            int a = checked((int)crossing.IncidentCellIds[0].Value - 1);
            int b = checked((int)crossing.IncidentCellIds[1].Value - 1);
            bool landA = heights[a] > seaLevel;
            bool landB = heights[b] > seaLevel;
            var row = byEdge[crossing.Id];
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                row.OwnershipKind);
            Assert.Null(row.InfrastructureOwner);
            Assert.Equal(0UL, row.ExternalTariffMicroPerWholeCargoUnit);
            if (landA && landB)
            {
                Int128 d = (Int128)heights[a] - heights[b];
                UInt128 delta = (UInt128)(d < 0 ? -d : d);
                if (delta > 0) nonFlatLandEdges++;
                ulong expectedOperation = checked((ulong)(
                    1_000_000UL + (UInt128)4_000_000UL * delta / scale));
                ulong expectedConstruction = checked((ulong)(
                    2_000_000UL + (UInt128)8_000_000UL * delta * delta /
                    (scale * scale)));
                Assert.Equal(expectedOperation,
                    row.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(expectedConstruction,
                    row.NaturalConstructionLaborMicro);
            }
            else if (landA || landB)
            {
                Assert.Equal(20_000_000UL,
                    row.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(6_000_000UL, row.NaturalConstructionLaborMicro);
            }
            else
            {
                Assert.Equal(100_000_000UL,
                    row.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(10_000_000UL, row.NaturalConstructionLaborMicro);
            }
        }
        Assert.True(nonFlatLandEdges > 0);
        Assert.Equal(result.Transport.Edges.Count,
            result.LandCrossingCount + result.CoastCrossingCount +
            result.SeaCrossingCount);
    }

    [Fact]
    public void ChangingConstructionCoefficientDoesNotRepriceOperationOrMarket()
    {
        var host = Host();
        var world = host.Session.GeneratedWorld;
        var fine = host.Incidence;
        var low = FoundationPhysicalNaturalTransportSeeder.Create(
            world, fine, Operating(), Construction(0UL));
        var high = FoundationPhysicalNaturalTransportSeeder.Create(
            world, fine, Operating(), Construction(20_000_000UL));
        int changedLabor = 0;
        foreach (var (a, b) in low.Transport.Edges.Zip(high.Transport.Edges))
        {
            Assert.Equal(a.Edge, b.Edge);
            Assert.Equal(a.FreePhysicalCostMicroPerWholeCargoUnit,
                b.FreePhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(a.CargoMicroAtReferenceLoad,
                b.CargoMicroAtReferenceLoad);
            if (a.NaturalConstructionLaborMicro != b.NaturalConstructionLaborMicro)
                changedLabor++;
        }
        Assert.True(changedLabor > 0);

        var snapshotLow = host.Snapshot.DeployInitialPhysicalTransport(
            low.Transport, world, fine);
        var snapshotHigh = host.Snapshot.DeployInitialPhysicalTransport(
            high.Transport, world, fine);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(13U, hasher.Compute(snapshotLow).FormatVersion);
        Assert.Equal(13U, hasher.Compute(snapshotHigh).FormatVersion);
        Assert.NotEqual(hasher.Compute(snapshotLow),
            hasher.Compute(snapshotHigh));
        var routesLow = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(snapshotLow, fine).Roads;
        var routesHigh = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(snapshotHigh, fine).Roads;
        Assert.Equal(routesLow.ToArray(), routesHigh.ToArray());
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ChangingOperationCoefficientDoesNotAlterConstructionOrCapacity()
    {
        var host = Host();
        var world = host.Session.GeneratedWorld;
        var fine = host.Incidence;
        var low = FoundationPhysicalNaturalTransportSeeder.Create(
            world, fine, Operating(0UL), Construction());
        var high = FoundationPhysicalNaturalTransportSeeder.Create(
            world, fine, Operating(12_000_000UL), Construction());
        int changedOperation = 0;
        foreach (var (a, b) in low.Transport.Edges.Zip(high.Transport.Edges))
        {
            Assert.Equal(a.Edge, b.Edge);
            Assert.Equal(a.NaturalConstructionLaborMicro,
                b.NaturalConstructionLaborMicro);
            Assert.Equal(a.CargoMicroAtReferenceLoad,
                b.CargoMicroAtReferenceLoad);
            if (a.FreePhysicalCostMicroPerWholeCargoUnit !=
                b.FreePhysicalCostMicroPerWholeCargoUnit)
                changedOperation++;
        }
        Assert.True(changedOperation > 0);
    }

    [Fact]
    public void PartialOrZeroConstructionDataFailsBeforeWorldDeployment()
    {
        var host = Host();
        var fine = host.Incidence;
        var world = host.Session.GeneratedWorld;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoundationNaturalConstructionSeedingPolicy(
                0UL, 1UL, 1UL, 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FoundationNaturalConstructionSeedingPolicy(
                1UL, 0UL, 1UL, 0UL));

        var complete = FoundationPhysicalNaturalTransportSeeder.Create(
            world, fine, Operating(), Construction());
        var original = complete.Transport.Edges.ToArray();
        var incomplete = original.ToArray();
        incomplete[0] = incomplete[0] with
        {
            NaturalConstructionLaborMicro = null
        };
        Assert.Throws<ArgumentException>(() =>
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                incomplete));
        var zero = original.ToArray();
        zero[0] = zero[0] with
        {
            NaturalConstructionLaborMicro = 0UL
        };
        Assert.Throws<ArgumentException>(() =>
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(zero));
        Assert.False(host.Snapshot.Transport.IsInitialized);
    }

    [Fact]
    public void OldV12HashStaysUntouchedAndNewV13MarketIsStillUncommitted()
    {
        var host = Host();
        var world = host.Session.GeneratedWorld;
        var fine = host.Incidence;
        var old = FoundationPhysicalNaturalTransportSeeder.Create(
            world, fine, Operating());
        Assert.False(old.Transport.HasNaturalConstructionLabor);
        Assert.All(old.Transport.Edges, row =>
            Assert.Null(row.NaturalConstructionLaborMicro));
        var v12 = host.Snapshot.DeployInitialPhysicalTransport(
            old.Transport, world, fine);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(12U, hasher.Compute(v12).FormatVersion);

        var newSeed = FoundationPhysicalNaturalTransportSeeder.Create(
            world, fine, Operating(), Construction());
        var v13 = host.Snapshot.DeployInitialPhysicalTransport(
            newSeed.Transport, world, fine);
        var before = hasher.Compute(v13);
        Assert.Equal(13U, before.FormatVersion);
        Assert.NotEqual(hasher.Compute(v12), before);
        Assert.Same(v13.Transport, v13.AdvanceRevision().Transport);
        var candidate = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(
                v13, fine, Demand(), Cargo(), maxProducerPasses: 64);
        Assert.Equal(candidate.Distribution.AllocationAudit
            .TotalAvailableGoodsMicro, candidate.Distribution.AllocationAudit
            .TotalAllocatedGoodsMicro);
        Assert.False(candidate.RepricedWithinCycle);
        Assert.False(candidate.NextCycleQuotes.HasSettledProvenance);
        Assert.Equal(before, hasher.Compute(v13));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }
}
