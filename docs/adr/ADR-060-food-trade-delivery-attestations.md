# ADR-060 — Market-Linked Food Delivery Attestations Are Not Food Access

**Status:** Proposed — pending semantic review and cross-platform regression

**Date:** 2026-10-26

## Context

The accepted market allocator produces recurrent, financially viable commercial trade allocations, not physical shipments. ADR-058 observes the portion of food demand allocated by that market. ADR-059 adds turn-scoped, externally supplied food-access observations with explicit evidence-source keys, but these observations are **independent** of negotiated trades. Neither commercial allocation nor observed household access establishes that a particular shipment was delivered.

We need an intermediate, opt-in reconciliation boundary to compare **specific market allocations** to externally supplied, turn-scoped **delivery declarations**, without silently inferring fulfillment or inventing a logistics simulation.

## Proposed decision

1. A caller may provide a `FoodTradeDeliveryAttestation` consisting of:
   - an exact immutable `EconomicTradeAllocation` reference/value (source economic point, source activity, destination economic point, commodity, unit price and transfer cost, and the selected quantity);
   - non-negative `RawAttestedDeliveredQuantity` in that commodity's original fixed-point flow units, **at most** the selected market allocation;
   - a stable nonempty `EvidenceSourceKey` describing provenance. The engine does **not** independently authenticate that source or inspect the real transport.
2. `FoodTradeDeliveryAttestationSnapshot` is valid for **exactly one `TurnNumber`**. Stale or invalid turns are rejected. The snapshot is immutable and canonically ordered by market allocation identity. Exactly one attestation per allocation identity per turn is accepted; duplicate or overlapping outside reports require explicit upstream reconciliation.
3. No receipt for a chosen food allocation means **unknown delivery**, not zero delivery. A receipt declaring zero means the caller explicitly reports zero delivered, not that the market allocation was zero. Source-corroborated quantities must not be inferred from any `EconomicMarketAllocationResult`, `FoodMarketDemandCoverageResult` or `FoodPointAccessObservation`.
4. `FoodTradeDeliveryReconciliationResolver` consumes the **actual market result**, the matching **food market demand coverage**, an explicitly registered food catalog and a turn-scoped attestation snapshot. It rejects:
   - food allocations missing corresponding food-demand coverage;
   - any market food allocation totals that disagree with the already calculated point/food coverage;
   - an attestation referencing a nonfood commodity, unselected trade, altered price, altered transfer cost or changed selected quantity;
   - delivered quantities greater than the corresponding selected market allocation;
   - stale or duplicate declarations.
5. Results contain canonical **per-allocation** rows with nullable attested delivery and nullable remaining non-attested portion. Food quantities are **summarized per commodity**, not globally, because ADR-057 permits distinct `GramsPerFlowUnit` scales. Reports distinguish the amount allocated with no attestation at all from a remaining un-attested part of an allocation for which some attestation was provided.
6. This is an **opt-in pure resolver**, not an automatic fifth turn phase. Callers may reconcile the accepted transient market and coverage events with their independently sourced shipment declarations. No persistent inventory, route movement, proof-of-delivery verification, food-access inference, population modification or canonical WorldState/hash v8 change occurs.

## Terminology and risks

- **Attested delivered** is an externally asserted delivered quantity that passed consistency checks against a commercial allocation. This is **not independent proof** of physical delivery.
- **Unattested remainder** means not supported by this submitted attestation. It does not necessarily mean lost, stolen, delayed or not delivered.
- **Unreported allocation** means delivery status is unknown. Missing evidence is never silently counted as zero.
- **Observed access** (ADR-059) is a separate boundary and must not automatically equal attested shipment delivery. A household may have other sources or lack access even when a carrier reports delivered goods.
- A physical logistics simulation, transport time, unique shipment identity, third-party verification, stock accounting, local sales/consumption and nutritional adequacy require separately accepted contracts. No default food identity, food need, calendar length, cargo capacity or consumption policy is added.

## Acceptance evidence

- Deterministic tests for partial/complete/zero and missing attestations, multiple suppliers, distinct commodity scales, proof-source keys, stale turn and duplicate identity failures, changed allocation quantity/price/cost, nonfood exclusions, strict market/coverage matching, checked per-food aggregates and read-only outputs.
- Integration using the accepted resident food market/access phase must show three **independent** values: trade-allocated food, externally attested delivered food, and externally observed accessible food.
- Cross-platform Windows, Ubuntu and macOS .NET 10 Release kernel and known hash regression on the exact PR head must be green.

ADR-060 is proposed, not a gameplay completion claim, and does not award any new GPP by itself.
