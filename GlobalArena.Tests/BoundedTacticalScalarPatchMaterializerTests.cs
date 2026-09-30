using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class BoundedTacticalScalarPatchMaterializerTests
{
    [Fact]
    public void NullStrategicFieldIsRejected()
    {
        var refinement =
            CreateRefinement();

        Assert.Throws<ArgumentNullException>(
            () =>
                BoundedTacticalScalarPatchMaterializer.Materialize(
                    null!,
                    new StrategicCellId(
                        1UL),
                    refinement));
    }

    [Fact]
    public void DefaultTargetStrategicCellIdIsRejected()
    {
        var field =
            CreateField();

        Assert.Throws<ArgumentException>(
            () =>
                BoundedTacticalScalarPatchMaterializer.Materialize(
                    field,
                    default,
                    CreateRefinement()));
    }

    [Fact]
    public void ForeignTargetStrategicCellIdIsRejected()
    {
        var field =
            CreateField();

        Assert.Throws<KeyNotFoundException>(
            () =>
                BoundedTacticalScalarPatchMaterializer.Materialize(
                    field,
                    new StrategicCellId(
                        999UL),
                    CreateRefinement()));
    }

    [Fact]
    public void NullRefinementIsRejected()
    {
        var field =
            CreateField();

        Assert.Throws<ArgumentNullException>(
            () =>
                BoundedTacticalScalarPatchMaterializer.Materialize(
                    field,
                    new StrategicCellId(
                        1UL),
                    null!));
    }

    [Fact]
    public void MismatchedCoarseParametersAreRejected()
    {
        var field =
            CreateField();

        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(
                    2,
                    0),
                new GoldbergParameters(
                    12,
                    0));

        Assert.Throws<ArgumentException>(
            () =>
                BoundedTacticalScalarPatchMaterializer.Materialize(
                    field,
                    new StrategicCellId(
                        1UL),
                    refinement));
    }

    [Fact]
    public void UnsupportedPhysicalRefinementIsRejected()
    {
        var field =
            CreateField();

        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    0),
                new GoldbergParameters(
                    2,
                    0));

        Assert.Throws<NotSupportedException>(
            () =>
                BoundedTacticalScalarPatchMaterializer.Materialize(
                    field,
                    new StrategicCellId(
                        1UL),
                    refinement));
    }

    [Fact]
    public void PatchIsBoundedComparedWithFullPhysicalMap()
    {
        var field =
            CreateField();

        var refinement =
            CreateRefinement();

        var patch =
            BoundedTacticalScalarPatchMaterializer.Materialize(
                field,
                new StrategicCellId(
                    1UL),
                refinement);

        var fullMap =
            PhysicalTacticalIncidenceMapper.Materialize(
                refinement);

        Assert.True(
            patch.Count > 0);

        Assert.True(
            patch.Count
            < fullMap.TileIncidences.Count);
    }

    [Fact]
    public void PatchMatchesExactlyTheTargetIncidentPhysicalTiles()
    {
        var field =
            CreateField();

        var refinement =
            CreateRefinement();

        var target =
            new StrategicCellId(
                1UL);

        var patch =
            BoundedTacticalScalarPatchMaterializer.Materialize(
                field,
                target,
                refinement);

        var expected =
            PhysicalTacticalIncidenceMapper.Materialize(
                refinement)
                .TileIncidences
                .Where(
                    incidence =>
                        incidence
                            .IncidentCoarseCellIds
                            .Contains(
                                target))
                .Select(
                    incidence =>
                        incidence
                            .PhysicalTacticalTileId)
                .ToArray();

        Assert.Equal(
            expected,
            patch.Samples.Select(
                sample =>
                    sample.PhysicalTacticalTileId));
    }

    [Fact]
    public void PatchSamplesAreInCanonicalPhysicalIdentityOrder()
    {
        var patch =
            CreatePatch(
                new StrategicCellId(
                    1UL));

        var ids =
            patch.Samples
                .Select(
                    sample =>
                        sample
                            .PhysicalTacticalTileId
                            .FineStrategicCellId
                            .Value)
                .ToArray();

        Assert.Equal(
            ids.Order(),
            ids);
    }

    [Fact]
    public void EverySampleIsIncidentToTargetStrategicCell()
    {
        var target =
            new StrategicCellId(
                1UL);

        var patch =
            CreatePatch(
                target);

        Assert.All(
            patch.Samples,
            sample =>
                Assert.Contains(
                    target,
                    sample.IncidentStrategicCellIds));
    }

    [Fact]
    public void EverySampleUsesRefinementFineParameters()
    {
        var patch =
            CreatePatch(
                new StrategicCellId(
                    1UL));

        Assert.All(
            patch.Samples,
            sample =>
                Assert.Equal(
                    patch.Refinement.FineParameters,
                    sample
                        .PhysicalTacticalTileId
                        .FineGoldbergParameters));
    }

    [Fact]
    public void InteriorSamplesEqualTargetStrategicValue()
    {
        var field =
            CreateField();

        var target =
            new StrategicCellId(
                1UL);

        var patch =
            BoundedTacticalScalarPatchMaterializer.Materialize(
                field,
                target,
                CreateRefinement());

        var expected =
            field.GetRawValue(
                target);

        Assert.All(
            patch.Samples.Where(
                sample =>
                    sample.IncidentStrategicCellIds.Count
                    == 1),
            sample =>
                Assert.Equal(
                    expected,
                    sample.RawValue));
    }

    [Fact]
    public void SharedSamplesUseDeterministicIncidentCellAverage()
    {
        var field =
            CreateField();

        var patch =
            BoundedTacticalScalarPatchMaterializer.Materialize(
                field,
                new StrategicCellId(
                    1UL),
                CreateRefinement());

        foreach (var sample in
            patch.Samples.Where(
                sample =>
                    sample.IncidentStrategicCellIds.Count
                    > 1))
        {
            Int128 total =
                0;

            foreach (var strategicCellId
                in sample.IncidentStrategicCellIds)
            {
                total +=
                    field.GetRawValue(
                        strategicCellId);
            }

            var expected =
                (long)(
                    total
                    / sample
                        .IncidentStrategicCellIds
                        .Count);

            Assert.Equal(
                expected,
                sample.RawValue);
        }
    }

    [Fact]
    public void AggregationWeightsAreSixThreeTwoByIncidenceCardinality()
    {
        var patch =
            CreatePatch(
                new StrategicCellId(
                    1UL));

        Assert.All(
            patch.Samples,
            sample =>
                Assert.Equal(
                    6
                    / sample
                        .IncidentStrategicCellIds
                        .Count,
                    sample.AggregationWeight));
    }

    [Fact]
    public void RepeatedMaterializationIsDeterministic()
    {
        var target =
            new StrategicCellId(
                1UL);

        var first =
            CreatePatch(
                target);

        var second =
            CreatePatch(
                target);

        Assert.Equal(
            CreateSignature(
                first),
            CreateSignature(
                second));
    }

    [Fact]
    public void DifferentTargetsProduceDifferentBoundedPhysicalScopes()
    {
        var first =
            CreatePatch(
                new StrategicCellId(
                    1UL));

        var second =
            CreatePatch(
                new StrategicCellId(
                    2UL));

        Assert.False(
            first.Samples
                .Select(
                    sample =>
                        sample.PhysicalTacticalTileId)
                .SequenceEqual(
                    second.Samples.Select(
                        sample =>
                            sample.PhysicalTacticalTileId)));
    }

    [Fact]
    public void OfficialG20ToG120RefinementIsSupported()
    {
        var parameters =
            new GoldbergParameters(
                2,
                0);

        var graph =
            new StrategicSurfaceGraph(
                GoldbergStrategicTopologyGenerator.Generate(
                    parameters));

        var field =
            new StrategicScalarField(
                graph,
                Enumerable.Range(
                    0,
                    graph.NodeCount)
                    .Select(
                        index =>
                            (long)index));

        var patch =
            BoundedTacticalScalarPatchMaterializer.Materialize(
                field,
                new StrategicCellId(
                    1UL),
                new GoldbergScaledRefinement(
                    parameters,
                    new GoldbergParameters(
                        12,
                        0)));

        Assert.True(
            patch.Count > 0);

        Assert.All(
            patch.Samples,
            sample =>
                Assert.Equal(
                    new GoldbergParameters(
                        12,
                        0),
                    sample
                        .PhysicalTacticalTileId
                        .FineGoldbergParameters));
    }

    [Fact]
    public void PatchDoesNotRetainGlobalPhysicalIncidenceMap()
    {
        var retainedTypes =
            typeof(BoundedTacticalScalarPatch)
                .GetProperties()
                .Select(
                    property =>
                        property.PropertyType)
                .Concat(
                    typeof(BoundedTacticalScalarPatch)
                        .GetFields(
                            System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic)
                        .Select(
                            field =>
                                field.FieldType))
                .ToArray();

        Assert.DoesNotContain(
            typeof(PhysicalTacticalIncidenceMap),
            retainedTypes);
    }

    private static BoundedTacticalScalarPatch CreatePatch(
        StrategicCellId target)
    {
        return BoundedTacticalScalarPatchMaterializer.Materialize(
            CreateField(),
            target,
            CreateRefinement());
    }

    private static StrategicScalarField CreateField()
    {
        var graph =
            new StrategicSurfaceGraph(
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        1,
                        0)));

        return new StrategicScalarField(
            graph,
            Enumerable.Range(
                0,
                graph.NodeCount)
                .Select(
                    index =>
                        checked(
                            (long)(
                                (index * 200_000)
                                - 1_000_000))));
    }

    private static GoldbergScaledRefinement CreateRefinement()
    {
        return new GoldbergScaledRefinement(
            new GoldbergParameters(
                1,
                0),
            new GoldbergParameters(
                6,
                0));
    }

    private static string CreateSignature(
        BoundedTacticalScalarPatch patch)
    {
        return string.Join(
            "|",
            patch.Samples.Select(
                sample =>
                    sample
                        .PhysicalTacticalTileId
                        .FineStrategicCellId
                        .Value
                    + ":"
                    + sample.RawValue
                    + ":"
                    + sample.AggregationWeight));
    }
}
