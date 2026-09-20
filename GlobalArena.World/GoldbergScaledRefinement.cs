namespace GlobalArena.World;

public sealed class GoldbergScaledRefinement
{
    public GoldbergParameters CoarseParameters { get; }

    public GoldbergParameters FineParameters { get; }

    public int Scale { get; }

    public GoldbergScaledRefinement(
        GoldbergParameters coarseParameters,
        GoldbergParameters fineParameters)
    {
        if (!coarseParameters.IsValid)
        {
            throw new ArgumentException(
                "Coarse Goldberg parameters must be valid.",
                nameof(coarseParameters));
        }

        if (!fineParameters.IsValid)
        {
            throw new ArgumentException(
                "Fine Goldberg parameters must be valid.",
                nameof(fineParameters));
        }

        var scale =
            DeriveScale(
                coarseParameters,
                fineParameters);

        CoarseParameters =
            coarseParameters;

        FineParameters =
            fineParameters;

        Scale =
            scale;
    }

    private static int DeriveScale(
        GoldbergParameters coarseParameters,
        GoldbergParameters fineParameters)
    {
        int scale;

        if (coarseParameters.M > 0)
        {
            if (fineParameters.M % coarseParameters.M != 0)
            {
                throw UnsupportedPair();
            }

            scale =
                fineParameters.M
                / coarseParameters.M;
        }
        else
        {
            if (fineParameters.N % coarseParameters.N != 0)
            {
                throw UnsupportedPair();
            }

            scale =
                fineParameters.N
                / coarseParameters.N;
        }

        if (scale < 2)
        {
            throw UnsupportedPair();
        }

        var expectedFineM =
            checked(
                (long)coarseParameters.M
                * scale);

        var expectedFineN =
            checked(
                (long)coarseParameters.N
                * scale);

        if (
            expectedFineM != fineParameters.M
            || expectedFineN != fineParameters.N)
        {
            throw UnsupportedPair();
        }

        return scale;
    }

    private static NotSupportedException UnsupportedPair()
    {
        return new NotSupportedException(
            "The Goldberg parameter pair is not supported by the scaled refinement contract.");
    }
}
