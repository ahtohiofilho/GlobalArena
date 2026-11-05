using System.Text.Json.Serialization;
using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Legacy laboratory-only fixture, introduced to verify physical material
/// incorporation, dedicated labor, elapsed economic time and hashed save
/// replay. WorkShelter was NOT approved as a real production building.
///
/// IMPORTANT: keep enum values and serialized record fields unchanged for
/// historic v27-v30 saves. Future approved facility types must be defined
/// by their real function/materials, not by this generic test artifact.
/// </summary>
public enum PhysicalPrimitiveFacilityKind : byte
{
    WorkShelter = 1
}

/// <summary>
/// Immutable, hash-domain-owned physical installation. While incomplete,
/// CommittedWorkers are RESERVED from an existing productive activity and
/// cannot be used for production or withdrawn. One later economic settlement
/// completes the basic site. The reservation ends on completion.
/// </summary>
public sealed record PhysicalPrimitiveFacility(
    int AssemblyActionOrdinal,
    PhysicalTacticalTileId Tile,
    EconomicActivityKind Activity,
    PhysicalPrimitiveFacilityKind Kind,
    ulong CommittedWorkers,
    ulong StartedMovementTurn,
    ulong CompletedMovementTurn,
    ulong OperatingWorkers = 0UL)
{
    /// <summary>
    /// Product boundary: this archived, hash-authenticated lab fixture is
    /// not an authorized player-facing economic building. Derived property
    /// only: no new fields in the canonical save or world hash.
    /// </summary>
    [JsonIgnore]
    public bool IsApprovedGameplayFacility => false;

    public bool IsOperational => CompletedMovementTurn != 0UL;
    public ulong ReservedWorkers => IsOperational ? 0UL : CommittedWorkers;
    /// <summary>
    /// Opt-in operators drawn from the EXISTING tile/job allocation.
    /// OperatingWorkers is not an extra population or a new yield multiplier.
    /// </summary>
    public bool HasAssistedWorkOperators => IsOperational && OperatingWorkers > 0UL;
    public bool AuthorizesProductivityBonus => false;
    public bool AuthorizesNewTradeableGood => false;
    public bool CanAuthorizeRealPurchasingPower => false;
}
