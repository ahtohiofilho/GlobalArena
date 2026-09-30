using GlobalArena.Benchmarks;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M36WorldGenerationBenchmarkContractTests
{
    [Fact]
    public void UsesFrozenWarmupAndMeasuredSampleCounts()
    {
        Assert.Equal(
            1,
            M36WorldGenerationBenchmarkContract.WarmupCount);

        Assert.Equal(
            5,
            M36WorldGenerationBenchmarkContract.MeasuredCount);
    }

    [Fact]
    public void PreservesFrozenPerformanceBudgets()
    {
        Assert.Equal(
            200.0,
            M36WorldGenerationBenchmarkContract
                .MedianElapsedBudgetMilliseconds);

        Assert.Equal(
            300.0,
            M36WorldGenerationBenchmarkContract
                .MaxElapsedBudgetMilliseconds);

        Assert.Equal(
            64L * 1024L * 1024L,
            M36WorldGenerationBenchmarkContract
                .MedianManagedAllocationBudgetBytes);

        Assert.Equal(
            96L * 1024L * 1024L,
            M36WorldGenerationBenchmarkContract
                .MaxManagedAllocationBudgetBytes);
    }

    [Fact]
    public void DefinesThreeBlockingCasesAndOneStressCase()
    {
        Assert.Equal(
            4,
            M36WorldGenerationBenchmarkContract.Cases.Count);

        Assert.Equal(
            3,
            M36WorldGenerationBenchmarkContract.Cases.Count(
                benchmarkCase =>
                    benchmarkCase.BlocksM3Exit));

        Assert.Single(
            M36WorldGenerationBenchmarkContract.Cases,
            benchmarkCase =>
                !benchmarkCase.BlocksM3Exit);
    }

    [Fact]
    public void DefinesFrozenBlockingClassIClassIIAndClassIIICases()
    {
        var blocking =
            M36WorldGenerationBenchmarkContract.Cases
                .Where(
                    benchmarkCase =>
                        benchmarkCase.BlocksM3Exit)
                .ToArray();

        Assert.Equal(
            new[]
            {
                new GoldbergParameters(15, 0),
                new GoldbergParameters(7, 7),
                new GoldbergParameters(14, 1)
            },
            blocking
                .Select(
                    benchmarkCase =>
                        benchmarkCase.Parameters)
                .ToArray());

        Assert.Equal(
            new ulong[]
            {
                3601UL,
                3602UL,
                3603UL
            },
            blocking
                .Select(
                    benchmarkCase =>
                        benchmarkCase.Seed)
                .ToArray());
    }

    [Fact]
    public void DefinesFrozenNonBlockingG24StressCase()
    {
        var stress =
            Assert.Single(
                M36WorldGenerationBenchmarkContract.Cases,
                benchmarkCase =>
                    !benchmarkCase.BlocksM3Exit);

        Assert.Equal(
            "class-i-stress-g24x0",
            stress.Id);

        Assert.Equal(
            3604UL,
            stress.Seed);

        Assert.Equal(
            new GoldbergParameters(
                24,
                0),
            stress.Parameters);
    }

    [Fact]
    public void FrozenBudgetsProvideHeadroomOverAcceptedCalibration()
    {
        const double ObservedBlockingMedianElapsedMax =
            77.164;

        const double ObservedBlockingMaxElapsedMax =
            112.262;

        const long ObservedBlockingMedianAllocationMax =
            45_357_680L;

        const long ObservedBlockingMaxAllocationMax =
            45_357_680L;

        Assert.True(
            M36WorldGenerationBenchmarkContract
                .MedianElapsedBudgetMilliseconds
            > ObservedBlockingMedianElapsedMax);

        Assert.True(
            M36WorldGenerationBenchmarkContract
                .MaxElapsedBudgetMilliseconds
            > ObservedBlockingMaxElapsedMax);

        Assert.True(
            M36WorldGenerationBenchmarkContract
                .MedianManagedAllocationBudgetBytes
            > ObservedBlockingMedianAllocationMax);

        Assert.True(
            M36WorldGenerationBenchmarkContract
                .MaxManagedAllocationBudgetBytes
            > ObservedBlockingMaxAllocationMax);
    }
}
