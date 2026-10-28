using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A player's or construction subsystem's proposed commissioning identity.
/// A proposal is not a completion certificate. It does not carry material
/// costs, inventory withdrawals, cash, a fabricated terminal type or items.
/// The stated capabilities must already exist in the recorded WorldState.
/// </summary>
public sealed class EconomicFacilityCommissioningIntent
{
    public ulong FacilityId { get; }
    public PhysicalTacticalTileId ExpectedTile { get; }
    public IReadOnlyList<TransportFacilityTransferCapability> ExpectedCapabilities { get; }

    public EconomicFacilityCommissioningIntent(
        ulong facilityId, PhysicalTacticalTileId expectedTile,
        IEnumerable<TransportFacilityTransferCapability> expectedCapabilities)
    {
        if (facilityId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        if (!expectedTile.IsValid)
            throw new ArgumentException("Commissioning requires a valid exact fine tile.",
                nameof(expectedTile));
        ArgumentNullException.ThrowIfNull(expectedCapabilities);
        var capabilities = expectedCapabilities.ToArray();
        if (capabilities.Length == 0 || capabilities.Any(x => x is null))
            throw new ArgumentException(
                "A transfer installation needs at least one explicit capability.",
                nameof(expectedCapabilities));
        capabilities = capabilities
            .OrderBy(x => x.FromTechnologyTypeId)
            .ThenBy(x => x.ToTechnologyTypeId)
            .ThenBy(x => x.TechnologyCatalogVersion)
            .ToArray();
        for (var i = 1; i < capabilities.Length; i++)
            if (capabilities[i - 1].FromTechnologyTypeId ==
                    capabilities[i].FromTechnologyTypeId &&
                capabilities[i - 1].ToTechnologyTypeId ==
                    capabilities[i].ToTechnologyTypeId &&
                capabilities[i - 1].TechnologyCatalogVersion ==
                    capabilities[i].TechnologyCatalogVersion)
                throw new ArgumentException(
                    "Duplicate commissioning capability.", nameof(expectedCapabilities));

        FacilityId = facilityId;
        ExpectedTile = expectedTile;
        ExpectedCapabilities = Array.AsReadOnly(capabilities);
    }
}

/// <summary>
/// Read-only, deterministically ordered structural preflight, NOT authority
/// to build or operate any facility. Construction costs and required items
/// must be independently defined, checked and settled before authorization.
/// </summary>
public sealed class EconomicFacilityCommissioningPreflightResult
{
    public ulong TurnOrdinal { get; }
    public ulong WorldRevision { get; }
    public WorldStateHash WorldHash { get; }
    public IReadOnlyList<ulong> StructurallyValidatedFacilityIds { get; }

    // Intentionally no mutable candidate world, event, executed flag or
    // Authorize/Apply/Commit method. In particular, this does not confer
    // permission to flip TransportFacilityRuntimeRecord.IsOperational.
    internal EconomicFacilityCommissioningPreflightResult(
        ulong turnOrdinal, ulong revision, WorldStateHash hash,
        IReadOnlyList<ulong> facilityIds)
    {
        TurnOrdinal = turnOrdinal;
        WorldRevision = revision;
        WorldHash = hash;
        StructurallyValidatedFacilityIds =
            Array.AsReadOnly(facilityIds.ToArray());
    }
}

/// <summary>
/// Structural half of the future facility-completion protocol. Rejects
/// stale or foreign world identities, duplicate/unknown/already operational
/// installations, foreign fine topology, and any requested location or
/// capability change. A second, NOT YET IMPLEMENTED authoritative gate
/// must verify construction inputs/finance and produce replayable events.
/// </summary>
public sealed class EconomicFacilityCommissioningPreflight
{
    private readonly CanonicalWorldStateHasher _hasher = new();

    public EconomicFacilityCommissioningPreflightResult Inspect(
        ulong turnOrdinal,
        WorldState world,
        WorldStateHash expectedWorldHash,
        StrategicSurfaceGraph fineGeography,
        IEnumerable<EconomicFacilityCommissioningIntent> intents)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(fineGeography);
        ArgumentNullException.ThrowIfNull(intents);
        if (!world.IsWorldBound)
            throw new InvalidOperationException(
                "Commissioning requires a bound, authoritative world identity.");

        var currentHash = _hasher.Compute(world);
        if (currentHash != expectedWorldHash)
            throw new InvalidOperationException(
                "Commissioning was planned against a stale or foreign world snapshot.");

        var ordered = intents.ToArray();
        if (ordered.Length == 0 || ordered.Any(x => x is null))
            throw new ArgumentException(
                "Structural preflight requires at least one nonnull proposal.",
                nameof(intents));
        ordered = ordered.OrderBy(x => x.FacilityId).ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i - 1].FacilityId == ordered[i].FacilityId)
                throw new ArgumentException(
                    "A facility may be commissioned only once in a candidate batch.",
                    nameof(intents));

        var topology = fineGeography.StrategicTopology;
        var resolution = topology.Parameters;
        foreach (var intent in ordered)
        {
            if (!world.TransportFacilities.TryFind(
                    intent.FacilityId, out var existing) ||
                existing is null)
                throw new InvalidOperationException(
                    "A commissioning proposal cannot create an unregistered facility.");
            if (existing.IsOperational)
                throw new InvalidOperationException(
                    "An already operational facility cannot be completed again.");
            if (intent.ExpectedTile != existing.Tile ||
                intent.ExpectedTile.FineGoldbergParameters != resolution)
                throw new InvalidOperationException(
                    "Commissioning cannot move a facility or use a foreign fine resolution.");

            // A qualified tile identity alone is not a graph membership
            // proof; verify the indexed actual topology. The graph is still
            // not a cryptographic derivation of the bound WORLD signature.
            try
            {
                fineGeography.GetNodeIndex(intent.ExpectedTile.FineStrategicCellId);
            }
            catch (KeyNotFoundException e)
            {
                throw new InvalidOperationException(
                    "The proposed tile is not in the supplied fine topology.", e);
            }

            var recorded = existing.Transfers;
            var demanded = intent.ExpectedCapabilities;
            if (recorded.Count != demanded.Count ||
                !recorded.Zip(demanded).All(pair =>
                    pair.First.FromTechnologyTypeId ==
                        pair.Second.FromTechnologyTypeId &&
                    pair.First.ToTechnologyTypeId ==
                        pair.Second.ToTechnologyTypeId &&
                    pair.First.TechnologyCatalogVersion ==
                        pair.Second.TechnologyCatalogVersion))
                throw new InvalidOperationException(
                    "Commissioning cannot add, omit or alter recorded transfer capabilities.");
        }

        return new EconomicFacilityCommissioningPreflightResult(
            turnOrdinal, world.Revision, currentHash,
            ordered.Select(x => x.FacilityId).ToArray());
    }
}
