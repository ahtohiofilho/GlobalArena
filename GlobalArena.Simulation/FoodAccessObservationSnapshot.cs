using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// A complete snapshot of *supplied observations*, not a complete census of
/// access. Omitted point/food pairs mean unknown, never zero accessible food.
/// Each snapshot applies to exactly one turn; stale data must fail closed.
/// </summary>
public sealed class FoodAccessObservationSnapshot
{
    private readonly IReadOnlyList<FoodPointAccessObservation> _observations;

    public TurnNumber Turn { get; }
    public IReadOnlyList<FoodPointAccessObservation> Observations => _observations;

    public FoodAccessObservationSnapshot(
        TurnNumber turn,
        IEnumerable<FoodPointAccessObservation> observations)
    {
        if (turn.Value == 0UL)
            throw new ArgumentException(
                "Access observations need an explicit valid turn.",
                nameof(turn));
        ArgumentNullException.ThrowIfNull(observations);

        var rows = observations.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Food access observations cannot contain null records.",
                nameof(observations));

        rows = rows
            .OrderBy(row => row.EconomicPointId.Value)
            .ThenBy(row => row.CommodityId.Value)
            .ToArray();

        for (var index = 1; index < rows.Length; index++)
        {
            if (rows[index - 1].EconomicPointId == rows[index].EconomicPointId &&
                rows[index - 1].CommodityId == rows[index].CommodityId)
                throw new ArgumentException(
                    "Only one access observation is allowed per point and food per turn; " +
                    "upstream sources must reconcile overlaps before submitting.",
                    nameof(observations));
        }

        Turn = turn;
        _observations = Array.AsReadOnly(rows);
    }
}
