# ADR-051 — worker transit demand and bounded SPD subblocks

Laboratory only. The definitive game must make every worker demand goods
even when not producing. During travel, each visited tile receives its
duration-weighted share of that worker's ORIGINAL demand per cycle.
No generated demand is multiplied by movement.

The 42-node fixture has 10 physical workers: six producing workers
across 3 nodes, and four nonproducers (traveler, builder, military,
idle). Demand exists in four nodes, two goods per node: P subset D.
No game C#/Godot implementation is claimed.

The second part experiments with WIDTH-2 exact principal subblocks of
the original analytical SPD J^T J producer block preconditioner. The
global MCP equations and Jv/Jtv stay unchanged. Counts charge ACTUAL
triangular sparse dot products w*(w+1)/2 rather than original w²
bookkeeping; these costs MUST NOT be compared as equal work units.

Python 3.12 stdlib, repo root:

    python -m unittest discover -s experiments/gate09/adr051_worker_block/tests -p 'test_*.py' -v
    python experiments/gate09/adr051_worker_block/ci/run_adr051.py --profile smoke --output .
    python experiments/gate09/adr051_worker_block/ci/run_adr051.py --profile ci --output .

CI compares an ADR047 baseline of two producers to the new subblock
solver and audits the existing maximum-density R13 2,562-node
240-group/120-market fixture under bounded work. The latter may be
NO-GO: the JSON distinguishes original F072 pass from POC success.

Only the independent original ADR042 certificate authorizes an F072
commercial PASS; the residual of the numerical MCP is not enough.
Congestion remains c_free*(1+(B/K)^4) and tariffs are unchanged.
F059 remains unresolved. The project has no implementation of
real-time periodic scheduling, worker movement economic settlement,
or C#/Godot integration in this PR.
