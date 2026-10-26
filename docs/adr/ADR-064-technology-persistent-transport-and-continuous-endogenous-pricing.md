# ADR-064 — Technology-Persistent Transport Infrastructure and Continuous Endogenous Pricing

**Status:** Proposed — documentation/contract reconciliation pending formal ratification  
**Date:** 2026-10-26  
**Scope:** Consolidation of previously discussed transport and continuous-pricing intentions, conflict audit of implemented contracts, and a prospective interface/performance implementation plan. It does not claim a new economic concept or change code.

## Context and documentation-propagation correction

The technology-persistent infrastructure idea, continuous endogenous prices and pre-evaluated unit costs for computational reuse were discussed before this ADR. **This document records and propagates those pre-existing design intentions; it does not originate them or assert they were already formally accepted in every contract.**

ADR-053 (Accepted) documents an older entirely abstract link model, where investment changes transfer capacity/cost without requiring technology identity. That text no longer reflects the clarified intended *future infrastructure model*: an existing stone road must never automatically become a railway just because investment accumulates. A more advanced type must be constructed/replaced explicitly under its own technology/construction rules. Improvements **within** one technology remain possible when that technology permits them.

ADRs 061–063 and merged PRs #12–#14 introduce exact smooth marginal congestion costs, a joint-food marginal market, and adaptive route choice. Their mathematics and deterministic audit contracts are reusable, but their current inputs are priced strategic edges, **not persisted built transport assets or services**. The accepted M4.4 transfer-signal implementation still uses abstract workforce-derived `RawEffectiveCapacity` and a hard per-route limit, retained only as a backward-compatible baseline. Do not represent it as the new physical infrastructure model.

**Continuity safeguard:** examples of an underfilled ocean freighter, a stone road and a modern railway are *tests of generality*, not mandates for mode-specific pricing formulas, fixed tariffs, predefined operating schedules or a fixed catalogue of modes. **Continuous, endogenous mathematical functions remain the pricing authority.** A precomputed unit price is a *derived evaluation or quote* of such a function for known inputs/context, intended to reduce repeated work in allocation—not an arbitrary externally imposed substitute for the function.

## Reconciled design direction and contracts pending ratification

### 1. Separate geography, technology, installed asset, service and market

- **Geography/topology:** immutable or versioned strategic cells and adjacency indicate possible physical corridors, not automatically constructed roads/railways.
- **Technology type:** an identity in an extensible, versioned ruleset; defines the feasible construction, operation and improvement family. No universal mandatory enum or complete transport-mode catalogue is frozen here.
- **Installed infrastructure asset:** a persistent, world-bound instance with its own identity, location, technology type, construction/maintenance state and approved changes. The asset's *technology identity must not change as a consequence of cumulative investment*. Building a different technology creates an explicit new/replacement asset/transition, never a silent upgrade.
- **Transport service/offer:** an economic operation using one or several built assets, with access/handling/transfer terms and pricing functions. Services may be aggregated; no compulsory individual ship, wagon, truck or timetable simulation is implied.
- **Market:** allocates goods among available services and routes, using calibrated, endogenous prices and exact or explicitly bounded economic arithmetic. It does not infer that investment spontaneously changed asset technology.

Parallel technologies on the same geographic corridor must be representable independently (multigraph/expanded multimodal service graph, with explicit transfer interfaces). Edge identity, service identity and consumed infrastructure identity must be auditable, even if the first implementation only exposes part of that contract.

### 2. Continuous functions are authoritative; evaluated unit prices are derived

Let `theta_i` denote explicit, versioned technological and economic inputs for service/infrastructure `i`, including approved asset properties, operating conditions, forecast state and pricing policy. Let `L_i` be relevant accumulated load and `q >= 0` a candidate additional quantity in declared units.

