using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Domain-separated SHA-256 digests are reproducible content identifiers,
/// NOT signatures or evidence of the caller's authority.
/// </summary>
internal static class ExperimentalCashAuthorizationDigest
{
    public static string Hash(IEnumerable<string> fields)
    {
        var bytes = new StringBuilder("GA-VS47-CANONICAL-V1\n");
        foreach (var value in fields)
        {
            var encoded = Encoding.UTF8.GetBytes(value);
            bytes.Append(encoded.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(value).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(bytes.ToString())));
    }

    public static string Rational(FoodTransportCostFraction value) =>
        value.Numerator.ToString(CultureInfo.InvariantCulture) + "/" +
        value.Denominator.ToString(CultureInfo.InvariantCulture);

    public static string Signed(ExperimentalSignedEconomicValue value) =>
        value.Numerator.ToString(CultureInfo.InvariantCulture) + "/" +
        value.Denominator.ToString(CultureInfo.InvariantCulture);

    public static string Key(ExperimentalCashAuditAccountKey key) =>
        ((int)key.Role).ToString(CultureInfo.InvariantCulture) + ":" +
        key.Point.Value + ":" + key.Destination.Value + ":" + key.ServiceId;

    public static string Corridor(ExperimentalSharedFreightCorridorKey key) =>
        key.FineCrossing.ToString() + ":" +
        key.TechnologyTypeId + ":" + key.TechnologyCatalogVersion;
}

/// <summary>
/// One immutable collection of original economic inputs for an explicit
/// turn, world state hash and versioned physical network. The supplied
/// provenance labels must come from an external trusted snapshot producer:
/// this type does not introspect WorldState or authenticate those labels.
/// </summary>
public sealed class ExperimentalCashAuthorizationSnapshot
{
    private readonly EconomicSupplyFlow[] _supplies;
    private readonly EconomicDemandFlow[] _demands;
    private readonly FoodCommodityCatalog _catalog;
    private readonly ExperimentalSimultaneousFreightLane[] _lanes;
    private readonly ExperimentalBuyerCashBudget[] _budgets;
    private readonly ExperimentalBuyerCommodityPrice[] _prices;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;

    public ulong TurnOrdinal { get; }
    public ulong NetworkRevision { get; }
    public string WorldStateHash { get; }
    public string ContentDigest { get; }

    public ExperimentalCashAuthorizationSnapshot(
        ulong turnOrdinal,
        string worldStateHash,
        ulong networkRevision,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<ExperimentalSimultaneousFreightLane> lanes,
        IEnumerable<ExperimentalBuyerCashBudget> budgets,
        IEnumerable<ExperimentalBuyerCommodityPrice> prices,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        if (turnOrdinal == 0 || networkRevision == 0 ||
            string.IsNullOrWhiteSpace(worldStateHash))
            throw new ArgumentException(
                "Audited commercial authorization requires a turn, world hash and network revision.");
        ArgumentNullException.ThrowIfNull(flows);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(prices);
        _congestion = congestion ?? throw new ArgumentNullException(nameof(congestion));
        _supplies = flows.SupplyFlows.ToArray();
        _demands = flows.DemandFlows.ToArray();
        _lanes = lanes.ToArray();
        _budgets = budgets.ToArray();
        _prices = prices.ToArray();
        TurnOrdinal = turnOrdinal;
        WorldStateHash = worldStateHash;
        NetworkRevision = networkRevision;
        if (_lanes.Length == 0 || _lanes.Any(x => x is null) ||
            _budgets.Any(x => x is null) || _prices.Any(x => x is null))
            throw new ArgumentException("Original monetary and freight offers cannot contain nulls or be empty.");
        ContentDigest = BuildDigest();
    }

    public ExperimentalCashAuditResult Audit(
        ExperimentalSimultaneousFreightAllocation candidate) =>
        new ExperimentalIndependentCashSettlementAuditor().Audit(
            candidate,
            new EconomicProductionDemandResult(_supplies, _demands),
            _catalog, _lanes, _budgets, _prices, _congestion);

