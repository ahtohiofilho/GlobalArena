# GA-Next — Sparse structural sharing for journaled construction and node-local worker demand
Date: 2026-11-18

## Approved semantics

A worker always has exactly one **physical fine tactical NODE** location.
A construction order targets a **real adjacent fine physical EDGE**. Either
endpoint may supply genuine workers; the worker stays at the same node and
continues to contribute its full demand to that node. Neither the midpoint
of an edge nor the location of other builders becomes an invented demand site.

This gate does **not** introduce slope/roughness node attributes, new
settlement points, virtual construction teams, automatically completed
roads, material consumption, tolls or markets. Construction labor stays
registered on the physical edge, and node-based worker-derived demand
stays registered where the worker really stands. Natural operation cost
remains linear in endpoint altitude difference, and labor requirement
remains quadratic, with explicit, independently tunable coefficients.

## Performance finding and improvement

PR #425 introduced correct v14 journaled worker work, but the previous
`PhysicalTransportInfrastructureRuntimeState.ApplyJournaledWork` allocated
a **complete new array of ALL E edge records per worker event** and
revalidated the complete world graph on every immutable snapshot.
The construction executor also linearly scanned all workers and edges to
verify each work event. Thus hundreds of accepted workers on a world of
thousands of edges repeated avoidable full inventory and worker scans.

This gate changes the **IN-MEMORY representation only**, not the gameplay
rule, schema, event identity or canonical hash:

- Retain the immutable, canonical, sorted v12/v13 physical edge record
  array as a single structurally shared geometry/ownership/cost basis.
- Upon first accepted work event, create a sparse page directory:
  each page covers **256 canonical fine edge records**, and untouched
  pages implicitly project to **explicit zero** in the v14 read view.
- Each subsequent event clones the small page directory and the
  **one affected 256-slot page**, leaving all other pages and static
  physical records shared and immutable. Historical world snapshots are
  unaffected by later events. Additional committed progress is clamped
  to the natural v13 work requirement as before.
- Expose the complete, readonly, canonical `Edges` list through a
  logical view that reconstructs immutable records at read time. The
  hasher still sees the identical v14 bytes: all edge identities,
  titles, base freight costs, capacities, natural labor requirements
  and committed labor (zero for untouched edges) in the same order.
  v8–v13 byte streams remain unchanged.
- Worker identities are already sorted in the authoritative v11 census;
  `PhysicalWorkerRuntimeState.FindWorker` uses O(log W) binary search.
  The new `Transport.TryGetEdge` uses O(log E) binary search by the
  qualified fine edge ID. No fake worker or edge identity is introduced.
- `WorldState.AdvanceRevision` and the single-edge journaled work
  transition reuse the prior immutable cross-domain validation. All
  public **new** world / worker / transport input paths still run the
  complete cross-domain checks; the two trusted copy paths preserve
  the same validated identity, owner roster and physical worker census.

### Complexity budget

For E fine edges, W workers, and B accepted work events, one work-event
mutation now updates O(E/256 + 256) progress-page pointers/values instead
of O(E) new edge-record allocations. Event entity lookups take
O(log W + log E), not O(W + E). One turn still performs its
intentional full canonical snapshot hash and full-world movement/economic
validation at the publication boundary; there is no claim of O(1)
full-game turn cost or complete world-scale benchmark certification.

**No hot-path price recalculation, freight re-quoting or market settlement**
is added by these work events. The universal
`P(Q)=sqrt(D/Q)` and existing lagged congestion remain untouched.

### Test acceptance

The new `FoundationPhysicalConstructionSparseLedgerTests` cover:
1. Structurally shared canonical edge storage, unchanged historical
   snapshots, one touched page and two disjoint pages across thousands
   of real fine edges, and canonical edge lookup;
2. Opposite-endpoint builders credited to one edge while real worker
   demand remains in two separate fine nodes, before and after turn;
3. One all-ready turn with **120 genuine civilization-owned builders**,
   exact journal/event/revision counts, complete replay hash, one
   allocated page, unchanged workforce demand and no built asset;
4. Exact binary lookup for all existing workers, and proper missing ID.
Existing v8–v14 hash, source ownership, freight, Godot and kernel
regressions must remain green.

### Deliberate next phases

This gate does not implement construction materials or resource inputs,
automatic operational upgrades, historical modes, final title
determination or acquisition/transfer of roads. The next business gate
should define what **materials and technology** constitute an
infrastructure asset and how their physical production is debited without
contradicting the abstract no-stock market. A single owner per edge and
event-journaled takeover/investment stay non-negotiable.

Work is Draft stacked on PR #425. NO merge to `main`.
