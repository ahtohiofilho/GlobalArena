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
    public void AlternativeG2ToG12GeneratesSignedPhysicalFieldsWithoutG18()
    {
        // R101 regression: the physical worldgen must derive its real fine
        // geometry from the refinement contract, not a hard-coded 3242 count.
        var coarse = new GoldbergParameters(2, 0);
        var fine = new GoldbergParameters(12, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(coarse, fine));
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, coarse));
        var signed = FoundationFineFirstPhysicalGeography.Generate(world, map);
        var replay = FoundationFineFirstPhysicalGeography.Generate(world, map);

        Assert.Equal(coarse, signed.CoarseParameters);
        Assert.Equal(fine, signed.FineParameters);
        Assert.Equal(1442, signed.FineElevationRaw.Count);
        Assert.Equal(map.FineTopology.Cells.Count, signed.FineTemperatureRaw.Count);
        Assert.Equal(signed.FineElevationRaw, replay.FineElevationRaw);
        Assert.Equal(signed.FineTemperatureRaw, replay.FineTemperatureRaw);
        Assert.Equal(signed.SeaLevelRaw, replay.SeaLevelRaw);
    }

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

    [Theory]
    [InlineData(20261012UL)]
    [InlineData(20261013UL)]
    public void LandAndPortInfrastructureShareOneInvestmentLawWithoutTransferFee(
        ulong seed)
    {
        // R125: crossing class determines c0/B, NOT a different
        // capital response formula, ownership rule or special transshipment
        // toll. The maritime overlay is opt-in and fine-first only.
        var host = Host(seed);
        var physical = host.Session.GeneratedWorld;
        var incidence = host.Incidence;
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            physical, incidence);
        var depths = fine.FineElevationRaw
            .Where(h => h <= fine.SeaLevelRaw)
            .Select(h => fine.SeaLevelRaw - h)
            .OrderBy(x => x).ToArray();
        var research = new FoundationExperimentalShallowFreightPolicy(
            depths[(depths.Length - 1) / 4], 10U, 3U, 20U);
        var monetary = new FoundationMonetaryConstructionPolicy(
            2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL);
        var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            physical, incidence, fine, Policy(), monetary, research);
        var rows = seeded.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        int ground = 0, port = 0, openSea = 0;
        foreach (var physicalEdge in incidence.FineTopology.Edges)
        {
            int a = checked((int)physicalEdge.IncidentCellIds[0].Value - 1);
            int b = checked((int)physicalEdge.IncidentCellIds[1].Value - 1);
            var crossing = fine.CrossingAtIndices(a, b);
            var edge = rows[physicalEdge.Id];
            ulong natural = edge.FreePhysicalCostMicroPerWholeCargoUnit;
            ulong bMoney = edge.NaturalConstructionCostMoneyMicro!.Value;
            Assert.True(natural > 0UL && bMoney > 0UL);
            Assert.Equal(natural,
                FoundationPhysicalAuthoritativeTransportProjector
                    .EffectiveUnitFreightCost(edge));
            Assert.Equal(0UL, edge.ExternalTariffMicroPerWholeCargoUnit);
            if (crossing == FoundationFineTransportCrossingKind.OpenSea)
            {
                openSea++;
                Assert.Throws<InvalidOperationException>(() =>
                    FoundationPhysicalAuthoritativeTransportProjector
                        .EffectiveUnitFreightCost(edge with
                        {
                            CommittedInvestmentMoneyMicro = bMoney
                        }));
                continue;
            }
            if (crossing == FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                continue; // The test does not make ice passages navigable.
            if (crossing == FoundationFineTransportCrossingKind.Ground)
                ground++;
            else if (crossing == FoundationFineTransportCrossingKind.Port)
                port++;
            else
                throw new InvalidOperationException("Unknown crossing class.");

            // Exactly the SAME universal c(I) for ground and port.
            // No separate fee for modal changes and no extra port multiplier.
            var invested = edge with
            {
                CommittedInvestmentMoneyMicro = bMoney
            };
            var actual = FoundationPhysicalAuthoritativeTransportProjector
                .EffectiveUnitFreightCost(invested);
            var squared = (System.Numerics.BigInteger)natural * natural;
            ulong expected = checked((ulong)
                FoundationPhysicalAuthoritativeTransportProjector
                    .IntegerSquareRoot(squared / 2));
            Assert.Equal(Math.Max(1UL, expected), actual);
            Assert.InRange(actual, 1UL, natural);
            Assert.Equal(edge.FreePhysicalCostMicroPerWholeCargoUnit,
                invested.FreePhysicalCostMicroPerWholeCargoUnit);
            Assert.Equal(bMoney,
                invested.NaturalConstructionCostMoneyMicro);
            Assert.Equal(0UL, invested.ExternalTariffMicroPerWholeCargoUnit);
        }
        Assert.True(ground > 0 && port > 0 && openSea > 0);
    }

    [Fact]
    public void ShallowPortQaOverlayChangesOnlyNavigableFineFirstSeaAndPortEdges()
    {
        var host = Host();
        var before = host.StateHash;
        var geography = FoundationFineFirstPhysicalGeography.Generate(
            host.Session.GeneratedWorld, host.Incidence);
        var depths = geography.FineElevationRaw
            .Where(h => h <= geography.SeaLevelRaw)
            .Select(h => geography.SeaLevelRaw - h)
            .OrderBy(h => h).ToArray();
        long threshold = depths[(depths.Length - 1) / 4];
        var policy = new FoundationExperimentalShallowFreightPolicy(
            threshold, 10U, 3U, 20U);
        var money = new FoundationMonetaryConstructionPolicy(
            2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL);
        var baseline = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            host.Session.GeneratedWorld, host.Incidence, geography,
            Policy(), money);
        var candidate = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            host.Session.GeneratedWorld, host.Incidence, geography,
            Policy(), money, policy);
        var replay = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            host.Session.GeneratedWorld, host.Incidence, geography,
            Policy(), money, policy);
        Assert.Equal(candidate.Transport.Edges.ToArray(), replay.Transport.Edges.ToArray());
        var originals = baseline.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        var projected = candidate.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        int changedPorts = 0, changedSea = 0;
        foreach (var edge in host.Incidence.FineTopology.Edges)
        {
            var a = checked((int)edge.IncidentCellIds[0].Value - 1);
            var b = checked((int)edge.IncidentCellIds[1].Value - 1);
            var kind = geography.CrossingAtIndices(a, b);
            var beforeEdge = originals[edge.Id];
            var afterEdge = projected[edge.Id];
            ulong baseCost = beforeEdge.FreePhysicalCostMicroPerWholeCargoUnit;
            ulong newCost = afterEdge.FreePhysicalCostMicroPerWholeCargoUnit;
            Assert.InRange(newCost, baseCost, checked(baseCost * 2UL));
            Assert.Equal(beforeEdge.CargoMicroAtReferenceLoad,
                afterEdge.CargoMicroAtReferenceLoad);
            Assert.Equal(beforeEdge.OwnershipKind, afterEdge.OwnershipKind);
            Assert.Equal(beforeEdge.ExternalTariffMicroPerWholeCargoUnit,
                afterEdge.ExternalTariffMicroPerWholeCargoUnit);
            if (kind is FoundationFineTransportCrossingKind.Ground or
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
            {
                Assert.Equal(beforeEdge, afterEdge);
            }
            else if (kind == FoundationFineTransportCrossingKind.OpenSea)
            {
                Assert.Equal(beforeEdge.NaturalConstructionCostMoneyMicro,
                    afterEdge.NaturalConstructionCostMoneyMicro);
                if (newCost > baseCost) changedSea++;
            }
            else
            {
                Assert.Equal(FoundationFineTransportCrossingKind.Port, kind);
                Assert.InRange(
                    afterEdge.NaturalConstructionCostMoneyMicro!.Value,
                    beforeEdge.NaturalConstructionCostMoneyMicro!.Value,
                    checked(beforeEdge.NaturalConstructionCostMoneyMicro!.Value * 2UL));
                if (newCost > baseCost) changedPorts++;
            }
        }
        Assert.True(changedPorts > 0 && changedSea > 0);
        Assert.Equal(before, host.StateHash);
        Assert.False(host.Snapshot.Transport.IsInitialized);
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
