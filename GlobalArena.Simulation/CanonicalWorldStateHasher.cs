using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class CanonicalWorldStateHasher
    : IWorldStateHasher
{
    // Legacy v8 hashes are byte-for-byte unchanged until worker census is initialized.
    public const uint FormatVersion = 8U;
    public const uint PhysicalWorkerFormatVersion = 9U;
    public const uint TacticalWorkerFormatVersion = 10U;
    public const uint OccupiedTacticalWorkerFormatVersion = 11U;
    // Only explicitly deployed physical infrastructure uses the v12 wire
    // extension; all old v8-v11 hashes remain byte-for-byte unchanged.
    public const uint PhysicalTransportFormatVersion = 12U;

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

        var version = worldState.Transport.IsInitialized
            ? PhysicalTransportFormatVersion
            : worldState.Workers.IsInitialized
                ? worldState.Workers.HasAuthoritativeOccupations
                    ? OccupiedTacticalWorkerFormatVersion
                    : worldState.Workers.HasAuthoritativeTacticalLocations
                        ? TacticalWorkerFormatVersion : PhysicalWorkerFormatVersion
                : FormatVersion;

        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        hash.AppendData(
            Magic);

        AppendUInt32(
            hash,
            version);

        AppendUInt32(
            hash,
            version);

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

        // New section is present ONLY in initialized individual-worker states.
        // Legacy absent-worker states retain exactly the original v8 bytes.
        if (worldState.Workers.IsInitialized)
        {
            AppendUInt32(hash, 35U);
            AppendUInt32(hash, 1U);
            AppendUInt32(hash, checked((uint)worldState.Workers.Workers.Count));
            foreach (var worker in worldState.Workers.Workers)
            {
                AppendUInt64(hash, worker.Id.Value);
                AppendUInt64(hash, worker.Owner.Value);
                AppendUInt64(hash, worker.CellId.Value);
                AppendUInt32(hash, worker.Commodity.Value);
                AppendUInt64(hash, worker.OutputMicroPerTurn);
                AppendUInt32(hash, (uint)worker.Activity);
                AppendUInt32(hash, worker.DemandMultiplierMicro);
                // v10 extends each canonical worker with QUALIFIED fine
                // geometry identity. v8/v9 byte streams are unchanged.
                if (version >= TacticalWorkerFormatVersion)
                {
                    var tile = worker.TacticalTileId ??
                        throw new InvalidOperationException(
                            "v10 worker census lacks an authoritative fine tile.");
                    AppendUInt32(hash, checked((uint)tile.FineGoldbergParameters.M));
                    AppendUInt32(hash, checked((uint)tile.FineGoldbergParameters.N));
                    AppendUInt64(hash, tile.FineStrategicCellId.Value);
                }
                // v11 explicitly adds a productive occupation code per
                // individual AFTER the v10 qualified fine geometry.
                // All old v8/v9/v10 byte streams remain unchanged.
                if (version >= OccupiedTacticalWorkerFormatVersion)
                    AppendUInt32(hash, (uint)(worker.Occupation ??
                        throw new InvalidOperationException(
                            "v11 worker has no authoritative productive occupation.")));
            }
        }

        // v12 transport data are canonical world facts, NOT preview quotes.
        // Include every crossing, natural base cost, cargo capacity, owner
        // and tariff. No freight surcharge or proposed market Q is hashed.
        if (worldState.Transport.IsInitialized)
        {
            AppendUInt32(hash, 40U);
            AppendUInt32(hash, 1U);
            AppendUInt32(hash, checked((uint)worldState.Transport.Edges.Count));
            foreach (var road in worldState.Transport.Edges)
            {
                AppendUInt32(hash, checked(
                    (uint)road.Edge.FineGoldbergParameters.M));
                AppendUInt32(hash, checked(
                    (uint)road.Edge.FineGoldbergParameters.N));
                AppendUInt64(hash, road.Edge.FineStrategicEdgeId.Value);
                AppendUInt32(hash, (uint)road.OwnershipKind);
                AppendUInt32(hash, road.InfrastructureOwner.HasValue ? 1U : 0U);
                if (road.InfrastructureOwner.HasValue)
                    AppendUInt64(hash, road.InfrastructureOwner.Value.Value);
                AppendUInt64(hash, road.FreePhysicalCostMicroPerWholeCargoUnit);
                AppendUInt64(hash, road.CargoMicroAtReferenceLoad);
                AppendUInt64(hash, road.ExternalTariffMicroPerWholeCargoUnit);
            }
        }

        var digest =
            hash.GetHashAndReset();

        return new WorldStateHash(
            version,
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