using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

public static class M2TopologyBenchmarkHarness
{
    private sealed record Measurement(
        double ElapsedMilliseconds,
        long AllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections);

    private sealed record WorkloadSnapshot(
        StrategicTopology StrategicTopology,
        StrategicTacticalBorderAggregate Aggregate,
        PhysicalTacticalIncidenceMap? PhysicalMap);

    private sealed record CaseResult(
        M2TopologyBenchmarkCase BenchmarkCase,
        double MedianElapsedMilliseconds,
        double MaxElapsedMilliseconds,
        long MedianAllocatedBytes,
        long MaxAllocatedBytes,
        bool MedianElapsedPass,
        bool MaxElapsedPass,
        bool MedianAllocationPass,
        bool MaxAllocationPass);

    public static int Run()
    {
        CultureInfo.DefaultThreadCurrentCulture =
            CultureInfo.InvariantCulture;

        CultureInfo.DefaultThreadCurrentUICulture =
            CultureInfo.InvariantCulture;

#if DEBUG
        Console.Error.WriteLine(
            "M2 topology benchmark acceptance must run in Release configuration.");

        Console.WriteLine(
            "RESULT=FAIL_M2_TOPOLOGY_BENCHMARK_CONFIGURATION");

        return 3;
#else
        try
        {
            WriteEnvironment();

            var results =
                new List<CaseResult>(
                    M2TopologyBenchmarkContract.Cases.Count);

            foreach (var benchmarkCase in
                M2TopologyBenchmarkContract.Cases)
            {
                results.Add(
                    MeasureCase(
                        benchmarkCase));
            }

            var blockingResults =
                results
                    .Where(
                        result =>
                            result.BenchmarkCase.BlocksM2Exit)
                    .ToArray();

            var stressResults =
                results
                    .Where(
                        result =>
                            !result.BenchmarkCase.BlocksM2Exit)
                    .ToArray();

            var blockingBudgetPass =
                blockingResults.All(
                    result =>
                        result.MedianElapsedPass
                        && result.MaxElapsedPass
                        && result.MedianAllocationPass
                        && result.MaxAllocationPass);

            Console.WriteLine(
                $"BLOCKING_CASES={blockingResults.Length}");

            Console.WriteLine(
                $"STRESS_CASES={stressResults.Length}");

            Console.WriteLine(
                $"BLOCKING_BUDGET_PASS={blockingBudgetPass}");

            Console.WriteLine(
                "STRESS_PERFORMANCE_BLOCKING=False");

            Console.WriteLine(
                "STRESS_CORRECTNESS_REQUIRED=True");

            if (!blockingBudgetPass)
            {
                Console.WriteLine(
                    "RESULT=FAIL_M2_TOPOLOGY_BENCHMARK_BUDGET");

                return 2;
            }

            Console.WriteLine(
                "RESULT=PASS_M2_TOPOLOGY_BENCHMARK_HARNESS");

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            Console.WriteLine(
                "RESULT=FAIL_M2_TOPOLOGY_BENCHMARK_CORRECTNESS");

            return 1;
        }
#endif
    }

    private static void WriteEnvironment()
    {
        Console.WriteLine(
            $"ENV_OS={RuntimeInformation.OSDescription}");

        Console.WriteLine(
            $"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");

        Console.WriteLine(
            $"ENV_ARCH={RuntimeInformation.ProcessArchitecture}");

        Console.WriteLine(
            $"ENV_PROCESSOR_COUNT={Environment.ProcessorCount}");

        Console.WriteLine(
            $"WARMUP_COUNT={M2TopologyBenchmarkContract.WarmupCount}");

        Console.WriteLine(
            $"MEASURED_COUNT={M2TopologyBenchmarkContract.MeasuredCount}");

        Console.WriteLine(
            $"MEDIAN_ELAPSED_BUDGET_MS={M2TopologyBenchmarkContract.MedianElapsedBudgetMilliseconds:F3}");

        Console.WriteLine(
            $"MAX_ELAPSED_BUDGET_MS={M2TopologyBenchmarkContract.MaxElapsedBudgetMilliseconds:F3}");

        Console.WriteLine(
            $"MEDIAN_ALLOCATION_BUDGET_BYTES={M2TopologyBenchmarkContract.MedianManagedAllocationBudgetBytes}");

        Console.WriteLine(
            $"MAX_ALLOCATION_BUDGET_BYTES={M2TopologyBenchmarkContract.MaxManagedAllocationBudgetBytes}");
    }

