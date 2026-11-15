# ADR-049 — Goldberg geography growth and upper bound R13 density

A strict Gate09 experiment on the UNCHANGED original F072 economy,
ADR047 producer-block MCP solver and independent ADR042 R13/R14 + F084F
economic certificate.

Two registered, separate profiles:

- geography: **5,762 Goldberg nodes / 17,280 edges**, with the SAME
  144 producers / 48 markets / 6 synthetic goods and SAME numerical
  work limits as ADR048's 2,562-node 144/48 case. This directly
  isolates the impact of geographic growth with identical economic
  agent counts. The CI returns failure unless original F072 passes.
- maximum_density: 2,562 Goldberg nodes / **240 producer groups /
  120 markets** / 10 synthetic goods at R13's built-in upper bound.
  Separate, predeclared budgets. NOT thousands of firms; run only
  after geography results justify the cost.

Immutable economics: price sqrt(D/Q), quartic congestion
c_free*(1+(B/K)^4), seller tariffs, full worker/producer supply,
no imaginary price floor and independent R13/R14 route separation
and F084F. The ORIGINAL F072 global and worst group gap must both
be <=1e-4 for PASS. Uncertified provisional results never become
published output.

The JSON/ZIP includes original fingerprints, accepted final or
NOT-final gaps, exact solver work (H/Krylov/CG/Cholesky/scans),
Python runner timing, and GO/NO-GO at each profile. The run is
sequential and DOES NOT implement cooperative scheduling, game
threads or actual turn CPU allocation.

Run Python 3.12 from repository root:

    python -m unittest discover -s experiments/gate09/adr049_geography_density/tests -p 'test_*.py' -v
    python experiments/gate09/adr049_geography_density/ci/run_adr049.py --profile geography --output .
    python experiments/gate09/adr049_geography_density/ci/run_adr049.py --profile maximum_density --output .

Tests validate original source SHA16, unchanged economics,
exact geography/density, independent certificate gating,
hard work budgets, input immutability and required fail
classification.

PR Draft stacked on ADR048 #337; NO MERGE, NO C#/Godot alterations,
F059 financial policy still unresolved.
