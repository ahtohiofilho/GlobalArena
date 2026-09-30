using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicTacticalFieldAggregatorTests
{
    [Fact]
    public void NullPatchIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicTacticalFieldAggregator.Aggregate(
                    null!));
    }

    [Fact]
    public void AggregatePreservesTargetStrategicIdentity()
    {
        var patch =
            CreatePatch(
                Enumerable.Repeat(
                    123L,
                    12));

        var aggregate =
            StrategicTacticalFieldAggregator.Aggregate(
                patch);

        Assert.Equal(
            patch.TargetStrategicCellId,
            aggregate.StrategicCellId);
    }

    [Fact]
    public void ConstantStrategicFieldRoundTripsExactly()
    {
        var patch =
            CreatePatch(
                Enumerable.Repeat(
                    456_789L,
                    12));

        var aggregate =
            StrategicTacticalFieldAggregator.Aggregate(
                patch);

        Assert.Equal(
            456_789L,
            aggregate.RawValue);
    }

    [Fact]
    public void AggregateMatchesIndependentExplicitWeightedMean()
    {
        var patch =
            CreatePatch(
                Enumerable.Range(
                    0,
                    12)
                    .Select(
                        index =>
                            checked(
                                (long)(
                                    (index * 150_000)
                                    - 700_000))));

        Int128 weightedTotal =
            0;

        long totalWeight =
            0;

        foreach (var sample
            in patch.Samples)
        {
            weightedTotal +=
                (Int128)sample.RawValue
                * sample.AggregationWeight;

            totalWeight +=
                sample.AggregationWeight;
        }

        var expected =
            (long)(
                weightedTotal
                / totalWeight);

        var aggregate =
            StrategicTacticalFieldAggregator.Aggregate(
                patch);

        Assert.Equal(
            expected,
            aggregate.RawValue);
    }

    [Fact]
    public void RepeatedAggregationIsDeterministic()
    {
        var patch =
            CreatePatch(
                Enumerable.Range(
                    0,
                    12)
                    .Select(
                        index =>
                            (long)index
                            * 10_000L));

        var first =
            StrategicTacticalFieldAggregator.Aggregate(
                patch);

        var second =
            StrategicTacticalFieldAggregator.Aggregate(
                patch);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void AggregateStaysInsideObservedPatchRange()
    {
        var patch =
            CreatePatch(
                Enumerable.Range(
                    0,
                    12)
                    .Select(
                        index =>
                            checked(
                                (long)(
                                    (index * 175_000)
                                    - 800_000))));

        var aggregate =
            StrategicTacticalFieldAggregator.Aggregate(
                patch);

        Assert.InRange(
            aggregate.RawValue,
            patch.Samples.Min(
                sample =>
                    sample.RawValue),
            patch.Samples.Max(
                sample =>
                    sample.RawValue));
    }

    [Fact]
    public void DifferentTargetScopesCanAggregateIndependently()
    {
        var parameters =
            new GoldbergParameters(
                1,
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
                            (long)index
                            * 100_000L));

        var refinement =
            new GoldbergScaledRefinement(
                parameters,
                new GoldbergParameters(
                    6,
                    0));

        var first =
            BoundedTacticalScalarPatchMaterializer.Materialize(
                field,
                new StrategicCellId(
                    1UL),
                refinement);

        var second =
            BoundedTacticalScalarPatchMaterializer.Materialize(
                field,
                new StrategicCellId(
                    2UL),
                refinement);

        var firstAggregate =
            StrategicTacticalFieldAggregator.Aggregate(
                first);

        var secondAggregate =
            StrategicTacticalFieldAggregator.Aggregate(
                second);

        Assert.Equal(
            new StrategicCellId(
                1UL),
            firstAggregate.StrategicCellId);

        Assert.Equal(
            new StrategicCellId(
                2UL),
            secondAggregate.StrategicCellId);
    }

    [Fact]
    public void AggregateSupportsOfficialG20ToG120BoundedPatch()
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
                Enumerable.Repeat(
                    -222_222L,
                    graph.NodeCount));

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

        var aggregate =
            StrategicTacticalFieldAggregator.Aggregate(
                patch);

        Assert.Equal(
            -222_222L,
            aggregate.RawValue);
    }

    private static BoundedTacticalScalarPatch CreatePatch(
        IEnumerable<long> rawValues)
    {
        var parameters =
            new GoldbergParameters(
                1,
                0);

        var graph =
            new StrategicSurfaceGraph(
                GoldbergStrategicTopologyGenerator.Generate(
                    parameters));

        var field =
            new StrategicScalarField(
                graph,
                rawValues);

        return BoundedTacticalScalarPatchMaterializer.Materialize(
            field,
            new StrategicCellId(
                1UL),
            new GoldbergScaledRefinement(
                parameters,
                new GoldbergParameters(
                    6,
                    0)));
    }
}