    private static CaseResult MeasureCase(
        M2TopologyBenchmarkCase benchmarkCase)
    {
        Console.WriteLine(
            $"CASE_BEGIN={benchmarkCase.Id};kind={benchmarkCase.WorkloadKind};blocking={benchmarkCase.BlocksM2Exit};"
            + $"coarse_m={benchmarkCase.CoarseParameters.M};coarse_n={benchmarkCase.CoarseParameters.N};"
            + $"fine_m={benchmarkCase.FineParameters?.M.ToString(CultureInfo.InvariantCulture) ?? "none"};"
            + $"fine_n={benchmarkCase.FineParameters?.N.ToString(CultureInfo.InvariantCulture) ?? "none"}");

        for (var warmupIndex = 1;
             warmupIndex <= M2TopologyBenchmarkContract.WarmupCount;
             warmupIndex++)
        {
            var warmup =
                ExecuteWorkload(
                    benchmarkCase);

            ValidateWorkload(
                benchmarkCase,
                warmup);

            Console.WriteLine(
                $"CASE_WARMUP={benchmarkCase.Id};index={warmupIndex};status=PASS");
        }

        var measurements =
            new Measurement[
                M2TopologyBenchmarkContract.MeasuredCount];

        for (var sampleIndex = 0;
             sampleIndex < measurements.Length;
             sampleIndex++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var gen0Before =
                GC.CollectionCount(
                    0);

            var gen1Before =
                GC.CollectionCount(
                    1);

            var gen2Before =
                GC.CollectionCount(
                    2);

            var allocatedBefore =
                GC.GetAllocatedBytesForCurrentThread();

            var startTimestamp =
                Stopwatch.GetTimestamp();

            var snapshot =
                ExecuteWorkload(
                    benchmarkCase);

            var elapsed =
                Stopwatch.GetElapsedTime(
                    startTimestamp);

            var allocatedAfter =
                GC.GetAllocatedBytesForCurrentThread();

            var measurement =
                new Measurement(
                    elapsed.TotalMilliseconds,
                    checked(
                        allocatedAfter
                        - allocatedBefore),
                    GC.CollectionCount(0)
                        - gen0Before,
                    GC.CollectionCount(1)
                        - gen1Before,
                    GC.CollectionCount(2)
                        - gen2Before);

            ValidateWorkload(
                benchmarkCase,
                snapshot);

            measurements[sampleIndex] =
                measurement;

            Console.WriteLine(
                $"CASE_SAMPLE={benchmarkCase.Id};index={sampleIndex + 1};"
                + $"elapsed_ms={measurement.ElapsedMilliseconds:F3};"
                + $"allocated_bytes={measurement.AllocatedBytes};"
                + $"gen0={measurement.Gen0Collections};"
                + $"gen1={measurement.Gen1Collections};"
                + $"gen2={measurement.Gen2Collections}");
        }

        var orderedElapsed =
            measurements
                .Select(
                    measurement =>
                        measurement.ElapsedMilliseconds)
                .Order()
                .ToArray();

        var orderedAllocation =
            measurements
                .Select(
                    measurement =>
                        measurement.AllocatedBytes)
                .Order()
                .ToArray();

        var medianElapsed =
            orderedElapsed[
                orderedElapsed.Length / 2];

        var maxElapsed =
            orderedElapsed[^1];

        var medianAllocation =
            orderedAllocation[
                orderedAllocation.Length / 2];

        var maxAllocation =
            orderedAllocation[^1];

        var medianElapsedPass =
            medianElapsed
            <= M2TopologyBenchmarkContract
                .MedianElapsedBudgetMilliseconds;

        var maxElapsedPass =
            maxElapsed
            <= M2TopologyBenchmarkContract
                .MaxElapsedBudgetMilliseconds;

        var medianAllocationPass =
            medianAllocation
            <= M2TopologyBenchmarkContract
                .MedianManagedAllocationBudgetBytes;

        var maxAllocationPass =
            maxAllocation
            <= M2TopologyBenchmarkContract
                .MaxManagedAllocationBudgetBytes;

        Console.WriteLine(
            $"CASE_METRICS={benchmarkCase.Id};"
            + $"median_elapsed_ms={medianElapsed:F3};"
            + $"max_elapsed_ms={maxElapsed:F3};"
            + $"median_allocated_bytes={medianAllocation};"
            + $"max_allocated_bytes={maxAllocation}");

        Console.WriteLine(
            $"CASE_BUDGET={benchmarkCase.Id};blocking={benchmarkCase.BlocksM2Exit};"
            + $"median_elapsed_pass={medianElapsedPass};"
            + $"max_elapsed_pass={maxElapsedPass};"
            + $"median_allocation_pass={medianAllocationPass};"
            + $"max_allocation_pass={maxAllocationPass}");

        var caseStatus =
            benchmarkCase.BlocksM2Exit
                ? medianElapsedPass
                    && maxElapsedPass
                    && medianAllocationPass
                    && maxAllocationPass
                        ? "PASS"
                        : "BLOCKED"
                : "STRESS_OBSERVED";

        Console.WriteLine(
            $"CASE_END={benchmarkCase.Id};status={caseStatus}");

        return new CaseResult(
            benchmarkCase,
            medianElapsed,
            maxElapsed,
            medianAllocation,
            maxAllocation,
            medianElapsedPass,
            maxElapsedPass,
            medianAllocationPass,
            maxAllocationPass);
    }

