# Global Arena - Gate 09 R26: progressive oracle-budgeted search

Status: research-only Python experiment. R20-R25 and C# authoritative engine unchanged. Passing CI is not proof of equilibrium, F-059 financial tie resolution, or production readiness.

## Hypothesis and design

R24/R25 exhaustively evaluated 5 candidates per step and performed worse than R21's simpler controls. R26 tests whether sequentially evaluating proposals can save oracle work while keeping prices, loads and worst-group regret visible.

All policies start from identical R20 network, flows and price/transport contracts. Each candidate uses the unchanged R20 F-072 best-destination target and R20 flow mixing, and is fully repriced through R14 on its resulting new state. Candidate alphas are tested in order: 1/4, 1/8, 1/16, 1/32, 1/64.

- progressive_global: stop at the first alpha where aggregate F-072 regret does not increase beyond absolute 1e-12 numerical tolerance. This extends R21 backtracking from four to five candidates.
- progressive_joint: stop at the first alpha where BOTH aggregate regret and worst-group regret do not increase (each within 1e-12 tolerance).
- If none succeeds, choose the best among already evaluated candidates: minimum aggregate gap for global policy; minimum worst of the relative aggregate and group regret ratios for joint policy. Log the fallback.
- Never freeze production to create false stability. Every seller retains full supply; all proposals use the same prior snapshot. No change to demand, price curve, route penalties, tariffs or ownership.

## Evaluation budget and controls

Controls are the unchanged R21 decaying alpha (one oracle per update), R21 backtracking (one to four), and R25 elastic opportunity (five). R26 progressive policies use one to five R14 evaluations, stopping early when admissible. Initial state costs one evaluation. When the remaining budget has fewer than five evaluations, only those candidates may run, with truncated search explicitly recorded.

Fixed maximum work budgets: smoke (42 nodes/8 updates/33 calls); CI (42/96/385 and 162/48/193); extended (42/160/641, 162/96/385, 642/24/97). Snapshots at cost frontiers 25,49,97,193,385 use only actual measured states; no interpolation. Some methods consume fewer calls due to the iteration cap or conservative reservations in original R24. Oracle calls are a partial cost proxy; source price/load snapshot work, financial audits and Python wall-clock time are reported separately and are not engine throughput measurements.

Every periodic and final checkpoint checks original R13 versus cached R14 on the identical frozen state; 100% production sold; exact equality of F-084F destination gross and financial credits. The immutable R20 16-file manifest must validate. Replay original R21 trajectories and compare first-accepted R21 backtracking candidate with R26 global policy.

Numerical stability remains five simultaneous thresholds <= 1e-4 for three consecutive updated snapshots: aggregate regret, worst producer regret, maximal price change, maximal edge load change, maximal market quantity change. Even if seen, it is not a convergence or monetary F-059 certificate.

## Commands from repository root

    python -m unittest discover -s experiments/gate09/r26/tests -p 'test_*.py' -v
    python experiments/gate09/r26/ci/run_r26.py --profile smoke --output .
    python experiments/gate09/r26/ci/run_r26.py --profile ci --output .
    python experiments/gate09/r26/ci/run_r26.py --profile extended --output .

Python 3.12 standard library; CI for Windows and Ubuntu, JSON and ZIP evidence. Keep PR Draft, stacked over R25, no merge.
