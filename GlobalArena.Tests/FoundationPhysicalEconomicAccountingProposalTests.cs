using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Financial evidence from real workers and fixed-freight allocation.
/// NEVER asserts monetary settlement, treasury mutation or invented
/// published economic cycles. Protect the universal price policy and
/// reject known owner-split physical market ambiguity before commit.
/// </summary>
public sealed class FoundationPhysicalEconomicAccountingProposalTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand(
        params CommodityId[] commodities) =>
        new(commodities,
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(
                profile => commodities.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo(
        params CommodityId[] commodities) =>
        new("precommit-physical-cargo",
            commodities.Select(item =>
                new FoundationCommodityCargoConversion(
                    item, 1_000_000UL)));

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Census,
        FoundationActivityCommodityDemandPolicy Demand,
        FoundationCommodityCargoConversionPolicy Cargo,
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Scales,
        FoundationPhysicalLaggedFreightCyclePlan Candidate);

    private static Scenario RealTwoTileScenario(
        bool collocateCivilizations = false,
        ulong remoteDemandMultiplierMicro = 100_000_000UL,
        ulong physicalCost = 100_000UL,
        bool traversable = true)
    {
        var host = Host();
        var map = host.Incidence;
        var border = map.FineTopology.Edges[0];
        var fine = map.Refinement.FineParameters;
        var first = new PhysicalTacticalTileId(fine, border.IncidentCellIds[0]);
        var second = new PhysicalTacticalTileId(fine, border.IncidentCellIds[1]);
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked(
                (int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];
        var census = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red, coarse(first), Grain,
                1_000_000UL, PhysicalWorkerActivity.Producing,
                1_000_000U, first, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Blue,
                coarse(collocateCivilizations ? first : second), Grain,
                0UL, PhysicalWorkerActivity.Other,
                checked((uint)remoteDemandMultiplierMicro),
                collocateCivilizations ? first : second,
                PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = Demand(Grain);
        var cargo = Cargo(Grain);
        var road = new FoundationPhysicalRoutingEdgeCost(
            new PhysicalTacticalEdgeId(fine, border.Id),
            FoundationInfrastructureOwnershipKind.Owned, Blue,
            physicalCost, 30_000UL);
        var roads = traversable
            ? new[] { road }
            : Array.Empty<FoundationPhysicalRoutingEdgeCost>();
        var scales = traversable
            ? new[] { new FoundationPhysicalEdgeCarryingScale(
                road.Edge, 1_000_000UL) }
            : Array.Empty<FoundationPhysicalEdgeCarryingScale>();
        var candidate = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateUniversalCandidate(
                census, map, demand, cargo, roads, scales);
        return new Scenario(host, census, demand, cargo, roads, scales,
            candidate);
    }

    private static FoundationPhysicalEconomicTaxRate[] Taxes(
        uint red = 1_000U, uint blue = 0U) =>
        new[] { new FoundationPhysicalEconomicTaxRate(Red, red),
                new FoundationPhysicalEconomicTaxRate(Blue, blue) };

    private static FoundationPhysicalEconomicAccountingProposal Evaluate(
        Scenario scenario,
        IEnumerable<FoundationPhysicalEconomicTaxRate>? tax = null) =>
        FoundationPhysicalEconomicAccountingReconciler.EvaluateCandidate(
            scenario.Census, scenario.Host.Incidence, scenario.Demand,
            scenario.Cargo, scenario.Candidate, tax ?? Taxes());

    [Fact]
    public void UniversalPriceAndActualWorkerDemandProduceExactBalancedProposal()
    {
        var s = RealTwoTileScenario();
        var hash = s.Host.StateHash;
        var snapshot = s.Host.Snapshot;
        var result = Evaluate(s);
        Assert.False(result.IsCommitted);
        Assert.False(s.Candidate.RepricedWithinCycle);
        Assert.False(s.Candidate.NextCycleQuotes.HasSettledProvenance);
        Assert.Equal(FoundationPhysicalEconomicAccountingProposal.MoneyScale,
            new BigInteger(10_000_000_000_000_000UL));
        Assert.All(result.Lines, row =>
        {
            Assert.True(row.GoodsMicro > 0UL);
            Assert.Equal(row.GrossScaled, row.PhysicalFreightScaled +
                row.ExternalTollsScaled + row.SellerTaxScaled +
                row.SellerNetScaled);
            Assert.Equal(row.GrossScaled / 10,
                row.SellerTaxScaled);
            Assert.Equal(Red, row.Seller);
        });
        Assert.Equal(result.GrossSalesScaled,
            result.PhysicalFreightScaled + result.ExternalTollsScaled +
            result.RevenueTaxScaled + result.NetSellerReceiptsScaled);
        var destinationTrade = result.Lines.Where(row =>
            row.Buyer == Blue).ToArray();
        Assert.NotEmpty(destinationTrade);
        Assert.All(destinationTrade, row =>
        {
            Assert.True(row.PhysicalFreightScaled > BigInteger.Zero);
            Assert.True(row.ExternalTollsScaled > BigInteger.Zero);
            Assert.True(row.MarketPriceMicroPerWholeGood > 0UL);
        });
        Assert.Equal(result.ExternalTollsScaled,
            Assert.Single(result.Accounts.Where(x => x.Civilization == Blue))
                .TollIncomeScaled);
        Assert.Equal(BigInteger.Zero,
            Assert.Single(result.Accounts.Where(x => x.Civilization == Red))
                .TollIncomeScaled);
        Assert.Equal(s.Candidate.Distribution.Candidates.Count,
            result.Lines.Count);
        Assert.Same(snapshot, s.Host.Snapshot);
        Assert.Equal(hash, s.Host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void AbsentPhysicalRoadMeansNoFreightAndNoInventedTrade()
    {
        var s = RealTwoTileScenario(
            remoteDemandMultiplierMicro: 1_000_000UL,
            traversable: false);
        var proposal = Evaluate(s);
        Assert.All(proposal.Lines, row =>
        {
            Assert.Equal(Red, row.Buyer);
            Assert.Equal(BigInteger.Zero, row.PhysicalFreightScaled);
            Assert.Equal(BigInteger.Zero, row.ExternalTollsScaled);
        });
        Assert.Equal(BigInteger.Zero, proposal.ExternalTollsScaled);
        Assert.Equal(BigInteger.Zero, proposal.PhysicalFreightScaled);
        Assert.All(proposal.Accounts, account =>
            Assert.Equal(BigInteger.Zero, account.TollIncomeScaled));
    }

    [Fact]
    public void CollocatedDifferentCivilizationsMustNotSettleTwoPricesInOnePhysicalMarket()
    {
        var s = RealTwoTileScenario(collocateCivilizations: true,
            remoteDemandMultiplierMicro: 1_000_000UL);
        var error = Assert.Throws<InvalidOperationException>(() =>
            Evaluate(s));
        Assert.Contains("owner-split water-filling", error.Message);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void MissingDuplicatedAndInvalidTaxPoliciesFailInsteadOfAssumingFreeTaxes()
    {
        var s = RealTwoTileScenario();
        Assert.Throws<ArgumentException>(() =>
            Evaluate(s, new[]
            {
                new FoundationPhysicalEconomicTaxRate(Red, 1_000U)
            }));
        Assert.Throws<ArgumentException>(() =>
            Evaluate(s, new[]
            {
                new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Blue, 0U)
            }));
        Assert.Throws<ArgumentException>(() =>
            Evaluate(s, new[]
            {
                new FoundationPhysicalEconomicTaxRate(Red, 10_001U),
                new FoundationPhysicalEconomicTaxRate(Blue, 0U)
            }));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Blue).Availability);
    }

    [Fact]
    public void RepeatedExecutionIsDeterministicAndNeverRepricesTheCurrentTurn()
    {
        var s = RealTwoTileScenario();
        var first = Evaluate(s);
        var repeat = Evaluate(s);
        Assert.Equal(first.Lines, repeat.Lines);
        Assert.Equal(first.Accounts, repeat.Accounts);
        Assert.Equal(first.GrossSalesScaled, repeat.GrossSalesScaled);
        Assert.Equal(first.RevenueTaxScaled, repeat.RevenueTaxScaled);
        Assert.False(first.IsCommitted);
        Assert.False(repeat.IsCommitted);
        Assert.All(s.Candidate.FixedCycleEdges, r =>
            Assert.Equal(100_000UL,
                r.PhysicalCostMicroPerWholeCargoUnit));
    }

    [Fact]
    public void RealProductionMustBeSoldOnlyToWorkersInItsDeclaredCommodityMarket()
    {
        var s = RealTwoTileScenario();
        var plan = s.Candidate;
        var output = Evaluate(s);
        Assert.Equal(plan.Distribution.AllocationAudit.TotalAvailableGoodsMicro,
            output.Lines.Aggregate(0UL, (sum, row) =>
                checked(sum + row.GoodsMicro)));
        Assert.All(output.Lines, row =>
        {
            Assert.Equal(Grain, row.Commodity);
            Assert.True(row.MarketPriceMicroPerWholeGood > 0UL);
        });
        Assert.Equal(1U,
            output.Lines.Select(x => x.Commodity.Value).Distinct().Single());
        Assert.NotEqual(0, s.Host.Incidence.FineTopology.Edges.Count);
    }
}
