using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

public static class M36WorldGenerationBenchmarkHarness
{
    private sealed record Measurement(
        double ElapsedMilliseconds,
        long AllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections);

    private sealed record CaseResult(
        M36WorldGenerationBenchmarkCase BenchmarkCase,
        double MedianElapsedMilliseconds,
        double MaxElapsedMilliseconds,
        long MedianAllocatedBytes,
        long MaxAllocatedBytes,
        bool MedianElapsedPass,
        bool MaxElapsedPass,
        bool MedianAllocationPass,
        bool MaxAllocationPass,
        string CanonicalSignature);

    public static int Run()
    {
        CultureInfo.DefaultThreadCurrentCulture =
            CultureInfo.InvariantCulture;

        CultureInfo.DefaultThreadCurrentUICulture =
            CultureInfo.InvariantCulture;

#if DEBUG
        Console.Error.WriteLine(
            "M3.6 world-generation benchmark acceptance must run in Release configuration.");

        Console.WriteLine(
            "RESULT=FAIL_M36_WORLDGEN_BENCHMARK_CONFIGURATION");

        return 3;
#else
        try
        {
            WriteEnvironment();

            var results =
                new List<CaseResult>(
                    M36WorldGenerationBenchmarkContract.Cases.Count);

            foreach (var benchmarkCase in
                M36WorldGenerationBenchmarkContract.Cases)
            {
                results.Add(
                    MeasureCase(
                        benchmarkCase));
            }

            var blockingResults =
                results
                    .Where(
                        result =>
                            result.BenchmarkCase.BlocksM3Exit)
                    .ToArray();

            var stressResults =
                results
                    .Where(
                        result =>
                            !result.BenchmarkCase.BlocksM3Exit)
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
                    "RESULT=FAIL_M36_WORLDGEN_BENCHMARK_BUDGET");

                return 2;
            }

