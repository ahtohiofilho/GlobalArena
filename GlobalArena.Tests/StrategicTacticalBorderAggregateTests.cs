using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicTacticalBorderAggregateTests
{
    [Fact]
    public void NullStrategicTopologyIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology);

        var bands =
            CreateBands(
                topology);

        Assert.Throws<ArgumentNullException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    null!,
                    regions,
                    bands));
    }

    [Fact]
    public void NullTacticalRegionEnumerableIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var bands =
            CreateBands(
                topology);

        Assert.Throws<ArgumentNullException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    null!,
                    bands));
    }

    [Fact]
    public void NullSharedBorderBandEnumerableIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology);

        Assert.Throws<ArgumentNullException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    null!));
    }

    [Fact]
    public void NullTacticalRegionElementIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology)
                .ToList();

        regions[0] =
            null!;

        var bands =
            CreateBands(
                topology);

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void NullSharedBorderBandElementIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology);

        var bands =
            CreateBands(
                topology)
                .ToList();

        bands[0] =
            null!;

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void MissingTacticalRegionIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology)
                .Skip(1)
                .ToArray();

        var bands =
            CreateBands(
                topology);

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void DuplicateTacticalRegionParentIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology)
                .ToArray();

        regions[^1] =
            MinimalTacticalRegionGraphGenerator.Generate(
                regions[0].StrategicCellId);

        var bands =
            CreateBands(
                topology);

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void ForeignTacticalRegionParentIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology)
                .ToArray();

        regions[^1] =
            MinimalTacticalRegionGraphGenerator.Generate(
                new StrategicCellId(
                    (ulong)topology.Cells.Count
                    + 1UL));

        var bands =
            CreateBands(
                topology);

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void MissingSharedBorderBandIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology);

        var bands =
            CreateBands(
                topology)
                .Skip(1)
                .ToArray();

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void DuplicateSharedBorderBandEdgeIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology);

        var bands =
            CreateBands(
                topology)
                .ToArray();

        bands[^1] =
            CreateReferenceBand(
                bands[0].StrategicEdgeId);

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void ForeignSharedBorderBandEdgeIsRejected()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            CreateRegions(
                topology);

        var bands =
            CreateBands(
                topology)
                .ToArray();

        bands[^1] =
            CreateReferenceBand(
                new StrategicEdgeId(
                    (ulong)topology.Edges.Count
                    + 1UL));

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicTacticalBorderAggregate(
                    topology,
                    regions,
                    bands));
    }

    [Fact]
    public void ShuffledInputsAreCanonicalizedToStrategicTopologyOrder()
    {
        var topology =
            CreateTopology(
                2,
                1);

        var regions =
            CreateRegions(
                topology)
                .Reverse()
                .ToArray();

        var bands =
            CreateBands(
                topology)
                .Reverse()
                .ToArray();

        var aggregate =
            new StrategicTacticalBorderAggregate(
                topology,
                regions,
                bands);

        Assert.Equal(
            topology.Cells
                .Select(
                    cell =>
                        cell.Id)
                .ToArray(),
            aggregate.TacticalRegions
                .Select(
                    region =>
                        region.StrategicCellId)
                .ToArray());

        Assert.Equal(
            topology.Edges
                .Select(
                    edge =>
                        edge.Id)
                .ToArray(),
            aggregate.SharedBorderBands
                .Select(
                    band =>
                        band.StrategicEdgeId)
                .ToArray());
    }

    [Fact]
    public void DerivedIncidenceCountAndOrderMatchStrategicEdges()
    {
        var topology =
            CreateTopology(
                2,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        Assert.Equal(
            topology.Edges.Count,
            aggregate.SharedBorderIncidences.Count);

        Assert.Equal(
            topology.Edges
                .Select(
                    edge =>
                        edge.Id)
                .ToArray(),
            aggregate.SharedBorderIncidences
                .Select(
                    incidence =>
                        incidence.StrategicEdge.Id)
                .ToArray());

        for (var index = 0; index < topology.Edges.Count; index++)
        {
            Assert.Same(
                topology.Edges[index],
                aggregate.SharedBorderIncidences[index].StrategicEdge);
        }
    }

    [Fact]
    public void EachDerivedIncidenceReferencesMatchingAuthoritativeBand()
    {
        var topology =
            CreateTopology(
                2,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        for (var index = 0; index < topology.Edges.Count; index++)
        {
            var incidence =
                aggregate.SharedBorderIncidences[index];

            Assert.Same(
                aggregate.SharedBorderBands[index],
                incidence.SharedBorderBand);

            Assert.Equal(
                incidence.StrategicEdge.Id,
                incidence.SharedBorderBand.StrategicEdgeId);
        }
    }

    [Fact]
    public void EachDerivedIncidenceResolvesTwoRegionsInIncidentCellOrder()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var aggregate =
            CreateAggregate(
                topology);

        var regionsById =
            aggregate.TacticalRegions
                .ToDictionary(
                    region =>
                        region.StrategicCellId);

        foreach (var incidence in aggregate.SharedBorderIncidences)
        {
            Assert.Equal(
                2,
                incidence.IncidentRegions.Count);

            Assert.Equal(
                incidence.StrategicEdge.IncidentCellIds[0],
                incidence.IncidentRegions[0].StrategicCellId);

            Assert.Equal(
                incidence.StrategicEdge.IncidentCellIds[1],
                incidence.IncidentRegions[1].StrategicCellId);

            Assert.Same(
                regionsById[
                    incidence.StrategicEdge.IncidentCellIds[0]],
                incidence.IncidentRegions[0]);

            Assert.Same(
                regionsById[
                    incidence.StrategicEdge.IncidentCellIds[1]],
                incidence.IncidentRegions[1]);
        }
    }

    [Fact]
    public void ExposedCollectionsAreReadOnlySnapshotsAndPreserveSuppliedReferences()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regionSource =
            CreateRegions(
                topology)
                .Reverse()
                .ToList();

        var bandSource =
            CreateBands(
                topology)
                .Reverse()
                .ToList();

        var regionReferences =
            regionSource.ToDictionary(
                region =>
                    region.StrategicCellId);

        var bandReferences =
            bandSource.ToDictionary(
                band =>
                    band.StrategicEdgeId);

        var aggregate =
            new StrategicTacticalBorderAggregate(
                topology,
                regionSource,
                bandSource);

        regionSource.Clear();
        bandSource.Clear();

        Assert.Same(
            topology,
            aggregate.StrategicTopology);

        Assert.Equal(
            topology.Cells.Count,
            aggregate.TacticalRegions.Count);

        Assert.Equal(
            topology.Edges.Count,
            aggregate.SharedBorderBands.Count);

        Assert.Equal(
            topology.Edges.Count,
            aggregate.SharedBorderIncidences.Count);

        foreach (var region in aggregate.TacticalRegions)
        {
            Assert.Same(
                regionReferences[region.StrategicCellId],
                region);
        }

        foreach (var band in aggregate.SharedBorderBands)
        {
            Assert.Same(
                bandReferences[band.StrategicEdgeId],
                band);
        }

        AssertReadOnly(
            aggregate.TacticalRegions);

        AssertReadOnly(
            aggregate.SharedBorderBands);

        AssertReadOnly(
            aggregate.SharedBorderIncidences);

        AssertReadOnly(
            aggregate.SharedBorderIncidences[0].IncidentRegions);
    }

    [Fact]
    public void RepeatedConstructionProducesIdenticalCanonicalSignature()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var regions =
            CreateRegions(
                topology);

        var bands =
            CreateBands(
                topology);

        var first =
            new StrategicTacticalBorderAggregate(
                topology,
                regions,
                bands);

        var second =
            new StrategicTacticalBorderAggregate(
                topology,
                regions.Reverse(),
                bands.Reverse());

        Assert.Equal(
            CreateSignature(
                first),
            CreateSignature(
                second));
    }

    [Theory]
    [InlineData(2, 0, 42, 120)]
    [InlineData(2, 2, 122, 360)]
    [InlineData(3, 2, 192, 570)]
    public void RepresentativeTopologiesHaveExpectedJointCoverage(
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

    private static IReadOnlyList<TacticalRegion> CreateRegions(
        StrategicTopology topology)
    {
        return StrategicTacticalRegionMaterializer.Materialize(
            topology);
    }

    private static IReadOnlyList<SharedBorderBand> CreateBands(
        StrategicTopology topology)
    {
        return StrategicEdgeSharedBorderBandMaterializer.Materialize(
            topology);
    }

    private static StrategicTacticalBorderAggregate CreateAggregate(
        StrategicTopology topology)
    {
        return new StrategicTacticalBorderAggregate(
            topology,
            CreateRegions(
                topology),
            CreateBands(
                topology));
    }

    private static SharedBorderBand CreateReferenceBand(
        StrategicEdgeId strategicEdgeId)
    {
        return new SharedBorderBand(
            strategicEdgeId,
            new[]
            {
                new SharedBorderElement(
                    new SharedBorderElementId(
                        strategicEdgeId,
                        1UL))
            });
    }

    private static void AssertReadOnly<T>(
        IReadOnlyList<T> values)
    {
        var mutableView =
            Assert.IsAssignableFrom<IList<T>>(
                values);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.RemoveAt(
                    0));
    }

    private static string[] CreateSignature(
        StrategicTacticalBorderAggregate aggregate)
    {
        return new[]
        {
            "R:"
            + string.Join(
                ",",
                aggregate.TacticalRegions.Select(
                    region =>
                        region.StrategicCellId.Value)),
            "B:"
            + string.Join(
                ",",
                aggregate.SharedBorderBands.Select(
                    band =>
                        band.StrategicEdgeId.Value)),
            "I:"
            + string.Join(
                "|",
                aggregate.SharedBorderIncidences.Select(
                    incidence =>
                        incidence.StrategicEdge.Id.Value
                        + ":"
                        + incidence.SharedBorderBand.StrategicEdgeId.Value
                        + ":"
                        + incidence.IncidentRegions[0].StrategicCellId.Value
                        + ","
                        + incidence.IncidentRegions[1].StrategicCellId.Value))
        };
    }
}
