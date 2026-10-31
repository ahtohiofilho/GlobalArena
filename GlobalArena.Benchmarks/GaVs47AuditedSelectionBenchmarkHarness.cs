using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Measures only select/re-audit/approve over synthetically repeated,
/// individually valid candidate invoices. Not a solver benchmark and not a
/// real-world authorization service (no authentication or replay database).
/// </summary>
public static class GaVs47AuditedSelectionBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;

    private sealed class ValueCurve : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(100, 1);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new((BigInteger)100 * q, Unit);
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("GA-VS47 authorization benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS47-SYNTHETIC-AUDITED-CANDIDATE-SELECTION");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var count in new[] { 8, 32, 128 })
                Measure(count);
            Console.WriteLine("RESULT=PASS_GA_VS47_AUDITED_CANDIDATE_BENCHMARK");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Console.WriteLine("RESULT=FAIL_GA_VS47_AUDITED_CANDIDATE_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int count)
    {
        var source = new EconomicPointId(1);
        var destination = new EconomicPointId(2);
        var commodity = new CommodityId(7);
        var supply = new EconomicSupplyFlow(
            source, commodity, EconomicActivityKind.Agriculture, Unit);
        var demand = new EconomicDemandFlow(destination, commodity, Unit);
        var flows = new EconomicProductionDemandResult(
            new[] { supply }, new[] { demand });
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(commodity, "food", 1)
        });
        var budget = new ExperimentalBuyerCashBudget(destination,
            new FoodTransportCostFraction(30, 1),
            new FoodTransportCostFraction(5, 1));
        var price = new ExperimentalBuyerCommodityPrice(
            destination, commodity, new FoodTransportCostFraction(10, 1));
        var calibration = new ExperimentalFreightTechnologyPricing(
            101, 7, 47, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var corridor = new ExperimentalSharedFreightCorridorKey(
            new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                new StrategicEdgeId(1)), 101, 7);
        var service = ExperimentalSharedFreightOption.Synthetic(
            1, new StrategicEconomicRouteKey(source, destination), calibration,
            1_000_000, new[] { corridor });
        var lane = new ExperimentalSimultaneousFreightLane(
            supply, demand, new[] { service });
        var congestion = new ExperimentalSharedFreightCongestionPolicy(
            1, 1_000_000);
        var candidate = new ExperimentalSimultaneousFreightReference().Compare(
            flows, catalog,
            new[] { new EconomicTurnCommodityValueOffer(
                destination, commodity, new ValueCurve()) },
            new[] { new EconomicTurnSourceReservation(
                source, commodity, supply.Activity, 0) },
            new[] { lane }, congestion, Unit,
            maxExactConfigurations: 8,
            cashSnapshot: new ExperimentalBuyerCashSnapshot(
                new[] { budget }, new[] { price })).Exact!;
        if (candidate.Trades.Count != 1)
            throw new InvalidOperationException(
                "Reference fixture must contain exactly one trade.");

        var snapshot = new ExperimentalCashAuthorizationSnapshot(
            47, "EXTERNAL-SYNTHETIC-WORLD-HASH-47", 3,
            flows, catalog, new[] { lane },
            new[] { budget }, new[] { price }, congestion);
        var proposals = Enumerable.Range(1, count)
            .Select(i => new ExperimentalAuditedCandidate((ulong)i, candidate))
            .ToArray();
        var policy = new ExperimentalAuditedSelectionPolicy(
            "GA-EXPERIMENTAL-MAX-VOLUME", 1,
            ExperimentalAuditedSelectionRule.MaxDeliveredRawVolumeThenLowestFreight);
        var gate = new ExperimentalAuditedCashCandidateAuthorization();
        var time = new double[3];
        var allocations = new long[3];
        string? signature = null;
        for (var i = -1; i < 3; i++)
        {
            var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            var selected = gate.Select(snapshot, proposals, policy,
                47, snapshot.WorldStateHash, snapshot.NetworkRevision);
            var approval = new ExperimentalCashAuthorityDecision(
                "GA-BENCHMARK-ACTOR", "APPROVAL-SYNTHETIC-47",
                true, 47, selected.SnapshotDigest,
                selected.CandidateDigest, selected.CandidateId,
                selected.Policy.PolicyId, selected.Policy.PolicyVersion);
            var receipt = gate.Authorize(selected, approval,
                47, snapshot.WorldStateHash, snapshot.NetworkRevision);
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            if (receipt.CandidateId != 1 ||
                selected.Audit.TotalOpening.CompareTo(
                    selected.Audit.TotalClosing) != 0 ||
                receipt.AuditJournalDigest.Length != 64)
                throw new InvalidOperationException(
                    "Audited selection must be reproducible and balanced.");
            var current = receipt.SnapshotDigest + ":" + receipt.CandidateDigest +
                ":" + receipt.AuditJournalDigest + ":" + receipt.CandidateId;
            if (i >= 0)
            {
                if (signature is null) signature = current;
                else if (!string.Equals(signature, current, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Independent policy decisions must be deterministic.");
                time[i] = elapsed;
                allocations[i] = bytes;
            }
            if (i == 2)
                Console.WriteLine(
                    $"CASE=candidates-{count};audits_per_sample={count + 1};" +
                    $"selected={receipt.CandidateId};" +
                    $"median_ms={time.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocations.OrderBy(x => x).ElementAt(1)}");
        }
    }
}
