namespace GlobalArena.World;

public sealed class StrategicResourcePotentialFieldSet
{
    public StrategicScalarField GeneralPotential { get; }

    private StrategicResourcePotentialFieldSet(
        StrategicScalarField generalPotential)
    {
        ArgumentNullException.ThrowIfNull(
            generalPotential);

        GeneralPotential =
            generalPotential;
    }

    public static StrategicResourcePotentialFieldSet Generate(
        WorldGenerationRequest request,
        StrategicSurfaceGraph surfaceGraph)
    {
        var generalPotential =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                surfaceGraph);

        return new StrategicResourcePotentialFieldSet(
            generalPotential);
    }
}
