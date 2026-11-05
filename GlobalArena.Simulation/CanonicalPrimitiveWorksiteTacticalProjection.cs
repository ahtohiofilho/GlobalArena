using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Visual lifecycle of a physical Neolithic worksite; this is a read-only
/// projection of canonical state, executed material assemblies and fresh
/// production evidence. It does not infer hypothetical bonuses or N.
/// </summary>
public enum PrimitiveWorksiteStage : byte
{
    NeedsPhysicalMaterials = 0,
    MaterialsAvailable = 1,
    AssemblyReady = 2,
    UnderConstruction = 3,
    BuiltUnstaffed = 4,
    Operating = 5
}

public sealed record CanonicalPrimitiveWorksiteTileView(
    PhysicalTacticalTileId Tile,
    EconomicActivityKind Activity,
    PrimitiveWorksiteStage Stage,
    ulong ResidentWorkers,
    ulong ActivityWorkers,
    ulong LocalFoodRaw,
    ulong LocalWoodRaw,
    ulong LocalStoneRaw,
    int AvailableAssemblyOrdinal,
    int FacilityAssemblyOrdinal,
    ulong ConstructionWorkers,
    ulong OperatingWorkers,
    ulong StartedTurn,
    ulong CompletedTurn,
    int TotalWorldFacilities,
    int TotalBuildingFacilities,
    int TotalOperatingFacilities,
    ulong CurrentMovementTurn,
    ulong CompletedEconomicCycles,
    ulong LastCycleMovementTurn,
    ulong LastCycleGlobalProductionRaw,
    bool HasFreshAssistedOutput,
    ulong LastAssistedProducedRaw)
{
    public bool HasWorldFacility => FacilityAssemblyOrdinal > 0;
    public bool MayStartConstruction =>
        !HasWorldFacility && AvailableAssemblyOrdinal > 0 && ActivityWorkers > 0;
    public bool MayOperate => Stage == PrimitiveWorksiteStage.BuiltUnstaffed;
    public bool MaySuspend => Stage == PrimitiveWorksiteStage.Operating;
    public bool CreatesAdditionalProduction => false;
    public bool CanAuthorizeRealPurchasingPower => false;
}

public static class CanonicalPrimitiveWorksiteTacticalProjection
{
    public static CanonicalPrimitiveWorksiteTileView Read(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId tile, EconomicActivityKind activity)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (activity is not (EconomicActivityKind.Agriculture or
            EconomicActivityKind.Forestry or EconomicActivityKind.Mining))
            throw new ArgumentOutOfRangeException(nameof(activity));
        var state = session.State;
        if (tile.FineGoldbergParameters != state.FineParameters ||
            !tile.IsValid || !session.IsPhysicalWorkableLand(tile.FineStrategicCellId))
            throw new ArgumentException("The worksite tile is invalid or non-workable.", nameof(tile));

        var before = session.CurrentHash;
        var site = state.PrimitiveFacilities.FirstOrDefault(x =>
            x.Tile == tile && x.Activity == activity);
        var readyFrame = session.ExecutedPrimitiveAssemblies
            .Where(x => x.FineTile == tile.FineStrategicCellId.Value &&
                        state.PrimitiveFacilities.All(f =>
                            f.AssemblyActionOrdinal != x.ActionOrdinal))
            .OrderBy(x => x.ActionOrdinal)
            .FirstOrDefault();

        var wood = session.StockAt(tile, new CommodityId(3U));
        var stone = session.StockAt(tile, new CommodityId(2U));
        var food = session.StockAt(tile, new CommodityId(1U));
        var workers = state.Allocations.Where(x =>
            x.TileId == tile && x.Activity == activity)
            .Aggregate(0UL, (sum, x) => checked(sum + x.Workers));
        var stage = site is not null
            ? !site.IsOperational
                ? PrimitiveWorksiteStage.UnderConstruction
                : site.OperatingWorkers == 0UL
                    ? PrimitiveWorksiteStage.BuiltUnstaffed
                    : PrimitiveWorksiteStage.Operating
            : readyFrame is not null
                ? PrimitiveWorksiteStage.AssemblyReady
                : wood > 0UL && stone > 0UL
                    ? PrimitiveWorksiteStage.MaterialsAvailable
                    : PrimitiveWorksiteStage.NeedsPhysicalMaterials;

        ulong assisted = 0UL;
        bool fresh = false;
        if (state.Cycles.Count > 0 &&
            state.MovementTurn == state.Cycles[^1].MovementTurn)
        {
            try
            {
                var witness = session.CaptureLatestFacilityAssistedWorkWitness();
                assisted = witness.Assisted.Where(x =>
                    x.FineTile == tile.FineStrategicCellId.Value &&
                    x.Activity == activity).Aggregate(0UL,
                    (sum, x) => checked(sum + x.AssistedProducedRaw));
                fresh = true;
            }
            catch (InvalidOperationException)
            {
                // A canonical staffing or other action after the closing
                // tick invalidates that exact receipt. Never label stale
                // previous-cycle data as live production.
            }
        }

        var lastCycle = state.Cycles.LastOrDefault();
        var view = new CanonicalPrimitiveWorksiteTileView(
            tile, activity, stage, session.WorkersAt(tile), workers,
            food, wood, stone, readyFrame?.ActionOrdinal ?? 0,
            site?.AssemblyActionOrdinal ?? 0,
            site?.ReservedWorkers ?? 0UL, site?.OperatingWorkers ?? 0UL,
            site?.StartedMovementTurn ?? 0UL,
            site?.CompletedMovementTurn ?? 0UL,
            state.PrimitiveFacilities.Count,
            state.PrimitiveFacilities.Count(x => !x.IsOperational),
            state.PrimitiveFacilities.Count(x =>
                x.IsOperational && x.OperatingWorkers > 0UL),
            state.MovementTurn, checked((ulong)state.Cycles.Count),
            lastCycle?.MovementTurn ?? 0UL,
            lastCycle?.ProducedRaw ?? 0UL, fresh, assisted);

        if (!before.Equals(session.CurrentHash))
            throw new InvalidOperationException(
                "Tactical worksite inspection unexpectedly changed the authoritative world.");
        return view;
    }
}
