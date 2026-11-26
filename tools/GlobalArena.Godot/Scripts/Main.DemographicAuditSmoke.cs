using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    // Explicit QA only. Reuse the exact signed opening, verified time slices
    // and public pure birth planner. No births from counterfactual samples
    // are committed or made visible in interactive gameplay.
    private void AuditIndividualDemography(FoundationTacticalHumanAiTurnHost host,
        WorldState opening, FoundationTacticalCollectiveReceipt receipt)
    {
        var hash = host.StateHash;
        var timeline = receipt.PersistentTimeline ?? throw new InvalidOperationException("Missing individual demographic calendar.");
        var batch = receipt.BirthBatch ?? throw new InvalidOperationException("Missing birth evidence.");
        var parents = batch.SelectedParents ?? throw new InvalidOperationException("Missing selected workers.");
        var owners = opening.Workers.Workers.Select(w => w.Owner).Distinct().OrderBy(o => o.Value).ToArray();
        var demand = GaExplicitEconomicQaConfiguration.Profile(true, true, true, true).Settlement.Demand;
        var slices = timeline.Slices.Select(s => new FoundationWorkerTemporalNodeSlice(s.Worker, s.Node,
            s.DurationMicro, s.Activity == PhysicalWorkerActivity.Producing ? s.DurationMicro : 0U,
            s.Activity, s.Occupation)).ToArray();
        var plan = FoundationPhysicalPresenceDemographicProjector.PlanIndividualBirths(opening, slices, demand, receipt.Turn.Value);
        if (hash.FormatVersion != 25U || plan.Presence.ScheduleDigest != batch.ScheduleDigest ||
            !parents.SequenceEqual(plan.Births.Select(b => b.Parent)) ||
            !batch.Newborns.Select(w => w.Owner).SequenceEqual(owners) ||
            !batch.Newborns.Select(w => (w.Owner, w.NatalNode, w.NatalCity))
                .SequenceEqual(plan.Births.Select(b => (b.Birth.Owner, (GlobalArena.World.PhysicalTacticalTileId?)b.Birth.NatalNode, b.Birth.NatalCity))) ||
            parents.Distinct().Count() != owners.Length)
            throw new InvalidOperationException("Individual birth plan differs from committed batch.");
        for (int i = 0; i < parents.Count; i++)
        {
            var parent = opening.Workers.Workers.Single(w => w.Id == parents[i]);
            var child = batch.Newborns[i];
            if (parent.Owner != child.Owner || child.BirthWaitTurns != 0UL ||
                !slices.Any(s => s.Worker == parent.Id && s.Node == child.NatalNode && s.PresenceMicro > 0U))
                throw new InvalidOperationException("Invalid parent, natal location or newborn waiting counter.");
        }
        foreach (var w in opening.Workers.Workers)
        {
            var actual = host.Snapshot.Workers.Workers.Single(x => x.Id == w.Id);
            ulong expected = parents.Contains(w.Id) ? 0UL : checked(w.BirthWaitTurns!.Value + 1UL);
            if (actual.BirthWaitTurns != expected)
                throw new InvalidOperationException("Individual waiting counter not advanced correctly.");
        }
        if (receipt.Turn.Value == 2UL)
        {
            // A pure counterfactual: two existing workers per owner, one
            // with weight 4 and one with weight 1. Test actual lottery
            // without changing the host snapshot or committing any birth.
            var firstOwner = owners[0];
            var first = opening.Workers.Workers.First(w => w.Owner == firstOwner).Id;
            var sample = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
                w.WithBirthWaitTurns(w.Id == first ? 3UL : 0UL)), true, true));
            if (new GlobalArena.Simulation.CanonicalWorldStateHasher().Compute(sample) ==
                new GlobalArena.Simulation.CanonicalWorldStateHasher().Compute(opening))
                throw new InvalidOperationException("Waiting counters missing from canonical hash.");
            int selected = 0;
            for (ulong t = 1; t <= 256; t++)
            {
                var draw = FoundationPhysicalPresenceDemographicProjector.PlanIndividualBirths(sample, slices, demand, t);
                if (draw.Births.Single(b => b.Birth.Owner == firstOwner).Parent == first) selected++;
            }
            if (selected < 170 || selected > 235)
                throw new InvalidOperationException($"Weighted worker selection inconsistent with 4:1 weights: {selected}/256.");
            GD.Print($"GA_NEXT_R2319_WEIGHT_SAMPLE_PASS samples=256 heavier={selected} uncommitted=true");
        }
        if (hash != host.StateHash) throw new InvalidOperationException("Individual demographic audit changed committed world.");
        GD.Print($"GA_NEXT_R2319_BIRTH_PASS cycle={receipt.Turn.Value} owners={owners.Length} one_per_owner=true weights=true");
    }

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
