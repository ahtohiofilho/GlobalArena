using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    // Explicit QA only. Reuse the exact signed opening, verified time slices
    // and public pure birth planner. No births from counterfactual samples
    // are committed or made visible in interactive gameplay.
    private void AuditUniformCityDemography(FoundationTacticalHumanAiTurnHost host,
        WorldState opening, FoundationTacticalCollectiveReceipt receipt)
    {
        var hash = host.StateHash;
        var timeline = receipt.PersistentTimeline ?? throw new InvalidOperationException("Missing uniform-city calendar.");
        var batch = receipt.BirthBatch ?? throw new InvalidOperationException("Missing birth batch.");
        var owners = opening.Workers.Workers.Select(w => w.Owner).Distinct().OrderBy(o => o.Value).ToArray();
        var prior = opening.Workers.Workers.Select(w => w.Id).ToHashSet();
        var existing = host.Snapshot.Workers.Workers.Where(w => prior.Contains(w.Id)).ToArray();
        if (hash.FormatVersion != 26U || batch.SelectedParents is not null ||
            !batch.Newborns.Select(w => w.Owner).SequenceEqual(owners) ||
            host.Snapshot.Workers.Workers.Any(w => !w.UniformCityDemography || w.BirthWaitTurns.HasValue) ||
            batch.Newborns.Any(child => !existing.Any(w => w.Owner == child.Owner &&
                w.TacticalTileId == child.NatalNode && w.Activity != PhysicalWorkerActivity.Traveling)))
            throw new InvalidOperationException("Stationary city birth or canonical policy mismatch.");

        if (receipt.Turn.Value == 3UL)
        {
            // Counterfactual only: three own opening workers at two real nodes,
            // split 2:1, and no orders. The other civilization is unchanged.
            // The lottery must ignore the population difference.
            var owner = owners[0];
            var local = opening.Workers.Workers.Where(w => w.Owner == owner).OrderBy(w => w.Id.Value).ToArray();
            var other = opening.Workers.Workers.First(w => w.Owner != owner);
            if (local.Length != 3 || local.Any(w => w.TacticalTileId != local[0].TacticalTileId) ||
                other.TacticalTileId == local[0].TacticalTileId)
                throw new InvalidOperationException("Expected distinct stationary opening sites.");
            var remote = local[2].Id;
            var sample = opening.WithWorkers(new PhysicalWorkerRuntimeState(opening.Workers.Workers.Select(w =>
                w.Id == remote ? w.WithTacticalAssignment(other.TacticalTileId!.Value, other.CellId,
                    PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned) : w), true, true));
            var sampleSlices = timeline.Slices.Select(s => s.Worker == remote
                ? s with { Node = other.TacticalTileId!.Value, Activity = PhysicalWorkerActivity.Other,
                    Occupation = PhysicalWorkerOccupation.Unassigned, Crop = null } : s).ToArray();
            int remoteBirths = 0;
            for (ulong t = 1; t <= 256; t++)
            {
                var plan = FoundationPhysicalPresenceDemographicProjector.PlanUniformCityBirths(sample, sampleSlices, batch.ScheduleDigest, t);
                if (plan.Count != owners.Length) throw new InvalidOperationException("Missing eligible owner birth.");
                if (plan.Single(b => b.Owner == owner).NatalNode == other.TacticalTileId) remoteBirths++;
            }
            if (remoteBirths < 90 || remoteBirths > 166)
                throw new InvalidOperationException($"City lottery unexpectedly population-weighted: {remoteBirths}/256.");
            WorldState Traveling(WorldState state, Func<PhysicalWorkerRuntimeRecord, bool> filter) =>
                state.WithWorkers(new PhysicalWorkerRuntimeState(state.Workers.Workers.Select(w => filter(w)
                    ? w.WithTacticalAssignment(w.TacticalTileId!.Value, w.CellId,
                        PhysicalWorkerActivity.Traveling, PhysicalWorkerOccupation.Unassigned) : w), true, true));
            var excluded = Traveling(sample, w => w.Id == remote);
            for (ulong t = 1; t <= 16; t++)
                if (FoundationPhysicalPresenceDemographicProjector.PlanUniformCityBirths(excluded, sampleSlices, batch.ScheduleDigest, t)
                    .Single(b => b.Owner == owner).NatalNode != local[0].TacticalTileId)
                    throw new InvalidOperationException("Transit-only site selected.");
            var allOwnTravel = Traveling(sample, w => w.Owner == owner);
            var otherOnly = FoundationPhysicalPresenceDemographicProjector.PlanUniformCityBirths(allOwnTravel, sampleSlices, batch.ScheduleDigest, 1UL);
            var allTravel = Traveling(sample, w => true);
            if (otherOnly.Count != owners.Length - 1 || otherOnly.Any(b => b.Owner == owner) ||
                FoundationPhysicalPresenceDemographicProjector.PlanUniformCityBirths(allTravel, sampleSlices, batch.ScheduleDigest, 1UL).Count != 0)
                throw new InvalidOperationException("Travel-only owners must not receive births.");
            GD.Print($"GA_NEXT_R23112_CITY_SAMPLE_PASS samples=256 smaller={remoteBirths} transit_excluded=true all_travel_skipped=true uncommitted=true");
        }
        if (hash != host.StateHash) throw new InvalidOperationException("City audit mutated the host.");
        GD.Print($"GA_NEXT_R23112_BIRTH_PASS cycle={receipt.Turn.Value} owners={owners.Length} uniform_city=true replay=true");
    }

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
