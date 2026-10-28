using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Pure read-only projection of explicitly recorded WorldState facilities
/// into the caller-attested D2 handoff snapshot. It binds the evidence to
/// an expected CANONICAL WORLD HASH and rejects any missing, inactive,
/// mislocated or unauthorized facility.
///
/// This verifies membership of a facility in the world snapshot, not the
/// legality of the construction event that created it. WorldState currently
/// has no construction-command lifecycle; that remains an independent gate.
/// The cost-bearing resource ID is an economic binding and is NOT a new
/// commodity, product, facility type or physical construction proof.
/// </summary>
public sealed class EconomicWorldFacilityAuthorizationProjector
{
    private readonly CanonicalWorldStateHasher _hasher = new();

    public EconomicFineTransferFacilityAuthorizationSnapshot Project(
        ulong turnOrdinal, WorldState world,
        WorldStateHash expectedWorldHash,
        IEnumerable<EconomicFineTransferFacilityEvidence> economicBindings)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(economicBindings);
        if (!world.IsWorldBound)
            throw new InvalidOperationException("No world-bound facility registry is available.");
        var actualHash = _hasher.Compute(world);
        if (expectedWorldHash != actualHash)
            throw new InvalidOperationException(
                "Facility authority references a stale or different world state.");

        var bindings = economicBindings.ToArray();
        if (bindings.Any(x => x is null))
            throw new ArgumentException("Null facility binding.", nameof(economicBindings));
        foreach (var binding in bindings)
        {
            if (!world.TransportFacilities.TryFind(binding.FacilityId, out var recorded) ||
                recorded is null ||
                recorded.Tile != binding.Tile ||
                !recorded.IsOperational ||
                !binding.IsOperational ||
                !recorded.CanTransfer(binding.FromTechnologyTypeId,
                    binding.ToTechnologyTypeId, binding.TechnologyCatalogVersion))
                throw new InvalidOperationException(
                    "Economic transfer is not supported by the recorded world facility and directed capability.");
        }

        // D2 independently checks a resource/facility mapping, direction,
        // operational status and fine geography when resolving a service.
        return new EconomicFineTransferFacilityAuthorizationSnapshot(
            turnOrdinal, bindings);
    }
}
