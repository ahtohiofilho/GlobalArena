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

## Changing economic-cycle snapshots: four sequential closes

The additional `--fine-economic-sequence` command reruns a **fresh, complete physical discovery → ex-ante quotation → dynamic commodity clearing** on four coherent post-movement snapshots. It is implemented in the **same benchmark harness** and run by the three-platform workflow. No economic time scale, movement-round interval, pricing mechanism or state persistence is introduced.

- **Cycle 1 — initial:** all originally built corridors open, explicit service offers, initial demand and forecasts.
- **Cycle 2 — forecast-and-demand:** same built physical infrastructure and market access, but a new ex-ante expected volume and changed destination demand/value curves. Paths should remain physically identical; quotes are nevertheless recomputed with a **new turn ordinal**, while commodity clearing responds to the changed curves.
- **Cycle 3 — closed-corridor:** closes an edge used by a real service, chosen from the actual constructed paths. Existing assets still exist, but that corridor becomes inaccessible; disconnected intents must vanish. All eligible offers receive fresh quotes.
- **Cycle 4 — reopened-new-asset:** reopens the corridor, removes the old asset identity and adds a **newly identified installed physical asset** on the same fine edge and under the same explicitly supplied technology/version. Market routes return but service-path provenance must reflect the replacement; there is no hidden retroactive upgrade.

Each sequence runs over two generated fine Goldberg fixtures: `fine-local` and `fine-connected`. Both the exhaustive market and the indexed market process **every cycle** with seven observations per mode; exact physical/commodity/freight ledger fingerprints must agree across market algorithms and replays. Within a sequence the fingerprint is expected to differ across changed inputs. Failure to lose offers on a closure or to detect the new asset identity is treated as an invalid workload. The benchmark records the number of **physically unchanged service paths** relative to the preceding cycle. This is an *upper-bound reuse opportunity*, **not** a proof that the routes can be safely cached without detailed dependency and access validation.

No route cache is implemented. Every close explicitly rebuilds the available service itinerary set, creates a fresh whole-itinerary cost function for each available offer and evaluates every eligible function **once** on that cycle's expectation. Fresh turn-owned `EconomicTurnFreightQuote` objects are formed even when a physically identical service path survives; **financial prices are never copied forward from previous cycles**. A separate test verifies prior financial trades remain immutable after later forecast/closure changes.

The CLI logs `SEQUENCE` with the cycle label, turn ordinal, accepted trades, current quote/function evaluation count, unchanged physical path count, snapshot-setup time and full ledger signature, plus `PHASE` rows for each mode/cycle. `SEQUENCE_INVARIANTS=...;status=PASS` certifies structural changes observed as expected, not that a caching strategy is correct or a full world has been simulated. The changing snapshots are synthesized as independent fixtures; real movement, construction and asset-authority state transitions remain future work. An occasional close-processing pause remains a possible gameplay tradeoff and no hard deadline is asserted.

## Further architectural milestones

This does **not** introduce a new scheduler, number of movement turns per closure, price-model cap, fixed per-mode tariff, global transportation hub algorithm, free geographic corridor, physical trade-in-transit simulation or a UI processing pause. An occasional economic close delay may be acceptable for real strategic depth; its frequency, maximum desired pause and presentation will be decided in a later design cycle after broader end-to-end profiling.

The next engineering step is to benchmark changing infrastructure/access **between** economic cycles and, separately, devise a scalable **whole-itinerary-cost-preserving** route candidate/ranking procedure. Where prices depend non-additively on the complete itinerary, conventional edge-weight Dijkstra alone is not a valid equivalent to the small-world exact oracle. Open-ocean free route discovery and authoritative state persistency remain separate tasks. ADR-064 remains Proposed.
