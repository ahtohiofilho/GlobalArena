using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

/// <summary>
/// GA-VS124: the generic WorkShelter was invented for integration testing,
/// NOT approved as a gameplay installation. Preserve all authenticated saves,
/// historic construction/worker invariants and the primitive without-building
/// economy while the Godot GUI gates this fixture behind diagnostics.
/// </summary>
public sealed class GaVs124UnapprovedWorkShelterLabBoundaryTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start();

    private static void OneFixedSixCycle(CanonicalPhysicalTacticalSession game)
    {
        for (var tick = 0; tick < 5; tick++)
            Assert.Null(game.AdvanceMovementTurn());
        Assert.NotNull(game.AdvanceMovementTurn());
    }

    private static CanonicalPhysicalTacticalSession Built(
        out int frameOrdinal)
    {
        var game = Start();
        OneFixedSixCycle(game);
        var frame = game.AssemblePrimitiveMaterialFrame(game.InitialTile,
            CanonicalPrimitiveAssemblyKind.UncommissionedMaterialFrame,1UL,1UL);
        frameOrdinal = frame.ActionOrdinal;
        var before = game.State;
        var started = game.StartPrimitiveFacility(game.InitialTile,
            frameOrdinal,EconomicActivityKind.Forestry,1UL);
        Assert.False(started.IsApprovedGameplayFacility);
        Assert.False(started.AuthorizesProductivityBonus);
        Assert.Equal(before.TotalWorkers,game.State.TotalWorkers);
        Assert.Equal(before.Stocks,game.State.Stocks);
        OneFixedSixCycle(game);
        Assert.True(Assert.Single(game.PrimitiveFacilities).IsOperational);
        return game;
    }

    [Fact]
    public void NormalPrimitiveEconomyDoesNotNeedOrAutoCreateTestShelter()
    {
        var game = Start();
        var hash = game.CurrentHash;
        var originalSave = game.ExportSaveJson();
        Assert.Empty(game.PrimitiveFacilities);
        Assert.Empty(CanonicalTacticalWorksiteMapProjection.FromSession(game).Tiles);
        Assert.Single(EmergentSettlementProjection.FromSession(game).Sites);
        Assert.Equal(hash,game.CurrentHash);
        Assert.Equal(originalSave,game.ExportSaveJson());

        OneFixedSixCycle(game);
        Assert.True(game.State.Cycles[^1].ProducedRaw > 0UL);
        Assert.Empty(game.PrimitiveFacilities);
        Assert.Empty(CanonicalTacticalWorksiteMapProjection.FromSession(game).Tiles);
        Assert.Single(EmergentSettlementProjection.FromSession(game).Sites);
        var saved = game.ExportSaveJson();
        Assert.Equal(saved,
            CanonicalPhysicalTacticalSession.RestoreSaveJson(saved).ExportSaveJson());
    }

    [Fact]
    public void ArchivedWorkShelterIsNeverAnApprovedGameplayFacility()
    {
        var game = Built(out int ordinal);
        var shelter = Assert.Single(game.PrimitiveFacilities);
        Assert.Equal(ordinal,shelter.AssemblyActionOrdinal);
        Assert.Equal(PhysicalPrimitiveFacilityKind.WorkShelter,shelter.Kind);
        Assert.False(shelter.IsApprovedGameplayFacility);
        Assert.False(shelter.AuthorizesProductivityBonus);
        Assert.False(shelter.AuthorizesNewTradeableGood);
        Assert.False(shelter.CanAuthorizeRealPurchasingPower);
        Assert.Equal(0UL,shelter.OperatingWorkers);
        Assert.True(shelter.IsOperational);

        var projection = CanonicalTacticalWorksiteMapProjection.FromSession(game);
        var tile = Assert.Single(projection.Tiles);
        Assert.False(tile.IsApprovedGameplayFacility);
        Assert.False(projection.ContainsApprovedGameplayFacilities);
        Assert.False(projection.MutatesCanonicalWorld);
        Assert.Equal(TacticalWorksiteMapStage.Completed,tile.Stage);
        Assert.Single(EmergentSettlementProjection.FromSession(game).Sites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingStaffedSave29AndTreasury30AreByteExactAfterReplay(
        bool dormantTreasury)
    {
        var game = Built(out int ordinal);
        var initial = game.CurrentHash;
        game.StaffPrimitiveFacility(game.InitialTile,ordinal,
            EconomicActivityKind.Forestry,1UL);
        Assert.False(Assert.Single(game.PrimitiveFacilities).IsApprovedGameplayFacility);
        Assert.NotEqual(initial,game.CurrentHash);
        if (dormantTreasury)
            game.EnableDormantTreasuryReservation();

        var save = game.ExportSaveJson();
        var env = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)
            ?? throw new InvalidOperationException("Missing save envelope.");
        Assert.Equal(dormantTreasury
                ? CanonicalPhysicalTacticalSession.FacilityAssistedWorkWithTreasuryEnvelopeFormat
                : CanonicalPhysicalTacticalSession.FacilityAssistedWorkEnvelopeFormat,
            env.Version);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save,restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash,restored.CurrentHash);
        Assert.Equal(game.Events,restored.Events);
        Assert.False(Assert.Single(restored.PrimitiveFacilities)
            .IsApprovedGameplayFacility);
        Assert.Equal(
            CanonicalTacticalWorksiteMapProjection.FromSession(game).Tiles.ToArray(),
            CanonicalTacticalWorksiteMapProjection.FromSession(restored).Tiles.ToArray());

        OneFixedSixCycle(game);
        OneFixedSixCycle(restored);
        Assert.Equal(game.CurrentHash,restored.CurrentHash);
        Assert.Equal(game.ExportSaveJson(),restored.ExportSaveJson());
    }

    [Fact]
    public void LegacyLabOnlyFacilityDoesNotGrantAdditionalProduction()
    {
        var lab = Built(out int ordinal);
        lab.StaffPrimitiveFacility(lab.InitialTile,ordinal,
            EconomicActivityKind.Forestry,1UL);

        // An identical real-world production tick with the same genuine
        // worker allocation, but no shelter, must yield identical output.
        // Independent from previous construction expenses and work cycles.
        var baseline = Start();
        OneFixedSixCycle(baseline);
        OneFixedSixCycle(baseline);
        Assert.Equal(lab.State.TotalWorkers,baseline.State.TotalWorkers);
        Assert.Equal(lab.State.Allocations,baseline.State.Allocations);
        OneFixedSixCycle(lab);
        OneFixedSixCycle(baseline);
        Assert.Equal(lab.State.Cycles[^1].ProducedRaw,
            baseline.State.Cycles[^1].ProducedRaw);
        Assert.True(lab.PrimitiveFacilities[0].OperatingWorkers > 0UL);
        Assert.False(lab.PrimitiveFacilities[0].IsApprovedGameplayFacility);
        var witness = lab.CaptureLatestFacilityAssistedWorkWitness();
        Assert.False(witness.CreatesAdditionalProduction);
        Assert.False(witness.AuthorizesFacilityYieldMultiplier);
        Assert.False(witness.CanAuthorizeRealPurchasingPower);
    }
}
