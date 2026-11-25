using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// No closed cycle is NOT the same as a closed cycle that transported no
/// goods. AuthorizedSubset never exposes hidden global totals or edge IDs.
/// </summary>
public enum FoundationEconomicTradeOverlayAvailability : uint
{
    NoClosedCycle = 1,
    Ready = 2,
    AuthorizedSubset = 3
}

/// <summary>
/// Per-participant immutable view of the last ACTUALLY closed economic cycle.
/// Nothing in this type makes a market settle or guesses a trade from workers.
/// </summary>
public sealed class FoundationLastClosedEconomicTradeView
{
    public FoundationEconomicTradeOverlayAvailability Availability { get; }
    public RuntimeWorldBinding WorldBinding { get; }
    public GoldbergParameters FineTopology { get; }
    public ulong? EconomicCycleOrdinal { get; }
    public TurnNumber? ClosedAtWorkerTurn { get; }
    public ulong? PublishedRevision { get; }
    public WorldStateHash? PublishedHash { get; }
    public string? CargoUnitCode { get; }
    public IReadOnlyList<FoundationPhysicalTradeEdgeLoad> UsedEdges { get; }

    internal FoundationLastClosedEconomicTradeView(
        FoundationEconomicTradeOverlayAvailability availability,
        RuntimeWorldBinding binding, GoldbergParameters fine,
        ulong? cycle, TurnNumber? workerTurn, ulong? revision,
        WorldStateHash? hash, string? unit,
        IEnumerable<FoundationPhysicalTradeEdgeLoad> edges)
    {
        Availability = availability;
        WorldBinding = binding;
        FineTopology = fine;
        EconomicCycleOrdinal = cycle;
        ClosedAtWorkerTurn = workerTurn;
        PublishedRevision = revision;
        PublishedHash = hash;
        CargoUnitCode = unit;
        UsedEdges = Array.AsReadOnly(edges.ToArray());
    }
}

/// <summary>
/// Privileged IN-ASSEMBLY handoff, reserved for a future authoritative
/// economic SETTLEMENT phase. The current v11 worker-turn host cannot create
/// this proof, and its worker journal is NOT a substitute for one.
/// No public application-client method issues this receipt.
/// </summary>
internal sealed class FoundationSettledEconomicTradeCycleArtifact
{
    internal WorldState CommittedSnapshot { get; }
    internal WorldStateHash StateHash { get; }
    internal ulong EconomicCycleOrdinal { get; }
    internal TurnNumber ClosedAtWorkerTurn { get; }
    internal FoundationPhysicalTradeEdgeLoadAudit CargoAudit { get; }
    internal IReadOnlyDictionary<CivilizationId, IReadOnlyList<PhysicalTacticalEdgeId>>
        AuthorizedVisibleEdges { get; }

    internal FoundationSettledEconomicTradeCycleArtifact(
        WorldState snapshot, WorldStateHash hash, ulong cycleOrdinal,
        TurnNumber closedAtWorkerTurn,
        FoundationPhysicalTradeEdgeLoadAudit cargoAudit,
        IReadOnlyDictionary<CivilizationId,
            IReadOnlyList<PhysicalTacticalEdgeId>> authorizedVisibleEdges)
    {
        CommittedSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        StateHash = hash;
        EconomicCycleOrdinal = cycleOrdinal;
        ClosedAtWorkerTurn = closedAtWorkerTurn;
        CargoAudit = cargoAudit ?? throw new ArgumentNullException(nameof(cargoAudit));
        AuthorizedVisibleEdges = authorizedVisibleEdges
            ?? throw new ArgumentNullException(nameof(authorizedVisibleEdges));
    }
}

/// <summary>
/// Strict read-side boundary. Production currently only exposes
/// NoClosedCycle: no commerce-resolution phase issues a settlement artifact.
/// The internal publication method validates world binding, hash/revision,
/// exact topology, monotone economic chronology, and viewer-specific
/// authorized edges before atomically replacing the previous read view.
/// </summary>
internal sealed class FoundationEconomicTradeCycleReadBoundary
{
    private readonly RuntimeWorldBinding _binding;
    private readonly GoldbergParameters _fine;
    private readonly HashSet<CivilizationId> _participants;
    private readonly Dictionary<CivilizationId,
        FoundationLastClosedEconomicTradeView> _noCycle;
    private readonly object _sync = new();

    private Dictionary<CivilizationId,
        FoundationLastClosedEconomicTradeView>? _last;
    private ulong _lastCycleOrdinal;
    // PRIVILEGED economic input: full crossing vector is never exposed
    // through participant-specific read views or the Godot client.
    private FoundationPhysicalLaggedFreightQuoteState? _nextSettledFreight;
    private string? _settledFreightCargoUnitCode;

