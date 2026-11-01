using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// In-memory VS85 receipt: price quotations are HYPOTHETICAL frozen VS78
/// local indices. Only its source canonical economic-cycle boundary is REAL.
/// It cannot be written into a canonical save or authorize production.
/// </summary>
public sealed class CanonicalCycleStampedPriceReceipt
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalWorkforceActivityRealizationPolicy[] _nextPolicies;
    private readonly CanonicalLaggedProductionActivitySignal[] _signals;
    private readonly string _sourceWorldHashHex;
    private readonly ulong _sourceNetworkRevision;
    private readonly ulong _sourceWorkerRevision;
    private readonly int _sourceJournalCount;
    private readonly int _sourceCycleCount;

    public ulong SourceMovementTurn { get; }
    public ulong SourceCompletedEconomicCycleTurn { get; }
    public string SourceWorldHashHex => _sourceWorldHashHex;
    public int SourceJournalCount => _sourceJournalCount;
    public int SourceEconomicCycleCount => _sourceCycleCount;
    public ulong SourceWorkerRevision => _sourceWorkerRevision;
    public ulong SourceNetworkRevision => _sourceNetworkRevision;
    public int MaximumResponseStepPermille { get; }
    public IReadOnlyList<CanonicalWorkforceActivityRealizationPolicy> FrozenNextActivityPolicies { get; }
    public IReadOnlyList<CanonicalLaggedProductionActivitySignal> HypotheticalPriorLocalPriceSignals { get; }
    public bool HasGenuineCompletedSourceCycle => true;
    public bool QuotesAreActualExecutedMarketPrices => false;
    public bool PersistsIntoCanonicalSave => false;
    public bool CreatesMoneyOrCredit => false;
    public bool CanAuthorizeSettlement => false;

    private CanonicalCycleStampedPriceReceipt(
        CanonicalPhysicalTacticalSession session,
        CanonicalLaggedProductionResponse feedback)
    {
        _session = session;
        var state = session.State;
        SourceMovementTurn = state.MovementTurn;
        SourceCompletedEconomicCycleTurn = state.Cycles[^1].MovementTurn;
        _sourceCycleCount = state.Cycles.Count;
        _sourceWorkerRevision = state.WorkerRevision;
        _sourceNetworkRevision = session.TransportNetwork.Revision;
        _sourceWorldHashHex = session.CurrentHash.HexDigest;
        _sourceJournalCount = session.Events.Count;
        MaximumResponseStepPermille = feedback.ConfiguredResponseStepPermille;
        _nextPolicies = feedback.NextProjection.Policies.ToArray();
        _signals = feedback.Signals.ToArray();
        FrozenNextActivityPolicies = Array.AsReadOnly(_nextPolicies);
        HypotheticalPriorLocalPriceSignals = Array.AsReadOnly(_signals);
    }

    public static CanonicalCycleStampedPriceReceipt Capture(
        CanonicalPhysicalTacticalSession session,
        CanonicalLaggedProductionResponse feedback)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(feedback);
        feedback.RequireCurrent(session);
        feedback.VerifyIndependent();
        var state = session.State;
        var events = session.Events;
        if (state.Cycles.Count == 0 ||
            state.Cycles[^1].MovementTurn != state.MovementTurn ||
            events.Count == 0 || events[^1].Kind != "tick" ||
            events[^1].AfterHashHex != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "VS85 source must be the canonical tick that actually closed the economic cycle, without an intervening physical reallocation.");
        var receipt = new CanonicalCycleStampedPriceReceipt(session, feedback);
        receipt.RequireExactlyNextSettledCycleIsNotYetAvailable(session);
        return receipt;
    }

    private void RequireExactlyNextSettledCycleIsNotYetAvailable(
        CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session) ||
            session.CurrentHash.HexDigest != _sourceWorldHashHex ||
            session.State.MovementTurn != SourceMovementTurn ||
            session.State.Cycles.Count != _sourceCycleCount ||
            session.State.WorkerRevision != _sourceWorkerRevision ||
            session.TransportNetwork.Revision != _sourceNetworkRevision ||
            session.Events.Count != _sourceJournalCount)
            throw new InvalidOperationException(
                "A newly frozen local price receipt must match its exact physical economic-cycle boundary.");
    }

    private void VerifyNextRealCycle(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "A local market price witness belongs to the originating physical session only.");
        var state = session.State;
        if (state.Cycles.Count != _sourceCycleCount + 1 ||
            state.Cycles[_sourceCycleCount - 1].MovementTurn !=
                SourceCompletedEconomicCycleTurn ||
            state.Cycles[^1].MovementTurn != state.MovementTurn ||
            state.MovementTurn <= SourceMovementTurn ||
            state.WorkerRevision != _sourceWorkerRevision ||
            session.TransportNetwork.Revision != _sourceNetworkRevision)
            throw new InvalidOperationException(
                "VS85 can only use the EXACT next real settled economic cycle, with unchanged worker assignments and institutional network.");

        var actions = session.Events;
        var elapsed = state.MovementTurn - SourceMovementTurn;
        if (elapsed > int.MaxValue ||
            actions.Count - _sourceJournalCount != (int)elapsed ||
            _sourceJournalCount > actions.Count ||
            string.Equals(session.CurrentHash.HexDigest,
                _sourceWorldHashHex, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The required real intervening movement ticks or world hash are missing.");

        var lastHash = _sourceWorldHashHex;
        for (var i = _sourceJournalCount; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action.Kind != "tick" || action.Source != 0UL ||
                action.Destination != 0UL || action.FromJob != 0 ||
                action.ToJob != 0 || action.Workers != 0UL ||
                action.BeforeHashHex != lastHash)
                throw new InvalidOperationException(
                    "Frozen source quotation crossed a non-tick action or a broken canonical event hash chain.");
            lastHash = action.AfterHashHex;
        }
        if (lastHash != session.CurrentHash.HexDigest)
            throw new InvalidOperationException(
                "The actual next cycle snapshot does not match the canonical tick journal.");
    }

    public CanonicalCycleStampedProductionProjection ApplyAfterNextSettledCycle(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery currentSites,
        CanonicalSimultaneousFreightReference currentFreight,
        IEnumerable<CanonicalSimultaneousFreightLot> nextRemoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentSites);
        ArgumentNullException.ThrowIfNull(currentFreight);
        ArgumentNullException.ThrowIfNull(nextRemoteLots);
        ArgumentNullException.ThrowIfNull(congestion);
        VerifyNextRealCycle(session);
        currentSites.RequireCurrent(session);
        currentFreight.RequireCurrent(session);
        var lots = nextRemoteLots.ToArray();
        var projected = CanonicalWorkforceRealizationProjection.FromSites(
            session, currentSites, currentFreight, _nextPolicies,
            lots, congestion);
        VerifyNextRealCycle(session);
        var answer = new CanonicalCycleStampedProductionProjection(
            this, session, currentSites, currentFreight, lots,
            congestion, projected);
        answer.VerifyIndependent();
        return answer;
    }

    internal void RequireRealNextCycle(CanonicalPhysicalTacticalSession session) =>
        VerifyNextRealCycle(session);
}

