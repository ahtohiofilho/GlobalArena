# GA-Next — explicit v15 economic startup for the actual Godot tactical scene
2026-11-18. Stacked after #429. Draft; never merge main automatically.

## Approved invariants

Workers exist at canonical physical fine nodes and continue generating demand at
their current node. All physically produced goods are sold and consumed during
each accepted economic cycle. No physical inventory, materials reserved for
construction, compulsory edge workers, or wallets for buyers exist. A
civilization can only invest previously available cash; sales credited at the
end of the current cycle fund the NEXT turn. Last settled cargo follows
canonical real fine edges; line width is actual visible cargo and line colors
are authoritative infrastructure owner IDs.

## Crucial boundary: numerical rules are NOT yet ratified

No complete numeric values were ratified for activity/commodity demand,
cargo conversions, tax rates, transport operating costs, height-based
capital costs, or civilization opening-money grants. Numerical values in
previous tests are **QA fixtures** only and may not silently become ordinary
game rules. The ordinary client retains the v11 worker-only startup until
a trusted gameplay bootstrap supplies an explicit complete profile.

`FoundationGameplayEconomicConfiguration` requires:

- `FoundationNaturalTransportSeedingPolicy` — exact free-flow road/crossing
  policies for land, coast and sea;
- `FoundationMonetaryConstructionPolicy` — exact money quotations per
  real crossing, quadratic altitude delta on land, no physical inputs;
- `FoundationAuthoritativeEconomicSettlementPolicy` — complete demand
  coefficients for every real activity and good, normalized cargo
  equivalents, tax rates (including explicit zero if approved), solver
  budgets;
- One explicitly defined `(CivilizationId, OpeningMoneyMicro)` pair
  for **every** actual civilization; opening money zero is permitted.

No default tax, opening balance, sale, physical material, quota or
commodity conversion is supplied.

## Godot gameplay ingress

A trusted client/session bootstrap calls
`Main.ConfigureAuthoritativeEconomicGameplay(profile)` **before
Godot `Main._Ready()`**. It is single-use and rejects late
hot-swapping during a game. At actual startup, the ordinary v11
concentrated-worker world is completed with v13 physical transport
and explicit treasury; the existing same-session real worker roster,
fine topology, human and original AI policies are preserved.
`FoundationTacticalHumanAiTurnHost` then enables the existing
all-ready v15 economic phase and per-player private trade publication.
The normal E key now closes a genuine economic cycle, updates treasury,
and updates the `LAST SETTLED TRADE ROUTES` layer. Event replay
independently re-audits every sale and private cargo view.

Absent this configuration, Godot behaves as before (v11, no trade
lines, no fictional market settlement). No shipped production config
contains the unapproved QA numbers.

## Explicit QA visual experiment, never ordinary gameplay

The ONLY baked numerical pack is
`GaExplicitEconomicQaConfiguration`, consumed **solely** when
`GA_NEXT_SETTLED_TRADE_VISUAL_SMOKE=1` is explicitly set for CI.
It duplicates test-only coefficients, prices and tax rates from the
earlier PR #428 regression fixture, with **both initial balances 0**.
It chooses three actual initial workers per civilization and deliberately
specializes the **existing** individual commodity labels at test setup
(red grows commodity 1, blue produces commodity 2) so that real
interregional freight can occur in the test. No phantom workers,
materials, demand sites or stocks are constructed. This does NOT
approve these figures for real historical/gameplay balance.

The Godot QA scene executes two ACTUAL E-key collective turns and
verifies fresh sales, real nonzero cash, private per-participant routes,
nonempty trade line geometry, v13→v15 hash transition, non-mutating
show/hide toggle and a replaced second-cycle historical view, while
preserving camera position, zoom and physical identities. In Xvfb,
screenshots of the last settled trade after each turn are captured as
CI artifacts. The ordinary v11 Godot smokes still run.

## Deployment caveats

This is an explicit *local gameplay bootstrap* entry point, not a
claim that arbitrary multiplayer clients may author authoritative
economic profiles. The future server-controlled civilization
authentication and policies still need a trusted network/session
implementation. The backend must supply one ratified configuration
before declaring monetary v15 default for all players. No numerical
parameters should be adopted from tests merely to make the map
more visually populated.

The completed trade overlay remains filtered by actual seller/buyer
involvement, does not disclose world totals, and can display an empty
cycle when all trade was local.
