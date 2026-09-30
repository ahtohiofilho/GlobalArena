namespace GlobalArena.World;

public static class StrategicElevationFieldGenerator
{
    public const long MinimumRawElevation =
        -StrategicScalarField.Denominator;

    public const long MaximumRawElevation =
        StrategicScalarField.Denominator;

    private const ulong SampleWidth =
        2_000_001UL;

    public static StrategicScalarField Generate(
        WorldGenerationRequest request,
        StrategicSurfaceGraph surfaceGraph)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            surfaceGraph);

        if (surfaceGraph.StrategicTopology.Parameters
            != request.StrategicParameters)
        {
            throw new ArgumentException(
                "Strategic surface graph parameters must match the generation request.",
                nameof(surfaceGraph));
        }

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Elevation);

        var values =
            new long[
                surfaceGraph.NodeCount];

        for (var index = 0;
             index < values.Length;
             index++)
        {
            var sample =
                stream.NextUInt64();

            values[index] =
                checked(
                    (long)(
                        sample
                        % SampleWidth)
                    - StrategicScalarField.Denominator);
        }

        return new StrategicScalarField(
            surfaceGraph,
            values);
    }
}
