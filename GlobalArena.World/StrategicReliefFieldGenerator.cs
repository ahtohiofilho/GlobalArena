namespace GlobalArena.World;

public static class StrategicReliefFieldGenerator
{
    public static StrategicScalarField Generate(
        StrategicScalarField elevation)
    {
        ArgumentNullException.ThrowIfNull(
            elevation);

        var graph =
            elevation.SurfaceGraph;

        var values =
            new long[
                graph.NodeCount];

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            var elevationRaw =
                elevation.GetRawValue(
                    index);

            var maximumDelta =
                0L;

            foreach (var neighborIndex
                in graph.GetNeighborIndexes(
                    index))
            {
                var delta =
                    checked(
                        elevation.GetRawValue(
                            neighborIndex)
                        - elevationRaw);

                if (delta == long.MinValue)
                {
                    throw new OverflowException(
                        "Relief delta magnitude exceeds Int64.");
                }

                var magnitude =
                    Math.Abs(
                        delta);

                if (magnitude
                    > maximumDelta)
                {
                    maximumDelta =
                        magnitude;
                }
            }

            values[index] =
                maximumDelta;
        }

        return new StrategicScalarField(
            graph,
            values);
    }
}
