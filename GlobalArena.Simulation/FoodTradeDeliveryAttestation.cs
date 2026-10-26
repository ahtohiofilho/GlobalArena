namespace GlobalArena.Simulation;

/// <summary>
/// Caller-provided declaration that some quantity associated with a specific
/// market allocation was delivered within the stated economic turn.
/// The engine checks the declared quantity against the market allocation but
/// does NOT authenticate the external evidence or observe actual transport.
/// </summary>
public sealed class FoodTradeDeliveryAttestation
{
    public EconomicTradeAllocation ReferencedAllocation { get; }
    public ulong RawAttestedDeliveredQuantity { get; }
    public string EvidenceSourceKey { get; }

    public FoodTradeDeliveryAttestation(
        EconomicTradeAllocation referencedAllocation,
        ulong rawAttestedDeliveredQuantity,
        string evidenceSourceKey)
    {
        ArgumentNullException.ThrowIfNull(referencedAllocation);
        ArgumentNullException.ThrowIfNull(evidenceSourceKey);

        if (rawAttestedDeliveredQuantity > referencedAllocation.RawQuantity)
            throw new ArgumentOutOfRangeException(
                nameof(rawAttestedDeliveredQuantity),
                "A reported delivery cannot exceed the referenced allocation.");

        if (string.IsNullOrWhiteSpace(evidenceSourceKey) ||
            evidenceSourceKey.Any(c =>
                !(c is >= 'a' and <= 'z') &&
                !(c is >= '0' and <= '9') &&
                c != '-' && c != '.'))
            throw new ArgumentException(
                "Evidence source keys must use lowercase ASCII letters, digits, hyphens or dots.",
                nameof(evidenceSourceKey));

        ReferencedAllocation = referencedAllocation;
        RawAttestedDeliveredQuantity = rawAttestedDeliveredQuantity;
        EvidenceSourceKey = evidenceSourceKey;
    }
}
