# ADR-059 — Turn-Scoped Food Access Observations Are Not Market Allocations

**Status:** Proposed — pending semantic review and cross-platform regression

**Date:** 2026-10-26

## Context

The flow-based economy (ADR-037 and ADR-053), resident census, explicit food commodity mass units (ADR-057), and food market demand coverage (ADR-058) now produce deterministic economic outcomes. The market considers trade profitability, transfer capacity, commodity identity and destination demand. These calculations do not establish whether any quantity of food was actually available to residents.

In particular, a food trade allocation must **not** be reinterpreted as physical delivery, household access or ingestion. Food can also be accessible through channels that a commercial trade resolver does not model; no such channel is currently simulated by default.

This proposal defines an explicit data boundary for **caller-reported local food accessibility** without pretending that the engine can verify the real-world evidence or derive it from existing market flows.

## Proposed decision

1. An optional `FoodPointAccessObservation` identifies an economic point, a registered food commodity, a non-negative accessible quantity in that commodity's existing raw flow units, and a nonempty stable `EvidenceSourceKey` supplied by the caller. The key declares provenance; the current engine **does not independently authenticate or validate the underlying observation**. The scale originates from the explicit `FoodCommodityDefinition.GramsPerFlowUnit`, not an assumed global food unit.
2. A `FoodAccessObservationSnapshot` belongs to exactly one `TurnNumber`; a default, invalid or stale turn fails closed. Records are immutable, canonically ordered and unique by point/commodity. Different sources cannot be added as independent quantities for the same key without an explicit upstream reconciliation policy that prevents double counting.
3. **Omitted observation means unknown access, not zero access.** A recorded zero quantity means the caller explicitly declared zero accessible raw units for that key, which is semantically different from silence. A snapshot with zero rows is valid and yields unknown access for all registered food demands.
4. A pure `FoodDemandAccessCoverageResolver` compares the accepted market food demand coverage with explicitly supplied access observations. For each registered food demand, it exposes original demand, trade-allocated quantity (independent reference only), optional raw accessible quantity, optional `min(demand, accessible)` and optional `demand - min(demand, accessible)`. Accessible quantities beyond current demand are reported as a non-negative excess; they are **not** invented stocks carried to future cycles.
5. Access observations for unregistered foods, unknown point/food demand pairs, duplicate entries, and mismatched turns are rejected. Missing observations do not contribute to observed zero-access gaps; unknown and observed demand aggregates stay separate, with checked `UInt128` arithmetic.
6. An opt-in `EconomicResidentFoodAccessTurnPhase` reuses accepted production/demand, market allocation and trade-coverage calculation, then emits a fourth audit event recording the independently supplied access comparison. The four events appear in deterministic order. The new phase shares the economic phase identity with its alternatives and must not be installed concurrently with them.
7. The observation system is **transient**, not a new `WorldState` population, food stock, health or access field. Canonical hash v8 is unchanged. The phase does not fabricate evidence, shipments, calories, household income or consumption.

## Limits and non-goals

- `RawDemandNotMatchedByAccess` is a comparison against the **declared access observation**, not starvation, illness, hunger, food insecurity or a biological need deficit.
- `RawDemandMatchedByAccess` does not prove the food was actually delivered, affordable, equitably distributed, edible or consumed.
- A large observed accessible quantity may exist while the market allocated little or nothing, and the reverse is also possible. Neither outcome is automatically considered contradictory because the observations concern a separate boundary that current economic routes do not authoritatively model.
- No source reliability weights, multiple-source aggregation, water/energy/caloric semantics, protected food-commodity IDs, geographic default yields, government transfers, food bank logistics, demographic mortality or long-term food storage are modeled.
- A `FoodAccessObservationSnapshot` is intentionally **turn-scoped**. Callers must provide a fresh snapshot for each new turn, or omit the optional access-observation phase. Reusing old observations across turns without refresh is forbidden.
- The design is not a promise that trusted food-availability data is already available in a real game session. It only defines how to consume externally supplied access observations if and when such evidence is introduced.

## Acceptance gates

- Tests establish unknown versus explicit zero, different observed and commercially allocated quantities, observed excess above demand, independent multi-food results, duplicate/cross-key protection, provenance key validation, stale-turn rejection, deterministic ordering and checked aggregation.
- An opt-in four-event authoritative, commandless turn preserves the resident census and canonical hash.
- Full .NET 10 Release kernel and state-hash regression passes on Windows, Ubuntu and macOS.
- No persistent-state, calendar, population growth, welfare or consumption mechanics are introduced, and no new GPP is credited by this proposal alone.

This ADR remains Proposed until implementation and its restricted interpretation are reviewed.
