using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit, opt-in legacy v8 to v9 world-state snapshot migration.
/// A v8 file cannot contain the section-35 transport facility registry, so
/// the migrated v9 state contains EMPTY facilities. This does not prove that
/// any real-world facilities were absent; caller MUST authorize the migration.
/// Neither ecological books nor material reserves are forged here.
/// </summary>
public sealed class EconomicV8ToV9WorldMigrationResult
{
    public WorldState MigratedWorld { get; }
    public WorldStateHash LegacyV8Hash { get; }
    public WorldStateHash MigratedV9Hash { get; }
    public byte[] SerializedV9World { get; }

    internal EconomicV8ToV9WorldMigrationResult(
        WorldState world, WorldStateHash legacyV8Hash,
        WorldStateHash migratedV9Hash, byte[] serialized)
    {
        MigratedWorld = world;
        LegacyV8Hash = legacyV8Hash;
        MigratedV9Hash = migratedV9Hash;
        SerializedV9World = serialized;
    }
}

public sealed class EconomicV8ToV9WorldSnapshotMigrator
{
    /// <summary>
    /// The accepted v8 digest is supplied independently by the host rather
    /// than trusted from the legacy file itself. The v8 bytes are checked
    /// against the original v8 canonical hasher, then re-encoded as v9 and
    /// checked against the new v9 hash. Worldgen remains a required input.
    /// </summary>
    public EconomicV8ToV9WorldMigrationResult Migrate(
        WorldGenerationResult generatedWorld, byte[] serializedV8World,
        WorldStateHash independentlyAcceptedV8Hash)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(serializedV8World);

        if (independentlyAcceptedV8Hash.FormatVersion !=
            LegacyWorldStateV8Hasher.FormatVersion)
            throw new NotSupportedException("Migration requires an accepted v8 hash.");

        var legacy = EconomicWorldStateV8SnapshotCodec.Decode(
            generatedWorld, serializedV8World);
        var actualLegacy = new LegacyWorldStateV8Hasher().Compute(legacy);
        if (actualLegacy != independentlyAcceptedV8Hash)
            throw new InvalidOperationException(
                "Legacy snapshot does not match the independently trusted v8 state.");

        // v8 has no section 35. These are NOT fabricated facilities.
        if (legacy.TransportFacilities.Facilities.Count != 0)
            throw new InvalidOperationException(
                "Migration may not erase pre-existing facility records.");

        var migrated = legacy.WithTransportFacilities(
            TransportFacilitiesRuntimeState.Empty);
        var v9Hash = new CanonicalWorldStateHasher().Compute(migrated);
        var encoded = EconomicWorldStateV9SnapshotCodec.Encode(migrated);

        var verified = EconomicWorldStateV9SnapshotCodec.Decode(
            generatedWorld, encoded);
        if (new CanonicalWorldStateHasher().Compute(verified) != v9Hash)
            throw new InvalidOperationException(
                "Migrated v9 save does not reproduce its intended canonical hash.");

        return new EconomicV8ToV9WorldMigrationResult(
            verified, actualLegacy, v9Hash, encoded);
    }
}
