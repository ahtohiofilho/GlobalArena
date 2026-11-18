# GA-Next — Authoritative construction labor by real workers (v14)

## Status and goal (2026-11-18)

This gate takes PR #424's noncommitting worker-to-edge labor proposal and
connects it to the existing **all-ready tactical turn commit gate**,
`PhasedTurnResolutionEngine`, event log, and deterministic independent
replay. The kernel executes accepted orders from real workers and accumulates
physical construction progress on real fine Goldberg edges. It does **not**
automatically create a road, owner, storage location, laborer, treasury,
trade settlement or material input.

### Physical data

An edge in the v13 natural-transport inventory has a canonical
`NaturalConstructionLaborMicro` based only on altitude delta between the
two true fine endpoints. The first accepted positive construction event
activates a *complete* v14 `CommittedConstructionLaborMicro` ledger, with
zero for every other canonical edge. Each later event increments exactly
one selected edge's accumulated physical labor, no more than the natural
requirement. A fully labor-satisfied edge is **still a natural crossing**:
it does not become a constructed, owned, higher-capacity or tolling asset
until a future authoritative infrastructure-completion gate approves
materials and ownership.

- The v14 hash appends canonical section `42` with one integer per
  canonical fine edge. This is opt-in; untouched v13 and older v8–v12
  hashes remain byte-for-byte unchanged.
- The primitive `RecordJournaledConstructionWork` copies the immutable
  transport inventory, checks the natural labor cap, and returns a new
  WorldState. It does not itself publish that state.
- The public world-only price/freight projector continues reading
  current physical costs, capacities, titles and tariffs unchanged.
  The market curve `P(Q)=sqrt(D/Q)` remains universal.

### All-ready collective authority

`FoundationTacticalHumanAiTurnHost` has an optional
`FoundationPhysicalConstructionLaborPolicy`. Without it, the existing
v10/v11 host behavior is untouched. With an explicitly supplied policy
and v13/v14 world, private human construction intents can be added
using `QueueHumanConstruction` or an atomic batch.

Every accepted order identifies the issuer, existing worker and real edge.
It is accepted only if the worker:

1. Exists and belongs to the issuing civilization;
2. Has `PhysicalWorkerActivity.Other` (no phantom builder);
3. Stands at an actual endpoint of the selected edge;
4. Has no concurrent move, activity or occupation order this turn;
5. Works on an existing, naturally unbuilt crossing whose natural labor
   requirement has not already been reached.

A worker's *commodity production capacity* is not treated as labor rate,
and the real workforce still generates demand whether idle or constructing.
The rate is explicit in `FoundationPhysicalConstructionLaborPolicy`.

All civilizations must be ready before the commit. The existing tactical
movement/occupation phase resolves first; the new construction phase
resolves against its staged state, rejects overlap with movement orders,
and issues a `PhysicalTransportConstructionWorkedEvent` per contributing
worker. Work credit equals exactly `min(explicitWorkerLabor, remainingEdgeLabor)`.
Each event includes its real worker, actual physical endpoint, edge,
positive credited labor and **expected source WorldState revision**.

The same `PhysicalTransportConstructionEventExecutor` validates every
event during private resolution **and independent journal replay**.
A duplicated or forged event with stale revision, wrong issuer,
wrong endpoint, invented worker or wrong quantity is rejected.
One credited event increments the revision once. A new optional
construction receipt coexists with the legacy movement receipt, and
the combined replay hash must match the published snapshot.

### Explicit limits / technical debt

- Progress is real, persistent in `WorldState` and hashable, but the
  worker's permanent `Activity` enum is **not** given a new Constructing
  variant yet. A turn order consumes that worker's single action for
  that turn; its `Other` state remains and still causes consumption.
  Future full activity scheduling may introduce a named role.
- No materials (wood, stone, metals) are consumed here. Materials and
  resource production must be linked to final infrastructure construction
  through separate physically grounded work/economic events.
- A project can reach its labor requirement with multiple civilizations'
  workers; this gate deliberately does **not** determine final legal title.
  Future ownership/capture and capacity upgrades require separate rules.
- All crossings remain accessible to the existing freight planner; this
  gate does not invent ships, ports, bridges or prehistoric travel.
- Each event currently creates a fresh immutable complete edge inventory,
  which may scale poorly for many simultaneous builders and many
  thousands of edges. This correctness-first gate needs a later sparse
  ledger/batched immutable update benchmark before unrestricted large
  worlds. Do not silently enable large-scale construction without that.
- The existing collective host combines real *movement* and *construction*
  events. Economic settlement, military conquest and physical stocks
  remain separate resolution phases. No direct treasury changes.
- Work-only completion of a natural edge does not automatically build
  infrastructure: it preserves the "no invented parallel road" rule.

## Acceptance

Tests validate genuine all-ready publication, no-progress before ready,
multi-turn accumulation, mixed movement+construction in the SAME turn,
worker ownership/eligibility, single-action protection, economic demand
conservation, unchanged costs/capacities/titles, exact v14 hashing and
replay, tamper rejection and labor sufficiency without a road. Directed
suite runs Ubuntu and Windows; kernel, StateHash, Godot and benchmark
regressions must also pass, subject to external CI runner availability.

Gate is Draft stacked on PR #424, **no merge to main**.
