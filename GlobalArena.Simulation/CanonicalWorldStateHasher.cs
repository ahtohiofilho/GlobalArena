using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class CanonicalWorldStateHasher
    : IWorldStateHasher
{
    // Hash v8 remains byte-for-byte compatible for legacy snapshots.
    // v9 commits to the added physical economy without changing old saves.
    public const uint FormatVersion = 8U;
    public const uint PhysicalEconomyFormatVersion = 9U;
    public const uint AdaptivePhysicalEconomyFormatVersion = 10U;
    public const uint Canonical01CPhysicalEconomyFormatVersion = 11U;
    public const uint PhysicalFoodCashFormatVersion = 12U;
    public const uint TimedWorkerTransitFormatVersion = 13U;

    private static readonly byte[] Magic =
    {
        (byte)'G',
        (byte)'A',
        (byte)'W',
        (byte)'S'
    };

    public WorldStateHash Compute(WorldState worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);
        return worldState.PhysicalEconomy is null
            ? ComputeLegacyV8(worldState)
            : ComputePhysicalV9(worldState);
    }

    private static WorldStateHash ComputeLegacyV8(WorldState worldState)
    {
        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        hash.AppendData(
            Magic);

        AppendUInt32(
            hash,
            FormatVersion);

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

        var digest =
            hash.GetHashAndReset();

        return new WorldStateHash(
            FormatVersion,
            Convert.ToHexString(
                digest));
    }

    private static WorldStateHash ComputePhysicalV9(WorldState worldState)
    {
        var physical = worldState.PhysicalEconomy
            ?? throw new InvalidOperationException("Missing physical economy.");
        var legacy = ComputeLegacyV8(worldState);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // Explicit domain separator, tagged fields and the exact legacy
        // digest bind all pre-existing state + topology to the new version.
        hash.AppendData(Magic);
        var format = physical.MovementMode == PhysicalWorkerMovementMode.TimedAdjacent
            ? TimedWorkerTransitFormatVersion
            : physical.FoodCash is not null
            ? PhysicalFoodCashFormatVersion
            : physical.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C
            ? Canonical01CPhysicalEconomyFormatVersion
            : physical.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty
                ? AdaptivePhysicalEconomyFormatVersion : PhysicalEconomyFormatVersion;
        AppendUInt32(hash, format);
        AppendUInt32(hash, 90U);
        hash.AppendData(Convert.FromHexString(legacy.HexDigest));
        AppendUInt32(hash, checked((uint)physical.CoarseParameters.M));
        AppendUInt32(hash, checked((uint)physical.CoarseParameters.N));
        AppendUInt32(hash, checked((uint)physical.FineParameters.M));
        AppendUInt32(hash, checked((uint)physical.FineParameters.N));
        AppendUInt64(hash, physical.MovementTurn);
        AppendUInt64(hash, physical.WorkerRevision);
        AppendUInt64(hash, physical.TotalWorkers);

        AppendUInt32(hash, checked((uint)physical.Allocations.Count));
        foreach (var allocation in physical.Allocations)
        {
            AppendUInt64(hash, allocation.TileId.FineStrategicCellId.Value);
            AppendUInt32(hash, (uint)allocation.Activity);
            AppendUInt64(hash, allocation.Workers);
        }

        AppendUInt32(hash, checked((uint)physical.Stocks.Count));
        foreach (var stock in physical.Stocks)
        {
            AppendUInt64(hash, stock.TileId.FineStrategicCellId.Value);
            AppendUInt32(hash, stock.Commodity.Value);
            AppendUInt64(hash, stock.RawQuantity);
        }

        AppendUInt32(hash, checked((uint)physical.Cycles.Count));
        foreach (var cycle in physical.Cycles)
        {
            AppendUInt64(hash, cycle.MovementTurn);
            AppendUInt64(hash, cycle.ProducedRaw);
            AppendUInt64(hash, cycle.FoodConsumedRaw);
            AppendUInt64(hash, cycle.FoodUnmetRaw);
        }

        if (physical.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty)
        {
            // New ruleset domain separator. Legacy V9 bytes stay identical.
            AppendUInt32(hash, 91U);
            AppendUInt32(hash, (uint)physical.CadenceMode);
            AppendUInt64(hash, PhysicalEconomicCadence.AdaptiveWindowTurns);
        }

        if (physical.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C)
        {
            // Binds the fine-grid biome/elevation producer ruleset to the hash.
            // Legacy V9 and V10 hash bytes and versions are not changed.
            AppendUInt32(hash, 92U);
            AppendUInt32(hash, (uint)physical.GeographyMode);
            AppendUInt32(hash, 1U); // GA-VS12 canonical primitive yield ruleset
            AppendUInt32(hash, 35U); // experimental 01C land percentile
        }

        if (physical.FoodCash is { } market)
        {
            // New version domain: exactly conserved money, exact food, and
            // full cash settlement log. No modification of the V9-V11 bytes.
            AppendUInt32(hash, 93U);
            AppendUInt32(hash, (uint)PhysicalFoodMarketMode.LocalCashSettlement);
            AppendUInt64(hash, market.InitialMoneySupplyMicrocredits);
            AppendUInt32(hash, checked((uint)market.Accounts.Count));
            foreach (var a in market.Accounts)
            {
                AppendUInt64(hash, a.Tile.FineStrategicCellId.Value);
                AppendUInt64(hash, a.HouseholdMicrocredits);
                AppendUInt64(hash, a.ProducerMicrocredits);
            }
            AppendUInt32(hash, checked((uint)market.Receipts.Count));
            foreach (var a in market.Receipts)
            {
                AppendUInt64(hash, a.MovementTurn);
                AppendUInt64(hash, a.Tile.FineStrategicCellId.Value);
                AppendUInt64(hash, a.FoodRaw);
                AppendUInt64(hash, a.PriceMicrocreditsPerUnit);
                AppendUInt64(hash, a.PaidMicrocredits);
            }
        }

        if (physical.MovementMode == PhysicalWorkerMovementMode.TimedAdjacent)
        {
            AppendUInt32(hash, 94U);
            AppendUInt32(hash, (uint)physical.MovementMode);
            AppendUInt32(hash, checked((uint)physical.Transits.Count));
            foreach (var order in physical.Transits)
            {
                AppendUInt64(hash, order.OrderId);
                AppendUInt64(hash, order.Source.FineStrategicCellId.Value);
                AppendUInt64(hash, order.Destination.FineStrategicCellId.Value);
                AppendUInt32(hash, (uint)order.FromJob);
                AppendUInt32(hash, (uint)order.ToJob);
                AppendUInt64(hash, order.Workers);
                AppendUInt64(hash, order.DepartureTurn);
                AppendUInt64(hash, order.ArrivalTurn);
            }
        }
        return new WorldStateHash(
            format,
            Convert.ToHexString(hash.GetHashAndReset()));
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