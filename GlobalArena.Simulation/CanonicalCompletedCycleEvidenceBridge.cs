using System.Numerics;
using GlobalArena.Economy;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable scalar facts from an ACTUALLY completed canonical economic cycle.
/// The existing canonical journal proves a tick and the runtime holds totals,
/// but neither currently records per-source final-goods provenance.
/// </summary>
public readonly record struct CanonicalCompletedCyclePhysicalFacts(
    ulong CycleOrdinal,
    ulong ClosingMovementTurn,
    ulong ResidentAndTravelingPopulation,
    ulong ActuallyProducedRaw,
    ulong ActuallyConsumedFoodRaw,
    ulong ActuallyUnmetFoodRaw,
    string ClosingWorldHashHex);

/// <summary>
/// The candidate conservation ledger is consistent with the canonical cycle
/// only at AGGREGATE level. Per-origin and final-use authenticity is absent.
/// </summary>
public readonly record struct CanonicalCycleCandidateOutputReview(
    ulong CanonicalCycleOrdinal,
    ulong ClosingMovementTurn,
    BigInteger CandidateGrossProducedRaw,
    ulong ActuallyProducedRaw,
    bool AgreesWithCanonicalAggregate,
    bool HasCanonicalProducerReceipts,
    bool HasCanonicalFinalDestinationReceipts,
    bool CanAuthorizePurchasingPower);

/// <summary>
/// REAL executed local food delivery, observed from the canonical cash-food
/// receipts. It is consumption, NOT proof that the same food was produced
/// this cycle or that wood and stone have final destinations.
/// </summary>
public readonly record struct CanonicalCycleExecutedFoodUseReview(
    ulong CanonicalCycleOrdinal,
    ulong ActuallyDeliveredLocalFoodRaw,
    ulong OtherConsumedFoodRaw,
    bool HasExecutedLocalFoodReceipts,
    bool ProvesSameCycleFoodProductionOrigin,
    bool CanAuthorizePurchasingPower);

