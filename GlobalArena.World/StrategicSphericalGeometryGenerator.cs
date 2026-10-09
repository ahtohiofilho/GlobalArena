namespace GlobalArena.World;

internal static class StrategicSphericalGeometryGenerator
{
    public static StrategicSphericalGeometry Generate(
        StrategicTopology topology)
    {
        ArgumentNullException.ThrowIfNull(
            topology);

        if (topology.CellConstructionProvenance.Count
            != topology.Cells.Count)
        {
            throw new InvalidOperationException(
                "Strategic topology must retain one construction provenance carrier per cell before spherical geometry can be materialized.");
        }

        var cellCenters =
            new SphericalPoint3[
                topology.Cells.Count];

        for (var index = 0;
             index < topology.Cells.Count;
             index++)
        {
            cellCenters[index] =
                CreateCellCenter(
                    topology.CellConstructionProvenance[
                        index]);
        }

        var vertexGeometry =
            new StrategicVertexGeometry[
                topology.Vertices.Count];

        for (var index = 0;
             index < topology.Vertices.Count;
             index++)
        {
            var vertex =
                topology.Vertices[index];

            var position =
                CreateDualVertexPosition(
                    vertex,
                    cellCenters);

            vertexGeometry[index] =
                new StrategicVertexGeometry(
                    vertex.Id,
                    position);
        }

        var cellGeometry =
            new StrategicCellGeometry[
                topology.Cells.Count];

        for (var index = 0;
             index < topology.Cells.Count;
             index++)
        {
            var cell =
                topology.Cells[index];

            var boundary =
                CreateCanonicalBoundary(
                    topology,
                    cell,
                    cellCenters[index],
                    vertexGeometry);

            cellGeometry[index] =
                new StrategicCellGeometry(
                    cell.Id,
                    cellCenters[index],
                    boundary);
        }

        return new StrategicSphericalGeometry(
            cellGeometry,
            vertexGeometry);
    }

    private static SphericalPoint3 CreateCellCenter(
        StrategicCellConstructionProvenance provenance)
    {
        if (!provenance.IsValid)
        {
            throw new InvalidOperationException(
                "Strategic cell construction provenance is invalid.");
        }

        var x = 0d;
        var y = 0d;
        var z = 0d;

        foreach (var contribution in
            provenance.GetContributions())
        {
            var direction =
                CanonicalIcosahedronGeometry
                    .GetSeedDirection(
                        contribution.SeedVertexId);

            var weight =
                (double)contribution.Weight;

            x +=
                weight * direction.X;

            y +=
                weight * direction.Y;

            z +=
                weight * direction.Z;
        }

        return new SphericalPoint3(
            x,
            y,
            z);
    }

    private static SphericalPoint3 CreateDualVertexPosition(
        StrategicVertex vertex,
        IReadOnlyList<SphericalPoint3> cellCenters)
    {
        var incident =
            vertex.IncidentCellIds;

        if (incident.Count != 3)
        {
            throw new InvalidOperationException(
                "Strategic dual vertex requires exactly three incident cells.");
        }

        var first =
            GetCellCenter(
                cellCenters,
                incident[0]);

        var second =
            GetCellCenter(
                cellCenters,
                incident[1]);

        var third =
            GetCellCenter(
                cellCenters,
                incident[2]);

        var firstX =
            second.X - first.X;

        var firstY =
            second.Y - first.Y;

        var firstZ =
            second.Z - first.Z;

        var secondX =
            third.X - first.X;

        var secondY =
            third.Y - first.Y;

        var secondZ =
            third.Z - first.Z;

        var normalX =
            firstY * secondZ
            - firstZ * secondY;

        var normalY =
            firstZ * secondX
            - firstX * secondZ;

        var normalZ =
            firstX * secondY
            - firstY * secondX;

        var outwardX =
            first.X
            + second.X
            + third.X;

        var outwardY =
            first.Y
            + second.Y
            + third.Y;

        var outwardZ =
            first.Z
            + second.Z
            + third.Z;

        var outwardDot =
            normalX * outwardX
            + normalY * outwardY
            + normalZ * outwardZ;

        if (!double.IsFinite(outwardDot)
            || outwardDot == 0d)
        {
            throw new InvalidOperationException(
                "Strategic dual vertex geometry is degenerate.");
        }

        if (outwardDot < 0d)
        {
            normalX =
                -normalX;

            normalY =
                -normalY;

            normalZ =
                -normalZ;
        }

        return new SphericalPoint3(
            normalX,
            normalY,
            normalZ);
    }

