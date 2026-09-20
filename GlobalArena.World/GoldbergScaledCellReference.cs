namespace GlobalArena.World;

public sealed class GoldbergScaledCellReference
{
    public IcosahedronSeedVertexId SeedVertexId { get; }

    public StrategicCellId CoarseCellId { get; }

    public StrategicCellId FineCellId { get; }

    public GoldbergScaledCellReference(
        IcosahedronSeedVertexId seedVertexId,
        StrategicCellId coarseCellId,
        StrategicCellId fineCellId)
    {
        if (!seedVertexId.IsValid)
        {
            throw new ArgumentException(
                "Seed vertex provenance ID must be valid.",
                nameof(seedVertexId));
        }

        if (!coarseCellId.IsValid)
        {
            throw new ArgumentException(
                "Coarse strategic cell ID must be valid.",
                nameof(coarseCellId));
        }

        if (!fineCellId.IsValid)
        {
            throw new ArgumentException(
                "Fine strategic cell ID must be valid.",
                nameof(fineCellId));
        }

        SeedVertexId =
            seedVertexId;

        CoarseCellId =
            coarseCellId;

        FineCellId =
            fineCellId;
    }
}
