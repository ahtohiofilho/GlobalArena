namespace GlobalArena.World;

public sealed class WorldGenerationRandomStream
{
    private ulong _state;

    internal WorldGenerationRandomStream(
        ulong seed)
    {
        _state = seed;
    }

    public ulong NextUInt64()
    {
        unchecked
        {
            _state +=
                0x9E3779B97F4A7C15UL;

            var value =
                _state;

            value =
                (value ^ (value >> 30))
                * 0xBF58476D1CE4E5B9UL;

            value =
                (value ^ (value >> 27))
                * 0x94D049BB133111EBUL;

            return value
                ^ (value >> 31);
        }
    }
}
