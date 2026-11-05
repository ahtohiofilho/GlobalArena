using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Canonical MVP: one primitive shelter/worksite frame using a previously
/// executed and physically debited wood/stone assembly. No fixed conversion
/// recipe, extra production, cash, city or ownership is created here.
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
    ulong CompletedMovementTurn)
{
    public bool IsOperational => CompletedMovementTurn != 0UL;
    public ulong ReservedWorkers => IsOperational ? 0UL : CommittedWorkers;
    public bool AuthorizesProductivityBonus => false;
    public bool AuthorizesNewTradeableGood => false;
    public bool CanAuthorizeRealPurchasingPower => false;
}
