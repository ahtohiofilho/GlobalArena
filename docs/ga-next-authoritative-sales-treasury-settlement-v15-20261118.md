# GA-Next — Authoritative sold-and-consumed production -> civilization treasury (v15)
2026-11-18 — stacked on monetary-only infrastructure PR #427.

## The approved money-only economy

One real worker remains at a **fine tactical node** and contributes demand
there. An active worker also produces its nominated commodity. In the
economic cycle, ALL output is sold and consumed; buyers have **no wallets**,
commodities have **no physical inventory**, and infrastructure requires
**monetary investment**, not wood, stone, stored goods or workers posted
to edges.

The already-authoritative v13/v14 transport inventory provides natural
physical freight, cargo capacity, real ownership/toll and the monetary
capital requirement. A closed cycle never changes those values by itself.
Only the treasury can fund the existing all-ready monetary investment order.

## Accepted economic cycle

An opt-in `FoundationAuthoritativeEconomicSettlementPolicy` supplies the
complete existing activity-by-commodity demand coefficients, cargo conversion,
post-revenue taxes for every real civilization and an explicit bounded
producer pass budget. No arbitrary market prices, sales values or tax
defaults are accepted.

The authoritative `foundation.verified-sales-consumption-treasury-v15`
**SYSTEM phase** runs after worker movement and after capital expenditure
orders in a fully all-ready collective turn:

1. Read real authoritative worker census, fine topology, civilization
   roster, committed physical transport, and last closed freight memory.
2. Solve ONE global marginal market at fixed freight costs using the
   existing universal curve `P(Q)=sqrt(D/Q)`. The prior settled cycle
   alone can contribute lagged congestion; an uncommitted preview cannot.
3. Require independently audited `TotalAvailableGoodsMicro ==
   TotalAllocatedGoodsMicro`, correct cargo units and no within-cycle
   freight re-quoting. This equals 100% production sold and consumed.
4. Independently evaluate exact seller sales at actual delivered
   quantities, physical freight expenses, third-party owner tolls and
   explicit post-revenue seller taxes.
5. Reconcile **gross sales = physical freight + owner tolls + seller
   taxes + seller net**. For each real civilization, credit
   `seller net + its seller taxes + received owner tolls`. Because
   buyers have no wallets, these credits arise from actual audited sales
   rather than synthetic spending accounts. The physical freight cost
   is not money paid to an invented counterparty; it is the existing
   operational cost of physically delivering goods.
6. One system-originated `FoundationEconomicSalesSettledEvent` includes
   actual produced/sold amounts, exact money totals, per-civilization
   receipts, cycle ordinal, source revision and **source WorldState hash**.
7. The event executor recomputes independent sale/revenue evidence on
   journal replay from the PRE-SETTLEMENT WorldState; no caller can
   submit artificial credits or reuse a duplicate cycle.
8. Treasury `CashScaled` and `TotalSettledEconomicIncomeScaled`
   are incremented atomically; **next-cycle** damped freight quotes
   are saved in the same immutable authoritative treasury object.

A v15 canonical hash serializes the increasing closed-cycle ordinal,
cargo-unit code, full canonical next-cycle freight vector and cumulative
closed income per real civilization. The already-signed treasury cash
is hashed in its existing v13 section. Historical v8–v14 hashes remain
unchanged; a v15 world may or may not yet contain invested capital.

## Chronology / risk management

A civilization cannot commit investment based on unclosed projected sales.
Existing (turn-start) cash funds orders in the **current** turn; validated
sales after those orders increase cash for the **following** turn. This
prevents anticipatory spending and overdrafts. The next economic cycle
reuses only quotes from the previous fully settled cycle, and applies
congestion after the previous cycle's volume was observed. No same-turn
freight fixed-point iteration was introduced.

The system phase is **opt-in** in the tactical host and requires the
monetary infrastructure gate + explicit settlement policies. When not
enabled, no sale is published, no cash is credited and v12/v13/v14
behavior remains as before. An empty order batch with settlement enabled
still closes the real economic cycle.

The old last-closed **visual trade-overlay boundary** is not yet
connected to the new system event. Freight quote memory IS persisted and
canonically hashed; the public trade-overlay continues showing
`NoClosedCycle` until a separate authorized visible-trades publication
phase binds the actual closed delivery audit to participant visibility.
This avoids advertising an unverified visible trade history.

This step does NOT create stockpiles, worker construction activity,
material requirements, completed infrastructure, infrastructure titles
or consumption-based buyer wallets. Construction completion/ownership
is an independent future gate, and its asset must use the one existing
edge infrastructure without parallel copies.

## Acceptance / audit tests

- Zero initial cash; one all-ready economic close makes actual audited
  sales and credits nonnegative exact money to civilizations.
- All physical production is sold and consumed; seller net, seller
  tax, owner toll and freight totals reconcile without per-route
  rounding or invented costs.
- A civilization cannot invest one monetary micro-unit before closing
  actual sales, but can invest using receipts at the NEXT turn while
  the next closed cycle continues earning revenue.
- Closed freight memory persists in v15 and is reused across economic
  cycles, never substituted with planner preview output.
- Forged gross sales, a duplicate event, stale source state or wrong
  origin cannot mint money on independent journal replay.
- Existing v13 money-only worlds with the settlement phase disabled
  remain unchanged. Worker census and physical transport identity are
  untouched by settlement, so demand still occurs at actual nodes.

This is a Draft PR stacked on #427. No merge to main.
