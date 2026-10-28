using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit migration certificate, not a gameplay event or administrative
/// permission. The independent trust anchors must be supplied by the host.
/// </summary>
public sealed class EconomicV8ToV9EcologicalMigrationResult
{
    public string AcceptedLegacyCheckpointSha256Hex { get; }
    public WorldStateHash AcceptedLegacyWorldHash { get; }
    public EconomicEcologicalCheckpoint MigratedCheckpoint { get; }
    public byte[] CompleteV9Bundle { get; }

    internal EconomicV8ToV9EcologicalMigrationResult(
        string legacyDigest, WorldStateHash legacyWorldHash,
        EconomicEcologicalCheckpoint migrated, byte[] encoded)
    {
        AcceptedLegacyCheckpointSha256Hex = legacyDigest;
        AcceptedLegacyWorldHash = legacyWorldHash;
        MigratedCheckpoint = migrated;
        CompleteV9Bundle = encoded;
    }
}

/// <summary>
/// G3-I5: validates the entire original GAFULL08 world + ecological sidecar
/// against independently accepted v8 trust anchors BEFORE constructing a new
/// GAFULL09. All environmental and commodity books are carried unchanged.
/// Facilities are necessarily empty in the v8 source; external facility
/// reconciliation requires a separate explicitly approved procedure.
/// </summary>
public sealed class EconomicV8ToV9EcologicalSaveMigrator
{
    private readonly EconomicV8ToV9WorldSnapshotMigrator _worldMigrator = new();

    public EconomicV8ToV9EcologicalMigrationResult Migrate(
        WorldGenerationResult generatedWorld,
        byte[] oldCompleteV8Bundle,
        WorldStateHash independentlyAcceptedV8WorldHash,
        string independentlyAcceptedV8EcologicalDigest)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(oldCompleteV8Bundle);
        var trusted = EconomicEcologicalSidecarArchive.NormalizeDigest(
            independentlyAcceptedV8EcologicalDigest);
        if (independentlyAcceptedV8WorldHash.FormatVersion !=
            LegacyWorldStateV8Hasher.FormatVersion)
            throw new NotSupportedException("Ecological migration needs a trusted v8 world hash.");

        // Includes authentic v8 world codec, v8 legacy hasher, v8 ecological
        // commitment, canonical ordering and all enclosing checksums.
        var old = EconomicLegacyV8CompleteSaveCodec.Decode(
            generatedWorld, oldCompleteV8Bundle);
        if (old.WorldHash != independentlyAcceptedV8WorldHash ||
            !string.Equals(old.Sha256Hex, trusted, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Legacy world/ecology differ from independently accepted v8 commitments.");

        var verifiedWorld = _worldMigrator.Migrate(
            generatedWorld,
            EconomicWorldStateV8SnapshotCodec.Encode(old.World),
            independentlyAcceptedV8WorldHash);

        var migrated = new EconomicEcologicalCheckpoint(
            generatedWorld, verifiedWorld.MigratedWorld,
            old.NaturalCapital, old.MaterialReserves, old.Policy, old.Commodities);
        if (migrated.WorldHash.FormatVersion !=
            CanonicalWorldStateHasher.FormatVersion ||
            migrated.WorldHash.FormatVersion != 9U)
            throw new InvalidOperationException("Migrated ecology must bind to the v9 world hash.");

        // No reinitialization, capacity changes, material mint/burn, new
        // cursor, changed calendar, or fabricated transport facility.
        if (!ReferenceEquals(migrated.NaturalCapital, old.NaturalCapital) ||
            !ReferenceEquals(migrated.MaterialReserves, old.MaterialReserves) ||
            !ReferenceEquals(migrated.Policy, old.Policy) ||
            !ReferenceEquals(migrated.Commodities, old.Commodities) ||
            migrated.NaturalCapital.LastAppliedTurn != old.NaturalCapital.LastAppliedTurn ||
            migrated.World.TransportFacilities.Facilities.Count != 0)
            throw new InvalidOperationException("Legacy migration changed physical or semantic state.");

        var encoded = EconomicCompleteV9SaveBundleCodec.Encode(migrated);
        var reloaded = EconomicCompleteV9SaveBundleCodec.Decode(
            generatedWorld, encoded);
        if (reloaded.Sha256Hex != migrated.Sha256Hex ||
            reloaded.WorldHash != migrated.WorldHash)
            throw new InvalidOperationException(
                "Migrated v9 bundle did not preserve its canonical commitment.");

        return new EconomicV8ToV9EcologicalMigrationResult(
            old.Sha256Hex, old.WorldHash, reloaded, encoded);
    }
}
