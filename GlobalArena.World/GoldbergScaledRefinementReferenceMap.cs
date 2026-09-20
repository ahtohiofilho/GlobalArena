namespace GlobalArena.World;

public sealed class GoldbergScaledRefinementReferenceMap
{
    public GoldbergScaledRefinement Refinement { get; }

    public IReadOnlyList<GoldbergScaledCellReference> CellReferences { get; }

    internal GoldbergScaledRefinementReferenceMap(
        GoldbergScaledRefinement refinement,
        IEnumerable<GoldbergScaledCellReference> cellReferences)
    {
        ArgumentNullException.ThrowIfNull(refinement);
        ArgumentNullException.ThrowIfNull(cellReferences);

        var references =
            cellReferences.ToArray();

        if (references.Length != 12)
        {
            throw new ArgumentException(
                "The canonical seed-vertex reference map must contain exactly 12 cell references.",
                nameof(cellReferences));
        }

        if (references.Any(reference => reference is null))
        {
            throw new ArgumentException(
                "Cell references cannot contain null values.",
                nameof(cellReferences));
        }

        if (references.Select(reference => reference.SeedVertexId).Distinct().Count() != 12)
        {
            throw new ArgumentException(
                "Seed vertex provenance IDs must be unique.",
                nameof(cellReferences));
        }

        if (references.Select(reference => reference.CoarseCellId).Distinct().Count() != 12)
        {
            throw new ArgumentException(
                "Coarse strategic cell IDs must be unique.",
                nameof(cellReferences));
        }

        if (references.Select(reference => reference.FineCellId).Distinct().Count() != 12)
        {
            throw new ArgumentException(
                "Fine strategic cell IDs must be unique.",
                nameof(cellReferences));
        }

        var ordered =
            references
                .OrderBy(reference => reference.SeedVertexId.Value)
                .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index].SeedVertexId.Value != index + 1)
            {
                throw new ArgumentException(
                    "Seed vertex provenance IDs must cover the canonical range 1 through 12.",
                    nameof(cellReferences));
            }
        }

        Refinement =
            refinement;

        CellReferences =
            Array.AsReadOnly(
                ordered);
    }
}
