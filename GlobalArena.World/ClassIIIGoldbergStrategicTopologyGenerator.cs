namespace GlobalArena.World;

internal static class ClassIIIGoldbergStrategicTopologyGenerator
{
    private static readonly SeedFace[] CanonicalIcosahedronFaces =
        new SeedFace[]
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

    private static readonly LocalPoint[] NeighborSteps =
        new LocalPoint[]
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
            new(1, -1),
            new(-1, 1)
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

        if (parameters.M <= 0
            || parameters.N <= 0
            || parameters.M == parameters.N)
        {
            throw new ArgumentException(
                "Class III Goldberg parameters require positive unequal m and n.",
                nameof(parameters));
        }

        EnsureMaterializable(parameters);

        var orientedFaces =
            CreateConsistentlyOrientedFaces();

        var localPattern =
            CreateLocalPattern(
                parameters.M,
                parameters.N);

        var localPointCount =
            localPattern.Points.Length;

        var totalLocalPointCount =
            checked(
                orientedFaces.Length
                * localPointCount);

        var disjointSet =
            new DisjointSet(
                totalLocalPointCount);

        var faceEdges =
            CreateFaceEdgeIncidence(
                orientedFaces);

        var rollLookups =
            CreateRollLookups(
                localPattern.LatticeIndices);

        var offset =
            new LatticeIndex(
                checked(parameters.M + parameters.N),
                parameters.M,
                checked(
                    checked(2 * parameters.M)
                    + parameters.N));

        foreach (var item in faceEdges.OrderBy(item => item.Key))
        {
            var references =
                item.Value
                    .OrderBy(reference => reference.FaceIndex)
                    .ThenBy(reference => reference.EdgeIndex)
                    .ToArray();

            if (references.Length != 2)
            {
                throw new InvalidOperationException(
                    "Every icosahedral edge must have exactly two incident faces.");
            }

            var firstReference =
                references[0];

            var secondReference =
                references[1];

            var firstRoll =
                (3 - firstReference.EdgeIndex) % 3;

            var secondRoll =
                (3 - secondReference.EdgeIndex) % 3;

            var secondLookup =
                rollLookups[secondRoll];

            for (var localIndex = 0;
                 localIndex < localPointCount;
                 localIndex++)
            {
                var rolled =
                    localPattern.LatticeIndices[localIndex]
                        .Roll(firstRoll);

                var target =
                    offset.Subtract(
                        rolled);

                if (!secondLookup.TryGetValue(
                    target,
                    out var secondLocalIndex))
                {
                    continue;
                }

                var firstGlobalIndex =
                    checked(
                        checked(
                            firstReference.FaceIndex
                            * localPointCount)
                        + localIndex);

                var secondGlobalIndex =
                    checked(
                        checked(
                            secondReference.FaceIndex
                            * localPointCount)
                        + secondLocalIndex);

                disjointSet.Union(
                    firstGlobalIndex,
                    secondGlobalIndex);
            }
        }

        var rootTriangles =
            new HashSet<CanonicalRootTriple>();

        for (var faceIndex = 0;
             faceIndex < orientedFaces.Length;
             faceIndex++)
        {
            foreach (var localTriangle in localPattern.Triangles)
            {
                var first =
                    disjointSet.Find(
                        checked(
                            checked(faceIndex * localPointCount)
                            + localTriangle.First));

                var second =
                    disjointSet.Find(
                        checked(
                            checked(faceIndex * localPointCount)
                            + localTriangle.Second));

                var third =
                    disjointSet.Find(
                        checked(
                            checked(faceIndex * localPointCount)
                            + localTriangle.Third));

                if (first == second
                    || first == third
                    || second == third)
                {
                    continue;
                }

                rootTriangles.Add(
                    CanonicalRootTriple.Create(
                        first,
                        second,
                        third));
            }
        }

        if ((ulong)rootTriangles.Count
            != parameters.StrategicVertexCount)
        {
            throw new InvalidOperationException(
                "Class III stitching produced an unexpected strategic vertex count.");
        }

        var orderedRoots =
            rootTriangles
                .SelectMany(triangle => triangle.ToArray())
                .Distinct()
                .Order()
                .ToArray();

