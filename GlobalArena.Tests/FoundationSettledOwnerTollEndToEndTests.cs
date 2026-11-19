using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// R79 closes the EXISTING production pipeline on a genuinely used,
/// positive, third-party-tolled edge. No new financial algorithm, tariff
/// defaults, or player-visible credit capability is introduced.
/// </summary>
public sealed class FoundationSettledOwnerTollEndToEndTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationAuthoritativeEconomicSettlementPolicy Policy() =>
        new(
            new FoundationActivityCommodityDemandPolicy(
                new[] { Grain, Wood },
                Enum.GetValues<FoundationPhysicalDemandProfile>()
                    .SelectMany(profile => new[] { Grain, Wood }.Select(item =>
                        new FoundationActivityCommodityDemandCoefficient(
                            profile, item, 1_000_000UL)))),
            new FoundationCommodityCargoConversionPolicy(
                "r79-real-sales-cargo", new[]
                {
                    new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
                    new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
                }),
            new[]
            {
                new FoundationPhysicalEconomicTaxRate(Red, 1_000U),
                new FoundationPhysicalEconomicTaxRate(Blue, 500U)
            }, maxProducerPasses: 64);

    [Fact]
    public void AuditedThirdPartyCrossingPaysOwnerOnceInRealClosedCycle()
    {
        var scene = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            scene.Session.GeneratedWorld, scene.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var policy = Policy();
        var unpriced = scene.Snapshot.DeployInitialPhysicalTransport(
                seed.Transport, scene.Session.GeneratedWorld, scene.Incidence)
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 0UL), (Blue, 0UL) }));
        var natural = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(unpriced, scene.Incidence,
                policy.Demand, policy.Cargo, maxProducerPasses: 64);
        var used = natural.Distribution.Candidates.First(candidate =>
            candidate.RouteTiles.Count > 1);
        CivilizationId seller = used.Seller;
        CivilizationId owner = seller == Red ? Blue : Red;
        var a = used.RouteTiles[0].FineStrategicCellId.Value;
        var b = used.RouteTiles[1].FineStrategicCellId.Value;
        var geometry = scene.Incidence.FineTopology.Edges.Single(edge =>
            (edge.IncidentCellIds[0].Value == a &&
             edge.IncidentCellIds[1].Value == b) ||
            (edge.IncidentCellIds[0].Value == b &&
             edge.IncidentCellIds[1].Value == a));
        var id = new PhysicalTacticalEdgeId(
            scene.Incidence.Refinement.FineParameters, geometry.Id);
        var seedWithToll = PhysicalTransportInfrastructureRuntimeState.CreateInitial(
            seed.Transport.Edges.Select(edge => edge.Edge == id
                ? edge with
                {
                    OwnershipKind = PhysicalTransportOwnershipKind.Owned,
                    InfrastructureOwner = owner,
                    ExternalTariffMicroPerWholeCargoUnit = 1UL
                } : edge));
        var opening = scene.Snapshot.DeployInitialPhysicalTransport(
                seedWithToll, scene.Session.GeneratedWorld, scene.Incidence)
            .DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
                new[] { (Red, 0UL), (Blue, 0UL) }));
        scene.Session.AcceptSnapshot(opening);

        // Independently derive the actual delivery and accounting under the
        // SAME explicit owned-toll inventory that the kernel will settle.
        var quoted = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateWorldCandidate(opening, scene.Incidence,
                policy.Demand, policy.Cargo, maxProducerPasses: 64);
        var independent = FoundationPhysicalEconomicAccountingReconciler
            .EvaluateCandidate(opening.Workers, scene.Incidence,
                policy.Demand, policy.Cargo, quoted, policy.TaxRates);
        ulong crossingCargo = 0UL;
        foreach (var delivery in quoted.Distribution.Candidates
            .Where(candidate => candidate.Seller == seller))
            for (int i = 1; i < delivery.RouteTiles.Count; i++)
            {
                var x = delivery.RouteTiles[i - 1].FineStrategicCellId.Value;
                var y = delivery.RouteTiles[i].FineStrategicCellId.Value;
                if ((x == a && y == b) || (x == b && y == a))
                    crossingCargo = checked(crossingCargo +
                        policy.Cargo.CargoMicro(
                            delivery.Commodity, delivery.GoodsMicro));
            }
        Assert.True(crossingCargo > 0UL,
            "Real seller must actually cross the owned toll edge.");
        Assert.True(independent.ExternalTollsScaled > 0);
        Assert.True(independent.Accounts.Single(row =>
            row.Civilization == owner).TollIncomeScaled > 0);

        var host = new FoundationTacticalHumanAiTurnHost(
            scene.Session, scene.Incidence, new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            }, enableMonetaryInfrastructure: true,
            settlementPolicy: policy);
        var initialHash = host.StateHash;
        host.SetHumanReady(Red);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        var ev = Assert.IsType<FoundationEconomicSalesSettledEvent>(
            receipt.EconomicSettlement);
        Assert.Equal(1UL, ev.EconomicCycleOrdinal);
        Assert.Equal(ev.ProducedGoodsMicro, ev.SoldAndConsumedGoodsMicro);
        Assert.Equal(independent.ExternalTollsScaled, ev.ExternalTollsScaled);
        Assert.Equal(independent.GrossSalesScaled, ev.GrossSalesScaled);
        Assert.Equal(independent.PhysicalFreightScaled, ev.PhysicalFreightScaled);
        Assert.Equal(independent.RevenueTaxScaled, ev.SellerTaxesScaled);
        Assert.Equal(independent.NetSellerReceiptsScaled, ev.SellerNetScaled);
        Assert.True(ev.SellerTaxesScaled > 0);
        Assert.Equal(ev.GrossSalesScaled,
            ev.PhysicalFreightScaled + ev.CreditedToTreasuriesScaled);
        Assert.Equal(ev.GrossSalesScaled,
            ev.PhysicalFreightScaled + ev.ExternalTollsScaled +
            ev.SellerTaxesScaled + ev.SellerNetScaled);
        Assert.Equal(ev.ExternalTollsScaled,
            ev.Credits.Aggregate(BigInteger.Zero,
                (sum, credit) => sum + credit.OwnerTollIncomeScaled));
        foreach (var line in independent.Accounts)
        {
            var actual = ev.Credits.Single(credit =>
                credit.Civilization == line.Civilization);
            Assert.Equal(line.TollIncomeScaled, actual.OwnerTollIncomeScaled);
            Assert.Equal(line.RevenueTaxScaled, actual.SellerTaxScaled);
            Assert.Equal(line.SellerNetScaled, actual.SellerNetScaled);
            Assert.Equal(actual.CashCreditScaled,
                host.Snapshot.Treasury.Get(line.Civilization).CashScaled);
        }
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.NotEqual(initialHash, host.StateHash);
        Assert.Equal(1UL,
            host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal);
        Assert.Same(opening.Transport, host.Snapshot.Transport);

        var executor = new FoundationEconomicSalesSettlementEventExecutor(
            scene.Session.GeneratedWorld, scene.Incidence, policy);
        var replay = executor.Execute(opening, ev,
            new SimulationContext(ev.Id.Turn, new SimulationSeed(712UL)));
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(replay));
        var forged = new FoundationEconomicSalesSettledEvent(
            ev.Id, ev.SourceWorldHash, ev.ExpectedWorldRevision,
            ev.EconomicCycleOrdinal, ev.ProducedGoodsMicro,
            ev.SoldAndConsumedGoodsMicro, ev.GrossSalesScaled,
            ev.PhysicalFreightScaled, ev.SellerTaxesScaled,
            ev.ExternalTollsScaled + BigInteger.One,
            ev.SellerNetScaled, ev.CreditedToTreasuriesScaled,
            ev.CargoUnitCode, ev.Credits);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(opening, forged,
                new SimulationContext(ev.Id.Turn, new SimulationSeed(712UL))));

        var view = host.GetLastClosedEconomicTradeView(seller);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.AuthorizedSubset,
            view.Availability);
        Assert.Contains(view.UsedEdges, entry =>
            entry.Edge == id && entry.TotalCargoMicro >= crossingCargo);
    }
}
