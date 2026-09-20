using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GoldbergScaledRefinementReferenceMapTests
{
    [Fact]
    public void NullRefinementIsRejectedByMapper()
    {
        Assert.Throws<ArgumentNullException>(
            () => GoldbergScaledRefinementReferenceMapper.Materialize(
                null!));
    }

    [Fact]
    public void ClassIScaleThreePairIsUnsupportedInThisTranche()
    {
        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    0),
                new GoldbergParameters(
                    3,
                    0));

        Assert.Throws<NotSupportedException>(
            () => GoldbergScaledRefinementReferenceMapper.Materialize(
                refinement));
    }

    [Fact]
    public void ClassIIPairIsUnsupportedInThisTranche()
    {
        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    1),
                new GoldbergParameters(
                    2,
                    2));

        Assert.Throws<NotSupportedException>(
            () => GoldbergScaledRefinementReferenceMapper.Materialize(
                refinement));
    }

    [Fact]
    public void ClassIIIPairIsUnsupportedInThisTranche()
    {
        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(
                    2,
                    1),
                new GoldbergParameters(
                    4,
                    2));

        Assert.Throws<NotSupportedException>(
            () => GoldbergScaledRefinementReferenceMapper.Materialize(
                refinement));
    }

    [Fact]
    public void G10ToG20ProducesExactlyTwelveReferencesInSeedOrder()
    {
        var map =
            CreateReferenceMap();

        Assert.Equal(
            12,
            map.CellReferences.Count);

        Assert.Equal(
            Enumerable.Range(
                1,
                12),
            map.CellReferences.Select(
                reference =>
                    reference.SeedVertexId.Value));
    }

    [Fact]
    public void AllCoarseCellsAreCoveredExactlyOnce()
    {
        var map =
            CreateReferenceMap();

        var coarseTopology =
            GoldbergStrategicTopologyGenerator.Generate(
                map.Refinement.CoarseParameters);

        Assert.Equal(
            coarseTopology.Cells
                .Select(cell => cell.Id)
                .OrderBy(id => id.Value),
            map.CellReferences
                .Select(reference => reference.CoarseCellId)
                .OrderBy(id => id.Value));
    }

    [Fact]
    public void FineReferenceIdsAreUnique()
    {
        var map =
            CreateReferenceMap();

        Assert.Equal(
            12,
            map.CellReferences
                .Select(reference => reference.FineCellId)
                .Distinct()
                .Count());
    }

    [Fact]
    public void ReferencedFineCellsAreTheTwelvePentagons()
    {
        var map =
            CreateReferenceMap();

        var fineTopology =
            GoldbergStrategicTopologyGenerator.Generate(
                map.Refinement.FineParameters);

        var referencedFineIds =
            map.CellReferences
                .Select(reference => reference.FineCellId)
                .OrderBy(id => id.Value)
                .ToArray();

        var pentagonIds =
            fineTopology.Cells
                .Where(
                    cell =>
                        cell.Kind
                        == StrategicCellKind.Pentagon)
                .Select(cell => cell.Id)
                .OrderBy(id => id.Value)
                .ToArray();

        Assert.Equal(
            pentagonIds,
            referencedFineIds);
    }

    [Fact]
    public void CanonicalReferenceVectorMatchesFrozenOracle()
    {
        var map =
            CreateReferenceMap();

        var expected =
            new[]
            {
                "1:1->6",
                "2:2->11",
                "3:3->15",
                "4:4->19",
                "5:5->23",
                "6:6->26",
                "7:7->30",
                "8:8->33",
                "9:9->36",
                "10:10->39",
                "11:11->41",
                "12:12->42"
            };

        Assert.Equal(
            expected,
            map.CellReferences.Select(
                reference =>
                    $"{reference.SeedVertexId.Value}:"
                    + $"{reference.CoarseCellId.Value}->"
                    + $"{reference.FineCellId.Value}"));
    }

    [Fact]
    public void RepeatedMaterializationIsCanonicalAndReadOnly()
    {
        var first =
            CreateReferenceMap();

        var second =
            CreateReferenceMap();

        Assert.Equal(
            CreateSignature(first),
            CreateSignature(second));

        var mutableView =
            Assert.IsAssignableFrom<IList<GoldbergScaledCellReference>>(
                first.CellReferences);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () => mutableView.Add(
                first.CellReferences[0]));
    }

    private static GoldbergScaledRefinementReferenceMap CreateReferenceMap()
    {
        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    0),
                new GoldbergParameters(
                    2,
                    0));

        return GoldbergScaledRefinementReferenceMapper.Materialize(
            refinement);
    }

    private static string CreateSignature(
        GoldbergScaledRefinementReferenceMap map)
    {
        return string.Join(
            "|",
            map.CellReferences.Select(
                reference =>
                    $"{reference.SeedVertexId.Value}:"
                    + $"{reference.CoarseCellId.Value}:"
                    + $"{reference.FineCellId.Value}"));
    }
}
