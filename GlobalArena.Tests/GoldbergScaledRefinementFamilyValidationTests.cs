using GlobalArena.World;
using System.Security.Cryptography;
using System.Text;

namespace GlobalArena.Tests;

public sealed class GoldbergScaledRefinementFamilyValidationTests
{
    private static readonly PairSpec[] RepresentativePairs =
    {
        new("ClassIBaseScale2", 1, 0, 2, 0, 2),
        new("ClassIGeneralScale2", 2, 0, 4, 0, 2),
        new("ClassIInvertedScale2", 0, 2, 0, 4, 2),
        new("ClassIScale3", 1, 0, 3, 0, 3),
        new("ClassIIBaseScale2", 1, 1, 2, 2, 2),
        new("ClassIIGeneralScale2", 2, 2, 4, 4, 2),
        new("ClassIIScale3", 1, 1, 3, 3, 3),
        new("ClassIIIRightScale2", 2, 1, 4, 2, 2),
        new("ClassIIILeftScale2", 1, 2, 2, 4, 2),
        new("ClassIIIRightScale3", 2, 1, 6, 3, 3),
        new("ClassIIILeftScale3", 1, 2, 3, 6, 3)
    };

    [Fact]
    public void ClassIBaseScaleTwoPreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[0]);
    }

    [Fact]
    public void ClassIGeneralScaleTwoPreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[1]);
    }

    [Fact]
    public void ClassIInvertedAxisScaleTwoPreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[2]);
    }

    [Fact]
    public void ClassIScaleThreePreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[3]);
    }

    [Fact]
    public void ClassIIBaseScaleTwoPreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[4]);
    }

    [Fact]
    public void ClassIIGeneralScaleTwoPreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[5]);
    }

    [Fact]
    public void ClassIIScaleThreePreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[6]);
    }

    [Fact]
    public void ClassIIIRightChiralityScaleTwoPreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[7]);
    }

    [Fact]
    public void ClassIIILeftChiralityScaleTwoPreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[8]);
    }

    [Fact]
    public void ClassIIIRightChiralityScaleThreePreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[9]);
    }

    [Fact]
    public void ClassIIILeftChiralityScaleThreePreservesPublicScaledInvariants()
    {
        AssertPairPublicInvariants(
            RepresentativePairs[10]);
    }

    [Fact]
    public void AllRepresentativeTopologiesReproduceIdenticalCanonicalSignatures()
    {
        foreach (var pair in RepresentativePairs)
        {
            var coarseParameters =
                new GoldbergParameters(
                    pair.CoarseM,
                    pair.CoarseN);

            var fineParameters =
                new GoldbergParameters(
                    pair.FineM,
                    pair.FineN);

            Assert.Equal(
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        coarseParameters)),
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        coarseParameters)));

            Assert.Equal(
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        fineParameters)),
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        fineParameters)));
        }
    }

    [Fact]
    public void AllRepresentativePairsPreserveEulerAndTwelvePentagons()
    {
        foreach (var pair in RepresentativePairs)
        {
            var coarse =
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        pair.CoarseM,
                        pair.CoarseN));

            var fine =
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        pair.FineM,
                        pair.FineN));

            AssertEulerAndPentagons(
                coarse);

            AssertEulerAndPentagons(
                fine);
        }
    }

    [Fact]
    public void CurrentReferenceAndContinuityMappingScopeRemainsSinglePair()
    {
        var supportedRefinement =
            CreateRefinement(
                RepresentativePairs[0]);

        var supportedReferenceMap =
            GoldbergScaledRefinementReferenceMapper.Materialize(
                supportedRefinement);

        var supportedContinuityMap =
            GoldbergScaledSharedBorderContinuityMapper.Materialize(
                supportedReferenceMap);

        Assert.Equal(
            12,
            supportedReferenceMap.CellReferences.Count);

        Assert.Equal(
            30,
            supportedContinuityMap.BorderReferences.Count);

        foreach (var pair in RepresentativePairs.Skip(1))
        {
            var refinement =
                CreateRefinement(
                    pair);

            Assert.Throws<NotSupportedException>(
                () => GoldbergScaledRefinementReferenceMapper.Materialize(
                    refinement));
        }
    }

    private static void AssertPairPublicInvariants(
        PairSpec pair)
    {
        var refinement =
            CreateRefinement(
                pair);

        Assert.Equal(
            pair.Scale,
            refinement.Scale);

        Assert.Equal(
            checked(
                pair.CoarseM
                * pair.Scale),
            refinement.FineParameters.M);

        Assert.Equal(
            checked(
                pair.CoarseN
                * pair.Scale),
            refinement.FineParameters.N);

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                refinement.FineParameters);

        Assert.Equal(
            refinement.CoarseParameters.StrategicCellCount,
            (ulong)coarse.Cells.Count);

        Assert.Equal(
            refinement.CoarseParameters.StrategicEdgeCount,
            (ulong)coarse.Edges.Count);

        Assert.Equal(
            refinement.CoarseParameters.StrategicVertexCount,
            (ulong)coarse.Vertices.Count);

        Assert.Equal(
            refinement.FineParameters.StrategicCellCount,
            (ulong)fine.Cells.Count);

        Assert.Equal(
            refinement.FineParameters.StrategicEdgeCount,
            (ulong)fine.Edges.Count);

        Assert.Equal(
            refinement.FineParameters.StrategicVertexCount,
            (ulong)fine.Vertices.Count);

        var scaleSquared =
            checked(
                (ulong)pair.Scale
                * (ulong)pair.Scale);

        Assert.Equal(
            checked(
                scaleSquared
                * checked(
                    (ulong)coarse.Cells.Count
                    - 2UL)
                + 2UL),
            (ulong)fine.Cells.Count);

        Assert.Equal(
            checked(
                scaleSquared
                * (ulong)coarse.Edges.Count),
            (ulong)fine.Edges.Count);

        Assert.Equal(
            checked(
                scaleSquared
                * (ulong)coarse.Vertices.Count),
            (ulong)fine.Vertices.Count);

        AssertEulerAndPentagons(
            coarse);

        AssertEulerAndPentagons(
            fine);
    }

    private static GoldbergScaledRefinement CreateRefinement(
        PairSpec pair)
    {
        return new GoldbergScaledRefinement(
            new GoldbergParameters(
                pair.CoarseM,
                pair.CoarseN),
            new GoldbergParameters(
                pair.FineM,
                pair.FineN));
    }

    private static void AssertEulerAndPentagons(
        StrategicTopology topology)
    {
        Assert.Equal(
            12,
            topology.Cells.Count(
                cell =>
                    cell.Kind
                    == StrategicCellKind.Pentagon));

        Assert.Equal(
            2L,
            checked(
                (long)topology.Cells.Count
                - topology.Edges.Count
                + topology.Vertices.Count));
    }

    private static string CreateTopologySignature(
        StrategicTopology topology)
    {
        var builder =
            new StringBuilder();

        builder.Append(
            topology.Parameters.M);
        builder.Append(':');
        builder.Append(
            topology.Parameters.N);
        builder.Append('|');

        foreach (var cell in topology.Cells)
        {
            builder.Append(
                cell.Id.Value);
            builder.Append(':');
            builder.Append(
                (int)cell.Kind);
            builder.Append(':');

            AppendIds(
                builder,
                cell.AdjacentCellIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                cell.IncidentEdgeIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                cell.IncidentVertexIds.Select(
                    id => id.Value));

            builder.Append('|');
        }

        foreach (var edge in topology.Edges)
        {
            builder.Append(
                edge.Id.Value);
            builder.Append(':');

            AppendIds(
                builder,
                edge.IncidentCellIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                edge.IncidentVertexIds.Select(
                    id => id.Value));

            builder.Append('|');
        }

        foreach (var vertex in topology.Vertices)
        {
            builder.Append(
                vertex.Id.Value);
            builder.Append(':');

            AppendIds(
                builder,
                vertex.IncidentCellIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                vertex.IncidentEdgeIds.Select(
                    id => id.Value));

            builder.Append('|');
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    builder.ToString())));
    }

    private static void AppendIds(
        StringBuilder builder,
        IEnumerable<ulong> values)
    {
        foreach (var value in values)
        {
            builder.Append(
                value);
            builder.Append(',');
        }
    }

    private sealed record PairSpec(
        string Name,
        int CoarseM,
        int CoarseN,
        int FineM,
        int FineN,
        int Scale);
}
