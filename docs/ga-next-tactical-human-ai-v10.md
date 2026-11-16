# GA-Next — Human + AI collective turn on canonical tactical tiles (v10)

**16 November 2026** — stacked PR #369, based on PR #368. Draft and opt-in.

## Scope and authority

The v9 human/AI foundation session used a G(3,0) StrategicCellId as the
worker's physical location. PR #368 introduced an explicit v10 position
qualified by PhysicalTacticalTileId and a fine G(18,0) movement journal.
This increment closes the gap in the **collective turn owner**, without
silently repurposing the old v9 queue, AI destination or player input.

The class `FoundationTacticalHumanAiTurnHost` receives a world-bound
v10 CanonicalGameSession, its exact PhysicalTacticalIncidenceMap,
the human civilization set and the AI civilization policy set. Every
registered civilization must have exactly one role. All worker orders
reference real fine tile IDs. No strategic movement IDs can be accepted.

## Turn contract

- One private set of intents per civilization, one effective order per
  physical worker. A ready human cannot change its order until reopening.
- Each AI decides once per not-yet-ready turn. Plans are validated in
  full before submission, and accepted proposals roll back if staging fails.
  The baseline AI may resume production at the *same physical tile* for a
  worker who is Traveling and has positive existing productive capacity.
  It does not add a new worker, product, demand account or economic result.
- All workers are unchanged until **every** participant is ready.
  The game owner builds commands ordered by stable worker identity and
  resolves a single canonical turn through the existing
  AuthoritativeTurnCommitGate and v10 movement phase.
- The resulting journal is replayed with the same
  PhysicalTacticalWorkerMoveEventExecutor, and a full v10 SHA-256 state
  hash must match. Event count determines WorldState revision; an
  all-ready empty turn advances the game clock without a phantom event.
- Rejected moves (foreign unit, wrong fine topology, non-adjacent tile,
  stale plan, duplicate worker/AI order) leave the published WorldState
  unchanged. No fine->coarse fallback is permitted.

## Initial worker positioning: explicit demo only

`FoundationTacticalInitialDeployment.Deploy` requires a complete
dictionary mapping each **existing** worker ID to exactly one real fine
tile; positions are checked against the actual source world and
coarse/fine incidence before snapshot publication.

`FoundationTacticalDemoScenario.CreateLocalHumanAiPreview` is a
reproducible, clearly named **demonstration**: it reuses the established
six-person v9 human+AI scenario, obtains its actual G(3,0) world and
G(18,0) canonical incidence, then assigns an unused fine tile within
each worker's coarse region in ID order. Worker #4 retains its Traveling
state until the AI is ready to resume production. Two reproductions
with equal world seed have identical placement and state hash.

**Not a final spawn policy:** this demo does not yet assess land/sea,
coastal accessibility, naval units, biological resources, climate,
civilization territory claims, transport infrastructure or housing.
Before activation in an ordinary game, generate legal and physically
appropriate initial locations for every worker, rather than treating
this test policy as production worldgen.

## New gates

`FoundationTacticalHumanAiV10Tests` covers (among other cases):

- Exactly six individually keyed workers and distinct valid G(18,0)
  locations, with identical hashes for repeated seeds;
- AI ready ahead of human but no turn commitment;
- One real fine-edge human move + one AI production restart in a
  single atomic journaled commit, with replay-matched v10 hash;
- A second empty collective turn that advances time with no extra
  worker or phantom revenue;
- No pending-plan edits after readiness without reopen;
- AI planning idempotence and rollback on invalid/foreign orders;
- Rejection of tactical teleport, foreign worker, wrong G(m,n)
  topology, stale external snapshot, missing or non-incident initial
  placements.

Windows/Ubuntu .NET builds and xUnit, the pre-existing v9 gameplay
and Godot 4.7.2 Mono real scene, cross-platform hash and kernel
regression remain mandatory.

## Next delivery gate — bridge to Godot

After full domain CI, wire the Godot local player to this *v10* host.
Use the actual per-worker fine tile to draw selective unit circles
anchored to the physical hex centers, reusing prior marker visual
assets. Keep the tactical viewport dominant and the Tab dock for
worker details. Map clicks may select an actual worker and adjacent
fine destination; G/P/X orders must refer only to the fine graph.
E closes the human+AI collective turn as before.

Do not activate production/consumption/cargo commerce on G(18,0) until
a fine-aware economic adapter is available; demand is one per actual
worker and follows the worker during travel. No new visual checkpoint
launcher is needed for this **domain-only** pull request; the next
Godot-facing PR will ship the validated automatic Windows launcher.
