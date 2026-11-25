using System.Numerics;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Finite marginal change in the owner's net seller receipts plus
/// revenue tax and toll income, for the SAME available interval. Not a
/// derivative, average productivity, settlement, or automatic assignment.</summary>
public sealed record FoundationBaseMarginalRevenue(
    PhysicalWorkerOccupation Occupation, byte? Crop, uint EffortMicro,
    BigInteger BaselineCreditScaled, BigInteger CandidateCreditScaled)
{
    public BigInteger MarginalCreditScaled => CandidateCreditScaled - BaselineCreditScaled;
}

/// <summary>Deterministic reference workload counters, not a wall-clock
/// measurement or a new economic state. A full evaluation is the existing
/// temporal financial reconciler, including its native market planner.</summary>
public sealed record FoundationBaseMarginalRevenueEvaluation(
    IReadOnlyList<FoundationBaseMarginalRevenue> Candidates,
    int FinancialEvaluations);

public sealed record FoundationBaseMarginalInterval(PhysicalWorkerId Worker, uint StartMicro);

public sealed record FoundationBaseMarginalIntervalResult(
    FoundationBaseMarginalInterval Interval, IReadOnlyList<FoundationBaseMarginalRevenue> Candidates);

public sealed record FoundationBaseMarginalBatchEvaluation(
    IReadOnlyList<FoundationBaseMarginalIntervalResult> Intervals,
    int FinancialEvaluations, int EquivalentIntervalsReused);

/// <summary>Reference oracle for one unallocated stationary interval in a
/// complete temporal calendar. One baseline and one canonical market solve
/// per eligible candidate; no outer equilibrium search or congestion loop.
/// Intentionally not connected to scheduler or human commands. R228.2
/// extends the exact reference to eligible operational work and reports its
/// cost in full financial evaluations before any production cutover.</summary>
public static class FoundationBaseMarginalRevenueEvaluator
{
    public static IReadOnlyList<FoundationBaseMarginalRevenue> Evaluate(
        WorldState opening, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        IReadOnlyList<FoundationPersistentWorkerSlice> calendar,
        PhysicalWorkerId workerId, uint startMicro,
        FoundationAgriculturalProductionPolicy agriculture,
        FoundationOperationalFreightShadowPolicy? operationalPolicy = null) =>
        EvaluateWithDiagnostics(opening, incidence, geography, policy, calendar,
            workerId, startMicro, agriculture, operationalPolicy).Candidates;

    public static FoundationBaseMarginalRevenueEvaluation EvaluateWithDiagnostics(
        WorldState opening, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        IReadOnlyList<FoundationPersistentWorkerSlice> calendar,
        PhysicalWorkerId workerId, uint startMicro,
        FoundationAgriculturalProductionPolicy agriculture,
        FoundationOperationalFreightShadowPolicy? operationalPolicy = null)
    {
        var batch = EvaluateBatch(opening, incidence, geography, policy, calendar,
            new[] { new FoundationBaseMarginalInterval(workerId, startMicro) }, agriculture, operationalPolicy);
        return new(batch.Intervals.Single().Candidates, batch.FinancialEvaluations);
    }

