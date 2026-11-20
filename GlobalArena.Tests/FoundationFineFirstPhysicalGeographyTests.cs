using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFineFirstPhysicalGeographyTests
{
    private static FoundationTacticalHumanAiTurnHost Host(ulong seed = 20261012UL) =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationNaturalTransportSeedingPolicy Policy() => new(
        new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
        new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
        new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
        10_000UL);

    [Fact]
    public void FineFieldsGenerateFirstAndStrategicRegionsOnlyAggregateTheirIncidences()
    {
        var host = Host();
        var before = host.StateHash;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            host.Session.GeneratedWorld, host.Incidence);
        var replay = FoundationFineFirstPhysicalGeography.Generate(
            host.Session.GeneratedWorld, host.Incidence);
        int total = host.Incidence.TileIncidences.Count;
        Assert.Equal(total, fine.FineElevationRaw.Count);
        Assert.Equal(total, fine.FineMoistureRaw.Count);
        Assert.Equal(total, fine.FineTemperatureRaw.Count);
        Assert.Equal(fine.FineElevationRaw, replay.FineElevationRaw);
        Assert.Equal(fine.FineMoistureRaw, replay.FineMoistureRaw);
        Assert.Equal(fine.FineTemperatureRaw, replay.FineTemperatureRaw);
        Assert.Equal(fine.SeaLevelRaw, replay.SeaLevelRaw);
        Assert.True(fine.LandTileCount > 0 && fine.LandTileCount < total);
        Assert.True(fine.FineElevationRaw.Distinct().Count() >
            host.Incidence.CoarseTopology.Cells.Count);

        // Coarse values are computed from fine tiles only, with no extra
        // independent random draw and no coarse V1 elevation input.
        var sums = new long[host.Incidence.CoarseTopology.Cells.Count];
        var counts = new int[sums.Length];
        var landCounts = new int[sums.Length];
        for (int i = 0; i < total; i++)
        {
            var incidence = host.Incidence.TileIncidences[i];
            var height = fine.FineElevationRaw[i];
            Assert.Equal(height > fine.SeaLevelRaw,
                fine.IsLand(incidence.PhysicalTacticalTileId));
            foreach (var coarse in incidence.IncidentCoarseCellIds)
            {
                var index = checked((int)coarse.Value - 1);
                sums[index] = checked(sums[index] + height);
                counts[index]++;
                if (height > fine.SeaLevelRaw) landCounts[index]++;
            }
        }
        for (int i = 0; i < sums.Length; i++)
        {
            Assert.Equal(sums[i] / counts[i],
                fine.StrategicMeanElevationRaw[i]);
            Assert.Equal(counts[i], fine.StrategicIncidentTileCount[i]);
            Assert.Equal(landCounts[i], fine.StrategicLandTileCount[i]);
        }
        Assert.False(host.Snapshot.Transport.IsInitialized);
        Assert.Equal(before, host.StateHash);
    }

    [Fact]
    public void FineTransportPreviewUsesFineFirstSeaAndSlopeWithoutChangingLegacyWorld()
    {
        var host = Host();
        var before = host.StateHash;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            host.Session.GeneratedWorld, host.Incidence);
        var legacy = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence, Policy());
        var actual = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            host.Session.GeneratedWorld, host.Incidence, fine, Policy());
        var again = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            host.Session.GeneratedWorld, host.Incidence, fine, Policy());
        Assert.Equal(fine.LandTileCount, actual.LandTileCount);
        Assert.Equal(actual.Transport.Edges.ToArray(),
            again.Transport.Edges.ToArray());
        Assert.Equal(host.Incidence.FineTopology.Edges.Count,
            actual.Transport.Edges.Count);
        Assert.True(actual.LandCrossingCount > 0 &&
            actual.CoastCrossingCount > 0 && actual.SeaCrossingCount > 0);
        Assert.NotEqual(legacy.LandTileCount, actual.LandTileCount);
        Assert.Equal(actual.Transport.Edges.Count,
            actual.LandCrossingCount + actual.CoastCrossingCount +
            actual.SeaCrossingCount);
        var rows = actual.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        foreach (var edge in host.Incidence.FineTopology.Edges)
        {
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            var mode = fine.CrossingAtIndices(a, b);
            var record = rows[edge.Id];
            if (mode == FoundationFineTransportCrossingKind.Ground)
            {
                ulong difference = (ulong)Math.Abs(
                    fine.FineElevationRaw[a] - fine.FineElevationRaw[b]);
                Assert.Equal(1_000UL + 10_000UL * difference /
                    (ulong)StrategicScalarField.Denominator,
                    record.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(1_000_000UL, record.CargoMicroAtReferenceLoad);
            }
            else if (mode == FoundationFineTransportCrossingKind.Port ||
                mode == FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
            {
                Assert.Equal(5_000UL, record.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(500_000UL, record.CargoMicroAtReferenceLoad);
            }
            else
            {
                Assert.Equal(FoundationFineTransportCrossingKind.OpenSea, mode);
                Assert.Equal(25_000UL, record.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(
                    FoundationPhysicalEndogenousCongestionFeedback
                        .UnboundedNaturalCargoCapacity,
                    record.CargoMicroAtReferenceLoad);
            }
        }
        Assert.All(actual.Transport.Edges, edge =>
        {
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                edge.OwnershipKind);
            Assert.Null(edge.InfrastructureOwner);
            Assert.Equal(0UL, edge.ExternalTariffMicroPerWholeCargoUnit);
        });
        Assert.False(host.Snapshot.Transport.IsInitialized);
        Assert.Equal(before, host.StateHash);

        var foreign = Host(20261013UL);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
                foreign.Session.GeneratedWorld, foreign.Incidence,
                fine, Policy()));
    }
}
