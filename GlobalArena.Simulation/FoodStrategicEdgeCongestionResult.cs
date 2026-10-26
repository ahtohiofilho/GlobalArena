namespace GlobalArena.Simulation;

/// <summary>
/// Canonical, immutable route-load and congestion-cost evaluation.
/// This does not alter existing trades or certify actual cargo delivery.
/// </summary>
public sealed class FoodStrategicEdgeCongestionResult
{
    public IReadOnlyList<FoodStrategicEdgeCongestionEntry> UsedEdges { get; }

    public FoodStrategicEdgeCongestionResult(
        IEnumerable<FoodStrategicEdgeCongestionEntry> usedEdges)
    {
        ArgumentNullException.ThrowIfNull(usedEdges);
        var rows = usedEdges.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Congestion results cannot contain null entries.",
                nameof(usedEdges));

        rows = rows.OrderBy(row => row.StrategicEdgeId.Value).ToArray();
        for (var index = 1; index < rows.Length; index++)
        {
            if (rows[index - 1].StrategicEdgeId == rows[index].StrategicEdgeId)
                throw new ArgumentException(
                    "Congestion results cannot contain duplicate strategic edges.",
                    nameof(usedEdges));
        }

        UsedEdges = Array.AsReadOnly(rows);
    }
}
