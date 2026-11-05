using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// GA-UI-SETTLEMENT-GLYPH-01: a permanent-place drawing represents the
/// existing same continuous population/economy, never a founded city,
/// upgrade tier, production facility, or parallel population store.
/// The only distinctions of encampments are genuine travel/location state.
/// </summary>
public sealed class GaVs122UnifiedInhabitedPlacePresentationTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent);

    [Fact]
    public void UnifiedSettlementIsOneProjectionOfResidentsNotThreeEstablishedSites()
    {
        var session = Start();
        var save = session.ExportSaveJson();
        var hash = session.CurrentHash;
        var settlement = EmergentSettlementProjection.FromSession(session);
        var place = Assert.Single(settlement.Sites);
        var workforce = TacticalWorkerMarkerProjection.FromPhysicalState(session.State);

        Assert.Equal(session.InitialTile, place.Tile);
        Assert.Equal(8UL, place.Workers);
        Assert.Equal(4UL, place.AgriculturalWorkers);
        Assert.Equal(2UL, place.ForestryWorkers);
        Assert.Equal(2UL, place.MiningWorkers);
        Assert.Equal(3, workforce.Groups.Count);
        Assert.Equal(place.Workers, workforce.Groups.Aggregate(0UL,
            (sum, x) => checked(sum + x.Workers)));
        Assert.Equal(session.State.TotalWorkers, settlement.TotalResidentWorkers);
        Assert.Equal(0UL, settlement.TotalTravellers);
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
    }

    [Fact]
    public void ReallocationsDoNotFoundOrUpgradeASettlement()
    {
        var session = Start();
        var tile = session.InitialTile;
        session.ReassignWorkers(tile, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        var place = Assert.Single(
            EmergentSettlementProjection.FromSession(session).Sites);
        Assert.Equal(tile, place.Tile);
        Assert.Equal(8UL, place.Workers);
        Assert.Equal(2UL, place.AgriculturalWorkers);
        Assert.Equal(4UL, place.ForestryWorkers);
        Assert.Equal(2UL, place.MiningWorkers);

        var copy = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            session.ExportSaveJson());
        Assert.Equal(place, Assert.Single(
            EmergentSettlementProjection.FromSession(copy).Sites));
        Assert.Equal(session.CurrentHash, copy.CurrentHash);
    }

    [Fact]
    public void PopulationInTravelDoesNotInventAnotherSettlementAtTemporaryStop()
    {
        var session = Start();
        var destination = session.AdjacentWorkable(session.InitialTile)[0];
        session.OrderWorkerTransit(session.InitialTile, destination,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);

        var transit = EmergentSettlementProjection.FromSession(session);
        var origin = Assert.Single(transit.Sites);
        Assert.Equal(6UL, origin.Workers);
        Assert.Equal(2UL, transit.TotalTravellers);
        Assert.Equal(8UL, checked(transit.TotalResidentWorkers +
            transit.TotalTravellers));
        Assert.Null(transit.At(destination));

        session.AdvanceMovementTurn();
        var midway = EmergentSettlementProjection.FromSession(session);
        Assert.Single(midway.Sites);
        Assert.Equal(2UL, midway.TotalTravellers);

        session.AdvanceMovementTurn();
        var after = EmergentSettlementProjection.FromSession(session);
        Assert.Equal(2, after.Sites.Count);
        Assert.Equal(2UL, after.At(destination)!.Workers);
        Assert.Equal(0UL, after.TotalTravellers);
        Assert.Equal(8UL, after.TotalResidentWorkers);
        Assert.Equal(session.State.TotalWorkers,
            checked(after.TotalResidentWorkers + after.TotalTravellers));
    }
}
