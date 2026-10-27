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

All graph paths lie on **actual generated fine-mesh adjacency**, but the tree, source/destination placement, forecasts and whole-itinerary continuous freight functions are **synthetic test fixtures**, not authentic geographic/transport-service production distributions. A single technological identity is used to isolate discovery and clear the conceptual boundaries. These workloads cannot justify maritime routing behavior, multimodal transshipment economics, asset capacities or global map performance.

## Interpreting evidence

`SCENARIO` reports fine topology size, constructed assets, explicit intents, quotes, accepted trades, the cost of preparing the synthetic snapshot separately and the stable full-cycle financial/path fingerprint.

`PHASE` reports median/maximum milliseconds and thread-local allocations for `discovery`, `quotation`, `clearing` and `total`, separately for `exhaustive` and `indexed`, with counts of examined economic lanes and evaluations of commodity marginal values. The physical candidate formation and quotation costs should agree between these two clearing modes; any allocation latency improvement must be **assigned to the clearing phase**, not erroneously to rerouting or quote construction.

`SMALL_ORACLE=PASS` confirms the small graph's exact whole-itinerary price oracle selects the same built path and exact quote as the fast connectivity reference. `RESULT=PASS_FINE_ECONOMIC_CYCLE_BENCHMARK` means **economic repeatability/equivalence assertions passed**, not that a requested framerate, pause limit or complete-world scale was achieved.

The previously documented [eight-case allocation-only reference](turn-locked-freight-market-reference.md) remains independently executable. Do not directly compare its quote-only or allocator-only times with this complete economic-close benchmark as if they measured the same inputs or work.

## Further architectural milestones

This does **not** introduce a new scheduler, number of movement turns per closure, price-model cap, fixed per-mode tariff, global transportation hub algorithm, free geographic corridor, physical trade-in-transit simulation or a UI processing pause. An occasional economic close delay may be acceptable for real strategic depth; its frequency, maximum desired pause and presentation will be decided in a later design cycle after broader end-to-end profiling.

The next engineering step is to benchmark changing infrastructure/access **between** economic cycles and, separately, devise a scalable **whole-itinerary-cost-preserving** route candidate/ranking procedure. Where prices depend non-additively on the complete itinerary, conventional edge-weight Dijkstra alone is not a valid equivalent to the small-world exact oracle. Open-ocean free route discovery and authoritative state persistency remain separate tasks. ADR-064 remains Proposed.