    private static StrategicVertexId[] CreateCanonicalBoundary(
        StrategicTopology topology,
        StrategicCell cell,
        SphericalPoint3 center,
        IReadOnlyList<StrategicVertexGeometry> vertexGeometry)
    {
        var incidentVertexIds =
            cell.IncidentVertexIds
                .OrderBy(
                    id =>
                        id.Value)
                .ToArray();

        var incidentSet =
            new HashSet<StrategicVertexId>(
                incidentVertexIds);

        var adjacent =
            incidentVertexIds.ToDictionary(
                id => id,
                _ => new List<StrategicVertexId>(2));

        foreach (var edgeId in
            cell.IncidentEdgeIds)
        {
            var edge =
                topology.Edges[
                    checked((int)edgeId.Value - 1)];

            if (!edge.IncidentCellIds.Contains(
                cell.Id))
            {
                throw new InvalidOperationException(
                    "Strategic cell incidence references an edge that does not reference the cell.");
            }

            var first =
                edge.IncidentVertexIds[0];

            var second =
                edge.IncidentVertexIds[1];

            if (!incidentSet.Contains(first)
                || !incidentSet.Contains(second))
            {
                throw new InvalidOperationException(
                    "Strategic cell boundary edge references a non-incident vertex.");
            }

            adjacent[first].Add(
                second);

            adjacent[second].Add(
                first);
        }

        if (adjacent.Values.Any(
            neighbors =>
                neighbors.Count != 2
                || neighbors[0] == neighbors[1]))
        {
            throw new InvalidOperationException(
                "Strategic cell incidence does not form one simple polygon cycle.");
        }

        var start =
            incidentVertexIds[0];

        var next =
            adjacent[start]
                .OrderBy(
                    id =>
                        id.Value)
                .First();

        var cycle =
            new List<StrategicVertexId>(
                incidentVertexIds.Length);

        var previous =
            default(StrategicVertexId);

        var current =
            start;

        for (var index = 0;
             index < incidentVertexIds.Length;
             index++)
        {
            cycle.Add(
                current);

            var neighbors =
                adjacent[current];

            StrategicVertexId candidate;

            if (index == 0)
            {
                candidate =
                    next;
            }
            else
            {
                candidate =
                    neighbors[0] == previous
                        ? neighbors[1]
                        : neighbors[0];
            }

            previous =
                current;

            current =
                candidate;
        }

        if (current != start
            || cycle.Distinct().Count()
                != incidentVertexIds.Length
            || !cycle
                .OrderBy(
                    id =>
                        id.Value)
                .SequenceEqual(
                    incidentVertexIds))
        {
            throw new InvalidOperationException(
                "Strategic cell incidence failed canonical cycle reconstruction.");
        }

        var winding =
            GetWindingMeasure(
                center,
                cycle,
                vertexGeometry);

        if (!double.IsFinite(winding)
            || winding == 0d)
        {
            throw new InvalidOperationException(
                "Strategic cell spherical winding is degenerate.");
        }

        if (winding < 0d)
        {
            cycle =
                new List<StrategicVertexId>(
                    new[]
                    {
                        cycle[0]
                    }
                    .Concat(
                        cycle
                            .Skip(1)
                            .Reverse()));

            winding =
                GetWindingMeasure(
                    center,
                    cycle,
                    vertexGeometry);
        }

        if (!double.IsFinite(winding)
            || winding <= 0d)
        {
            throw new InvalidOperationException(
                "Strategic cell boundary could not be normalized to outward counter-clockwise winding.");
        }

        return cycle.ToArray();
    }

    private static double GetWindingMeasure(
        SphericalPoint3 center,
        IReadOnlyList<StrategicVertexId> boundary,
        IReadOnlyList<StrategicVertexGeometry> vertexGeometry)
    {
        var measure =
            0d;

        for (var index = 0;
             index < boundary.Count;
             index++)
        {
            var first =
                GetVertexPosition(
                    vertexGeometry,
                    boundary[index]);

            var second =
                GetVertexPosition(
                    vertexGeometry,
                    boundary[
                        (index + 1)
                        % boundary.Count]);

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
                center.X * crossX
                + center.Y * crossY
                + center.Z * crossZ;
        }

        return measure;
    }

    private static SphericalPoint3 GetCellCenter(
        IReadOnlyList<SphericalPoint3> centers,
        StrategicCellId cellId)
    {
        if (!cellId.IsValid
            || cellId.Value > (ulong)centers.Count)
        {
            throw new InvalidOperationException(
                "Strategic vertex references an unknown strategic cell.");
        }

        return centers[
            checked((int)cellId.Value - 1)];
    }

    private static SphericalPoint3 GetVertexPosition(
        IReadOnlyList<StrategicVertexGeometry> vertices,
        StrategicVertexId vertexId)
    {
        if (!vertexId.IsValid
            || vertexId.Value > (ulong)vertices.Count)
        {
            throw new InvalidOperationException(
                "Strategic cell references an unknown strategic vertex.");
        }

        return vertices[
            checked((int)vertexId.Value - 1)]
            .Position;
    }
}
