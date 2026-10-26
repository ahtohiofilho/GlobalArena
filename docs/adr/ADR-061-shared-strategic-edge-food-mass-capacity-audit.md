# ADR-061 — Smooth Shared-Edge Congestion Costs Without a Hard Throughput Limit

**Status:** Proposed — pending architecture review and cross-platform regression

**Date:** 2026-10-26

## Context

ADR-053 accepts **abstract transport, not a mandatory vehicle or mode simulation**. Logistics is a fluid strategic service subject to increasing marginal cost as traffic grows. The existing market currently resolves each commodity separately and `EconomicTransferSignal.RawEffectiveCapacity` is a per-commodity solver constraint, **not** a credible hard physical carrying limit across different commodities. A rigid shared corridor capacity would incorrectly make freight impossible at one arbitrary point, even though sufficiently large traffic is still possible at increasing logistical cost.

The example of pack animals on a stone road illustrates the intended behavior: carrying a modest load is economical, while handling vastly larger freight volumes by the same general arrangement becomes extremely costly and may induce choices of better infrastructure, routes or reduced traffic. No specific vehicle or transport class is required in code.

## Proposed decision

1. Replace the previous PR #12 hard-overload audit with a smooth, **threshold-free convex transport cost**. For each strategic edge `e`, an opt-in, caller-defined profile supplies:
   - `S_e > 0`: a **characteristic throughput scale** in grams per economic cycle, **not a capacity ceiling**, allowed traffic limit or discontinuity;
   - `b_e >= 0`: baseline raw marginal transport cost per gram;
   - `a_e > 0`: additional raw marginal cost per gram at load `S_e`. These are *explicit ruleset calibration inputs*, not global/default balance values.
2. At nonnegative physical mass load `L` on that edge, define the **marginal** raw cost per gram by the continuously differentiable function:

   `MC_e(L) = b_e + a_e * (L / S_e)^2`.

   There is **no overload status, capacity cliff, clipping, routing prohibition or penalty switch** when `L` crosses `S_e` (or any other load). `S_e` merely normalizes the smooth curve. Its derivative `2 a_e L / S_e^2` is nonnegative, increasing with traffic.
3. The integral, useful for a later joint market optimization, is:

   `TC_e(L) = b_e*L + a_e*L^3 / (3*S_e^2)`.

   This is a **convex total cost** whose marginal cost is the formula in (2). No retroactive charge is added to already committed economic settlements; the evaluated cost is a **candidate for a future simultaneous allocation objective**, not a current correction of M4.4.
4. Reuse `StrategicEconomicRouteResolver` against a matching world-bound `WorldState` and current edge-access snapshot. Route **only source/destination pairs present in the selected market**, caching by direction. A truly closed strategic edge remains prohibited by explicit geopolitical/access rules, which is separate from congestion pricing.
5. Use ADR-057's caller-defined `FoodCommodityDefinition.GramsPerFlowUnit` to convert each selected trade's raw quantity to **exact micrograms**, then sum food loads from all selected trades traversing each edge. Both cost formulas are evaluated using **exact, canonical `BigInteger` rational arithmetic**, with no loss from floating-point rounding, clipping, 64-bit overflow or special threshold branch. Inputs and evaluated results are deterministic.
6. Require an explicit congestion profile for every **used** edge; omission is unknown cost, **not free movement**. Reject unknown or duplicate profile edges and a world-binding mismatch. Reject markets with nonfood/undeclared physical-unit commodities, since ignoring their shared traffic would give a falsely low congestion cost.
7. Produce an immutable, sorted `FoodStrategicEdgeCongestionResult` with per-used-edge shared load, **marginal** and **integrated total** transport costs. It contains **no overloaded flag, maximum capacity or overage**. An empty market or same-cell exchange does not require a strategic-edge profile, although local service/handling costs may be modeled separately.
8. Remain an **opt-in pure cost evaluator** without changing current market selections, `WorldState`, hash v8, in-flight shipments or the accepted M4.4 allocation mechanism. The next separate checkpoint can incorporate marginal costs into a joint, multi-commodity optimization **before** trade settlement, rather than only describing them after the fact.

## Interpretation and tradeoffs

- The cost is per gram and the physical mass normalization of each food is explicit. `BigInteger` rational raw-cost units are *not yet* transparently interchangeable with the existing per-commodity `EconomicTradeAllocation.RawUnitTransferCost`; a future pricing integration must account for grams per whole flow unit and the fixed-point unit convention.
- The quadratic marginal form is a **modeling choice, not an empirically calibrated road/vehicle constant**. Other smooth convex curve families may be adopted through a separately tested ruleset contract; changing parameters never introduces a hard logistics maximum.
- This deliberately does **not** equate effective cost with distance, select transport modes or promise that unlimited transport can be funded. Route closures and market profitability can still prevent trade for reasons **other than a technical capacity cliff**.
- Shared edges are used as a geography-sensitive reporting basis, not a guarantee that the current per-commodity market actually satisfies a simultaneous economy-wide objective.
- No road/rail/mule inventories, fixed pack-animal carrying capacities, stock balances, mandatory storage, delivery assurance, food access/consumption, artificial settlement population, tariffs, default food IDs, calendar conversion or GPP credit is introduced.

## Acceptance

- Exact marginal costs at zero, half, one, two and many multiples of the characteristic scale; smoothly increasing marginal cost with no special behavior at `S`; convex integrated total cost and exact rational comparisons.
- Two commodities traded over the same generated-world strategic edges contribute **combined physical load**. Test using the real resident-food market, plus deterministic profile reordering, closed/unknown routes, missing/duplicate profile declarations, same-cell trade, unknown food mass, larger-than-`UInt64` exact cost values and unchanged canonical state/hash.
- Full .NET 10 Release kernel and canonical hash suites succeed on Windows, Ubuntu and macOS for the updated PR head.
- Explicit review of the fact that this PR offers the **cost function and its exact aggregation**, not yet the new market optimizer that internalizes it.

**Previous candidate rejected:** the first PR #12 proposal used an edge-specific hard mass capacity and reported overload at a fixed value. It was replaced on the same unmerged branch because it contradicted the intended soft, economic saturation model.
