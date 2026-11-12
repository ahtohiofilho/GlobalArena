# Global Arena — Gate 09 R21: diagnostic convergence trajectories

**Experimental only.** R21 does not implement an authoritative clearing solver, does not certify equilibrium and does not ratify F-059. It depends on the frozen 16-file R20 package and validates its SHA-256 manifest before execution; no R20 source file is modified.

## What changed after R20

R20 successfully compared identical snapshots over at most ten updates but did not satisfy F-072's gap target of 1e-4. R21 increases the **diagnostic** horizon independently of the legacy R13Scenario iteration limit, and studies stability of simultaneous repricing, edge congestion and route selection.

Five policies use the identical R20 R14 cached oracle at **each post-update** state:

- fixed alpha = 1/16;
- fixed alpha = 1/8 (R20 reference);
- fixed alpha = 1/4;
- decaying alpha = (1/4)/sqrt(1+(iteration-1)/8);
- counterfactual backtracking over alpha = [1/4, 1/8, 1/16, 1/32].

Backtracking takes the first proposal whose **recomputed** gap is no higher than the previous gap (1e-12 tolerance). When all proposals increase the gap, it takes the smallest-gap candidate and records the forced uphill step. This is an explicitly costly **diagnostic**, not an approved production optimization. No artificial demand, buyer wallet, inventory, tariff policy, market-utility bias, or economy-wide conservation exception is introduced.

Each trajectory records F-072 relative and maximum group gaps, tie counts, active columns, flow SHA, price and load change, target churn, uphill steps, minimum observed gap, the first three consecutive snapshots to meet the numerical thresholds (if any), and number of candidate evaluations. At audited checkpoints it compares the cached oracle against the unmodified original R13 evaluator and independently checks F-084F financial projection, complete sales, and monetary conservation. A successful diagnostic run does **not** mean convergência ou produção aprovada.

## How to run

From repository root:

    python -m unittest discover -s experiments/gate09/r21/tests -p 'test_*.py' -v
    python experiments/gate09/r21/ci/run_r21.py --profile smoke --output .
    python experiments/gate09/r21/ci/run_r21.py --profile ci --output .
    python experiments/gate09/r21/ci/run_r21.py --profile extended --output .

Smoke uses 12 updates on 42 nodes; CI uses 96 on 42 nodes and 48 on 162 nodes; extended uses 192/96/24 on 42/162/642 nodes. Profiles produce JSON and ZIP evidence under the specified output directory, including failures. No third-party Python modules, .NET SDK or Godot required. Windows/Linux CI runs both unittest contracts and CI diagnostic profiles.

## Decision gate

Interpret the traces before revising the algorithm. If substantial improvement stalls, analyze whether non-monotonic best responses, path switching, fixed point instability, capacity penalties, precision or financial-route ties are the dominant cause. Do not weaken the 1e-4 threshold to claim success. F-059 remains separate even when all numerical thresholds happen to pass. PR is stacked on R20, stays Draft, has no merge and does not touch GlobalArena.Kernel, Runtime or Simulation.