    internal FoundationEconomicTradeCycleReadBoundary(
        RuntimeWorldBinding world, GoldbergParameters fine,
        IEnumerable<CivilizationId> participants)
    {
        _binding = world ?? throw new ArgumentNullException(nameof(world));
        if (!fine.IsValid)
            throw new ArgumentException(
                "Closed-cycle views need an existing fine Goldberg topology.",
                nameof(fine));
        _fine = fine;
        ArgumentNullException.ThrowIfNull(participants);
        var ids = participants.ToArray();
        if (ids.Length == 0 ||
            ids.Any(civ => !civ.IsValid) ||
            ids.Distinct().Count() != ids.Length)
            throw new ArgumentException(
                "Authorized participant identities must be valid and unique.",
                nameof(participants));
        _participants = ids.ToHashSet();
        _noCycle = ids.ToDictionary(
            civ => civ, _ => new FoundationLastClosedEconomicTradeView(
                FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                _binding, _fine, null, null, null, null, null,
                Array.Empty<FoundationPhysicalTradeEdgeLoad>()));
    }

    internal FoundationLastClosedEconomicTradeView Read(
        CivilizationId requester, WorldState currentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        if (!_participants.Contains(requester))
            throw new InvalidOperationException(
                "Unknown civilization cannot read economic-cycle transport.");
        if (currentSnapshot.WorldBinding != _binding)
            throw new InvalidOperationException(
                "Economic-cycle view belongs to another world.");
        lock (_sync)
            return _last is null ? _noCycle[requester] : _last[requester];
    }

    /// <summary>
    /// New v15 authoritative read-only publication. Invoked ONLY after
    /// all-ready commit AND an independent recomputed event replay passed.
    /// Each participant's edge volumes are calculated exclusively from
    /// sales it actually bought or sold, not the GLOBAL cargo audit. This
    /// avoids leaking unrelated routes or global edge-volume totals.
    ///
    /// No new rules for exploration, surveillance, geography or diplomatic
    /// intelligence are inferred by this basic private trade entitlement.
    /// </summary>
    internal void PublishVerifiedParticipantSettlement(
        FoundationEconomicSalesSettledEvent settled,
        WorldState committedSnapshot, TurnNumber nextWorkerTurn)
    {
        ArgumentNullException.ThrowIfNull(settled);
        ArgumentNullException.ThrowIfNull(committedSnapshot);
        var audited = settled.AuditedParticipantEdges;
        if (settled.Id.OriginKind != EventOriginKind.System ||
            settled.Id.SystemPhaseId !=
                FoundationAuthoritativeEconomicSalesSettlementPhase.StablePhaseId ||
            settled.Id.Turn.Value == 0UL ||
            settled.Id.Turn.Value >= nextWorkerTurn.Value ||
            settled.EconomicCycleOrdinal == 0UL ||
            committedSnapshot.WorldBinding != _binding ||
            committedSnapshot.Treasury.LastSettledEconomicCycleOrdinal !=
                settled.EconomicCycleOrdinal ||
            committedSnapshot.Treasury.LastSettledCargoUnitCode !=
                settled.CargoUnitCode ||
            committedSnapshot.Revision <= settled.ExpectedWorldRevision ||
            !committedSnapshot.Workers.HasAuthoritativeOccupations ||
            audited is null ||
            audited.Count != _participants.Count ||
            audited.Keys.Any(civ => !_participants.Contains(civ)))
            throw new InvalidOperationException(
                "Participant trade publication requires the actual just-" +
                "committed v15 market, correct world and complete privacy.");

        var hash = new CanonicalWorldStateHasher().Compute(committedSnapshot);
        if (hash.FormatVersion !=
            CanonicalWorldStateHasher.ClosedEconomicCycleFormatVersion)
            throw new InvalidOperationException(
                "No participant route may be exposed from an uncommitted market.");

        // Materialize every private presentation view BEFORE the atomic
        // dictionary switch. Global volume/used-edge counts never enter
        // individual view metadata.
        var next = new Dictionary<CivilizationId,
            FoundationLastClosedEconomicTradeView>(_participants.Count);
        var validEdges = committedSnapshot.Transport.Edges
            .ToDictionary(e => e.Edge);
        foreach (var civ in _participants.OrderBy(c => c.Value))
        {
            if (!audited.TryGetValue(civ, out var visible) ||
                visible is null)
                throw new InvalidOperationException(
                    "A participating civilization has no authorized trade view.");
            var localIds = new HashSet<PhysicalTacticalEdgeId>();
            foreach (var line in visible)
            {
                if (line is null || line.TotalCargoMicro == 0UL ||
                    !localIds.Add(line.Edge) ||
                    !validEdges.TryGetValue(line.Edge, out var actual) ||
                    line.Edge.FineGoldbergParameters != _fine ||
                    line.FirstTile.FineGoldbergParameters != _fine ||
                    line.SecondTile.FineGoldbergParameters != _fine ||
                    line.Owner != actual.InfrastructureOwner ||
                    line.ByCommodity.Aggregate(0UL, (sum, item) =>
                        checked(sum + item.CargoMicro)) != line.TotalCargoMicro)
                    throw new InvalidOperationException(
                        "Participant route must reference a real, positive " +
                        "owned crossing without duplicates or invented load.");
            }

            // "AuthorizedSubset" is deliberately always used: even when
            // a participant happens to see every used edge, the UI must
            // not reveal whether unseen global trade exists elsewhere.
            next.Add(civ, new FoundationLastClosedEconomicTradeView(
                FoundationEconomicTradeOverlayAvailability.AuthorizedSubset,
                _binding, _fine, settled.EconomicCycleOrdinal,
                settled.Id.Turn, committedSnapshot.Revision, hash,
                settled.CargoUnitCode, visible));
        }

        lock (_sync)
        {
            if (settled.EconomicCycleOrdinal !=
                    checked(_lastCycleOrdinal + 1UL))
                throw new InvalidOperationException(
                    "Visible economic history must be consecutive: " +
                    "reject duplicate or stale trade publications.");
            _last = next;
            _lastCycleOrdinal = settled.EconomicCycleOrdinal;
            // v15 has its OWN authoritative quote memory in the
            // WorldState treasury. Do not synthesize legacy quote state.
            // This field only services the older independent publisher.
            _nextSettledFreight = null;
            _settledFreightCargoUnitCode = null;
        }
    }

