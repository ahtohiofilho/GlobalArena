using System.Numerics;

namespace GlobalArena.Economy;

/// <summary>
/// Classification of a claimed destination for ONE producer receipt.
/// A transfer or intermediate input is not additional final production.
/// </summary>
public enum PhysicalOutputDispositionRole : byte
{
    FinalLocalUse = 1,
    FinalRemoteUse = 2,
    IntermediateInput = 3,
    ExplicitRetention = 4
}

/// <summary>
/// Candidate production, NOT an authenticated canonical execution event.
/// </summary>
public readonly record struct PhysicalProductionClaim(
    ulong ReceiptId, ulong EconomicCycle, ulong SourcePointId,
    uint Commodity, ulong ProducedRaw);

/// <summary>
/// One claimed classified disposition backed by a production receipt.
/// </summary>
public readonly record struct PhysicalDispositionClaim(
    ulong ReceiptId, ulong EconomicCycle, ulong ProductionReceiptId,
    ulong DestinationPointId, uint Commodity, ulong QuantityRaw,
    PhysicalOutputDispositionRole Role);

/// <summary>Explicit raw-to-physical unit conversion per commodity.</summary>
public readonly record struct PhysicalCommodityUnitScale(
    uint Commodity, ulong RawUnitsPerPhysicalUnit);

public readonly record struct PhysicalFinalOutputPreflightRow(
    uint Commodity, BigInteger FinalLocalRaw, BigInteger FinalRemoteRaw,
    BigInteger IntermediateRaw, BigInteger RetainedRaw,
    decimal CandidateFinalQuantity);

/// <summary>
/// A closed physical conservation preflight, NOT an authenticated snapshot.
/// It cannot authorize RealCompletedCycleObservation, N or prices without
/// a later, independent, canonical event-journal witness.
/// </summary>
public sealed class PhysicalFinalOutputPreflight
{
    public ulong ClaimedCompletedCycle { get; }
    public ulong ClaimedPopulation { get; }
    public IReadOnlyList<PhysicalFinalOutputPreflightRow> Commodities { get; }
    public IReadOnlyList<RealFinalOutputRow> CandidateFinalOutput { get; }
    public bool HasCanonicalLedgerAttestation => false;
    public bool AuthenticatesBaseUnitPrices => false;
    public bool CanAuthorizePurchasingPower => false;
    public bool ChangesPhysicalState => false;

    internal PhysicalFinalOutputPreflight(
        ulong claimedCompletedCycle, ulong claimedPopulation,
        PhysicalFinalOutputPreflightRow[] commodityRows)
    {
        ClaimedCompletedCycle = claimedCompletedCycle;
        ClaimedPopulation = claimedPopulation;
        Commodities = Array.AsReadOnly((PhysicalFinalOutputPreflightRow[])commodityRows.Clone());
        CandidateFinalOutput = Array.AsReadOnly(commodityRows
            .Select(x => new RealFinalOutputRow(x.Commodity, x.CandidateFinalQuantity))
            .ToArray());
    }
}

