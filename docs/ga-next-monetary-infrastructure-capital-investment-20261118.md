# GA-Next — MONETARY infrastructure construction and treasury, not physical materials or worker labor
2026-11-18 • corrective gate from PR #422, independently of superseded PRs #423–#426.

## Product-level source of truth

**All goods produced by real workers are sold and consumed during the same
economic cycle. There are no physical inventories, construction stockpiles,
material-input debits or buyer wallets.** Revenue and applicable taxes are
monetary; civilizations invest their own existing cash to improve the
infrastructure on a real fine-graph edge. Worker physical locations remain
fine **nodes**, and all workers continue to produce demand at those nodes;
they do not need to stand next to or work on an infrastructure edge.
A monetary action does not create, destroy, relocate or reclassify a worker.

These statements supersede the compulsory worker-labor construction model
in PRs #423–#426. Those Draft PRs must not be merged.

## Physical operating friction and monetary capital requirement

For terrestrial crossing A–B, the generated-world signed altitude projection
provides the only geographic difficulty measure:

```
DeltaH = abs(h_B - h_A)
H = StrategicScalarField.Denominator

OperatingFreePhysicalCostMicro = LandOperatingBase
                               + floor(K_operating * DeltaH / H)

NaturalConstructionCostMoneyMicro = LandMonetaryBase
                                  + floor(K_capital * DeltaH^2 / H^2)
```

The formulas use checked, integer fixed-point arithmetic with explicit
policy parameters and no new node attributes for slope or roughness. A
uniform high-altitude plateau has no altitude penalty merely for being
high. Coastal/sea crossing classes use explicit separate cost baselines;
they do not get a terrestrial altitude surcharge. Historical marine
technology/availability is still a separate feature.

The original three-argument `FoundationPhysicalNaturalTransportSeeder.Create`
is unchanged and still emits v12 natural crossings. The opt-in four-argument
variant supplies `FoundationMonetaryConstructionPolicy`, a **money quote**
for every crossing. It does not encode labor or materials.

## Treasury money is exact and authoritative

A complete `CivilizationTreasuryRuntimeState` contains exactly one
civilization cash balance and cumulative infrastructure investment
outflow per civilization, with canonical IDs and nonnegative cash.
It is uninitialized in historical v8–v12 worlds. The opening balances are
declared explicitly at one-time world-materialization and may all be zero;
there is **no silent free opening grant**.

The existing precommit economic accounting proposal uses the exact
money domain `MoneyScale = 10^16` per whole monetary unit, arising from
goods micro × price micro × 10,000 basis points. Treasury uses the same
BigInteger scale. A quote or funded amount is in monetary micro-units
(1e-6); converting it to treasury scaled units multiplies by `10^10`
exactly, without route-level rounding.

There is NO authoritative sales settlement currently. The market
reconciler's sales, tax, freight, and toll receipts are **proposals only**
and cannot be transformed into real money simply by calling this gate.
For deterministic end-to-end investment tests, an explicit initial
balance is injected as a *test fixture*, NEVER claimed as market revenue.
The next genuine integration gate must journal a complete validated
economic settlement that credits civilization accounts from sold
and consumed output. No public unconditional money-credit API is exposed
in this gate.

## Atomic investment from available money

An opt-in `FoundationTacticalHumanAiTurnHost` with monetary infrastructure
enabled (off by default) lets a HUMAN civilization queue an amount in money
micro-units against one real physical edge. It is an order of the
**civilization**, not of any worker. It must:
- Name a registered issuing civilization, an actual unbuilt crossing,
  and an explicitly positive monetary amount;
- Not exceed remaining *natural monetary construction cost* including
  all already queued civilization investments in this collective turn;
- Not exceed the issuing civilization's cash, including all its own
  other queued investments;
- Remain private and uncommitted until all participants are ready;
- Use the ordinary turn-policy/commit gate and a deterministic, independent
  replay of journaled events.

The `FoundationMonetaryInfrastructureInvestedEvent` verifies world
identity, exact turn/revision, real edge, real investor and current cash
before atomically debiting the treasury and crediting **capital spent**
on the edge. The ledger is immutable, with a canonical v14 hash; the
accepted investment creates no physical material consumption, employment
contract or new demand site. Capital outflow and edge progress are
equal in exact scales. Insufficient funds / fake provenance / old
revisions fail closed.

**This gate does NOT activate a constructed road automatically.** Even
when capital reaches the quoted requirement, current physical freight,
cargo capacity, owner title and external tariff remain unchanged.
Road levels, efficiency gains, military takeover, last-investor
title and maritime access must be separately specified and approved.
There is never a parallel infrastructure on one real crossing.

## Versions, safety and acceptance

- v8–v12 canonical bytes remain unchanged. The three-argument v12
  seeder and old host behavior remain intact.
- Opt-in v13: all-edge monetary cost section 41 and complete treasury
  section 43 (explicit absent/present flag and exact nonnegative money).
- Opt-in v14 on first accepted actual cash debit: invested-money section
  42, ordered by canonical real crossing.
- The canonical market function `P(Q)=sqrt(D/Q)`, owner-exempt tolls,
  physical operating costs and turn-locked congestion remain unchanged.
- No worker is assigned or required to build an edge. Thus workers'
  node-local demand stays attached to their existing node; investing
  civilizations can fund **geographically remote edges**.
- The first implementation uses immutable full-edge arrays on investment.
  Scale optimizations can be added later, if profiling justifies them,
  without copying the superseded physical labor ledger.

Tests cover all real crossing monetary prices (quadratic land,
class-based coast/sea); old v12 hashing; explicit treasury and exact
money scales; zero/insufficient cash; all-ready funding and replay;
multiple turns; mixed worker activity and money investment; forged
amounts/owners/stale events; unchanged workers, materials, prices,
tolls and freight; no road on mere investment completion.

**Draft PR based on #422, NO merge to main.** Any PR #423–#426 containing
worker-labor construction is superseded and must remain unmerged.
