using GlobalArena.Benchmarks;
if (args.Contains("--vs63-authorized-freight")) return GaVs60SessionTradePreviewBenchmarkHarness.Run(discoverFreight: true);
if (args.Contains("--vs64-freight-portfolio")) return GaVs60SessionTradePreviewBenchmarkHarness.Run(discoverPortfolio: true);
if (args.Contains("--vs65-joint-budget")) return GaVs60SessionTradePreviewBenchmarkHarness.Run(jointBudget: true);
if (args.Contains("--vs66-joint-selection")) return GaVs60SessionTradePreviewBenchmarkHarness.Run(selectJointFreight: true);
if (args.Contains("--vs62-discover-sites")) return GaVs60SessionTradePreviewBenchmarkHarness.Run(discoverSites: true);
if (args.Contains("--vs61-live-primitive")) return GaVs60SessionTradePreviewBenchmarkHarness.Run(livePrimitive: true);
if (args.Contains("--vs60-session-preview")) return GaVs60SessionTradePreviewBenchmarkHarness.Run();
if (args.Contains("--vs59-independent-selection")) return GaVs59IndependentSelectionAuditBenchmarkHarness.Run();
if (args.Contains("--vs58-simultaneous-authority")) return GaVs58SimultaneousAuthorityBenchmarkHarness.Run();

if (args.Any(argument => string.Equals(argument, "--vs57-session-authority", StringComparison.Ordinal)))
{
    return GaVs57SessionTransportBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs56-built-rights", StringComparison.Ordinal)))
{
    return GaVs56BuiltNetworkInstitutionalBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs55-institutional-freight", StringComparison.Ordinal)))
{
    return GaVs55InstitutionalFreightBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs54-physical-freight", StringComparison.Ordinal)))
{
    return GaVs54PhysicalFreightReconciliationBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs53-calibration", StringComparison.Ordinal)))
{
    return GaVs53AbstractPriceFreightCalibrationBenchmarkHarness.Run();
}

if (args.Any(argument =>
    string.Equals(argument, "--vs52-abstract-purchasing", StringComparison.Ordinal)))
{
    return GaVs52AbstractPurchasingBenchmarkHarness.Run();
}

if (args.Length > 0 &&
    string.Equals(args[0], "--vs51-crash-worker", StringComparison.Ordinal))
{
    return GaVs51AbruptCheckpointRecoveryHarness.RunWorker(args);
}

if (args.Any(argument =>
    string.Equals(argument, "--vs51-abrupt-recovery", StringComparison.Ordinal)))
{
    return GaVs51AbruptCheckpointRecoveryHarness.Run();
}

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
