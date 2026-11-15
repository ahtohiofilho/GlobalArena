# Gate09 ADR-048 — economic density, CPU budget reservation planning

The Global Arena economy must scale to THOUSANDS of Goldberg nodes.
ADR-047 found an independently certified F072 equilibrium for 2,562
nodes with 48 producer groups and 24 markets. That did NOT validate a
denser economy.

This runner uses the EXACT ORIGINAL immutable R13 synthetic Goldberg
world constructor and ADR-047 producer-block NCP solver; it changes
ONLY the number of producers, products, markets, and geographical nodes
in separate registered fixture configurations.

Profiles:
- smoke: 2,562 nodes / 48 groups / 24 markets (ADR047 regression)
- ci: 2,562 nodes / 96 groups / 32 markets (first density test)
- extended: 96/32 AND 144/48 on 2,562 nodes
- geography: 5,762 nodes / 144 groups / 48 markets

This does NOT rewrite producer-market preference, prices P=sqrt(D/Q),
quartic congestion c_free*(1+(B/K)^4), tariff ownership or financial
conservation. Every claim of economic success REQUIRES independent
original ADR042 R13/R14 + F084F + global AND worst group F072 gaps
<= 1e-4. An uncertified state NEVER becomes an accepted final state.

A CycleReservation model checks an EX-POST nominal period budget and
a REQUIRED reservation for other game layers; it does not implement
real CPU-time limits, yield points, asynchronous C# scheduling or a
Godot runtime. The supplied sensitivity budgets are examples, NOT
canonized gameplay timing. A hypothetical admission is not a real
game snapshot commit.

For reproduction, Python 3.12 standard library, repository root:

    python -m unittest discover -s experiments/gate09/adr048_density_budget/tests -p 'test_*.py' -v
    python experiments/gate09/adr048_density_budget/ci/run_adr048.py --profile ci --output .
    python experiments/gate09/adr048_density_budget/ci/run_adr048.py --profile extended --output .
    python experiments/gate09/adr048_density_budget/ci/run_adr048.py --profile geography --output .

Each run writes a structured JSON and ZIP with ALL work units, original
commercial pass/no-go and observed Python runner wall time. A green CI
on the first density experiment validates the runner/invariants; it
does NOT mean the denser economy achieved F072 until the separate
static_original_F072_go boolean is true.

Draft PR stacked on ADR-047 PR #336. No merge or C#/Godot changes.
F059 financial tie policy is still separate and unratified.
