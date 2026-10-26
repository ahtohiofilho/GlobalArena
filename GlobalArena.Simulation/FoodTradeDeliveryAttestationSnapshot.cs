using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// One-cycle canonical snapshot of caller-provided market delivery declarations.
/// A missing declaration leaves delivery *unknown*, not zero. One attestation
/// per market allocation key; overlapping sources require upstream accounting.
/// </summary>
public sealed class FoodTradeDeliveryAttestationSnapshot
{
    public TurnNumber Turn { get; }
    public IReadOnlyList<FoodTradeDeliveryAttestation> Attestations { get; }

    public FoodTradeDeliveryAttestationSnapshot(
        TurnNumber turn,
        IEnumerable<FoodTradeDeliveryAttestation> attestations)
    {
        if (turn.Value == 0UL)
            throw new ArgumentException(
                "Delivery declarations require a valid economic turn.",
                nameof(turn));
        ArgumentNullException.ThrowIfNull(attestations);

        var rows = attestations.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Delivery declarations cannot contain null rows.",
                nameof(attestations));

        rows = rows
            .OrderBy(row => row.ReferencedAllocation.CommodityId.Value)
            .ThenBy(row => row.ReferencedAllocation.SourceEconomicPointId.Value)
            .ThenBy(row => (byte)row.ReferencedAllocation.SourceActivity)
            .ThenBy(row => row.ReferencedAllocation.DestinationEconomicPointId.Value)
            .ThenByDescending(row => row.ReferencedAllocation.RawUnitValue)
            .ThenBy(row => row.ReferencedAllocation.RawUnitTransferCost)
            .ToArray();

        var keys = new HashSet<FoodTradeAllocationIdentity>();
        foreach (var row in rows)
        {
            if (!keys.Add(FoodTradeAllocationIdentity.From(row.ReferencedAllocation)))
                throw new ArgumentException(
                    "Multiple delivery attestations for one market allocation must be reconciled upstream.",
                    nameof(attestations));
        }

        Turn = turn;
        Attestations = Array.AsReadOnly(rows);
    }
}
