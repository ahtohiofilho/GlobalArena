using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Pure, optional reconciliation of caller-ATTESTED shipment quantities
/// against specific profitable market allocation rows in the current turn.
/// Neither market allocation nor access observation manufactures a receipt.
/// No transport simulation, evidence authentication, delivery guarantee,
/// resident access, ingestion or persistent commodity balance is implied.
/// </summary>
public sealed class FoodTradeDeliveryReconciliationResolver
{
    public FoodTradeDeliveryReconciliationResult Resolve(
        EconomicMarketAllocationResult market,
        FoodMarketDemandCoverageResult foodCoverage,
        FoodCommodityCatalog catalog,
        FoodTradeDeliveryAttestationSnapshot declarations,
        TurnNumber currentTurn)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(foodCoverage);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(declarations);

        if (currentTurn.Value == 0UL || declarations.Turn != currentTurn)
            throw new InvalidOperationException(
                "Food delivery declarations must belong to the current market turn.");

        var foodDemandByDestination = new Dictionary<
            (EconomicPointId, CommodityId), FoodMarketDemandCoverageEntry>();

        foreach (var coverage in foodCoverage.Entries)
        {
            if (!catalog.Contains(coverage.CommodityId))
                throw new InvalidOperationException(
                    $"Food coverage contains an unregistered food commodity {coverage.CommodityId.Value}.");

            foodDemandByDestination.Add(
                (coverage.EconomicPointId, coverage.CommodityId), coverage);
        }

        var foodMarket = new Dictionary<
            FoodTradeAllocationIdentity, EconomicTradeAllocation>();

        var actualAllocations = new Dictionary<
            (EconomicPointId, CommodityId), UInt128>();

        foreach (var trade in market.Allocations)
        {
            if (!catalog.Contains(trade.CommodityId))
                continue;

            var destination = (trade.DestinationEconomicPointId, trade.CommodityId);
            if (!foodDemandByDestination.TryGetValue(destination, out var coverage))
                throw new InvalidOperationException(
                    "Registered food market allocation does not correspond to food demand coverage.");

            var prior = actualAllocations.TryGetValue(destination, out var accumulated)
                ? accumulated
                : 0U;

            var next = prior + trade.RawQuantity;
            if (next > coverage.RawTradeAllocatedQuantity)
                throw new InvalidOperationException(
                    "Actual registered food trade allocations exceed the reported market coverage.");

            actualAllocations[destination] = next;

            if (!foodMarket.TryAdd(FoodTradeAllocationIdentity.From(trade), trade))
                throw new InvalidOperationException(
                    "Duplicated allocation identity in the market result.");
        }

        foreach (var (destination, coverage) in foodDemandByDestination)
        {
            var raw = actualAllocations.TryGetValue(destination, out var quantity)
                ? quantity
                : 0U;
            if (raw != coverage.RawTradeAllocatedQuantity)
                throw new InvalidOperationException(
                    "Food market coverage differs from the actual selected market allocations.");
        }

        var attestations = new Dictionary<
            FoodTradeAllocationIdentity, FoodTradeDeliveryAttestation>();

        foreach (var declaration in declarations.Attestations)
        {
            var reference = declaration.ReferencedAllocation;

            if (!catalog.Contains(reference.CommodityId))
                throw new InvalidOperationException(
                    "A food delivery attestation references an unregistered or nonfood commodity.");

            var key = FoodTradeAllocationIdentity.From(reference);
            if (!foodMarket.TryGetValue(key, out var chosen))
                throw new InvalidOperationException(
                    "A delivery declaration references a trade not allocated by the market.");

            // Rate terms identify the trade; exact quantity prevents accepting
            // a forged or obsolete allocation with the same price and route.
            if (reference.RawQuantity != chosen.RawQuantity)
                throw new InvalidOperationException(
                    "A delivery declaration references a different quantity than the market selected.");

            if (declaration.RawAttestedDeliveredQuantity > chosen.RawQuantity)
                throw new InvalidOperationException(
                    "Reported food delivery exceeds the selected market allocation.");

            if (!attestations.TryAdd(key, declaration))
                throw new InvalidOperationException(
                    "Duplicated delivery declaration identity.");
        }

        return new FoodTradeDeliveryReconciliationResult(
            foodMarket.Select(kv =>
            {
                attestations.TryGetValue(kv.Key, out var attestation);
                return new FoodTradeDeliveryReconciliationEntry(
                    kv.Value, attestation);
            }));
    }
}