/// <summary>
/// GA-ECON-REBASE-04: binds a candidate real-output preflight to the exact
/// canonical cycle-closing tick/hash and actual physical aggregate. Never
/// upgrades an externally supplied claim into authenticated final production.
/// </summary>
public sealed class CanonicalCompletedCycleEvidenceBridge
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly int _journalCount;
    private readonly int _cycleCount;
    private readonly ulong _workerRevision;
    private readonly ulong _transportRevision;
    private readonly CanonicalCompletedCyclePhysicalFacts _facts;

    public CanonicalCompletedCyclePhysicalFacts Facts => _facts;
    public bool ProvesActualCycleClosure => true;
    public bool ProvesActualAggregateProductionAndFoodUse => true;
    public bool AuthenticatesPerCommodityProduction => false;
    public bool AuthenticatesFinalDestinations => false;
    public bool CanAuthorizePurchasingPower => false;
    public bool WritesToCanonicalSave => false;
    public bool ExecutesMarketOrProduction => false;

    private CanonicalCompletedCycleEvidenceBridge(
        CanonicalPhysicalTacticalSession session,
        CanonicalCompletedCyclePhysicalFacts facts)
    {
        _session = session;
        _facts = facts;
        _journalCount = session.Events.Count;
        _cycleCount = session.State.Cycles.Count;
        _workerRevision = session.State.WorkerRevision;
        _transportRevision = session.TransportNetwork.Revision;
    }

    private static CanonicalCompletedCyclePhysicalFacts ReadActualCompletedCycle(
        CanonicalPhysicalTacticalSession session)
    {
        var state = session.State;
        var events = session.Events;
        if (state.Cycles.Count == 0 || events.Count == 0 ||
            state.Cycles[^1].MovementTurn != state.MovementTurn)
            throw new InvalidOperationException(
                "The source is not an actually completed canonical economic cycle.");

        var action = events[^1];
        var hash = session.CurrentHash.HexDigest;
        if (action.Kind != "tick" || action.Source != 0UL ||
            action.Destination != 0UL || action.FromJob != 0 ||
            action.ToJob != 0 || action.Workers != 0UL ||
            action.FoodRaw != 0UL ||
            action.AfterHashHex != hash ||
            action.AfterRevision != session.Game.Snapshot.Revision ||
            (events.Count > 1 &&
             action.BeforeHashHex != events[^2].AfterHashHex))
            throw new InvalidOperationException(
                "The cycle-closing tick must be the latest canonical hash-linked action.");

        var cycle = state.Cycles[^1];
        if (cycle.MovementTurn == 0UL || state.TotalWorkers == 0UL)
            throw new InvalidOperationException(
                "A canonical cycle witness requires a positive settlement turn and population.");

        return new CanonicalCompletedCyclePhysicalFacts(
            checked((ulong)state.Cycles.Count),
            cycle.MovementTurn, state.TotalWorkers,
            cycle.ProducedRaw, cycle.FoodConsumedRaw, cycle.FoodUnmetRaw, hash);
    }

    public static CanonicalCompletedCycleEvidenceBridge Capture(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var facts = ReadActualCompletedCycle(session);
        var result = new CanonicalCompletedCycleEvidenceBridge(session, facts);
        result.VerifyIndependent();
        return result;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(_session, session) ||
            session.Events.Count != _journalCount ||
            session.State.Cycles.Count != _cycleCount ||
            session.State.WorkerRevision != _workerRevision ||
            session.TransportNetwork.Revision != _transportRevision ||
            session.CurrentHash.HexDigest != _facts.ClosingWorldHashHex ||
            session.State.MovementTurn != _facts.ClosingMovementTurn)
            throw new InvalidOperationException(
                "Cycle evidence is stale or belongs to another canonical session.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        if (ReadActualCompletedCycle(_session) != _facts)
            throw new InvalidOperationException(
                "Reconstructed canonical production/consumption counters diverged.");
        RequireCurrent(_session);
    }

    /// <summary>
    /// Necessary, NOT sufficient, conditions for claimed final output:
    /// cycle, real population and total physical production must match.
    /// Commodity/source/destination rows are still untrusted until canonical
    /// per-lot receipts exist. Equality of aggregates is never authentication.
    /// </summary>
    public CanonicalCycleCandidateOutputReview ReviewCandidate(
        CanonicalPhysicalTacticalSession session,
        PhysicalFinalOutputPreflight candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        VerifyIndependent();
        RequireCurrent(session);

        if (candidate.ClaimedCompletedCycle != _facts.CycleOrdinal ||
            candidate.ClaimedPopulation != _facts.ResidentAndTravelingPopulation)
            throw new InvalidOperationException(
                "Candidate cycle or population differs from the real canonical settlement.");

        var claimedRaw = BigInteger.Zero;
        foreach (var row in candidate.Commodities)
            claimedRaw += row.FinalLocalRaw + row.FinalRemoteRaw +
                row.IntermediateRaw + row.RetainedRaw;
        if (claimedRaw != new BigInteger(_facts.ActuallyProducedRaw))
            throw new InvalidOperationException(
                "Candidate physical production does not reconcile to the exact canonical aggregate.");

        RequireCurrent(session);
        return new CanonicalCycleCandidateOutputReview(
            _facts.CycleOrdinal, _facts.ClosingMovementTurn, claimedRaw,
            _facts.ActuallyProducedRaw, true,
            HasCanonicalProducerReceipts: false,
            HasCanonicalFinalDestinationReceipts: false,
            CanAuthorizePurchasingPower: false);
    }

    /// <summary>
    /// Verified physical consumption from executed V12 food receipts.
    /// Nominal payment is NOT used as a proxy for physical production or N.
    /// A legacy food purchase may draw old stocks; no origin is invented.
    /// </summary>
    public CanonicalCycleExecutedFoodUseReview ReviewExecutedLocalFoodUse(
        CanonicalPhysicalTacticalSession session,
        CanonicalExecutedLocalFoodPriceWitness executedFood)
    {
        ArgumentNullException.ThrowIfNull(executedFood);
        VerifyIndependent();
        RequireCurrent(session);
        executedFood.RequireCurrent(session);
        executedFood.VerifyIndependent();
        if (executedFood.CompletedEconomicCycleMovementTurn !=
                _facts.ClosingMovementTurn ||
            executedFood.SourceCanonicalWorldHashHex !=
                _facts.ClosingWorldHashHex ||
            executedFood.TotalActualLocalFoodRaw >
                _facts.ActuallyConsumedFoodRaw)
            throw new InvalidOperationException(
                "Food settlement receipt is not part of this canonical physical cycle.");

        RequireCurrent(session);
        return new CanonicalCycleExecutedFoodUseReview(
            _facts.CycleOrdinal, executedFood.TotalActualLocalFoodRaw,
            _facts.ActuallyConsumedFoodRaw -
                executedFood.TotalActualLocalFoodRaw,
            executedFood.HasExecutedPriceObservation,
            ProvesSameCycleFoodProductionOrigin: false,
            CanAuthorizePurchasingPower: false);
    }
}
