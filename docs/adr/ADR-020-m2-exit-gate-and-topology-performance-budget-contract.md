# ADR-020 — M2 Exit Gate and Topology Performance Budget Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.4 closed scaled-refinement validation while deliberately preserving several unresolved M2 exit requirements.

The M2.5 entry audit at baseline:

`e9e50c0ff957b286186e16cc7e9de86a01100256`

confirmed:

- Release build: 0 warnings, 0 errors;
- tests: 366/366;
- no repository mutation;
- 38/38 snapshot hashes valid;
- `GlobalArena.Benchmarks` remains a `Hello, World!` placeholder;
- the existing cross-platform kernel workflow does not execute a benchmark/scalability gate;
- tactical regions still use the three-cell reference graph;
- physical `TacticalCell`-to-shared-border attachment is absent;
- cross-region tactical traversal/navigability is absent;
- multi-family durable refinement lineage is absent;
- hierarchy/refinement mapping maturity remains `0.00`.

Therefore M2.5 cannot be reduced to a performance-only stage.

## Decision

### 1. M2.5 decomposition

M2.5 is decomposed into:

1. M2.5.1 — Exit Gate Requirements & Performance Budget Contract;
2. M2.5.2 — Strategic/Tactical Physical Boundary Attachment & Cross-Region Traversal;
3. M2.5.3 — Hierarchy/Refinement Coverage Decision for Officially Supported Goldberg Families;
4. M2.5.4 — Headless Scalability Benchmark Harness & Baseline;
5. M2.5.5 — Cross-Platform Regression, Exit Audit & M2 Formal Close.

### 2. M2 exit requirements

M2 may close only when evidence demonstrates the accepted M2 topology model is:

- headless;
- deterministic;
- topologically closed;
- navigable;
- explicit about strategic-to-tactical parent/child semantics;
- explicit about physical/shared boundary attachment;
- able to traverse neighboring tactical regions through the canonical boundary contract;
- explicit about the Goldberg refinement/family scope officially supported for M2;
- covered by quantitative scalability evidence;
- covered by cross-platform regression.

Passing a performance benchmark alone is insufficient.

### 3. Canonical M2 topology load cases

The first quantitative M2 topology baseline uses three family-representative large cases:

- Class I: `G(16,0)`, `T=256`, 2562 cells, 7680 edges, 5120 vertices;
- Class II: `G(10,10)`, `T=300`, 3002 cells, 9000 edges, 6000 vertices;
- Class III: `G(12,7)`, `T=277`, 2772 cells, 8310 edges, 5540 vertices.

These are M2 acceptance load cases.

They are not a declaration of final V1 planet-size limits.

### 4. Benchmark workload

M2.5.4 must measure construction from scratch of the complete production workload required by the final accepted M2 topology after M2.5.2 and M2.5.3.

At minimum the workload includes:

- `StrategicTopology`;
- all `TacticalRegion` instances;
- all `SharedBorderBand` instances;
- `StrategicTacticalBorderAggregate`;
- any additional production structure required for physical boundary attachment or cross-region traversal by M2.5.2/M2.5.3.

A benchmark that measures only the pre-M2.5 three-cell reference pipeline cannot close M2 if later subcheckpoints add required topology structures.

M2.5.3-B further specializes the final workload rule:

- the Class I `G(16,0)` acceptance case must include the official scale-6 physical hierarchy workload, including its `G(96,0)` fine topology and required physical incidence/traversal structures;
- Class II `G(10,10)` and Class III `G(12,7)` remain strategic/logical generator acceptance cases because Class II/III physical hierarchy is outside the official M2 support boundary;
- benchmark reports must state this workload distinction explicitly and must not imply Class II/III physical hierarchy coverage.

The existing hard budgets remain unchanged until measured evidence justifies either optimization or a governed budget revision.

### 5. Measurement protocol

For each canonical load case:

- Release build;
- headless execution;
- .NET 10;
- one warmup;
- five measured samples;
- same process for warmup and measured samples;
- record OS;
- record runtime version;
- record process architecture;
- record elapsed milliseconds;
- record managed allocated bytes;
- validate output counts/coverage before accepting the sample.

The benchmark harness implementation is deferred to M2.5.4.

This ADR is framework-agnostic: BenchmarkDotNet is not required by contract.

### 6. Hard M2 topology baseline budgets

For each canonical load case:

- median elapsed time must be `<= 1000 ms`;
- maximum elapsed sample must be `<= 2000 ms`;
- median managed allocation must be `<= 192 MiB`;
- maximum managed allocation sample must be `<= 256 MiB`.

A single canonical case exceeding a hard budget blocks the M2 exit gate.

Budgets cannot be silently relaxed.

A budget change requires:

- explicit architectural/governance update;
- new evidence;
- documented reason;
- rerun of affected acceptance gates.

### 7. Audit evidence used to set the first envelope

The entry audit executed a threshold-free observational probe against the current reference workload.

Large cases observed:

- `G(16,0)`: median 84.784 ms, max 116.604 ms, median allocation 63,876,136 bytes;
- `G(10,10)`: median 65.171 ms, max 70.425 ms, median allocation 75,541,888 bytes;
- `G(12,7)`: median 58.798 ms, max 82.909 ms, median allocation 62,493,848 bytes.

The hard budgets intentionally include broad headroom over this first observation to reduce false failures caused by CI/runtime variation while still detecting large regressions.

