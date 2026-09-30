namespace GlobalArena.World;

public static class StrategicTacticalFieldAggregator
{
    public static StrategicTacticalAggregate Aggregate(
        BoundedTacticalScalarPatch patch)
    {
        ArgumentNullException.ThrowIfNull(
            patch);

        Int128 weightedTotal =
            0;

        long totalWeight =
            0;

        foreach (var sample
            in patch.Samples)
        {
            weightedTotal +=
                (Int128)sample.RawValue
                * sample.AggregationWeight;

            totalWeight =
                checked(
                    totalWeight
                    + sample.AggregationWeight);
        }

        if (totalWeight <= 0)
        {
            throw new InvalidOperationException(
                "Bounded tactical patch must have positive aggregation weight.");
        }

        var aggregate =
            checked(
                (long)(
                    weightedTotal
                    / totalWeight));

        return new StrategicTacticalAggregate(
            patch.TargetStrategicCellId,
            aggregate);
    }
}