    /// <summary>
    /// Publish exclusively from the replay-verified temporal sale after the
    /// collective commit. Historical route ownership was checked against
    /// the actual freight-stage world during derivation and replay; it
    /// must not be recolored from later commissioned infrastructure.
    /// </summary>
    internal void PublishVerifiedTemporalSettlement(
        FoundationTemporalSalesSettledEvent settled,
        WorldState committedSnapshot, TurnNumber nextWorkerTurn)
    {
        ArgumentNullException.ThrowIfNull(settled);
        ArgumentNullException.ThrowIfNull(committedSnapshot);
        var audited = settled.AuditedParticipantEdges;
        if (settled.Id.OriginKind != EventOriginKind.System ||
            settled.Id.SystemPhaseId != FoundationTemporalEconomicJournalGate.StablePhaseId ||
            settled.Id.SystemOrdinal != 1UL ||
            settled.Id.Turn.Value == 0UL ||
            settled.Id.Turn.Value >= nextWorkerTurn.Value ||
            settled.EconomicCycleOrdinal == 0UL ||
            committedSnapshot.WorldBinding != _binding ||
            committedSnapshot.Treasury.LastSettledEconomicCycleOrdinal != settled.EconomicCycleOrdinal ||
            committedSnapshot.Treasury.LastSettledCargoUnitCode != settled.CargoUnitCode ||
            committedSnapshot.Revision <= settled.PreSettlementRevision ||
            !committedSnapshot.Workers.HasAuthoritativeOccupations ||
            audited is null || audited.Count != _participants.Count ||
            audited.Keys.Any(civ => !_participants.Contains(civ)))
            throw new InvalidOperationException("Temporal trade publication requires the committed replay-verified sale.");

        var hash = new CanonicalWorldStateHasher().Compute(committedSnapshot);
        var validEdges = committedSnapshot.Transport.Edges.Select(e => e.Edge).ToHashSet();
        var next = new Dictionary<CivilizationId, FoundationLastClosedEconomicTradeView>(_participants.Count);
        foreach (var civ in _participants.OrderBy(c => c.Value))
        {
            if (!audited.TryGetValue(civ, out var visible) || visible is null)
                throw new InvalidOperationException("Missing participant trade evidence.");
            var ids = new HashSet<PhysicalTacticalEdgeId>();
            foreach (var line in visible)
            {
                if (line is null || line.TotalCargoMicro == 0UL ||
                    !ids.Add(line.Edge) || !validEdges.Contains(line.Edge) ||
                    line.Edge.FineGoldbergParameters != _fine ||
                    line.FirstTile.FineGoldbergParameters != _fine ||
                    line.SecondTile.FineGoldbergParameters != _fine ||
                    line.ByCommodity.Aggregate(0UL, (sum, item) => checked(sum + item.CargoMicro)) != line.TotalCargoMicro)
                    throw new InvalidOperationException("Invalid replay-verified temporal trade crossing.");
            }
            next.Add(civ, new FoundationLastClosedEconomicTradeView(
                FoundationEconomicTradeOverlayAvailability.AuthorizedSubset,
                _binding, _fine, settled.EconomicCycleOrdinal, settled.Id.Turn,
                committedSnapshot.Revision, hash, settled.CargoUnitCode, visible));
        }
        lock (_sync)
        {
            if (settled.EconomicCycleOrdinal != checked(_lastCycleOrdinal + 1UL))
                throw new InvalidOperationException("Temporal trade publication must be consecutive.");
            _last = next;
            _lastCycleOrdinal = settled.EconomicCycleOrdinal;
            _nextSettledFreight = null;
            _settledFreightCargoUnitCode = null;
        }
    }

