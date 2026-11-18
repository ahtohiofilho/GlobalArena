# GA-Next — Real-worker construction work allocation (precommit gate, 2026-11-18)

## Invariant and source of truth

All infrastructure construction must be performed by existing individual
physical workers. Each worker contributes economic demand irrespective of
whether currently producing goods, traveling, idle or assigned to construction.
No phantom specialist/buyer, parallel infrastructure, free labor, local
stockpile, seller wallet or construction treasury is introduced here.

The v13 complete edge inventory carries immutable **natural construction
labor requirement**, already computed from the altitude difference of two
real endpoints. This PR adds an audit-only planner that ties possible
one-turn labor to **real worker identities, their actual tactical tiles and
their civilization owners**.

### Exact candidate contract

`FoundationPhysicalConstructionWorkAssignment` is an explicit proposed
`(Issuer, WorkerId, PhysicalTacticalEdgeId)` tuple. It is NOT an accepted
turn order. The planner reads a canonically bound `WorldState` with v11
individual workers and the v13 complete construction inventory. It verifies
the exact original world signature and fine Goldberg graph. Each submitted
worker must:

1. Exist in that immutable authoritative census;
2. Be owned by the explicitly identified issuer;
3. Currently have `PhysicalWorkerActivity.Other` (a **conservative
   temporary eligible non-producing state**, NOT a new actual construction
   activity; a worker doing something else may not silently contribute);
4. Be physically at **either endpoint** of the selected real fine edge;
5. Be listed at most once, so no double assignment across edges;
6. Target a natural, unbuilt `NoInfrastructure` crossing.

`FoundationPhysicalConstructionLaborPolicy` provides one explicitly
positive `LaborMicroPerWorkerPerTurn`, the same for all assigned workers.
Worker `OutputMicroPerTurn` is *commodity* capacity and is deliberately
**not** repurposed as construction productivity. Activity's demand
multiplier is likewise never repurposed as labor.

The planner groups work by canonical edge identity, then sorts participating
workers by their real individual ID before crediting a project's labor.
For each worker, credit is

```
credit = min(explicitWorkerLaborPerTurn, edgeRequiredLabor - creditedSoFar)
unused = explicitWorkerLaborPerTurn - credit
```

`AvailableLabor = CreditableLabor + UnusedLabor` holds exactly in
checked integer micro-units. A project whose natural requirement was met
may report `MeetsLaborRequirement=true`, but this is **labor sufficiency
only**, NOT completed infrastructure. No energy/cargo/money is invented,
and no unused work is redistributed to phantom projects.

## Critical authority and settlement boundary

- The proposal's `IsCommitted` is always false. It carries a source
  `WorldStateHash` as provenance and only read-only result arrays.
- The caller submits **candidate** assignments; this API cannot
  demonstrate they came from an accepted collective turn. A future
  `QueueHumanConstruction`/AI order, all-ready validation and atomic
  **journaled work event** must authorize any real progress.
- Because current v11 worker activity has only `Other`, this gate does
  **not** encode a new persisted `Constructing` activity and does not
  incorrectly interpret all `Other` workers as builders. Only
  **explicit eligible assignments** yield candidate labor.
- Future authoritative progress tracking must store accumulated labor
  per edge, enforce all turn/event ordering and ensure no parallel roads.
  It will also need a deterministic ownership rule for concurrent
  investment, conquest, material inputs and physical capacity improvements.
- The project does not publish a constructed road, change owners,
  tariffs, freight capacity, active worker orders, state revision,
  economics, next-cycle congestion, inventories, money, journal or
  last closed trade.
- Universal `P(Q)=sqrt(D/Q)` and demand driven by all real workers remain
  unchanged. Current-cycle transport is still frozen; no premature
  next-cycle quote is created.

## Performance and checks

Index all existing workers and canonical fine edges once. Work is
O(W + E + A log A) over real workers W, fine edges E and submitted
assignments A. Reversed assignment order gives identical attribution
and source hash.

The test gate validates physical presence, legal ownership, exclusion
of producing/traveling/missing workers, duplicate rejection, exact
micro-unit conservation, labor caps and deterministic ordering,
compatibility with v12 and rejection of a foreign world, unchanged
worker-derived demand and unchanged physical inventory. Existing
market, freight, StateHash, kernel and Godot suites must remain green.

Draft stacked on PR #423. No merge to `main`.
