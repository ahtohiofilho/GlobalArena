# ADR-046 — M4.4 Accumulated Integration Validation & Close

**Status:** Accepted

**Date:** 2026-10-06

## Context

M4.4 established the executable economic route, transfer, market and cache foundations through:

- ADR-041 — M4.4 contract freeze;
- ADR-042 — deterministic strategic route identity, reachability and access;
- ADR-043 — transfer cost and capacity signal;
- ADR-044 — deterministic market allocation and settlement output;
- ADR-045 — route dependency cache and incremental invalidation.

The isolated M4.4 capabilities had reached functional-isolated factor `0.50`.

A simple sum of component-level regressions was not sufficient to claim integrated maturity.

M4.4-F therefore required an explicit end-to-end proof that route/cache state, transfer economics, allocation and settlement execute coherently in one deterministic pipeline.

## Decision

### 1. M4.4 integrated pipeline is accepted

The accumulated M4.4 validation includes:

- M4.4-B route/reachability behavior;
- M4.4-C transfer cost/capacity behavior;
- M4.4-D market allocation/settlement behavior;
- M4.4-E route cache/invalidation behavior;
- M4.4-F direct integrated economic-flow tests.

The accepted integrated chain is:

`route cache -> route resolution -> transfer signal -> market allocation -> settlement`

### 2. Direct integrated behavior is proven

The integration suite proves:

- an adjacent reachable route produces a transfer signal and profitable settlement;
- closing the direct strategic edge invalidates the affected cached route;
- recomputation uses the longer reachable route;
- higher transfer cost reduces net settlement return;
- reopening the edge restores the canonical route and original settlement;
- an unreachable cached relationship suppresses trade;
- reopening a queried frontier edge invalidates the unreachable cache entry and restores trade;
- same-point exchange produces zero-hop, zero-transfer-cost settlement;
- repeated end-to-end execution is deterministic.

### 3. Cache correctness remains dependency-driven

Final traversal dependencies and search dependencies remain distinct.

The integrated tests confirm that access changes propagate through selective route-cache invalidation into transfer cost and final settlement behavior.

No global cache flush is required for the tested dependency changes.

### 4. State authority remains unchanged

M4.4 derived artifacts remain outside `WorldState`, including:

- route cache;
- detailed route-resolution search dependencies;
- transfer signals;
- allocation residual state;
- trade allocation results;
- settlement outputs.

Canonical WorldState hash remains format `7`.

### 5. No production-code change was required for M4.4-F

M4.4-F adds integration evidence only.

The accepted production contracts from M4.4-B through M4.4-E remain unchanged.

### 6. Accumulated regression baseline

Accepted local Release validation:

- direct M4.4-F integration tests: `6/6`;
- accumulated M4.4 tests: `125/125`;
- full suite: `1011/1011`;
- compiler warnings/errors: `0/0`.

A post-commit cross-platform regression on the formal-close commit is required before M4.5 implementation begins.

### 7. Maturity promotion

The M4.4 capability set is:

- Strategic route identity & pathfinding — `16 GPP`;
- Transport/transfer cost & capacity signal — `10 GPP`;
- Market allocation & financial settlement — `14 GPP`;
- Route dependency cache & invalidation — `14 GPP`.

Total M4.4 capability budget:

`54 GPP`.

Before M4.4-F close:

`27.00 / 54 GPP` at factor `0.50`.

After accumulated integration validation:

`37.80 / 54 GPP` at factor `0.70`.

Promotion delta:

`+10.80 GPP`.

Economy becomes:

`58.80 / 140 GPP — 42.0%`.

Project becomes:

`331.30 / 1000 — 33.1%`.

The `0.85` validated factor remains reserved for later M4 exit evidence, including broader cross-domain/systemic validation and applicable performance/memory evidence.

### 8. M4.4 is closed

M4.4 — Markets, Trade, Routes & Flow Allocation is formally closed at integrated maturity.

The next stage is:

**M4.5 — Units, Orders & Strategic Movement**

The next operational checkpoint is:

**M4.5-A — Units, Orders & Strategic Movement Contract Freeze**

## Consequences

M4 now has an integrated deterministic economic path from strategic reachability through transfer economics to settlement, including selective cache invalidation after strategic-edge access changes.

The next work shifts to Warfare-side units, orders and strategic movement without expanding M4.4 scope.
