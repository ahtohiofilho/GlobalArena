namespace GlobalArena.World;

public sealed class SharedBorderBand
{
    public StrategicEdgeId StrategicEdgeId { get; }

    public IReadOnlyList<SharedBorderElement> Elements { get; }

    public SharedBorderBand(
        StrategicEdgeId strategicEdgeId,
        IEnumerable<SharedBorderElement> elements)
    {
        if (!strategicEdgeId.IsValid)
        {
            throw new ArgumentException(
                "Strategic edge ID must be valid.",
                nameof(strategicEdgeId));
        }

        ArgumentNullException.ThrowIfNull(
            elements);

        var elementArray =
            elements.ToArray();

        if (elementArray.Length == 0)
        {
            throw new ArgumentException(
                "A shared border band must contain at least one shared border element.",
                nameof(elements));
        }

        if (elementArray.Any(
            element =>
                element is null))
        {
            throw new ArgumentException(
                "Shared border elements cannot contain null values.",
                nameof(elements));
        }

        var orderedElements =
            elementArray
                .OrderBy(
                    element =>
                        element.Id.LocalOrdinal)
                .ToArray();

        if (orderedElements.Any(
            element =>
                element.Id.StrategicEdgeId
                != strategicEdgeId))
        {
            throw new ArgumentException(
                "Every shared border element must belong to the band's strategic edge.",
                nameof(elements));
        }

        if (orderedElements
            .Select(
                element =>
                    element.Id)
            .Distinct()
            .Count()
            != orderedElements.Length)
        {
            throw new ArgumentException(
                "Shared border element IDs cannot contain duplicates.",
                nameof(elements));
        }

        for (var index = 0; index < orderedElements.Length; index++)
        {
            var expectedOrdinal =
                (ulong)index + 1UL;

            if (orderedElements[index].Id.LocalOrdinal
                != expectedOrdinal)
            {
                throw new ArgumentException(
                    "Shared border element ordinals must be contiguous canonical one-based values.",
                    nameof(elements));
            }
        }

        StrategicEdgeId =
            strategicEdgeId;

        Elements =
            Array.AsReadOnly(
                orderedElements);
    }
}
