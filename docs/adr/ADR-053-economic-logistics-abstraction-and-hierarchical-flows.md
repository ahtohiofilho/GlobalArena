# ADR-053 — Abstract Logistics, Emergent Hubs and Hierarchical Economic Flows

**Status:** Accepted — product/architecture direction (implementation pending)
**Date:** 2026-10-11
**Supersedes:** No historical ADR. Refines ADR-037 and ADR-041 without claiming their implementation is complete.

## Context and goals

Global Arena is a turn-based economic/geopolitical strategy game with comparatively lean warfare. Historically, all-to-all route searches and full per-turn recalculation were expensive. The economy must offer meaningful geography and policy choices without transport micromanagement. The authoritative C#/.NET kernel stays platform-independent and headless, separate from presentation.

## Binding design

1. Economy uses persistent `EconomicPoint` identities, not mandatory city, market, customs or hub entities. There are two intended **location roles**: productive economic centers and ports. Port functionality may coexist with production and population; urban development and hub status emerge from workforce, population, infrastructure, throughput and geography. The original `EconomicPoint` domain abstraction remains authoritative. Customs points are not a separate economic-location category; border policy/tolls may be expressed as edge/access modifiers if introduced later.
2. Three economic activities remain Agriculture, Mining and Trade/Logistics. Population and labor emerge/grow and allocate by opportunities and demand. Agriculture may be diffuse; mining is tied to deposits. Production and demand are transient each economic cycle; financial settlement is the output; there is no mandatory physical stock persistence. Preserve ADR-037's small exchange without specialized infrastructure and increasing marginal cost near throughput capacity.
3. The strategic graph is the global routing authority. The tactical graph and renderer are not used to recompute economy globally. No per-producer graph cloning and no exhaustive all-pairs shortest-path matrix per turn. Employ sparse economic points, candidate markets, cache and dependency/version-aware invalidation. Recompute broadly if correctness requires it (e.g. network cost decreases can improve previously unrelated routes).
4. Abstract transport: **do not introduce mandatory road/rail/ship/mode enums, vehicle inventories, stations or train schedules.** Links and economic points provide access/reachability, effective capacity, cost, throughput saturation and policy constraints. Infrastructure investment changes these attributes, not an explicit physical modal classification. The player manages economic/geopolitical effects rather than transport vehicles.
5. **No commodity-specific logistics profile in initial scope.** Items may differ in production, demand and market price, but share the same logistics policy/edge cost function for equal quantity, barring later evidence-based scope change. Hub choice can nevertheless differ **by item** as a consequence of item-specific market attractiveness, volumes, supply/demand, congestion and net returns; do not hardcode hub-per-commodity classes.
6. Passage is distinct from service/access. A high-throughput corridor passing a small center does not imply cheap loading/unloading or direct service there. Local centers normally access a dominant hub; a second hub may be considered when generalized access costs are competitive. Dominance uses effective cost, not geographic distance alone. Candidate thresholds, hysteresis, capacity and exception criteria are calibration parameters, not frozen constants.
7. Hubs are emergent and reversible. Their **derived logistics aggregations** consolidate smaller centers for routing; individual points retain population, production and demand identities and local updates. Default commerce is hub-mediated, with narrowly justified direct paths and dual-hub competition. Hubs are not compulsory physical stopovers for every consignment, nor authoritative replacement of their constituent economic points.
8. The model supports large-volume consolidation and nonlocal exports when net returns exceed local options. Market prices respond to destination supply. Maintain the accepted ADR-041 deterministic allocation and settlement boundaries. The previous water-filling idea is a conceptual candidate, **not a frozen operational algorithm**, especially where setup/aggregation costs create nonconvexities. Do not simulate millions of physical packages, vehicles or independent route searches.
9. Logistics cost model remains to be calibrated. It must allow transfer cost, capacity-sensitive congestion and potential shared activation/consolidation economics without double-charging shared infrastructure. Permanent infrastructure investment must be distinguished from per-cycle operational costs. No mandatory fixed charge per product or per unit is frozen here.
10. Algorithm selection remains open: candidate generation, hub/region-level flows, iterative allocation and reference small-world solver. Require deterministic ordering/fixed-point-compatible signals, cache evidence, stress benchmarks, incremental-vs-reference correctness tests, conservation, capacity compliance and controlled turn times. Keep graphical presentation read-only for the authoritative economy.

## Non-goals and open calibration

No current binding choice of exact price function, objective function, solver, threshold, route candidate count, congestion curve, detailed pricing policy, hub clustering rule, or fixed performance deadline. Discussed numerical examples were illustrative, not accepted balance parameters. Prior discussions of uniform final item price per market and supply-responsive pricing remain design hypotheses unless reconciled with an accepted ADR; this document does not retroactively assert that a specific formula has been implemented.

## Acceptance gates

- Tiny-world reference cases demonstrate local vs distant profitable export, hub competition, changed access, congestion, emergence/demotion and edge cost decreases.
- Measured route-query counts, CPU time and memory are compared with a simple exhaustive oracle on bounded worlds.
- Identical state/input/seed produces deterministic canonical flow, settlement and state hash.
- UI conveys capacity, access and net return without demanding transport micromanagement.

## Compatibility

ADR-037 and ADR-041 remain historically authoritative for previously accepted contracts. This ADR narrows future product direction where their detailed mechanics were left open. Implementations changing existing hashes/contracts require their own migration and QA gate.
