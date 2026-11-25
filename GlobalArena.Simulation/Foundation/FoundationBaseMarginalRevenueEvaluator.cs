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

/// <summary>Reference oracle for one unallocated stationary interval in a
/// complete temporal calendar. One baseline and one canonical market solve
/// per eligible candidate; no outer equilibrium search or congestion loop.
/// Intentionally not connected to scheduler or human commands in R228.1.</summary>
public static class FoundationBaseMarginalRevenueEvaluator
{
    public static IReadOnlyList<FoundationBaseMarginalRevenue> Evaluate(
        WorldState opening, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        IReadOnlyList<FoundationPersistentWorkerSlice> calendar,
        PhysicalWorkerId workerId, uint startMicro,
        FoundationAgriculturalProductionPolicy agriculture,
        FoundationOperationalFreightShadowPolicy? operationalPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(agriculture);
        if (!ReferenceEquals(agriculture.Geography, geography) ||
            geography.FineParameters != incidence.Refinement.FineParameters)
            throw new InvalidOperationException("Marginal evaluation requires matching signed geography.");
        var worker = opening.Workers.Workers.Single(w => w.Id == workerId);
        var ordered = calendar.OrderBy(s => s.Worker.Value).ThenBy(s => s.StartMicro).ToArray();
        int index = Array.FindIndex(ordered, s => s.Worker == workerId && s.StartMicro == startMicro);
        if (index < 0) throw new ArgumentException("Unknown available interval.");
        var idle = ordered[index];
        if (idle.Activity != PhysicalWorkerActivity.Other ||
            idle.Occupation != PhysicalWorkerOccupation.Unassigned || idle.ProjectId.HasValue ||
            idle.Crop.HasValue || idle.DurationMicro == 0)
            throw new InvalidOperationException("Only unallocated effort is eligible for marginal evaluation.");
        if (ordered.Select(s => s.Worker).Distinct().Count() != opening.Workers.Workers.Count)
            throw new InvalidOperationException("Marginal evaluation requires the entire opening census.");
        // Validate even when no activity is eligible.
        var baseline = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, ordered, policy.Demand, agriculture);
        if (geography.SurfaceAtIndex(checked((int)idle.Node.FineStrategicCellId.Value - 1)) != FoundationFineSurfaceKind.Land)
            return Array.Empty<FoundationBaseMarginalRevenue>();
        BigInteger Credit(FoundationTemporalEconomicTurnInputs inputs)
        {
            var result = FoundationTemporalFinancialReconciler.Evaluate(opening, incidence, inputs,
                policy, geography, opening, operationalPolicy);
            var account = result.Accounting.Accounts.Single(a => a.Civilization == worker.Owner);
            return account.SellerNetScaled + account.RevenueTaxScaled + account.TollIncomeScaled;
        }
        var baselineCredit = Credit(baseline);
        var rows = new List<FoundationBaseMarginalRevenue>();
        void Score(PhysicalWorkerOccupation role, byte? crop, PhysicalWorkerActivity activity)
        {
            var candidate = (FoundationPersistentWorkerSlice[])ordered.Clone();
            candidate[index] = idle with { Activity = activity, Occupation = role, Crop = crop };
            var projected = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, candidate, policy.Demand, agriculture);
            rows.Add(new(role, crop, idle.DurationMicro, baselineCredit, Credit(projected)));
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
        // Logistics is deliberately not ranked until frozen-quote marginal
        // eligibility and attribution are specified. Existing logistics in
        // the supplied calendar is still included in canonical reconciliation.
        return Array.AsReadOnly(rows.OrderByDescending(r => r.MarginalCreditScaled)
            .ThenBy(r => r.Occupation).ThenBy(r => r.Crop).ToArray());
    }
}
