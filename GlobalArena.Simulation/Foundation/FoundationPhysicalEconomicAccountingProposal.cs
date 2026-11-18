using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit post-revenue tax policy for ONE existing civilization. 10,000
/// basis points = 100%. No hardcoded rates, synthetic treasury, wallet
/// balance or silent zero-rate defaults are assumed.
/// </summary>
public sealed record FoundationPhysicalEconomicTaxRate(
    CivilizationId Civilization, uint GrossRevenueBasisPoints);

/// <summary>
/// ONE proposed transaction after fixed-freight distribution, showing
/// price * delivered quantity, physical freight, third-party toll and tax.
/// All money amounts are in the SAME exact rational domain with denominator
/// MoneyScale = MicroGoods * MicroMoney * TaxBasisPoints. No per-route
/// premature rounding, currency balances or fictional buyer wallets.
/// </summary>
public sealed record FoundationPhysicalEconomicAccountingLine(
    ulong DeliveryOrdinal,
    CivilizationId Seller,
    CivilizationId Buyer,
    PhysicalTacticalTileId SourceTile,
    PhysicalTacticalTileId BuyerTile,
    CommodityId Commodity,
    ulong GoodsMicro,
    ulong MarketPriceMicroPerWholeGood,
    BigInteger GrossScaled,
    BigInteger PhysicalFreightScaled,
    BigInteger ExternalTollsScaled,
    BigInteger SellerTaxScaled,
    BigInteger SellerNetScaled);

/// <summary>
/// Per-civilization reconciliation of a complete hypothetical economic
/// distribution. Taxes and owner tolls are computed, but are NOT yet
/// committed to authoritative treasury balances or WorldState journals.
/// </summary>
public sealed record FoundationPhysicalEconomicAccountTotal(
    CivilizationId Civilization,
    BigInteger SalesGrossScaled,
    BigInteger PhysicalFreightScaled,
    BigInteger ExternalTollsPaidScaled,
    BigInteger TollIncomeScaled,
    BigInteger RevenueTaxScaled,
    BigInteger SellerNetScaled);

/// <summary>
/// PRE-COMMIT financial evidence only. A complete policy, authoritative
/// infrastructure source, economic journal and atomic financial settlement
/// are still required before gameplay can publish these numbers as sales.
/// </summary>
public sealed class FoundationPhysicalEconomicAccountingProposal
{
    public const ulong CommodityMicro = 1_000_000UL;
    public const uint BasisPoints = 10_000U;
    public static readonly BigInteger MoneyScale =
        new BigInteger(CommodityMicro) * CommodityMicro * BasisPoints;

    public IReadOnlyList<FoundationPhysicalEconomicAccountingLine> Lines { get; }
    public IReadOnlyList<FoundationPhysicalEconomicAccountTotal> Accounts { get; }
    public BigInteger GrossSalesScaled { get; }
    public BigInteger PhysicalFreightScaled { get; }
    public BigInteger ExternalTollsScaled { get; }
    public BigInteger RevenueTaxScaled { get; }
    public BigInteger NetSellerReceiptsScaled { get; }
    public bool IsCommitted => false;

    internal FoundationPhysicalEconomicAccountingProposal(
        FoundationPhysicalEconomicAccountingLine[] lines,
        FoundationPhysicalEconomicAccountTotal[] accounts)
    {
        Lines = Array.AsReadOnly(lines);
        Accounts = Array.AsReadOnly(accounts);
        GrossSalesScaled = lines.Aggregate(BigInteger.Zero,
            (s, line) => s + line.GrossScaled);
        PhysicalFreightScaled = lines.Aggregate(BigInteger.Zero,
            (s, line) => s + line.PhysicalFreightScaled);
        ExternalTollsScaled = lines.Aggregate(BigInteger.Zero,
            (s, line) => s + line.ExternalTollsScaled);
        RevenueTaxScaled = lines.Aggregate(BigInteger.Zero,
            (s, line) => s + line.SellerTaxScaled);
        NetSellerReceiptsScaled = lines.Aggregate(BigInteger.Zero,
            (s, line) => s + line.SellerNetScaled);
    }
}

