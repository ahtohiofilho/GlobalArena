using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-declared local accessible quantity for one food in one economic
/// cycle, with an explicit provenance key. This is a supplied observation,
/// not evidence verified by the simulation, not proof of ingestion, and is
/// never silently derived from a market trade allocation.
/// </summary>
public sealed class FoodPointAccessObservation
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawAccessibleQuantity { get; }
    public string EvidenceSourceKey { get; }

    public FoodPointAccessObservation(
        EconomicPointId economicPointId,
        CommodityId commodityId,
        ulong rawAccessibleQuantity,
        string evidenceSourceKey)
    {
        if (!economicPointId.IsValid)
            throw new ArgumentException(
                "Food access observation requires a valid point ID.",
                nameof(economicPointId));
        if (!commodityId.IsValid)
            throw new ArgumentException(
                "Food access observation requires a valid commodity ID.",
                nameof(commodityId));
        ArgumentNullException.ThrowIfNull(evidenceSourceKey);
        if (string.IsNullOrWhiteSpace(evidenceSourceKey) ||
            evidenceSourceKey.Any(c =>
                !(c is >= 'a' and <= 'z') &&
                !(c is >= '0' and <= '9') &&
                c != '-' && c != '.'))
            throw new ArgumentException(
                "Evidence source key must use nonempty lowercase ASCII letters, digits, hyphens or dots.",
                nameof(evidenceSourceKey));

        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawAccessibleQuantity = rawAccessibleQuantity;
        EvidenceSourceKey = evidenceSourceKey;
    }
}
