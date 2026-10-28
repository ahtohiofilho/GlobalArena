using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Independent, read-only audit of the existing opt-in congestion-market
/// candidate. Rebuilds supply/demand conservation and shared physical-edge
/// loads and costs from declared inputs. It never commits a WorldState.
/// This is NOT a certificate of endogenous-price clearing, route reachability,
/// individual participation, global optimality or cash settlement.
/// </summary>
public sealed class EconomicCongestionCandidateAuditor
{
    public void Audit(
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<FoodStrategicEdgeCongestionProfile> profiles,
        EconomicCongestionMarketResult candidate)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(candidate);

        var supply = flows.SupplyFlows.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId, row.Activity),
            row => (BigInteger)row.RawQuantity);
        var demand = flows.DemandFlows.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId),
            row => (BigInteger)row.RawQuantity);
        var remainingSupply = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), BigInteger>(supply);
        var remainingDemand = new Dictionary<
            (EconomicPointId, CommodityId), BigInteger>(demand);

        var declaredProfiles = new Dictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null || !declaredProfiles.TryAdd(profile.StrategicEdgeId, profile))
                throw new InvalidOperationException("Missing or duplicated declared edge cost profile.");
        }

        var recomputedLoads = new Dictionary<StrategicEdgeId, BigInteger>();
        var sumIncrementalCost = Zero();
        foreach (var selection in candidate.Selections)
        {
            var source = (selection.SourceEconomicPointId, selection.CommodityId, selection.SourceActivity);
            var destination = (selection.DestinationEconomicPointId, selection.CommodityId);
            var quantity = (BigInteger)selection.RawQuantity;
            if (!remainingSupply.TryGetValue(source, out var sourceLeft) ||
                !remainingDemand.TryGetValue(destination, out var destinationLeft) ||
                sourceLeft < quantity || destinationLeft < quantity)
                throw new InvalidOperationException("Candidate exceeds declared supply/demand or uses an unknown lane.");
            remainingSupply[source] = sourceLeft - quantity;
            remainingDemand[destination] = destinationLeft - quantity;

            if (!catalog.TryGet(selection.CommodityId, out var food) || food is null)
                throw new InvalidOperationException("Candidate uses an unknown physical commodity mass.");
            var micrograms = quantity * food.GramsPerFlowUnit;
            // Recompute each tranche's exact charge from the resource loads
            // BEFORE this decision, not merely the grand total at the end.
            // Matching aggregate cost is insufficient: a shifted charge can
            // make an individual transaction look artificially profitable.
            var expectedIncrement = Zero();
            foreach (var edge in selection.RoutedEdgePath)
            {
                if (!declaredProfiles.TryGetValue(edge, out var profile))
                    throw new InvalidOperationException("Candidate uses an edge with no declared real-cost profile.");

                var previousLoad = recomputedLoads.GetValueOrDefault(edge, BigInteger.Zero);
                var nextLoad = previousLoad + micrograms;
                var beforeCost = profile.EvaluateTotalCost(previousLoad);
                var afterCost = profile.EvaluateTotalCost(nextLoad);
                expectedIncrement = Add(expectedIncrement, SubtractNonnegative(afterCost, beforeCost));
                recomputedLoads[edge] = nextLoad;
            }
            if (selection.RawAdditionalCongestionCost.CompareTo(expectedIncrement) != 0)
                throw new InvalidOperationException(
                    "Individual tranche reports an incorrect shared-resource cost increment.");
            sumIncrementalCost = Add(sumIncrementalCost, expectedIncrement);
        }

        if (candidate.FinalCongestion.UsedEdges.Count != recomputedLoads.Count)
            throw new InvalidOperationException("Reported shared-edge set differs from selected routes.");
        var sumRealCost = Zero();
        foreach (var reported in candidate.FinalCongestion.UsedEdges)
        {
            if (!recomputedLoads.TryGetValue(reported.StrategicEdgeId, out var load) ||
                reported.LoadMicrograms != load)
                throw new InvalidOperationException("Reported shared-edge load disagrees with selected goods.");

            var profile = declaredProfiles[reported.StrategicEdgeId];
            var real = profile.EvaluateTotalCost(load);
            var marginal = profile.EvaluateMarginalCostPerGram(load);
            if (reported.TotalCost.CompareTo(real) != 0 ||
                reported.MarginalCostPerGram.CompareTo(marginal) != 0)
                throw new InvalidOperationException("Reported congestion cost disagrees with declared cost function.");
            sumRealCost = Add(sumRealCost, real);
        }

        // The existing sequential-marginal resolver prices each tranche by
        // the exact increment of the same integrated shared-resource cost.
        // This equality holds for that contract; it is NOT a generic identity
        // for institutional tariffs or turn-locked transport quotes.
        if (sumIncrementalCost.CompareTo(sumRealCost) != 0)
            throw new InvalidOperationException("Selected incremental freight cost does not reconcile to real resource cost.");
    }

    private static FoodTransportCostFraction Zero() =>
        new(BigInteger.Zero, BigInteger.One);

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static FoodTransportCostFraction SubtractNonnegative(
        FoodTransportCostFraction later, FoodTransportCostFraction earlier) =>
        new(later.Numerator * earlier.Denominator -
                earlier.Numerator * later.Denominator,
            later.Denominator * earlier.Denominator);
}
