# ADR-061 — one-shot measurable CPU ROI for original F072 solver

High-ROI experiment. Avoid another series of low-ROI memory probes.

Target: R13's original path_audit repeatedly evaluates edge congestion
and external tariff weights during Dijkstra/shortest-path-DAG passes,
although those weights are frozen inside one snapshot.

In this NEW LAB ONLY module, `R13WeightsOncePerSnapshot` independently
reproduces the exact original Dijkstra and canonical path/tie ordering
while precomputing seller-specific edge costs once per physical loads
object. Zero-load route checks reuse weights across producers on the
same original network. There is NO reuse across changed nonzero loads.

IMPORTANT: the original R13 binding is monkeypatched only during the
candidate solve and always restored. After restoration, a SECOND,
**fully unmodified ADR042 static certificate** is independently run
on the committed final economic allocation. If its gap/status differs
from the candidate solve or either original certificate fails, CI red.
The original independent verification never trusts an optimized route
audit alone. The controls are entirely unmodified.

The economic oracle, prices sqrt(D/Q), quartic congestion, R14,
tariffs, seller ownership, workers and original numerical work limits
do not change. No C#/Godot implementation is touched.

Two cases: 192 and 384 demand nodes, both 4096 workers and the 5762
Goldberg nodes. Full original mandatory 48/96/192/384 F072 regressions
run separately on Ubuntu and Windows.

ROI gate: ONE shot >=20% CPU reduction at 384 nodes on both systems,
and <=10% peak RSS increase. Equivalence is mandatory, and failure
is red. A non-improving but semantically correct trial is explicitly
NO_GO for the optimization, even if workflow jobs complete successfully.
CI timing is not a game performance or a causal speedup guarantee.

Run from repo root (Python 3.12 standard library):

    python -m unittest discover -s experiments/gate09/adr061_route_precompute/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr061_route_precompute/ci/run_adr061.py --fixture 384 --policy baseline --output .
    python -u experiments/gate09/adr061_route_precompute/ci/run_adr061.py --fixture 384 --policy precompute --output .

The CI performs cross-policy same-HEAD/OS ZIP evidence comparison.

Draft stacked on PR #349; no original source edits, no F059,
no merge, and no authorization to claim 2048 demand-node equilibrium.
