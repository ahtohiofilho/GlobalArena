# Gate 09 R32 - frozen price and transport best-market rankings

Research-only numerical diagnostic. Does not change R20-R31, authoritative C#,
Godot, tariffs, ownership, taxes, price or demand functions, production,
F-084F settlement or F-059 policy. NO global convergence claim.

## Hypothesis

R31 found the absolute change in relative market log price exceeded
the relative generalized transport cost in all 915 observed switches.
A bigger algebraic component is NOT a proof of causal price influence.
To distinguish score-rank sensitivity, R32 constructs four complete
rankings of every commodity-compatible destination for each producer
at each original F072 update. The unchanged score is S=P/(1+C), where
C is the shortest generalized seller-specific route cost.

The rankings are:
- old_actual = old prices and old frozen shortest-path costs;
- price_only = new prices and old frozen shortest-path costs;
- transport_only = old prices and new frozen shortest-path costs;
- new_actual = new prices and new frozen shortest-path costs.

The two mixed rankings are ALGEBRAIC hybrids and may not be consistent
with P=sqrt(D/Q), supply conservation or any simultaneous clearing
state. NEVER apply them to flows, money, tariff policy, demand or
solver updates. They describe the dependence of computed score ranks
on existing frozen price/transport inputs, not causal intervention.

Each old_actual and new_actual full ranking must exactly reproduce
the original R14 best market, canonical route and best score.
The original deterministic (-score, canonical column) sorting is used.

For every changed destination A -> B, record BOTH:
- whether B beats A pairwise under price_only and transport_only,
  including when BOTH individual changes are insufficient but the
  combined real score change reverses the pair;
- which destination wins globally among ALL compatible markets under
  each hybrid. A third market can win even if B beats A.

Verify log-score identity and original quantity-price curve for
fixed synthetic demand D:
  delta ln(S_B/S_A) = delta ln(P_B/P_A)
                      - delta ln((1+C_B)/(1+C_A));
  delta ln(P_B/P_A) = -0.5 ln(Q_B_new/Q_B_old)
                       + 0.5 ln(Q_A_new/Q_A_old).

The quantity terms are exact accounting identities, not claims
that a specific producer caused the price change.

## Controls and strict cost accounting

All trajectories use unchanged R21 decay_sqrt, R21 backtracking,
R21 fixed_1_8 and R26 progressive_global. Hybrid rankings call
ZERO additional R14 oracle evaluations because initial and final
snapshots were already fully evaluated. This does not mean zero
computer work: independent FrozenRouteCache shortest-path scans
needed for hypothetical rankings are separately recorded.

R20 SHA256 frozen manifest 16/16; unchanged R21/R26/R31 replay,
independent R13 vs R14 check, 100% production sold, F-084F exact
micro-unit conservation, positive finite prices, deterministic
prefixes and financial audit including final snapshot. The five
numerical criteria remain <=1e-4 for three consecutive post-update
snapshots (aggregate F-072 gap, worst-group gap, price, edge-load
and market-quantity relative changes). No F-059 ratification,
equilibrium existence/uniqueness theorem or production promotion.

## Commands (repository root)

    python -m unittest discover -s experiments/gate09/r32/tests -p 'test_*.py' -v
    python experiments/gate09/r32/ci/run_r32.py --profile smoke --output .
    python experiments/gate09/r32/ci/run_r32.py --profile ci --output .
    python experiments/gate09/r32/ci/run_r32.py --profile extended --output .

CI: 42 nodes/96 steps; 162 nodes/48 steps, four original policies.
Extended adds 642 nodes/24 steps. Python 3.12, Windows and Ubuntu;
full per-group JSON and ZIP evidence. PR Draft stacked on R31.