    private static WorkloadSnapshot ExecuteWorkload(
        M2TopologyBenchmarkCase benchmarkCase)
    {
        return benchmarkCase.WorkloadKind switch
        {
            M2TopologyBenchmarkWorkloadKind.Logical =>
                ExecuteLogicalWorkload(
                    benchmarkCase),

            M2TopologyBenchmarkWorkloadKind.Physical =>
                ExecutePhysicalWorkload(
                    benchmarkCase),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported benchmark workload kind: {benchmarkCase.WorkloadKind}.")
        };
    }

    private static WorkloadSnapshot ExecuteLogicalWorkload(
        M2TopologyBenchmarkCase benchmarkCase)
    {
        if (benchmarkCase.FineParameters is not null)
        {
            throw new InvalidOperationException(
                "Logical benchmark cases cannot define fine Goldberg parameters.");
        }

        var topology =
            GoldbergStrategicTopologyGenerator.Generate(
                benchmarkCase.CoarseParameters);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        var aggregate =
            new StrategicTacticalBorderAggregate(
                topology,
                regions,
                bands);

        return new WorkloadSnapshot(
            topology,
            aggregate,
            null);
    }

    private static WorkloadSnapshot ExecutePhysicalWorkload(
        M2TopologyBenchmarkCase benchmarkCase)
    {
        var fineParameters =
            benchmarkCase.FineParameters
            ?? throw new InvalidOperationException(
                "Physical benchmark cases require fine Goldberg parameters.");

        var map =
            PhysicalTacticalIncidenceMapper.Materialize(
                new GoldbergScaledRefinement(
                    benchmarkCase.CoarseParameters,
                    fineParameters));

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                map.CoarseTopology);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                map.CoarseTopology);

        var aggregate =
            new StrategicTacticalBorderAggregate(
                map.CoarseTopology,
                regions,
                bands);

        return new WorkloadSnapshot(
            map.CoarseTopology,
            aggregate,
            map);
    }

    private static void ValidateWorkload(
        M2TopologyBenchmarkCase benchmarkCase,
        WorkloadSnapshot snapshot)
    {
        ValidateStrategicTopology(
            benchmarkCase.CoarseParameters,
            snapshot.StrategicTopology);

        Require(
            ReferenceEquals(
                snapshot.Aggregate.StrategicTopology,
                snapshot.StrategicTopology),
            $"{benchmarkCase.Id}: aggregate must retain the authoritative strategic topology.");

        Require(
            snapshot.Aggregate.TacticalRegions.Count
            == snapshot.StrategicTopology.Cells.Count,
            $"{benchmarkCase.Id}: tactical region coverage mismatch.");

        Require(
            snapshot.Aggregate.SharedBorderBands.Count
            == snapshot.StrategicTopology.Edges.Count,
            $"{benchmarkCase.Id}: shared border band coverage mismatch.");

        Require(
            snapshot.Aggregate.SharedBorderIncidences.Count
            == snapshot.StrategicTopology.Edges.Count,
            $"{benchmarkCase.Id}: shared border incidence coverage mismatch.");

        if (benchmarkCase.WorkloadKind
            == M2TopologyBenchmarkWorkloadKind.Logical)
        {
            Require(
                snapshot.PhysicalMap is null,
                $"{benchmarkCase.Id}: logical case unexpectedly produced a physical map.");

            return;
        }

        var fineParameters =
            benchmarkCase.FineParameters
            ?? throw new InvalidOperationException(
                $"{benchmarkCase.Id}: physical case is missing fine parameters.");

        var map =
            snapshot.PhysicalMap
            ?? throw new InvalidOperationException(
                $"{benchmarkCase.Id}: physical case did not produce a physical map.");

        Require(
            ReferenceEquals(
                map.CoarseTopology,
                snapshot.StrategicTopology),
            $"{benchmarkCase.Id}: physical map coarse topology must be authoritative.");

        Require(
            map.Refinement.CoarseParameters
            == benchmarkCase.CoarseParameters,
            $"{benchmarkCase.Id}: coarse refinement parameters mismatch.");

        Require(
            map.Refinement.FineParameters
            == fineParameters,
            $"{benchmarkCase.Id}: fine refinement parameters mismatch.");

        Require(
            map.Refinement.Scale == 6,
            $"{benchmarkCase.Id}: M2 physical benchmark scale must remain 6.");

        ValidateStrategicTopology(
            fineParameters,
            map.FineTopology);

        Require(
            (ulong)map.TileIncidences.Count
            == fineParameters.StrategicCellCount,
            $"{benchmarkCase.Id}: physical tile coverage mismatch.");

        var interior =
            map.TileIncidences.Count(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 1);

        var edgeShared =
            map.TileIncidences.Count(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 2);

        var vertexShared =
            map.TileIncidences.Count(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 3);

        var coarseTriangulationNumber =
            benchmarkCase
                .CoarseParameters
                .TriangulationNumber;

        var expectedInterior =
            checked(
                310UL
                * coarseTriangulationNumber
                + 2UL);

        var expectedEdgeShared =
            checked(
                30UL
                * coarseTriangulationNumber);

        var expectedVertexShared =
            checked(
                20UL
                * coarseTriangulationNumber);

        Require(
            (ulong)interior == expectedInterior,
            $"{benchmarkCase.Id}: physical interior signature mismatch.");

        Require(
            (ulong)edgeShared == expectedEdgeShared,
            $"{benchmarkCase.Id}: physical edge-shared signature mismatch.");

        Require(
            (ulong)vertexShared == expectedVertexShared,
            $"{benchmarkCase.Id}: physical vertex-shared signature mismatch.");
    }

    private static void ValidateStrategicTopology(
        GoldbergParameters parameters,
        StrategicTopology topology)
    {
        Require(
            topology.Parameters == parameters,
            $"G({parameters.M},{parameters.N}): topology parameters mismatch.");

        Require(
            (ulong)topology.Cells.Count
            == parameters.StrategicCellCount,
            $"G({parameters.M},{parameters.N}): strategic cell count mismatch.");

        Require(
            (ulong)topology.Edges.Count
            == parameters.StrategicEdgeCount,
            $"G({parameters.M},{parameters.N}): strategic edge count mismatch.");

        Require(
            (ulong)topology.Vertices.Count
            == parameters.StrategicVertexCount,
            $"G({parameters.M},{parameters.N}): strategic vertex count mismatch.");
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}
