# Global Arena — Foundation fine physical workers / v10 gate 01

**Date:** 2026-11-16  
**Branch:** feature/ga-next-authoritative-tactical-workers-v10-20261116  
**Status:** isolated draft, no merge and no gameplay activation

## Problem and design decision

The existing game host places every canonical worker in a G(3,0)
`StrategicCellId` and uses G(3,0) edges for movement. Godot displays
G(18,0) tactical tiles; using an existing G(3,0) worker location as a
G(18,0) tile would silently teleport the physical worker into an
unrelated node. The new foundation must not do this.

We explicitly distinguish:

- **v8:** no initialized canonical physical worker census;
- **v9:** initialized individual workers whose location remains a coarse
  G(3,0) cell. The historical all-ready human+AI host and journal run
  unchanged;
- **v10 (opt-in):** initialized canonical workers who each have a
  `PhysicalTacticalTileId` qualified by the exact G(18,0) refinement,
  while their old `CellId` becomes only a compatibility/presentation
  region. It is not a permitted movement endpoint.

The v10 bit is attached to the entire census, never to individual mixed
rows. The schema cannot be downgraded to v9 in normal
`WorldState.WithWorkers`, and v9 movement cannot operate on v10 workers.

## Explicit deployment (no fabricated coordinates)

`WorldState.DeployTacticalWorkers` requires:

1. A world-bound, already initialized **v9** census.
2. An explicitly supplied complete **v10** census preserving exactly
   the same IDs, ownership, activities, commodity, output, multipliers
   and coarse presentation region as the original.
3. The actual `WorldGenerationResult` whose cryptographic signature
   equals the bound WorldState, together with an official class-I
   scale-six `PhysicalTacticalIncidenceMap` that refines the same
   coarse Goldberg parameters.
4. Each submitted fine tile's **canonical incidence** contains its
   worker's old presentation region.

This is not an implicit migration for existing saved games. A separate
initial-placement policy will decide which exact G(18,0) tile hosts each
worker; the v10 test scenario supplies legal explicit placements. The
application's human+AI foundation preview remains v9 for now.

## Journal and physical graph

`PhysicalTacticalWorkerMoveCommand` and
`PhysicalTacticalWorkerMovedEvent` carry typed physical tile IDs and a
qualified `PhysicalTacticalEdgeId`. The v10 turn phase checks that each
worker belongs to the issuer, each command belongs to the current
turn, there are no duplicate worker orders, and source and destination
are equal (activity-only change) or adjacent in the **fine**
`StrategicTopology` corresponding to G(18,0).

One event moves by **one fine edge per turn**, and the same
`PhysicalTacticalWorkerMoveEventExecutor` executes both normal
resolution and `SimulationEventLogReplayer`. Its event records the
resulting coarse region strictly for backwards-compatible presentation:
it keeps the previous region when the destination is still incident to
it; otherwise it uses the lowest incident coarse ID. This is not a
second movement, extra demand or new worker.

The staged WorldState is immutable and only committed through the
existing authority/commit gates. There is no physical movement at
order-queue time.

## Canonical hashes

`CanonicalWorldStateHasher` chooses format **v10** for complete
fine-worker censuses, appending each worker's G(m,n) qualified
parameters and fine tile ID after its existing v9 worker fields.
Its field order and integer serialization are deterministic.

The full byte stream of v8/v9 stays identical to the previous
versions. A v10 worker-position change changes hash even if coarse
presentation region stays the same.

## Explicitly blocked compatibility paths

- v9 `PhysicalWorkerMovementTurnPhase` and event executor reject v10.
- The existing v9 collective human+AI host rejects v10 snapshots
  rather than offering misleading coarse G/P/X movement.
- The v9 coarse economic market projection rejects v10 workers rather
  than counting demand at the wrong scale. A fine market adapter will
  be required in the correct implementation phase.

## Validation gates

- Windows and Ubuntu: xUnit tests for migration, world-binding,
  coarse incidence, no duplicated workers, no new output/demand,
  incompatible/mixed state rejection, v8/v9 hash compatibility,
  v10 hash identity, adjacent traversal, activity-only event,
  journal/replay equivalence, foreign orders, teleport and forged
  edge rejection.
- Existing v9 console gameplay, human+AI turn and Godot scene
  regressions remain green.
- **Not yet shipped:** canonical initial fine-placement worldgen,
  v10 collective human+AI host/AI pathfinding/ready gate, v10 fine
  production/demand/transport market, individual markers or
  movement controls in Godot.
- **No visual checkpoint launcher needed for this domain-only PR.**
  The next Godot-facing PR must include a one-command auto-launch
  script pinned to its verified SHA, as already established.

No merges until domain checks and all existing regressions pass.
