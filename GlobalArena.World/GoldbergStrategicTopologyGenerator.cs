namespace GlobalArena.World;

public static class GoldbergStrategicTopologyGenerator
{
    private static readonly CanonicalTriangle[] IcosahedronFaces =
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

        if (parameters.M != 0 && parameters.N != 0)
        {
            throw new NotSupportedException(
                "M2.1.5.A supports Class I Goldberg topologies only: G(m,0) or G(0,n).");
        }

        var frequency =
            Math.Max(
                parameters.M,
                parameters.N);

        return GenerateClassI(
            parameters,
            frequency);
    }

    private static StrategicTopology GenerateClassI(
        GoldbergParameters parameters,
        int frequency)
    {
        EnsureMaterializable(parameters);

        var vertexKeys =
            new HashSet<ClassILatticeVertexKey>();

        foreach (var face in IcosahedronFaces)
        {
            for (var i = 0; i <= frequency; i++)
            {
                for (var j = 0; j <= frequency - i; j++)
                {
                    vertexKeys.Add(
                        CreateClassIVertexKey(
                            face,
                            frequency,
                            i,
                            j));
                }
            }
        }

        var orderedVertexKeys =
            vertexKeys
                .Order()
                .ToArray();

        if ((ulong)orderedVertexKeys.Length
            != parameters.StrategicCellCount)
        {
            throw new InvalidOperationException(
                "Class I subdivision produced an unexpected strategic cell count.");
        }

        var cellIdsByVertexKey =
            new Dictionary<ClassILatticeVertexKey, StrategicCellId>(
                orderedVertexKeys.Length);

        for (var index = 0; index < orderedVertexKeys.Length; index++)
        {
            cellIdsByVertexKey.Add(
                orderedVertexKeys[index],
                new StrategicCellId(
                    (ulong)index + 1UL));
        }

        var triangleKeys =
            new HashSet<CanonicalCellTriple>();

        foreach (var face in IcosahedronFaces)
        {
            for (var i = 0; i < frequency; i++)
            {
                for (var j = 0; j < frequency - i; j++)
                {
                    triangleKeys.Add(
                        CanonicalCellTriple.Create(
                            GetCellId(
                                face,
                                frequency,
                                i,
                                j,
                                cellIdsByVertexKey),
                            GetCellId(
                                face,
                                frequency,
                                i + 1,
                                j,
                                cellIdsByVertexKey),
                            GetCellId(
                                face,
                                frequency,
                                i,
                                j + 1,
                                cellIdsByVertexKey)));

                    if (i + j <= frequency - 2)
                    {
                        triangleKeys.Add(
                            CanonicalCellTriple.Create(
                                GetCellId(
                                    face,
                                    frequency,
                                    i + 1,
                                    j,
                                    cellIdsByVertexKey),
                                GetCellId(
                                    face,
                                    frequency,
                                    i + 1,
                                    j + 1,
                                    cellIdsByVertexKey),
                                GetCellId(
                                    face,
                                    frequency,
                                    i,
                                    j + 1,
                                    cellIdsByVertexKey)));
                    }
                }
            }
        }

        var orderedTriangleKeys =
            triangleKeys
                .Order()
                .ToArray();

        if ((ulong)orderedTriangleKeys.Length
            != parameters.StrategicVertexCount)
        {
            throw new InvalidOperationException(
                "Class I subdivision produced an unexpected strategic vertex count.");
        }

        var orderedEdgeKeys =
            orderedTriangleKeys
                .SelectMany(GetPairs)
                .Distinct()
                .Order()
                .ToArray();

        if ((ulong)orderedEdgeKeys.Length
            != parameters.StrategicEdgeCount)
        {
            throw new InvalidOperationException(
                "Class I subdivision produced an unexpected strategic edge count.");
        }

        var edgeIdsByPair =
            new Dictionary<CanonicalCellPair, StrategicEdgeId>(
                orderedEdgeKeys.Length);

        var incidentVertexIdsByEdge =
            new Dictionary<CanonicalCellPair, List<StrategicVertexId>>(
                orderedEdgeKeys.Length);

        for (var index = 0; index < orderedEdgeKeys.Length; index++)
        {
            edgeIdsByPair.Add(
                orderedEdgeKeys[index],
                new StrategicEdgeId(
                    (ulong)index + 1UL));

            incidentVertexIdsByEdge.Add(
                orderedEdgeKeys[index],
                new List<StrategicVertexId>(2));
        }

        var cellCount = orderedVertexKeys.Length;
        var adjacentCellIds =
            CreateSetArray<StrategicCellId>(cellCount);
        var incidentEdgeIdsByCell =
            CreateSetArray<StrategicEdgeId>(cellCount);
        var incidentVertexIdsByCell =
            CreateSetArray<StrategicVertexId>(cellCount);

        var vertices =
            new StrategicVertex[orderedTriangleKeys.Length];

        for (var index = 0; index < orderedTriangleKeys.Length; index++)
        {
            var triangle = orderedTriangleKeys[index];
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
                    triangle.ToArray(),
                    incidentEdgeIds);

            foreach (var cellId in triangle.ToArray())
            {
                incidentVertexIdsByCell[
                    checked((int)cellId.Value - 1)]
                    .Add(vertexId);
            }

            foreach (var pair in pairs)
            {
                incidentVertexIdsByEdge[pair]
                    .Add(vertexId);
            }
        }

        var edges =
            new StrategicEdge[orderedEdgeKeys.Length];

        for (var index = 0; index < orderedEdgeKeys.Length; index++)
        {
            var pair = orderedEdgeKeys[index];
            var incidentVertexIds =
                incidentVertexIdsByEdge[pair]
                    .OrderBy(id => id.Value)
                    .ToArray();

            if (incidentVertexIds.Length != 2)
            {
                throw new InvalidOperationException(
                    "Every Class I strategic edge must have exactly two incident strategic vertices.");
            }

            var edgeId =
                new StrategicEdgeId(
                    (ulong)index + 1UL);

            edges[index] =
                new StrategicEdge(
                    edgeId,
                    pair.First,
                    pair.Second,
                    incidentVertexIds[0],
                    incidentVertexIds[1]);

            var firstIndex =
                checked((int)pair.First.Value - 1);
            var secondIndex =
                checked((int)pair.Second.Value - 1);

            adjacentCellIds[firstIndex]
                .Add(pair.Second);
            adjacentCellIds[secondIndex]
                .Add(pair.First);

            incidentEdgeIdsByCell[firstIndex]
                .Add(edgeId);
            incidentEdgeIdsByCell[secondIndex]
                .Add(edgeId);
        }

        var cells =
            new StrategicCell[cellCount];

        var pentagonCount = 0;
        var hexagonCount = 0;

        for (var index = 0; index < cellCount; index++)
        {
            var degree =
                adjacentCellIds[index].Count;

            var kind =
                degree switch
                {
                    5 => StrategicCellKind.Pentagon,
                    6 => StrategicCellKind.Hexagon,
                    _ => throw new InvalidOperationException(
                        $"Class I strategic cell degree must be 5 or 6, but was {degree}.")
                };

            if (kind == StrategicCellKind.Pentagon)
            {
                pentagonCount++;
            }
            else
            {
                hexagonCount++;
            }

            cells[index] =
                new StrategicCell(
                    new StrategicCellId(
                        (ulong)index + 1UL),
                    kind,
                    adjacentCellIds[index],
                    incidentEdgeIdsByCell[index],
                    incidentVertexIdsByCell[index]);
        }

        if ((ulong)pentagonCount != parameters.PentagonCount
            || (ulong)hexagonCount != parameters.HexagonCount)
        {
            throw new InvalidOperationException(
                "Class I subdivision produced unexpected pentagon or hexagon counts.");
        }

        return new StrategicTopology(
            parameters,
            cells,
            edges,
            vertices);
    }

    private static StrategicCellId GetCellId(
        CanonicalTriangle face,
        int frequency,
        int i,
        int j,
        IReadOnlyDictionary<ClassILatticeVertexKey, StrategicCellId> cellIdsByVertexKey)
    {
        return cellIdsByVertexKey[
            CreateClassIVertexKey(
                face,
                frequency,
                i,
                j)];
    }

    private static ClassILatticeVertexKey CreateClassIVertexKey(
        CanonicalTriangle face,
        int frequency,
        int i,
        int j)
    {
        if (i < 0
            || j < 0
            || i + j > frequency)
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                "Class I barycentric coordinates must lie inside the seed face.");
        }

        return ClassILatticeVertexKey.Create(
            face.First,
            frequency - i - j,
            face.Second,
            i,
            face.Third,
            j);
    }

    private static HashSet<T>[] CreateSetArray<T>(
        int count)
        where T : notnull
    {
        var result =
            new HashSet<T>[count];

        for (var index = 0; index < count; index++)
        {
            result[index] =
                new HashSet<T>();
        }

        return result;
    }

    private static CanonicalCellPair[] GetPairs(
        CanonicalCellTriple triangle)
    {
        return new[]
        {
            CanonicalCellPair.Create(
                triangle.First,
                triangle.Second),
            CanonicalCellPair.Create(
                triangle.First,
                triangle.Third),
            CanonicalCellPair.Create(
                triangle.Second,
                triangle.Third)
        };
    }

    private static void EnsureMaterializable(
        GoldbergParameters parameters)
    {
        if (parameters.StrategicCellCount > int.MaxValue
            || parameters.StrategicEdgeCount > int.MaxValue
            || parameters.StrategicVertexCount > int.MaxValue)
        {
            throw new NotSupportedException(
                "Requested Goldberg topology exceeds the current in-memory implementation limit.");
        }
    }

    private readonly record struct ClassILatticeVertexKey(
        int FirstVertex,
        int FirstWeight,
        int SecondVertex,
        int SecondWeight,
        int ThirdVertex,
        int ThirdWeight)
        : IComparable<ClassILatticeVertexKey>
    {
        public static ClassILatticeVertexKey Create(
            int firstVertex,
            int firstWeight,
            int secondVertex,
            int secondWeight,
            int thirdVertex,
            int thirdWeight)
        {
            var weightedVertices =
                new[]
                {
                    new WeightedSeedVertex(
                        firstVertex,
                        firstWeight),
                    new WeightedSeedVertex(
                        secondVertex,
                        secondWeight),
                    new WeightedSeedVertex(
                        thirdVertex,
                        thirdWeight)
                }
                .Where(item => item.Weight > 0)
                .OrderBy(item => item.Vertex)
                .ToArray();

            return new ClassILatticeVertexKey(
                weightedVertices.ElementAtOrDefault(0).Vertex,
                weightedVertices.ElementAtOrDefault(0).Weight,
                weightedVertices.ElementAtOrDefault(1).Vertex,
                weightedVertices.ElementAtOrDefault(1).Weight,
                weightedVertices.ElementAtOrDefault(2).Vertex,
                weightedVertices.ElementAtOrDefault(2).Weight);
        }

        public int CompareTo(
            ClassILatticeVertexKey other)
        {
            var comparison =
                FirstVertex.CompareTo(
                    other.FirstVertex);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                FirstWeight.CompareTo(
                    other.FirstWeight);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                SecondVertex.CompareTo(
                    other.SecondVertex);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                SecondWeight.CompareTo(
                    other.SecondWeight);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                ThirdVertex.CompareTo(
                    other.ThirdVertex);

            return comparison != 0
                ? comparison
                : ThirdWeight.CompareTo(
                    other.ThirdWeight);
        }
    }

    private readonly record struct WeightedSeedVertex(
        int Vertex,
        int Weight);

    private readonly record struct CanonicalCellPair(
        StrategicCellId First,
        StrategicCellId Second)
        : IComparable<CanonicalCellPair>
    {
        public static CanonicalCellPair Create(
            StrategicCellId first,
            StrategicCellId second)
        {
            if (first == second)
            {
                throw new ArgumentException(
                    "Canonical strategic edge endpoints must be distinct.");
            }

            return first.Value < second.Value
                ? new CanonicalCellPair(
                    first,
                    second)
                : new CanonicalCellPair(
                    second,
                    first);
        }

        public int CompareTo(
            CanonicalCellPair other)
        {
            var comparison =
                First.Value.CompareTo(
                    other.First.Value);

            return comparison != 0
                ? comparison
                : Second.Value.CompareTo(
                    other.Second.Value);
        }
    }

    private readonly record struct CanonicalCellTriple(
        StrategicCellId First,
        StrategicCellId Second,
        StrategicCellId Third)
        : IComparable<CanonicalCellTriple>
    {
        public static CanonicalCellTriple Create(
            StrategicCellId first,
            StrategicCellId second,
            StrategicCellId third)
        {
            var values =
                new[]
                {
                    first,
                    second,
                    third
                }
                .OrderBy(id => id.Value)
                .ToArray();

            if (values.Distinct().Count() != 3)
            {
                throw new ArgumentException(
                    "Canonical strategic triangle must contain three distinct cells.");
            }

            return new CanonicalCellTriple(
                values[0],
                values[1],
                values[2]);
        }

        public StrategicCellId[] ToArray()
        {
            return new[]
            {
                First,
                Second,
                Third
            };
        }

        public int CompareTo(
            CanonicalCellTriple other)
        {
            var comparison =
                First.Value.CompareTo(
                    other.First.Value);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                Second.Value.CompareTo(
                    other.Second.Value);

            return comparison != 0
                ? comparison
                : Third.Value.CompareTo(
                    other.Third.Value);
        }
    }

    private readonly record struct CanonicalTriangle(
        int First,
        int Second,
        int Third);
}
