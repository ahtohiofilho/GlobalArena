using GlobalArena.Benchmarks;

if (args.Any(argument =>
    string.Equals(argument, "--vs44-cash-budget", StringComparison.Ordinal)))
{
    return GaVs44CashBudgetBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs43-simultaneous-freight", StringComparison.Ordinal)))
{
    return GaVs43SimultaneousFreightBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs42-shared-freight", StringComparison.Ordinal)))
{
    return GaVs42SharedCorridorBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs41-freight-routing", StringComparison.Ordinal)))
{
    return GaVs41FreightRoutingBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--fine-economic-sequence", StringComparison.Ordinal)))
{
    return FineEconomicCycleBenchmarkHarness.RunSequence();
}

if (args.Any(argument =>
    string.Equals(argument, "--fine-economic-cycle", StringComparison.Ordinal)))
{
    return FineEconomicCycleBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--turn-freight-market", StringComparison.Ordinal)))
{
    return TurnLockedFreightMarketBenchmarkHarness.Run();
}

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
