namespace GlobalArena.World;

public static class StrategicTemperatureFieldGenerator
{
    public const long MinimumRawTemperature =
        -StrategicScalarField.Denominator;

    public const long MaximumRawTemperature =
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
                WorldGenerationRandomDomain.Temperature);

        var values =
            new long[
                surfaceGraph.NodeCount];

        for (var index = 0;
             index < values.Length;
             index++)
        {
            values[index] =
                checked(
                    (long)(
                        stream.NextUInt64()
                        % SampleWidth)
                    - StrategicScalarField.Denominator);
        }

        return new StrategicScalarField(
            surfaceGraph,
            values);
    }
}
