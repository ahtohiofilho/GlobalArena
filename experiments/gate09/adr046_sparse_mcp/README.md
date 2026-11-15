# ADR-046 — sparse-incidence matrix-free F072 complementarity solver

Same economics and independent ADR042 certification as ADR045. The
ONLY changed core is the linear algebra: use analytic edge/market
incidence for Jv and J^T v, and bounded preconditioned CG instead
of allocating dense Jacobian J and normal equation matrix J^T J.
No alternate demand, congestion exponent, tariff or commercial
preference. Root selection still requires original ADR042 F072
global AND worst group gap <=1e-4, R13/R14 and F084F.

The matrix-free data structure stores active column lists, route-edge
incidence, marketplace identities and quartic congestion slopes. It
does not store O(N^2) Jacobian entries. Krylov products are charged,
including transpose products and rejected LM directions.

Tests cover exact matrix-vector parity with ADR045's original
analytic dense J, adjoint identity, finite differences, PCG normal
equation parity, no physical mass creation, unchanged tariff/price,
bounded failure, and original 2562-node Goldberg source construction.

Bounded CI matrix runs controlled two-producer problems, original
42-node economy, stressed 162-node economy, and 2562-node / 7680-edge
R13 scenario (48 producers, 24 priced markets, up to 288 columns).
Passing on the 2562-node scenario is NOT a claim of thousands of
independent producers or an achieved game runtime benchmark.

For comparison, the first controlled two-producer fixture also runs
the unchanged original ADR045 DENSE solver. Both must ultimately be
certified by the unchanged ADR042 independent original F072 oracle.

Python 3.12, repository root:

    python -m unittest discover -s experiments/gate09/adr046_sparse_mcp/tests -p 'test_*.py' -v
    python experiments/gate09/adr046_sparse_mcp/ci/run_adr046.py --profile smoke --output .
    python experiments/gate09/adr046_sparse_mcp/ci/run_adr046.py --profile ci --output .

All NO-GO are preserved in JSON/ZIP. The CI's essential contract
is successful exact algebra parity and the controlled small fixtures;
a 2562-node NO-GO must be reported honestly and becomes a scaling
issue to address, never permission to change F072.

Draft PR stacked on #334, NO MERGE or C# changes, F059 unresolved.
