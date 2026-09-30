using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicScalarFieldTests
{
    [Fact]
    public void DenominatorIsOneMillion()
    {
        Assert.Equal(
            1_000_000L,
            StrategicScalarField.Denominator);
    }

    [Fact]
    public void ConstructorRejectsNullGraph()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new StrategicScalarField(
                    null!,
                    Array.Empty<long>()));
    }

    [Fact]
    public void ConstructorRejectsNullValues()
    {
        var graph =
            CreateGraph();

        Assert.Throws<ArgumentNullException>(
            () =>
                new StrategicScalarField(
                    graph,
                    null!));
    }

    [Fact]
    public void TooFewValuesAreRejected()
    {
        var graph =
            CreateGraph();

        var values =
            new long[
                graph.NodeCount - 1];

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicScalarField(
                    graph,
                    values));
    }

    [Fact]
    public void TooManyValuesAreRejected()
    {
        var graph =
            CreateGraph();

        var values =
            new long[
                graph.NodeCount + 1];

        Assert.Throws<ArgumentException>(
            () =>
                new StrategicScalarField(
                    graph,
                    values));
    }

    [Fact]
    public void CountMatchesSurfaceGraph()
    {
        var graph =
            CreateGraph();

        var field =
            new StrategicScalarField(
                graph,
                CreateValues(
                    graph));

        Assert.Equal(
            graph.NodeCount,
            field.Count);
    }

    [Fact]
    public void RawValuesPreserveCanonicalInputOrder()
    {
        var graph =
            CreateGraph();

        var values =
            CreateValues(
                graph);

        var field =
            new StrategicScalarField(
                graph,
                values);

        Assert.Equal(
            values,
            field.RawValues);
    }

    [Fact]
    public void GetRawValueByIndexUsesCanonicalOrder()
    {
        var graph =
            CreateGraph();

        var values =
            CreateValues(
                graph);

        var field =
            new StrategicScalarField(
                graph,
                values);

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            Assert.Equal(
                values[index],
                field.GetRawValue(
                    index));
        }
    }

    [Fact]
    public void GetRawValueByCellIdUsesSurfaceGraphIndex()
    {
        var graph =
            CreateGraph();

        var values =
            CreateValues(
                graph);

        var field =
            new StrategicScalarField(
                graph,
                values);

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            Assert.Equal(
                values[index],
                field.GetRawValue(
                    graph.GetCellId(
                        index)));
        }
    }

    [Fact]
    public void SignedExtremesArePreserved()
    {
        var graph =
            CreateGraph();

        var values =
            CreateValues(
                graph);

        values[0] =
            long.MinValue;

        values[^1] =
            long.MaxValue;

        var field =
            new StrategicScalarField(
                graph,
                values);

        Assert.Equal(
            long.MinValue,
            field.GetRawValue(
                0));

        Assert.Equal(
            long.MaxValue,
            field.GetRawValue(
                graph.NodeCount - 1));
    }

    [Fact]
    public void SourceArrayMutationDoesNotAffectField()
    {
        var graph =
            CreateGraph();

        var values =
            CreateValues(
                graph);

        var expected =
            values[0];

        var field =
            new StrategicScalarField(
                graph,
                values);

        values[0] =
            checked(expected + 999L);

        Assert.Equal(
            expected,
            field.GetRawValue(
                0));
    }

    [Fact]
    public void RawValuesSnapshotIsReadOnly()
    {
        var graph =
            CreateGraph();

        var field =
            new StrategicScalarField(
                graph,
                CreateValues(
                    graph));

        var values =
            Assert.IsAssignableFrom<
                IList<long>>(
                field.RawValues);

        Assert.Throws<NotSupportedException>(
            () =>
                values.Add(
                    1L));
    }

    [Fact]
    public void OutOfRangeIndexesAreRejected()
    {
        var graph =
            CreateGraph();

        var field =
            new StrategicScalarField(
                graph,
                CreateValues(
                    graph));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                field.GetRawValue(
                    -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                field.GetRawValue(
                    graph.NodeCount));
    }

    [Fact]
    public void DefaultCellIdIsRejected()
    {
        var graph =
            CreateGraph();

        var field =
            new StrategicScalarField(
                graph,
                CreateValues(
                    graph));

        Assert.Throws<ArgumentException>(
            () =>
                field.GetRawValue(
                    default(StrategicCellId)));
    }

    [Fact]
    public void ForeignCellIdIsRejected()
    {
        var graph =
            CreateGraph();

        var field =
            new StrategicScalarField(
                graph,
                CreateValues(
                    graph));

        var foreign =
            new StrategicCellId(
                checked(
                    (ulong)graph.NodeCount
                    + 1UL));

        Assert.Throws<KeyNotFoundException>(
            () =>
                field.GetRawValue(
                    foreign));
    }

    [Fact]
    public void RepeatedFieldsFromSameInputsAreEquivalent()
    {
        var graph =
            CreateGraph();

        var values =
            CreateValues(
                graph);

        var first =
            new StrategicScalarField(
                graph,
                values);

        var second =
            new StrategicScalarField(
                graph,
                values);

        Assert.Equal(
            first.RawValues,
            second.RawValues);

        Assert.Same(
            graph,
            first.SurfaceGraph);

        Assert.Same(
            graph,
            second.SurfaceGraph);
    }

    private static StrategicSurfaceGraph CreateGraph()
    {
        return new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(
                new GoldbergParameters(
                    1,
                    0)));
    }

    private static long[] CreateValues(
        StrategicSurfaceGraph graph)
    {
        return Enumerable.Range(
            0,
            graph.NodeCount)
            .Select(
                index =>
                    checked(
                        ((long)index - 6L)
                        * StrategicScalarField.Denominator))
            .ToArray();
    }
}
