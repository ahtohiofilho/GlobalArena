using System.Numerics;

namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// F-084F/F-071: pure financial projection of already-cleared transient
/// sale decompositions. This is neither a routing solver nor a journal.
/// All credits are positive inflows from the implicit outside consumer
/// sector, reconciled EXACTLY across sellers and third-party edge owners.
/// The F-073 reference weight remains a LAB HYPOTHESIS, not ratified law.
/// </summary>
public static class DestinationRevenueLaboratory
{
    public const long Scale = 1_000_000;

    /// <summary>
    /// Quotes F-043 P=sqrt(D/Q), independently for EACH destination+product.
    /// When Q=0, price is unavailable and gross is 0. For Q,D>0,
    /// price is floored to millionths and R=floor(Q*P/Scale).
    /// A precision-underflowing positive price fails explicitly rather
    /// than inventing an economically free sale. No floating point.
    /// </summary>
    public static DestinationMarketQuote Quote(DestinationMarketInput market)
    {
        if (market.DestinationNodeId < 0 || market.CommodityId < 0 ||
            market.TotalSoldQuantityRaw < 0 || market.DemandRaw < 0)
            throw new ArgumentOutOfRangeException(nameof(market));

        if (market.TotalSoldQuantityRaw == 0)
            return new DestinationMarketQuote(market.DestinationNodeId, market.CommodityId,
                0, market.DemandRaw, 0, 0);

        if (market.DemandRaw == 0)
            throw new ArgumentException("Positive sale without positive demand is not defined by F-043.",
                nameof(market));

        var numerator = (BigInteger)market.DemandRaw * Scale * Scale;
        var squaredPriceRaw = numerator / market.TotalSoldQuantityRaw;
        var rawPrice = IntegerSquareRoot(squaredPriceRaw);
        if (rawPrice == 0)
            throw new InvalidOperationException("Positive market price underflows fixed-point precision.");

        long price = checked((long)rawPrice);
        long gross = checked((long)((BigInteger)market.TotalSoldQuantityRaw * price / Scale));
        return new DestinationMarketQuote(market.DestinationNodeId, market.CommodityId,
            market.TotalSoldQuantityRaw, market.DemandRaw, price, gross);
    }

    public static DestinationRevenueProjection Settle(
        IReadOnlyList<DestinationMarketInput> marketInputs,
        IReadOnlyList<TransientDestinationSale> saleInputs)
    {
        ArgumentNullException.ThrowIfNull(marketInputs);
        ArgumentNullException.ThrowIfNull(saleInputs);

        // Stable sort and stable identities remove input-enumeration priority.
        var markets = marketInputs.Select(Quote)
            .OrderBy(x => x.DestinationNodeId)
            .ThenBy(x => x.CommodityId).ToArray();
        var marketKeys = new HashSet<(long, long)>();
        foreach (var market in markets)
        {
            if (!marketKeys.Add((market.DestinationNodeId, market.CommodityId)))
                throw new ArgumentException("Duplicate destination-product market.", nameof(marketInputs));
        }

        var sales = saleInputs.OrderBy(x => x?.FlowId ?? -1).ToArray();
        var flowIds = new HashSet<long>();
        var snapshots = new Dictionary<long, EdgeSnapshot>();

        foreach (var sale in sales)
        {
            if (sale is null || sale.FlowId < 0 || sale.OriginNodeId < 0 ||
                sale.DestinationNodeId < 0 || sale.CommodityId < 0 ||
                sale.SellerCivilizationId < 0 || sale.SoldQuantityRaw <= 0)
                throw new ArgumentException("Invalid transient sale identity or positive quantity.",
                    nameof(saleInputs));
            if (!flowIds.Add(sale.FlowId))
                throw new ArgumentException("Duplicate flow identity: projection would double count.",
                    nameof(saleInputs));
            if (!marketKeys.Contains((sale.DestinationNodeId, sale.CommodityId)))
                throw new ArgumentException("Sale has no matching destination-product market.",
                    nameof(saleInputs));
            if (sale.UsedEdges is null)
                throw new ArgumentException("Sale requires an explicit (possibly empty) edge usage.",
                    nameof(saleInputs));
            if ((sale.OriginNodeId == sale.DestinationNodeId) != (sale.UsedEdges.Count == 0))
                throw new ArgumentException(
                    "Local sale must use zero edges; non-local sale must have attributed edges.",
                    nameof(saleInputs));

            var used = new HashSet<long>();
            foreach (var edge in sale.UsedEdges)
            {
                if (edge is null || edge.StableEdgeId < 0 || edge.Holdings is null)
                    throw new ArgumentException("Invalid transient edge.", nameof(saleInputs));
                if (!used.Add(edge.StableEdgeId))
                    throw new ArgumentException("Same sale cannot pay the same edge twice.",
                        nameof(saleInputs));

                var holdings = CanonicalHoldings(edge);
                if (snapshots.TryGetValue(edge.StableEdgeId, out var previous))
                {
                    if (previous.Tariff != edge.Tariff ||
                        !previous.Holdings.AsSpan().SequenceEqual(holdings))
                        throw new ArgumentException(
                            "Conflicting tariff/ownership snapshots for one edge in a batch.",
                            nameof(saleInputs));
                }
                else
                    snapshots.Add(edge.StableEdgeId, new EdgeSnapshot(edge.Tariff, holdings));
            }
        }

        var perSale = new List<TransientSaleRevenue>(sales.Length);
        var credits = new SortedDictionary<long, long>();
        long totalGross = 0;

        foreach (var market in markets)
        {
            var subset = sales.Where(x =>
                x.DestinationNodeId == market.DestinationNodeId &&
                x.CommodityId == market.CommodityId).ToArray();

            long totalQuantity = 0;
            foreach (var sale in subset)
                totalQuantity = checked(totalQuantity + sale.SoldQuantityRaw);
            if (totalQuantity != market.TotalSoldQuantityRaw)
                throw new ArgumentException(
                    "All sold quantity at every destination-product must be attributed exactly.",
                    nameof(saleInputs));

            var allocatedGross = ApportionMarketGross(market.GrossRevenueRaw,
                market.TotalSoldQuantityRaw, subset);
            for (int i = 0; i < subset.Length; i++)
            {
                var settled = SettleSingleFlow(subset[i], market.PriceRaw, allocatedGross[i], snapshots);
                perSale.Add(settled);
                totalGross = checked(totalGross + settled.GrossRevenueRaw);
                AddCredit(credits, settled.SellerCivilizationId, settled.SellerNetRaw);
                foreach (var owner in settled.ExternalOwnerCredits)
                    AddCredit(credits, owner.OwnerCivilizationId, owner.CreditRaw);
            }
        }

        var finalCredits = credits.Select(x => new CivilizationRevenueCredit(x.Key, x.Value)).ToArray();
        long totalCredits = 0;
        foreach (var credit in finalCredits)
            totalCredits = checked(totalCredits + credit.CreditRaw);
        if (totalCredits != totalGross)
            throw new InvalidOperationException("F-060 financial conservation failure.");

        return new DestinationRevenueProjection(
            markets,
            perSale.OrderBy(x => x.FlowId).ToArray(),
            finalCredits,
            totalGross,
            totalCredits);
    }

