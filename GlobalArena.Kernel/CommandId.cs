namespace GlobalArena.Kernel;

public readonly record struct CommandId
{
    public TurnNumber Turn { get; }

    public ulong Sequence { get; }

    public CommandId(TurnNumber turn, ulong sequence)
    {
        if (sequence == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                "Command sequence must be greater than zero.");
        }

        Turn = turn;
        Sequence = sequence;
    }
}
