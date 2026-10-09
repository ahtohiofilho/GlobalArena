namespace GlobalArena.World;

internal static class CanonicalIcosahedronGeometry
{
    private static readonly SphericalPoint3[] SeedDirections =
        CreateSeedDirections();

    public static SphericalPoint3 GetSeedDirection(
        int seedVertexId)
    {
        if (seedVertexId < 1
            || seedVertexId > SeedDirections.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seedVertexId),
                "Canonical geometry seed ID is outside the supported 1..32 range.");
        }

        return SeedDirections[
            seedVertexId - 1];
    }

    private static SphericalPoint3[] CreateSeedDirections()
    {
        var phi =
            (1d + Math.Sqrt(5d))
            / 2d;

        var result =
            new SphericalPoint3[32];

        result[0] =
            new SphericalPoint3(
                0d,
                -1d,
                -phi);

        result[1] =
            new SphericalPoint3(
                0d,
                1d,
                -phi);

        result[2] =
            new SphericalPoint3(
                -phi,
                0d,
                -1d);

        result[3] =
            new SphericalPoint3(
                -1d,
                -phi,
                0d);

        result[4] =
            new SphericalPoint3(
                1d,
                -phi,
                0d);

        result[5] =
            new SphericalPoint3(
                phi,
                0d,
                -1d);

        result[6] =
            new SphericalPoint3(
                -1d,
                phi,
                0d);

        result[7] =
            new SphericalPoint3(
                -phi,
                0d,
                1d);

        result[8] =
            new SphericalPoint3(
                0d,
                -1d,
                phi);

        result[9] =
            new SphericalPoint3(
                phi,
                0d,
                1d);

        result[10] =
            new SphericalPoint3(
                1d,
                phi,
                0d);

        result[11] =
            new SphericalPoint3(
                0d,
                1d,
                phi);

        for (var faceIndex = 0;
             faceIndex < 20;
             faceIndex++)
        {
            var seedVertexIds =
                GoldbergStrategicTopologyGenerator
                    .GetCanonicalIcosahedronFaceVertexIds(
                        faceIndex);

            var first =
                result[
                    seedVertexIds[0] - 1];

            var second =
                result[
                    seedVertexIds[1] - 1];

            var third =
                result[
                    seedVertexIds[2] - 1];

            result[12 + faceIndex] =
                new SphericalPoint3(
                    first.X + second.X + third.X,
                    first.Y + second.Y + third.Y,
                    first.Z + second.Z + third.Z);
        }

        return result;
    }
}
