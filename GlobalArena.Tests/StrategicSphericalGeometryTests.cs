using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicSphericalGeometryTests
{
    private const double UnitTolerance =
        1e-12;

    private const double GeometryTolerance =
        1e-11;

    [Fact]
    public void SphericalPointRejectsZeroVector()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SphericalPoint3(
                    0d,
                    0d,
                    0d));
    }

    [Theory]
    [InlineData(double.NaN, 0d, 1d)]
    [InlineData(double.PositiveInfinity, 0d, 1d)]
    [InlineData(0d, double.NegativeInfinity, 1d)]
    public void SphericalPointRejectsNonFiniteComponents(
        double x,
        double y,
        double z)
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SphericalPoint3(
                    x,
                    y,
                    z));
    }

    [Fact]
    public void SphericalPointNormalizesFiniteVector()
    {
        var point =
            new SphericalPoint3(
                3d,
                4d,
                0d);

        AssertClose(
            0.6d,
            point.X,
            UnitTolerance);

        AssertClose(
            0.8d,
            point.Y,
            UnitTolerance);

        AssertClose(
            0d,
            point.Z,
            UnitTolerance);

        AssertUnit(
            point);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void GeneratedGeometryCoversTopologyWithCanonicalUnitSphereData(
        int m,
        int n)
    {
        var result =
            Generate(
                17UL,
                m,
                n);

        var topology =
            result.StrategicTopology;

        var geometry =
            result.StrategicSphericalGeometry;

        Assert.Equal(
            topology.Cells.Count,
            geometry.Cells.Count);

        Assert.Equal(
            topology.Vertices.Count,
            geometry.Vertices.Count);

        for (var index = 0;
             index < topology.Cells.Count;
             index++)
        {
            var cell =
                topology.Cells[index];

            var cellGeometry =
                geometry.Cells[index];

            Assert.Equal(
                cell.Id,
                cellGeometry.CellId);

            AssertUnit(
                cellGeometry.Center);

            Assert.Equal(
                cell.IncidentVertexIds.Count,
                cellGeometry.BoundaryVertexIds.Count);

            Assert.Equal(
                cell.IncidentVertexIds
                    .OrderBy(
                        id =>
                            id.Value),
                cellGeometry.BoundaryVertexIds
                    .OrderBy(
                        id =>
                            id.Value));

            Assert.Equal(
                cell.IncidentVertexIds
                    .OrderBy(
                        id =>
                            id.Value)
                    .First(),
                cellGeometry.BoundaryVertexIds[0]);

            AssertBoundaryUsesAuthoritativeEdges(
                topology,
                cell,
                cellGeometry);

            Assert.True(
                GetWindingMeasure(
                    geometry,
                    cellGeometry)
                > 0d);
        }

        for (var index = 0;
             index < topology.Vertices.Count;
             index++)
        {
            var vertex =
                topology.Vertices[index];

            var vertexGeometry =
                geometry.Vertices[index];

            Assert.Equal(
                vertex.Id,
                vertexGeometry.VertexId);

            AssertUnit(
                vertexGeometry.Position);

            AssertDualVertex(
                geometry,
                vertex,
                vertexGeometry);
        }
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void RepeatedMaterializationIsGeometricallyConsistent(
        int m,
        int n)
    {
        var first =
            Generate(
                23UL,
                m,
                n);

        var second =
            Generate(
                23UL,
                m,
                n);

        AssertEquivalentGeometry(
            first.StrategicSphericalGeometry,
            second.StrategicSphericalGeometry);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void WorldSeedDoesNotPerturbDerivedStrategicGeometry(
        int m,
        int n)
    {
        var first =
            Generate(
                1UL,
                m,
                n);

        var second =
            Generate(
                2UL,
                m,
                n);

        AssertEquivalentGeometry(
            first.StrategicSphericalGeometry,
            second.StrategicSphericalGeometry);
    }

    [Fact]
    public void ClassIIIDistinctChiralitiesBothMaterializeCanonicalGeometry()
    {
        var first =
            Generate(
                31UL,
                2,
                1);

        var mirror =
            Generate(
                31UL,
                1,
                2);

        Assert.Equal(
            first.StrategicTopology.Cells.Count,
            mirror.StrategicTopology.Cells.Count);

        Assert.Equal(
            first.StrategicSphericalGeometry.Cells.Count,
            mirror.StrategicSphericalGeometry.Cells.Count);

        Assert.All(
            first.StrategicSphericalGeometry.Cells,
            cell =>
                AssertUnit(
                    cell.Center));

        Assert.All(
            mirror.StrategicSphericalGeometry.Cells,
            cell =>
                AssertUnit(
                    cell.Center));
    }

    private static WorldGenerationResult Generate(
        ulong seed,
        int m,
        int n)
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        return generator.Generate(
            new WorldGenerationRequest(
                new WorldSeed(
                    seed),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    m,
                    n)));
    }

    private static void AssertEquivalentGeometry(
        StrategicSphericalGeometry expected,
        StrategicSphericalGeometry actual)
    {
        Assert.Equal(
            expected.Cells.Count,
            actual.Cells.Count);

        Assert.Equal(
            expected.Vertices.Count,
            actual.Vertices.Count);

        for (var index = 0;
             index < expected.Cells.Count;
             index++)
        {
            var expectedCell =
                expected.Cells[index];

            var actualCell =
                actual.Cells[index];

            Assert.Equal(
                expectedCell.CellId,
                actualCell.CellId);

            AssertPointClose(
                expectedCell.Center,
                actualCell.Center);

            Assert.Equal(
                expectedCell.BoundaryVertexIds,
                actualCell.BoundaryVertexIds);
        }

        for (var index = 0;
             index < expected.Vertices.Count;
             index++)
        {
            var expectedVertex =
                expected.Vertices[index];

            var actualVertex =
                actual.Vertices[index];

            Assert.Equal(
                expectedVertex.VertexId,
                actualVertex.VertexId);

            AssertPointClose(
                expectedVertex.Position,
                actualVertex.Position);
        }
    }

    private static void AssertBoundaryUsesAuthoritativeEdges(
        StrategicTopology topology,
        StrategicCell cell,
        StrategicCellGeometry geometry)
    {
        for (var index = 0;
             index < geometry.BoundaryVertexIds.Count;
             index++)
        {
            var first =
                geometry.BoundaryVertexIds[index];

            var second =
                geometry.BoundaryVertexIds[
                    (index + 1)
                    % geometry.BoundaryVertexIds.Count];

            var matchingEdges =
                cell.IncidentEdgeIds
                    .Select(
                        edgeId =>
                            topology.Edges[
                                checked(
                                    (int)edgeId.Value
                                    - 1)])
                    .Count(
                        edge =>
                            edge.IncidentVertexIds.Contains(
                                first)
                            && edge.IncidentVertexIds.Contains(
                                second));

            Assert.Equal(
                1,
                matchingEdges);
        }
    }

    private static void AssertDualVertex(
        StrategicSphericalGeometry geometry,
        StrategicVertex vertex,
        StrategicVertexGeometry vertexGeometry)
    {
        var centers =
            vertex.IncidentCellIds
                .Select(
                    cellId =>
                        geometry.GetCell(
                            cellId)
                            .Center)
                .ToArray();

        var firstDot =
            Dot(
                vertexGeometry.Position,
                centers[0]);

        var secondDot =
            Dot(
                vertexGeometry.Position,
                centers[1]);

        var thirdDot =
            Dot(
                vertexGeometry.Position,
                centers[2]);

        AssertClose(
            firstDot,
            secondDot,
            GeometryTolerance);

        AssertClose(
            firstDot,
            thirdDot,
            GeometryTolerance);

        var outward =
            new[]
            {
                centers[0].X
                    + centers[1].X
                    + centers[2].X,
                centers[0].Y
                    + centers[1].Y
                    + centers[2].Y,
                centers[0].Z
                    + centers[1].Z
                    + centers[2].Z
            };

        var outwardDot =
            vertexGeometry.Position.X
                * outward[0]
            + vertexGeometry.Position.Y
                * outward[1]
            + vertexGeometry.Position.Z
                * outward[2];

        Assert.True(
            outwardDot > 0d);
    }

    private static double GetWindingMeasure(
        StrategicSphericalGeometry geometry,
        StrategicCellGeometry cell)
    {
        var measure =
            0d;

        for (var index = 0;
             index < cell.BoundaryVertexIds.Count;
             index++)
        {
            var first =
                geometry.GetVertex(
                    cell.BoundaryVertexIds[index])
                    .Position;

            var second =
                geometry.GetVertex(
                    cell.BoundaryVertexIds[
                        (index + 1)
                        % cell.BoundaryVertexIds.Count])
                    .Position;

            var crossX =
                first.Y * second.Z
                - first.Z * second.Y;

            var crossY =
                first.Z * second.X
                - first.X * second.Z;

            var crossZ =
                first.X * second.Y
                - first.Y * second.X;

            measure +=
                cell.Center.X * crossX
                + cell.Center.Y * crossY
                + cell.Center.Z * crossZ;
        }

        return measure;
    }

    private static double Dot(
        SphericalPoint3 first,
        SphericalPoint3 second)
    {
        return first.X * second.X
            + first.Y * second.Y
            + first.Z * second.Z;
    }

    private static void AssertUnit(
        SphericalPoint3 point)
    {
        Assert.True(
            double.IsFinite(point.X)
            && double.IsFinite(point.Y)
            && double.IsFinite(point.Z));

        AssertClose(
            1d,
            Math.Sqrt(
                point.LengthSquared),
            UnitTolerance);
    }

    private static void AssertPointClose(
        SphericalPoint3 expected,
        SphericalPoint3 actual)
    {
        AssertClose(
            expected.X,
            actual.X,
            GeometryTolerance);

        AssertClose(
            expected.Y,
            actual.Y,
            GeometryTolerance);

        AssertClose(
            expected.Z,
            actual.Z,
            GeometryTolerance);
    }

    private static void AssertClose(
        double expected,
        double actual,
        double tolerance)
    {
        Assert.True(
            Math.Abs(
                expected - actual)
            <= tolerance,
            $"Expected {expected:R} and {actual:R} to differ by no more than {tolerance:R}.");
    }
}
