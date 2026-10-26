namespace GlobalArena.Simulation;

/// <summary>
/// Immutable, deterministically ordered capacity observations for edges
/// traversed by food market allocations. A breach is diagnostic, not a
/// corrected market allocation or proof that goods moved physically.
/// </summary>
public sealed class FoodStrategicEdgeCapacityAudit
{
    public IReadOnlyList<FoodStrategicEdgeMassLoad> UsedEdges { get; }
    public bool AllUsedEdgesWithinCapacity { get; }
    public int OverloadedEdgeCount { get; }

    public FoodStrategicEdgeCapacityAudit(
        IEnumerable<FoodStrategicEdgeMassLoad> usedEdges)
    {
        ArgumentNullException.ThrowIfNull(usedEdges);
        var rows = usedEdges.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Transport capacity audit cannot contain null rows.",
                nameof(usedEdges));

        rows = rows.OrderBy(row => row.StrategicEdgeId.Value).ToArray();
        for (var i = 1; i < rows.Length; i++)
        {
            if (rows[i - 1].StrategicEdgeId == rows[i].StrategicEdgeId)
                throw new ArgumentException(
                    "Transport audit cannot contain duplicate strategic edges.",
                    nameof(usedEdges));
        }

        UsedEdges = Array.AsReadOnly(rows);
        OverloadedEdgeCount = rows.Count(row => row.IsOverCapacity);
        AllUsedEdgesWithinCapacity = OverloadedEdgeCount == 0;
    }
}
