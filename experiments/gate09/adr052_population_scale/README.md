# ADR-052 — 8,192 physical workers on 5,762 Goldberg nodes

Laboratory proof that worker-sourced production, travel demand, spatial
demand and economic compression can coexist in the ORIGINAL F072 graph.

Three registered populations:
* Unit: 162 nodes / 128 workers / 4 productive nodes / 16 demand nodes
* Pilot: 5,762 nodes / 2,048 workers / 12 productive nodes / 48 demand nodes
* Spatial: 5,762 nodes / 8,192 workers / 512 productive nodes /
  2,048 demand nodes / 1,024 aggregated Groups / 4,096 Markets.

Every single worker demands ALL goods in their illustrative activity
profile, regardless of whether they produce; one producing worker also
demands locally. Travel demand is prorated by exact tile occupancy
slots in a six-turn cycle and never duplicated. All travel steps
follow actual graph edges. Production node set is subset of demand node
set. The original R13 topology/tariffs/owners and quartic congestion
are unchanged. R13 independent synthetic demands ARE DISCARDED.

Groups combine producer workers by (node,commodity,seller) preserving
total supply but NOT establishing that individual future economic
institutions are identical. Markets aggregate worker demand by
(node,commodity). The initial real-supply seed funds all markets;
it is NOT a commercial equilibrium.

IMPORTANT distinction:
* Unit: independent original F072 commercial certificate mandatory.
* Pilot: after the initial successful Windows/Ubuntu F072 original
  certification, it is a MANDATORY commercial regression. CI must fail
  if the independent ADR042 certificate no longer approves the
  2,048-worker F072 equilibrium.
* Spatial: 8,192 workers across 2,048 demand nodes is a construction
  / replay / conservation / cost-scale gate ONLY. We do NOT claim
  commercial equilibrium at 4,096 markets because we do NOT solve it.

The F072 price sqrt(D/Q), congestion c_free*(1+(B/K)^4),
seller-specific tariffs, global original R13/R14 separation, F084F,
and global and worst-group 1e-4 commercial tolerance remain unchanged.
F059 remains unresolved; no Godot/C# code, periodic game scheduler
or subturn edge interpolation is delivered.

Run from repo root, Python 3.12 standard library:

    python -m unittest discover -s experiments/gate09/adr052_population_scale/tests -p 'test_*.py' -v
    python experiments/gate09/adr052_population_scale/ci/run_adr052.py --profile smoke --output .
    python experiments/gate09/adr052_population_scale/ci/run_adr052.py --profile ci --output .

Each run writes JSON and ZIP with separate worker ledger contract,
economic original independent certificate and solver work limits.
Draft PR stacked on ADR-051 #340, NO MERGE.
