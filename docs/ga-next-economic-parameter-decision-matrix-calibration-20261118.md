# GA-Next — A/B economic parameter governance (noncommitting gate)
2026-11-18. Stacked on PR #430; no automatic merge to main.

## Hard game rules: already approved, NOT calibration dimensions

- One physical worker is associated with one canonical fine-graph **node**,
  generates demand at that node even when moving, serving in the military,
  or not producing. Production uses only active real production workers.
- Every produced commodity is **sold and consumed during that same closed
  economic cycle**. No stored supplies, material consumption by building,
  buyer wallets, artificial market points or fixed buyer spending.
- ONE universal market price curve: **P(Q) = sqrt(D/Q)** with normalized
  baseline P0=1 for all markets and commodities. Market-specific
  arbitrary initial prices are not introduced. Marginal allocation
  prioritizes zero-distance local sales at equal prices, then pays real
  route costs when economically feasible.
- Freight comes from physical world crossings and real investments,
  with natural costs even on unbuilt crossings; closed-cycle
  congestion becomes observable in **the next cycle only**.
- Civilization treasury accepts only audited real monetary sales
  and owner toll receipts. Monetary investment is debited before
  that turn's economic close, and no materials, edge workers or
  physical stockpiles are required to build.
- Infrastructure owner is exempt from tolls on its own freight.
  Historical policy rules about asset completion, military possession,
  and investment title are not silently decided by this calibration.

## Parameters still requiring an explicit game design decision

| Dimension | Current implementation | Decision status |
|---|---|---|
| Worker activity/commodity demand | Full strictly-positive profile × commodity matrix | **To calibrate**; do not insert 1 micro or 1 worker as a universal hidden demand floor |
| Physical cargo conversion | Positive exact cargo-per-good by commodity and a stated cargo unit | **To calibrate**; test 1:1 cargo is only an experiment, not physical equivalence |
| Operating cost, natural capacity | Separate land/coast/sea baselines, land height difference linear surcharge | **To calibrate**, especially early-game intercontinental impracticability |
| Monetary capital to build | Separate land/coast/sea money and land height difference squared surcharge | **To calibrate**; building uses money, never materials or construction labor |
| Post-revenue civilization tax | One explicit basis-point rate for every actual civilization (zero allowed) | **To decide**; tests' 10% and 5% are not approval |
| Opening civilization cash | Explicit per-civilization balances | **To decide**; zero supported, no grant inferred |
| Solver workload | Explicit max producer passes; freight smoothing currently quarter-step | **Engineering budget**, not an economic price policy |
| Road completion/efficiency | Existing money spent recorded, no automatic road or new owner | **Separate feature gate** before monetary investment becomes a live transport upgrade |

## New implementable calibration API

`FoundationEconomicParameterCalibration.Preview(frozenWorld, generatedWorld,
incidence, explicitProfile, label)`

Starting from a signed, virgin v11 worker world, the evaluator:

1. Rejects incomplete civilizational tax/opening-cash coverage or
   undeclared worker goods. Seeds actual fine-edge transport from the
   **complete supplied candidate configuration**.
2. Runs the **existing** fixed-freight global worker-backed market
   coordinator, with real physical cargo and identical universal prices
   (not a synthetic market or simplified proxy).
3. Records demand, active productive capacity, allocations,
   physical edge traversal volume, used real crossings and, only
   if **100% of output was allocated**, exact money revenues,
   freight, owner tolls, taxes, net seller proceeds and
   potential per-civilization credit.
4. Returns a SHA-256 policy fingerprint over *all* operating, capacity,
   construction, activity demand, cargo, tax, opening cash and solver
   coefficients. Profile labels are NOT part of the fingerprint.
5. Returns the source v11 world hash and a separate v13 candidate
   world hash. Neither is an approval seal or an economic receipt.
   The actual canonical session, workers, world, treasury and event
   journal are NEVER mutated. `IsCommitted=false` always.
6. If allocation is incomplete, reports `WouldSettleAllProducedGoods=false`
   and does NOT publish revenue as if settlement had occurred.

`Compare(source, generated, incidence, baseline, alternative)` runs
both **on the SAME immutable worker world**, with the SAME commodity catalog,
cargo unit, opening balances and producer-pass budget, then returns
signed exact deltas for demand, allocations, transported cargo,
gross sales, physical freight, seller taxes and potential treasury
credit. Profiles can differ only through explicitly authored economic
coefficients. No combined multi-factor claim should be interpreted as
the isolated causal effect of an individual parameter: for valid
sensitivity studies change ONE declared category at a time.

### Economic finding requiring product-level interpretation

**Changing the post-revenue tax alone does not change available civilization
cash in the current model** when the seller tax is credited back to the
same seller civilization's treasury. It decreases that seller's
net income and increases its tax receipt by the identical amount.
This is a property of the implemented v15 accounting identity, not a
recommendation or a claim that all tax designs are economically neutral.
A different fiscal destination or distribution rule would be a
separate, explicitly approved mechanics change. The calibration
protects against describing higher tax as more money created.

**A policy fingerprint can differ while the physical WorldState hash is
unchanged.** Demand rates, tax rules and cargo conversions are caller
configuration rather than persisted canonical world fields. Therefore
a reproducible match/replay needs BOTH canonical world hash and
policy fingerprint/provenance; canonical world identity alone is
insufficient to identify which economic rules were used. Making policy
provenance authoritative across restarts/multiplayer is a future gate.

## Deterministic validation and acceptance

- Same frozen world + same explicit profile (including differently
  named scenario labels) gives identical fingerprint and exact reports;
  the input world hash and snapshot are unchanged.
- Tax-only A/B: shared gross sales and physical flows, larger seller tax,
  reduced seller net, identical total cash credit. No fictitious tax
  production or buyer wallet.
- One activity-good demand coefficient affects actual worker demand
  but does not change physical worker census, available goods or any
  treasury without settlement.
- Cargo conversion and capital monetary cost produce distinct
  fingerprints; a change only to capital cost alters construction
  quotes without inventing transport traffic or immediate operating
  advantages.
- Compare refuses changed opening money or mismatched cargo units.
- Existing v11/v13/v15 runtime and Godot tests continue; previews
  never create a sale, credit a real account or enable unapproved
  numeric defaults.

## What must be decided before turning v15 on for ordinary games

A modest, explicit starter numeric table covering initial commodities,
all six worker activity profiles, a physically meaningful common
cargo unit, natural freight/capacity per crossing class,
quadratic investment prices, tax rule (including explicit zero if
desired), and opening cash. We should test 2–3 deliberately different
candidate configurations against the SAME initial map and workers,
prioritizing historical-scale emergent feasibility and useful early
local trade while disallowing unrealistic long routes.
Do NOT tune to maximize attractive colorful lines, create an artificial
price jump, or add a physical stockpile.
