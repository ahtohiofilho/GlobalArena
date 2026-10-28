using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-I3 world snapshot v9, specifically PR #50 facility identity,
/// fine physical tile, operational status and directed technology transfers.
/// Does not require or silently import G3-I2 ecological sidecar data.
/// </summary>
public sealed class G3I3V9WorldSnapshotMigrationTests
{
    private static readonly CivilizationId C1 = new(1UL);
    private static readonly CivilizationId C2 = new(2UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static WorldState World(WorldGenerationResult generated,
        bool facilities = true, bool population = true)
    {
        var land = generated.StrategicTopology.Cells.Select(x => x.Id)
            .Where(id => generated.StrategicPhysicalFields.LandWater.GetKind(id)
                == StrategicLandWaterKind.Land)
            .Take(2).ToArray();
        Assert.Equal(2, land.Length);

        var state = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[]
            {
                new CivilizationRuntimeRecord(C1, land[0]),
                new CivilizationRuntimeRecord(C2, land[1])
            }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(land[0], C1),
                new StrategicTerritoryControlEntry(land[1], C2)
            }))
            .WithDiplomacy(new BaselineDiplomacyRuntimeState(new[]
            {
                new BaselineDiplomacyRelationEntry(
                    C1, C2, BaselineDiplomacyRelationKind.Ally)
            }));

        var economy = new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(A, C1, land[0], 7UL, new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 5UL),
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Mining, 2UL)
            }),
            new EconomicPointRuntimeState(B, C2, land[1], 3UL, new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.TradeLogistics, 3UL)
            })
        });
        state = population
            ? state.WithEconomyAndPopulation(economy,
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(A, 7UL),
                    new PopulationPointRuntimeState(B, 3UL)
                }))
            : state.WithEconomy(economy);

        state = state.WithWarfare(new WarfareRuntimeState(new[]
        {
            new MilitaryUnitRuntimeState(new MilitaryUnitId(3UL), C2, land[1])
        }));

        if (facilities)
            state = state.WithTransportFacilities(Facilities());

        return state.WithRestoredRevision(19UL);
    }

    private static TransportFacilitiesRuntimeState Facilities() =>
        new(new[]
        {
            new TransportFacilityRuntimeRecord(20UL,
                new PhysicalTacticalTileId(
                    new GoldbergParameters(4, 0), new StrategicCellId(12UL)),
                false, new[]
                {
                    new TransportFacilityTransferCapability(21UL, 35UL, 1UL)
                }),
            new TransportFacilityRuntimeRecord(10UL,
                new PhysicalTacticalTileId(
                    new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                true, new[]
                {
                    new TransportFacilityTransferCapability(8UL, 9UL, 2UL),
                    new TransportFacilityTransferCapability(4UL, 5UL, 1UL)
                })
        });

    private static void Rechecksum(byte[] bytes) =>
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32))
            .CopyTo(bytes, bytes.Length - 32);

    [Fact]
    public void V9RoundTripPreservesEveryDomainAndAllFacilityIdentities()
    {
        var generated = Generate();
        var world = World(generated);
        var bytes = EconomicWorldStateV9SnapshotCodec.Encode(world);
        var loaded = EconomicWorldStateV9SnapshotCodec.Decode(generated, bytes);

        Assert.Equal(19UL, loaded.Revision);
        Assert.Equal(9U, new CanonicalWorldStateHasher().Compute(loaded).FormatVersion);
        Assert.Equal(new CanonicalWorldStateHasher().Compute(world),
            new CanonicalWorldStateHasher().Compute(loaded));
        Assert.Equal(2, loaded.Civilizations.Records.Count);
        Assert.Equal(2, loaded.Territory.Controls.Count);
        Assert.Single(loaded.Diplomacy.Relations);
        Assert.Equal(2, loaded.Economy.EconomicPoints.Count);
        Assert.True(loaded.Population.IsInitialized);
        Assert.Equal(2, loaded.Population.PointPopulations.Count);
        Assert.Single(loaded.Warfare.Units);

        Assert.Equal(2, loaded.TransportFacilities.Facilities.Count);
        var facility = loaded.TransportFacilities.Facilities[0];
        Assert.Equal(10UL, facility.FacilityId);
        Assert.True(facility.IsOperational);
        Assert.Equal(3, facility.Tile.FineGoldbergParameters.M);
        Assert.Equal(0, facility.Tile.FineGoldbergParameters.N);
        Assert.Equal(7UL, facility.Tile.FineStrategicCellId.Value);
        Assert.Equal(2, facility.Transfers.Count);
        Assert.Equal(4UL, facility.Transfers[0].FromTechnologyTypeId);
        Assert.Equal(5UL, facility.Transfers[0].ToTechnologyTypeId);
        Assert.Equal(1UL, facility.Transfers[0].TechnologyCatalogVersion);
        Assert.Equal(8UL, facility.Transfers[1].FromTechnologyTypeId);
        Assert.Equal(9UL, facility.Transfers[1].ToTechnologyTypeId);
        Assert.Equal(2UL, facility.Transfers[1].TechnologyCatalogVersion);

        var other = loaded.TransportFacilities.Facilities[1];
        Assert.Equal(20UL, other.FacilityId);
        Assert.False(other.IsOperational);
        Assert.Equal(4, other.Tile.FineGoldbergParameters.M);
        Assert.Equal(12UL, other.Tile.FineStrategicCellId.Value);
        Assert.Equal(bytes, EconomicWorldStateV9SnapshotCodec.Encode(loaded));
    }

    [Fact]
    public void UninitializedLegacyCensusAndEmptyFacilitiesRemainDistinct()
    {
        var generated = Generate();
        var state = World(generated, facilities: false, population: false);
        var bytes = EconomicWorldStateV9SnapshotCodec.Encode(state);
        var loaded = EconomicWorldStateV9SnapshotCodec.Decode(generated, bytes);
        Assert.False(loaded.Population.IsInitialized);
        Assert.Empty(loaded.Population.PointPopulations);
        Assert.Empty(loaded.TransportFacilities.Facilities);
        Assert.Equal(10UL, loaded.Economy.EconomicPoints.Aggregate(
            0UL, (total, point) => total + point.Workforce));
        Assert.Equal(bytes, EconomicWorldStateV9SnapshotCodec.Encode(loaded));
    }

    [Fact]
    public void ExplicitV8MigrationPreservesLegacyStateAndCreatesNoFacilities()
    {
        var generated = Generate();
        var legacy = World(generated, facilities: false);
        var legacyBytes = EconomicWorldStateV8SnapshotCodec.Encode(legacy);
        var expected = new LegacyWorldStateV8Hasher().Compute(legacy);
        Assert.Equal(8U, expected.FormatVersion);

        var result = new EconomicV8ToV9WorldSnapshotMigrator()
            .Migrate(generated, legacyBytes, expected);

        Assert.Equal(expected, result.LegacyV8Hash);
        Assert.Equal(9U, result.MigratedV9Hash.FormatVersion);
        Assert.NotEqual(result.LegacyV8Hash, result.MigratedV9Hash);
        Assert.Equal(19UL, result.MigratedWorld.Revision);
        Assert.Equal(2, result.MigratedWorld.Economy.EconomicPoints.Count);
        Assert.True(result.MigratedWorld.Population.IsInitialized);
        Assert.Single(result.MigratedWorld.Warfare.Units);
        Assert.Empty(result.MigratedWorld.TransportFacilities.Facilities);
        Assert.Equal(result.SerializedV9World,
            EconomicWorldStateV9SnapshotCodec.Encode(result.MigratedWorld));
        Assert.Throws<InvalidDataException>(() =>
            EconomicWorldStateV9SnapshotCodec.Decode(generated, legacyBytes));
    }

    [Fact]
    public void MigrationRequiresIndependentlyTrustedV8HashAndDisallowsFacilityLoss()
    {
        var generated = Generate();
        var legacy = World(generated, facilities: false);
        var bytes = EconomicWorldStateV8SnapshotCodec.Encode(legacy);
        var actual = new LegacyWorldStateV8Hasher().Compute(legacy);
        var wrong = new WorldStateHash(8U, new string('A', 64));

        var migrator = new EconomicV8ToV9WorldSnapshotMigrator();
        Assert.Throws<InvalidOperationException>(() =>
            migrator.Migrate(generated, bytes, wrong));
        Assert.Throws<NotSupportedException>(() =>
            migrator.Migrate(generated, bytes,
                new CanonicalWorldStateHasher().Compute(legacy)));
        Assert.Throws<InvalidOperationException>(() =>
            migrator.Migrate(Generate(1UL), bytes, actual));
        Assert.Throws<InvalidOperationException>(() =>
            EconomicWorldStateV8SnapshotCodec.Encode(World(generated)));
    }

    [Fact]
    public void FacilityIdentityLocationStatusAndDirectionAllChangeV9Hash()
    {
        var generated = Generate();
        var world = World(generated);
        var original = new CanonicalWorldStateHasher().Compute(world);

        var swapped = world.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(99UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                    true,
                    new[] { new TransportFacilityTransferCapability(4UL, 5UL, 1UL) })
            }));
        Assert.NotEqual(original, new CanonicalWorldStateHasher().Compute(swapped));

        var changedStatus = world.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(10UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                    false,
                    new[] { new TransportFacilityTransferCapability(4UL, 5UL, 1UL) })
            }));
        Assert.NotEqual(original, new CanonicalWorldStateHasher().Compute(changedStatus));

        var reverse = world.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(10UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                    true,
                    new[] { new TransportFacilityTransferCapability(5UL, 4UL, 1UL) })
            }));
        Assert.NotEqual(original, new CanonicalWorldStateHasher().Compute(reverse));
    }

    [Fact]
    public void CorruptionWrongWorldAndV8LabelOnV9AreRejected()
    {
        var generated = Generate();
        var world = World(generated);
        var bytes = EconomicWorldStateV9SnapshotCodec.Encode(world);

        Assert.Throws<InvalidOperationException>(() =>
            EconomicWorldStateV9SnapshotCodec.Decode(Generate(1UL), bytes));

        var damaged = (byte[])bytes.Clone();
        damaged[damaged.Length / 2] ^= 0x01;
        Assert.Throws<InvalidDataException>(() =>
            EconomicWorldStateV9SnapshotCodec.Decode(generated, damaged));
        Assert.Throws<InvalidDataException>(() =>
            EconomicWorldStateV9SnapshotCodec.Decode(generated, bytes[..^1]));

        var masquerade = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(
            masquerade.AsSpan(12, 4), 8U);
        Rechecksum(masquerade);
        Assert.Throws<NotSupportedException>(() =>
            EconomicWorldStateV9SnapshotCodec.Decode(generated, masquerade));
    }

    [Fact]
    public void CanonicalFacilityOrderingYieldsIdenticalSnapshotBytes()
    {
        var generated = Generate();
        var world = World(generated);
        var facilities = world.TransportFacilities.Facilities.Reverse().ToArray();
        var reversed = world.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(facilities));
        Assert.Equal(EconomicWorldStateV9SnapshotCodec.Encode(world),
            EconomicWorldStateV9SnapshotCodec.Encode(reversed));
    }

    [Fact]
    public void Version9NeverRewritesOriginalV8StateOrCensus()
    {
        var generated = Generate();
        var old = World(generated, facilities: false, population: false);
        var oldHash = new LegacyWorldStateV8Hasher().Compute(old);
        var oldBytes = EconomicWorldStateV8SnapshotCodec.Encode(old);
        var result = new EconomicV8ToV9WorldSnapshotMigrator()
            .Migrate(generated, oldBytes, oldHash);
        Assert.Equal(oldHash, new LegacyWorldStateV8Hasher().Compute(old));
        Assert.Equal(oldBytes, EconomicWorldStateV8SnapshotCodec.Encode(old));
        Assert.False(result.MigratedWorld.Population.IsInitialized);
        Assert.Empty(result.MigratedWorld.TransportFacilities.Facilities);
    }
}
