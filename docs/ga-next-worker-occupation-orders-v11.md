# GA-Next — individual productive occupation orders (v11)

**2026-11-16 · PR #375 · stacked on PR #374 · Draft / no merge**

## Why

The preceding capital demo finally displays the intended initial
six **individual** workers on one physical tile per civilization,
grouped into three true occupational markers with numeric population.
The current step makes that role distribution a meaningful,
journal-backed decision rather than a graphic mock.

## Invariant: reassignment is not physical movement

A valid v11 reallocation changes *only* the productive occupation
of one worker (Agriculture, Forestry, Mining). It explicitly retains
`PhysicalWorkerId`, civilization, qualified G(18,0) tile, strategic
presentation region, `CommodityId`, `OutputMicroPerTurn`,
`DemandMultiplierMicro`, and the operational activity
(`Producing`, `Traveling`, `Military`, `Other`).

Reassignment is NOT an instantaneous new crop, mine output, new
commodity, completed work, new demand, cost, tax, sale, or price.
The occupational role is not inferred from commodity ID.
`Unassigned` is not a currently orderable productive profession.

An order targets an **existing owned** person, with a different
valid v11 role, at the very same canonical physical tile.
Coarse G(3,0) addresses are not accepted as fine destinations.
Legacy v10 workers, missing worker identities, opposite
civilization identities, duplicate no-op roles, cross-edge role
orders, and changed activity bundled with reassignment are rejected.

## Commit and replay

`FoundationTacticalHumanAiTurnHost.QueueHumanOccupation`
queues an intent inside the per-civilization single-order-per-worker
dictionary, exactly like G (move) and P (resume production).
Queue, change, and X cancellation do not change canonical hash,
groups, color, population, nor WorldState revision. Any different
order for the same person replaces the prior pending order.

The accepted E collective turn, after all-ready human + AI, now
permits `NextOccupation` as an **optional field** of the existing
qualified fine worker command and its replay event. The phase
validates and applies it to an immutable `PhysicalWorkerRuntimeRecord`;
the event executor independently validates owner, tile, activity,
changed occupation and v11 availability, and reconstructs the same
record in the journal replay. The existing canonical hash v11
incorporates the occupation code, so replay equality proves the
change. v8/v9/v10 hashes and the older `NextOccupation=null`
event behavior remain unchanged.

Only one event changes this person's role per turn. Workers have
no duplicate output or demand and no phantom replacement.
Multiple people may be reallocated on the same turn, deterministically
by their canonical worker IDs.

## UI

- Selection is map-first: click a real green/brown/grey profession disc
  repeatedly to rotate through the actual people within that group.
  Tab still lists every person separately.
- Keyboard shortcuts in the tactical view, without the planetary
  atlas open: **A** = Agriculture, **V** = Vegetal/Forestry,
  **N** = Mining. The operational dock shows the same three compact
  buttons with disabled states for unavailable/no-op roles.
- The order status and pending row show the intended new occupation,
  not a fictitious physical move or already applied production change.
  X cancels, E applies the entire collective human+AI journal.
- After applying the role, both the existing coplanar disc counts and
  the non-modal per-group hover update from the accepted authoritative
  census. If an old group becomes empty, no empty group is drawn.

## Representative demo sequence

Six human people share their home tile, initially 2 Agriculture +
2 Forestry + 2 Mining. An agriculture worker #1 first moves by a
real edge (G/E), producing 1 farmer at the home and 1 at the neighbor.
Then a second farmer #4 at the home is reassigned to Mining
(N/X/N/E). The home then has 0 Agriculture, 2 Forestry, 3 Mining;
the neighboring physical tile still has 1 farmer. The total remains
6 people under that civilization and 12 overall, with unchanged
per-person nominal capacity and demand.

## Verification

- xUnit: no mutation before E, replay for 1 or multiple reassigned
  people, deterministic order independent of input sequence,
  cancellation and replacement, owner protection, invalid v11 roles,
  legacy v10 rejection, identical count/identity/position/item/
  capacity/demand facts.
- Godot 4.7.2 Mono headless and real Xvfb input:
  G/X/G/E/P/E then N/X/N/E, no premature group recoloring,
  stable mouse selection, hashes and occupancy after acceptance.
  PNG `04_after_reassignment.png` captures the real three-group
  visual after one occupation group disappears at the capital.
- Windows automatic PowerShell launcher pins and verifies the exact
  source blobs from PR #375 before creating an isolated worktree.

**Out of scope:** changing item production based on profession,
agricultural suitability of specific physical biomes, settlement
prices, revenues, taxes, wages, capital budgets, multiple-occupation
combinations and large-scale selection operations. These require a
separate economic design and performance gate.
