using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class CanonicalWorldStateHasher
    : IWorldStateHasher
{
    public const uint FormatVersion = 8U;
    public const uint PhysicalEconomyFormatVersion = 9U;

    private static readonly byte[] Magic =
    {
        (byte)'G',
        (byte)'A',
        (byte)'W',
        (byte)'S'
    };

    public WorldStateHash Compute(
        WorldState worldState)
    {
        ArgumentNullException.ThrowIfNull(
            worldState);

        var format = worldState.PhysicalEconomy is null
            ? FormatVersion : PhysicalEconomyFormatVersion;

        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        hash.AppendData(
            Magic);

        AppendUInt32(
            hash,
            format);

        AppendUInt32(
            hash,
            8U);

        if (worldState.WorldBinding is null)
        {
            AppendUInt32(
                hash,
                0U);
        }
        else
        {
            AppendUInt32(
                hash,
                1U);

            AppendUInt32(
                hash,
                checked(
                    (uint)worldState
                        .WorldBinding
                        .WorldSignatureFormatVersion));

            hash.AppendData(
                Convert.FromHexString(
                    worldState
                        .WorldBinding
                        .WorldSignatureSha256Hex));

            AppendUInt64(
                hash,
                worldState
                    .WorldBinding
                    .StrategicCellCount);
        }

        AppendUInt64(
            hash,
            worldState.Revision);

        AppendUInt32(
            hash,
            10U);

        AppendUInt32(
            hash,
            checked(
                (uint)worldState
                    .Civilizations
                    .Records
                    .Count));

        foreach (var civilization in
            worldState
                .Civilizations
                .Records)
        {
            AppendUInt64(
                hash,
                civilization.Id.Value);

            if (civilization.StartCellId.HasValue)
            {
                AppendUInt32(
                    hash,
                    1U);

                AppendUInt64(
                    hash,
                    civilization
                        .StartCellId
                        .Value
                        .Value);
            }
            else
            {
                AppendUInt32(
                    hash,
                    0U);
            }
        }

        AppendUInt32(
            hash,
            15U);

        AppendUInt32(
            hash,
            checked(
                (uint)worldState
                    .Territory
                    .Controls
                    .Count));

        foreach (var control in
            worldState
                .Territory
                .Controls)
        {
            AppendUInt64(
                hash,
                control.StrategicCellId.Value);

            AppendUInt64(
                hash,
                control.Controller.Value);
        }

        AppendUInt32(
            hash,
            18U);

        AppendUInt32(
            hash,
            checked(
                (uint)worldState
                    .Diplomacy
                    .Relations
                    .Count));

        foreach (var relation in
            worldState
                .Diplomacy
                .Relations)
        {
            AppendUInt64(
                hash,
                relation.First.Value);

            AppendUInt64(
                hash,
                relation.Second.Value);

            AppendUInt32(
                hash,
                checked(
                    (uint)relation.Relation));
        }

        AppendUInt32(
            hash,
            20U);

        AppendUInt32(
            hash,
            checked(
                (uint)worldState
                    .Economy
                    .EconomicPoints
                    .Count));

        foreach (var point in
            worldState
                .Economy
                .EconomicPoints)
        {
            AppendUInt64(
                hash,
                point.Id.Value);

            AppendUInt64(
                hash,
                point.Owner.Value);

            AppendUInt64(
                hash,
                point.StrategicCellId.Value);

            AppendUInt64(
                hash,
                point.Workforce);

            AppendUInt32(
                hash,
                checked(
                    (uint)point
                        .ActivityAllocations
                        .Count));

            foreach (var allocation in
                point.ActivityAllocations)
            {
                AppendUInt32(
                    hash,
                    checked(
                        (uint)allocation.Kind));

                AppendUInt64(
                    hash,
                    allocation.Workforce);
            }
        }

        // Population is resident authoritative state in hash format v8.
        // The initialization bit differentiates an absent census from an
        // explicitly validated census containing zero residents/points.
        AppendUInt32(
            hash,
            25U);

        AppendUInt32(
            hash,
            worldState.Population.IsInitialized ? 1U : 0U);

        AppendUInt32(
            hash,
            checked((uint)worldState.Population.PointPopulations.Count));

        foreach (var population in worldState.Population.PointPopulations)
        {
            AppendUInt64(hash, population.EconomicPointId.Value);
            AppendUInt64(hash, population.Residents);
        }

        AppendUInt32(
            hash,
            30U);

        AppendUInt32(
            hash,
            checked(
                (uint)worldState
                    .Warfare
                    .Units
                    .Count));

        foreach (var unit in
            worldState
                .Warfare
                .Units)
        {
            AppendUInt64(
                hash,
                unit.Id.Value);

            AppendUInt64(
                hash,
                unit.Owner.Value);

            AppendUInt64(
                hash,
                unit.StrategicCellId.Value);
        }

        if (worldState.PhysicalEconomy is { } physical)
        {
            // New v9 tagged section. Old v8 snapshot hashes stay BIT-FOR-BIT
            // identical; physical positions and stocks are no longer invisible
            // to the authoritative hash when this slice exists.
            AppendUInt32(hash, 35U);
            AppendUInt32(hash, checked((uint)physical.CoarseParameters.M));
            AppendUInt32(hash, checked((uint)physical.CoarseParameters.N));
            AppendUInt32(hash, checked((uint)physical.FineParameters.M));
            AppendUInt32(hash, checked((uint)physical.FineParameters.N));
            AppendUInt64(hash, physical.MovementTurn);
            AppendUInt64(hash, physical.WorkerRevision);
            AppendUInt64(hash, physical.EconomicCycles);
            AppendUInt64(hash, physical.TotalWorkers);
            AppendUInt32(hash, checked((uint)physical.Workers.Count));
            foreach (var entry in physical.Workers)
            {
                AppendUInt64(hash, entry.TileId.FineStrategicCellId.Value);
                AppendUInt32(hash, checked((uint)entry.Activity));
                AppendUInt64(hash, entry.Workers);
            }
            AppendUInt32(hash, checked((uint)physical.Stocks.Count));
            foreach (var entry in physical.Stocks)
            {
                AppendUInt64(hash, entry.TileId.FineStrategicCellId.Value);
                AppendUInt32(hash, entry.CommodityId.Value);
                AppendUInt64(hash, entry.RawQuantity);
            }
        }

        var digest =
            hash.GetHashAndReset();

        return new WorldStateHash(
            format,
            Convert.ToHexString(
                digest));
    }

    private static void AppendUInt32(
        IncrementalHash hash,
        uint value)
    {
        Span<byte> buffer =
            stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(
            buffer,
            value);

        hash.AppendData(
            buffer);
    }

    private static void AppendUInt64(
        IncrementalHash hash,
        ulong value)
    {
        Span<byte> buffer =
            stackalloc byte[8];

        BinaryPrimitives.WriteUInt64BigEndian(
            buffer,
            value);

        hash.AppendData(
            buffer);
    }
}