        if ((ulong)orderedRoots.Length
            != parameters.StrategicCellCount)
        {
            throw new InvalidOperationException(
                "Class III stitching produced an unexpected strategic cell count.");
        }

        var cellIdsByRoot =
            new Dictionary<int, StrategicCellId>(
                orderedRoots.Length);

        for (var index = 0;
             index < orderedRoots.Length;
             index++)
        {
            cellIdsByRoot.Add(
                orderedRoots[index],
                new StrategicCellId(
                    (ulong)index + 1UL));
        }

        var constructionProvenance =
            CreateConstructionProvenance(
                parameters,
                orientedFaces,
                localPattern,
                disjointSet,
                orderedRoots);

        var orderedTriangleKeys =
            rootTriangles
                .Select(
                    triangle =>
                        CanonicalCellTriple.Create(
                            cellIdsByRoot[triangle.First],
                            cellIdsByRoot[triangle.Second],
                            cellIdsByRoot[triangle.Third]))
                .Distinct()
                .Order()
                .ToArray();

        if ((ulong)orderedTriangleKeys.Length
            != parameters.StrategicVertexCount)
        {
            throw new InvalidOperationException(
                "Class III canonicalization changed the strategic vertex count.");
        }

        return AssembleTopology(
            parameters,
            orderedRoots.Length,
            orderedTriangleKeys,
            constructionProvenance);
    }

    private static StrategicTopology AssembleTopology(
        GoldbergParameters parameters,
        int cellCount,
        IReadOnlyList<CanonicalCellTriple> orderedTriangleKeys,
        IReadOnlyList<StrategicCellConstructionProvenance>
            constructionProvenance)
    {
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
                "Class III stitching produced an unexpected strategic edge count.");
        }

        var edgeIdsByPair =
            new Dictionary<CanonicalCellPair, StrategicEdgeId>(
                orderedEdgeKeys.Length);

        var incidentVertexIdsByEdge =
            new Dictionary<CanonicalCellPair, List<StrategicVertexId>>(
                orderedEdgeKeys.Length);

        for (var index = 0;
             index < orderedEdgeKeys.Length;
             index++)
        {
            edgeIdsByPair.Add(
                orderedEdgeKeys[index],
                new StrategicEdgeId(
                    (ulong)index + 1UL));

            incidentVertexIdsByEdge.Add(
                orderedEdgeKeys[index],
                new List<StrategicVertexId>(2));
        }

        var adjacentCellIds =
            CreateSetArray<StrategicCellId>(
                cellCount);

        var incidentEdgeIdsByCell =
            CreateSetArray<StrategicEdgeId>(
                cellCount);

        var incidentVertexIdsByCell =
            CreateSetArray<StrategicVertexId>(
                cellCount);

        var vertices =
            new StrategicVertex[
                orderedTriangleKeys.Count];

        for (var index = 0;
             index < orderedTriangleKeys.Count;
             index++)
        {
            var triangle =
                orderedTriangleKeys[index];

            var vertexId =
                new StrategicVertexId(
                    (ulong)index + 1UL);

            var pairs =
                GetPairs(
                    triangle);

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
            new StrategicEdge[
                orderedEdgeKeys.Length];

        for (var index = 0;
             index < orderedEdgeKeys.Length;
             index++)
        {
            var pair =
                orderedEdgeKeys[index];

            var incidentVertexIds =
                incidentVertexIdsByEdge[pair]
                    .OrderBy(id => id.Value)
                    .ToArray();

            if (incidentVertexIds.Length != 2)
            {
                throw new InvalidOperationException(
                    "Every Class III strategic edge must have exactly two incident strategic vertices.");
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
                checked(
                    (int)pair.First.Value
                    - 1);

            var secondIndex =
                checked(
                    (int)pair.Second.Value
                    - 1);

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
            new StrategicCell[
                cellCount];

        var pentagonCount = 0;
        var hexagonCount = 0;

        for (var index = 0;
             index < cellCount;
             index++)
        {
            var degree =
                adjacentCellIds[index].Count;

            var kind =
                degree switch
                {
                    5 => StrategicCellKind.Pentagon,
                    6 => StrategicCellKind.Hexagon,
                    _ => throw new InvalidOperationException(
                        $"Class III strategic cell degree must be 5 or 6, but was {degree}.")
                };

            if (kind
                == StrategicCellKind.Pentagon)
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

        if ((ulong)pentagonCount
                != parameters.PentagonCount
            || (ulong)hexagonCount
                != parameters.HexagonCount)
        {
            throw new InvalidOperationException(
                "Class III stitching produced unexpected pentagon or hexagon counts.");
        }

        return new StrategicTopology(
            parameters,
            cells,
            edges,
            vertices,
            constructionProvenance);
    }

    private static StrategicCellConstructionProvenance[]
        CreateConstructionProvenance(
            GoldbergParameters parameters,
            IReadOnlyList<SeedFace> orientedFaces,
            LocalPattern localPattern,
            DisjointSet disjointSet,
            IReadOnlyList<int> orderedRoots)
    {
        var expectedRoots =
            new HashSet<int>(
                orderedRoots);

        var provenanceByRoot =
            new Dictionary<
                int,
                StrategicCellConstructionProvenance>(
                orderedRoots.Count);

        var localPointCount =
            localPattern.Points.Length;

        for (var faceIndex = 0;
             faceIndex < orientedFaces.Count;
             faceIndex++)
        {
            var face =
                orientedFaces[faceIndex];

            for (var localIndex = 0;
                 localIndex < localPointCount;
                 localIndex++)
            {
                var root =
                    disjointSet.Find(
                        checked(
                            checked(
                                faceIndex
                                * localPointCount)
                            + localIndex));

                if (!expectedRoots.Contains(
                    root))
                {
                    continue;
                }

                if (!TryCreateConstructionProvenance(
                    parameters,
                    face,
                    localPattern.Points[
                        localIndex],
                    out var candidate))
                {
                    continue;
                }

                if (provenanceByRoot.TryGetValue(
                    root,
                    out var existing))
                {
                    if (existing != candidate)
                    {
                        throw new InvalidOperationException(
                            "Class III stitched equivalents must resolve to identical stable construction provenance.");
                    }

                    continue;
                }

                provenanceByRoot.Add(
                    root,
                    candidate);
            }
        }

        if (provenanceByRoot.Count
            != orderedRoots.Count)
        {
            throw new InvalidOperationException(
                "Class III stable construction provenance must cover every final strategic cell.");
        }

        return orderedRoots
            .Select(
                root =>
                    provenanceByRoot[root])
            .ToArray();
    }

    private static bool TryCreateConstructionProvenance(
        GoldbergParameters parameters,
        SeedFace face,
        LocalPoint point,
        out StrategicCellConstructionProvenance provenance)
    {
        var triangulationNumber =
            checked(
                (long)parameters
                    .TriangulationNumber);

        var m =
            (long)parameters.M;

        var n =
            (long)parameters.N;

        var p =
            (long)point.P;

        var q =
            (long)point.Q;

        var firstWeight =
            checked(
                triangulationNumber
                - checked(m * p)
                - checked(
                    checked(m + n)
                    * q));

        var secondWeight =
            checked(
                checked(
                    checked(m + n)
                    * p)
                + checked(n * q));

        var thirdWeight =
            checked(
                checked(-n * p)
                + checked(m * q));

        if (firstWeight < 0
            || secondWeight < 0
            || thirdWeight < 0)
        {
            provenance =
                default;

            return false;
        }

        if (checked(
            checked(
                firstWeight
                + secondWeight)
            + thirdWeight)
            != triangulationNumber)
        {
            throw new InvalidOperationException(
                "Class III barycentric construction weights must sum to the Goldberg triangulation number.");
        }

        provenance =
            StrategicCellConstructionProvenance.Create(
                face.First,
                firstWeight,
                face.Second,
                secondWeight,
                face.Third,
                thirdWeight);

        return true;
    }

    private static SeedFace[] CreateConsistentlyOrientedFaces()
    {
        var incidence =
            new Dictionary<SeedEdge, List<int>>();

        for (var faceIndex = 0;
             faceIndex < CanonicalIcosahedronFaces.Length;
             faceIndex++)
        {
            foreach (var edge in CanonicalIcosahedronFaces[faceIndex].GetUndirectedEdges())
            {
                if (!incidence.TryGetValue(
                    edge,
                    out var faces))
                {
                    faces =
                        new List<int>(2);

                    incidence.Add(
                        edge,
                        faces);
                }

                faces.Add(
                    faceIndex);
            }
        }

        if (incidence.Count != 30
            || incidence.Values.Any(faces => faces.Count != 2))
        {
            throw new InvalidOperationException(
                "Canonical icosahedron seed must contain 30 manifold edges.");
        }

        var oriented =
            new SeedFace?[
                CanonicalIcosahedronFaces.Length];

        oriented[0] =
            CanonicalIcosahedronFaces[0];

        var queue =
            new Queue<int>();

        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            var faceIndex =
                queue.Dequeue();

            var face =
                oriented[faceIndex]!.Value;

            foreach (var directedEdge in face.GetDirectedEdges())
            {
                var edge =
                    SeedEdge.Create(
                        directedEdge.First,
                        directedEdge.Second);

                var incidentFaces =
                    incidence[edge];

                var adjacentFaceIndex =
                    incidentFaces[0] == faceIndex
                        ? incidentFaces[1]
                        : incidentFaces[0];

                if (!oriented[adjacentFaceIndex].HasValue)
                {
                    var canonical =
                        CanonicalIcosahedronFaces[
                            adjacentFaceIndex];

                    var candidates =
                        canonical
                            .GetCyclicRotations()
                            .Where(
                                candidate =>
                                    candidate.ContainsDirectedEdge(
                                        directedEdge.Second,
                                        directedEdge.First))
                            .ToArray();

                    if (candidates.Length == 0)
                    {
                        candidates =
                            canonical
                                .ReverseOrientation()
                                .GetCyclicRotations()
                                .Where(
                                    candidate =>
                                        candidate.ContainsDirectedEdge(
                                            directedEdge.Second,
                                            directedEdge.First))
                                .ToArray();
                    }

                    if (candidates.Length == 0)
                    {
                        throw new InvalidOperationException(
                            "Unable to orient adjacent icosahedron face consistently.");
                    }

                    oriented[adjacentFaceIndex] =
                        candidates
                            .Order()
                            .First();

                    queue.Enqueue(
                        adjacentFaceIndex);
                }
                else if (!oriented[adjacentFaceIndex]!.Value
                    .ContainsDirectedEdge(
                        directedEdge.Second,
                        directedEdge.First))
                {
                    throw new InvalidOperationException(
                        "Icosahedron face orientation is inconsistent across a shared edge.");
                }
            }
        }

        if (oriented.Any(face => !face.HasValue))
        {
            throw new InvalidOperationException(
                "Canonical icosahedron face graph must be connected.");
        }

        return oriented
            .Select(face => face!.Value)
            .ToArray();
    }

    private static Dictionary<SeedEdge, List<FaceEdgeReference>>
        CreateFaceEdgeIncidence(
            IReadOnlyList<SeedFace> orientedFaces)
    {
        var result =
            new Dictionary<SeedEdge, List<FaceEdgeReference>>();

        for (var faceIndex = 0;
             faceIndex < orientedFaces.Count;
             faceIndex++)
        {
            var edges =
                orientedFaces[faceIndex]
                    .GetDirectedEdges();

            for (var edgeIndex = 0;
                 edgeIndex < edges.Length;
                 edgeIndex++)
            {
                var edge =
                    SeedEdge.Create(
                        edges[edgeIndex].First,
                        edges[edgeIndex].Second);

                if (!result.TryGetValue(
                    edge,
                    out var references))
                {
                    references =
                        new List<FaceEdgeReference>(2);

                    result.Add(
                        edge,
                        references);
                }

                references.Add(
                    new FaceEdgeReference(
                        faceIndex,
                        edgeIndex));
            }
        }

        if (result.Count != 30
            || result.Values.Any(references => references.Count != 2))
        {
            throw new InvalidOperationException(
                "Oriented icosahedron seed must contain exactly 30 shared edges.");
        }

        return result;
    }

    private static LocalPattern CreateLocalPattern(
        int m,
        int n)
    {
        var firstCorner =
            new LocalPoint(
                0,
                0);

        var secondCorner =
            new LocalPoint(
                m,
                n);

        var thirdCorner =
            new LocalPoint(
                -n,
                checked(m + n));

        var pLow =
            Math.Min(
                0,
                Math.Min(
                    m,
                    -n))
            - 1;

        var pHigh =
            Math.Max(
                0,
                Math.Max(
                    m,
                    -n))
            + 1;

        var qLow =
            Math.Min(
                0,
                Math.Min(
                    n,
                    checked(m + n)))
            - 1;

        var qHigh =
            Math.Max(
                0,
                Math.Max(
                    n,
                    checked(m + n)))
            + 1;

        var kept =
            new HashSet<LocalPoint>();

        for (var q = qLow;
             q <= qHigh;
             q++)
        {
            for (var p = pLow;
                 p <= pHigh;
                 p++)
            {
                var point =
                    new LocalPoint(
                        p,
                        q);

                if (IsInsideOrOnBoundary(
                    firstCorner,
                    secondCorner,
                    thirdCorner,
                    point))
                {
                    kept.Add(
                        point);
                }
            }
        }

        var corners =
            new HashSet<LocalPoint>(
                new[]
                {
                    firstCorner,
                    secondCorner,
                    thirdCorner
                });

        var halo =
            new HashSet<LocalPoint>();

        foreach (var point in kept.Order())
        {
            if (corners.Contains(point))
            {
                continue;
            }

            foreach (var step in NeighborSteps)
            {
                var neighbor =
                    new LocalPoint(
                        checked(point.P + step.P),
                        checked(point.Q + step.Q));

                if (kept.Contains(neighbor))
                {
                    continue;
                }

                if (IsOutsideExactlyOneSide(
                    firstCorner,
                    secondCorner,
                    thirdCorner,
                    neighbor))
                {
                    halo.Add(
                        neighbor);
                }
            }
        }

        var points =
            kept
                .Order()
                .Concat(
                    halo.Order())
                .ToArray();

        var indexByPoint =
            new Dictionary<LocalPoint, int>(
                points.Length);

        for (var index = 0;
             index < points.Length;
             index++)
        {
            indexByPoint.Add(
                points[index],
                index);
        }

        var triangles =
            new List<LocalTriangle>();

        for (var q = qLow;
             q < qHigh;
             q++)
        {
            for (var p = pLow;
                 p < pHigh;
                 p++)
            {
                var firstUp =
                    new LocalPoint(
                        p,
                        q);

                var secondUp =
                    new LocalPoint(
                        checked(p + 1),
                        q);

                var thirdUp =
                    new LocalPoint(
                        p,
                        checked(q + 1));

                TryAddLocalTriangle(
                    indexByPoint,
                    triangles,
                    firstUp,
                    secondUp,
                    thirdUp);

                var firstDown =
                    secondUp;

                var secondDown =
                    new LocalPoint(
                        checked(p + 1),
                        checked(q + 1));

                var thirdDown =
                    thirdUp;

                TryAddLocalTriangle(
                    indexByPoint,
                    triangles,
                    firstDown,
                    secondDown,
                    thirdDown);
            }
        }

        var latticeIndices =
            points
                .Select(
                    point =>
                        new LatticeIndex(
                            checked(point.P + point.Q),
                            checked(m - point.P),
                            checked(
                                checked(m + n)
                                - point.Q)))
                .ToArray();

        return new LocalPattern(
            points,
            triangles.ToArray(),
            latticeIndices);
    }

    private static void TryAddLocalTriangle(
        IReadOnlyDictionary<LocalPoint, int> indexByPoint,
        ICollection<LocalTriangle> triangles,
        LocalPoint first,
        LocalPoint second,
        LocalPoint third)
    {
        if (!indexByPoint.TryGetValue(
            first,
            out var firstIndex)
            || !indexByPoint.TryGetValue(
                second,
                out var secondIndex)
            || !indexByPoint.TryGetValue(
                third,
                out var thirdIndex))
        {
            return;
        }

        triangles.Add(
            new LocalTriangle(
                firstIndex,
                secondIndex,
                thirdIndex));
    }

    private static Dictionary<LatticeIndex, int>[] CreateRollLookups(
        IReadOnlyList<LatticeIndex> latticeIndices)
    {
        var result =
            new Dictionary<LatticeIndex, int>[3];

        for (var roll = 0;
             roll < result.Length;
             roll++)
        {
            var lookup =
                new Dictionary<LatticeIndex, int>(
                    latticeIndices.Count);

            for (var localIndex = 0;
                 localIndex < latticeIndices.Count;
                 localIndex++)
            {
                var key =
                    latticeIndices[localIndex]
                        .Roll(roll);

                if (!lookup.TryAdd(
                    key,
                    localIndex))
                {
                    throw new InvalidOperationException(
                        "Class III lattice index must be unique within a face.");
                }
            }

            result[roll] =
                lookup;
        }

        return result;
    }

    private static bool IsInsideOrOnBoundary(
        LocalPoint firstCorner,
        LocalPoint secondCorner,
        LocalPoint thirdCorner,
        LocalPoint point)
    {
        return SignedSide(
                firstCorner,
                secondCorner,
                point) >= 0
            && SignedSide(
                secondCorner,
                thirdCorner,
                point) >= 0
            && SignedSide(
                thirdCorner,
                firstCorner,
                point) >= 0;
    }

    private static bool IsOutsideExactlyOneSide(
        LocalPoint firstCorner,
        LocalPoint secondCorner,
        LocalPoint thirdCorner,
        LocalPoint point)
    {
        var outsideCount = 0;

        if (SignedSide(
            firstCorner,
            secondCorner,
            point) < 0)
        {
            outsideCount++;
        }

        if (SignedSide(
            secondCorner,
            thirdCorner,
            point) < 0)
        {
            outsideCount++;
        }

        if (SignedSide(
            thirdCorner,
            firstCorner,
            point) < 0)
        {
            outsideCount++;
        }

        return outsideCount == 1;
    }

    private static long SignedSide(
        LocalPoint first,
        LocalPoint second,
        LocalPoint point)
    {
        var firstX =
            checked(
                checked(2L * first.P)
                + first.Q);

        var firstY =
            (long)first.Q;

        var secondX =
            checked(
                checked(2L * second.P)
                + second.Q);

        var secondY =
            (long)second.Q;

        var pointX =
            checked(
                checked(2L * point.P)
                + point.Q);

        var pointY =
            (long)point.Q;

        return checked(
            checked(
                (secondX - firstX)
                * (pointY - firstY))
            - checked(
                (pointX - firstX)
                * (secondY - firstY)));
    }

    private static HashSet<T>[] CreateSetArray<T>(
        int count)
        where T : notnull
    {
        var result =
            new HashSet<T>[count];

        for (var index = 0;
             index < count;
             index++)
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

    private sealed record LocalPattern(
        LocalPoint[] Points,
        LocalTriangle[] Triangles,
        LatticeIndex[] LatticeIndices);

    private readonly record struct LocalPoint(
        int P,
        int Q)
        : IComparable<LocalPoint>
    {
        public int CompareTo(
            LocalPoint other)
        {
            var comparison =
                P.CompareTo(
                    other.P);

            return comparison != 0
                ? comparison
                : Q.CompareTo(
                    other.Q);
        }
    }

    private readonly record struct LocalTriangle(
        int First,
        int Second,
        int Third);

    private readonly record struct LatticeIndex(
        int First,
        int Second,
        int Third)
    {
        public LatticeIndex Roll(
            int shift)
        {
            return shift switch
            {
                0 => this,
                1 => new LatticeIndex(
                    Third,
                    First,
                    Second),
                2 => new LatticeIndex(
                    Second,
                    Third,
                    First),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(shift))
            };
        }

        public LatticeIndex Subtract(
            LatticeIndex other)
        {
            return new LatticeIndex(
                checked(First - other.First),
                checked(Second - other.Second),
                checked(Third - other.Third));
        }
    }

    private readonly record struct FaceEdgeReference(
        int FaceIndex,
        int EdgeIndex);

    private readonly record struct DirectedSeedEdge(
        int First,
        int Second);

    private readonly record struct SeedEdge(
        int First,
        int Second)
        : IComparable<SeedEdge>
    {
        public static SeedEdge Create(
            int first,
            int second)
        {
            if (first == second)
            {
                throw new ArgumentException(
                    "Seed edge endpoints must be distinct.");
            }

            return first < second
                ? new SeedEdge(
                    first,
                    second)
                : new SeedEdge(
                    second,
                    first);
        }

        public int CompareTo(
            SeedEdge other)
        {
            var comparison =
                First.CompareTo(
                    other.First);

            return comparison != 0
                ? comparison
                : Second.CompareTo(
                    other.Second);
        }
    }

    private readonly record struct SeedFace(
        int First,
        int Second,
        int Third)
        : IComparable<SeedFace>
    {
        public DirectedSeedEdge[] GetDirectedEdges()
        {
            return new[]
            {
                new DirectedSeedEdge(
                    First,
                    Second),
                new DirectedSeedEdge(
                    Second,
                    Third),
                new DirectedSeedEdge(
                    Third,
                    First)
            };
        }

        public SeedEdge[] GetUndirectedEdges()
        {
            return GetDirectedEdges()
                .Select(
                    edge =>
                        SeedEdge.Create(
                            edge.First,
                            edge.Second))
                .ToArray();
        }

        public SeedFace[] GetCyclicRotations()
        {
            return new[]
            {
                this,
                new SeedFace(
                    Second,
                    Third,
                    First),
                new SeedFace(
                    Third,
                    First,
                    Second)
            };
        }

        public SeedFace ReverseOrientation()
        {
            return new SeedFace(
                First,
                Third,
                Second);
        }

        public bool ContainsDirectedEdge(
            int first,
            int second)
        {
            return GetDirectedEdges()
                .Any(
                    edge =>
                        edge.First == first
                        && edge.Second == second);
        }

        public int CompareTo(
            SeedFace other)
        {
            var comparison =
                First.CompareTo(
                    other.First);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                Second.CompareTo(
                    other.Second);

            return comparison != 0
                ? comparison
                : Third.CompareTo(
                    other.Third);
        }
    }

    private readonly record struct CanonicalRootTriple(
        int First,
        int Second,
        int Third)
        : IComparable<CanonicalRootTriple>
    {
        public static CanonicalRootTriple Create(
            int first,
            int second,
            int third)
        {
            var values =
                new[]
                {
                    first,
                    second,
                    third
                }
                .Order()
                .ToArray();

            if (values.Distinct().Count() != 3)
            {
                throw new ArgumentException(
                    "Canonical root triangle must contain three distinct roots.");
            }

            return new CanonicalRootTriple(
                values[0],
                values[1],
                values[2]);
        }

        public int[] ToArray()
        {
            return new[]
            {
                First,
                Second,
                Third
            };
        }

        public int CompareTo(
            CanonicalRootTriple other)
        {
            var comparison =
                First.CompareTo(
                    other.First);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison =
                Second.CompareTo(
                    other.Second);

            return comparison != 0
                ? comparison
                : Third.CompareTo(
                    other.Third);
        }
    }

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

    private sealed class DisjointSet
    {
        private readonly int[] _parent;

        public DisjointSet(
            int count)
        {
            _parent =
                Enumerable.Range(
                    0,
                    count)
                    .ToArray();
        }

        public int Find(
            int value)
        {
            var current =
                value;

            while (_parent[current] != current)
            {
                _parent[current] =
                    _parent[
                        _parent[current]];

                current =
                    _parent[current];
            }

            return current;
        }

        public void Union(
            int first,
            int second)
        {
            var firstRoot =
                Find(first);

            var secondRoot =
                Find(second);

            if (firstRoot == secondRoot)
            {
                return;
            }

            if (firstRoot < secondRoot)
            {
                _parent[secondRoot] =
                    firstRoot;
            }
            else
            {
                _parent[firstRoot] =
                    secondRoot;
            }
        }
    }
}
