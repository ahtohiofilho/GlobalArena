# ADR-053 — original F072 on more physical demand nodes

The game must model demand created by workers, not independent artificial
market lists. This gate reuses the ADR052 worker-coupled population and
ADR051 exact global MCP with local SPD preconditioning.

Four fixed scenarios on the same 5,762 Goldberg nodes:

- 2,048 workers / 48 demand nodes / 96 markets — mandatory original
  ADR042 F072 regression established by ADR052.
- SAME 2,048 workers / 96 demand nodes / 192 markets — new bounded
  exploratory original F072 equilibrium trial (PASS or explicit NO-GO).
- 4,096 workers / 192 demand nodes / 384 markets — only ledger and
  physically funded positive market initial states, NOT equilibrium.
- 8,192 workers / 2,048 demand nodes / 4,096 markets — only spatial
  ledger and initial states, NOT equilibrium.

Every worker demands even when not producing. Travelers allocate
demand by actual tile occupancy duration; no duplicated consumption.
Producer locations are subset of demand locations. Every worker and
route is grounded in original R13 Goldberg edges with seller tariffs
and original quartic c_free*(1+(B/K)^4) cost.

Independent ADR042 original global AND worst group F072 regrets <=1e-4,
original price sqrt(D/Q), R13/R14 commercial separation and F084F are
required for any commercial PASS. No synthetic R13 demand is imported.

Reproduce from repository root (Python 3.12 standard library):

    python -m unittest discover -s experiments/gate09/adr053_market_coverage/tests -p 'test_*.py' -v
    python experiments/gate09/adr053_market_coverage/ci/run_adr053.py --profile ci --output .

A green CI means all mandatory 48-node commercial and 96/192/2048
spatial accounting gates passed; only the separate F072 certificate
fields say whether the NEW 96-node case achieved equilibrium.

No C#/Godot production integration, no periodic execution scheduler,
no official F059 financial tie policy. Draft PR stacked on #341.
