# GA-Next — last settled cycle trade-volume overlay (participant-private)
2026-11-18 • stacked on PR #428, no merge to `main`.

## Purpose

Show freight lines of the **last actually closed economic cycle** on the
tactical spherical fine Goldberg map, without converting uncommitted
water-filling previews into historical commerce. Line thickness encodes
**the amount of cargo transported**; line color encodes the **real owner of
the edge infrastructure** (RGB test identities for owned assets, neutral
for unbuilt and unowned). The geometry connects adjacent **physical tile
centers**, NOT polygon border vertices. Worker positions and demands stay
on their actual nodes; no construction-material consumption or worker
construction activity is introduced.

## Trusted source and narrow authorization

The v15 `FoundationEconomicSalesSettledEvent` is produced by a trusted
SYSTEM phase after actual producer/consumer market clearing, route/edge
freight audit and revenue/tax/toll reconciliation. The new projection is
built while resolving this economic cycle; independent journal replay
recomputes the full economic plan and compares *every* projected
participant route, crossing cargo, owner, and commodity load.

A civilization receives an edge ONLY when it actually participated as
**seller OR buyer** in a delivery that traversed the edge. Its reported
cargo on that edge is the **sum of cargo on its participated deliveries**,
not the world's overall edge traffic. A delivery where buyer and seller
are the same civilization is counted only once. Filtering uses actual
transaction parties, not capital ownership assumptions or guessed fog of
war. Each visible crossing is a subset of the independently audited
global load, with owner and endpoints from real topology.

The global real freight vector used for next-cycle congestion is NEVER
sent to `TacticalContextPreview`. The read model remains
`FoundationLastClosedEconomicTradeView`, with an
`AuthorizedSubset` availability state even when the participant happens
to see every route. That avoids leaking global trade completeness through
a status flag. No traffic for unrelated deliveries or off-map endpoints
is invented.

## Publication life cycle

1. All participants ready -> worker movement and monetary investment
   resolve in private -> audited economic SYSTEM phase settles.
2. Complete journal and authoritative StateHash published. The host
   independently replays that journal and verifies its canonical hash.
3. Only then `FoundationEconomicTradeCycleReadBoundary` atomically
   replaces its per-civilization last-closed views, verifying the
   actual v15 cycle number, committed revision/hash, world binding,
   source physical topology and no duplicate cycle.
4. `FoundationTacticalHumanAiTurnHost.GetLastClosedEconomicTradeView(civ)`
   is the only gameplay-facing route read. A nonparticipant cannot
   read or infer global history.
5. The Godot tactical viewport accepts that authorized read view and
   renders ribbons directly between stored canonical physical tile
   centers in the *current resident chart*. Width is a compressed
   monotone function of visible cargo, e.g. square-root relative load.
   It never changes economic cargo or geography. The toggle
   `LAST SETTLED TRADE ROUTES` hides/shows this view-only mesh.
6. Camera focus and presentation projection rebuild the same route
   projection, without solving the market again; no route is painted
   when no closed cycle has been published.

### Explicit limits

**The ordinary demonstration client still creates a worker-only v11
host without an economic settlement policy.** Consequently its
historical-trade overlay correctly displays nothing until a real v15
host with explicit economic policies is injected by the gameplay
bootstrap. This gate establishes the authenticated data channel and
Godot renderer; it does NOT invent market parameters to make decorative
lines appear in the default demonstration.

No fog-of-war/intelligence or national infrastructure visibility policy
has been approved yet. The first production authorization is strictly
participated sales/purchases; richer visibility is a future game-rule
decision. The overlay is not a statement that all routes and all
global freight are visible. There is no stored physical inventory,
buyer wallet, phantom transport or constructed road.

## Acceptance

Tests check journaled first and subsequent economic cycles, correct
read-view chronology/hash/cargo unit, old worker-only NoClosedCycle,
per-civilization edge loads equal to independently re-derived
seller/buyer delivery cargo (and never exceed global loads),
correct real edge owner, no outsider read, proper handling of empty
views, unchanged workforce/treasury and v15 canonical hash. Godot
build and smoke validate the scene component and sidebar toggle.