    private static EdgeCapitalHolding[] CanonicalHoldings(TransientEdgeUse edge)
    {
        var holdings = edge.Holdings.OrderBy(x => x.CivilizationId).ToArray();
        long total = 0;
        long previous = -1;
        foreach (var holding in holdings)
        {
            if (holding.CivilizationId < 0 || holding.CapitalRawInvalid())
                throw new ArgumentException("Invalid owner civilization or incorporated capital.");
            if (holding.CivilizationId == previous)
                throw new ArgumentException("Repeated owner on one edge.");
            previous = holding.CivilizationId;
            total = checked(total + holding.IncorporatedCapitalRaw);
        }

        if (edge.Tariff.WeightRaw < 0 || edge.Tariff.IncorporatedCapitalRaw < 0 ||
            total != edge.Tariff.IncorporatedCapitalRaw ||
            (total == 0 && edge.Tariff.WeightRaw != 0))
            throw new ArgumentException("Ownership must reconcile with local tariff capital.");

        return holdings;
    }

    private static long[] ApportionMarketGross(long gross, long quantity,
        IReadOnlyList<TransientDestinationSale> sales)
    {
        var result = new long[sales.Count];
        if (quantity == 0)
            return result;

        var remainders = new BigInteger[sales.Count];
        long baseSum = 0;
        for (int i = 0; i < sales.Count; i++)
        {
            BigInteger numerator = (BigInteger)gross * sales[i].SoldQuantityRaw;
            result[i] = checked((long)(numerator / quantity));
            remainders[i] = numerator % quantity;
            baseSum = checked(baseSum + result[i]);
        }

        long residual = gross - baseSum;
        var rank = Enumerable.Range(0, sales.Count)
            .OrderByDescending(i => remainders[i])
            .ThenBy(i => sales[i].FlowId).ToArray();

        if (residual < 0 || residual > rank.Length)
            throw new InvalidOperationException("Gross value apportionment inconsistency.");
        for (int i = 0; i < residual; i++)
            result[rank[i]] = checked(result[rank[i]] + 1);
        return result;
    }

