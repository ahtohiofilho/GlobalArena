using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Single-writer, in-memory publication boundary between a closed headless turn
/// and the canonical game session. It does not implement economy, combat,
/// simultaneous order semantics, phase scheduling, persistence or network delivery.
/// Domain/phase policies must finish before the authoritative result arrives here.
/// </summary>
public sealed class AuthoritativeTurnCommitGate
{
    private readonly CanonicalGameSession _session;
    private readonly ITurnPolicyResolutionGate _resolutionGate;
    private readonly object _sync = new();

    public uint RulesetVersion { get; }
    public TurnNumber NextTurn { get; private set; }
    public TurnNumber? LastCommittedTurn { get; private set; }

    public AuthoritativeTurnCommitGate(
        CanonicalGameSession session,
        ITurnPolicyResolutionGate resolutionGate,
        TurnNumber firstTurn,
        uint rulesetVersion)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _resolutionGate = resolutionGate ?? throw new ArgumentNullException(nameof(resolutionGate));
        if (firstTurn.Value == 0UL)
            throw new ArgumentOutOfRangeException(nameof(firstTurn));
        if (rulesetVersion == 0U)
            throw new ArgumentOutOfRangeException(nameof(rulesetVersion));

        NextTurn = firstTurn;
        RulesetVersion = rulesetVersion;
    }

    /// <summary>
    /// Resolves a policy-closed turn and publishes precisely its final snapshot.
    /// An open turn does not advance. Conflicting or repeated turn submissions fail
    /// closed rather than being silently treated as successful retries.
    /// A single authoritative owner must exclusively manage the supplied session.
    /// </summary>
    public TurnPolicyResolutionGateResult ResolveAndCommit(
        TurnPolicyResolutionGateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        lock (_sync)
        {
            var turn = input.PolicyInput.Turn;
            if (turn != NextTurn)
                throw new InvalidOperationException("Turn publication requires the exact next authoritative turn.");

            // Calculate next identity before any state publication. This prevents
            // a failed overflow check from creating a partial commit.
            var followingTurn = turn.Next();
            var planningSnapshot = _session.Snapshot;
            if (!ReferenceEquals(input.ResolutionInput.WorldState, planningSnapshot))
                throw new InvalidOperationException("Turn was planned against a stale or foreign session snapshot.");

            foreach (var command in input.ResolutionInput.Commands)
            {
                if (command is null || command.Id.Turn != turn)
                    throw new InvalidOperationException("All commands must belong to the turn being committed.");
            }

            var gateResult = _resolutionGate.Resolve(input)
                ?? throw new InvalidOperationException("Turn policy gate returned no result.");

            if (!gateResult.WasResolved)
            {
                if (gateResult.ResolutionResult is not null)
                    throw new InvalidOperationException("An open turn cannot carry a resolved state.");
                return gateResult;
            }

            var resolved = gateResult.ResolutionResult
                ?? throw new InvalidOperationException("A closed turn must contain a final state.");

            if (!ReferenceEquals(_session.Snapshot, planningSnapshot))
                throw new InvalidOperationException("Session changed while the turn was being resolved.");

            // AcceptSnapshot checks world identity and revision; nothing is
            // published until the policy and domain resolver have completed.
            // Equal revisions and genuine no-op turns remain representable.
            _session.AcceptSnapshot(resolved.ResultingWorldState);
            LastCommittedTurn = turn;
            NextTurn = followingTurn;
            return gateResult;
        }
    }
}