    /// <summary>
    /// Independent counterfactuals against ONE frozen baseline calendar.
    /// Not sequential allocation: a selected role is never fed back into
    /// another interval. All reuse is local to this invocation. For a
    /// full-turn stationary idle worker only, swapping identical economic
    /// identities within the same owner/node cannot change aggregate market
    /// inputs or operational service. Partial intervals remain ungrouped
    /// because per-worker rounding and earlier production can differ.
    /// </summary>
    public static FoundationBaseMarginalBatchEvaluation EvaluateBatch(
        WorldState opening, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        IReadOnlyList<FoundationPersistentWorkerSlice> calendar,
        IReadOnlyList<FoundationBaseMarginalInterval> intervals,
        FoundationAgriculturalProductionPolicy agriculture,
        FoundationOperationalFreightShadowPolicy? operationalPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(intervals);
        ArgumentNullException.ThrowIfNull(agriculture);
        operationalPolicy?.RequireValid();
        if (operationalPolicy is not null && !opening.WorkerOrders.IsInitialized)
            throw new InvalidOperationException("Operational evaluation requires persistent opening infrastructure.");
        if (!ReferenceEquals(agriculture.Geography, geography) ||
            geography.FineParameters != incidence.Refinement.FineParameters)
            throw new InvalidOperationException("Marginal evaluation requires matching signed geography.");
        if (intervals.Count == 0 || intervals.Any(x => x is null) ||
            intervals.Distinct().Count() != intervals.Count)
            throw new ArgumentException("Batch requires unique available intervals.", nameof(intervals));
        var people = opening.Workers.Workers.ToDictionary(w => w.Id);
        var ordered = calendar.OrderBy(s => s.Worker.Value).ThenBy(s => s.StartMicro).ToArray();
        var indices = ordered.Select((s, i) => (Slice: s, Index: i))
            .ToDictionary(x => (x.Slice.Worker, x.Slice.StartMicro), x => x.Index);
        var requested = intervals.OrderBy(x => x.Worker.Value).ThenBy(x => x.StartMicro).ToArray();
        // Validate every request before any financial work, including cache hits.
        foreach (var interval in requested)
        {
            if (!people.ContainsKey(interval.Worker) ||
                !indices.TryGetValue((interval.Worker, interval.StartMicro), out int index))
                throw new ArgumentException("Unknown available interval.");
            var idle = ordered[index];
            if (idle.Activity != PhysicalWorkerActivity.Other ||
                idle.Occupation != PhysicalWorkerOccupation.Unassigned || idle.ProjectId.HasValue ||
                idle.Crop.HasValue || idle.DurationMicro == 0)
                throw new InvalidOperationException("Only unallocated effort is eligible for marginal evaluation.");
        }
        if (ordered.Select(s => s.Worker).Distinct().Count() != opening.Workers.Workers.Count)
            throw new InvalidOperationException("Marginal evaluation requires the entire opening census.");
        var baseline = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, ordered, policy.Demand, agriculture);
        int financialEvaluations = 0;
        int reused = 0;
        FoundationTemporalFinancialReconciliation? baselineFinancial = null;
        FoundationTemporalFinancialReconciliation Finance(FoundationTemporalEconomicTurnInputs inputs)
        {
            financialEvaluations = checked(financialEvaluations + 1);
            return FoundationTemporalFinancialReconciler.Evaluate(opening, incidence, inputs,
                policy, geography, opening, operationalPolicy);
        }
        static BigInteger Credit(FoundationTemporalFinancialReconciliation result, CivilizationId owner)
        {
            var account = result.Accounting.Accounts.Single(a => a.Civilization == owner);
            return account.SellerNetScaled + account.RevenueTaxScaled + account.TollIncomeScaled;
        }
        var equivalent = new Dictionary<(CivilizationId Owner, PhysicalTacticalTileId Node,
            uint Demand, CommodityId Commodity, ulong Output, PhysicalWorkerOccupation? Occupation),
            IReadOnlyList<FoundationBaseMarginalRevenue>>();
        var results = new List<FoundationBaseMarginalIntervalResult>();
        foreach (var interval in requested)
        {
            int index = indices[(interval.Worker, interval.StartMicro)];
            var worker = people[interval.Worker];
            var idle = ordered[index];
            if (geography.SurfaceAtIndex(checked((int)idle.Node.FineStrategicCellId.Value - 1)) != FoundationFineSurfaceKind.Land)
            {
                results.Add(new(interval, Array.Empty<FoundationBaseMarginalRevenue>()));
                continue;
            }
            // The complete validated calendar guarantees this is the sole
            // interval for a full-turn idle worker. Keep all native economic
            // facts in the key (including dormant occupation).
            bool canReuse = idle.StartMicro == 0U && idle.DurationMicro == 1_000_000U;
            var key = (worker.Owner, idle.Node, worker.DemandMultiplierMicro, worker.Commodity,
                worker.OutputMicroPerTurn, worker.Occupation);
            if (canReuse && equivalent.TryGetValue(key, out var cached))
            {
                reused++;
                results.Add(new(interval, cached));
                continue;
            }
            baselineFinancial ??= Finance(baseline);
            var baselineCredit = Credit(baselineFinancial, worker.Owner);
            var rows = new List<FoundationBaseMarginalRevenue>();
            void Score(PhysicalWorkerOccupation role, byte? crop, PhysicalWorkerActivity activity)
            {
                var candidate = (FoundationPersistentWorkerSlice[])ordered.Clone();
                candidate[index] = idle with { Activity = activity, Occupation = role, Crop = crop };
                var projected = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, candidate, policy.Demand, agriculture);
                rows.Add(new(role, crop, idle.DurationMicro, baselineCredit, Credit(Finance(projected), worker.Owner)));
            }
            foreach (var crop in CropCatalog.All.OrderBy(c => (byte)c.Id))
                if (agriculture.Resolve(idle.Node, crop.Id).WholeTurnOutput > 0)
                    Score(PhysicalWorkerOccupation.Agriculture, (byte)crop.Id, PhysicalWorkerActivity.Producing);
            // Legacy forestry/mining share a worker commodity/output, so do not
            // fabricate distinct yields: only the worker's existing native role
            // is a supported non-crop candidate in this reference stage.
            if (worker.OutputMicroPerTurn > 0 && policy.Demand.Commodities.Contains(worker.Commodity) &&
                worker.Occupation is PhysicalWorkerOccupation.Forestry or PhysicalWorkerOccupation.Mining)
                Score(worker.Occupation.Value, null, PhysicalWorkerActivity.Producing);
            // The same signed crossing kinds and land-terminal rule used by
            // ProjectTemporal determine eligibility. The canonical reconciler
            // computes the actual proportional service and fixed quote discount
            // before clearing each hypothetical market. No benefit from a mere
            // infrastructure title or from cargo-dependent congestion is assumed.
            if (operationalPolicy is not null)
            {
                int indexAtNode = checked((int)idle.Node.FineStrategicCellId.Value - 1);
                bool land = false, port = false;
                foreach (var edge in incidence.FineTopology.Edges)
                {
                    if (!edge.IncidentCellIds.Contains(idle.Node.FineStrategicCellId)) continue;
                    var other = edge.IncidentCellIds.Single(id => id != idle.Node.FineStrategicCellId);
                    var kind = geography.CrossingAtIndices(indexAtNode, checked((int)other.Value - 1));
                    land |= kind == FoundationFineTransportCrossingKind.Ground;
                    port |= kind == FoundationFineTransportCrossingKind.Port &&
                        geography.FineElevationRaw[indexAtNode] > geography.SeaLevelRaw;
                    if (land && port) break;
                }
                if (land) Score(PhysicalWorkerOccupation.LandTransport, null, PhysicalWorkerActivity.Other);
                if (port) Score(PhysicalWorkerOccupation.PortOperations, null, PhysicalWorkerActivity.Other);
            }
            var ranked = Array.AsReadOnly(rows.OrderByDescending(r => r.MarginalCreditScaled)
                .ThenBy(r => r.Occupation).ThenBy(r => r.Crop).ToArray());
            if (canReuse) equivalent.Add(key, ranked);
            results.Add(new(interval, ranked));
        }
        return new(Array.AsReadOnly(results.ToArray()), financialEvaluations, reused);
    }
}
