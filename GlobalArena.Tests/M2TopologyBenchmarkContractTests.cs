using GlobalArena.Benchmarks;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M2TopologyBenchmarkContractTests
{
    [Fact]
    public void UsesFrozenWarmupAndMeasuredSampleCounts()
    {
        Assert.Equal(
            1,
            M2TopologyBenchmarkContract.WarmupCount);

        Assert.Equal(
            5,
            M2TopologyBenchmarkContract.MeasuredCount);
    }

    [Fact]
    public void PreservesFrozenHardBudgets()
    {
        Assert.Equal(
            1000.0,
            M2TopologyBenchmarkContract
                .MedianElapsedBudgetMilliseconds);

        Assert.Equal(
            2000.0,
            M2TopologyBenchmarkContract
                .MaxElapsedBudgetMilliseconds);

        Assert.Equal(
            192L * 1024L * 1024L,
            M2TopologyBenchmarkContract
                .MedianManagedAllocationBudgetBytes);

        Assert.Equal(
            256L * 1024L * 1024L,
            M2TopologyBenchmarkContract
                .MaxManagedAllocationBudgetBytes);
    }

    [Fact]
    public void DefinesFourBlockingCasesAndOneStressCase()
    {
        Assert.Equal(
            5,
            M2TopologyBenchmarkContract.Cases.Count);

        Assert.Equal(
            4,
            M2TopologyBenchmarkContract.Cases.Count(
                benchmarkCase =>
                    benchmarkCase.BlocksM2Exit));

        Assert.Single(
            M2TopologyBenchmarkContract.Cases,
            benchmarkCase =>
                !benchmarkCase.BlocksM2Exit);
    }

    [Fact]
    public void DefinesFrozenStrategicLogicalCases()
    {
        var logicalCases =
            M2TopologyBenchmarkContract.Cases
                .Where(
                    benchmarkCase =>
                        benchmarkCase.WorkloadKind
                        == M2TopologyBenchmarkWorkloadKind.Logical)
                .ToArray();

        Assert.Equal(
            3,
            logicalCases.Length);

        Assert.Equal(
            new[]
            {
                new GoldbergParameters(15, 0),
                new GoldbergParameters(7, 7),
                new GoldbergParameters(14, 1)
            },
            logicalCases
                .Select(
                    benchmarkCase =>
                        benchmarkCase.CoarseParameters)
                .ToArray());

        Assert.All(
            logicalCases,
            benchmarkCase =>
            {
                Assert.True(
                    benchmarkCase.BlocksM2Exit);

                Assert.Null(
                    benchmarkCase.FineParameters);
            });
    }

    [Fact]
    public void DefinesBlockingPhysicalScaleSixAcceptanceCase()
    {
        var physicalAcceptance =
            Assert.Single(
                M2TopologyBenchmarkContract.Cases,
                benchmarkCase =>
                    benchmarkCase.WorkloadKind
                        == M2TopologyBenchmarkWorkloadKind.Physical
                    && benchmarkCase.BlocksM2Exit);

        Assert.Equal(
            new GoldbergParameters(
                4,
                0),
            physicalAcceptance.CoarseParameters);

        Assert.Equal(
            new GoldbergParameters(
                24,
                0),
            physicalAcceptance.FineParameters);

        var refinement =
            new GoldbergScaledRefinement(
                physicalAcceptance.CoarseParameters,
                physicalAcceptance.FineParameters!.Value);

        Assert.Equal(
            6,
            refinement.Scale);
    }

    [Fact]
    public void DefinesNonBlockingG16ToG96StressCase()
    {
        var stress =
            Assert.Single(
                M2TopologyBenchmarkContract.Cases,
                benchmarkCase =>
                    !benchmarkCase.BlocksM2Exit);

        Assert.Equal(
            M2TopologyBenchmarkWorkloadKind.Physical,
            stress.WorkloadKind);

        Assert.Equal(
            new GoldbergParameters(
                16,
                0),
            stress.CoarseParameters);

        Assert.Equal(
            new GoldbergParameters(
                96,
                0),
            stress.FineParameters);
    }
}
