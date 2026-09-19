using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class CanonicalWorldStateHasher
    : IWorldStateHasher
{
    public const uint FormatVersion = 1U;

    private const int PayloadByteLength = 16;

    public WorldStateHash Compute(
        WorldState worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);

        Span<byte> payload =
            stackalloc byte[PayloadByteLength];

        payload[0] = (byte)'G';
        payload[1] = (byte)'A';
        payload[2] = (byte)'W';
        payload[3] = (byte)'S';

        BinaryPrimitives.WriteUInt32BigEndian(
            payload.Slice(
                4,
                4),
            FormatVersion);

        BinaryPrimitives.WriteUInt64BigEndian(
            payload.Slice(
                8,
                8),
            worldState.Revision);

        var digest =
            SHA256.HashData(
                payload);

        return new WorldStateHash(
            FormatVersion,
            Convert.ToHexString(
                digest));
    }
}
