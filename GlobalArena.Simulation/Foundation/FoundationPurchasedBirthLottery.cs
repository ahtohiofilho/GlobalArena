using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationPurchasedBirthCandidate(
    PhysicalWorkerId Worker, PhysicalTacticalTileId Node, ulong WaitTurns);

/// <summary>Detached planning only. No treasury debit, worker creation or
/// canonical state mutation. Caller must supply verified physical evidence.
/// </summary>
public static class FoundationPurchasedBirthLottery
{
    public static IReadOnlyList<FoundationPurchasedBirthCandidate> ReadCandidates(
        WorldState snapshot, FoundationFineFirstPhysicalGeography fine, CivilizationId owner)
    {
        if (!snapshot.PurchasedDemography.IsInitialized || !owner.IsValid)
            throw new InvalidOperationException("Purchased birth candidates require a deployed policy.");
        _ = snapshot.PurchasedDemography.Get(owner);
        var crossing = snapshot.EdgeTravels.Pending.Select(p => p.Worker).ToHashSet();
        if (snapshot.EdgeTravels.NavalVoyages is { } voyages)
            crossing.UnionWith(voyages.Select(v => v.Worker));
        return Array.AsReadOnly(snapshot.Workers.Workers
            .Where(w => w.Owner == owner && w.BirthWaitTurns > 0UL &&
                w.Activity != PhysicalWorkerActivity.Traveling && !crossing.Contains(w.Id) &&
                w.TacticalTileId.HasValue)
            .Where(w =>
            {
                var tile = w.TacticalTileId!.Value;
                int index = checked((int)tile.FineStrategicCellId.Value - 1);
                return index >= 0 && index < fine.FineElevationRaw.Count &&
                    fine.SurfaceAtIndex(index) == FoundationFineSurfaceKind.Land &&
                    fine.CanTraverseOnFoot(index);
            })
            .OrderBy(w => w.Id.Value)
            .Select(w => new FoundationPurchasedBirthCandidate(w.Id, w.TacticalTileId!.Value,
                w.BirthWaitTurns!.Value)).ToArray());
    }

    // Canonical ID order; no replacement, no +1, no newborns in this draw.
    // A full batch must fit before any draw. UInt128 accommodates the sum of
    // all ulong weights in a CLR-sized array. Equal-worker mode is diagnostic
    // only and operates on the same positive-weight eligible population.
    public static IReadOnlyList<FoundationPurchasedBirthCandidate> Select(
        IEnumerable<FoundationPurchasedBirthCandidate> candidates, CivilizationId owner,
        SimulationContext context, ulong quantity, bool equalWorkerDiagnostic = false)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (!owner.IsValid || context.Turn.Value == 0UL) throw new ArgumentException("Invalid draw context.");
        var rows = candidates.OrderBy(c => c.Worker.Value).ToArray();
        if (rows.Any(c => !c.Worker.IsValid || !c.Node.IsValid) ||
            rows.Select(c => c.Worker).Distinct().Count() != rows.Length)
            throw new ArgumentException("Invalid or duplicated candidate.");
        rows = rows.Where(c => c.WaitTurns > 0UL).ToArray();
        if (quantity > (ulong)rows.Length)
            throw new InvalidOperationException("Purchase batch exceeds distinct positive-weight parents.");
        if (quantity == 0UL) return Array.Empty<FoundationPurchasedBirthCandidate>();
        var tree = new UInt128[checked(rows.Length + 1)];
        UInt128 total = 0;
        UInt128 Weight(int i) => equalWorkerDiagnostic ? 1UL : rows[i].WaitTurns;
        for (int i = 1; i < tree.Length; i++)
        {
            var weight = Weight(i - 1);
            total += weight;
            tree[i] += weight;
            long parent = (long)i + (i & -i);
            if (parent < tree.Length) tree[(int)parent] += tree[i];
        }
        var result = new FoundationPurchasedBirthCandidate[(int)quantity];
        int bit = 1;
        while (bit <= rows.Length / 2) bit *= 2;
        for (int ordinal = 0; ordinal < result.Length; ordinal++)
        {
            var pick = Draw(context, owner, (ulong)ordinal, total);
            int index = 0;
            for (int step = bit; step > 0; step /= 2)
            {
                long next = (long)index + step;
                if (next < tree.Length && tree[(int)next] <= pick)
                { index = (int)next; pick -= tree[index]; }
            }
            result[ordinal] = rows[index];
            var weight = Weight(index);
            total -= weight;
            for (long i = index + 1L; i < tree.Length; i += i & -i)
                tree[(int)i] -= weight;
        }
        return Array.AsReadOnly(result);
    }

    private static UInt128 Draw(SimulationContext context, CivilizationId owner, ulong ordinal, UInt128 bound)
    {
        UInt128 threshold = unchecked((UInt128)0 - bound) % bound;
        Span<byte> input = stackalloc byte[48];
        BinaryPrimitives.WriteUInt64LittleEndian(input, 0x5232333642495254UL);
        BinaryPrimitives.WriteUInt64LittleEndian(input[8..], context.Seed.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(input[16..], context.Turn.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(input[24..], owner.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(input[32..], ordinal);
        Span<byte> digest = stackalloc byte[32];
        for (ulong retry = 0; ; retry = checked(retry + 1UL))
        {
            BinaryPrimitives.WriteUInt64LittleEndian(input[40..], retry);
            SHA256.HashData(input, digest);
            UInt128 number = ((UInt128)BinaryPrimitives.ReadUInt64LittleEndian(digest[8..]) << 64)
                | BinaryPrimitives.ReadUInt64LittleEndian(digest);
            if (number >= threshold) return number % bound;
        }
    }

    // Called only after the eventual authorized batch, including zero-purchase
    // turns. This returns a detached worker census; no state is committed.
    public static IReadOnlyList<PhysicalWorkerRuntimeRecord> AdvanceWaitingCounters(
        IEnumerable<PhysicalWorkerRuntimeRecord> workers, IEnumerable<PhysicalWorkerId> selected)
    {
        var rows = workers.ToArray();
        var ids = selected.ToArray();
        var parents = ids.ToHashSet();
        if (parents.Count != ids.Length || rows.Any(w => !w.BirthWaitTurns.HasValue) ||
            parents.Any(id => !rows.Any(w => w.Id == id && w.BirthWaitTurns > 0UL)))
            throw new InvalidOperationException("Counter reset requires distinct existing positive-weight parents.");
        return Array.AsReadOnly(rows.Select(w => w.WithBirthWaitTurns(
            parents.Contains(w.Id) ? 0UL : checked(w.BirthWaitTurns!.Value + 1UL))).ToArray());
    }
}