These observations do not constitute acceptance evidence because the workload may change in M2.5.2/M2.5.3.

### 8. Benchmark invalidation rule

If M2.5.2, M2.5.3 or any later pre-close change modifies the production topology workload required by M2:

- prior performance results are informational only;
- M2.5.4/M2.5.5 must rerun the benchmark against the new workload.

### 9. Scope boundaries

This contract does not:

- define final V1 world-size budgets;
- prove high-resolution tactical scalability;
- close `RISK-002 — Tactical resolution scalability`;
- close `RISK-012 — Memory footprint`;
- define a universal Goldberg lineage mapping;
- replace physical boundary attachment;
- replace cross-region navigability.

### 10. Progress accounting

M2.5.1 does not promote GPP.

Current totals remain:

- GPP: `109.50 / 1000`;
- Global Progress: `11.0%`;
- Planet Topology: `39.50 / 90 — 43.9%`;
- `Strategic ↔ tactical hierarchy/refinement mapping`: `0.00`;
- `RISK-003`: HIGH.

### 11. M2.5.4 governed scale-envelope revision

M2.5.4 produced two additional evidence sets after the original M2.5.1 contract:

Performance decomposition:

`GlobalArena-Evidence-M2.5.4-A-R4-READONLY-PERFORMANCE-DECOMPOSITION-DIAGNOSTIC-20260928-182725.zip`

SHA-256:

`537c207a16a300af343e4927fddfda0284dd64c11b35623734e73e0146a4cc0f`

Scale-envelope audit:

`GlobalArena-Evidence-M2.5.4-B-R1-READONLY-SCALE-ENVELOPE-AUDIT-20260928-185801.zip`

SHA-256:

`431e433f0c880719f8b431aea594e71acd0e06808a7f9d3f936d9eb6bfa3db5f`

The evidence shows:

- full physical `G(16,0) -> G(96,0)` is topologically valid and completes;
- its measured performance exceeds all four original blocking budgets;
- fine `G(96,0)` topology generation dominates that workload;
- fine topology contributed about `87.74%` of managed allocation and `75.34%` of elapsed time in the decomposition run;
- scale-6 managed allocation per fine tile remained approximately linear across `k=4,8,12,15`;
- allocation-per-tile spread was `1.010`;
- time-per-tile spread was `1.696`.

The original M2.5.1 workload selection is therefore revised in a governed way. The hard thresholds are not relaxed.

#### 11.1 Blocking M2 product-acceptance lane

The permanent M2.5.4 harness must include these blocking cases:

- Class I strategic/logical: `G(15,0)`;
- Class II strategic/logical: `G(7,7)`;
- Class III strategic/logical: `G(14,1)`;
- Class I physical semantic acceptance: `G(4,0) -> G(24,0)`, scale 6.

Each strategic/logical case includes the accepted logical M2 workload:

- `StrategicTopology`;
- all `TacticalRegion` instances;
- all `SharedBorderBand` instances;
- `StrategicTacticalBorderAggregate`.

The physical acceptance case additionally includes:

- authoritative coarse and fine `StrategicTopology`;
- complete `PhysicalTacticalIncidenceMap`;
- physical incidence validation required by the production mapper;
- the same logical structures materialized from the authoritative coarse topology.

The unchanged hard budgets apply to every blocking case:

- median elapsed `<= 1000 ms`;
- maximum elapsed sample `<= 2000 ms`;
- median managed allocation `<= 192 MiB`;
- maximum managed allocation sample `<= 256 MiB`.

#### 11.2 Non-blocking engineering stress lane

The permanent harness must also retain:

`G(16,0) -> G(96,0)`

as a full physical stress baseline.

Stress-case topology correctness, output validation and deterministic semantics remain mandatory.

Its elapsed/allocation figures are recorded and compared over time, but exceeding the product-acceptance thresholds does not by itself block M2.

A crash, invalid topology, broken lineage/incidence semantics or determinism regression in the stress case still blocks the corresponding correctness gate.

#### 11.3 Provisional product scale hypothesis

Current product planning expects:

- strategic Goldberg size around `m+n <= 15`;
- tactical density up to roughly 12 rings in some contexts and normally less.

These are hypotheses, not frozen V1 limits.

Final scale will be selected only after later visual and gameplay validation.

The strategic and tactical maxima cannot be treated as independent simultaneous requirements. Their product determines the physical workload.

The M2 Class I scale-6 hierarchy is a semantic/refinement contract and must not be interpreted as the final V1 tactical density.

#### 11.4 Combined envelope rule

M2.5.4 benchmarks a combined scale envelope.

The benchmark must not infer a final V1 planet size from one topology parameter alone.

Larger technically valid workloads remain desirable as robustness headroom and may be retained as stress cases even when they exceed the blocking product-performance envelope.
## Consequences

Positive:

- M2 exit requirements become explicit and auditable;
- performance thresholds are defined before the benchmark harness is implemented;
- family-representative load cases are frozen;
- benchmark evidence cannot silently survive a material workload change;
- performance cannot conceal missing topology semantics.

Negative:

- M2 remains open;
- boundary attachment and navigability still require production work;
- refinement coverage still requires an explicit decision;
- first performance thresholds may need revision if later M2 production semantics materially increase the workload.

## Invariant

M2 can close only on evidence produced against the final accepted M2 topology workload. Performance evidence and topology-semantic evidence are both mandatory; neither substitutes for the other.
