using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS119: an opt-in physical work shelter, authenticated by WorldState
/// hash v27 and paid for with existing wood/stone frame and real labor-time.
/// </summary>
public sealed class GaVs119PhysicalPrimitiveFacilityTests
{
    private static CanonicalPhysicalTacticalSession Prepared(
        out CanonicalExecutedPrimitiveAssembly frame,
        PhysicalEconomicCadenceMode mode = PhysicalEconomicCadenceMode.LegacyFixedSix)
    {
        var game = CanonicalPhysicalTacticalSession.Start(cadenceMode: mode);
        for (var i = 0; i < 6; i++)
            game.AdvanceMovementTurn();
        Assert.True(game.StockAt(game.InitialTile, new CommodityId(3U)) >= 1UL);
        Assert.True(game.StockAt(game.InitialTile, new CommodityId(2U)) >= 1UL);
        frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,
            1UL, 1UL);
        return game;
    }

    private static CanonicalPhysicalSaveEnvelope Envelope(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(json)
        ?? throw new InvalidOperationException("No save envelope.");

    private static CanonicalPhysicalSavePayload Payload(string json) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(json).PayloadJson)
        ?? throw new InvalidOperationException("No save payload.");

    private static string Rewrite(string save,
        Func<CanonicalPhysicalSavePayload, CanonicalPhysicalSavePayload> edit,
        int? version = null)
    {
        var envelope = Envelope(save);
        var raw = JsonSerializer.Serialize(edit(Payload(save)));
        return JsonSerializer.Serialize(envelope with
        {
            Version = version ?? envelope.Version,
            PayloadJson = raw,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
        });
    }

    private static void AdvanceSix(CanonicalPhysicalTacticalSession game)
    {
        for (var i = 0; i < 5; i++)
            Assert.Null(game.AdvanceMovementTurn());
        Assert.NotNull(game.AdvanceMovementTurn());
    }

    [Fact]
    public void StartingPhysicalFacilityChangesCanonicalHashButNotWorkforceOrMaterials()
    {
        var game = Prepared(out var frame);
        var beforeState = game.State;
        var beforeHash = game.CurrentHash;
        var beforeSave = game.ExportSaveJson();

        var site = game.StartPrimitiveFacility(game.InitialTile,
            frame.ActionOrdinal, EconomicActivityKind.Forestry, 1UL);

        Assert.Equal(frame.ActionOrdinal, site.AssemblyActionOrdinal);
        Assert.Equal(PhysicalPrimitiveFacilityKind.WorkShelter, site.Kind);
        Assert.False(site.IsOperational);
        Assert.Equal(1UL, site.ReservedWorkers);
        Assert.Equal(beforeState.MovementTurn, site.StartedMovementTurn);
        Assert.Equal(0UL, site.CompletedMovementTurn);
        Assert.False(site.AuthorizesProductivityBonus);
        Assert.False(site.AuthorizesNewTradeableGood);
        Assert.False(site.CanAuthorizeRealPurchasingPower);
        Assert.Equal(beforeState.Stocks, game.State.Stocks);
        Assert.Equal(beforeState.Allocations, game.State.Allocations);
        Assert.Equal(beforeState.TotalWorkers, game.State.TotalWorkers);
        Assert.Equal(beforeState.Cycles, game.State.Cycles);
        Assert.Equal(beforeState.WorkerRevision, game.State.WorkerRevision);
        Assert.NotEqual(beforeHash, game.CurrentHash);
        Assert.Equal(CanonicalWorldStateHasher.PrimitiveFacilityHashFormatVersion,
            game.CurrentHash.FormatVersion);
        Assert.Single(game.PrimitiveFacilities);
        Assert.Equal(1UL, game.ConstructionWorkersAt(
            game.InitialTile, EconomicActivityKind.Forestry));
        Assert.Equal(PhysicalPrimitiveFacilityKind.WorkShelter,
            Assert.Single(game.Game.Snapshot.PhysicalEconomy!.PrimitiveFacilities).Kind);
        Assert.DoesNotContain("PrimitiveFacilities", Envelope(beforeSave).PayloadJson);
        Assert.Equal("start_primitive_facility", game.Events[^1].Kind);
        Assert.Equal(frame.ActionOrdinal, game.Events[^1].FacilityAssemblyOrdinal);
        Assert.Equal((byte)EconomicActivityKind.Forestry,
            game.Events[^1].FacilityActivity);
    }

    [Fact]
    public void ConstructionCrewDoesNotProduceNormalGoodsDuringItsEconomicCycle()
    {
        var active = Prepared(out var activeFrame);
        var control = Prepared(out _);
        Assert.Equal(control.CurrentHash, active.CurrentHash);
        var rate = active.PrimitiveYieldAt(
            active.InitialTile, EconomicActivityKind.Forestry);
        Assert.True(rate > 0L);
        var originalWorkers = active.State.TotalWorkers;
        active.StartPrimitiveFacility(active.InitialTile,
            activeFrame.ActionOrdinal, EconomicActivityKind.Forestry, 1UL);
        for (var i = 0; i < 6; i++)
        {
            var controlCycle = control.AdvanceMovementTurn();
            var activeCycle = active.AdvanceMovementTurn();
            Assert.Equal(controlCycle is null, activeCycle is null);
            if (i < 5)
                Assert.False(active.PrimitiveFacilities[0].IsOperational);
        }

        Assert.True(active.PrimitiveFacilities[0].IsOperational);
        Assert.Equal(12UL, active.PrimitiveFacilities[0].CompletedMovementTurn);
        Assert.Equal(0UL, active.ConstructionWorkersAt(
            active.InitialTile, EconomicActivityKind.Forestry));
        Assert.Equal(originalWorkers, active.State.TotalWorkers);
        Assert.Equal((ulong)rate,
            control.State.Cycles[^1].ProducedRaw - active.State.Cycles[^1].ProducedRaw);
        Assert.Equal((ulong)rate,
            control.StockAt(control.InitialTile, new CommodityId(3U)) -
            active.StockAt(active.InitialTile, new CommodityId(3U)));

        // Labor returns to normal activity after this real settlement;
        // the built shelter adds no extra yield or worker headcount.
        AdvanceSix(control);
        AdvanceSix(active);
        Assert.Equal(control.State.Cycles[^1].ProducedRaw,
            active.State.Cycles[^1].ProducedRaw);
        Assert.Equal(originalWorkers, active.State.TotalWorkers);
        Assert.True(active.PrimitiveFacilities[0].IsOperational);
    }

    [Fact]
    public void ConstructionWorkersKeepFoodDemandAndCannotBeReassignedOrMoved()
    {
        var game = Prepared(out var frame);
        var tile = game.InitialTile;
        var forestWorkers = game.State.Allocations.Single(x =>
            x.TileId == tile && x.Activity == EconomicActivityKind.Forestry).Workers;
        var demand = game.FoodDemandAt(tile);
        game.StartPrimitiveFacility(tile, frame.ActionOrdinal,
            EconomicActivityKind.Forestry, 1UL);
        Assert.Equal(demand, game.FoodDemandAt(tile));
        var original = game.ExportSaveJson();
        Assert.ThrowsAny<Exception>(() =>
            game.ReassignWorkers(tile, EconomicActivityKind.Forestry,
                EconomicActivityKind.Agriculture, forestWorkers));
        var neighbor = game.AdjacentWorkable(tile)[0];
        Assert.ThrowsAny<Exception>(() =>
            game.MoveWorkers(tile, neighbor, EconomicActivityKind.Forestry,
                EconomicActivityKind.Agriculture, forestWorkers));
        Assert.Equal(original, game.ExportSaveJson());

        AdvanceSix(game);
        Assert.True(game.PrimitiveFacilities[0].IsOperational);
        game.ReassignWorkers(tile, EconomicActivityKind.Forestry,
            EconomicActivityKind.Agriculture, forestWorkers);
        Assert.Equal(0UL, game.ConstructionWorkersAt(
            tile, EconomicActivityKind.Forestry));
        Assert.True(game.PrimitiveFacilities[0].IsOperational);
    }

    [Fact]
    public void FacilityStartIsAtomicAndRejectsInvalidOrDuplicateMaterialEvidence()
    {
        var game = Prepared(out var frame);
        var state = game.ExportSaveJson();
        var forestWorkers = game.State.Allocations.Single(x =>
            x.Activity == EconomicActivityKind.Forestry &&
            x.TileId == game.InitialTile).Workers;

        Assert.ThrowsAny<Exception>(() =>
            game.StartPrimitiveFacility(game.InitialTile,
                999, EconomicActivityKind.Forestry, 1UL));
        Assert.ThrowsAny<Exception>(() =>
            game.StartPrimitiveFacility(game.InitialTile,
                frame.ActionOrdinal, EconomicActivityKind.Forestry, 0UL));
        Assert.ThrowsAny<Exception>(() =>
            game.StartPrimitiveFacility(game.InitialTile,
                frame.ActionOrdinal, EconomicActivityKind.Forestry,
                forestWorkers + 1UL));
        Assert.ThrowsAny<Exception>(() =>
            game.StartPrimitiveFacility(game.InitialTile,
                frame.ActionOrdinal, EconomicActivityKind.TradeLogistics, 1UL));
        Assert.Equal(state, game.ExportSaveJson());

        game.StartPrimitiveFacility(game.InitialTile, frame.ActionOrdinal,
            EconomicActivityKind.Forestry, forestWorkers);
        var after = game.ExportSaveJson();
        Assert.ThrowsAny<Exception>(() =>
            game.StartPrimitiveFacility(game.InitialTile, frame.ActionOrdinal,
                EconomicActivityKind.Mining, 1UL));
        Assert.Equal(after, game.ExportSaveJson());
    }

    [Fact]
    public void OneEconomicCycleNotAnArbitraryMovementTickCompletesConstruction()
    {
        var game = Prepared(out var frame);
        game.StartPrimitiveFacility(game.InitialTile, frame.ActionOrdinal,
            EconomicActivityKind.Mining, 1UL);
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(game.AdvanceMovementTurn());
            Assert.False(game.PrimitiveFacilities[0].IsOperational);
            Assert.Equal(1UL, game.ConstructionWorkersAt(
                game.InitialTile, EconomicActivityKind.Mining));
        }
        Assert.NotNull(game.AdvanceMovementTurn());
        Assert.True(game.PrimitiveFacilities[0].IsOperational);
        Assert.Equal(12UL, game.PrimitiveFacilities[0].CompletedMovementTurn);
        Assert.Equal(0UL, game.ConstructionWorkersAt(
            game.InitialTile, EconomicActivityKind.Mining));
        Assert.Single(game.State.PrimitiveFacilities);
    }

    [Fact]
    public void AdaptiveEarlyGameFinishesAtNextActualEconomicClosing()
    {
        var game = Prepared(out var frame,
            PhysicalEconomicCadenceMode.AdaptiveThirty);
        var startedAt = game.State.MovementTurn;
        game.StartPrimitiveFacility(game.InitialTile, frame.ActionOrdinal,
            EconomicActivityKind.Forestry, 1UL);
        Assert.False(game.PrimitiveFacilities[0].IsOperational);
        Assert.NotNull(game.AdvanceMovementTurn());
        Assert.Equal(startedAt + 1UL,
            game.PrimitiveFacilities[0].CompletedMovementTurn);
        Assert.True(game.PrimitiveFacilities[0].IsOperational);
        Assert.Equal(CanonicalWorldStateHasher.PrimitiveFacilityHashFormatVersion,
            game.CurrentHash.FormatVersion);
    }

    [Fact]
    public void Save27ReplaysIdenticalInProgressAndCompletedCanonicalInstallations()
    {
        var game = Prepared(out var frame);
        var oldSave = game.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.MaterialAssemblyEnvelopeFormat,
            Envelope(oldSave).Version);
        Assert.Equal(oldSave,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(oldSave).ExportSaveJson());

        game.StartPrimitiveFacility(game.InitialTile, frame.ActionOrdinal,
            EconomicActivityKind.Forestry, 1UL);
        var savePending = game.ExportSaveJson();
        Assert.Equal(CanonicalPhysicalTacticalSession.PrimitiveFacilityEnvelopeFormat,
            Envelope(savePending).Version);
        Assert.Single(Payload(savePending).PrimitiveFacilities!);
        Assert.Equal(0UL,
            Payload(savePending).PrimitiveFacilities![0].CompletedTurn);
        var replayPending = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            savePending);
        Assert.Equal(savePending, replayPending.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replayPending.CurrentHash);
        Assert.Equal(game.State.PrimitiveFacilities.ToArray(),
            replayPending.State.PrimitiveFacilities.ToArray());

        AdvanceSix(game);
        var saveComplete = game.ExportSaveJson();
        Assert.Equal(12UL, Payload(saveComplete).PrimitiveFacilities![0].CompletedTurn);
        var replayComplete = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            saveComplete);
        Assert.Equal(saveComplete, replayComplete.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replayComplete.CurrentHash);
        Assert.Equal(game.State.PrimitiveFacilities.ToArray(),
            replayComplete.State.PrimitiveFacilities.ToArray());
        Assert.Equal(game.Events, replayComplete.Events);
        Assert.Equal(CanonicalWorldStateHasher.PrimitiveFacilityHashFormatVersion,
            replayComplete.CurrentHash.FormatVersion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Save28CoexistsWithTreasuryActivatedBeforeOrAfterFacility(
        bool treasuryFirst)
    {
        var game = Prepared(out var frame);
        if (treasuryFirst)
            game.EnableDormantTreasuryReservation();
        game.StartPrimitiveFacility(game.InitialTile, frame.ActionOrdinal,
            EconomicActivityKind.Mining, 1UL);
        AdvanceSix(game);
        if (!treasuryFirst)
            game.EnableDormantTreasuryReservation();

        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.PrimitiveFacilityWithTreasuryEnvelopeFormat,
            Envelope(save).Version);
        Assert.NotNull(Payload(save).TreasuryReservation);
        Assert.Single(Payload(save).PrimitiveFacilities!);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(game.State.PrimitiveFacilities.ToArray(),
            replay.State.PrimitiveFacilities.ToArray());
    }

    [Fact]
    public void PreviousFieldCommissioningSave25IsByteCompatibleWithoutNewFacility()
    {
        var game = Prepared(out var frame);
        AdvanceSix(game);
        game.CommissionPrimitiveWorkSite(game.InitialTile,
            frame.ActionOrdinal, EconomicActivityKind.Forestry);
        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.PrimitiveWorkSiteEnvelopeFormat,
            Envelope(save).Version);
        Assert.DoesNotContain("PrimitiveFacilities",
            Envelope(save).PayloadJson);
        Assert.Equal(save,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(save).ExportSaveJson());
    }

    [Fact]
    public void CannotTamperPhysicalFacilityStateEvenWithRecomputedSaveChecksum()
    {
        var game = Prepared(out var frame);
        game.StartPrimitiveFacility(game.InitialTile, frame.ActionOrdinal,
            EconomicActivityKind.Forestry, 1UL);
        AdvanceSix(game);
        var save = game.ExportSaveJson();
        var changedState = Rewrite(save, p =>
        {
            var sites = p.PrimitiveFacilities!.ToArray();
            sites[0] = sites[0] with { CompletedTurn =
                checked(sites[0].CompletedTurn + 1UL) };
            return p with { PrimitiveFacilities = sites };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(changedState));

        var changedAction = Rewrite(save, p =>
        {
            var actions = p.Actions.ToArray();
            var index = Array.FindIndex(actions,
                x => x.Kind == "start_primitive_facility");
            actions[index] = actions[index] with { FacilityAssemblyOrdinal = 999 };
            return p with { Actions = actions };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(changedAction));

        var downgraded = Rewrite(save, p => p,
            CanonicalPhysicalTacticalSession.MaterialAssemblyEnvelopeFormat);
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(downgraded));
    }
}
