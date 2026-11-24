using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationWorkerFoodIntake(PhysicalWorkerId Worker, CommodityId Commodity, ulong ConsumedMicro);

/// <summary>Read-only cycle observation. Actual market deliveries are consumption under
/// the closed-market game rule; individual shares are modeled, not measured.</summary>
public static class FoundationSettledFoodIntake
{
    internal static FoundationWorkerFoodIntake[] Attribute(WorldState opening,
        FoundationTemporalEconomicTurnInputs temporal, FoundationPhysicalGlobalMarginalCoordinationPlan plan,
        IReadOnlyCollection<CommodityId> foods)
    {
        var owners = opening.Workers.Workers.ToDictionary(w => w.Id, w => w.Owner);
        var presence = temporal.Slices.GroupBy(s => (Owner: owners[s.Worker], s.Node))
            .ToDictionary(g => g.Key, g => g.GroupBy(s => s.Worker)
                .Select(w => (Worker: w.Key, Time: w.Aggregate(0UL, (sum, s) => checked(sum + s.PresenceMicro))))
                .OrderBy(w => w.Worker.Value).ToArray());
        var foodSet = foods.ToHashSet();
        var totals = owners.Keys.ToDictionary(w => w, w => foods.ToDictionary(c => c, _ => 0UL));
        foreach (var delivery in plan.Candidates.Where(c => foodSet.Contains(c.Commodity))
            .GroupBy(c => (c.Buyer, c.BuyerTile, c.Commodity)))
        {
            ulong quantity = delivery.Aggregate(0UL, (sum, c) => checked(sum + c.GoodsMicro));
            if (!presence.TryGetValue((delivery.Key.Buyer, delivery.Key.BuyerTile), out var workers))
                throw new InvalidOperationException("Food delivery without actual buyer presence.");
            var shares = Split(quantity, workers.Select(w => (w.Worker, w.Time)).ToArray());
            foreach (var share in shares)
                totals[share.Worker][delivery.Key.Commodity] = checked(totals[share.Worker][delivery.Key.Commodity] + share.Quantity);
        }
        var rows = totals.OrderBy(w => w.Key.Value).SelectMany(w => w.Value.OrderBy(c => c.Key.Value)
            .Select(c => new FoundationWorkerFoodIntake(w.Key, c.Key, c.Value))).ToArray();
        foreach (var food in foods)
            if (rows.Where(r => r.Commodity == food).Aggregate(0UL, (sum, r) => checked(sum + r.ConsumedMicro)) !=
                temporal.Production.Where(p => p.Commodity == food).Aggregate(0UL, (sum, p) => checked(sum + p.ProducedMicro)))
                throw new InvalidOperationException("Individual food attribution did not conserve settled crop output.");
        return rows;
    }

    // Largest remainder per buyer/node/food, sorted stable worker identity for ties.
    public static IReadOnlyList<(PhysicalWorkerId Worker, ulong Quantity)> Split(ulong quantity,
        IReadOnlyList<(PhysicalWorkerId Worker, ulong Time)> workers)
    {
        if (workers.Count == 0 || workers.Any(w => !w.Worker.IsValid || w.Time == 0) ||
            workers.Select(w => w.Worker).Distinct().Count() != workers.Count)
            throw new ArgumentException("Food attribution requires unique workers with positive presence.");
        ulong time = workers.Aggregate(0UL, (sum, w) => checked(sum + w.Time));
        var shares = workers.Select(w => {
            UInt128 n = (UInt128)quantity * w.Time;
            return (w.Worker, Quantity: (ulong)(n / time), Remainder: n % time);
        }).OrderByDescending(w => w.Remainder).ThenBy(w => w.Worker.Value).ToArray();
        ulong left = quantity - shares.Aggregate(0UL, (sum, w) => checked(sum + w.Quantity));
        return Array.AsReadOnly(shares.Select((w, i) => (w.Worker, checked(w.Quantity + ((ulong)i < left ? 1UL : 0UL))))
            .OrderBy(w => w.Worker.Value).ToArray());
    }

    public static CanonicalNutritionCandidateResult Evaluate(PhysicalWorkerId worker,
        IReadOnlyList<FoundationWorkerFoodIntake> settled, IReadOnlyDictionary<CommodityId, double> conversions,
        CanonicalNutritionCandidateParameters parameters, Func<double, double>? sufficiency = null)
    {
        ArgumentNullException.ThrowIfNull(settled);
        ArgumentNullException.ThrowIfNull(conversions);
        var foods = settled.Where(r => r.Worker == worker).ToArray();
        if (!worker.IsValid || conversions.Count != 6 || foods.Length != 6 ||
            foods.Any(r => !conversions.ContainsKey(r.Commodity)))
            throw new ArgumentException("Nutrition requires six explicit settled foods and conversions.");
        return new CanonicalNutritionCandidateEvaluator().Evaluate(1.0,
            foods.Select(r => new CanonicalNutritionFoodIntake(r.Commodity, r.ConsumedMicro, conversions[r.Commodity])),
            parameters, sufficiency);
    }
}