/// <summary>
/// ECON-REBASE-03: deterministic conservation at the candidate final-output
/// boundary. The current gameplay has no authenticated multi-commodity
/// final-destination journal; these claims MUST NOT be promoted to N.
/// </summary>
public static class PhysicalFinalOutputEvidenceAdapter
{
    public static PhysicalFinalOutputPreflight ReconcileClaims(
        ulong claimedCompletedCycle, ulong claimedPopulation,
        IEnumerable<PhysicalProductionClaim> productions,
        IEnumerable<PhysicalDispositionClaim> dispositions,
        IEnumerable<PhysicalCommodityUnitScale> scales)
    {
        ArgumentNullException.ThrowIfNull(productions);
        ArgumentNullException.ThrowIfNull(dispositions);
        ArgumentNullException.ThrowIfNull(scales);
        if (claimedCompletedCycle == 0UL)
            throw new ArgumentOutOfRangeException(nameof(claimedCompletedCycle));
        if (claimedPopulation == 0UL)
            throw new ArgumentOutOfRangeException(nameof(claimedPopulation));

        var scalesByCommodity = new Dictionary<uint, ulong>();
        foreach (var scale in scales)
        {
            if (scale.Commodity == 0U || scale.RawUnitsPerPhysicalUnit == 0UL)
                throw new ArgumentOutOfRangeException(nameof(scales),
                    "Explicit positive commodity keys and raw-unit scales are required.");
            if (!scalesByCommodity.TryAdd(scale.Commodity, scale.RawUnitsPerPhysicalUnit))
                throw new ArgumentException("Duplicate commodity unit scale.", nameof(scales));
        }
        if (scalesByCommodity.Count == 0)
            throw new ArgumentException("The closed commodity universe must be explicit.", nameof(scales));

        var origins = new Dictionary<ulong, PhysicalProductionClaim>();
        foreach (var origin in productions)
        {
            if (origin.ReceiptId == 0UL || origin.SourcePointId == 0UL ||
                origin.Commodity == 0U || origin.ProducedRaw == 0UL ||
                origin.EconomicCycle != claimedCompletedCycle ||
                !scalesByCommodity.ContainsKey(origin.Commodity))
                throw new ArgumentException("Invalid, foreign-cycle or unscaled producer claim.",
                    nameof(productions));
            if (!origins.TryAdd(origin.ReceiptId, origin))
                throw new ArgumentException("Duplicate producer receipt identity.",
                    nameof(productions));
        }

        var seenDispositionIds = new HashSet<ulong>();
        var totalsByOrigin = origins.Keys.ToDictionary(id => id, _ => BigInteger.Zero);
        var sums = scalesByCommodity.Keys.ToDictionary(
            commodity => commodity, _ => new BigInteger[4]);

        foreach (var item in dispositions)
        {
            if (item.ReceiptId == 0UL || item.QuantityRaw == 0UL ||
                item.DestinationPointId == 0UL ||
                item.EconomicCycle != claimedCompletedCycle ||
                !origins.TryGetValue(item.ProductionReceiptId, out var origin) ||
                origin.Commodity != item.Commodity ||
                item.Role is < PhysicalOutputDispositionRole.FinalLocalUse or
                    > PhysicalOutputDispositionRole.ExplicitRetention)
                throw new ArgumentException("Invalid, unbacked or foreign-cycle disposition.",
                    nameof(dispositions));
            if (!seenDispositionIds.Add(item.ReceiptId))
                throw new ArgumentException("Duplicate disposition receipt identity.",
                    nameof(dispositions));
            if ((item.Role is PhysicalOutputDispositionRole.FinalLocalUse or
                 PhysicalOutputDispositionRole.ExplicitRetention) &&
                item.DestinationPointId != origin.SourcePointId ||
                item.Role == PhysicalOutputDispositionRole.FinalRemoteUse &&
                item.DestinationPointId == origin.SourcePointId)
                throw new ArgumentException("Final local/remote or retained destination contradicts source point.",
                    nameof(dispositions));

            var next = totalsByOrigin[item.ProductionReceiptId] + item.QuantityRaw;
            if (next > origin.ProducedRaw)
                throw new InvalidOperationException(
                    "The same origin cannot distribute more goods than it produced.");
            totalsByOrigin[item.ProductionReceiptId] = next;
            sums[item.Commodity][(int)item.Role - 1] += item.QuantityRaw;
        }

        foreach (var origin in origins.Values)
            if (totalsByOrigin[origin.ReceiptId] != origin.ProducedRaw)
                throw new InvalidOperationException(
                    "Every produced unit requires one classified physical destination; no invisible inventory.");

        var rows = scalesByCommodity.OrderBy(x => x.Key).Select(kv =>
        {
            var values = sums[kv.Key];
            // Intermediate inputs and retained goods are excluded.
            var finalRaw = values[0] + values[1];
            var finalQuantity = checked((decimal)finalRaw / kv.Value);
            return new PhysicalFinalOutputPreflightRow(kv.Key,
                values[0], values[1], values[2], values[3], finalQuantity);
        }).ToArray();
        return new PhysicalFinalOutputPreflight(
            claimedCompletedCycle, claimedPopulation, rows);
    }
}
