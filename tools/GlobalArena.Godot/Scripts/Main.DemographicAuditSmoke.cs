using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    // Explicit QA only. Reuse the exact signed opening, verified time slices
    // and public pure birth planner. No births from counterfactual samples
    // are committed or made visible in interactive gameplay.
    private void AuditUnitOpeningDemography(FoundationTacticalHumanAiTurnHost host,
        WorldState opening, FoundationTacticalCollectiveReceipt receipt)
    {
        var hash = host.StateHash;
        var timeline = receipt.PersistentTimeline ?? throw new InvalidOperationException("Missing demographic calendar.");
        var batch = receipt.BirthBatch ?? throw new InvalidOperationException("Missing birth evidence.");
        var owners = opening.Workers.Workers.Select(w => w.Owner).Distinct().OrderBy(o => o.Value).ToArray();
        var policy = GaExplicitEconomicQaConfiguration.Profile(true, true, true, true).Settlement;
        var presence = FoundationPhysicalPresenceDemographicProjector.Project(opening,
            timeline.Slices.Select(s => new FoundationWorkerTemporalNodeSlice(s.Worker, s.Node,
                s.DurationMicro, s.Activity == PhysicalWorkerActivity.Producing ? s.DurationMicro : 0U,
                s.Activity, s.Occupation)).ToArray(), policy.Demand);
        if (owners.Length != 2 || presence.ScheduleDigest != batch.ScheduleDigest ||
            presence.WorkerTurnMicro != checked((ulong)opening.Workers.Workers.Count * 1_000_000UL))
            throw new InvalidOperationException("Demographic evidence mismatch.");
        var actual = FoundationPhysicalPresenceDemographicProjector.PlanBirths(opening, presence, owners, receipt.Turn.Value);
        if (!actual.Births.Select(b => (b.Owner, b.NatalNode, b.NatalCity))
            .SequenceEqual(batch.Newborns.Select(w => (w.Owner, w.NatalNode!.Value, w.NatalCity))))
            throw new InvalidOperationException("Birth planner differs from actual committed batch.");
        string weights = string.Join(",", owners.Select(o => $"{o.Value}:{presence.Buckets.Where(b => b.Owner == o).Aggregate(0UL, (sum,b) => checked(sum + b.PresenceMicro))}"));
        string births = string.Join(",", owners.Select(o => $"{o.Value}:{actual.Births.Count(b => b.Owner == o)}"));
        GD.Print($"GA_NEXT_R2318E_BIRTH_AUDIT_PASS cycle={receipt.Turn.Value} weights_micro={weights} births={births} actual_batch=true");
        if (receipt.Turn.Value == 1UL)
        {
            if (opening.Workers.Workers.Count != 2 ||
                owners.Any(o => presence.Buckets.Where(b => b.Owner == o).Sum(b => (decimal)b.PresenceMicro) != 1_000_000m))
                throw new InvalidOperationException("Expected equal one-worker opening.");
            // R164 reinforces the selected bucket after each birth. With
            // equal starting weights the three possible owner counts (0,1,2)
            // each have probability 1/3. Sample distinct deterministic turn
            // keys on this fixed opening; broad bounds catch gross bias,
            // not a promise of exact frequencies in a real evolving world.
            const int samples = 1024;
            var counts = new int[3];
            for (ulong t = 1; t <= (ulong)samples; t++)
            {
                var plan = FoundationPhysicalPresenceDemographicProjector.PlanBirths(opening, presence, owners, t);
                if (plan.IsCommitted || plan.Births.Count != 2)
                    throw new InvalidOperationException("Invalid counterfactual birth plan.");
                counts[plan.Births.Count(b => b.Owner == owners[0])]++;
            }
            if (counts.Any(n => n < 250 || n > 430))
                throw new InvalidOperationException($"Unexpected equal-opening birth sample: {string.Join(",", counts)}");
            GD.Print($"GA_NEXT_R2318E_EQUAL_SAMPLE_PASS samples={samples} zero={counts[0]} one={counts[1]} two={counts[2]} uncommitted=true");
        }
        if (hash != host.StateHash)
            throw new InvalidOperationException("Demographic audit changed committed world.");
    }
}
