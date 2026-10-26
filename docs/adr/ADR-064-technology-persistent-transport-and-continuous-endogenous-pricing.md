# ADR-064 — Technology-Persistent Transport Infrastructure and Continuous Endogenous Pricing

**Status:** Proposed — documentation/contract reconciliation pending formal ratification  
**Date:** 2026-10-26  
**Scope:** Consolidation of previously discussed transport and continuous-pricing intentions, conflict audit of implemented contracts, and a prospective interface/performance implementation plan. It does not claim a new economic concept or change code.

## Context and documentation-propagation correction

The technology-persistent infrastructure idea, continuous endogenous prices and pre-evaluated unit costs for computational reuse were discussed before this ADR. **This document records and propagates those pre-existing design intentions; it does not originate them or assert they were already formally accepted in every contract.**

ADR-053 (Accepted) documents an older entirely abstract link model, where investment changes transfer capacity/cost without requiring technology identity. That text no longer reflects the clarified intended *future infrastructure model*: an existing stone road must never automatically become a railway just because investment accumulates. A more advanced type must be constructed/replaced explicitly under its own technology/construction rules. Improvements **within** one technology remain possible when that technology permits them.

ADRs 061–063 and merged PRs #12–#14 introduce exact smooth marginal congestion costs, a joint-food marginal market, and adaptive route choice. Their mathematics and deterministic audit contracts are reusable, but their current inputs are priced strategic edges, **not persisted built transport assets or services**. The accepted M4.4 transfer-signal implementation still uses abstract workforce-derived `RawEffectiveCapacity` and a hard per-route limit, retained only as a backward-compatible baseline. Do not represent it as the new physical infrastructure model.

