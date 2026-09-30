using GlobalArena.Benchmarks;

if (args.Any(
    argument =>
        string.Equals(
            argument,
            "--m36-worldgen",
            StringComparison.Ordinal)))
{
    return M36WorldGenerationBenchmarkHarness.Run();
}

return M2TopologyBenchmarkHarness.Run();
