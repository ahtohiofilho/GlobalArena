using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Numerics;
using System.Text;
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
    // Monetary capital quotes only: physical workers and materials are
    // not construction inputs. All v8-v12 bytes are unchanged.
    public const uint MonetaryConstructionFormatVersion = 13U;
    // First ACTUAL treasury debit activates canonical v14 edge progress.
    public const uint MonetaryInvestmentFormatVersion = 14U;
    // First verified sale+consumption settlement and trusted carry-forward
    // congestion activates v15 without modifying historical v8–v14 bytes.
    public const uint ClosedEconomicCycleFormatVersion = 15U;
    // Opt-in stable cities and worker HOME assignments. No old v8-v15
    // hash changes unless the canonical ledger has been deployed.
    public const uint CanonicalCityResidenceFormatVersion = 16U;
    // Only opt-in persisted journeys activate v17. Historical hashes stay stable.
    public const uint PersistedPhysicalTravelFormatVersion = 17U;
    public const uint PersistedConstructionFormatVersion = 18U;
    public const uint ActivatedConstructionFormatVersion = 19U;

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

        var version = worldState.Constructions.IsInitialized
            ? (worldState.Constructions.Projects.Any(p => p.IsActivated)
                ? ActivatedConstructionFormatVersion : PersistedConstructionFormatVersion)
            : worldState.EdgeTravels.IsInitialized
            ? PersistedPhysicalTravelFormatVersion
            : worldState.CityResidences.IsInitialized
            ? CanonicalCityResidenceFormatVersion
            : worldState.Treasury.HasClosedEconomicCycle
            ? ClosedEconomicCycleFormatVersion
            : worldState.Transport.HasCommittedInvestments
                ? MonetaryInvestmentFormatVersion
            : worldState.Transport.HasMonetaryConstructionCosts
                ? MonetaryConstructionFormatVersion
                : worldState.Transport.IsInitialized
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

        if (worldState.Transport.HasMonetaryConstructionCosts)
        {
            AppendUInt32(hash, 41U);
            AppendUInt32(hash, 1U);
            AppendUInt32(hash, checked((uint)worldState.Transport.Edges.Count));
            foreach (var edge in worldState.Transport.Edges)
                AppendUInt64(hash, edge.NaturalConstructionCostMoneyMicro ??
                    throw new InvalidOperationException(
                        "A v13 monetary edge has no construction cost."));

            // Treasury may remain UNINITIALIZED while opening balances
            // are being explicitly deployed; those are not market sales.
            AppendUInt32(hash, 43U);
            AppendUInt32(hash, worldState.Treasury.IsInitialized ? 1U : 0U);
            if (worldState.Treasury.IsInitialized)
            {
                AppendUInt32(hash, checked((uint)worldState.Treasury.Accounts.Count));
                foreach (var account in worldState.Treasury.Accounts)
                {
                    AppendUInt64(hash, account.Civilization.Value);
                    AppendNonnegativeBigInteger(hash, account.CashScaled);
                    AppendNonnegativeBigInteger(hash,
                        account.TotalInfrastructureInvestmentScaled);
                }
            }
        }

        if (worldState.Transport.HasCommittedInvestments)
        {
            AppendUInt32(hash, 42U);
            AppendUInt32(hash, 1U);
            AppendUInt32(hash, checked((uint)worldState.Transport.Edges.Count));
            foreach (var edge in worldState.Transport.Edges)
                AppendUInt64(hash, edge.CommittedInvestmentMoneyMicro ??
                    throw new InvalidOperationException(
                        "A v14 monetary investment ledger is incomplete."));
        }

        if (worldState.Treasury.HasClosedEconomicCycle)
        {
            var settled = worldState.Treasury;
            AppendUInt32(hash, 44U);
            AppendUInt32(hash, 1U);
            AppendUInt64(hash, settled.LastSettledEconomicCycleOrdinal);
            byte[] cargoCode = Encoding.UTF8.GetBytes(
                settled.LastSettledCargoUnitCode ??
                throw new InvalidOperationException(
                    "Closed market lacks a settled cargo unit."));
            AppendUInt32(hash, checked((uint)cargoCode.Length));
            hash.AppendData(cargoCode);
            AppendUInt32(hash, checked(
                (uint)settled.LastSettledFreightQuotes.Count));
            foreach (var quote in settled.LastSettledFreightQuotes)
            {
                AppendUInt32(hash, checked(
                    (uint)quote.Edge.FineGoldbergParameters.M));
                AppendUInt32(hash, checked(
                    (uint)quote.Edge.FineGoldbergParameters.N));
                AppendUInt64(hash, quote.Edge.FineStrategicEdgeId.Value);
                AppendUInt64(hash,
                    quote.FreePhysicalCostMicroPerWholeCargoUnit);
                AppendUInt64(hash, quote.ReferenceCargoMicro);
                AppendUInt64(hash,
                    quote.QuotedCongestionSurchargeMicroPerWholeCargoUnit);
            }
            AppendUInt32(hash, checked((uint)settled.Accounts.Count));
            foreach (var account in settled.Accounts)
            {
                AppendUInt64(hash, account.Civilization.Value);
                AppendNonnegativeBigInteger(hash,
                    account.TotalSettledEconomicIncomeScaled);
            }
        }

        if (worldState.CityResidences.IsInitialized)
        {
            var ledger = worldState.CityResidences;
            if (!ledger.IsInitialized)
                throw new InvalidOperationException(
                    "v16 must contain an authoritative residence ledger.");
            AppendUInt32(hash, 45U);
            AppendUInt32(hash, 1U);
            AppendUInt32(hash, checked((uint)ledger.Cities.Count));
            foreach (var city in ledger.Cities)
            {
                AppendUInt64(hash, city.Id.Value);
                AppendUInt64(hash, city.Owner.Value);
                AppendUInt32(hash, checked((uint)city.Tile.FineGoldbergParameters.M));
                AppendUInt32(hash, checked((uint)city.Tile.FineGoldbergParameters.N));
                AppendUInt64(hash, city.Tile.FineStrategicCellId.Value);
            }
            AppendUInt32(hash, checked((uint)ledger.Residences.Count));
            foreach (var entry in ledger.Residences)
            {
                AppendUInt64(hash, entry.Worker.Value);
                AppendUInt64(hash, entry.HomeCity.Value);
            }
        }

        if (worldState.EdgeTravels.IsInitialized)
        {
            AppendUInt32(hash, 46U);
            AppendUInt32(hash, 1U);
            AppendUInt32(hash, checked((uint)worldState.EdgeTravels.Pending.Count));
            foreach (var journey in worldState.EdgeTravels.Pending)
            {
                AppendUInt64(hash, journey.Worker.Value);
                AppendUInt64(hash, journey.Owner.Value);
                AppendUInt32(hash, checked((uint)journey.Source.FineGoldbergParameters.M));
                AppendUInt32(hash, checked((uint)journey.Source.FineGoldbergParameters.N));
                AppendUInt64(hash, journey.Source.FineStrategicCellId.Value);
                AppendUInt64(hash, journey.Destination.FineStrategicCellId.Value);
                AppendUInt64(hash, journey.Edge.FineStrategicEdgeId.Value);
                AppendUInt64(hash, journey.RemainingMicro);
                AppendUInt64(hash, journey.OriginalDurationMicro);
            }
        }

        if (worldState.Constructions.IsInitialized)
        {
            AppendUInt32(hash, 47U);
            AppendUInt32(hash, 1U);
            AppendUInt32(hash, checked((uint)worldState.Constructions.Projects.Count));
            foreach (var project in worldState.Constructions.Projects)
            {
                AppendUInt64(hash, project.Id);
                AppendUInt32(hash, checked((uint)project.Edge.FineGoldbergParameters.M));
                AppendUInt32(hash, checked((uint)project.Edge.FineGoldbergParameters.N));
                AppendUInt64(hash, project.Edge.FineStrategicEdgeId.Value);
                AppendUInt64(hash, project.Investor.Value);
                AppendUInt64(hash, project.FundedMoneyMicro);
                AppendUInt64(hash, project.RequiredWorkMicro);
                AppendUInt64(hash, project.CompletedWorkMicro);
                if (version >= ActivatedConstructionFormatVersion)
                    AppendUInt32(hash, project.IsActivated ? 1U : 0U);
            }
        }

        var digest =
            hash.GetHashAndReset();

        return new WorldStateHash(
            version,
            Convert.ToHexString(
                digest));
    }

    /// <summary>
    /// Canonical exact-money encoding: unsigned, big-endian, minimal.
    /// No per-route rounding or arbitrary 64-bit cash ceiling.
    /// </summary>
    private static void AppendNonnegativeBigInteger(
        IncrementalHash hash, BigInteger value)
    {
        if (value.Sign < 0)
            throw new InvalidOperationException(
                "Negative treasury money cannot be hashed.");
        byte[] bytes = value.IsZero ? Array.Empty<byte>() :
            value.ToByteArray(isUnsigned: true, isBigEndian: true);
        AppendUInt32(hash, checked((uint)bytes.Length));
        hash.AppendData(bytes);
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