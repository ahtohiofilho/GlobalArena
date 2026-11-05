using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS118: post-assembly FIELD commissioning based on a real completed
/// productive cycle. This is not dedicated building labor and gives no bonus.
/// </summary>
public sealed class GaVs118PrimitiveWorkSiteCommissioningTests
{
    private static CanonicalPhysicalTacticalSession Stocked()
    {
        var game = CanonicalPhysicalTacticalSession.Start();
        for (var i = 0; i < 6; i++)
            game.AdvanceMovementTurn();
        Assert.True(game.StockAt(game.InitialTile, new CommodityId(3U)) > 0UL);
        Assert.True(game.StockAt(game.InitialTile, new CommodityId(2U)) > 0UL);
        return game;
    }

    private static CanonicalPhysicalTacticalSession Prepared(
        out CanonicalExecutedPrimitiveAssembly frame)
    {
        var game = Stocked();
        frame = game.AssemblePrimitiveMaterialFrame(
            game.InitialTile, CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,
            1UL, 1UL);
        return game;
    }

    private static void CompleteOneMoreRealCycle(
        CanonicalPhysicalTacticalSession game)
    {
        for (var i = 0; i < 5; i++)
            Assert.Null(game.AdvanceMovementTurn());
        Assert.NotNull(game.AdvanceMovementTurn());
    }

