using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A small, explicit bundle of real in-place occupation transfer evaluations.
/// Every candidate is computed from exactly the same canonical baseline.
/// This is neither an exhaustive worker x city search nor a new-birth simulator.
/// </summary>
public sealed class FoundationRealMarginalTransferAssessment
{
    public FoundationMarginalLaborDiagnostic Ranking { get; }
    public IReadOnlyList<FoundationMarginalLaborObservation> Evaluated { get; }
    public int CompleteMarketSolutions { get; }
    public bool IsCommitted => false;

    internal FoundationRealMarginalTransferAssessment(
        FoundationMarginalLaborDiagnostic ranking,
        FoundationMarginalLaborObservation[] evaluated)
    {
        Ranking = ranking;
        Evaluated = Array.AsReadOnly(evaluated);
        CompleteMarketSolutions = checked(evaluated.Length * 3);
    }
}

public static class FoundationRealMarginalTransferEvaluator
{
    /// <summary>
    /// Evaluates an EXPLICIT budgeted set of existing workers for one
    /// in-place conversion to LAND or PORT. Each requires 3 independent
    /// market solves (all-producing; after transfer with baseline freight;
    /// after transfer with shadow freight). Always rebuilds from baseline.
    /// No authority or automatic decisions. No migration, no new workers.
    /// </summary>
    public static FoundationRealMarginalTransferAssessment Evaluate(
        WorldState baseline,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy marketPolicy,
        FoundationOperationalProductivityPolicy productivity,
        FoundationOperationalFreightShadowPolicy shadowPolicy,
        IReadOnlyList<PhysicalWorkerId> candidateWorkers,
        PhysicalWorkerOccupation destination,
        int evaluationBudget,
        int topCount)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(marketPolicy);
        ArgumentNullException.ThrowIfNull(productivity);
        ArgumentNullException.ThrowIfNull(shadowPolicy);
        ArgumentNullException.ThrowIfNull(candidateWorkers);
        if (destination != PhysicalWorkerOccupation.LandTransport &&
            destination != PhysicalWorkerOccupation.PortOperations)
            throw new ArgumentOutOfRangeException(nameof(destination));
        if (evaluationBudget < 1 || candidateWorkers.Count == 0 ||
            candidateWorkers.Count > evaluationBudget || topCount < 1)
            throw new ArgumentException(
                "A real economic evaluation requires an explicit nonzero " +
                "candidate set within a finite evaluation budget.");
        if (!baseline.Workers.HasAuthoritativeOccupations ||
            !baseline.Workers.HasAuthoritativeTacticalLocations ||
            !baseline.Transport.IsInitialized ||
            baseline.WorldBinding is null)
            throw new ArgumentException(
                "Real marginal candidates require a bound authoritative physical world.");

        var all = baseline.Workers.Workers.ToDictionary(w => w.Id);
        var ids = candidateWorkers.OrderBy(id => id.Value).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Duplicate worker candidate.", nameof(candidateWorkers));

        foreach (var id in ids)
        {
            if (!all.TryGetValue(id, out var worker) ||
                worker.Activity != PhysicalWorkerActivity.Producing ||
                worker.TacticalTileId is not { IsValid: true } ||
                worker.Occupation is not
                    (PhysicalWorkerOccupation.Agriculture or
                     PhysicalWorkerOccupation.Forestry or
                     PhysicalWorkerOccupation.Mining))
                throw new ArgumentException(
                    "Each candidate must be an existing authoritative productive individual.",
                    nameof(candidateWorkers));
        }

        // The hash captures the entire read-only shared source, including
        // physical workers and transport. It is NEVER generated from variants.
        var fingerprint = new CanonicalWorldStateHasher().Compute(baseline);
        string baselineIdentity = fingerprint.ToString() ??
            throw new InvalidOperationException("Missing world fingerprint.");
        var evaluated = new List<FoundationMarginalLaborObservation>();
        foreach (var id in ids)
        {
            var person = all[id];
            var variantWorkers = baseline.Workers.Workers.Select(w =>
                w.Id == id
                    ? w.WithTacticalAssignment(
                        w.TacticalTileId!.Value, w.CellId,
                        PhysicalWorkerActivity.Other, destination)
                    : w).ToArray();
            var variant = baseline.WithWorkers(new PhysicalWorkerRuntimeState(
                variantWorkers, tacticalLocationsAuthoritative: true,
                occupationsAuthoritative: true));
            var compared =
                FoundationOperationalEconomicCounterfactualProjector.Compare(
                    baseline, variant, incidence, geography,
                    marketPolicy, productivity, shadowPolicy);
            var assessment = FoundationOperationalEconomicAssessmentProjector
                .Evaluate(compared);
            evaluated.Add(new FoundationMarginalLaborObservation(
                baselineIdentity,
                FoundationMarginalLaborChangeKind.ExistingWorkerTransfer,
                person.Owner, person.TacticalTileId!.Value,
                destination,
                assessment.CombinedDelta.TotalCivilizationCashEquivalentScaled,
                id, person.TacticalTileId!.Value));
        }
        var observations = evaluated.ToArray();
        return new FoundationRealMarginalTransferAssessment(
            FoundationMarginalLaborDiagnosticIndexer.Rank(
                observations, topCount),
            observations);
    }
}