            Console.WriteLine(
                "RESULT=PASS_M36_WORLDGEN_BENCHMARK_HARNESS");

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                exception);

            Console.WriteLine(
                "RESULT=FAIL_M36_WORLDGEN_BENCHMARK_CORRECTNESS");

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
            $"ENV_PROCESSOR_IDENTIFIER={Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown"}");

        Console.WriteLine(
            $"WARMUP_COUNT={M36WorldGenerationBenchmarkContract.WarmupCount}");

        Console.WriteLine(
            $"MEASURED_COUNT={M36WorldGenerationBenchmarkContract.MeasuredCount}");

        Console.WriteLine(
            $"MEDIAN_ELAPSED_BUDGET_MS={M36WorldGenerationBenchmarkContract.MedianElapsedBudgetMilliseconds:F3}");

        Console.WriteLine(
            $"MAX_ELAPSED_BUDGET_MS={M36WorldGenerationBenchmarkContract.MaxElapsedBudgetMilliseconds:F3}");

        Console.WriteLine(
            $"MEDIAN_ALLOCATION_BUDGET_BYTES={M36WorldGenerationBenchmarkContract.MedianManagedAllocationBudgetBytes}");

        Console.WriteLine(
            $"MAX_ALLOCATION_BUDGET_BYTES={M36WorldGenerationBenchmarkContract.MaxManagedAllocationBudgetBytes}");
    }

    private static CaseResult MeasureCase(
        M36WorldGenerationBenchmarkCase benchmarkCase)
    {
        Console.WriteLine(
            $"CASE_BEGIN={benchmarkCase.Id};blocking={benchmarkCase.BlocksM3Exit};"
            + $"seed={benchmarkCase.Seed};m={benchmarkCase.Parameters.M};n={benchmarkCase.Parameters.N};"
            + $"cells={benchmarkCase.Parameters.StrategicCellCount};"
            + $"edges={benchmarkCase.Parameters.StrategicEdgeCount};"
            + $"vertices={benchmarkCase.Parameters.StrategicVertexCount}");

        string? expectedSignature =
            null;

        for (var warmupIndex = 1;
             warmupIndex <= M36WorldGenerationBenchmarkContract.WarmupCount;
             warmupIndex++)
        {
            var warmup =
                ExecuteWorkload(
                    benchmarkCase);

            ValidateWorkload(
                benchmarkCase,
                warmup);

            expectedSignature =
                WorldGenerationCanonicalSignature
                    .Compute(
                        warmup)
                    .Sha256Hex;

            Console.WriteLine(
                $"CASE_WARMUP={benchmarkCase.Id};index={warmupIndex};"
                + $"signature={expectedSignature};status=PASS");
        }

        var measurements =
            new Measurement[
                M36WorldGenerationBenchmarkContract.MeasuredCount];

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

            var result =
                ExecuteWorkload(
                    benchmarkCase);

            var elapsed =
                Stopwatch.GetElapsedTime(
                    startTimestamp);

            var allocatedAfter =
                GC.GetAllocatedBytesForCurrentThread();

            ValidateWorkload(
                benchmarkCase,
                result);

            var signature =
                WorldGenerationCanonicalSignature
                    .Compute(
                        result)
                    .Sha256Hex;

            if (!string.Equals(
                expectedSignature,
                signature,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{benchmarkCase.Id}: canonical signature changed across repeated generation.");
            }

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

            measurements[sampleIndex] =
                measurement;

            Console.WriteLine(
                $"CASE_SAMPLE={benchmarkCase.Id};index={sampleIndex + 1};"
                + $"elapsed_ms={measurement.ElapsedMilliseconds:F3};"
                + $"allocated_bytes={measurement.AllocatedBytes};"
                + $"gen0={measurement.Gen0Collections};"
                + $"gen1={measurement.Gen1Collections};"
                + $"gen2={measurement.Gen2Collections};"
                + $"signature={signature}");
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
            <= M36WorldGenerationBenchmarkContract
                .MedianElapsedBudgetMilliseconds;

        var maxElapsedPass =
            maxElapsed
            <= M36WorldGenerationBenchmarkContract
                .MaxElapsedBudgetMilliseconds;

        var medianAllocationPass =
            medianAllocation
            <= M36WorldGenerationBenchmarkContract
                .MedianManagedAllocationBudgetBytes;

        var maxAllocationPass =
            maxAllocation
            <= M36WorldGenerationBenchmarkContract
                .MaxManagedAllocationBudgetBytes;

        Console.WriteLine(
            $"CASE_METRICS={benchmarkCase.Id};"
            + $"median_elapsed_ms={medianElapsed:F3};"
            + $"max_elapsed_ms={maxElapsed:F3};"
            + $"median_allocated_bytes={medianAllocation};"
            + $"max_allocated_bytes={maxAllocation};"
            + $"max_gen0={measurements.Max(measurement => measurement.Gen0Collections)};"
            + $"max_gen1={measurements.Max(measurement => measurement.Gen1Collections)};"
            + $"max_gen2={measurements.Max(measurement => measurement.Gen2Collections)};"
            + $"signature={expectedSignature}");

        Console.WriteLine(
            $"CASE_BUDGET={benchmarkCase.Id};blocking={benchmarkCase.BlocksM3Exit};"
            + $"median_elapsed_pass={medianElapsedPass};"
            + $"max_elapsed_pass={maxElapsedPass};"
            + $"median_allocation_pass={medianAllocationPass};"
            + $"max_allocation_pass={maxAllocationPass}");

        var caseStatus =
            benchmarkCase.BlocksM3Exit
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
            maxAllocationPass,
            expectedSignature
            ?? throw new InvalidOperationException(
                $"{benchmarkCase.Id}: missing canonical signature."));
    }

    private static WorldGenerationResult ExecuteWorkload(
        M36WorldGenerationBenchmarkCase benchmarkCase)
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        return generator.Generate(
            new WorldGenerationRequest(
                new WorldSeed(
                    benchmarkCase.Seed),
                WorldGenerationVersion.Initial,
                benchmarkCase.Parameters));
    }

    private static void ValidateWorkload(
        M36WorldGenerationBenchmarkCase benchmarkCase,
        WorldGenerationResult result)
    {
        Require(
            result.Request.Seed.Value
            == benchmarkCase.Seed,
            $"{benchmarkCase.Id}: request seed mismatch.");

        Require(
            result.Request.StrategicParameters
            == benchmarkCase.Parameters,
            $"{benchmarkCase.Id}: request Goldberg parameters mismatch.");

        Require(
            result.StrategicTopology.Parameters
            == benchmarkCase.Parameters,
            $"{benchmarkCase.Id}: topology Goldberg parameters mismatch.");

        Require(
            (ulong)result.StrategicTopology.Cells.Count
            == benchmarkCase.Parameters.StrategicCellCount,
            $"{benchmarkCase.Id}: strategic cell count mismatch.");

        var nodeCount =
            result.StrategicSurfaceGraph.NodeCount;

        Require(
            result.StrategicPhysicalFields.Elevation.Count == nodeCount,
            $"{benchmarkCase.Id}: elevation coverage mismatch.");

        Require(
            result.StrategicPhysicalFields.Relief.Count == nodeCount,
            $"{benchmarkCase.Id}: relief coverage mismatch.");

        Require(
            result.StrategicPhysicalFields.LandWater.Count == nodeCount,
            $"{benchmarkCase.Id}: land-water coverage mismatch.");

        Require(
            result.StrategicClimateFields.Temperature.Count == nodeCount,
            $"{benchmarkCase.Id}: temperature coverage mismatch.");

        Require(
            result.StrategicClimateFields.Moisture.Count == nodeCount,
            $"{benchmarkCase.Id}: moisture coverage mismatch.");

        Require(
            result.StrategicClimateFields.WaterAvailability.Count == nodeCount,
            $"{benchmarkCase.Id}: water-availability coverage mismatch.");

        Require(
            result.StrategicHydrologyFields.Count == nodeCount,
            $"{benchmarkCase.Id}: hydrology coverage mismatch.");

        Require(
            result.StrategicBiomes.Count == nodeCount,
            $"{benchmarkCase.Id}: biome coverage mismatch.");

        Require(
            result.StrategicResourcePotentialFields.GeneralPotential.Count == nodeCount,
            $"{benchmarkCase.Id}: resource-potential coverage mismatch.");

        Require(
            result.StrategicHabitability.Count == nodeCount,
            $"{benchmarkCase.Id}: habitability coverage mismatch.");

        Require(
            result.StrategicCivilizationPlacementSuitability.Count == nodeCount,
            $"{benchmarkCase.Id}: placement-suitability coverage mismatch.");
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
