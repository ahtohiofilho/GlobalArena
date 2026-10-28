using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit uncalibrated physical scale / ecological rates for the G3-F
/// reference. Never a global cap on workers or commercial traffic.
/// </summary>
public sealed class EconomicNaturalCapitalPolicy
{
    public ulong RawWoodAtFullEndowment { get; }
    public ulong RawStoneAtFullEndowment { get; }
    public long RawWoodRecoveryFraction { get; }
    public long RawSoilRecoveryFraction { get; }
    public long RawSoilWearPerFoodUnit { get; }

    public EconomicNaturalCapitalPolicy(
        ulong rawWoodAtFullEndowment, ulong rawStoneAtFullEndowment,
        long rawWoodRecoveryFraction, long rawSoilRecoveryFraction,
        long rawSoilWearPerFoodUnit)
    {
        if (rawWoodAtFullEndowment == 0UL || rawStoneAtFullEndowment == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawWoodAtFullEndowment),
                "Resource scales must be positive and explicitly calibrated.");
        ValidateFraction(rawWoodRecoveryFraction, nameof(rawWoodRecoveryFraction));
        ValidateFraction(rawSoilRecoveryFraction, nameof(rawSoilRecoveryFraction));
        ValidateFraction(rawSoilWearPerFoodUnit, nameof(rawSoilWearPerFoodUnit));

        RawWoodAtFullEndowment = rawWoodAtFullEndowment;
        RawStoneAtFullEndowment = rawStoneAtFullEndowment;
        RawWoodRecoveryFraction = rawWoodRecoveryFraction;
        RawSoilRecoveryFraction = rawSoilRecoveryFraction;
        RawSoilWearPerFoodUnit = rawSoilWearPerFoodUnit;
    }

    internal static void ValidateFraction(long fraction, string name)
    {
        if (fraction < 0L || fraction > StrategicScalarField.Denominator)
            throw new ArgumentOutOfRangeException(name,
                "Ecological rate must be a normalized nonnegative fixed-point fraction.");
    }
}

/// <summary>
/// Physical environmental state BY STRATEGIC CELL. Standing trees and loose
/// surface stone are *in situ*, not player-owned commodity inventories.
/// Soil vitality is a normalized portion of local soil suitability.
/// </summary>
public sealed class EconomicNaturalCapitalCellState
{
    public StrategicCellId CellId { get; }
    public ulong RawWoodCarryingCapacity { get; }
    public ulong RawStandingWood { get; }
    public ulong RawSurfaceStone { get; }
    public long RawSoilVitality { get; }

    public EconomicNaturalCapitalCellState(
        StrategicCellId cellId, ulong rawWoodCarryingCapacity,
        ulong rawStandingWood, ulong rawSurfaceStone, long rawSoilVitality)
    {
        if (!cellId.IsValid)
            throw new ArgumentException("Natural capital needs a valid strategic cell.");
        if (rawStandingWood > rawWoodCarryingCapacity)
            throw new ArgumentOutOfRangeException(nameof(rawStandingWood),
                "Standing wood cannot exceed its physical carrying capacity.");
        EconomicNaturalCapitalPolicy.ValidateFraction(rawSoilVitality,
            nameof(rawSoilVitality));

        CellId = cellId;
        RawWoodCarryingCapacity = rawWoodCarryingCapacity;
        RawStandingWood = rawStandingWood;
        RawSurfaceStone = rawSurfaceStone;
        RawSoilVitality = rawSoilVitality;
    }
}

/// <summary>
/// Immutable candidate ecological snapshot, not integrated into WorldState,
/// canonical hash, save files or an authoritative turn scheduler.
/// </summary>
public sealed class EconomicNaturalCapitalBook
{
    public ulong LastAppliedTurn { get; }
    public uint DerivationVersion { get; }
    public IReadOnlyList<EconomicNaturalCapitalCellState> Cells { get; }

    public EconomicNaturalCapitalBook(
        ulong lastAppliedTurn, uint derivationVersion,
        IEnumerable<EconomicNaturalCapitalCellState> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (derivationVersion != StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion)
            throw new ArgumentOutOfRangeException(nameof(derivationVersion));

        var ordered = cells.ToArray();
        if (ordered.Any(x => x is null))
            throw new ArgumentException("Natural capital cells cannot be null.");
        ordered = ordered.OrderBy(x => x.CellId.Value).ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i - 1].CellId == ordered[i].CellId)
                throw new ArgumentException("Duplicate natural-capital cell.", nameof(cells));

        LastAppliedTurn = lastAppliedTurn;
        DerivationVersion = derivationVersion;
        Cells = Array.AsReadOnly(ordered);
    }

    public EconomicNaturalCapitalCellState At(StrategicCellId cell) =>
        Cells.First(x => x.CellId == cell);
}

/// <summary>
/// Deterministic initialization from G3-E v2, using caller-supplied physical
/// capacity scales. No extra resource is created when a cell is already used:
/// initialization is only valid as an explicit first snapshot.
/// </summary>
public static class EconomicNaturalCapitalInitializer
{
    public static EconomicNaturalCapitalBook Initialize(
        WorldGenerationResult generatedWorld, EconomicNaturalCapitalPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(policy);
        var e = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generatedWorld);
        const ulong d = (ulong)StrategicScalarField.Denominator;

        var rows = generatedWorld.StrategicTopology.Cells.Select(cell =>
        {
            var wood = checked((ulong)(
                ((UInt128)policy.RawWoodAtFullEndowment *
                    (ulong)e.WoodyVegetation.GetRawValue(cell.Id)) / d));
            var stone = checked((ulong)(
                ((UInt128)policy.RawStoneAtFullEndowment *
                    (ulong)e.ExposedStone.GetRawValue(cell.Id)) / d));
            return new EconomicNaturalCapitalCellState(
                cell.Id, wood, wood, stone, e.SoilSuitability.GetRawValue(cell.Id));
        });

        return new EconomicNaturalCapitalBook(0UL,
            StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion, rows);
    }
}