/// <summary>
/// The TIME boundary is proven by a real canonical cycle and its journal.
/// Production/market volumes are still VS83 HYPOTHETICAL and read-only.
/// </summary>
public sealed class CanonicalCycleStampedProductionProjection
{
    private readonly CanonicalCycleStampedPriceReceipt _receipt;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalSimultaneousFreightReference _freight;
    private readonly CanonicalSimultaneousFreightLot[] _lots;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;
    private readonly string _resultWorldHashHex;

    internal CanonicalCycleStampedPriceReceipt SourcePriceReceipt => _receipt;
    public CanonicalWorkforceRealizationProjection HypotheticalNextProduction { get; }
    public ulong NextRealCompletedEconomicCycleTurn { get; }
    public string NextRealCompletedEconomicCycleWorldHashHex => _resultWorldHashHex;
    public bool ProvesActualChronologicalEconomicCycle => true;
    public bool ProvesRealMarketSaleOrPayment => false;
    public bool CreatesInventoriesOrMoney => false;
    public bool ExecutesWorkforceReassignment => false;
    public bool CanAuthorizeRealSettlement => false;

    internal CanonicalCycleStampedProductionProjection(
        CanonicalCycleStampedPriceReceipt receipt,
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freight,
        CanonicalSimultaneousFreightLot[] lots,
        ExperimentalSharedFreightCongestionPolicy congestion,
        CanonicalWorkforceRealizationProjection projected)
    {
        _receipt = receipt;
        _session = session;
        _sites = sites;
        _freight = freight;
        _lots = (CanonicalSimultaneousFreightLot[])lots.Clone();
        _congestion = congestion;
        HypotheticalNextProduction = projected;
        NextRealCompletedEconomicCycleTurn = session.State.Cycles[^1].MovementTurn;
        _resultWorldHashHex = session.CurrentHash.HexDigest;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Cycle-stamped scenario cannot be reused in another session.");
        _receipt.RequireRealNextCycle(session);
        _sites.RequireCurrent(session);
        _freight.RequireCurrent(session);
        HypotheticalNextProduction.RequireCurrent(session);
        if (session.State.MovementTurn != NextRealCompletedEconomicCycleTurn ||
            session.CurrentHash.HexDigest != _resultWorldHashHex)
            throw new InvalidOperationException(
                "Cycle-stamped scenario was invalidated by a subsequent world transition.");
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = CanonicalWorkforceRealizationProjection.FromSites(
            _session, _sites, _freight, _receipt.FrozenNextActivityPolicies,
            _lots, _congestion);
        if (!HypotheticalNextProduction.Policies.SequenceEqual(replay.Policies) ||
            !HypotheticalNextProduction.Sources.SequenceEqual(replay.Sources) ||
            (HypotheticalNextProduction.Allocation is null) !=
                (replay.Allocation is null) ||
            (HypotheticalNextProduction.Allocation is not null &&
                (!HypotheticalNextProduction.Allocation.Sources.SequenceEqual(
                    replay.Allocation!.Sources) ||
                 !HypotheticalNextProduction.Allocation.LocalMarkets.SequenceEqual(
                    replay.Allocation.LocalMarkets) ||
                 HypotheticalNextProduction.Allocation.AuthenticatedNominalFreightCost.CompareTo(
                    replay.Allocation.AuthenticatedNominalFreightCost) != 0)))
            throw new InvalidOperationException(
                "Independent VS85 replay diverged from frozen prior activity rates or newly authenticated current physical production.");
        HypotheticalNextProduction.VerifyIndependent();
        RequireCurrent(_session);
    }
}
