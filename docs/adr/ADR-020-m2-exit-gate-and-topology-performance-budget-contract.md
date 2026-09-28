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
