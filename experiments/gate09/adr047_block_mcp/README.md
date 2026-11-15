# Global Arena ADR-047 — producer block preconditioned MCP

Experimental multi-producer, multi-market Python numerical solver.
The ORIGINAL F072 economy is unchanged. Quartic congestion, seller
tariffs, full physically conserved production, prices P=sqrt(D/Q)
and original static commercial tolerance 1e-4 are mandatory.

Each producer's independent route variables plus its multiplier
form one EXACT local principal block of the matrix-free analytical
normal equation. Sparse-incidence construction of the local block,
small Cholesky and global preconditioned CG avoid all-global dense
Jacobians and dense normal systems.

Active columns start with ALL existing funded routes plus the two
cheapest ORIGINAL reciprocal-H route options of each producer.
Every computed restricted NCP root must be followed by complete
seller-specific global shortest-route separation, and ALL omitted
strictly better routes must be added in another generation.

A final PASS only occurs after the ORIGINAL independent ADR042
certificate, including R13/R14, F084F and global/worst group
F072 regret <=1e-4. Q=0 price flooring, demand changes and
simplification of quartic congestion are forbidden.

Partial numerical attempts are never committed; their independent
F072 audit, if available within the predeclared budget, is labeled
PROVISIONAL. On work-budget limits the status remains NO-GO.

Run at repository root:

    python -m unittest discover -s experiments/gate09/adr047_block_mcp/tests -p 'test_*.py' -v
    python experiments/gate09/adr047_block_mcp/ci/run_adr047.py --profile ci --output .

CI on Windows/Ubuntu covers 22 unit tests, 2 controlled
multigroup models, 42/162-node synthetic economies and
2,562 Goldberg nodes with 48 producers, 24 markets.
No game engine or main changes. F059 remains unratified.
PR Draft stacked on PR #335.