/// <summary>
/// Strict independent pre-commit reconciler. Does not change WorldState,
/// publish a trade, generate revenue, or certify a market equilibrium.
/// Applies the COMMON p=sqrt(D/Q) curve using only real worker-backed demand
/// and the actual quantity allocated. All freight rates are frozen from
/// the already-built turn plan; prior-cycle congestion is not recalculated.
///
/// Workers of distinct civilizations at one tile share ONE physical market.
/// Buyer ownership is retained for provenance, never to partition price D/Q.
/// </summary>
public static class FoundationPhysicalEconomicAccountingReconciler
{
    public static FoundationPhysicalEconomicAccountingProposal EvaluateCandidate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy cargoPolicy,
        FoundationPhysicalLaggedFreightCyclePlan candidate,
        IEnumerable<FoundationPhysicalEconomicTaxRate> taxRates)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(cargoPolicy);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(taxRates);
        if (candidate.RepricedWithinCycle ||
            candidate.NextCycleQuotes.HasSettledProvenance)
            throw new InvalidOperationException(
                "Pre-commit accounting requires one fixed-freight planning " +
                "candidate, never a settled or intra-turn repriced cycle.");

        // Do not introduce a default tax rate. Every real civilization
        // needs one explicit post-revenue rule, including zero rates.
        var civs = census.Workers.Select(w => w.Owner).Distinct()
            .OrderBy(x => x.Value).ToArray();
        var rates = new Dictionary<CivilizationId, uint>();
        foreach (var rate in taxRates)
        {
            if (rate is null || !rate.Civilization.IsValid ||
                rate.GrossRevenueBasisPoints > 10_000U ||
                !rates.TryAdd(rate.Civilization,
                    rate.GrossRevenueBasisPoints))
                throw new ArgumentException(
                    "Tax rules require unique, real civilization identities " +
                    "and rates between zero and 10000 basis points.",
                    nameof(taxRates));
        }
        if (rates.Count != civs.Length || civs.Any(c => !rates.ContainsKey(c)))
            throw new ArgumentException(
                "A complete explicit tax policy is needed for every " +
                "civilization in the real workforce.", nameof(taxRates));

        // The source price is the *universal* normalized curve, and the
        // worker's activity still produces demand at its real local tile.
        var market = FoundationPhysicalCommodityDemandProjector
            .Project(census, demandPolicy);
        if (!market.Commodities.SequenceEqual(cargoPolicy.Commodities))
            throw new InvalidOperationException(
                "Tax accounting may not change the physical commodity catalog.");
        // Multiple real owners at one location share ONE price and Q.
        // Independent audit: one and only one physical source, destination,
        // cargo conversion, traversable edge, owner, and quantity for each
        // real delivered lot. Candidate audit alone is not proof.
        var edges = candidate.FixedCycleEdges.ToArray();
        var (_, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, edges);
        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, cargoPolicy,
            candidate.Distribution.Candidates, ownership);
        var original = candidate.Distribution.AllocationAudit;
        if (audit.TotalAllocatedGoodsMicro != original.TotalAllocatedGoodsMicro ||
            audit.TotalAvailableGoodsMicro != original.TotalAvailableGoodsMicro ||
            audit.DeliveryCount != original.DeliveryCount ||
            audit.Freight.TotalEdgeTraversalCargoMicro !=
                original.Freight.TotalEdgeTraversalCargoMicro ||
            !audit.Freight.UsedEdges.Select(x => (x.Edge, x.TotalCargoMicro))
                .SequenceEqual(original.Freight.UsedEdges
                    .Select(x => (x.Edge, x.TotalCargoMicro))))
            throw new InvalidOperationException(
                "Independent physical re-audit differs from the candidate.");

        var totals = candidate.Distribution.Candidates
            .GroupBy(x => (x.BuyerTile, x.Buyer, x.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL,
                (sum, x) => checked(sum + x.GoodsMicro)));
        var declaredTotals = candidate.Distribution.MarketTotals.ToDictionary(
            t => (t.Tile, t.Buyer, t.Commodity));
        if (declaredTotals.Count != market.DemandByCivilization.Count ||
            market.DemandByCivilization.Any(line =>
                !declaredTotals.TryGetValue(
                    (line.Tile, line.Owner, line.Commodity), out var t) ||
                t.DemandMicro != line.DemandMicro ||
                t.AllocatedGoodsMicro != totals.GetValueOrDefault(
                    (line.Tile, line.Owner, line.Commodity))))
            throw new InvalidOperationException(
                "Candidate marginal market quantities disagree with real " +
                "physical demand and actual allocated goods.");

        var markets = market.MarketDemand.ToDictionary(
            x => (x.Tile, x.Commodity), x => x.DemandMicro);
        var sharedTotals = candidate.Distribution.Candidates
            .GroupBy(x => (x.BuyerTile, x.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL,
                (sum, x) => checked(sum + x.GoodsMicro)));
        var prices = new Dictionary<
            (PhysicalTacticalTileId Tile, CommodityId Commodity), ulong>();
        foreach (var pair in sharedTotals)
        {
            if (pair.Value == 0UL ||
                !markets.TryGetValue(pair.Key, out var d) || d == 0UL)
                throw new InvalidOperationException(
                    "A positive real delivery requires positive shared " +
                    "physical demand and a real received quantity.");
            // Exact integer floor(sqrt(D/Q)*1e6), no floating point and
            // no arbitrary quote multipliers. Integer square root is stable
            // on every architecture and preserves the approved P0=1.
            BigInteger price = FoundationPhysicalMarginalIntegratedUtility
                .IntegerSqrt(new BigInteger(d) *
                    FoundationPhysicalEconomicAccountingProposal.CommodityMicro *
                    FoundationPhysicalEconomicAccountingProposal.CommodityMicro /
                    pair.Value);
            prices.Add(pair.Key, checked((ulong)price));
        }

        // The path is audited independently, then costed exactly once at
        // the FIXED prices used by this turn. No late congestion update.
        var topo = incidence.FineTopology.Edges.ToDictionary(
            e => (Math.Min(e.IncidentCellIds[0].Value,
                        e.IncidentCellIds[1].Value),
                  Math.Max(e.IncidentCellIds[0].Value,
                        e.IncidentCellIds[1].Value)),
            e => new PhysicalTacticalEdgeId(
                incidence.Refinement.FineParameters, e.Id));
        var roads = edges.ToDictionary(e => e.Edge);
        var rows = new List<FoundationPhysicalEconomicAccountingLine>();
        var tariffIncome = civs.ToDictionary(c => c, _ => BigInteger.Zero);
        foreach (var delivery in candidate.Distribution.Candidates
            .OrderBy(x => x.DeliveryOrdinal))
        {
            var key = (delivery.BuyerTile, delivery.Commodity);
            ulong price = prices[key];
            ulong cargo = cargoPolicy.CargoMicro(
                delivery.Commodity, delivery.GoodsMicro);
            BigInteger physicalRates = BigInteger.Zero;
            BigInteger tollRates = BigInteger.Zero;
            var path = delivery.RouteTiles;
            for (int i = 1; i < path.Count; i++)
            {
                ulong a = path[i - 1].FineStrategicCellId.Value;
                ulong b = path[i].FineStrategicCellId.Value;
                if (!topo.TryGetValue((Math.Min(a, b), Math.Max(a, b)),
                        out var crossing) ||
                    !roads.TryGetValue(crossing, out var road))
                    throw new InvalidOperationException(
                        "Accounting cannot use an unknown physical route.");
                physicalRates += road.PhysicalCostMicroPerWholeCargoUnit;
                if (road.InfrastructureOwner is { } owner &&
                    owner != delivery.Seller)
                {
                    tollRates += road.ExternalTariffMicroPerWholeCargoUnit;
                    if (!tariffIncome.ContainsKey(owner))
                        throw new InvalidOperationException(
                            "Infrastructure owner is outside the existing " +
                            "civilization roster; no fictitious recipient.");
                    tariffIncome[owner] +=
                        new BigInteger(cargo) *
                        road.ExternalTariffMicroPerWholeCargoUnit *
                        FoundationPhysicalEconomicAccountingProposal.BasisPoints;
                }
            }
            BigInteger gross = new BigInteger(delivery.GoodsMicro) *
                price * FoundationPhysicalEconomicAccountingProposal.BasisPoints;
            BigInteger freight = new BigInteger(cargo) * physicalRates *
                FoundationPhysicalEconomicAccountingProposal.BasisPoints;
            BigInteger toll = new BigInteger(cargo) * tollRates *
                FoundationPhysicalEconomicAccountingProposal.BasisPoints;
            BigInteger tax = new BigInteger(delivery.GoodsMicro) * price *
                rates[delivery.Seller];
            BigInteger sellerNet = gross - freight - toll - tax;
            if (sellerNet < 0)
                throw new InvalidOperationException(
                    "Candidate would force a negative-return sale; a real " +
                    "economic settlement cannot certify this allocation.");
            rows.Add(new FoundationPhysicalEconomicAccountingLine(
                delivery.DeliveryOrdinal, delivery.Seller, delivery.Buyer,
                delivery.SourceTile, delivery.BuyerTile, delivery.Commodity,
                delivery.GoodsMicro, price, gross, freight, toll, tax,
                sellerNet));
        }

        var byOwner = civs.Select(civ =>
        {
            var own = rows.Where(r => r.Seller == civ).ToArray();
            return new FoundationPhysicalEconomicAccountTotal(
                civ,
                own.Aggregate(BigInteger.Zero, (s, r) => s + r.GrossScaled),
                own.Aggregate(BigInteger.Zero, (s, r) =>
                    s + r.PhysicalFreightScaled),
                own.Aggregate(BigInteger.Zero, (s, r) =>
                    s + r.ExternalTollsScaled),
                tariffIncome[civ],
                own.Aggregate(BigInteger.Zero, (s, r) => s + r.SellerTaxScaled),
                own.Aggregate(BigInteger.Zero, (s, r) => s + r.SellerNetScaled));
        }).ToArray();

        var result = new FoundationPhysicalEconomicAccountingProposal(
            rows.ToArray(), byOwner);
        if (result.GrossSalesScaled != result.PhysicalFreightScaled +
                result.ExternalTollsScaled + result.RevenueTaxScaled +
                result.NetSellerReceiptsScaled ||
            result.ExternalTollsScaled != byOwner.Aggregate(
                BigInteger.Zero, (sum, total) => sum + total.TollIncomeScaled))
            throw new InvalidOperationException(
                "Exact financial reconciliation failed: seller, owner " +
                "and government accounts must balance without rounding.");
        return result;
    }
}