    private static CanonicalPhysicalSaveEnvelope Envelope(string save) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)
            ?? throw new InvalidOperationException("Missing save envelope.");

    private static CanonicalPhysicalSavePayload Payload(string save) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            Envelope(save).PayloadJson)
            ?? throw new InvalidOperationException("Missing save payload.");

    private static string Alter(
        string save, Func<CanonicalPhysicalSavePayload,
            CanonicalPhysicalSavePayload> edit, int? envelopeVersion = null)
    {
        var envelope = Envelope(save);
        var body = JsonSerializer.Serialize(edit(Payload(save)));
        return JsonSerializer.Serialize(envelope with
        {
            Version = envelopeVersion ?? envelope.Version,
            PayloadJson = body,
            PayloadSha256Hex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(body)))
        });
    }

    [Fact]
    public void CannotCommissionWithoutARealCompletedPostAssemblyEconomicCycle()
    {
        var game = Prepared(out var frame);
        var original = game.ExportSaveJson();
        var beforeHash = game.CurrentHash;
        Assert.Throws<InvalidOperationException>(() =>
            game.CommissionPrimitiveWorkSite(game.InitialTile,
                frame.ActionOrdinal, EconomicActivityKind.Forestry));
        Assert.Equal(original, game.ExportSaveJson());
        Assert.Equal(beforeHash, game.CurrentHash);
        Assert.Empty(game.CommissionedPrimitiveWorkSites);

        // An off-cadence movement tick is NOT an economic close.
        Assert.Null(game.AdvanceMovementTurn());
        Assert.Throws<InvalidOperationException>(() =>
            game.CommissionPrimitiveWorkSite(game.InitialTile,
                frame.ActionOrdinal, EconomicActivityKind.Forestry));
    }

    [Theory]
    [InlineData(EconomicActivityKind.Forestry, 3U)]
    [InlineData(EconomicActivityKind.Mining, 2U)]
    [InlineData(EconomicActivityKind.Agriculture, 1U)]
    public void ExecutedPostAssemblyWorkCanRegisterTheExistingPrimitiveRole(
        EconomicActivityKind job, uint expectedCommodity)
    {
        var game = Prepared(out var frame);
        CompleteOneMoreRealCycle(game);
        var initialStocks = game.State.Stocks.ToArray();
        var lastCycle = game.State.Cycles[^1];
        var currentHash = game.CurrentHash.HexDigest;
        var workers = game.State.TotalWorkers;
        var beforeEvents = game.Events.Count;

        var registered = game.CommissionPrimitiveWorkSite(
            game.InitialTile, frame.ActionOrdinal, job);

        Assert.Equal(frame.ActionOrdinal, registered.AssemblyActionOrdinal);
        Assert.Equal(beforeEvents + 1, registered.CommissionActionOrdinal);
        Assert.Equal(game.InitialTile.FineStrategicCellId.Value, registered.FineTile);
        Assert.Equal(job, registered.Activity);
        Assert.Equal(expectedCommodity, registered.ExecutedCommodity);
        Assert.Equal(2UL, registered.WitnessCycleOrdinal);
        Assert.Equal(12UL, registered.WitnessMovementTurn);
        Assert.True(registered.WitnessProducedRaw > 0UL);
        Assert.True(registered.HasExecutedPostAssemblyWorkEvidence);
        Assert.True(registered.IsRegisteredForExistingPrimitiveWork);
        Assert.False(registered.IsDedicatedConstructionLaborAuthenticated);
        Assert.False(registered.IsCanonicalWorldAsset);
        Assert.False(registered.AuthorizesAdditionalProductivity);
        Assert.False(registered.CreatesTradeableManufacturedGood);
        Assert.False(registered.CanAuthorizeRealPurchasingPower);

        Assert.Equal(currentHash, registered.BeforeWorldHashHex);
        Assert.Equal(game.CurrentHash.HexDigest, registered.AfterWorldHashHex);
        Assert.Equal(initialStocks, game.State.Stocks);
        Assert.Equal(lastCycle, game.State.Cycles[^1]);
        Assert.Equal(workers, game.State.TotalWorkers);
        Assert.True(game.IsCommissionedWorkSiteCurrentlyStaffed(registered));
        Assert.Single(game.CommissionedPrimitiveWorkSites);
        var action = game.Events[^1];
        Assert.Equal("commission_primitive", action.Kind);
        Assert.Equal(frame.ActionOrdinal, action.CommissionAssemblyOrdinal);
        Assert.Equal((byte)job, action.CommissionActivity);
        Assert.Equal(0UL, action.Workers);
        Assert.Equal(0UL, action.MaterialRaw);
        Assert.Equal(0UL, action.AssemblyWoodRaw);
        Assert.Equal(0UL, action.AssemblyStoneRaw);
        Assert.NotEqual(action.BeforeHashHex, action.AfterHashHex);
    }

    [Fact]
    public void DuplicateAssemblyOrSameTileRoleCannotBeCommissionedTwice()
    {
        var game = Prepared(out var first);
        var second = game.AssemblePrimitiveMaterialFrame(
            game.InitialTile, CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,
            1UL, 1UL);
        CompleteOneMoreRealCycle(game);
        game.CommissionPrimitiveWorkSite(game.InitialTile,
            first.ActionOrdinal, EconomicActivityKind.Forestry);
        var saved = game.ExportSaveJson();

        Assert.Throws<InvalidOperationException>(() =>
            game.CommissionPrimitiveWorkSite(game.InitialTile,
                first.ActionOrdinal, EconomicActivityKind.Mining));
        Assert.Throws<InvalidOperationException>(() =>
            game.CommissionPrimitiveWorkSite(game.InitialTile,
                second.ActionOrdinal, EconomicActivityKind.Forestry));
        Assert.Equal(saved, game.ExportSaveJson());

        game.CommissionPrimitiveWorkSite(game.InitialTile,
            second.ActionOrdinal, EconomicActivityKind.Mining);
        Assert.Equal(2, game.CommissionedPrimitiveWorkSites.Count);
        Assert.Equal(2, game.ExecutedPrimitiveAssemblies.Count);
    }

    [Fact]
    public void WrongLocationOrUnknownAssemblyIsRefusedWithoutMutation()
    {
        var game = Prepared(out var frame);
        CompleteOneMoreRealCycle(game);
        var other = game.AdjacentWorkable(game.InitialTile)[0];
        var snapshot = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.CommissionPrimitiveWorkSite(other,
                frame.ActionOrdinal, EconomicActivityKind.Forestry));
        Assert.Throws<InvalidOperationException>(() =>
            game.CommissionPrimitiveWorkSite(game.InitialTile,
                frame.ActionOrdinal + 10, EconomicActivityKind.Forestry));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            game.CommissionPrimitiveWorkSite(game.InitialTile,
                frame.ActionOrdinal, EconomicActivityKind.TradeLogistics));
        Assert.Equal(snapshot, game.ExportSaveJson());
        Assert.Empty(game.CommissionedPrimitiveWorkSites);
    }

    [Fact]
    public void NoFieldWorkAfterTheFrameMeansNoCommissioning()
    {
        var game = Prepared(out var frame);
        game.ReassignWorkers(game.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 2UL);
        CompleteOneMoreRealCycle(game);
        Assert.Throws<InvalidOperationException>(() =>
            game.CommissionPrimitiveWorkSite(game.InitialTile,
                frame.ActionOrdinal, EconomicActivityKind.Forestry));
        Assert.Empty(game.CommissionedPrimitiveWorkSites);
    }

    [Fact]
    public void WorkSiteStaffingIsARealTimeQueryNotAnInventedAutomaticLaborer()
    {
        var game = Prepared(out var frame);
        CompleteOneMoreRealCycle(game);
        var site = game.CommissionPrimitiveWorkSite(
            game.InitialTile, frame.ActionOrdinal, EconomicActivityKind.Forestry);
        Assert.True(game.IsCommissionedWorkSiteCurrentlyStaffed(site));
        game.ReassignWorkers(game.InitialTile,
            EconomicActivityKind.Forestry, EconomicActivityKind.Agriculture, 2UL);
        Assert.False(game.IsCommissionedWorkSiteCurrentlyStaffed(site));
        Assert.Single(game.CommissionedPrimitiveWorkSites);
    }

    [Fact]
    public void Save25ReplaysExactCommissioningEvidenceAndHistory()
    {
        var game = Prepared(out var frame);
        CompleteOneMoreRealCycle(game);
        var original = game.CommissionPrimitiveWorkSite(game.InitialTile,
            frame.ActionOrdinal, EconomicActivityKind.Forestry);
        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.PrimitiveWorkSiteEnvelopeFormat,
            Envelope(save).Version);
        Assert.Contains("CommissionAssemblyOrdinal", Envelope(save).PayloadJson);

        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        var restored = Assert.Single(replay.CommissionedPrimitiveWorkSites);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(game.Events, replay.Events);
        Assert.Equal(original.WitnessClosingTickHashHex,
            restored.WitnessClosingTickHashHex);
        Assert.Equal(original.WitnessProducedRaw, restored.WitnessProducedRaw);
        Assert.Equal(original.WitnessCycleOrdinal, restored.WitnessCycleOrdinal);
        Assert.Equal(original.AfterWorldHashHex, restored.AfterWorldHashHex);
        Assert.True(replay.IsCommissionedWorkSiteCurrentlyStaffed(restored));
        Assert.False(restored.CanAuthorizeRealPurchasingPower);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Save26ReplaysTreasuryActivatedBeforeOrAfterFieldCommissioning(
        bool activateBefore)
    {
        var game = Prepared(out var frame);
        if (activateBefore)
            game.EnableDormantTreasuryReservation();
        CompleteOneMoreRealCycle(game);
        game.CommissionPrimitiveWorkSite(game.InitialTile,
            frame.ActionOrdinal, EconomicActivityKind.Mining);
        if (!activateBefore)
            game.EnableDormantTreasuryReservation();

        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.PrimitiveWorkSiteWithTreasuryEnvelopeFormat,
            Envelope(save).Version);
        Assert.NotNull(Payload(save).TreasuryReservation);
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.Events, replay.Events);
        Assert.Equal(game.CurrentHash, replay.CurrentHash);
        Assert.Equal(game.CommissionedPrimitiveWorkSites[0].WitnessProducedRaw,
            replay.CommissionedPrimitiveWorkSites[0].WitnessProducedRaw);
    }

    [Fact]
    public void ExistingAssemblyOnlySave23HasExactlyTheSameBytesWithoutCommissioning()
    {
        var game = Prepared(out _);
        var save = game.ExportSaveJson();
        Assert.Equal(
            CanonicalPhysicalTacticalSession.MaterialAssemblyEnvelopeFormat,
            Envelope(save).Version);
        Assert.DoesNotContain("CommissionActivity", Envelope(save).PayloadJson);
        Assert.Equal(save,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(save).ExportSaveJson());
    }

    [Fact]
    public void ChangedCommissioningProofIsRejectedEvenWithRehashedSaveEnvelope()
    {
        var game = Prepared(out var frame);
        CompleteOneMoreRealCycle(game);
        game.CommissionPrimitiveWorkSite(game.InitialTile,
            frame.ActionOrdinal, EconomicActivityKind.Forestry);
        var save = game.ExportSaveJson();
        var changed = Alter(save, payload =>
        {
            var actions = payload.Actions.ToArray();
            actions[^1] = actions[^1] with { CommissionActivity =
                (byte)EconomicActivityKind.Mining };
            return payload with { Actions = actions };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(changed));

        var withoutAuthorization = Alter(save, p => p,
            CanonicalPhysicalTacticalSession.MaterialAssemblyEnvelopeFormat);
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(withoutAuthorization));

        var invalidAssembly = Alter(save, payload =>
        {
            var actions = payload.Actions.ToArray();
            actions[^1] = actions[^1] with { CommissionAssemblyOrdinal = 999 };
            return payload with { Actions = actions };
        });
        Assert.ThrowsAny<Exception>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(invalidAssembly));
    }

    [Fact]
    public void RegisteredSiteSurvivesLaterCyclesButNeverProducesASecondOutput()
    {
        var game = Prepared(out var frame);
        CompleteOneMoreRealCycle(game);
        var site = game.CommissionPrimitiveWorkSite(game.InitialTile,
            frame.ActionOrdinal, EconomicActivityKind.Forestry);
        for (var i = 0; i < 6; i++)
            game.AdvanceMovementTurn();
        Assert.Equal(3UL, (ulong)game.State.Cycles.Count);
        Assert.Single(game.CommissionedPrimitiveWorkSites);
        Assert.Equal(site.WitnessProducedRaw,
            game.CommissionedPrimitiveWorkSites[0].WitnessProducedRaw);
        var save = game.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, replay.ExportSaveJson());
        Assert.Equal(game.State.Cycles, replay.State.Cycles);
        Assert.False(replay.CommissionedPrimitiveWorkSites[0].AuthorizesAdditionalProductivity);
    }
}
