using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SharedBorderStageValidationTests
{
    [Fact]
    public void EachStrategicCellIncidenceSetMatchesIncidentEdgeIds()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        foreach (var strategicCell in topology.Cells)
        {
            var actualIncidentEdgeIds =
                aggregate.SharedBorderIncidences
                    .Where(
                        incidence =>
                            incidence.IncidentRegions.Any(
                                region =>
                                    region.StrategicCellId
                                    == strategicCell.Id))
                    .Select(
                        incidence =>
                            incidence.StrategicEdge.Id)
                    .OrderBy(
                        strategicEdgeId =>
                            strategicEdgeId.Value)
                    .ToArray();

            Assert.Equal(
                strategicCell.IncidentEdgeIds,
                actualIncidentEdgeIds);
        }
    }

    [Fact]
    public void PentagonAndHexagonRegionsHaveExpectedDerivedIncidenceDegree()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        var cellsById =
            topology.Cells.ToDictionary(
                strategicCell =>
                    strategicCell.Id);

        foreach (var region in aggregate.TacticalRegions)
        {
            var strategicCell =
                cellsById[
                    region.StrategicCellId];

            var expectedDegree =
                strategicCell.Kind switch
                {
                    StrategicCellKind.Pentagon => 5,
                    StrategicCellKind.Hexagon => 6,
                    _ => throw new InvalidOperationException(
                        "Unsupported strategic cell kind.")
                };

            var actualDegree =
                aggregate.SharedBorderIncidences.Count(
                    incidence =>
                        incidence.IncidentRegions.Any(
                            incidentRegion =>
                                incidentRegion.StrategicCellId
                                == region.StrategicCellId));

            Assert.Equal(
                expectedDegree,
                actualDegree);
        }
    }

    [Fact]
    public void HandshakeCountHoldsAndEveryIncidenceIsObservedByExactlyTwoRegions()
    {
        var topology =
            CreateTopology(
                2,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        var totalRegionalObservations =
            aggregate.TacticalRegions.Sum(
                region =>
                    aggregate.SharedBorderIncidences.Count(
                        incidence =>
                            incidence.IncidentRegions.Any(
                                incidentRegion =>
                                    incidentRegion.StrategicCellId
                                    == region.StrategicCellId)));

        Assert.Equal(
            checked(
                2 * topology.Edges.Count),
            totalRegionalObservations);

        foreach (var incidence in aggregate.SharedBorderIncidences)
        {
            Assert.Equal(
                2,
                incidence.IncidentRegions.Count);

            Assert.Equal(
                2,
                incidence.IncidentRegions
                    .Select(
                        region =>
                            region.StrategicCellId)
                    .Distinct()
                    .Count());

            Assert.Equal(
                incidence.StrategicEdge.IncidentCellIds,
                incidence.IncidentRegions
                    .Select(
                        region =>
                            region.StrategicCellId)
                    .ToArray());
        }
    }

    [Fact]
    public void IntegratedBorderElementIdsAreGloballyUniqueAndEdgeLocalWithOrdinalOneReference()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        var elementIds =
            aggregate.SharedBorderBands
                .SelectMany(
                    band =>
                        band.Elements)
                .Select(
                    element =>
                        element.Id)
                .ToArray();

        Assert.Equal(
            elementIds.Length,
            elementIds
                .Distinct()
                .Count());

        foreach (var band in aggregate.SharedBorderBands)
        {
            Assert.All(
                band.Elements,
                element =>
                    Assert.Equal(
                        band.StrategicEdgeId,
                        element.Id.StrategicEdgeId));

            Assert.Contains(
                band.Elements,
                element =>
                    element.Id.LocalOrdinal
                    == 1UL);
        }
    }

    [Fact]
    public void TacticalAdjacencyRemainsStrictlyParentLocalInIntegratedAggregate()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        foreach (var region in aggregate.TacticalRegions)
        {
            var localCellIds =
                region.Cells
                    .Select(
                        cell =>
                            cell.Id)
                    .ToHashSet();

            foreach (var cell in region.Cells)
            {
                Assert.Equal(
                    region.StrategicCellId,
                    cell.Id.ParentStrategicCellId);

                Assert.All(
                    cell.AdjacentCellIds,
                    adjacentCellId =>
                    {
                        Assert.Equal(
                            region.StrategicCellId,
                            adjacentCellId.ParentStrategicCellId);

                        Assert.Contains(
                            adjacentCellId,
                            localCellIds);
                    });
            }
        }
    }

    [Fact]
    public void IndependentlyMaterializedFullPipelinesHaveIdenticalCanonicalSignature()
    {
        var parameters =
            new GoldbergParameters(
                3,
                2);

        var first =
            CreateAggregate(
                GoldbergStrategicTopologyGenerator.Generate(
                    parameters));

        var second =
            CreateAggregate(
                GoldbergStrategicTopologyGenerator.Generate(
                    parameters));

        Assert.NotSame(
            first.StrategicTopology,
            second.StrategicTopology);

        Assert.Equal(
            CreateCanonicalSignature(
                first),
            CreateCanonicalSignature(
                second));
    }

    [Theory]
    [InlineData(0, 2, 42, 120)]
    [InlineData(1, 2, 72, 210)]
    [InlineData(2, 1, 72, 210)]
    [InlineData(3, 1, 132, 390)]
    public void AdditionalRepresentativeTopologiesHaveExpectedIntegratedCoverage(
        int m,
        int n,
        int expectedRegionCount,
        int expectedBandCount)
    {
        var topology =
            CreateTopology(
                m,
                n);

        var aggregate =
            CreateAggregate(
                topology);

        Assert.Equal(
            expectedRegionCount,
            topology.Cells.Count);

        Assert.Equal(
            expectedBandCount,
            topology.Edges.Count);

        Assert.Equal(
            expectedRegionCount,
            aggregate.TacticalRegions.Count);

        Assert.Equal(
            expectedBandCount,
            aggregate.SharedBorderBands.Count);

        Assert.Equal(
            expectedBandCount,
            aggregate.SharedBorderIncidences.Count);

        Assert.All(
            aggregate.SharedBorderIncidences,
            incidence =>
                Assert.Equal(
                    2,
                    incidence.IncidentRegions.Count));
    }

    private static StrategicTopology CreateTopology(
        int m,
        int n)
    {
        return GoldbergStrategicTopologyGenerator.Generate(
            new GoldbergParameters(
                m,
                n));
    }

    private static StrategicTacticalBorderAggregate CreateAggregate(
        StrategicTopology topology)
    {
        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        return new StrategicTacticalBorderAggregate(
            topology,
            regions,
            bands);
    }

    private static string[] CreateCanonicalSignature(
        StrategicTacticalBorderAggregate aggregate)
    {
        var strategicSignature =
            aggregate.StrategicTopology.Cells
                .Select(
                    strategicCell =>
                        strategicCell.Id.Value
                        + ":"
                        + string.Join(
                            ",",
                            strategicCell.IncidentEdgeIds.Select(
                                strategicEdgeId =>
                                    strategicEdgeId.Value)))
                .ToArray();

        var tacticalSignature =
            aggregate.TacticalRegions
                .Select(
                    region =>
                        region.StrategicCellId.Value
                        + ":"
                        + string.Join(
                            "|",
                            region.Cells.Select(
                                cell =>
                                    cell.Id.LocalOrdinal
                                    + ">"
                                    + string.Join(
                                        ",",
                                        cell.AdjacentCellIds.Select(
                                            adjacentCellId =>
                                                adjacentCellId.LocalOrdinal)))))
                .ToArray();

        var borderSignature =
            aggregate.SharedBorderBands
                .Select(
                    band =>
                        band.StrategicEdgeId.Value
                        + ":"
                        + string.Join(
                            ",",
                            band.Elements.Select(
                                element =>
                                    element.Id.StrategicEdgeId.Value
                                    + "."
                                    + element.Id.LocalOrdinal)))
                .ToArray();

        var incidenceSignature =
            aggregate.SharedBorderIncidences
                .Select(
                    incidence =>
                        incidence.StrategicEdge.Id.Value
                        + ":"
                        + incidence.SharedBorderBand.StrategicEdgeId.Value
                        + ":"
                        + incidence.IncidentRegions[0].StrategicCellId.Value
                        + ","
                        + incidence.IncidentRegions[1].StrategicCellId.Value)
                .ToArray();

        return new[]
        {
            "S:"
            + string.Join(
                ";",
                strategicSignature),
            "T:"
            + string.Join(
                ";",
                tacticalSignature),
            "B:"
            + string.Join(
                ";",
                borderSignature),
            "I:"
            + string.Join(
                ";",
                incidenceSignature)
        };
    }
}