    private static TransientSaleRevenue SettleSingleFlow(
        TransientDestinationSale sale, long priceRaw, long grossRaw,
        IReadOnlyDictionary<long, EdgeSnapshot> snapshots)
    {
        var shares = new List<RevenueShare> {
            new(-1, sale.SellerCivilizationId, Scale)
        };
        long totalExternalWeight = 0;

        foreach (var edge in sale.UsedEdges.OrderBy(e => e.StableEdgeId))
        {
            var snapshot = snapshots[edge.StableEdgeId];
            var reference = snapshot.Tariff;
            var holdings = snapshot.Holdings;
            long totalCapital = reference.IncorporatedCapitalRaw;
            if (totalCapital == 0 || reference.WeightRaw == 0)
                continue;

            long ownedBySeller = 0;
            foreach (var holding in holdings)
                if (holding.CivilizationId == sale.SellerCivilizationId)
                    ownedBySeller = holding.IncorporatedCapitalRaw;

            long payable = checked((long)((BigInteger)reference.WeightRaw *
                (totalCapital - ownedBySeller) / totalCapital));
            if (payable == 0)
                continue;

            var apportioned = new List<(long Owner, long Base, BigInteger Rem)>();
            long baseTotal = 0;
            foreach (var holding in holdings)
            {
                if (holding.CivilizationId == sale.SellerCivilizationId ||
                    holding.IncorporatedCapitalRaw == 0)
                    continue;
                BigInteger weighted = (BigInteger)reference.WeightRaw *
                    holding.IncorporatedCapitalRaw;
                long part = checked((long)(weighted / totalCapital));
                apportioned.Add((holding.CivilizationId, part, weighted % totalCapital));
                baseTotal = checked(baseTotal + part);
            }

            long remaining = payable - baseTotal;
            if (remaining < 0 || remaining > apportioned.Count)
                throw new InvalidOperationException("External owner-weight apportionment failed.");

            var ranked = apportioned.OrderByDescending(x => x.Rem)
                .ThenBy(x => x.Owner).Select(x => x.Owner).Take((int)remaining).ToHashSet();

            foreach (var part in apportioned.OrderBy(x => x.Owner))
            {
                long weight = checked(part.Base + (ranked.Contains(part.Owner) ? 1 : 0));
                if (weight > 0)
                    shares.Add(new RevenueShare(edge.StableEdgeId, part.Owner, weight));
            }
            totalExternalWeight = checked(totalExternalWeight + payable);
        }

        long weightCheck = 0;
        foreach (var s in shares.Skip(1))
            weightCheck = checked(weightCheck + s.WeightRaw);
        if (weightCheck != totalExternalWeight)
            throw new InvalidOperationException("Edge coownership weights do not reconcile.");

        var denominator = (BigInteger)Scale + totalExternalWeight;
        var amounts = new long[shares.Count];
        var remainders = new BigInteger[shares.Count];
        long baseSum = 0;
        for (int i = 0; i < shares.Count; i++)
        {
            var weighted = (BigInteger)grossRaw * shares[i].WeightRaw;
            amounts[i] = checked((long)(weighted / denominator));
            remainders[i] = weighted % denominator;
            baseSum = checked(baseSum + amounts[i]);
        }

        long residual = grossRaw - baseSum;
        var residualRank = Enumerable.Range(0, shares.Count)
            .OrderByDescending(i => remainders[i])
            .ThenBy(i => shares[i].EdgeId)
            .ThenBy(i => shares[i].CivilizationId)
            .ToArray();

        if (residual < 0 || residual > residualRank.Length)
            throw new InvalidOperationException("Sale revenue split cannot be reconciled.");
        for (int i = 0; i < residual; i++)
            amounts[residualRank[i]] = checked(amounts[residualRank[i]] + 1);

        var ownerCredits = new List<TariffOwnerCredit>();
        long checkedSum = amounts[0];
        for (int i = 1; i < shares.Count; i++)
        {
            checkedSum = checked(checkedSum + amounts[i]);
            if (amounts[i] > 0)
                ownerCredits.Add(new TariffOwnerCredit(
                    shares[i].EdgeId, shares[i].CivilizationId, amounts[i]));
        }

        if (checkedSum != grossRaw)
            throw new InvalidOperationException("Seller + external owners must equal gross.");
        return new TransientSaleRevenue(
            sale.FlowId, sale.OriginNodeId, sale.DestinationNodeId,
            sale.CommodityId, sale.SellerCivilizationId, sale.SoldQuantityRaw,
            priceRaw, grossRaw, totalExternalWeight, amounts[0], ownerCredits);
    }

    private static void AddCredit(SortedDictionary<long, long> credits, long id, long value)
    {
        if (value == 0) return;
        credits.TryGetValue(id, out long current);
        credits[id] = checked(current + value);
    }

    private static BigInteger IntegerSquareRoot(BigInteger n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n < 2) return n;

        BigInteger x = n;
        BigInteger y = (x + 1) >> 1;
        while (y < x)
        {
            x = y;
            y = (x + n / x) >> 1;
        }
        return x;
    }

    private readonly record struct EdgeSnapshot(
        EdgeTariffReference Tariff,
        EdgeCapitalHolding[] Holdings);
    private readonly record struct RevenueShare(
        long EdgeId, long CivilizationId, long WeightRaw);
}

internal static class CapitalValidationExtensions
{
    public static bool CapitalRawInvalid(this EdgeCapitalHolding owner)
        => owner.IncorporatedCapitalRaw < 0;
}
