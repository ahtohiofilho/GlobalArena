using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS120: actual facility-assisted production channel from REAL staffed
/// workers, without an unauthorized yield multiplier or duplicated output.
/// </summary>
public sealed class GaVs120FacilityAssistedWorkTests
{
    private static CanonicalPhysicalTacticalSession Prepared(
        out int frameOrdinal,
        PhysicalEconomicCadenceMode mode = PhysicalEconomicCadenceMode.LegacyFixedSix)
    {
        var game = CanonicalPhysicalTacticalSession.Start(cadenceMode: mode);
        for (var i = 0; i < 6; i++)
            game.AdvanceMovementTurn();
        var frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame, 1UL, 1UL);
        frameOrdinal = frame.ActionOrdinal;
        game.StartPrimitiveFacility(game.InitialTile, frameOrdinal,
            EconomicActivityKind.Forestry, 1UL);
        return game;
    }

    private static void CompleteCycle(CanonicalPhysicalTacticalSession game)
    {
        var previousCycles = game.State.Cycles.Count;
        var safety = 0;
        while (game.State.Cycles.Count == previousCycles)
        {
            Assert.True(safety++ < 35);
            game.AdvanceMovementTurn();
        }
    }

    private static CanonicalPhysicalSaveEnvelope Envelope(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)
        ?? throw new InvalidOperationException("Missing canonical envelope.");

    private static CanonicalPhysicalSavePayload Payload(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(json).PayloadJson)
        ?? throw new InvalidOperationException("Missing canonical payload.");

    private static string Tamper(
        string save, Func<CanonicalPhysicalSavePayload,
            CanonicalPhysicalSavePayload> edit, int? envelopeVersion = null)
    {
        var envelope = Envelope(save);
        var raw = JsonSerializer.Serialize(edit(Payload(save)));
        return JsonSerializer.Serialize(envelope with
        {
            Version = envelopeVersion ?? envelope.Version,
            PayloadJson = raw,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
        });
    }

    [Fact]
    public void FacilityNeedsToBeCompleteBeforeWorkersCanOperateIt()
    {
        var game = Prepared(out var ordinal);
        var save = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.StaffPrimitiveFacility(game.InitialTile, ordinal,
                EconomicActivityKind.Forestry, 1UL));
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Equal(CanonicalWorldStateHasher.PrimitiveFacilityHashFormatVersion,
            game.CurrentHash.FormatVersion);
        CompleteCycle(game);
        Assert.True(game.PrimitiveFacilities[0].IsOperational);
        Assert.Equal(0UL, game.PrimitiveFacilities[0].OperatingWorkers);
    }

    [Fact]
    public void ActiveStaffingIsCanonicallyHashedAndDoesNotMintPeopleOrMaterials()
    {
        var game = Prepared(out var ordinal);
        CompleteCycle(game);
        var initial = game.State;
        var oldHash = game.CurrentHash;
        var saved = game.ExportSaveJson();
        var site = game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);

        Assert.True(site.IsOperational);
        Assert.True(site.HasAssistedWorkOperators);
        Assert.Equal(1UL, site.OperatingWorkers);
        Assert.Equal(initial.Allocations, game.State.Allocations);
        Assert.Equal(initial.Stocks, game.State.Stocks);
        Assert.Equal(initial.TotalWorkers, game.State.TotalWorkers);
        Assert.Equal(initial.Cycles, game.State.Cycles);
        Assert.Equal(initial.WorkerRevision, game.State.WorkerRevision);
        Assert.NotEqual(oldHash, game.CurrentHash);
        Assert.Equal(CanonicalWorldStateHasher.FacilityAssistedWorkHashFormatVersion,
            game.CurrentHash.FormatVersion);
        Assert.Equal(29, Envelope(game.ExportSaveJson()).Version);
        Assert.Single(Payload(game.ExportSaveJson()).PrimitiveFacilities!);
        Assert.Equal(1UL,
            Payload(game.ExportSaveJson()).PrimitiveFacilities![0].OperatingWorkers);
        Assert.Equal("staff_primitive_facility", game.Events[^1].Kind);
        Assert.Equal(ordinal, game.Events[^1].FacilityAssemblyOrdinal);
        Assert.Equal(1UL, game.Events[^1].Workers);
        Assert.False(site.AuthorizesProductivityBonus);
        Assert.False(site.CanAuthorizeRealPurchasingPower);
        Assert.Equal(27, Envelope(saved).Version);
    }

    [Fact]
    public void FacilityThroughputIsAnExactSubsetOfAlreadyProducedPhysicalGoods()
    {
        var staffed = Prepared(out var ordinal);
        var control = Prepared(out _);
        CompleteCycle(staffed);
        CompleteCycle(control);
        var rate = staffed.PrimitiveYieldAt(
            staffed.InitialTile, EconomicActivityKind.Forestry);
        Assert.True(rate > 0L);
        staffed.StaffPrimitiveFacility(staffed.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);

        CompleteCycle(staffed);
        CompleteCycle(control);

        Assert.Equal(control.State.Cycles[^1], staffed.State.Cycles[^1]);
        Assert.Equal(control.State.Stocks, staffed.State.Stocks);
        Assert.Equal(control.State.TotalWorkers, staffed.State.TotalWorkers);
        var witness = staffed.CaptureLatestFacilityAssistedWorkWitness();
        witness.VerifyIndependent();
        Assert.True(witness.IsActuallyExecutedOutput);
        Assert.False(witness.CreatesAdditionalProduction);
        Assert.False(witness.AuthorizesFacilityYieldMultiplier);
        Assert.False(witness.CanAuthorizeRealPurchasingPower);
        var row = Assert.Single(witness.Assisted);
        Assert.Equal(ordinal, row.FacilityAssemblyOrdinal);
        Assert.Equal(staffed.InitialTile.FineStrategicCellId.Value, row.FineTile);
        Assert.Equal(EconomicActivityKind.Forestry, row.Activity);
        Assert.Equal(3U, row.Commodity);
        Assert.Equal(1UL, row.OperatingWorkers);
        Assert.Equal((ulong)rate, row.AssistedProducedRaw);
        Assert.Equal((ulong)rate, witness.TotalAssistedProducedRaw);

        var real = staffed.CaptureLatestExecutedPhysicalCycleProvenance();
        var allForestry = Assert.Single(real.Production.Where(x =>
            x.SourceFineTile == row.FineTile &&
            x.Activity == EconomicActivityKind.Forestry));
        Assert.True(row.AssistedProducedRaw <= allForestry.ProducedRaw);
        Assert.Equal(allForestry.ProducedRaw,
            (allForestry.ProducedRaw - row.AssistedProducedRaw)
            + row.AssistedProducedRaw);
        Assert.Equal(real.ActuallyProducedRaw, staffed.State.Cycles[^1].ProducedRaw);
    }

    [Fact]
    public void ExistingOperatingWorkersCannotMoveOrReassignUntilUnstaffed()
    {
        var game = Prepared(out var ordinal);
        CompleteCycle(game);
        game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);
        var tile = game.InitialTile;
        var allForest = game.State.Allocations.Single(x =>
            x.TileId == tile && x.Activity == EconomicActivityKind.Forestry).Workers;
        var before = game.ExportSaveJson();
        Assert.ThrowsAny<Exception>(() =>
            game.ReassignWorkers(tile, EconomicActivityKind.Forestry,
                EconomicActivityKind.Agriculture, allForest));
        Assert.ThrowsAny<Exception>(() =>
            game.MoveWorkers(tile, game.AdjacentWorkable(tile)[0],
                EconomicActivityKind.Forestry,
                EconomicActivityKind.Agriculture, allForest));
        Assert.Equal(before, game.ExportSaveJson());

        game.StaffPrimitiveFacility(tile, ordinal,
            EconomicActivityKind.Forestry, 0UL);
        Assert.Equal(0UL, game.PrimitiveFacilities[0].OperatingWorkers);
        Assert.Equal(CanonicalWorldStateHasher.PrimitiveFacilityHashFormatVersion,
            game.CurrentHash.FormatVersion);
        game.ReassignWorkers(tile, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, allForest);
        Assert.True(game.PrimitiveFacilities[0].IsOperational);
        Assert.Equal(0UL, game.PrimitiveFacilities[0].OperatingWorkers);
    }

    [Fact]
    public void CapacityAndIdentityAreEnforcedWithoutPartialChanges()
    {
        var game = Prepared(out var ordinal);
        CompleteCycle(game);
        var save = game.ExportSaveJson();
        Assert.ThrowsAny<Exception>(() =>
            game.StaffPrimitiveFacility(game.InitialTile, ordinal,
                EconomicActivityKind.Forestry, 2UL));
        Assert.ThrowsAny<Exception>(() =>
            game.StaffPrimitiveFacility(game.InitialTile, ordinal+10,
                EconomicActivityKind.Forestry, 1UL));
        Assert.ThrowsAny<Exception>(() =>
            game.StaffPrimitiveFacility(game.InitialTile, ordinal,
                EconomicActivityKind.Mining, 1UL));
        Assert.ThrowsAny<Exception>(() =>
            game.StaffPrimitiveFacility(game.InitialTile, ordinal,
                EconomicActivityKind.TradeLogistics, 1UL));
        Assert.ThrowsAny<Exception>(() =>
            game.StaffPrimitiveFacility(game.AdjacentWorkable(game.InitialTile)[0],
                ordinal, EconomicActivityKind.Forestry, 1UL));
        Assert.Equal(save, game.ExportSaveJson());

        game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);
        var after = game.ExportSaveJson();
        Assert.ThrowsAny<Exception>(() =>
            game.StaffPrimitiveFacility(game.InitialTile, ordinal,
                EconomicActivityKind.Forestry, 1UL));
        Assert.Equal(after, game.ExportSaveJson());
    }

    [Fact]
    public void Save29ReplaysPhysicalOperatorAssignmentsAndAssistedOutput()
    {
        var game = Prepared(out var ordinal);
        CompleteCycle(game);
        game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);
        var start = game.ExportSaveJson();
        Assert.Equal(29, Envelope(start).Version);
        Assert.Equal(start,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(start).ExportSaveJson());

        CompleteCycle(game);
        var save = game.ExportSaveJson();
        var expected = game.CaptureLatestFacilityAssistedWorkWitness();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var received = replay.CaptureLatestFacilityAssistedWorkWitness();
        received.VerifyIndependent();
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(game.Events, replay.Events);
        Assert.Equal(game.State.PrimitiveFacilities.ToArray(),
            replay.State.PrimitiveFacilities.ToArray());
        Assert.Equal(expected.Assisted.ToArray(), received.Assisted.ToArray());
        Assert.Equal(expected.TotalAssistedProducedRaw,
            received.TotalAssistedProducedRaw);
        Assert.Equal(CanonicalWorldStateHasher.FacilityAssistedWorkHashFormatVersion,
            replay.CurrentHash.FormatVersion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Save30TreasuryBeforeOrAfterStaffingReplaysExactly(bool before)
    {
        var game = Prepared(out var ordinal);
        if (before)
            game.EnableDormantTreasuryReservation();
        CompleteCycle(game);
        game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);
        CompleteCycle(game);
        if (!before)
            game.EnableDormantTreasuryReservation();

        var save = game.ExportSaveJson();
        Assert.Equal(30, Envelope(save).Version);
        Assert.NotNull(Payload(save).TreasuryReservation);
        Assert.Equal(1UL,
            Assert.Single(Payload(save).PrimitiveFacilities!).OperatingWorkers);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(game.Events, restored.Events);
        Assert.Equal(game.State.PrimitiveFacilities.ToArray(),
            restored.State.PrimitiveFacilities.ToArray());
        // The source-hash of the current work evidence can become stale when
        // treasury is activated after the closing tick, but replay remains exact.
    }

    [Fact]
    public void Save29RemainsEvenAfterOperatorsHaveBeenReleased()
    {
        var game = Prepared(out var ordinal);
        CompleteCycle(game);
        game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);
        CompleteCycle(game);
        game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 0UL);
        Assert.Equal(CanonicalWorldStateHasher.PrimitiveFacilityHashFormatVersion,
            game.CurrentHash.FormatVersion);
        var save = game.ExportSaveJson();
        Assert.Equal(29, Envelope(save).Version);
        Assert.Equal(0UL,
            Assert.Single(Payload(save).PrimitiveFacilities!).OperatingWorkers);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.Events, replay.Events);
    }

    [Fact]
    public void LegacyFacility27AndTreasury28RetainExactBytesWithoutStaffing()
    {
        var game = Prepared(out _);
        CompleteCycle(game);
        var v27 = game.ExportSaveJson();
        Assert.Equal(27, Envelope(v27).Version);
        Assert.DoesNotContain("OperatingWorkers", Envelope(v27).PayloadJson);
        Assert.Equal(v27,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(v27).ExportSaveJson());
        game.EnableDormantTreasuryReservation();
        var v28 = game.ExportSaveJson();
        Assert.Equal(28, Envelope(v28).Version);
        Assert.DoesNotContain("OperatingWorkers", Envelope(v28).PayloadJson);
        Assert.Equal(v28,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(v28).ExportSaveJson());
    }

    [Fact]
    public void InvalidOperatorsCannotBeSmuggledThroughRehashedSave()
    {
        var game = Prepared(out var ordinal);
        CompleteCycle(game);
        game.StaffPrimitiveFacility(game.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);
        CompleteCycle(game);
        var save = game.ExportSaveJson();

        var overCapacity = Tamper(save, payload =>
        {
            var facilities = payload.PrimitiveFacilities!.ToArray();
            facilities[0] = facilities[0] with { OperatingWorkers = 99UL };
            return payload with { PrimitiveFacilities = facilities };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(overCapacity));

        var fakeStaffing = Tamper(save, payload =>
        {
            var actions = payload.Actions.ToArray();
            var idx = Array.FindIndex(actions,
                x => x.Kind == "staff_primitive_facility");
            actions[idx] = actions[idx] with { Workers = 99UL };
            return payload with { Actions = actions };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(fakeStaffing));

        var unauthorizedV27 = Tamper(save, p => p, 27);
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(unauthorizedV27));
    }

    [Fact]
    public void AdaptiveCadenceOperatesWithoutAddingAnExtraProductionTurn()
    {
        var operated = Prepared(out var ordinal,
            PhysicalEconomicCadenceMode.AdaptiveThirty);
        var control = Prepared(out _,
            PhysicalEconomicCadenceMode.AdaptiveThirty);
        CompleteCycle(operated);
        CompleteCycle(control);
        operated.StaffPrimitiveFacility(operated.InitialTile, ordinal,
            EconomicActivityKind.Forestry, 1UL);
        CompleteCycle(operated);
        CompleteCycle(control);
        Assert.Equal(control.State.Cycles[^1], operated.State.Cycles[^1]);
        Assert.Equal(control.State.Stocks, operated.State.Stocks);
        Assert.Single(operated.CaptureLatestFacilityAssistedWorkWitness().Assisted);
    }
}
