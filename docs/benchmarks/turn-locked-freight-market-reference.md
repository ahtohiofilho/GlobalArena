# Turn-locked freight allocation — reference benchmark

**Status:** Empirical reference and indexed baseline for [Issue #16](https://github.com/ahtohiofilho/GlobalArena/issues/16); observational performance numbers only. Expanded to sparse commercial graph fixtures and larger dense cases without changing the economic model.

## Economics being measured

The already-merged `EconomicTurnLockedFreightMarketResolver` (PR #19) consumes a **complete freight unit-cost quote computed before each turn**, derived from a caller-provided continuous mathematical function and a pre-turn expected volume. The quote remains unchanged while that turn's raw commodity quantities are allocated. The **commodity's own market marginal value** continues to change with delivered-to-market allocations. Producer reservation costs are explicit and separate. The solver remains a **quantized greedy reference**, not a guaranteed globally optimal water-filling solver or a complete transport infrastructure engine.

This benchmark uses **synthetic-only** continuously differentiable curves, synthetic routes (source/destination keys rather than a multimodal graph), and synthetic supply/demand. They are **not game defaults, historical modes, freight tariff coefficients or balance choices**. Benchmark input size does not represent a target world size. A future production workload suite must be documented separately.

## Repeatable invocation

```sh
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj --configuration Release -- --turn-freight-market
```

Run using .NET 10 Release. The dedicated [workflow](../../.github/workflows/turn-locked-freight-market-benchmark.yml) runs the same command on Ubuntu, Windows and macOS, uploading logs and `dotnet --info` evidence.

Each case runs one initial resolution, one warmup cycle and **five measured groups**: (1) exhaustive reference, (2) indexed candidates, (3) exhaustive with diagnostic counters, and (4) indexed with diagnostic counters. **All four forms must yield the same exact SHA-256 transcript** covering trades, origins, destinations, quantities, commodity marginal values, integrated values, source reservations, freight and net returns. The initial quote function runs *before* allocation timing: its evaluation count is reported separately. **Five additional quote-construction samples** independently measure the deterministic construction of the immutable pre-turn quote array using the identical continuous ruleset function and a SHA-256 quote fingerprint. Quote construction and allocation are never conflated.

| Synthetic case | Sources | Destinations | Commodities | Route quotes | Potential commodity lanes |
| --- | ---: | ---: | ---: | ---: | ---: |
| reference-profit-boundary | 1 | 1 | 1 | 1 | 1 |
| reference-small | 2 | 2 | 2 | 4 | 8 |
| reference-medium | 4 | 4 | 3 | 16 | 48 |
| reference-wide | 8 | 8 | 4 | 64 | 256 |
| local-ring-sparse | 24 | 24 | 4 | 72 | 288 |
| hub-and-local-sparse | 16 | 16 | 5 | 60 | 300 |
| complete-medium-large | 16 | 16 | 5 | 256 | 1,280 |
| complete-stress | 24 | 24 | 5 | 576 | 2,880 |

Each producer provides three whole-flow units per commodity; each destination requests four. The numerical decision quantum is one whole-flow unit, and the fail-closed decision budget exceeds full potential supply. **The profit-boundary case uses a test-only steeper continuous marginal commodity-value decline**, forcing the quantity search to stop inside a numerical tranche. Its binary-search iteration count must be positive, so benchmark coverage cannot accidentally disappear. The other three cases intentionally exercise increasingly broad profitable candidate sets.

## Output and interpretation

- `CASE=...`: exact economic transcript signature, quote evaluations, candidate lane count, accepted decisions, number of decision rounds, lane examinations, marginal/integrated commodity value evaluations, quantity-search calls and binary-search iterations.
- `METRICS=...`: **median and maximum** observed elapsed milliseconds and thread-local managed allocated bytes for exhaustive/indexed, with/without diagnostics, across five samples.
- `QUOTE_METRICS=...`: **five independent evaluations** of the pre-turn quote construction stage (medians and maxima of elapsed time and managed allocations), with quote-function call counts and the same deterministic quote-array SHA-256 on all repeats. This excludes allocator work.
- `INDEX_COUNTERS=...`: candidate-indexed alternative search counts. Only the selected source balance and destination+commodity valuation are dirtied following a trade. The pre-turn transport quote is never re-priced within the turn. Exact margin ordering and earliest-canonical-lane tie-breaks match the exhaustive baseline.
- `METRICS=...;mode=indexed` and `mode=indexed-diagnostics`: measure the indexed strategy beside the unoptimized reference; indexed mode is **opt-in** and does not change the reference default.
- `RESULT=PASS_TURN_FREIGHT_MARKET_REFERENCE_BENCHMARK`: all repeated runs produced the same exact economic transcript and deterministic operation counters; **this is not a speed/scale pass criterion**.

Run-to-run measurements can vary with hardware, OS, JIT, GC, CPU throttling and build configuration. Do not treat cross-platform differences, baseline-vs-instrumented overhead or any single result as a statistically significant optimization gain. Instrumentation counters are an optional **non-authoritative** caller-owned object and are never serialized to `WorldState` or included in canonical hashes. On failed calls, counters may describe partial work; the next invocation resets them.

**Further milestones:** design an accelerated candidate-dirty/event-driven approach that preserves exact economic order and final ledger under the **same frozen-freight rule**; compare it with this unoptimized reference on the same machine and benchmark inputs. Measure quote construction and real multimodal route search separately when those contracts exist. Do not compare ledger equality to the intentionally different PR #13/#14 live-congestion market.

## Connectivity matrix: what the graph fixtures do and do not mean

The original benchmark fixtures use every source/destination pair, i.e. a **complete bipartite quoted economic network**. The added `local-ring-sparse` case quotes only three destinations per source (the aligned destination plus its neighboring two, with wrap-around), and `hub-and-local-sparse` uses source/destination index 0 as economic hubs plus two local options per other source. Both are **synthetic deterministically enumerated access patterns**: absent quotations deny an economic lane. Neither is a physical/geographic map, a claim that transport infrastructure was constructed, a historical trade network or a substitute for ADR-064's future technology-aware multimodal graph. They test the computational implications of sparse versus connected candidate sets while keeping a continuous, forecast-derived cost of freight fixed for each turn.

The dense 16×16×5 and 24×24×5 cases increase candidate lanes and are **stress observations**, not game-scale certification. Workload sizes and all illustrative price coefficients are fixed test inputs only. Do not derive a global auto-selection threshold from this small set: tiny markets can favor exhaustive scanning; a future adaptive policy must be proven and measured under representative workloads, using deterministic public selection criteria if ever enabled.

## Candidate-index experiment (follow-up to reference baseline)

The `EconomicTurnFreightCandidateSelectionMode.DependencyIndex` mode stores exactly computed candidate marginal profitability in a balanced ordered set. Its dependencies are **source supply** and **destination+commodity demand**, so accepting one tranche refreshes only candidate lanes sharing either balance. A canonical lane ordinal breaks rational-profit ties exactly as the exhaustive selector did. The algorithm uses no cached freight repricing: the turn-locked quote is unchanged and the independently continuous commodity market value remains authoritative. The same integrals and settlement functions are called for selected parcels.

All four benchmark modes must produce byte-identical trade transcripts, checked before any performance comparison. The outcome is a deterministic greedy approximation under the **same pricing rules**, not an assertion of global economic optimality. The reference loop remains callable and is the correctness oracle. Benchmarked speed or memory benefits must be reported per input/OS; indexing can have setup and heap costs on very small or sparse scenarios. The synthetic workload is not a forecast of full-game performance.