    public void VerifyCurrent(
        ulong currentTurn, string currentWorldHash, ulong currentNetworkRevision)
    {
        if (currentTurn != TurnOrdinal ||
            !string.Equals(currentWorldHash, WorldStateHash, StringComparison.Ordinal) ||
            currentNetworkRevision != NetworkRevision ||
            !string.Equals(BuildDigest(), ContentDigest, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "A candidate cannot be authorized for an altered economic or physical snapshot.");
    }

    private string BuildDigest()
    {
        var fields = new List<string>
        {
            "SNAPSHOT", TurnOrdinal.ToString(CultureInfo.InvariantCulture),
            WorldStateHash, NetworkRevision.ToString(CultureInfo.InvariantCulture),
            _congestion.SaturationWeight.ToString(CultureInfo.InvariantCulture),
            _congestion.SaturationScaleMicrograms.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(_supplies
            .Select(s => $"S:{s.EconomicPointId.Value}:{s.CommodityId.Value}:{(int)s.Activity}:{s.RawQuantity}")
            .OrderBy(x => x, StringComparer.Ordinal));
        fields.AddRange(_demands
            .Select(d => $"D:{d.EconomicPointId.Value}:{d.CommodityId.Value}:{d.RawQuantity}")
            .OrderBy(x => x, StringComparer.Ordinal));
        fields.AddRange(_catalog.Commodities
            .Select(c => $"M:{c.CommodityId.Value}:{c.GramsPerFlowUnit}")
            .OrderBy(x => x, StringComparer.Ordinal));
        fields.AddRange(_budgets
            .Select(b => $"B:{b.Buyer.Value}:{ExperimentalCashAuthorizationDigest.Rational(b.GrossCash)}:{ExperimentalCashAuthorizationDigest.Rational(b.ProtectedResidual)}")
            .OrderBy(x => x, StringComparer.Ordinal));
        fields.AddRange(_prices
            .Select(p => $"P:{p.Buyer.Value}:{p.Commodity.Value}:{ExperimentalCashAuthorizationDigest.Rational(p.PricePerWholeFlowUnit)}")
            .OrderBy(x => x, StringComparer.Ordinal));

        var routeLines = new List<string>();
        foreach (var lane in _lanes)
        {
            var prefix = $"L:{lane.Supply.EconomicPointId.Value}:{lane.Supply.CommodityId.Value}:{(int)lane.Supply.Activity}:{lane.Demand.EconomicPointId.Value}";
            foreach (var option in lane.Services)
            {
                var cost = option.Pricing;
                routeLines.Add(prefix + ":" + option.ServiceId + ":" +
                    cost.RulesetVersion + ":" +
                    cost.GeographicBaseNumerator.ToString(CultureInfo.InvariantCulture) + ":" +
                    cost.OverheadNumerator.ToString(CultureInfo.InvariantCulture) + ":" +
                    cost.OverheadSmoothingMicrograms.ToString(CultureInfo.InvariantCulture) + ":" +
                    cost.SaturationNumerator.ToString(CultureInfo.InvariantCulture) + ":" +
                    cost.SaturationScaleMicrograms.ToString(CultureInfo.InvariantCulture) + ":" +
                    string.Join("|", option.Corridors.Select(
                        ExperimentalCashAuthorizationDigest.Corridor)));
            }
        }
        fields.AddRange(routeLines.OrderBy(x => x, StringComparer.Ordinal));
        return ExperimentalCashAuthorizationDigest.Hash(fields);
    }

    internal string Fingerprint(
        ExperimentalSimultaneousFreightAllocation candidate,
        ExperimentalCashAuditResult audit)
    {
        var fields = new List<string>
        {
            "PROPOSAL", ContentDigest,
            ExperimentalCashAuthorizationDigest.Signed(candidate.NetEconomicValue),
            ExperimentalCashAuthorizationDigest.Rational(candidate.IntegratedBuyerValue),
            ExperimentalCashAuthorizationDigest.Rational(candidate.ProducerReservationCost),
            ExperimentalCashAuthorizationDigest.Rational(candidate.SharedFreightCost),
            ExperimentalCashAuthorizationDigest.Rational(audit.TotalOpening),
            ExperimentalCashAuthorizationDigest.Rational(audit.TotalClosing),
            ExperimentalCashAuthorizationDigest.Rational(audit.TotalCommodityPurchases),
            ExperimentalCashAuthorizationDigest.Rational(audit.TotalFreight)
        };
        fields.AddRange(candidate.Trades.Select(x =>
            $"T:{x.Source.Value}:{x.Destination.Value}:{x.Commodity.Value}:{x.ServiceId}:{x.RawQuantity}")
            .OrderBy(x => x, StringComparer.Ordinal));
        fields.AddRange(candidate.CorridorLoads.Select(x =>
            "Q:" + ExperimentalCashAuthorizationDigest.Corridor(x.Key) + ":" +
            x.Value.ToString(CultureInfo.InvariantCulture))
            .OrderBy(x => x, StringComparer.Ordinal));
        fields.AddRange(candidate.CashLedger.Select(x =>
            $"C:{x.Buyer.Value}:{ExperimentalCashAuthorizationDigest.Rational(x.GrossCash)}:" +
            ExperimentalCashAuthorizationDigest.Rational(x.ProtectedResidual) + ":" +
            ExperimentalCashAuthorizationDigest.Rational(x.CommodityCharges) + ":" +
            ExperimentalCashAuthorizationDigest.Rational(x.FreightCharges) + ":" +
            ExperimentalCashAuthorizationDigest.Rational(x.TotalCommitment) + ":" +
            ExperimentalCashAuthorizationDigest.Rational(x.RemainingSpendableCash))
            .OrderBy(x => x, StringComparer.Ordinal));
        fields.AddRange(audit.Transfers.Select(x =>
            $"J:{ExperimentalCashAuthorizationDigest.Key(x.Debit)}:" +
            ExperimentalCashAuthorizationDigest.Key(x.Credit) + ":" +
            (int)x.Kind + ":" + x.Commodity.Value + ":" + x.RawQuantity + ":" +
            ExperimentalCashAuthorizationDigest.Rational(x.Amount))
            .OrderBy(x => x, StringComparer.Ordinal));
        return ExperimentalCashAuthorizationDigest.Hash(fields);
    }
}
