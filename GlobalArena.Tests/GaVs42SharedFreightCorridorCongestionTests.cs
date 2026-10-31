using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs42SharedFreightCorridorCongestionTests
{
    private const ulong Technology = 101UL;
    private const ulong Catalog = 7UL;
    private static readonly GoldbergParameters Fine = new(18, 0);
    private static readonly StrategicEconomicRouteKey CommercialKey =
        new(new EconomicPointId(1UL), new EconomicPointId(2UL));

    private static ExperimentalFreightTechnologyPricing Calibration(
        ulong technology = Technology, BigInteger? standaloneSaturation = null,
        BigInteger? overhead = null) =>
        new(technology, Catalog, 42UL,
            ExperimentalLandInfrastructureKind.StoneRoad,
            1, 1, 1, overhead ?? BigInteger.Zero,
            1_000_000, standaloneSaturation ?? BigInteger.Zero, 1_000_000);

    private static ExperimentalSharedFreightCorridorKey Resource(
        ulong edge, ulong technology = Technology) =>
        new(new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(edge)),
            technology, Catalog);

    private static ExperimentalSharedFreightOption Option(
        ulong serviceId, long baseNumerator,
        ExperimentalSharedFreightCorridorKey resource,
        BigInteger? standaloneSaturation = null,
        BigInteger? overhead = null) =>
        ExperimentalSharedFreightOption.Synthetic(
            serviceId, CommercialKey,
            Calibration(resource.TechnologyTypeId, standaloneSaturation, overhead),
            baseNumerator, new[] { resource });

    private static ExperimentalSharedFreightLot Lot(ulong id,
        params ExperimentalSharedFreightOption[] offers) =>
        new(id, 1_000_000, offers);

    private static ExperimentalSharedFreightCongestionPolicy Policy(
        BigInteger? weight = null, BigInteger? scale = null) =>
        new(weight ?? 10, scale ?? 1_000_000);

    [Fact]
    public void SharedCrowdingMakesSplittingLotsCheaperThanIndependentPreference()
    {
        var a = Resource(1);
        var b = Resource(2);
        var lots = new[]
        {
            Lot(2, Option(1, 1_000_000, a), Option(2, 2_000_000, b)),
            Lot(1, Option(1, 1_000_000, a), Option(2, 2_000_000, b))
        };
        var comparison = new ExperimentalSharedFreightAllocationLaboratory()
            .Compare(lots, Policy(), maxExactConfigurations: 16);
        Assert.Equal(new ulong[] { 1, 1 }, comparison.Independent.ChosenServiceIds);
        Assert.Equal(0, comparison.Independent.TotalCost.CompareTo(
            new FoodTransportCostFraction(82, 1)));
        Assert.Equal(0, comparison.Coordinated.TotalCost.CompareTo(
            new FoodTransportCostFraction(23, 1)));
        Assert.NotNull(comparison.Exact);
        Assert.Equal(0, comparison.Coordinated.TotalCost.CompareTo(
            comparison.Exact!.TotalCost));
        Assert.Equal(new ulong[] { 1, 2 }, comparison.Exact.ChosenServiceIds);
        Assert.Equal(new BigInteger(1_000_000), comparison.Exact.CorridorLoads[a]);
        Assert.Equal(new BigInteger(1_000_000), comparison.Exact.CorridorLoads[b]);
        Assert.Equal(new BigInteger(4), comparison.PossibleConfigurations);
    }

    [Fact]
    public void StandaloneVs40SaturationIsNotChargedOnTopOfSharedEdgeCongestion()
    {
        var crowded = Option(1, 1_000_000, Resource(1),
            standaloneSaturation: BigInteger.Pow(10, 18));
        var lot = Lot(1, crowded);
        var result = ExperimentalSharedFreightBatchCost.Evaluate(
            new[] { lot }, new[] { 0 }, Policy());
        // One gram shipment costs 1 (geography) + 10 (shared corridor)
        // regardless of the unused standalone VS40 saturation coefficient.
        Assert.Equal(0, result.TotalCost.CompareTo(
            new FoodTransportCostFraction(11, 1)));
        Assert.True(crowded.Pricing.EvaluateRawUnitCostPerGram(1_000_000)
            .CompareTo(new FoodTransportCostFraction(11, 1)) > 0);
    }

    [Fact]
    public void ASecondTechnologyOnTheSameFineCrossingDoesNotShareSaturation()
    {
        var road = Resource(1, Technology);
        var rail = Resource(1, 102UL);
        var sameResource = new[]
        {
            Lot(1, Option(1, 0, road)),
            Lot(2, Option(1, 0, road))
        };
        var separate = new[]
        {
            Lot(1, Option(1, 0, road)),
            Lot(2, Option(2, 0, rail))
        };
        var together = ExperimentalSharedFreightBatchCost.Evaluate(
            sameResource, new[] { 0, 0 }, Policy());
        var split = ExperimentalSharedFreightBatchCost.Evaluate(
            separate, new[] { 0, 0 }, Policy());
        Assert.Equal(0, together.TotalCost.CompareTo(
            new FoodTransportCostFraction(80, 1)));
        Assert.Equal(0, split.TotalCost.CompareTo(
            new FoodTransportCostFraction(20, 1)));
        Assert.Equal(2, split.CorridorLoads.Count);
    }

    [Fact]
    public void SmoothCongestionHasNoHardLimitEvenAtVeryLargePhysicalLoads()
    {
        var lot = new ExperimentalSharedFreightLot(
            1, BigInteger.Pow(10, 24),
            new[] { Option(1, 0, Resource(1)) });
        var eval = ExperimentalSharedFreightBatchCost.Evaluate(
            new[] { lot }, new[] { 0 }, Policy());
        Assert.True(eval.TotalCost.Numerator > eval.TotalCost.Denominator);
        Assert.Equal(BigInteger.Pow(10, 24), eval.CorridorLoads[Resource(1)]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExperimentalSharedFreightCongestionPolicy(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExperimentalSharedFreightCongestionPolicy(-1, 1));
    }

    [Fact]
    public void NoCongestionRecoversCheapestIndependentWholeServicePrices()
    {
        var options = new[] { Option(2, 2_000_000, Resource(1)),
            Option(1, 1_000_000, Resource(2)) };
        var comparison = new ExperimentalSharedFreightAllocationLaboratory()
            .Compare(new[] { Lot(2, options), Lot(1, options) }, Policy(0),
                maxExactConfigurations: 16);
        Assert.Equal(new ulong[] { 1, 1 }, comparison.Independent.ChosenServiceIds);
        Assert.Equal(0, comparison.Coordinated.TotalCost.CompareTo(
            comparison.Independent.TotalCost));
        Assert.Equal(0, comparison.Exact!.TotalCost.CompareTo(
            comparison.Independent.TotalCost));
    }

    [Fact]
    public void DuplicateLotsUnknownRoutesAndNonMatchingOriginsAreRejected()
    {
        var valid = Option(1, 1_000_000, Resource(1));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalSharedFreightLot(1, 10,
                new[] { valid, valid }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExperimentalSharedFreightLot(1, 0, new[] { valid }));
        var otherRoute = ExperimentalSharedFreightOption.Synthetic(
            2, new StrategicEconomicRouteKey(
                new EconomicPointId(2), new EconomicPointId(3)),
            Calibration(), 0, new[] { Resource(2) });
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalSharedFreightLot(1, 10,
                new[] { valid, otherRoute }));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalSharedFreightOption(3, CommercialKey,
                valid.Pricing, new[] { Resource(1), Resource(1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExperimentalSharedFreightBatchCost.Evaluate(
                new[] { Lot(1, valid) }, new[] { 1 }, Policy()));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalSharedFreightAllocationLaboratory().Compare(
                new[] { Lot(1, valid), Lot(1, valid) }, Policy()));
    }

    [Fact]
    public void BoundedExactSearchNeverPretendsToCertifyLargePortfolios()
    {
        var options = new[]
        {
            Option(1, 1_000_000, Resource(1)),
            Option(2, 2_000_000, Resource(2))
        };
        var many = Enumerable.Range(1, 20)
            .Select(i => Lot((ulong)i, options)).ToArray();
        var comparison = new ExperimentalSharedFreightAllocationLaboratory()
            .Compare(many, Policy(), maxImprovementPasses: 3,
                maxExactConfigurations: 256);
        Assert.Null(comparison.Exact);
        Assert.Equal(BigInteger.Pow(2, 20), comparison.PossibleConfigurations);
        Assert.True(comparison.Coordinated.TotalCost.CompareTo(
            comparison.Independent.TotalCost) <= 0);
    }

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0), Fine,
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    [Fact]
    public void RealGoldbergBuiltOffersKeepVersionedPhysicalCorridorsAndGameState()
    {
        var game = Start();
        var saved = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var first = game.InitialTile;
        var near = game.AdjacentWorkable(first).ToHashSet();
        var last = near.SelectMany(game.AdjacentWorkable)
            .Where(x => x != first && !near.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
        var middle = game.AdjacentWorkable(last).Where(near.Contains)
            .OrderBy(x => x.FineStrategicCellId.Value).First();

        EconomicTransportBuiltAsset Asset(
            PhysicalTacticalTileId a, PhysicalTacticalTileId b,
            ulong assetId, ulong tech)
        {
            var edge = game.FineWorld.StrategicTopology.Edges.First(e =>
                e.IncidentCellIds.Contains(a.FineStrategicCellId) &&
                e.IncidentCellIds.Contains(b.FineStrategicCellId));
            return new EconomicTransportBuiltAsset(assetId, tech,
                new PhysicalTacticalEdgeId(Fine, edge.Id), Catalog);
        }

        var assets = new[]
        {
            Asset(first, middle, 11, Technology),
            Asset(middle, last, 12, Technology),
            Asset(first, middle, 21, 102),
            Asset(middle, last, 22, 102)
        };
        var network = new ExperimentalLandInfrastructureSnapshot(
            game, facts, assets,
            new Dictionary<(ulong, ulong), ExperimentalLandInfrastructureKind>
            {
                [(Technology, Catalog)] = ExperimentalLandInfrastructureKind.StoneRoad,
                [(102, Catalog)] = ExperimentalLandInfrastructureKind.StoneRoad
            }, StrategicEdgeAccessSnapshot.AllOpen, 1);
        var anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>
        {
            [new EconomicPointId(1)] = first,
            [new EconomicPointId(2)] = last
        };
        var routeKey = CommercialKey;
        var intents = new[]
        {
            new EconomicFineTransportServiceIntent(
                101, routeKey, Technology, Catalog, 1_000_000),
            new EconomicFineTransportServiceIntent(
                102, routeKey, 102, Catalog, 1_000_000)
        };
        var policies = new[] { Calibration(Technology), Calibration(102) };
        var offers = new ExperimentalCostOptimalFreightItineraryDiscovery()
            .Discover(game, facts, network, anchors,
                StrategicEdgeAccessSnapshot.AllOpen, assets, intents, policies);
        Assert.Equal(2, offers.Count);
        var options = offers.Select(o => ExperimentalSharedFreightOption
            .FromBuiltOffer(o, game, anchors, assets,
                StrategicEdgeAccessSnapshot.AllOpen)).ToArray();
        Assert.Equal(2, options[0].Corridors.Count);
        Assert.Equal(2, options[1].Corridors.Count);
        Assert.NotEqual(options[0].Corridors[0], options[1].Corridors[0]);
        Assert.Equal(options[0].Corridors[0].FineCrossing,
            options[1].Corridors[0].FineCrossing);

        var comparisons = new ExperimentalSharedFreightAllocationLaboratory()
            .Compare(new[] {
                new ExperimentalSharedFreightLot(1, 1_000_000, options),
                new ExperimentalSharedFreightLot(2, 1_000_000, options)
            }, Policy(), maxExactConfigurations: 16);
        Assert.Equal(4, comparisons.PossibleConfigurations);
        Assert.True(comparisons.Coordinated.TotalCost.CompareTo(
            comparisons.Independent.TotalCost) <= 0);

        var closed = new StrategicEdgeAccessSnapshot(
            new[] { assets[0].GeographicEdgeId });
        Assert.Throws<InvalidOperationException>(() =>
            ExperimentalSharedFreightOption.FromBuiltOffer(
                offers[0], game, anchors, assets, closed));
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }
}
