# Fine-mesh economic close: end-to-end reference benchmark

**Scope:** observational performance checkpoint for [Issue #16](https://github.com/ahtohiofilho/GlobalArena/issues/16) after PRs #24–#26. The experiment deliberately measures **one economic closure on a completed geographic/asset/market snapshot**, rather than deciding the duration or presentation of movement rounds.

## Execute and inspect

```sh
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj \
  --configuration Release -- --fine-economic-cycle
```

The existing [three-platform freight benchmark workflow](../../.github/workflows/turn-locked-freight-market-benchmark.yml) runs this command **after** the historical eight-scenario reference benchmark, saving `fine-economic-cycle-benchmark.txt` and `dotnet --info` as downloadable artifacts for Windows, macOS and Ubuntu. The mode requires .NET 10 **Release** and has **no performance acceptance threshold**.

## What the timer includes

The caller supplies a consistent, synthetic post-movement snapshot: a real generated Goldberg **fine topology**, a constructed connected tree of qualified fine-edge assets, immutable economic-point fine-tile anchors, technology/version identities, explicitly declared service intents, forecast mass, commodities and production/demand curves. Geography generation and the creation of this fixture are reported as `snapshot_fixture_ms` **outside** the economic-cycle timer: this benchmark neither advances movement rounds nor simulates persistence/authoritative construction.

Each measured cycle then executes, sequentially:

1. **Discovery:** `EconomicFineTransportServiceItineraryDiscovery` finds a canonical minimum-leg itinerary **only over built, open, operational** physical corridors matching each declared service intent's technology and catalogue version. A caller-supplied whole-itinerary pricing factory creates the mathematical function here but does **not evaluate its pre-turn forecast yet**.
2. **Quotation:** `EconomicTransportServiceQuoteResolver` revalidates geography, infrastructure and offers, calls **each eligible service's whole-itinerary cost function once** on its ex-ante mass forecast, selects a stable service per economic pair and produces immutable quotes. No second charge/price calculation is introduced.
3. **Market:** `EconomicTurnLockedFreightMarketResolver` clears food commodities using the **same frozen unit freight quotes**, separately supplied producer reservations and changing destination commodity marginal values. Both **Exhaustive** and opt-in **DependencyIndex** modes execute on identical inputs.

The *total* stopwatch includes the three steps and their invocation overhead. Separate stopwatches capture each stage. Thread-local allocated bytes and phase elapsed times are recorded for each step and for the total. No GC pause, memory peak, parallelism or thread scheduling is claimed to have been fully characterized by this single-threaded .NET benchmark.

A complete **SHA-256 fingerprint** includes every selected service ID, physical asset path, route key, ruleset version, forecast load, exact rational freight price and every accepted trade's quantity, marginal commodity valuation, integrated value, freight charge, reservation cost and net return. The fingerprint must match across **seven repeated measurements per mode**, exhaustive versus indexed and (where applicable) all three OS. Each phase and the full cycle output **median and maximum observed** time and managed allocated bytes; the maximum of seven samples is **not** a statistically sound worst-case/tail-latency bound. Signatures and ledger audits run **after** the timed cycle.

The physical-route oracle from PR #26 has exponential complexity over simple paths. It is therefore used **once outside the timer** in the tiny constructed-tree fixture to independently verify route quotation against connectivity-based discovery. It is **not** executed in the larger benchmark scenarios. On a tree, each point pair has one simple route, so the route choice is known; these measurements do **not** demonstrate optimal selection among many physically competing itineraries.

## Synthetic workloads

| Case | Generated fine cells | Constructed assets | Sources × destinations | Commodities | Service intents | Graph structure |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `fine-smoke` | 42 | 29 | 2 × 2 | 2 | 4 | Tiny connected tree; cross-checked against exhaustive cost oracle |
| `fine-local` | 162 | 99 | 6 × 6 | 2 | 18 | Each source declares three local commercial partners |
| `fine-hub` | 642 | 299 | 8 × 8 | 3 | 28 | Declared central source/destination hubs plus local links |
| `fine-connected` | 1,442 | 749 | 12 × 12 | 3 | 144 | Dense economic intents over sparse built physical tree |
| `fine-multipath` | 642 | 489 | 10 × 10 | 3 | 100 | Physical connected tree plus **140 additional constructed loop edges**, creating competing itineraries |

All graph paths lie on **actual generated fine-mesh adjacency**, but the constructed networks, source/destination placement, forecasts and whole-itinerary continuous freight functions are **synthetic test fixtures**, not authentic geographic/transport-service production distributions. Four original scenarios remain sparse constructed trees; the new `fine-multipath` case has **140 extra constructed non-tree physical corridors**, and consequently route alternatives. A single technological identity is used to isolate discovery and clear the conceptual boundaries. These workloads cannot justify maritime routing behavior, multimodal transshipment economics, asset capacities or global map performance.

## Interpreting evidence

`SCENARIO` reports fine topology size, constructed assets, explicit intents, quotes, accepted trades, the cost of preparing the synthetic snapshot separately and the stable full-cycle financial/path fingerprint.

`PHASE` reports median/maximum milliseconds and thread-local allocations for `discovery`, `quotation`, `clearing` and `total`, separately for `exhaustive` and `indexed`, with counts of examined economic lanes and evaluations of commodity marginal values. The physical candidate formation and quotation costs should agree between these two clearing modes; any allocation latency improvement must be **assigned to the clearing phase**, not erroneously to rerouting or quote construction.

`SMALL_ORACLE=PASS` confirms the small graph's exact whole-itinerary price oracle selects the same built path and exact quote as the fast connectivity reference. `RESULT=PASS_FINE_ECONOMIC_CYCLE_BENCHMARK` means **economic repeatability/equivalence assertions passed**, not that a requested framerate, pause limit or complete-world scale was achieved.

The previously documented [eight-case allocation-only reference](turn-locked-freight-market-reference.md) remains independently executable. Do not directly compare its quote-only or allocator-only times with this complete economic-close benchmark as if they measured the same inputs or work.

## Changing economic-cycle snapshots: four sequential closes

The additional `--fine-economic-sequence` command reruns a **fresh, complete physical discovery → ex-ante quotation → dynamic commodity clearing** on four coherent post-movement snapshots. It is implemented in the **same benchmark harness** and run by the three-platform workflow. No economic time scale, movement-round interval, pricing mechanism or state persistence is introduced.

- **Cycle 1 — initial:** all originally built corridors open, explicit service offers, initial demand and forecasts.
- **Cycle 2 — forecast-and-demand:** same built physical infrastructure and market access, but a new ex-ante expected volume and changed destination demand/value curves. Paths should remain physically identical; quotes are nevertheless recomputed with a **new turn ordinal**, while commodity clearing responds to the changed curves.
- **Cycle 3 — closed-corridor:** closes an edge used by a real service, chosen from the actual constructed paths. Existing assets still exist, but that corridor becomes inaccessible; disconnected intents must vanish. All eligible offers receive fresh quotes.
- **Cycle 4 — reopened-new-asset:** reopens the corridor, removes the old asset identity and adds a **newly identified installed physical asset** on the same fine edge and under the same explicitly supplied technology/version. Market routes return but service-path provenance must reflect the replacement; there is no hidden retroactive upgrade.

Each sequence runs over two generated fine Goldberg fixtures: `fine-local` and `fine-connected`. Both the exhaustive market and the indexed market process **every cycle** with seven observations per mode; exact physical/commodity/freight ledger fingerprints must agree across market algorithms and replays. Within a sequence the fingerprint is expected to differ across changed inputs. Failure to lose offers on a closure or to detect the new asset identity is treated as an invalid workload. The benchmark records the number of **physically unchanged service paths** relative to the preceding cycle. This is an *upper-bound reuse opportunity*, **not** a proof that the routes can be safely cached without detailed dependency and access validation.

No route cache is implemented. Every close explicitly rebuilds the available service itinerary set, creates a fresh whole-itinerary cost function for each available offer and evaluates every eligible function **once** on that cycle's expectation. Fresh turn-owned `EconomicTurnFreightQuote` objects are formed even when a physically identical service path survives; **financial prices are never copied forward from previous cycles**. A separate test verifies prior financial trades remain immutable after later forecast/closure changes.

The CLI logs `SEQUENCE` with the cycle label, turn ordinal, accepted trades, current quote/function evaluation count, unchanged physical path count, snapshot-setup time and full ledger signature, plus `PHASE` rows for each mode/cycle. `SEQUENCE_INVARIANTS=...;status=PASS` certifies structural changes observed as expected, not that a caching strategy is correct or a full world has been simulated. The changing snapshots are synthesized as independent fixtures; real movement, construction and asset-authority state transitions remain future work. An occasional close-processing pause remains a possible gameplay tradeoff and no hard deadline is asserted.

## Opt-in physical-path-only dependency cache (reference experiment)

The optional `EconomicFineTransportPathCache` accompanies
`EconomicFineTransportServiceItineraryDiscovery.Discover`. It stores **only
ordered built-asset IDs** for a canonical minimum-leg physical path. It never
retains `EconomicTransportServiceOffer`, pricing functions, any whole-itinerary
cost value, `EconomicTurnFreightQuote`, commodity valuations or trades. The
caller owns one cache across snapshots; it is non-authoritative and not
concurrency-safe. Failed input validation/pricing does not publish a partial
cache. The cache is replaced after every successful snapshot, so it does not
grow across arbitrarily many previous economic cycles.

A reuse requires an exact match on the **fine generated topology's full edge
and endpoint identities**, qualified physical source and destination tile IDs,
and the declared technology type plus catalogue version. For each technology,
the cache separately compares an exact canonical list of **available**
built-asset IDs and their physical edge IDs, after operational checks and
geographic closures. Hence physical modifications to a *different technology*
do not invalidate an unaffected mode's path.

Two physical-world changes permit exact canonical-path reuse:

1. The available graph for that technology is **unchanged**; the same physical
   source and destination still connect through the same minimum-leg route.
2. The new available graph is only a **subset** of the former available graph
   (asset removals, closures or operational suspension), while **every asset
   on the selected old path survives with its original ID and corridor**.
   Deleting physical edges cannot introduce a shorter or lexicographically
   preferred alternative, so the old selected path remains the exact BFS
   solution. If even one chosen leg was removed, or there was an addition,
   relocation, reopening, asset replacement or technology redefinition,
   the corresponding path must be rediscovered.

Even on a cache hit, the **entire current snapshot is validated**, a new
caller-defined whole-itinerary function is generated, each eligible service
is re-quoted **once from the current economic-cycle forecast**, and market
allocation starts with current supply/demand and commodity values. The cache
does not transform a hop-minimal reference into the economically cheapest
whole-service route; price-aware routing remains a separate, nonadditive
problem represented by the small-world oracle.

The `--fine-economic-sequence` benchmark now runs a third
`indexed-cached-physical` variant against the independent full-recomputation
exhaustive/indexed variants. Seven **independent four-cycle sequences** use
separate caller-owned caches; no warm cache leaks across sampled sequences.
Physical/service/quote/trade SHA-256 transcripts must match exactly for all
strategies, cycles, replays and operating systems. `PATH_REUSE` logs actual
reused and newly searched paths **and fresh quotation counts**, while
`PHASE` reports observed phase and total timing/allocation costs. This is
an opt-in correctness experiment, not an adopted cache for authoritative
turn execution or a global performance guarantee. Snapshot fixture generation
is still outside the timed economic close; no movement cadence or acceptable
UI delay was selected. ADR-064 remains Proposed.

## Shared-source traversal and competing constructed paths

The canonical **minimum-physical-leg** discoverer now uses one deterministic
breadth-first **predecessor tree per (physical origin tile, technology type,
technology catalogue version)** for all intents requiring a new path in
the same economic close. It then reconstructs each destination's selected
built-asset itinerary from the shared tree. Multiple services from one origin
previously traversed the same constructed adjacency repeatedly. As before,
edges are ordered by asset ID and then destination node; breadth-first
discovery therefore preserves minimum-leg selection and canonical asset-ID
tie-breaking. This is **not** a new cheapest-commercial-freight algorithm:
the whole-itinerary price function may be nonadditive.

The discoverer also directly indexes edges from the immutable canonical
one-based `StrategicTopology.Edges` sequence. This avoids allocating an
edge-ID dictionary on each call while retaining closed-edge and foreign-asset
fail-closed validation; the topology constructor guarantees contiguous
identity ordinals. Source trees are transient per economic close and never
constitute an authoritative map or commodity/pricing cache.
`EconomicFineTransportPathReuseDiagnostics.SourceTreeBuilds` reports how
many distinct physical source trees were built, separately from how many
service intents were searched. Cached physical itineraries remain opt-in
and retain exactly the same conservative invalidation rules as PR #29.

The new `fine-multipath` workload uses **642 generated fine cells** and
**489 explicitly built physical assets**: a 349-edge connected spanning
tree over 350 visited cells and 140 additional actually constructed
fine-grid edges. One hundred directed economic service intents compete
over multiple available physical routes; three synthetic commodities
are commercially cleared. No bare geography-only route is admitted.
The previous four tree-based scenarios remain intact.

For that competing-path case, an extra **next-economic-cycle experiment**
rebuilds all forecasts (triples are not hard-coded; the supplied expected
mass is doubled in this synthetic case), then runs fresh indexed discovery
versus a independently prewarmed *physical-only* path cache. Both arms
form new freight quotes and solve the market; their **complete path/service/
quote/financial SHA-256 fingerprints** must match exactly on each replay.
The `MULTIPATH` marker prints the number of reused physical paths and
freshly evaluated freight quotes. The small-world **whole-itinerary
price-aware exhaustive oracle** is intentionally not applied to this
densely connected fixture, since enumerating all simple paths is
exponential. Consequently, matching the minimum-leg reference does
**not** prove that its selected path is the commercially cheapest choice.

The benchmark reports **observational elapsed times and allocations**;
fewer BFS trees or cache reuse are not accepted as a speedup absent
measured evidence. No fixed economic cycle frequency, mode tariff, route
capacity, pause threshold or ADR-064 ratification is introduced.

## Further architectural milestones

This does **not** introduce a new scheduler, number of movement turns per closure, price-model cap, fixed per-mode tariff, global transportation hub algorithm, free geographic corridor, physical trade-in-transit simulation or a UI processing pause. An occasional economic close delay may be acceptable for real strategic depth; its frequency, maximum desired pause and presentation will be decided in a later design cycle after broader end-to-end profiling.

The next engineering step is to benchmark changing infrastructure/access **between** economic cycles and, separately, devise a scalable **whole-itinerary-cost-preserving** route candidate/ranking procedure. Where prices depend non-additively on the complete itinerary, conventional edge-weight Dijkstra alone is not a valid equivalent to the small-world exact oracle. Open-ocean free route discovery and authoritative state persistency remain separate tasks. ADR-064 remains Proposed.

## Exact terminal-leaf pruning for whole-itinerary price routing (opt-in)

The **price-aware small-world route oracle**, `EconomicFinePriceAwareServiceResolver`, now accepts an optional `EconomicFinePriceAwareSearchMode.TerminalLeafPruning`. Its default remains **Exhaustive**, the unmodified complete simple-path reference. This optimization makes no assertion that the whole-itinerary cost is additive or that the shortest *physical* path has the lowest *commercial* freight.

For a declared service and its specific constructed technology ID + catalogue version, repeatedly remove **nonterminal physical vertices whose eligible built-asset degree is at most one**. Preserve source and destination regardless of degree. Such a nonterminal cannot lie in **any simple source–destination path**, because reaching it and then leaving would require two incident eligible physical edges. Removing it may create new dead leaves, which can also be removed. Parallel built assets count as separate incidences; this may conservatively keep some unusable vertices but never removes a valid route. This calculation is based entirely on *available constructed physical assets*, not speculative freight prices, hidden modal tariffs, built-in transport capacities or a world-graph approximation.

After that exact physical prefilter, the oracle still **enumerates all remaining simple paths**, asks the caller for each **whole-itinerary** function and evaluates it once at the **ex-ante cycle forecast**. Exact rational price comparisons and tie-breaking retain their original semantics; the downstream commodity value remains dynamic and immutable freight quotes stay turn-owned. A link examination or complete-path budget is still fail-closed, never replaced by a partial best-price choice. New non-authoritative diagnostics separately count pruned physical vertices and rejected adjacency links.

The existing `--fine-economic-cycle` executable includes a **separately reported**, small `PRICE_ORACLE_PRUNE` benchmark on a constructed Goldberg physical network: 95 visited fine tiles in a tree, 94 spanning-tree physical assets, **two additional actually built cycle edges**, four explicit directional service intents and synthetic full-path cost functions. Many branch paths cannot reach their destinations without revisiting the origin. Seven warm, paired Release observations compare original exhaustive path enumeration with terminal-leaf pruning on exact completed-path count, function evaluation count, the **complete selected physical path and rational quote SHA-256 transcript**, examined physical links, elapsed milliseconds and managed thread allocations. `PRICE_ORACLE_PARITY=PASS` requires exact equality of all selected route/price outputs while strictly reducing examined links.

**Limits:** this is an exact graph-theoretic elimination of dead-end branches, not an asymptotic solution to enumerating exponentially many simple paths in dense cyclic graphs. Its leaf peeling also consumes CPU and memory and may **regress time** when there are few removable branches. Only observed comparisons on selected synthetic workloads may establish a benefit; no automatic activation, world-scale acceleration claim, route model change, cycle frequency, optional user-facing pause policy or ADR-064 ratification is implied. Issue #16 remains open for stronger admissible pruning and representative workloads with many competitive costed physical routes.

## Block-cut corridor elimination for exact whole-itinerary costs

`EconomicFinePriceAwareSearchMode.BlockCutCorridorPruning` is a third,
**opt-in** exact graph-theoretic candidate filter for the small-world
`EconomicFinePriceAwareServiceResolver`. The **Exhaustive** default and
`TerminalLeafPruning` mode remain independently available.

For each explicitly authorized directional service's compatible **built,
operational, open** physical network, construct an undirected vertex-level
graph and determine its **biconnected components and articulation points**
via an **iterative**, stack-safe Tarjan traversal. Build the associated
block-cut tree. Because a simple source-to-destination path cannot enter
a side block and return through the **same articulation vertex**, every
valid physical itinerary is contained entirely within blocks on the
unique block-cut tree corridor connecting source and destination. Exclude
vertices outside those blocks *before* the existing complete simple-path
enumeration and whole-itinerary freight price evaluations.

This is strictly stronger than repeatedly deleting degree-one physical
dead ends for networks with **dangling constructed loops**: a cycle attached
at a single articulation vertex has no leaves, but cannot be used in a
simple itinerary to a destination outside it. Parallel independently built
assets on the same physical adjacency are **deduplicated only for
articulation analysis**, not from the original asset-level candidate
enumeration, where each priced service-path remains distinct. Technology
catalogue version and access restrictions are still applied before graph
construction. All allowed complete paths continue to have their
caller-defined whole-itinerary freight functions evaluated once from
that economic cycle's forecast. No invented additive edge tariff,
physical throughput cap or new modal handoff is introduced.

An independent generated Goldberg regression constructs a valid
source-to-destination priced cycle with a cheaper *longer* route and
an additional **built triangle attached at the source alone**.
The block-cut mode must eliminate that pendant cyclic detour, preserve
the exact profitable alternatives, evaluate the identical number of
full-path freight functions, and examine fewer physical links than
both exhaustive enumeration and degree-one leaf peeling. Seeded
input permutations, parallel assets, closures, and forecast changes
are compared against the full exhaustive oracle.

One regression also anchors a destination economic point **on a physical
itinerary's intermediate cell**, with commodity demand there, but
provides **no commercial service intent ending at that point**:
geographic passage must never synthesize a new freight quote, trade
endpoint or loading/unloading stop. This **does not** implement port,
rail terminal or transshipment eligibility; those require separate
operational installations and explicit contracts in a later logistics
milestone. In particular, geographic proximity to a shipping route is
not commercial access.

The `PRICE_ORACLE_PRUNE` benchmark now reports three paired
seven-sample modes (`exhaustive`,
`terminal-leaf-pruning`, `block-cut-corridor-pruning`), with
precisely matching accepted service/path/quote SHA-256 signatures and
full-path cost-function evaluation counts. Extra block-cut preprocessing
has its own time/allocation cost. **Neither** exact graph pruning mode
solves the exponential growth of genuinely many simple paths
*inside* a highly connected biconnected block; no general full-world
speedup, mandatory time budget, or economic-cycle frequency is claimed.
ADR-064 remains Proposed.
