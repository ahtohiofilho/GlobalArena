# Turn-locked freight allocation — reference benchmark

**Status:** First empirical baseline for [Issue #16](https://github.com/ahtohiofilho/GlobalArena/issues/16); observational performance numbers only.

## Economics being measured

The already-merged `EconomicTurnLockedFreightMarketResolver` (PR #19) consumes a **complete freight unit-cost quote computed before each turn**, derived from a caller-provided continuous mathematical function and a pre-turn expected volume. The quote remains unchanged while that turn's raw commodity quantities are allocated. The **commodity's own market marginal value** continues to change with delivered-to-market allocations. Producer reservation costs are explicit and separate. The solver remains a **quantized greedy reference**, not a guaranteed globally optimal water-filling solver or a complete transport infrastructure engine.

This benchmark uses **synthetic-only** continuously differentiable curves, synthetic routes (source/destination keys rather than a multimodal graph), and synthetic supply/demand. They are **not game defaults, historical modes, freight tariff coefficients or balance choices**. Benchmark input size does not represent a target world size. A future production workload suite must be documented separately.

## Repeatable invocation

```sh
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj --configuration Release -- --turn-freight-market
```

Run using .NET 10 Release. The dedicated [workflow](../../.github/workflows/turn-locked-freight-market-benchmark.yml) runs the same command on Ubuntu, Windows and macOS, uploading logs and `dotnet --info` evidence.

Each case runs one initial resolution, one warmup pair and **five measured pairs**: the uninstrumented reference, then the same reference with optional counters enabled. Both forms must yield the **same exact SHA-256 transcript** covering trades, origins, destinations, quantities, commodity marginal values, integrated values, source reservations, freight and net returns. The initial quote function runs *before* timing: quote-evaluation count is reported separately, since this first benchmark measures **allocator cost** and not full turn construction.

| Synthetic case | Sources | Destinations | Commodities | Route quotes | Potential commodity lanes |
| --- | ---: | ---: | ---: | ---: | ---: |
| reference-profit-boundary | 1 | 1 | 1 | 1 | 1 |
| reference-small | 2 | 2 | 2 | 4 | 8 |
| reference-medium | 4 | 4 | 3 | 16 | 48 |
| reference-wide | 8 | 8 | 4 | 64 | 256 |

Each producer provides three whole-flow units per commodity; each destination requests four. The numerical decision quantum is one whole-flow unit, and the fail-closed decision budget exceeds full potential supply. **The profit-boundary case uses a test-only steeper continuous marginal commodity-value decline**, forcing the quantity search to stop inside a numerical tranche. Its binary-search iteration count must be positive, so benchmark coverage cannot accidentally disappear. The other three cases intentionally exercise increasingly broad profitable candidate sets.

## Output and interpretation

- `CASE=...`: exact economic transcript signature, quote evaluations, candidate lane count, accepted decisions, number of decision rounds, lane examinations, marginal/integrated commodity value evaluations, quantity-search calls and binary-search iterations.
- `METRICS=...`: **median and maximum** observed elapsed milliseconds and thread-local managed allocated bytes for baseline and diagnostics runs, across five samples.
- `RESULT=PASS_TURN_FREIGHT_MARKET_REFERENCE_BENCHMARK`: all repeated runs produced the same exact economic transcript and deterministic operation counters; **this is not a speed/scale pass criterion**.

Run-to-run measurements can vary with hardware, OS, JIT, GC, CPU throttling and build configuration. Do not treat cross-platform differences, baseline-vs-instrumented overhead or any single result as a statistically significant optimization gain. Instrumentation counters are an optional **non-authoritative** caller-owned object and are never serialized to `WorldState` or included in canonical hashes. On failed calls, counters may describe partial work; the next invocation resets them.

**Further milestones:** design an accelerated candidate-dirty/event-driven approach that preserves exact economic order and final ledger under the **same frozen-freight rule**; compare it with this unoptimized reference on the same machine and benchmark inputs. Measure quote construction and real multimodal route search separately when those contracts exist. Do not compare ledger equality to the intentionally different PR #13/#14 live-congestion market.
