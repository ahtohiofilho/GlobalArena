# ADR-062 — Opt-in Joint Food Market Clearing with Smooth Marginal Congestion Prices

**Status:** Proposed — pending architecture and cross-platform validation

**Date:** 2026-10-26

## Context

The accepted M4.4 `EconomicMarketAllocationResolver` considers each commodity in an independent network and enforces the legacy `EconomicTransferSignal.RawEffectiveCapacity` as a per-route band limit. This is not a physical law or an appropriate model of the user's envisioned stone roads and pack-animal transport: very high volumes should remain feasible in principle, subject to steeply rising marginal expense.

ADR-061 introduced an exact, threshold-free **shared strategic-edge convex congestion cost evaluator**, with explicit food-unit mass and cost calibration inputs. Its first checkpoint measures the cost of **already selected** trades, but does not influence the quantities the market selects. The next checkpoint must price congestion **before selection**.

## Proposed decision

1. Introduce a separate **opt-in** `EconomicCongestionPricedFoodMarketResolver`. Do **not** change or silently migrate the accepted M4.4 flow algorithm, its fixed-point economics or existing consumers of `EconomicMarketAllocationResult`.
2. Caller must supply:
   - the authoritative world-bound state and generated strategic graph;
   - canonical production/demand flows;
   - one explicit positive `EconomicCongestionDemandBid.RawUnitValue` per demand point and food, in existing raw monetary units per whole commodity flow unit;
   - `EconomicCongestionRouteOffer.RawBaseUnitTransferCost` per directional source/destination pair (no capacity field);
   - a complete food commodity mass catalog and explicitly calibrated ADR-061 profiles on **used** edges;
   - a numerical `RawDecisionQuantum > 0` and a fail-closed `MaxDecisions` computational budget.
3. Jointly consider candidate trades across **all registered food commodities**, in deterministic canonical lane order. Route actual source/destination options across the existing strategic graph with current access rules. No physical edge or route capacity is imposed. A genuinely closed strategic corridor can remain closed under non-logistical strategic-access rules.
4. For each iterative decision, compute a **current marginal net value per whole commodity unit** as the explicit destination bid minus base transfer price minus the sum of edge marginal congestion prices multiplied by that food's **grams per whole flow unit**:

   `marginal_net = raw_bid - raw_base_cost - Σ_e [ MC_e(L_e) * grams_per_flow_unit ]`

   using exact `BigInteger` rationals with no rounding. Select the currently highest positive marginal-value lane. In the presence of the same positive margin, prefer the canonical point/commodity/activity order. Binary-search the largest positive *ending* marginal quantity on that lane (monotone convex marginal cost) and advance by at most the explicit decision quantum. Recompute **shared edge load and competing commodities' prices after every decision**. There is no `L ≥ S` branch, overloaded flag or maximum route volume. Scarce goods and demand remain bounded by the physical quantities declared as supply/demand, not a cargo bottleneck.
5. For every selected tranche, charge the **exact difference of the integral total congestion cost** on each shared edge, plus explicit baseline transfer price, against the explicit bid value. Each tranche must have strictly positive exact net return. Do **not** misreport this non-linear cost as the flat per-unit `EconomicTradeAllocation.RawUnitTransferCost` contract. Use a new typed `EconomicCongestionMarketSelection` outcome with exact rational gross, baseline, congestion and net values.
6. The `EconomicCongestionMarketSearchPolicy.RawDecisionQuantum` is a **numerical approximation granularity**, not freight throughput capacity, per-turn limit, or technically fixed overload point. `MaxDecisions` is a computational safety budget that **throws**, never truncates results, if profitable unprocessed trades remain. No hidden defaults are installed by the engine.
7. Offer an opt-in `EconomicResidentCongestionPricedTurnPhase` that calculates existing workforce-based production and resident-based demand, then performs joint food market clearing **before publishing both typed system events**. Share the stable economic phase identity with accepted alternatives so users cannot accidentally double-run economic markets. The resolver and phase do not change `WorldState`, persistent inventories, physical dispatch or canonical hash v8.

## Accuracy and limits

The deterministic successive-marginal greedy procedure is a **quantized approximation**, **not** a mathematical certificate of global social-surplus optimality for general, multi-edge, multi-commodity convex min-cost flow. In particular it does not backtrack already committed tranches, enumerate alternate paths, solve equilibrium bids/prices for each commodity, or enforce fairness. Calibrations and the computational quantum are caller-defined, and changing the quantum can alter a market's decisions. An exact global convex flow optimizer would need a separate tested algorithm and may not be practical at the planetary scale.

The current solver requires *food-only* flows with explicit physical mass for all participating commodities, to avoid pretending that omitted nonfood freight has no congestion externality. The returned cost is a new exact-rational economic contract; existing food trade coverage, physical-delivery attestations and access observations expecting a legacy `EconomicMarketAllocationResult` do **not** automatically reinterpret it.

No transport mode classes, animal capacity, trucks, railcars, hard edge ceilings, warehouses, mandatory consumption, starvation, GPP, calendar-based deliveries, balancing constants, or default unit-values are created.

## Acceptance

- Unit tests for smooth shared-edge competition between at least two foods, exact integral cost accounting and strictly positive net returns, increasing volume from increased bids with no hard thresholds, zero or nonexistent profitable routes, closed geopolitical edges, missing edge prices/food scales, duplicate/missing bids/offers, deterministic ordering and immutable results.
- A resident-demand authoritative turn with a valid generated world produces two typed audit events without state mutation, and cannot coexist with the legacy economic phase.
- Fail-closed behavior for exhausted decision budgets.
- Full .NET 10 Release and known hash vectors green in GitHub Actions for Windows, Ubuntu and macOS on the exact PR head.

This decision establishes an explicit **endogenous** congestion-priced alternative. It does not claim completion of general cargo logistics or replacement of the legacy market.
