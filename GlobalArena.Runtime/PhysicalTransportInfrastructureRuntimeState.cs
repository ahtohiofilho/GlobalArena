using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Natural, unbuilt connectivity is a physical crossing, not an absent road.
/// An unowned constructed crossing differs from a natural crossing, and only
/// an explicitly owned crossing can collect an external seller toll.
/// </summary>
public enum PhysicalTransportOwnershipKind : uint
{
    NoInfrastructure = 2U,
    UnownedInfrastructure = 3U,
    Owned = 4U
}

/// <summary>
/// ONE immutable canonical fine crossing. Natural base transport cost and
/// reference cargo capacity are explicit, never inferred from a missing edge.
/// Cargo units and toll money are the same fixed-point units as the physical
/// turn-locked freight coordinator. No market price is stored here.
/// </summary>
public sealed record PhysicalTransportEdgeRuntimeRecord(
    PhysicalTacticalEdgeId Edge,
    PhysicalTransportOwnershipKind OwnershipKind,
    CivilizationId? InfrastructureOwner,
    ulong FreePhysicalCostMicroPerWholeCargoUnit,
    ulong CargoMicroAtReferenceLoad,
    ulong ExternalTariffMicroPerWholeCargoUnit,
    ulong? NaturalConstructionCostMoneyMicro = null,
    ulong? CommittedInvestmentMoneyMicro = null);

/// <summary>
/// Complete and immutable, world-seeded physical-transport inventory.
/// Uninitialized is distinct from a traversable natural graph. A fully
/// initialized world MUST contain every canonical fine crossing exactly once;
/// WorldState verifies this against the authoritative incidence topology.
/// No infrastructure construction/upgrade/takeover is allowed through this
/// seed-only schema deployment API; those require future journaled turn events.
/// </summary>
public sealed class PhysicalTransportInfrastructureRuntimeState
{
    public static PhysicalTransportInfrastructureRuntimeState Uninitialized { get; } =
        new(false, Array.Empty<PhysicalTransportEdgeRuntimeRecord>());

    public bool IsInitialized { get; }

    /// <summary>V13 complete monetary building costs, not physical labor.</summary>
    public bool HasMonetaryConstructionCosts { get; }

    /// <summary>V14 complete invested-money ledger after an actual debit.</summary>
    public bool HasCommittedInvestments { get; }

    public IReadOnlyList<PhysicalTransportEdgeRuntimeRecord> Edges { get; }

    private PhysicalTransportInfrastructureRuntimeState(
        bool initialized, PhysicalTransportEdgeRuntimeRecord[] edges)
    {
        IsInitialized = initialized;
        HasMonetaryConstructionCosts = initialized && edges.Length > 0 &&
            edges.All(e => e.NaturalConstructionCostMoneyMicro.HasValue);
        HasCommittedInvestments = initialized && edges.Length > 0 &&
            edges.All(e => e.CommittedInvestmentMoneyMicro.HasValue);
        Edges = Array.AsReadOnly(edges);
    }

    public static PhysicalTransportInfrastructureRuntimeState CreateInitial(
        IEnumerable<PhysicalTransportEdgeRuntimeRecord> crossings)
    {
        ArgumentNullException.ThrowIfNull(crossings);
        var records = crossings.ToArray();
        if (records.Any(r => r?.CommittedInvestmentMoneyMicro is not null))
            throw new ArgumentException(
                "Initial physical transport cannot inject invested cash. " +
                "Only an accepted monetary journal event may create progress.",
                nameof(crossings));
        bool anyMoney = records.Any(r =>
            r?.NaturalConstructionCostMoneyMicro is not null);
        if (anyMoney && records.Any(r =>
            r?.NaturalConstructionCostMoneyMicro is not > 0UL))
            throw new ArgumentException(
                "The complete V13 inventory needs positive monetary " +
                "construction costs on ALL canonical edges.",
                nameof(crossings));
        if (records.Length == 0)
            throw new ArgumentException(
                "An initialized physical world needs explicit natural or built " +
                "transport evidence for every fine crossing.", nameof(crossings));

        var seen = new HashSet<PhysicalTacticalEdgeId>();
        GoldbergParameters? fine = null;
        foreach (var row in records)
        {
            if (row is null || !row.Edge.IsValid ||
                !Enum.IsDefined(row.OwnershipKind) ||
                row.FreePhysicalCostMicroPerWholeCargoUnit == 0UL ||
                row.CargoMicroAtReferenceLoad == 0UL ||
                !seen.Add(row.Edge) ||
                (row.OwnershipKind == PhysicalTransportOwnershipKind.Owned
                    ? row.InfrastructureOwner is not { IsValid: true }
                    : row.InfrastructureOwner.HasValue ||
                      row.ExternalTariffMicroPerWholeCargoUnit != 0UL))
                throw new ArgumentException(
                    "Each physical crossing must be unique, have positive " +
                    "natural freight and capacity, and valid ownership/toll.",
                    nameof(crossings));
            fine ??= row.Edge.FineGoldbergParameters;
            if (row.Edge.FineGoldbergParameters != fine.Value)
                throw new ArgumentException(
                    "Physical transport cannot mix incompatible fine worlds.",
                    nameof(crossings));
        }

        Array.Sort(records, (a, b) =>
            a.Edge.FineStrategicEdgeId.Value.CompareTo(
                b.Edge.FineStrategicEdgeId.Value));
        return new PhysicalTransportInfrastructureRuntimeState(true, records);
    }

    /// <summary>
    /// Apply a verified MONEY debit to exactly one canonical natural edge.
    /// This is capital expenditure, NOT material/commodity consumption.
    /// Title and operational performance remain unchanged until a later
    /// authoritative infrastructure-completion policy is implemented.
    /// </summary>
    public PhysicalTransportInfrastructureRuntimeState RecordMoneyInvestment(
        PhysicalTacticalEdgeId edge, ulong moneyMicro)
    {
        if (!HasMonetaryConstructionCosts || moneyMicro == 0UL || !edge.IsValid)
            throw new InvalidOperationException(
                "Monetary investment requires complete v13 construction quotes.");
        int index = -1;
        for (int i = 0; i < Edges.Count; i++)
            if (Edges[i].Edge == edge)
            {
                index = i;
                break;
            }
        if (index < 0)
            throw new InvalidOperationException("Unknown physical crossing.");
        var record = Edges[index];
        if (record.OwnershipKind != PhysicalTransportOwnershipKind.NoInfrastructure)
            throw new InvalidOperationException(
                "This gate only finances a natural, unbuilt crossing.");
        ulong required = record.NaturalConstructionCostMoneyMicro!.Value;
        ulong prior = record.CommittedInvestmentMoneyMicro ?? 0UL;
        if (prior >= required || moneyMicro > required - prior)
            throw new InvalidOperationException(
                "Monetary investment exceeds the real remaining capital cost.");

        var rows = Edges.Select((r, i) => r with
        {
            CommittedInvestmentMoneyMicro = i == index
                ? checked(prior + moneyMicro)
                : r.CommittedInvestmentMoneyMicro ?? 0UL
        }).ToArray();
        return new PhysicalTransportInfrastructureRuntimeState(true, rows);
    }
}
