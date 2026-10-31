using GlobalArena.Benchmarks;

if (args.Length > 0 &&
    string.Equals(args[0], "--vs50-gate-worker", StringComparison.Ordinal))
{
    return GaVs50InterprocessCheckpointBenchmarkHarness.RunWorker(args);
}

if (args.Any(argument =>
    string.Equals(argument, "--vs50-interprocess-gate", StringComparison.Ordinal)))
{
    return GaVs50InterprocessCheckpointBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs49-recoverable-checkpoint", StringComparison.Ordinal)))
{
    return GaVs49RecoverableCheckpointBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs48-atomic-settlement", StringComparison.Ordinal)))
{
    return GaVs48AtomicSettlementBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs47-audited-selection", StringComparison.Ordinal)))
{
    return GaVs47AuditedSelectionBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs46-independent-cash-audit", StringComparison.Ordinal)))
{
    return GaVs46IndependentCashAuditBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs45-cash-alpha", StringComparison.Ordinal)))
{
    return GaVs45CashAlphaBenchmarkHarness.Run();
}

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