**Continuity safeguard:** examples of an underfilled ocean freighter, a stone road and a modern railway are *tests of generality*, not mandates for mode-specific pricing formulas, fixed numerical tariffs, predefined operating schedules or a fixed catalogue of modes. **Continuous, endogenous mathematical functions remain the pricing authority.** The previously discussed *transport* unit cost is evaluated **before a turn** from a forecast/expected volume and held constant for that turn's allocation. This fixes the cross-decision feedback problem in sequential water-filling: later parcels must not retroactively reduce the transport cost already used for earlier parcels. **Commodity market value is a separate, endogenous, potentially within-turn variable.** The transport quote is computed, not hand-assigned, and recalculated for subsequent turns.

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
- A service's *transport unit cost used for allocation/settlement* may be calculated by a **continuous, endogenous** function `u_i(theta_i, expected_load, relevant economic state)`. It is evaluated from a **pre-turn snapshot**, not re-evaluated from loads accumulating during the same market-clearing turn. Depending on the eventual transport ruleset, a separate resource-cost curve or operator tariff may exist; neither is automatically the same as the settled transport quote. Do not silently double-charge congestion already included in the quote.
- Occupancy expectation may enter a general mathematical pricing model as one endogenous input. Underutilization **can** lower a quote under some policies, but this is not an unconditional rule for all technologies, nor a manually coded ocean-freighter special case.
- A `TurnTransportUnitCostQuote` (conceptual name, **not an existing C# type**) is the computed **unit transport cost** for service/route `i` and economic turn `t`. Its input is a versioned pre-turn forecast and technology/operating-policy snapshot. The same quote is reused for all compatible parcels in that turn; the financial transport charge for quantity `q` is **`q * unit_transport_cost[i,t]` in declared transport units**, irrespective of the load accumulated by earlier parcels that turn. It does **not** imply that the underlying continuous resource-cost function is linear: `DeltaC_i(L,q)` may still differ from this charge and remain useful for operations/forecasting. This equality applies to the turn-locked *transport charge*, not to a varying instantaneous marginal resource cost. No retrospective resettlement.

The policy must declare which outputs are marginal resource cost, integrated resource cost, average cost, turn-locked **transport unit cost**, and independently formed **commodity market value**, plus precise units, turn identity, forecast snapshot, validity and deterministic arithmetic/rounding. New families must not silently weaken exact settlement or compatibility promises of existing APIs.

### 2.1. Essential separation: transport cost locked per turn vs commodity market value

The pre-evaluation addresses a **logical circular dependency in sequential allocation**, not primarily a memoization/cache opportunity:

1. At the start of economic turn `t`, calculate a deterministic expected freight volume `Vhat[i,t]` from already available information. The forecasting policy must specify provenance (past turns, committed state, explicit expectations), initialization, and units. **Do not use the as-yet-unknown final allocation of turn `t` as an input.**
2. Evaluate the governing **continuous** transport unit-cost function once for each relevant service/route and quote context: `cTransport[i,t] = fTransport_i(Vhat[i,t], technology[i,t], policy[i,t], known_inputs[t])`. The function may reflect economies of scale and utilization; no numeric modal tariff is fixed by this ADR.
3. **Freeze `cTransport[i,t]` during the entire market-clearing turn.** For any eligible tranche of mass `dq`, the transport component of commercial evaluation is `cTransport[i,t] * dq`, with explicitly calibrated unit conversion. Accumulating allocated freight is **not** a trigger to lower/reprice this component during turn `t`; previously settled tranches are never re-costed.
4. Separately calculate the **market value of each commodity** using its supply/demand pricing mechanism, which can evolve as quantities are allocated during turn `t`. A fixed transport unit cost is **not** a fixed commodity sale price, commodity marginal utility, bid or offer value.
5. After the turn, use realized volumes and other relevant outcomes to form the forecast for turn `t+1`; evaluate `cTransport[i,t+1]` with the same continuous rules. Thus the freight cost can rise or fall **across turns** while being stable **within each turn**.

For each traded parcel, the commercial net-value comparison conceptually has separate terms: `marginal_commodity_value(g,d,t,q) - origin_value_or_offer(g,s,t,q) - cTransport(route,t) * declared_transport_units(q)`. The precise source-value/commodity-price policy is separately versioned and **not frozen here**. If a financial transport quote includes congestion, do not add `MC(L)` or its integral again as an intra-turn transport charge.

**Why:** a continuously declining transport unit cost as current-turn loading increases would make a later parcel economically change earlier parcels' freight cost. Holding the **function-derived transport quote** constant for one turn removes that specific feedback loop, without freezing commodity pricing or abandoning continuous transport functions.

**Existing-code boundary:** `EconomicCongestionRouteOffer.RawBaseUnitTransferCost` is already a fixed caller input, but `EconomicCongestionPricedFoodMarketResolver` *also* reprices congestion at every tranche and charges an exact incremental integral. Thus PR #13/#14 do **not** yet implement a complete turn-locked all-in unit transport cost. They remain working opt-in baselines for that *different* economic rule, not proof that this contractual distinction is already implemented.

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

- **Before each economic turn**, compute and freeze transport unit-cost quotes from continuous functions of the forecast/expectation and relevant technology/operating inputs; use one immutable quote snapshot throughout allocation and financial settlement. Derived values may be cached. **Within the same turn, changes in allocated freight do not recalculate unit transport cost.** New observations affect later-turn forecasts and quotes.
- Reuse prices/paths across equivalent candidates and use batched, event-driven allocation, monotone intervals, or analytically determined breakpoints where supported **by the underlying functions**.
- **Between turns**, changes in realized load, expected occupancy, infrastructure, access, service availability or policy version trigger quote re-evaluation and dependent route/cache invalidation as required; newly built or cheaper **previously unused** alternatives must be considered. **Within a turn**, freight-load changes do *not* invalidate the locked quote; any exceptional closure/availability transition requires an explicit, deterministic policy and must not retroactively reprice already settled trades.
- In the **turn-locked transport-cost allocation mode**, calculate the trade's transport charge from the unchanged quote and selected mass/quantity; continue to evaluate **commodity market value** according to its independent demand/supply function when selecting trades. A continuously varying congestion/resource-cost integral may be computed as a *separate* physical/operating cost or forecasting signal, but **must not be added again to the same freight charge**. The implemented PR #13/#14 live-marginal-congestion market remains a separate opt-in reference behavior until an explicit, versioned migration, not a hidden extra per-tranche transport surcharge.
- The current `LargestProfitableQuantity` binary search relies on monotonic marginal profitability in its **current live-congestion algorithm**. A **turn-locked transport quote removes within-turn decreasing transport-unit costs from this decision loop**, addressing the circular-dependency motivation; the commodity market's marginal value may still change and must retain its own tested monotonicity/search conditions. A later *different* policy allowing dynamic transport quotes or nonconvex activation cannot borrow the monotonicity proof without validation.
- Define forecast provenance and deterministic initialization (including the first turn, zero-volume cases and any smoothing of prior-turn observations). **Transport quotes are fixed for the entire current turn and recalculated between turns, not in an intra-turn fixed-point loop.** If a separate future feature wants live congestion repricing, treat it as a distinct policy with its own non-circular solver, not as the default turn-locked transport contract.
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
- Transport unit-cost quotes are reproducible from the same ruleset and **pre-turn forecast**; never use an unexplained hardcoded per-modal price. **Every compatible trade in a turn faces the same service/route unit transport quote** regardless of allocation order or final volume, and `transport_charge = quantity * locked_quote` in declared units. Independent variable resource/congestion-cost integrals remain mathematically correct if computed for accounting or subsequent forecasting, but must not be added to an all-in locked freight charge.
- Lower expected occupancy may produce a lower **next-turn transport quote only if the chosen endogenous function implies it**; realized additional parcels in the *current* turn must never retroactively reduce earlier parcels' unit freight cost. No mandatory shipping-discount rule is imposed.
- Counterexamples to monotone marginal-profitability must be rejected or handled with an appropriate search method.
- Route and price caches remain equivalent to uncached authoritative results after construction, decommissioning, access changes, forecast/price changes and cheaper formerly unchosen alternatives.
- No artificial fixed physical limit at congestion scale `S`; no silent retroactive transport-class conversion; no mixing economic selection with delivered or consumed goods.
- In a two-turn synthetic example where transport-unit cost is a decreasing continuous function of **expected** volume, a rising *realized current-turn* volume cannot change its pre-turn quote or any earlier trade's transport charge; next turn's quote may change according to the updated forecast.
- Hold one transport-quote snapshot constant while changing commodity supply/demand: commodity marginal value and allocation are allowed to vary without changing the locked freight unit cost.
- Compare one-tranche vs multi-tranche allocations (with controlled commodity-side conditions): freight unit quote and cumulative `quantity * quote` remain invariant to tranche ordering; exclude double-counting of incremental congestion integrals in turn-locked mode.
- Demonstrate deterministic replay with exactly specified forecast provenance, first-turn initialization, zero-volume treatment, unit conversions and state/hash-version boundaries.
- Preserve cross-platform .NET 10 regression/hash checks and confirm any new canonical-state version deliberately, not as an incidental side effect.

**Decision checkpoint:** This ADR remains **Proposed** as a propagation/reconciliation record until architecture review ratifies its precise effect on ADR-053's accepted text. No new mathematical principle is being asserted; continuous equations, not preassigned modal unit prices, remain the agreed modeling direction. Separate implementation PRs and benchmark gates still apply.
