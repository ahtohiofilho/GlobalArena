using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// G3-B2 isolated reference: debits a candidate material book only after
/// ALL declared construction requirements are affordable. No asset or world
/// command is created, and this MUST NOT be used as an authoritative build.
/// </summary>
public sealed class EconomicConstructionMaterialDebitCandidateResolver
{
    private readonly EconomicConstructionMaterialQuoteResolver _quotes = new();

    public EconomicMaterialReserveBook Debit(
        WorldState world, CivilizationId owner,
        EconomicMaterialReserveBook original,
        IEnumerable<EconomicConstructionMaterialRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(requirements);

        var requested = requirements.ToArray();
        if (requested.Length == 0)
            throw new ArgumentException(
                "Material debit needs at least one declared requirement.",
                nameof(requirements));

        // Validation of ownership and duplicate keys occurs in the shared
        // quote resolver. No mutation occurs on failure.
        var quote = _quotes.Evaluate(world, owner, original, requested);
        if (!quote.IsMateriallyAffordable)
            throw new InvalidOperationException(
                "Construction material requirements exceed local reserves.");

        var balances = original.Balances.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        foreach (var line in quote.Lines)
        {
            var req = line.Requirement;
            var key = (req.EconomicPointId, req.CommodityId);
            var remaining = balances[key] - req.RawQuantity;
            if (remaining == 0UL)
                balances.Remove(key);
            else
                balances[key] = remaining;
        }

        return new EconomicMaterialReserveBook(
            balances.Select(x => new EconomicMaterialReserveBalance(
                x.Key.EconomicPointId, x.Key.CommodityId, x.Value)),
            original.AppliedTurnCursors);
    }
}
