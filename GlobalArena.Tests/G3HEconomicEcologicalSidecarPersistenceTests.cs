using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-H: deterministic sidecar bytes and replay, tied to an independently
/// verified WorldState. A sidecar is NOT a standalone world save or proof
/// that an untrusted user was authorized to generate a checkpoint.
/// </summary>
public sealed class G3HEconomicEcologicalSidecarPersistenceTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(2, 0)));

    private static WorldState World(WorldGenerationResult generated)
    {
        var fields = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            fields.WoodyVegetation.GetRawValue(id) > 0L &&
            fields.ExposedStone.GetRawValue(id) > 0L);
        return WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(A, Owner, cell, 4UL, new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Forestry, 4UL)
                    })
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(A, 4UL)
                }));
    }

    private static EconomicNaturalCapitalPolicy Policy() =>
        new(100UL * U, 100UL * U, 0L, 0L, 0L);

    private static EconomicEcologicalCheckpoint Initial(
        WorldGenerationResult generated, WorldState world) =>
        new(generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, Policy()),
            EconomicMaterialReserveBook.Empty, Policy(), Mapping);

    private static EconomicDemandProfile[] Demand() =>
        new[] { new EconomicDemandProfile(A, Food, 500_000L) };

    private static EconomicMaterialOriginHoldOrder[] Holds() =>
        new[]
        {
            new EconomicMaterialOriginHoldOrder(
                A, Wood, EconomicActivityKind.Forestry, U / 100UL)
        };

    private static EconomicEcologicalCheckpoint Next(
        WorldGenerationResult generated, EconomicEcologicalCheckpoint previous,
        ulong turn, bool hold = true) =>
        new EconomicEcologicalTurnCommitResolver().Propose(
            generated, previous, previous.Sha256Hex,
            turn, Demand(), hold ? Holds() :
                Array.Empty<EconomicMaterialOriginHoldOrder>()).ProposedNext;

    private static void Rechecksum(byte[] bytes)
    {
        var checksum = SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32));
        checksum.CopyTo(bytes, bytes.Length - 32);
    }

    [Fact]
    public void InitialAndAdvancedSidecarsRoundTripExactly()
    {
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        var afterOneTurn = Next(generated, initial, 1UL);

        foreach (var snapshot in new[] { initial, afterOneTurn })
        {
            var bytes = EconomicEcologicalSidecarCodec.Encode(snapshot);
            var rehydrated = EconomicEcologicalSidecarCodec.Decode(
                generated, world, bytes);

            Assert.Equal(snapshot.Sha256Hex, rehydrated.Sha256Hex);
            Assert.Equal(snapshot.WorldHash, rehydrated.WorldHash);
            Assert.Equal(snapshot.NaturalCapital.LastAppliedTurn,
                rehydrated.NaturalCapital.LastAppliedTurn);
            Assert.Equal(snapshot.MaterialReserves.Balances.Select(x => x.RawQuantity),
                rehydrated.MaterialReserves.Balances.Select(x => x.RawQuantity));
            Assert.Equal(bytes, EconomicEcologicalSidecarCodec.Encode(rehydrated));
        }

        Assert.Equal(U / 100UL, afterOneTurn.MaterialReserves.RawAvailable(A, Wood));
        Assert.Equal(1UL, afterOneTurn.NaturalCapital.LastAppliedTurn);
        Assert.Equal(1U, EconomicEcologicalSidecarCodec.FormatVersion);
        Assert.Equal(8U, initial.WorldHash.FormatVersion);
    }

    [Fact]
    public void DifferentWorldOrChangedWorldStateCannotLoadSidecar()
    {
        var generated = Generate();
        var world = World(generated);
        var bytes = EconomicEcologicalSidecarCodec.Encode(Initial(generated, world));

        Assert.Throws<InvalidOperationException>(() =>
            EconomicEcologicalSidecarCodec.Decode(
                Generate(1UL), world, bytes));
        Assert.Throws<InvalidOperationException>(() =>
            EconomicEcologicalSidecarCodec.Decode(
                generated, world.AdvanceRevision(), bytes));
    }

    [Fact]
    public void DamagedChecksumAndTruncatedBytesAreRejected()
    {
        var generated = Generate();
        var world = World(generated);
        var bytes = EconomicEcologicalSidecarCodec.Encode(Initial(generated, world));
        var damaged = (byte[])bytes.Clone();
        damaged[damaged.Length / 2] ^= 0x01;

        Assert.Throws<InvalidDataException>(() =>
            EconomicEcologicalSidecarCodec.Decode(generated, world, damaged));
        Assert.Throws<InvalidDataException>(() =>
            EconomicEcologicalSidecarCodec.Decode(
                generated, world, bytes[..^1]));
        Assert.Throws<InvalidDataException>(() =>
            EconomicEcologicalSidecarCodec.Decode(
                generated, world, Array.Empty<byte>()));
    }

    [Fact]
    public void UnsupportedFormatAndDifferentWorldHashFormatFailClosed()
    {
        var generated = Generate();
        var world = World(generated);
        var baseline = EconomicEcologicalSidecarCodec.Encode(Initial(generated, world));

        var unknownVersion = (byte[])baseline.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(
            unknownVersion.AsSpan(8, 4), 2U);
        Rechecksum(unknownVersion);
        Assert.Throws<NotSupportedException>(() =>
            EconomicEcologicalSidecarCodec.Decode(generated, world, unknownVersion));

        var differentWorldFormat = (byte[])baseline.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(
            differentWorldFormat.AsSpan(12, 4), 9U);
        Rechecksum(differentWorldFormat);
        Assert.Throws<InvalidOperationException>(() =>
            EconomicEcologicalSidecarCodec.Decode(
                generated, world, differentWorldFormat));
    }

    [Fact]
    public void ImpossibleCellCountAndTrailingBytesFailEvenWithValidChecksum()
    {
        var generated = Generate();
        var world = World(generated);
        var source = EconomicEcologicalSidecarCodec.Encode(Initial(generated, world));

        // Header 8 + format 4 + world format 4 + two hashes 64 +
        // derivation 4 + turn 8 + policy 40 + commodity mapping 12.
        const int naturalCellCountOffset = 144;
        var impossibleCount = (byte[])source.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(
            impossibleCount.AsSpan(naturalCellCountOffset, 4), uint.MaxValue);
        Rechecksum(impossibleCount);
        Assert.Throws<InvalidDataException>(() =>
            EconomicEcologicalSidecarCodec.Decode(
                generated, world, impossibleCount));

        var trailing = new byte[source.Length + 1];
        Array.Copy(source, 0, trailing, 0, source.Length - 32);
        trailing[source.Length - 32] = 0x99;
        Rechecksum(trailing);
        Assert.Throws<InvalidDataException>(() =>
            EconomicEcologicalSidecarCodec.Decode(generated, world, trailing));
    }

    [Fact]
    public void ReplayAfterSidecarLoadMatchesThreeSequentialCommitments()
    {
        var generated = Generate();
        var world = World(generated);
        var first = Initial(generated, world);
        var second = Next(generated, first, 1UL);
        var third = Next(generated, second, 2UL, hold: false);
        var fourth = Next(generated, third, 3UL, hold: false);

        var steps = new[]
        {
            new EconomicEcologicalReplayStep(1UL, second.Sha256Hex, Demand(), Holds()),
            new EconomicEcologicalReplayStep(2UL, third.Sha256Hex, Demand(),
                Array.Empty<EconomicMaterialOriginHoldOrder>()),
            new EconomicEcologicalReplayStep(3UL, fourth.Sha256Hex, Demand(),
                Array.Empty<EconomicMaterialOriginHoldOrder>())
        };

        var replayed = new EconomicEcologicalSidecarReplayVerifier().Replay(
            generated, world,
            EconomicEcologicalSidecarCodec.Encode(first), steps);
        Assert.Equal(fourth.Sha256Hex, replayed.Sha256Hex);
        Assert.Equal(3UL, replayed.NaturalCapital.LastAppliedTurn);
        Assert.Equal(fourth.MaterialReserves.RawAvailable(A, Wood),
            replayed.MaterialReserves.RawAvailable(A, Wood));

        // The same replay can start from a saved intermediate checkpoint.
        var resumed = new EconomicEcologicalSidecarReplayVerifier().Replay(
            generated, world,
            EconomicEcologicalSidecarCodec.Encode(second), steps.Skip(1));
        Assert.Equal(fourth.Sha256Hex, resumed.Sha256Hex);
    }

    [Fact]
    public void ReplayRejectsDivergentExpectedDigestAndRepeatedTurns()
    {
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        var second = Next(generated, initial, 1UL);
        var sidecar = EconomicEcologicalSidecarCodec.Encode(initial);

        var wrong = new[]
        {
            new EconomicEcologicalReplayStep(
                1UL, initial.Sha256Hex, Demand(), Holds())
        };
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicEcologicalSidecarReplayVerifier().Replay(
                generated, world, sidecar, wrong));

        var repeated = new[]
        {
            new EconomicEcologicalReplayStep(
                1UL, second.Sha256Hex, Demand(), Holds()),
            new EconomicEcologicalReplayStep(
                1UL, second.Sha256Hex, Demand(), Holds())
        };
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicEcologicalSidecarReplayVerifier().Replay(
                generated, world, sidecar, repeated));
    }
}
