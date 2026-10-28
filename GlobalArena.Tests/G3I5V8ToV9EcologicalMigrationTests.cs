using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-I5: real historical GAFULL08 bytes are independently validated,
/// then every natural-capital, reserve and replay-cursor unit is preserved
/// in a GAFULL09 bound to the new authoritative facility-aware hash.
/// </summary>
public sealed class G3I5V8ToV9EcologicalMigrationTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId One = new(1UL);
    private static readonly CivilizationId Two = new(2UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);
    private static readonly EconomicNaturalCapitalPolicy Policy =
        new(15UL * D, 22UL * D, 250_000L, 120_000L, 50_000L);

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(2, 0)));

    private static EconomicLegacyV8EcologicalState Legacy(
        WorldGenerationResult generated)
    {
        var potentials = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var land = generated.StrategicTopology.Cells.Select(c => c.Id).Where(id =>
            potentials.WoodyVegetation.GetRawValue(id) > 0L &&
            potentials.ExposedStone.GetRawValue(id) > 0L).Take(2).ToArray();
        Assert.Equal(2, land.Length);

        var world = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { One, Two }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(land[0], One),
                new StrategicTerritoryControlEntry(land[1], Two)
            }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(A, One, land[0], 6UL, new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Forestry, 4UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture, 2UL)
                    }),
                    new EconomicPointRuntimeState(B, Two, land[1], 5UL, new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining, 3UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture, 2UL)
                    })
                }), new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(A, 6UL),
                    new PopulationPointRuntimeState(B, 5UL)
                }))
            .WithRestoredRevision(17UL);

        var initial = EconomicNaturalCapitalInitializer.Initialize(generated, Policy);
        var natural = new EconomicNaturalCapitalBook(3UL,
            StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion,
            initial.Cells.Select(x => x.CellId == land[0]
                ? new EconomicNaturalCapitalCellState(
                    x.CellId, x.RawWoodCarryingCapacity,
                    x.RawStandingWood - x.RawStandingWood / 3UL,
                    x.RawSurfaceStone - x.RawSurfaceStone / 4UL,
                    x.RawSoilVitality - x.RawSoilVitality / 5L)
                : x));
        var reserves = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(A, Wood, 8UL * D + 17UL),
            new EconomicMaterialReserveBalance(B, Stone, 9UL * D + 31UL),
            new EconomicMaterialReserveBalance(A, Food, 2UL * D + 5UL)
        }, new[]
        {
            new EconomicMaterialReserveTurnCursor(One, 3UL),
            new EconomicMaterialReserveTurnCursor(Two, 3UL)
        });
        return new EconomicLegacyV8EcologicalState(
            generated, world, natural, reserves, Policy, Mapping);
    }

    private static EconomicV8ToV9EcologicalMigrationResult Migrate(
        WorldGenerationResult generated, EconomicLegacyV8EcologicalState prior) =>
        new EconomicV8ToV9EcologicalSaveMigrator().Migrate(generated,
            EconomicLegacyV8CompleteSaveCodec.Encode(prior),
            prior.WorldHash, prior.Sha256Hex);

    [Fact]
    public void AuthenticV8CompleteSaveMigratesToV9WithoutChangingAnyStocks()
    {
        var generated = Generate();
        var prior = Legacy(generated);
        var v8Bytes = EconomicLegacyV8CompleteSaveCodec.Encode(prior);
        var loadedV8 = EconomicLegacyV8CompleteSaveCodec.Decode(generated, v8Bytes);

        Assert.Equal(8U, loadedV8.WorldHash.FormatVersion);
        Assert.Equal(prior.Sha256Hex, loadedV8.Sha256Hex);
        Assert.Equal(v8Bytes, EconomicLegacyV8CompleteSaveCodec.Encode(loadedV8));

        var migrated = Migrate(generated, prior);
        var latest = EconomicCompleteV9SaveBundleCodec.Decode(
            generated, migrated.CompleteV9Bundle);
        Assert.Equal(8U, migrated.AcceptedLegacyWorldHash.FormatVersion);
        Assert.Equal(prior.Sha256Hex, migrated.AcceptedLegacyCheckpointSha256Hex);
        Assert.Equal(9U, latest.WorldHash.FormatVersion);
        Assert.NotEqual(prior.Sha256Hex, latest.Sha256Hex);
        Assert.Equal(migrated.MigratedCheckpoint.Sha256Hex, latest.Sha256Hex);
        Assert.Empty(latest.World.TransportFacilities.Facilities);
        Assert.Equal(17UL, latest.World.Revision);
        Assert.Equal(11UL, latest.World.Economy.EconomicPoints.Aggregate(
            0UL, (sum, x) => sum + x.Workforce));
        Assert.Equal(11UL, latest.World.Population.PointPopulations.Aggregate(
            0UL, (sum, x) => sum + x.Residents));

        Assert.Equal(prior.NaturalCapital.LastAppliedTurn,
            latest.NaturalCapital.LastAppliedTurn);
        Assert.Equal(prior.NaturalCapital.Cells.Select(x => (
            x.CellId, x.RawWoodCarryingCapacity, x.RawStandingWood,
            x.RawSurfaceStone, x.RawSoilVitality)),
            latest.NaturalCapital.Cells.Select(x => (
            x.CellId, x.RawWoodCarryingCapacity, x.RawStandingWood,
            x.RawSurfaceStone, x.RawSoilVitality)));
        Assert.Equal(prior.MaterialReserves.Balances.Select(x => (
            x.EconomicPointId, x.CommodityId, x.RawQuantity)),
            latest.MaterialReserves.Balances.Select(x => (
            x.EconomicPointId, x.CommodityId, x.RawQuantity)));
        Assert.Equal(prior.MaterialReserves.AppliedTurnCursors.Select(x => (
            x.CivilizationId, x.LastAppliedTurn)),
            latest.MaterialReserves.AppliedTurnCursors.Select(x => (
            x.CivilizationId, x.LastAppliedTurn)));
        Assert.Equal(prior.Commodities.Food, latest.Commodities.Food);
        Assert.Equal(prior.Commodities.Wood, latest.Commodities.Wood);
        Assert.Equal(prior.Commodities.Stone, latest.Commodities.Stone);
        Assert.Equal(prior.Policy.RawWoodAtFullEndowment, latest.Policy.RawWoodAtFullEndowment);
        Assert.Equal(prior.Policy.RawStoneAtFullEndowment, latest.Policy.RawStoneAtFullEndowment);
        Assert.Equal(prior.Policy.RawWoodRecoveryFraction, latest.Policy.RawWoodRecoveryFraction);
        Assert.Equal(prior.Policy.RawSoilRecoveryFraction, latest.Policy.RawSoilRecoveryFraction);
        Assert.Equal(prior.Policy.RawSoilWearPerFoodUnit, latest.Policy.RawSoilWearPerFoodUnit);
        Assert.Equal(v8Bytes, EconomicLegacyV8CompleteSaveCodec.Encode(prior));
    }

    [Fact]
    public void RepeatedMigrationProducesExactlyTheSameV9Bytes()
    {
        var generated = Generate();
        var prior = Legacy(generated);
        var first = Migrate(generated, prior);
        var second = Migrate(generated, prior);
        Assert.Equal(first.CompleteV9Bundle, second.CompleteV9Bundle);
        Assert.Equal(first.MigratedCheckpoint.Sha256Hex,
            second.MigratedCheckpoint.Sha256Hex);
    }

    [Fact]
    public void IncorrectTrustedEcologicalDigestAndWorldHashAreRejected()
    {
        var generated = Generate();
        var prior = Legacy(generated);
        var bytes = EconomicLegacyV8CompleteSaveCodec.Encode(prior);
        var migrator = new EconomicV8ToV9EcologicalSaveMigrator();

        Assert.Throws<InvalidOperationException>(() =>
            migrator.Migrate(generated, bytes, prior.WorldHash,
                new string('A', 64)));
        Assert.Throws<InvalidOperationException>(() =>
            migrator.Migrate(generated, bytes,
                new WorldStateHash(8U, new string('B', 64)), prior.Sha256Hex));
        Assert.Throws<NotSupportedException>(() =>
            migrator.Migrate(generated, bytes,
                new CanonicalWorldStateHasher().Compute(prior.World),
                prior.Sha256Hex));
        Assert.Throws<InvalidOperationException>(() =>
            migrator.Migrate(Generate(1UL), bytes, prior.WorldHash,
                prior.Sha256Hex));
    }

    [Fact]
    public void TamperingInnerEcologyCannotBeHiddenByRecomputingOuterChecksum()
    {
        var generated = Generate();
        var prior = Legacy(generated);
        var source = EconomicLegacyV8CompleteSaveCodec.Encode(prior);
        var tampered = (byte[])source.Clone();

        tampered[tampered.Length - 80] ^= 0x01;
        System.Security.Cryptography.SHA256.HashData(
            tampered.AsSpan(0, tampered.Length - 32))
            .CopyTo(tampered, tampered.Length - 32);

        Assert.Throws<InvalidDataException>(() =>
            new EconomicV8ToV9EcologicalSaveMigrator().Migrate(
                generated, tampered, prior.WorldHash, prior.Sha256Hex));
    }

    [Fact]
    public void LegacyReaderRefusesV9FilesAndOriginalV8CannotHideFacilities()
    {
        var generated = Generate();
        var prior = Legacy(generated);
        var future = Migrate(generated, prior);
        Assert.Throws<InvalidDataException>(() =>
            EconomicLegacyV8CompleteSaveCodec.Decode(
                generated, future.CompleteV9Bundle));
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV9SaveBundleCodec.Decode(
                generated, EconomicLegacyV8CompleteSaveCodec.Encode(prior)));

        var withFacility = prior.World.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(11UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                    true, new[]
                    {
                        new TransportFacilityTransferCapability(1UL, 2UL, 1UL)
                    })
            }));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicLegacyV8EcologicalState(
                generated, withFacility,
                prior.NaturalCapital, prior.MaterialReserves,
                prior.Policy, prior.Commodities));
    }

    [Fact]
    public void MigratedCheckpointCanContinueFromTheNextSequentialTurn()
    {
        var generated = Generate();
        var prior = Legacy(generated);
        var latest = Migrate(generated, prior).MigratedCheckpoint;

        var next = new EconomicEcologicalTurnCommitResolver().Propose(
            generated, latest, latest.Sha256Hex, 4UL,
            new[]
            {
                new EconomicDemandProfile(A, Food, 500_000L),
                new EconomicDemandProfile(B, Food, 500_000L)
            }, Array.Empty<EconomicMaterialOriginHoldOrder>());

        Assert.Equal(4UL, next.ProposedNext.NaturalCapital.LastAppliedTurn);
        Assert.Equal(4UL, next.ProposedNext.MaterialReserves.LastAppliedTurn(One));
        Assert.Equal(4UL, next.ProposedNext.MaterialReserves.LastAppliedTurn(Two));
        Assert.Equal(prior.MaterialReserves.RawAvailable(A, Wood),
            next.ProposedNext.MaterialReserves.RawAvailable(A, Wood));
        Assert.Equal(prior.MaterialReserves.RawAvailable(B, Stone),
            next.ProposedNext.MaterialReserves.RawAvailable(B, Stone));
        Assert.Equal(9U, next.ProposedNext.WorldHash.FormatVersion);
        Assert.NotEqual(latest.Sha256Hex, next.ProposedNext.Sha256Hex);
    }
}