- A differentiable marginal resource/operating/congestion cost can be represented as `m_i(L; theta_i)`; the exact increment is `DeltaC_i(L,q) = integral_L^(L+q) m_i(x; theta_i) dx`.
- The already implemented congestion family `m_e(L)=b_e+a_e(L/S_e)^2` and its exact `BigInteger` rational integral remain valid first-class policies. Here `S_e` is a characteristic **cost scale**, not a maximum traffic volume or infrastructure-upgrade level.
- A service's *offered unit price* or tariff may be calculated by a **different, continuous, endogenous** pricing function `p_i(theta_i, expectations, market conditions, load)`. The tariff paid and the operator's real marginal resource cost are **not automatically identical**; do not silently double-charge a congestion term included in an offer.
- Occupancy expectation may enter a general mathematical pricing model as one endogenous input. Underutilization **can** lower a quote under some policies, but this is not an unconditional rule for all technologies, nor a manually coded ocean-freighter special case.
- A `UnitPriceQuote` (conceptual name, **not an existing C# type**) is a value **computed from** a versioned continuous function and a specified snapshot of inputs. It may be reused while those dependencies are unchanged. It is never a new exogenous authority, never an indefinite fixed marginal price, and never permission to replace `DeltaC_i` with `q * p_i(L)` for arbitrary `q` when the function varies with `q`.

The policy must declare which outputs are marginal cost, integrated cost, average cost and commercial unit quote, plus precise units, applicability interval, dependency revisions, validity and deterministic numerical/error guarantees. New families must not silently weaken exact settlement or compatibility promises of existing APIs.

### 3. Preserve economic math while optimizing allocation

The computational goal is to **reduce function evaluations, path searches and iterations**, not substitute fixed hand-authored prices for continuous price formation. This is an opportunity to evaluate on the existing code, **not a claim that the concept or all optimizations are newly invented or absent**.

**Verified implementation inventory (as of PR #14 merged):**

| Solver/component | Already implemented | Not yet implemented in that component |
| --- | --- | --- |
| Legacy `EconomicMarketAllocationResolver` (ADR-044) | Bounded deterministic supply/demand/transfer bands and a residual min-cost network with Bellman-Ford augmentations; no per-raw-unit simulation | Technology/service identity and continuous-function-derived quote cache; retains legacy `RawEffectiveCapacity` |
| `EconomicCongestionPricedFoodMarketResolver` (ADRs 062–063) | Joint-food successive-marginal selection, configurable `RawDecisionQuantum`, exact `BigInteger` integrals and binary search on monotone marginal profitability | Persistent asset/service inputs, precomputed reusable endogenous price quotes, incremental dirty-lane repricing and performance evidence |
| `StrategicMarginalCongestionRouteResolver` (ADR-063) | Deterministic current-load cheapest-path search | Reuse of adaptive shortest-path results across successive market tranches; currently invoked anew per viable lane/decision |
| `StrategicEconomicRouteCache` (ADR-045) | Sparse BFS reachability/access dependency invalidation | Correct cache for weighted dynamic service price changes, construction and newly attractive previously unused alternatives |

The implemented trade solver is a **greedy quantized approximation**, not interchangeable by name with a continuous equilibrium water-filling solution. Benchmark both existing baselines before selecting any new acceleration strategy.

- Evaluate and cache stable coefficients, forecasts, exact integrals or function-derived quotes for a pricing epoch/snapshot.
- Reuse prices/paths across equivalent candidates and use batched, event-driven allocation, monotone intervals, or analytically determined breakpoints where supported **by the underlying functions**.
- On changed relevant edge load, occupancy forecast, active infrastructure, access, service availability or policy version, recompute or invalidate every dependent result. A newly built or repriced *previously unused* alternative may become optimal: invalidating only traversed edges is insufficient.
- Re-evaluate exact marginal/integral cost when deciding a tranche and settling its value, or use a separately approved **bounded approximation** with explicit worst-case error and reference-oracle validation.
- The current `LargestProfitableQuantity` binary search relies on a nonincreasing marginal net return with increasing batch load (in the existing convex-congestion regime). An endogenous quote with decreasing price or nonconvex activation/scale effects **does not automatically satisfy this precondition**. New solvers must enforce/verify relevant monotonicity, split into valid intervals, or use a different proven procedure; never reuse binary search without checking.
- A lagged/frozen forecast used to calculate quotes is an explicit policy snapshot, not a replacement for current-load congestion settlement. If tariffs feed back within a market cycle, define deterministic price/allocation update ordering, convergence/termination and fail-closed behavior; do not presume equilibrium.
- The successive-marginal greedy clearing in PRs #13/#14 remains an **approximation**, not a general welfare-optimal water-filling or all-route equilibrium solver.

### 4. Investment and capacity semantics

- Distinguish construction expenditure, maintenance and operational spending, plus allowed **within-technology** improvements. No unlimited accumulation of anonymous capacity converts an asset into another technology.
- The technology ruleset may constrain feasible inputs/operations; any physical impossibility must be explicit and physically motivated. Avoid resurrecting an arbitrary congestion cliff at `L=S`. Exceeding a characteristic cost scale remains economically expensive, **not automatically forbidden**.
- A future real-world-like service may have true discrete availability or departure constraints; that is a *separate approved physical-service contract*, not an implied limit on all geographic corridors.
- Reconcile the small-exchange baseline of M4.4 by explicitly representing whatever basic/local movement the ruleset allows; do not create free, universal implicit modern infrastructure.

### 5. Versioning, contracts and provenance

The infrastructure catalogue, authoritative asset state and service-provider interfaces must remain separate from the renderer and independent of Godot. Any persistent asset data added to `WorldState` requires an explicit canonical state/hash version decision and migration; no silent v8 mutation.

Audit each realized market tranche sufficiently to link its selected route/service/asset dependencies and the function-policy version or quote snapshot used for pricing, while respecting existing provenance boundaries (commercial selection is not a proof of physical delivery or consumption).

Do not presuppose a final vehicle inventory, transport-company simulation, complete list of modal classes, unit tariff, logistic behavior coefficient, demand elasticity or calibration data in this ADR.

## Reconciliation with prior accepted/proposed ADRs

| Contract | Existing scope | Proposed forward treatment |
| --- | --- | --- |
| ADR-041 (Accepted) | Deterministic market, strategic routing and deferred deep infrastructure | Preserve historical M4.4 baseline; add built-asset/service input seam prospectively |
| ADR-043 (Accepted) | Workforce-derived effective capacity; uniform cost per hop; hard legacy band limit | **Legacy-only** until an explicitly versioned migration; not the future model's physical road/rail capacity |
| ADR-044 (Accepted) | Finite residual min-cost network and deterministic bands per commodity | Keep baseline; assess reuse of its batching/residual ideas without inheriting abstract capacity semantics |
| ADR-045 (Accepted) | Sparse route cache and dependency invalidation for unweighted BFS | Extend to all relevant price/topology/access/service versions; improve treatment of newly cheaper *untraversed* alternatives |
| ADR-053 (Accepted) | Abstract logistics with no mandatory transport-mode identity | **Reconcile an older documented assumption with the previously discussed technology-persistent intention:** after review, supersede §4 where inconsistent; retain emergent hubs, hierarchical flows, market sparsification and non-micromanagement |
| ADR-061 (Proposed, PR #12 merged) | Exact smooth shared-edge congestion, no capacity cliff | Reuse continuous marginal/integral cost mathematics; eventually parameterize by built infrastructure/service |
| ADR-062 (Proposed, PR #13 merged) | Joint food market, exact integral settlement, quantified greedy steps | Preserve validated behavior as opt-in reference; introduce price-function-derived service inputs in a later reviewed PR |
| ADR-063 (Proposed, PR #14 merged) | Dynamic cheapest marginal route on strategic graph | Keep deterministic Dijkstra core; later extend to multi-edge assets, service transfers and technology-aware dependencies |

This documentation reconciliation does **not** rewrite historical ADRs, invent a new strategy, or claim retroactive formal acceptance where not recorded. The conflicting accepted text requires an architecture review/acceptance checkpoint to establish which version is binding. Current code remains authoritative for the *implemented legacy/opt-in baselines* until replaced by validated migrations.

## Implementation checkpoints (not part of this documentation PR)

1. **Contract/model PR:** identifiers, versioned technology catalogue, built-asset network, transport-service pricing-function interfaces, explicit invariants and sample synthetic test fixtures, with no assumed final technology list.
2. **Continuous pricing adapter PR:** map a service's authoritative functions to exact integral/marginal results and optional memoized unit quotes; keep accepted PR #12–#14 outputs unchanged under their previous opt-in settings.
3. **Allocation/route integration PR:** multimodal alternatives, priced interchanges, correct invalidation/repricing, and no implicit road-to-rail conversion. Use reference small-world solver to check economic results.
4. **Persistence/migration PR:** explicit built-infrastructure authoritative state and hash/version changes, after separate compatibility review.
5. **Performance benchmark gate:** compare reference vs optimized versions at multiple world/market sizes. Measure wall-clock/CPU, allocations, function/integral evaluations, route-query count, invalidation work, memory, determinism, net-return/conservation and approximation error. No unmeasured speedup claims.

## Acceptance and non-regression gates

- Two independent technologies may coexist on the same corridor and retain stable identities under within-technology investment; a new technology requires an explicit construction/replacement event.
- Function-derived quotes are reproducible from the same ruleset/state/forecast inputs; never use an unexplained hardcoded per-modal price. For varying cost functions, integrated settlement matches the reference analytic integral or a declared bounded numerical method.
- Low demand/forecast conditions can change a service quote **only if the chosen endogenous function implies it**; no mandatory shipping-discount rule is imposed.
- Counterexamples to monotone marginal-profitability must be rejected or handled with an appropriate search method.
- Route and price caches remain equivalent to uncached authoritative results after construction, decommissioning, access changes, forecast/price changes and cheaper formerly unchosen alternatives.
- No artificial fixed physical limit at congestion scale `S`; no silent retroactive transport-class conversion; no mixing economic selection with delivered or consumed goods.
- Preserve cross-platform .NET 10 regression/hash checks and confirm any new canonical-state version deliberately, not as an incidental side effect.

**Decision checkpoint:** This ADR remains **Proposed** as a propagation/reconciliation record until architecture review ratifies its precise effect on ADR-053's accepted text. No new mathematical principle is being asserted; continuous equations, not preassigned modal unit prices, remain the agreed modeling direction. Separate implementation PRs and benchmark gates still apply.
