# Gate09 ADR-045 — general multigroup MCP/VI solver POC

Solves smoothed Fischer-Burmeister NCP with analytic cross-market/
cross-congestion Jacobian on producer supply simplexes, using damped
Levenberg–Marquardt and shortest-path global column separation.

This is **not** another F072 best-response-mixing policy. The original
economic formula stays P=sqrt(D/Q), congestion c_free*(1+(B/K)^4),
seller-specific tariff, and market score P/(1+C). Numerical
representation uses the equivalent reciprocal H=(1+C)*sqrt(Q/D).
No fake buyers, price floor, inventory or supply.

Original frozen F072 economics are verified only by the independent
ADR042 static certificate (R13, R14, F084F, global and worst-group
gaps <=1e-4). An NCP residual alone never constitutes a PASS.

The original input flow is immutable. A reachable Q=0 starting market
can receive a tiny REAL transfer from a producer's physical production
for numerical differentiation. A final Q=0 is never certified.

Predeclared default ceilings:
- 1200 complete H evaluations including rejected line-search trials;
- 320 seller-specific shortest-path scans;
- 6 full independent ADR042 certificates;
- 192 active flow/route columns;
- 4 global column-generation rounds;
- 60 accepted Newton/LM updates;
- 14 backtracking trials per LM candidate.

Run from the repository root using Python 3.12 stdlib:

    python -m unittest discover -s experiments/gate09/adr045_general_mcp/tests -p 'test_*.py' -v
    python experiments/gate09/adr045_general_mcp/ci/run_adr045.py --profile smoke --output .
    python experiments/gate09/adr045_general_mcp/ci/run_adr045.py --profile ci --output .
    python experiments/gate09/adr045_general_mcp/ci/run_adr045.py --profile extended --output .

CI runs controlled 1–3-market and two-producer fixtures, plus two
42-node R34 scenarios; extended additionally tries 162 nodes with a
smaller budget. Every larger-map NO-GO is disclosed, never considered
successful production or C# integration.

PR Draft stacked on ADR044 #333. No merge, no original code changes,
financial F059 unresolved.
