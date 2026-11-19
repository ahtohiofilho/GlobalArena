using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// R83 read-only safety boundary for future versioned WorldState promotion.
/// The current state hash, save and event replay remain V1. The return value
/// is an admission EVIDENCE, not an authoritative world mutation.
/// </summary>
public static class FoundationFineFirstMigrationAdmission
{
    public sealed record Evidence(
        FoundationFineFirstWorldgenIdentity FineIdentity,
        RuntimeWorldBinding ExistingLegacyBinding,
        ulong ExistingRevision,
        int FineTileCount);

    public static Evidence InspectBeforeMigration(
        WorldState legacySnapshot,
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography physical,
        FoundationFineFirstWorldgenIdentity requestedIdentity)
    {
        ArgumentNullException.ThrowIfNull(legacySnapshot);
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(physical);
        ArgumentNullException.ThrowIfNull(requestedIdentity);

        if (legacySnapshot.WorldBinding is not { } legacy ||
            legacy != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "R83 cannot migrate an unbound or foreign legacy world.");

        // A deployed edge inventory encodes V1 land/sea classification,
        // construction quotes, and lagged freight. Rebinding this live
        // economic ledger to changed terrain would silently falsify it.
        if (legacySnapshot.Transport.IsInitialized ||
            legacySnapshot.Treasury.IsInitialized)
            throw new InvalidOperationException(
                "An active V1 transport/economic world needs a separate " +
                "journaled and hashed migration, never a quiet terrain swap.");

        FoundationFineFirstWorldgenCanonicalSignature.RequireExact(
            requestedIdentity, generatedWorld, incidence, physical);
        return new Evidence(requestedIdentity, legacy,
            legacySnapshot.Revision, physical.FineElevationRaw.Count);
    }
}
