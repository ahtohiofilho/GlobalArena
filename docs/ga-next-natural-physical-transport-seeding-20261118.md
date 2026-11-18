# GA-Next — Natural land/coast/sea physical transport seeding (2026-11-18)

## Decision

PR #421 introduced a complete, canonically hashed v12 physical transport
inventory, but a production world must never receive invented/fallback road
and capacity vectors. This gate introduces an **explicit deterministic
initializer** from the same signed canonical V1 physical worldgen data.
It does NOT implement infrastructure investment or final game balancing.

### Source of the terrain

The currently bound worker host uses
`WorldGenerationVersion.Initial`, whose accepted
`WorldGenerationResult.StrategicPhysicalFields` exposes canonical
strategic elevation, sea level, and land/water classification.

**The worldgen does not yet expose an independent authoritative altitude
sample for every fine tactical tile.** Therefore this gate computes the
integer arithmetic mean of each fine tile's one to three incident coarse
elevations. It compares that mean to the worldgen sea level with the
same strict `elevation > seaLevel` land test as
`StrategicLandWaterMap`. The projection creates NO independent detailed
relief or hydrological/topographical claim. It is a coarse-derived
interim transport classification, not a final continuous geographic model.

Moreover, the V1 elevation generator samples independent per-coarse-cell
random values. That limits geographic realism; a future continuous
V2 physical surface must use its own signature-checked adapter, not
be silently substituted for V1. The current implementation rejects V2.

### Three real physical crossing classes

Classification is by **both actual fine endpoints** of a canonical
dual-graph crossing:

- **Land–land:** explicit land free-flow baseline; positive reference
  carrying capacity; additive nonnegative land-slope cost calculated
  from the absolute difference of the two projected elevations,
  with exact fixed-point arithmetic.
- **Land–sea (coast):** explicitly supplied coast cost and carrying
  capacity; no silent port/toll or land-slope multiplier.
- **Sea–sea:** explicitly supplied sea cost and carrying capacity;
  no invented naval asset or civilization ownership.

Every canonical fine crossing appears **exactly once**, with
`NoInfrastructure`, `owner=null`, `tariff=0`. A natural edge is a
traversable connection, not a built road; an absent record is an error
at v12 deployment, not a free route. These are physical crossing classes,
not civilization-specific economic markets.

### Parameters and security boundary

`FoundationNaturalTransportSeedingPolicy` requires six positive inputs:
a physical baseline cost and reference cargo capacity for each of
land, coast, and sea. A separate nonnegative land-slope cost is explicit.
**There is no engine-default or adopted gameplay numeric policy.**
Sample numerical fixtures live only in tests. The independent
`PhysicalTransportInfrastructureRuntimeState.CreateInitial` validates
positive cost/capacity, canonical ordering and absent title for natural edges.
`WorldState.DeployInitialPhysicalTransport` validates that the exact
generated world's canonical signature matches the world binding and
that the complete fine crossing topology was supplied. Only then is
the resulting inventory an authoritative, v12-hashed fact.

`FoundationPhysicalLaggedFreightCyclePlanner.CoordinateWorldCandidate`
continues to take actual workers and the immutable transport inventory
from a v12 `WorldState`. It does not take an externally authored
per-edge quote vector. Normalized universal price
`P(Q)=sqrt(D/Q)` remains unchanged, with D and Q aggregated by
physical (tile, commodity), NOT civilization. Seller-specific actual
physical costs and owner tolls remain separate. Congestion is only
quoted for the **following settled cycle**, never recomputed within
the current turn.

### What is not proved by this gate

**Large historical distances are not guaranteed to be prohibitively
expensive by a universal P(Q) curve alone**: if different physical
markets have different real D, a remote market's first finite quantum
can offer higher marginal revenue. Geography and transport costs
interact; we must not claim that the first drop of water-filling
always remains local without additional proof.

This gate also does NOT implement mode-specific access (ships, ports,
bridges), regional biome impedance, river crossing, road construction,
capacity investment, military takeover, tariff governance,
dynamic authoritative transport transition events, settlement,
treasury entries, or trading history. Nor does it ratify a practical
initial land/coast/sea balance. All these remain separate downstream
gates. In particular, an ocean edge's presence is not evidence of
a seagoing fleet or commercially viable transoceanic trade.

## Acceptance evidence

The new `FoundationPhysicalNaturalTransportSeederTests` verify
complete natural crossing coverage, all terrain classes, independently
recomputed land slope, owner/tariff absence, policy sensitivity,
repeatability and stable v12 hashing, signature rejection of wrong
generated worlds, and the uncommitted world-only economic candidate.
No old v8-v11 state hash or economic settlement is modified.

Gate is intentionally **Draft on #421**, no merge to `main`.
