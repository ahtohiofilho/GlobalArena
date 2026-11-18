# GA-Next — Market identity is physical, not political (2026-11-18)

## Decision and scope

A unique market is identified by **(fine tactical tile, commodity)**. The
worker-demand projector already exposes both per-civilization demand lines
(for legal/provenance attribution) and their consolidated physical market
(for economic price discovery). This gate connects the latter to marginal
water-filling and financial precommit accounting. **No treasury, journal,
settlement, state hash, economic publication, or turn transition is changed.**

## Mathematical invariants

- For every commodity and physical market, the marginal price remains
  **P(Q) = sqrt(D / Q)**, normalized to one money unit at Q=D. The same
  normalization applies to every commodity and civilization. There is no
  political, era, distance, or local quote multiplier in the gameplay entry.
- **D** is the sum of the ACTUAL workers' activity-weighted demand at that
  tile. **Q** is the sum of EVERY delivered good unit of that commodity at
  the tile, regardless of seller or buyer citizenship.
- Water-filling sees **one destination per (tile, commodity)**. A producer's
  own real physical and third-party tariff costs still vary by route and
  seller. Cargo and edge costs are fixed during the turn; observed edge loads
  feed a quote only for the following *settled* turn.
- The market's total physical goods are attributed to existing demand
  participants using deterministic largest-remainder shares of their demand.
  Shares are in exact cargo quanta: no invented buyer, demand, route, stock,
  or quantity ceiling. For any market, the sum of attributed goods is Q;
  this attribution DOES NOT affect the optimal physical destination.
- Historical experimental quote interfaces retain their explicit line
  completeness, but reference quotes differing by civilization **inside the
  same physical market** fail closed rather than producing two prices.
- The independent accounting audit checks each real shipment by owner and
  computes its price from the shared physical **(D, Q)**. Taxes are assessed
  on seller gross revenue; owner-exempt tolls and physical freight remain
  separate, using the fixed per-turn rates.

## Scope and residual blockers

This branch is stacked on PR #419 and remains Draft. It resolves the
owner-split **pricing/market-identity** issue; it does NOT authenticate
infrastructure or its owner in authoritative world state. Until the
infrastructure source is world-backed and the economic journal/atomic
settlement is implemented, all resulting proposals remain uncommitted.

## Regression strategy

Existing distributor, coordinator, integrated utility, lagged freight,
accounting, kernel, world hash, and Godot smoke suites must remain green.
New assertions cover rival civilizations sharing exactly one D/Q/price
and rejection of conflicting quotes within one physical market.
