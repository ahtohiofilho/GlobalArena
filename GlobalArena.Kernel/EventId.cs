namespace GlobalArena.Kernel;

public readonly record struct EventId
{
    public CommandId OriginCommandId { get; }

    public ulong Sequence { get; }

    public EventId(CommandId originCommandId, ulong sequence)
    {
        if (sequence == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                "Event sequence must be greater than zero.");
        }

        OriginCommandId = originCommandId;
        Sequence = sequence;
    }
}
