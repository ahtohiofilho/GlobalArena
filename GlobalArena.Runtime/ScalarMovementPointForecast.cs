namespace GlobalArena.Runtime;

/// <summary>
/// Read-only completion of one edge under an integer movement budget.
/// Index is one-based; multiple edges may complete within one movement turn.
/// No combat readiness, modal or terrain behavior is inferred here.
/// </summary>
public sealed record ScalarMovementLegForecast(
    int LegNumber,
    ulong CostPoints,
    ulong CumulativeCostPoints,
    ulong CompletionTurn,
    ulong UnusedPointsOnCompletionTurn);

/// <summary>
/// A pure forecast of scalar travel. This is NOT active V13-V17 movement
/// state. Unused points belong only to the arrival turn and cannot be banked
/// over idle turns; consumers must not serialize this as a legacy itinerary.
/// </summary>
public sealed record ScalarMovementRouteForecast(
    ulong DepartureTurn,
    ulong PointsPerTurn,
    ulong TotalCostPoints,
    ulong TotalMovementTurns,
    ulong ArrivalTurn,
    ulong UnusedPointsOnArrivalTurn,
    IReadOnlyList<ScalarMovementLegForecast> Legs);

/// <summary>
/// Prospective integer movement-points policy. Each supplied edge has a
/// positive abstract cost; the service never assumes a terrain penalty,
/// converts map distances to meters, or grants implicit boats/bridges.
/// Passing the entire list means one player command for a multi-edge route.
/// All arithmetic is checked, deterministic and independent of economic ticks.
/// </summary>
public static class ScalarMovementPointForecast
{
    public static ScalarMovementRouteForecast Preview(
        ulong departureTurn, ulong pointsPerTurn,
        IReadOnlyList<ulong> edgeCosts)
    {
        if (pointsPerTurn == 0UL)
            throw new ArgumentOutOfRangeException(nameof(pointsPerTurn));
        ArgumentNullException.ThrowIfNull(edgeCosts);
        if (edgeCosts.Count == 0)
            throw new ArgumentException(
                "A planned route requires at least one physical edge.",
                nameof(edgeCosts));

        var legs = new ScalarMovementLegForecast[edgeCosts.Count];
        ulong cumulative = 0UL;
        for (var i = 0; i < edgeCosts.Count; i++)
        {
            var cost = edgeCosts[i];
            if (cost == 0UL)
                throw new ArgumentOutOfRangeException(
                    nameof(edgeCosts), "A physical edge cannot cost zero movement points.");
            cumulative = checked(cumulative + cost);
            // Avoid cumulative + pointsPerTurn - 1: that overflows on
            // otherwise legitimate inputs close to ulong.MaxValue.
            var elapsed = checked((cumulative - 1UL) / pointsPerTurn + 1UL);
            var completion = checked(departureTurn + elapsed);
            var usedInFinalTurn = cumulative % pointsPerTurn;
            var remaining = usedInFinalTurn == 0UL
                ? 0UL : pointsPerTurn - usedInFinalTurn;
            legs[i] = new ScalarMovementLegForecast(
                checked(i + 1), cost, cumulative, completion, remaining);
        }

        var last = legs[^1];
        return new ScalarMovementRouteForecast(
            departureTurn, pointsPerTurn, cumulative,
            checked(last.CompletionTurn - departureTurn),
            last.CompletionTurn, last.UnusedPointsOnCompletionTurn,
            Array.AsReadOnly(legs));
    }
}
