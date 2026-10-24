using System;

namespace GlobalArena.Kernel;

/// <summary>
/// A command event comes from a submitted player or AI order. A system event
/// comes from a named authoritative turn phase, not from a fabricated command.
/// </summary>
public enum EventOriginKind
{
    Command = 1,
    System = 2
}

public readonly record struct EventId
{
    private readonly CommandId _originCommandId;

    public EventOriginKind OriginKind { get; }
    public TurnNumber Turn { get; }
    public string? SystemPhaseId { get; }
    public ulong SystemOrdinal { get; }
    public ulong Sequence { get; }

    /// <summary>
    /// Legacy command origin. Explicitly unavailable for system-generated events.
    /// This prevents economic events from impersonating player commands.
    /// </summary>
    // A default(EventId) is invalid, but its legacy command field must still
    // be inspectable so domain constructors can reject it with ArgumentException.
    // Only a genuinely system-originated event has no command provenance.
    public CommandId OriginCommandId => OriginKind == EventOriginKind.System
        ? throw new InvalidOperationException(
            "A system event has no originating player command.")
        : _originCommandId;

    // Source-compatible command-event constructor.
    public EventId(CommandId originCommandId, ulong sequence)
    {
        if (sequence == 0UL)
            throw new ArgumentOutOfRangeException(nameof(sequence),
                "Event sequence must be greater than zero.");
        _originCommandId = originCommandId;
        OriginKind = EventOriginKind.Command;
        Turn = originCommandId.Turn;
        SystemPhaseId = null;
        SystemOrdinal = 0UL;
        Sequence = sequence;
    }

    private EventId(TurnNumber turn, string phaseId, ulong ordinal, ulong sequence)
    {
        if (turn.Value == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turn),
                "System events require a valid authoritative turn.");
        if (string.IsNullOrWhiteSpace(phaseId))
            throw new ArgumentException(
                "System event phase identity must not be blank.", nameof(phaseId));
        if (ordinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(ordinal),
                "System event ordinal must be greater than zero.");
        if (sequence == 0UL)
            throw new ArgumentOutOfRangeException(nameof(sequence),
                "Event sequence must be greater than zero.");
        _originCommandId = default;
        OriginKind = EventOriginKind.System;
        Turn = turn;
        SystemPhaseId = phaseId;
        SystemOrdinal = ordinal;
        Sequence = sequence;
    }

    /// <summary>
    /// Stable provenance within one turn and named phase, independent of the
    /// submitted command batch and the order in which commands arrived.
    /// </summary>
    public static EventId ForSystemPhase(
        TurnNumber turn, string phaseId, ulong ordinal, ulong sequence = 1UL) =>
        new(turn, phaseId, ordinal, sequence);
}
