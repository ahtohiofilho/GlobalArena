namespace GlobalArena.World;

public static class GoldbergStrategicTopologyGenerator
{
    private static readonly CanonicalTriangle[] G10IcosahedronFaces =
        new CanonicalTriangle[]
        {
            new(1, 2, 3),
            new(1, 2, 6),
            new(1, 3, 4),
            new(1, 4, 5),
            new(1, 5, 6),
            new(2, 3, 7),
            new(2, 6, 11),
            new(2, 7, 11),
            new(3, 4, 8),
            new(3, 7, 8),
            new(4, 5, 9),
            new(4, 8, 9),
            new(5, 6, 10),
            new(5, 9, 10),
            new(6, 10, 11),
            new(7, 8, 12),
            new(7, 11, 12),
            new(8, 9, 12),
            new(9, 10, 12),
            new(10, 11, 12)
        };

    public static StrategicTopology Generate(
        GoldbergParameters parameters)
    {
        if (!parameters.IsValid)
        {
            throw new ArgumentException(
                "Goldberg parameters must be valid.",
                nameof(parameters));
        }

        if (parameters.M != 1 || parameters.N != 0)
        {
            throw new NotSupportedException(
                "M2.1.4 supports only the minimal G(1,0) strategic topology.");
        }

        return GenerateG10(parameters);
    }

    private static StrategicTopology GenerateG10(
        GoldbergParameters parameters)
    {
        var canonicalPairs =
            G10IcosahedronFaces
                .SelectMany(GetPairs)
                .Distinct()
                .OrderBy(pair => pair.First)
                .ThenBy(pair => pair.Second)
                .ToArray();

        if (canonicalPairs.Length != 30)
        {
            throw new InvalidOperationException(
                "Canonical G(1,0) seed must contain exactly 30 unique edges.");
        }

        var edgeIdsByPair =
            new Dictionary<CanonicalPair, StrategicEdgeId>();

        var vertexIdsByPair =
            canonicalPairs.ToDictionary(
                pair => pair,
                _ => new List<StrategicVertexId>());

        for (var index = 0; index < canonicalPairs.Length; index++)
        {
            edgeIdsByPair.Add(
                canonicalPairs[index],
                new StrategicEdgeId(
                    (ulong)index + 1UL));
        }

        var vertices =
            new StrategicVertex[G10IcosahedronFaces.Length];

        for (var index = 0; index < G10IcosahedronFaces.Length; index++)
        {
            var triangle = G10IcosahedronFaces[index];
            var vertexId =
                new StrategicVertexId(
                    (ulong)index + 1UL);
            var pairs = GetPairs(triangle);
            var incidentEdgeIds =
                pairs
                    .Select(pair => edgeIdsByPair[pair])
                    .OrderBy(id => id.Value)
                    .ToArray();

            vertices[index] =
                new StrategicVertex(
                    vertexId,
                    new[]
                    {
                        new StrategicCellId((ulong)triangle.First),
                        new StrategicCellId((ulong)triangle.Second),
                        new StrategicCellId((ulong)triangle.Third)
                    },
                    incidentEdgeIds);

            foreach (var pair in pairs)
            {
                vertexIdsByPair[pair].Add(vertexId);
            }
        }

        var edges =
            new StrategicEdge[canonicalPairs.Length];

        for (var index = 0; index < canonicalPairs.Length; index++)
        {
            var pair = canonicalPairs[index];
            var incidentVertexIds =
                vertexIdsByPair[pair]
                    .OrderBy(id => id.Value)
                    .ToArray();

            if (incidentVertexIds.Length != 2)
            {
                throw new InvalidOperationException(
                    "Every canonical G(1,0) edge must have exactly two incident vertices.");
            }

            edges[index] =
                new StrategicEdge(
                    new StrategicEdgeId(
                        (ulong)index + 1UL),
                    new StrategicCellId(
                        (ulong)pair.First),
                    new StrategicCellId(
                        (ulong)pair.Second),
                    incidentVertexIds[0],
                    incidentVertexIds[1]);
        }

        var cells =
            new StrategicCell[12];

        for (var cellOrdinal = 1; cellOrdinal <= cells.Length; cellOrdinal++)
        {
            var cellId =
                new StrategicCellId(
                    (ulong)cellOrdinal);

            var adjacentCellIds =
                canonicalPairs
                    .Where(pair => pair.Contains(cellOrdinal))
                    .Select(
                        pair =>
                            new StrategicCellId(
                                (ulong)pair.Other(cellOrdinal)))
                    .OrderBy(id => id.Value)
                    .ToArray();

            var incidentEdgeIds =
                canonicalPairs
                    .Select(
                        (pair, index) =>
                            new
                            {
                                Pair = pair,
                                Id =
                                    new StrategicEdgeId(
                                        (ulong)index + 1UL)
                            })
                    .Where(item => item.Pair.Contains(cellOrdinal))
                    .Select(item => item.Id)
                    .OrderBy(id => id.Value)
                    .ToArray();

            var incidentVertexIds =
                G10IcosahedronFaces
                    .Select(
                        (triangle, index) =>
                            new
                            {
                                Triangle = triangle,
                                Id =
                                    new StrategicVertexId(
                                        (ulong)index + 1UL)
                            })
                    .Where(item => item.Triangle.Contains(cellOrdinal))
                    .Select(item => item.Id)
                    .OrderBy(id => id.Value)
                    .ToArray();

            cells[cellOrdinal - 1] =
                new StrategicCell(
                    cellId,
                    StrategicCellKind.Pentagon,
                    adjacentCellIds,
                    incidentEdgeIds,
                    incidentVertexIds);
        }

        return new StrategicTopology(
            parameters,
            cells,
            edges,
            vertices);
    }

    private static CanonicalPair[] GetPairs(
        CanonicalTriangle triangle)
    {
        return new[]
        {
            CanonicalPair.Create(
                triangle.First,
                triangle.Second),
            CanonicalPair.Create(
                triangle.First,
                triangle.Third),
            CanonicalPair.Create(
                triangle.Second,
                triangle.Third)
        };
    }

    private readonly record struct CanonicalPair(
        int First,
        int Second)
    {
        public static CanonicalPair Create(
            int first,
            int second)
        {
            if (first == second)
            {
                throw new ArgumentException(
                    "Canonical edge endpoints must be distinct.");
            }

            return first < second
                ? new CanonicalPair(first, second)
                : new CanonicalPair(second, first);
        }

        public bool Contains(int value)
        {
            return First == value || Second == value;
        }

        public int Other(int value)
        {
            if (First == value)
            {
                return Second;
            }

            if (Second == value)
            {
                return First;
            }

            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Value is not an endpoint of this canonical edge.");
        }
    }

    private readonly record struct CanonicalTriangle(
        int First,
        int Second,
        int Third)
    {
        public bool Contains(int value)
        {
            return First == value
                || Second == value
                || Third == value;
        }
    }
}
