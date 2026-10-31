using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlobalArena.Simulation;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Interprocess gate demonstration on the runner's actual filesystem.
/// A child .NET process probes the same lock-file path while the parent is
/// holding the OS file share denial. The benchmark reports acquisition cost,
/// not a production settlement throughput SLA or a distributed lock proof.
/// </summary>
public static class GaVs50InterprocessCheckpointBenchmarkHarness
{
    public static int RunWorker(string[] args)
    {
        if (args.Length != 3 || args[0] != "--vs50-gate-worker" ||
            !int.TryParse(args[2], CultureInfo.InvariantCulture, out var ms))
            return 4;
        try
        {
            using var gate = ExperimentalCheckpointProcessGate.Acquire(
                args[1], TimeSpan.FromMilliseconds(ms));
            Console.WriteLine("VS50_GATE_ACQUIRED");
            return 0;
        }
        catch (IOException)
        {
            Console.WriteLine("VS50_GATE_DENIED");
            return 17;
        }
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS50 interprocess benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS50-INTERPROCESS-CHECKPOINT-GATE");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var attempts in new[] { 8, 32, 128 })
                Measure(attempts);
            Console.WriteLine("RESULT=PASS_GA_VS50_INTERPROCESS_GATE_BENCHMARK");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Console.WriteLine("RESULT=FAIL_GA_VS50_INTERPROCESS_GATE_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int attempts)
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "ga-vs50-gate-benchmark", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "cash.ga-vs49-lab.json");
            var medians = new double[3];
            for (var run = -1; run < 3; run++)
            {
                // Genuine independently started process, not just Tasks or
                // multiple object references inside the same runtime.
                using (ExperimentalCheckpointProcessGate.Acquire(path))
                    Probe(path, 60, expectedExit: 17,
                        expectedOutput: "VS50_GATE_DENIED");
                Probe(path, 5000, expectedExit: 0,
                    expectedOutput: "VS50_GATE_ACQUIRED");

                var started = Stopwatch.GetTimestamp();
                for (var i = 0; i < attempts; i++)
                    using (ExperimentalCheckpointProcessGate.Acquire(path))
                    {
                        if (!File.Exists(path + ".gate"))
                            throw new IOException("Sidecar gate disappeared.");
                    }
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (run >= 0) medians[run] = elapsed;
            }
            Console.WriteLine($"CASE=gate-acquisitions-{attempts};" +
                $"child_process_denied=PASS;child_process_after_release=PASS;" +
                $"median_uncontended_gate_ms={medians.OrderBy(x => x).ElementAt(1):F3}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void Probe(
        string path, int timeoutMs, int expectedExit, string expectedOutput)
    {
        var assemblyPath = typeof(GaVs50InterprocessCheckpointBenchmarkHarness)
            .Assembly.Location;
        using var child = new Process();
        child.StartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        child.StartInfo.ArgumentList.Add(assemblyPath);
        child.StartInfo.ArgumentList.Add("--vs50-gate-worker");
        child.StartInfo.ArgumentList.Add(path);
        child.StartInfo.ArgumentList.Add(timeoutMs.ToString(
            CultureInfo.InvariantCulture));
        if (!child.Start())
            throw new InvalidOperationException(
                "Could not start an independent .NET lock-probe process.");
        if (!child.WaitForExit(20_000))
        {
            child.Kill(entireProcessTree: true);
            throw new TimeoutException("Independent checkpoint lock probe hung.");
        }
        var output = child.StandardOutput.ReadToEnd();
        var error = child.StandardError.ReadToEnd();
        if (child.ExitCode != expectedExit ||
            !output.Contains(expectedOutput, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Checkpoint gate child returned {child.ExitCode} instead of " +
                $"{expectedExit}: stdout={output}; stderr={error}");
    }
}
