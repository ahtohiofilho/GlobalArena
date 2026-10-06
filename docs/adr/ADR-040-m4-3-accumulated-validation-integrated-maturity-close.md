# ADR-040 — M4.3 Accumulated Validation, Integrated Maturity & Close

**Status:** Accepted

**Date:** 2026-10-06

## Context

ADR-037 froze the flow-based Economy direction for M4.3.

ADR-038 established the localized resident EconomicPoint/workforce/activity substrate.

ADR-039 established deterministic transient production and demand flow resolution.

M4.3-D performs the accumulated validation needed to decide whether the M4.3 capabilities operate coherently as one Economy foundation before M4.4 introduces global markets, routes and allocation.

## Decision

### 1. M4.3 is accepted as an integrated Economy foundation

The following contracts are accepted together:

- stable EconomicPoint identity and civilization ownership;
- strategic routing anchor;
- localized total workforce;
- sparse Agriculture, Mining and TradeLogistics workforce allocations;
- CommodityId reuse;
- deterministic Agriculture/Mining supply resolution;
- deterministic recurring demand resolution;
- canonical transient flow ordering;
- no mandatory persistent commodity inventory;
- WorldState canonical hash format `7`.

### 2. Production and demand remain transient

M4.3 closes without adding per-cycle supply or demand to resident WorldState.

The canonical resident Economy remains point/workforce/activity state.

Transient production/demand output is an input boundary for M4.4 rather than durable physical stock.

### 3. Cross-platform evidence is accepted

The M4.3-C production commit passed the complete `886/886` GlobalArena Release suite on:

- Windows;
- Ubuntu;
- macOS.

The complete suite includes CanonicalWorldStateHasherTests on the same hash-v7 commit.

The dedicated state-hash workflow did not auto-trigger because the M4.3-C commit did not modify a path included by that workflow's push filter.

This does not invalidate hash-v7 evidence because the complete cross-platform regression executed the canonical hash tests on all three operating systems.

### 4. M4.3 accumulated local validation is accepted

The accumulated M4.3 targeted suite passes:

`113/113`.

The full local Release suite passes:

`886/886`.

Compiler warnings/errors remain:

`0/0`.

### 5. Integrated maturity promotion

The M4.3-owned Economy capability set consists of:

- `Commodity identity, economic points/locality & flow substrate`: `12 GPP`;
- `Workforce, production & demand`: `18 GPP`.

Combined budget:

`30 GPP`.

Before M4.3-D these capabilities carried:

- substrate: `6.00 / 12` at factor `0.50`;
- workforce/production/demand: `9.00 / 18` at factor `0.50`.

Accumulated deterministic validation plus cross-platform consumer evidence promotes both to:

`Integrated — factor 0.70`.

Therefore:

- substrate becomes `8.40 / 12`;
- workforce/production/demand becomes `12.60 / 18`;
- combined M4.3 capability set becomes `21.00 / 30`.

M4.3-D GPP delta:

`+6.00 GPP`.

Economy becomes:

`21.00 / 140 GPP — 15.0%`.

Project becomes:

`293.50 / 1000 — 29.4%`.

The `0.85` Validated factor remains reserved for broader downstream consumer, scale and M4 exit evidence.

### 6. M4.3 is closed

M4.3 exit requirements are accepted for the current scope:

- localized EconomicPoint identity/ownership exists;
- workforce/activity state exists without mandatory cities;
- Agriculture and Mining produce localized deterministic supply under explicit inputs;
- TradeLogistics is executable as an activity identity/workforce allocation;
- production is not persisted as mandatory physical inventory;
- demand is independent from stored goods;
- ownership/world-binding invariants remain active;
- equivalent input ordering is deterministic;
- resident Economy remains covered by canonical WorldState hashing;
- migration from stock semantics is audited;
- accumulated Release regression is green;
- representative cross-platform regression is accepted.

### 7. M4.4 becomes the critical path

The next checkpoint is:

**M4.4-A — Markets, Trade, Routes & Flow Allocation Contract Freeze**

M4.4-A must freeze the global allocation boundary before implementation, including:

- supply/demand input consumption;
- reachable-market semantics;
- strategic graph routing authority;
- transfer cost/capacity signals;
- route dependency and invalidation boundaries;
- financial settlement boundary;
- deterministic allocation semantics;
- performance strategy for multiple producers and consumers.

Water-filling remains a leading semantic hypothesis, but literal packet iteration remains unfrozen.

## Consequences

M4.3 is formally complete.

Global Arena now has an integrated deterministic local Economy foundation that can feed a global market/trade layer without introducing durable commodity inventory.

M4.4 may focus on allocation, reachability, transport economics and settlement rather than redesigning point/workforce/production/demand ownership.
