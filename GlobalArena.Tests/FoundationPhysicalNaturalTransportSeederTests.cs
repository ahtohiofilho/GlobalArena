using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Natural land/coast/sea physical data, not a new tax, road asset, naval
/// policy, market price, settlement receipt or production turn.
/// </summary>
public sealed class FoundationPhysicalNaturalTransportSeederTests
{
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);
    private static readonly CivilizationId Red = new(1UL);

    private static FoundationTacticalHumanAiTurnHost Host(ulong seed = 20261012UL) =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    // FIXTURE economic inputs ONLY. The engine intentionally has no
    // unreviewed/default natural crossing rates or cargo capacities.
    private static FoundationNaturalTransportSeedingPolicy Policy(
        ulong land = 1_000_000UL,
        ulong coast = 20_000_000UL,
        ulong sea = 100_000_000UL,
        ulong slope = 4_000_000UL) =>
        new(new FoundationNaturalTransportCostBand(land, 1_000_000UL),
            new FoundationNaturalTransportCostBand(coast, 100_000UL),
            new FoundationNaturalTransportCostBand(sea, 10_000UL),
            slope);

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain, Wood },
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(
                profile => new[] { Grain, Wood }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("natural-freight-fixture", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
            new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
        });

    [Fact]
    public void NaturalWorldSeedsEveryFineEdgeWithoutPoliticalOwnershipOrTolls()
    {
        var host = Host();
        var before = host.StateHash;
        var source = host.Session.GeneratedWorld;
        var result = FoundationPhysicalNaturalTransportSeeder.Create(
            source, host.Incidence, Policy());
        Assert.True(result.Transport.IsInitialized);
        Assert.Equal(host.Incidence.FineTopology.Edges.Count,
            result.Transport.Edges.Count);
        Assert.Equal(host.Incidence.TileIncidences.Count,
            result.LandTileCount + result.SeaTileCount);
        Assert.Equal(result.Transport.Edges.Count,
            result.LandCrossingCount + result.CoastCrossingCount +
            result.SeaCrossingCount);
        Assert.True(result.LandCrossingCount > 0);
        Assert.True(result.CoastCrossingCount > 0);
        Assert.True(result.SeaCrossingCount > 0);
        Assert.All(result.Transport.Edges, row =>
        {
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                row.OwnershipKind);
            Assert.Null(row.InfrastructureOwner);
            Assert.Equal(0UL, row.ExternalTariffMicroPerWholeCargoUnit);
            Assert.True(row.FreePhysicalCostMicroPerWholeCargoUnit > 0UL);
            Assert.True(row.CargoMicroAtReferenceLoad > 0UL);
        });
        Assert.False(host.Snapshot.Transport.IsInitialized);
        Assert.Equal(before, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void TerrainCrossingClassesAndSlopeAreComputedFromCanonicalSeaLevel()
    {
        var host = Host();
        var source = host.Session.GeneratedWorld;
        var surface = source.StrategicPhysicalFields;
        var actual = FoundationPhysicalNaturalTransportSeeder.Create(
            source, host.Incidence, Policy());
        var tileHeights = host.Incidence.TileIncidences.Select(tile =>
            tile.IncidentCoarseCellIds.Aggregate(0L,
                (sum, coarse) => checked(sum +
                    surface.Elevation.GetRawValue(coarse))) /
            tile.IncidentCoarseCellIds.Count).ToArray();
        var rows = actual.Transport.Edges.ToDictionary(
            row => row.Edge.FineStrategicEdgeId);
        foreach (var edge in host.Incidence.FineTopology.Edges)
        {
            int i = checked((int)edge.IncidentCellIds[0].Value - 1);
            int j = checked((int)edge.IncidentCellIds[1].Value - 1);
            bool landI = tileHeights[i] > surface.SeaLevel.RawValue;
            bool landJ = tileHeights[j] > surface.SeaLevel.RawValue;
            var row = rows[edge.Id];
            if (landI && landJ)
            {
                ulong rise = (ulong)Math.Abs(tileHeights[i] - tileHeights[j]);
                ulong expected = 1_000_000UL +
                    (4_000_000UL * rise) /
                    (ulong)StrategicScalarField.Denominator;
                Assert.Equal(expected, row.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(1_000_000UL, row.CargoMicroAtReferenceLoad);
            }
            else if (landI || landJ)
            {
                Assert.Equal(20_000_000UL,
                    row.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(100_000UL, row.CargoMicroAtReferenceLoad);
            }
            else
            {
                Assert.Equal(100_000_000UL,
                    row.FreePhysicalCostMicroPerWholeCargoUnit);
                Assert.Equal(
                    FoundationPhysicalEndogenousCongestionFeedback
                        .UnboundedNaturalCargoCapacity,
                    row.CargoMicroAtReferenceLoad);
            }
        }
    }

    [Fact]
    public void SeedingIsDeterministicAndParametricWithoutChangingOldWorldHashes()
    {
        var host = Host();
        var original = host.Snapshot;
        var previousHash = host.StateHash;
        var a = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence, Policy());
        var b = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence, Policy());
        Assert.Equal(a.LandTileCount, b.LandTileCount);
        Assert.Equal(a.SeaTileCount, b.SeaTileCount);
        Assert.Equal(a.LandCrossingCount, b.LandCrossingCount);
        Assert.Equal(a.CoastCrossingCount, b.CoastCrossingCount);
        Assert.Equal(a.SeaCrossingCount, b.SeaCrossingCount);
        Assert.Equal(a.Transport.Edges.ToArray(), b.Transport.Edges.ToArray());
        var worldA = original.DeployInitialPhysicalTransport(
            a.Transport, host.Session.GeneratedWorld, host.Incidence);
        var worldB = original.DeployInitialPhysicalTransport(
            b.Transport, host.Session.GeneratedWorld, host.Incidence);
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(12U, hasher.Compute(worldA).FormatVersion);
        Assert.Equal(hasher.Compute(worldA), hasher.Compute(worldB));
        Assert.Equal(previousHash, host.StateHash);
        Assert.Equal(11U, host.StateHash.FormatVersion);

        var different = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            Policy(sea: 200_000_000UL));
        var changed = original.DeployInitialPhysicalTransport(
            different.Transport, host.Session.GeneratedWorld, host.Incidence);
        Assert.NotEqual(hasher.Compute(worldA), hasher.Compute(changed));
    }

    [Fact]
    public void SeedingRejectsAbsentEconomicsAndForeignCanonicalWorlds()
    {
        var host = Host();
        var source = host.Session.GeneratedWorld;
        Assert.Throws<ArgumentException>(() =>
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(0UL, 1UL),
                new FoundationNaturalTransportCostBand(1UL, 1UL),
                new FoundationNaturalTransportCostBand(1UL, 1UL), 0UL));
        Assert.Throws<ArgumentException>(() =>
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1UL, 0UL),
                new FoundationNaturalTransportCostBand(1UL, 1UL),
                new FoundationNaturalTransportCostBand(1UL, 1UL), 0UL));

        var other = Host(seed: 20261013UL);
        var foreign = FoundationPhysicalNaturalTransportSeeder.Create(
            other.Session.GeneratedWorld, other.Incidence, Policy());
        Assert.Throws<InvalidOperationException>(() =>
            host.Snapshot.DeployInitialPhysicalTransport(
                foreign.Transport, other.Session.GeneratedWorld,
                host.Incidence));

        var valid = FoundationPhysicalNaturalTransportSeeder.Create(
            source, host.Incidence, Policy());
        Assert.Throws<InvalidOperationException>(() =>
            host.Snapshot.DeployInitialPhysicalTransport(
                PhysicalTransportInfrastructureRuntimeState.CreateInitial(
                    valid.Transport.Edges.Skip(1)),
                source, host.Incidence));
        Assert.False(host.Snapshot.Transport.IsInitialized);
    }

    [Fact]
    public void WorldOnlyMarketCandidateUsesNaturalCostsButPublishesNoTrade()
    {
        var host = Host();
        var original = host.Snapshot;
        var natural = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence, Policy());
        var world = original.DeployInitialPhysicalTransport(
            natural.Transport, host.Session.GeneratedWorld, host.Incidence);
        var before = new CanonicalWorldStateHasher().Compute(world);
        var projected = FoundationPhysicalAuthoritativeTransportProjector
            .FromWorld(world, host.Incidence);
        Assert.Equal(world.Transport.Edges.Count, projected.Roads.Count);
        Assert.All(projected.Roads, row =>
        {
            Assert.Equal(FoundationInfrastructureOwnershipKind.NoInfrastructure,
                row.OwnershipKind);
            Assert.Null(row.InfrastructureOwner);
            Assert.Equal(0UL, row.ExternalTariffMicroPerWholeCargoUnit);
        });
        var candidate = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(world, host.Incidence, Demand(), Cargo(),
                maxProducerPasses: 64);
        // A historical settled quote with an extreme congestion surcharge
        // must not alter authoritative R112 routing, allocation or next-cycle
        // freight. Keep the same physical graph and reference scales so the
        // test would detect a regression that accidentally reuses old rates.
        // The legacy quote snapshot constructor is deliberately internal.
        // Build a synthetic settled history only within the regression test,
        // without expanding the production API to permit fabricated receipts.
        var legacyRows = candidate.NextCycleQuotes.Edges.Select(q => q with
        {
            QuotedCongestionSurchargeMicroPerWholeCargoUnit = 50_000_000UL
        }).ToArray();
        var constructor = typeof(FoundationPhysicalLaggedFreightQuoteState)
            .GetConstructor(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(ulong),
                    typeof(FoundationPhysicalLaggedFreightEdgeState[]),
                    typeof(bool) },
                modifiers: null);
        Assert.NotNull(constructor);
        var historical = (FoundationPhysicalLaggedFreightQuoteState)
            constructor.Invoke(new object[] { 7UL, legacyRows, true });
        var replay = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(world, host.Incidence, Demand(), Cargo(),
                previousClosedCycleQuotes: historical, maxProducerPasses: 64);
        Assert.Equal(candidate.FixedCycleEdges.ToArray(),
            replay.FixedCycleEdges.ToArray());
        // Aggregate totals alone could conceal rerouting between buyers.
        // Compare the complete ordered distribution transcript as well.
        Assert.Equal(
            candidate.Distribution.Candidates.Select(c => (
                c.DeliveryOrdinal, c.Seller, c.SourceOccupation,
                c.SourceTile, c.Buyer, c.BuyerTile, c.Commodity,
                c.GoodsMicro,
                Route: string.Join(",", c.RouteTiles.Select(t =>
                    t.FineStrategicCellId.Value)))).ToArray(),
            replay.Distribution.Candidates.Select(c => (
                c.DeliveryOrdinal, c.Seller, c.SourceOccupation,
                c.SourceTile, c.Buyer, c.BuyerTile, c.Commodity,
                c.GoodsMicro,
                Route: string.Join(",", c.RouteTiles.Select(t =>
                    t.FineStrategicCellId.Value)))).ToArray());
        Assert.Equal(candidate.Distribution.MarketTotals.ToArray(),
            replay.Distribution.MarketTotals.ToArray());
        Assert.Equal(candidate.Distribution.MovedGoodsByPass.ToArray(),
            replay.Distribution.MovedGoodsByPass.ToArray());
        Assert.Equal(candidate.Distribution.Passes,
            replay.Distribution.Passes);
        Assert.Equal(candidate.Distribution.ProductiveLotCount,
            replay.Distribution.ProductiveLotCount);
        Assert.Equal(candidate.Distribution.TotalReallocatedGoodsMicro,
            replay.Distribution.TotalReallocatedGoodsMicro);
        Assert.Equal(
            candidate.Distribution.AllocationAudit.Freight
                .TotalEdgeTraversalCargoMicro,
            replay.Distribution.AllocationAudit.Freight
                .TotalEdgeTraversalCargoMicro);
        Assert.Equal(
            candidate.Distribution.AllocationAudit.TotalAllocatedGoodsMicro,
            replay.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.All(replay.NextCycleQuotes.Edges, q =>
            Assert.Equal(0UL,
                q.QuotedCongestionSurchargeMicroPerWholeCargoUnit));
        Assert.All(replay.EdgeReadings, q =>
        {
            Assert.Equal(q.FreePhysicalCostMicroPerWholeCargoUnit,
                q.CurrentPhysicalQuoteMicroPerWholeCargoUnit);
            Assert.Equal(q.FreePhysicalCostMicroPerWholeCargoUnit,
                q.DampedNextPhysicalQuoteMicroPerWholeCargoUnit);
        });
        Assert.Equal(8UL, replay.NextCycleQuotes.AuditedPreviewCycles);
        Assert.False(replay.NextCycleQuotes.HasSettledProvenance);

        Assert.False(candidate.RepricedWithinCycle);
        Assert.False(candidate.NextCycleQuotes.HasSettledProvenance);
        Assert.All(candidate.NextCycleQuotes.Edges, q =>
            Assert.Equal(0UL,
                q.QuotedCongestionSurchargeMicroPerWholeCargoUnit));
        Assert.All(candidate.EdgeReadings, q =>
        {
            Assert.Equal(q.FreePhysicalCostMicroPerWholeCargoUnit,
                q.CurrentPhysicalQuoteMicroPerWholeCargoUnit);
            Assert.Equal(q.FreePhysicalCostMicroPerWholeCargoUnit,
                q.TargetNextPhysicalQuoteMicroPerWholeCargoUnit);
        });
        Assert.Equal(0UL, candidate.Distribution.AllocationAudit.Freight.UsedEdges
            .Aggregate(0UL, (sum, edge) => checked(sum +
                (edge.OwnershipKind ==
                    FoundationInfrastructureOwnershipKind.Owned
                        ? edge.TotalCargoMicro : 0UL))));
        Assert.Equal(
            candidate.Distribution.AllocationAudit.TotalAvailableGoodsMicro,
            candidate.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }
}