    /// <summary>
    /// Privileged kernel-only input for the NEXT economic turn. Returns
    /// null exactly when no economic cycle has ever closed. Rejects a
    /// published history whose freight was NOT registered atomically;
    /// never use preview/candidate quotes as a fallback.
    /// </summary>
    internal FoundationPhysicalLaggedFreightQuoteState?
        ReadLastSettledFreightForNextCycle(WorldState currentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        if (currentSnapshot.WorldBinding != _binding)
            throw new InvalidOperationException(
                "Cannot read freight history from another world.");
        lock (_sync)
        {
            if (_lastCycleOrdinal == 0UL)
                return null;
            if (_nextSettledFreight is null ||
                !_nextSettledFreight.HasSettledProvenance ||
                _nextSettledFreight.AuditedPreviewCycles != _lastCycleOrdinal)
                throw new InvalidOperationException(
                    "Economic history exists but no trusted settled " +
                    "freight quote is available; never substitute a preview.");
            return _nextSettledFreight;
        }
    }

    /// <summary>
    /// Transactional handoff for a FUTURE real economic settlement phase:
    /// accept the canonical settled trade publication AND prepare one
    /// next-cycle freight quotation from that very same audited cargo.
    /// All preflights must succeed before either historical state changes.
    /// The closed current turn is NEVER re-priced or re-solved.
    ///
    /// The existing worker-only gameplay host cannot invoke a genuine
    /// settled economic artifact; a worker turn is not a settlement.
    /// </summary>
    internal void PublishSettledCycleWithNextFreight(
        FoundationSettledEconomicTradeCycleArtifact artifact,
        WorldState currentSnapshot,
        TurnNumber nextWorkerTurn,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowRoads,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        string comparableCargoUnitCode,
        uint smoothingDenominator =
            FoundationPhysicalLaggedFreightCyclePlanner.DefaultSmoothingDenominator)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        lock (_sync)
        {
            if (_lastCycleOrdinal == ulong.MaxValue ||
                artifact.EconomicCycleOrdinal != _lastCycleOrdinal + 1UL)
                throw new InvalidOperationException(
                    "Freight history requires consecutive settled " +
                    "economic cycles; no missing or duplicated cycle.");
            if (_lastCycleOrdinal != 0UL &&
                (_nextSettledFreight is null ||
                 _settledFreightCargoUnitCode != comparableCargoUnitCode))
                throw new InvalidOperationException(
                    "Cannot resume freight from an unpublished or differently " +
                    "unitized closed cycle. No synthetic history is permitted.");

            // This is a pure, deterministic O(edges) post-close calculation.
            // No market solve, path search or same-turn cost feedback.
            var nextPhysicalEdges = FoundationPhysicalSettledFreightQuoteUpdater
                .FromClosedAudit(artifact.CargoAudit, _fine,
                    freeFlowRoads, carryingScales, comparableCargoUnitCode,
                    artifact.EconomicCycleOrdinal, _nextSettledFreight,
                    smoothingDenominator);
            // Only this privileged publisher may stamp provenanced quotes.
            // Preallocate the immutable object before the final atomic
            // publication so there is no post-commit allocation failure.
            // It cannot escape to another consumer until publication succeeds.
            var nextFreight = new FoundationPhysicalLaggedFreightQuoteState(
                artifact.EconomicCycleOrdinal, nextPhysicalEdges,
                hasSettledProvenance: true);

            // Existing boundary validates canonical world hash, immutable
            // committed snapshot, turn chronology, fine topology, and
            // per-civilization visibility. Lock is reentrant in C#.
            // On any failure, neither historical trade nor freight changes.
            PublishFromSettledEconomicPhase(
                artifact, currentSnapshot, nextWorkerTurn);

            _nextSettledFreight = nextFreight;
            _settledFreightCargoUnitCode = comparableCargoUnitCode;
        }
    }

    /// <summary>
    /// Unreachable from the currently configured v11 worker-only phases.
    /// The future settlement phase must supply committed final deliveries,
    /// unit conversion, and authoritative per-civilization visibility.
    /// Validation here prevents metadata mismatch and accidental leakage;
    /// this method does NOT itself prove a solver calculated real deliveries.
    /// </summary>
    internal void PublishFromSettledEconomicPhase(
        FoundationSettledEconomicTradeCycleArtifact artifact,
        WorldState currentSnapshot,
        TurnNumber nextWorkerTurn)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(currentSnapshot);
        var snapshot = artifact.CommittedSnapshot;
        var audit = artifact.CargoAudit;
        if (artifact.EconomicCycleOrdinal == 0UL ||
            artifact.ClosedAtWorkerTurn.Value == 0UL ||
            nextWorkerTurn.Value == 0UL ||
            artifact.ClosedAtWorkerTurn.Value >= nextWorkerTurn.Value ||
            !ReferenceEquals(snapshot, currentSnapshot) ||
            snapshot.WorldBinding != _binding ||
            snapshot.Workers.HasAuthoritativeOccupations != true ||
            artifact.StateHash != new CanonicalWorldStateHasher().Compute(snapshot))
            throw new InvalidOperationException(
                "Economic settlement must match one committed authoritative " +
                "v11 world snapshot, digest and completed worker turn.");
        if (string.IsNullOrWhiteSpace(audit.CargoUnitCode) ||
            audit.UsedEdges.Any(line =>
                line.Edge.FineGoldbergParameters != _fine ||
                line.FirstTile.FineGoldbergParameters != _fine ||
                line.SecondTile.FineGoldbergParameters != _fine))
            throw new InvalidOperationException(
                "Economic cargo audit belongs to a different fine topology.");

        var visible = artifact.AuthorizedVisibleEdges;
        if (visible.Count != _participants.Count ||
            visible.Keys.Any(civ => !_participants.Contains(civ)))
            throw new InvalidOperationException(
                "Economic settlement must specify visibility for each participant.");

        var used = audit.UsedEdges.ToDictionary(edge => edge.Edge);
        var next = new Dictionary<CivilizationId,
            FoundationLastClosedEconomicTradeView>(_participants.Count);
        foreach (var civ in _participants.OrderBy(x => x.Value))
        {
            if (!visible.TryGetValue(civ, out var candidates) ||
                candidates is null)
                throw new InvalidOperationException(
                    "Missing participant visibility evidence.");
            var ids = candidates.ToArray();
            if (ids.Any(edge => !edge.IsValid || !used.ContainsKey(edge)) ||
                ids.Distinct().Count() != ids.Length)
                throw new InvalidOperationException(
                    "Visibility includes unknown, duplicated or foreign trade edges.");
            var byEdge = ids.ToHashSet();
            var authorized = audit.UsedEdges
                .Where(edge => byEdge.Contains(edge.Edge)).ToArray();
            var availability = ids.Length == used.Count
                ? FoundationEconomicTradeOverlayAvailability.Ready
                : FoundationEconomicTradeOverlayAvailability.AuthorizedSubset;
            next.Add(civ, new FoundationLastClosedEconomicTradeView(
                availability, _binding, _fine,
                artifact.EconomicCycleOrdinal,
                artifact.ClosedAtWorkerTurn,
                snapshot.Revision, artifact.StateHash,
                audit.CargoUnitCode, authorized));
        }

        // Nothing changes if validation fails; a later worker-only turn does
        // not replace the economic snapshot or recolor its historical owners.
        lock (_sync)
        {
            if (artifact.EconomicCycleOrdinal <= _lastCycleOrdinal ||
                (_last is not null && next.Values.Any(view =>
                    view.PublishedRevision < _last.Values.First().PublishedRevision ||
                    view.ClosedAtWorkerTurn!.Value.Value <
                        _last.Values.First().ClosedAtWorkerTurn!.Value.Value)))
                throw new InvalidOperationException(
                    "Closed economic cycles cannot be replayed, reordered or rolled back.");
            _last = next;
            _lastCycleOrdinal = artifact.EconomicCycleOrdinal;
            // A legacy publication WITHOUT the protected freight policy
            // cannot leave a stale previous-cycle quote available to a
            // future market turn.
            _nextSettledFreight = null;
            _settledFreightCargoUnitCode = null;
        }
    }
}
