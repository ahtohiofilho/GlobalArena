# Global Arena — Gate 09 R20: bounded integrated commercial sweep

**Status:** experimental, non-authoritative. **Not** an integrated C# engine, production clearance or equilibrium proof.

## What is actually executed

The R20 runner embeds, unmodified, the 11 Python source modules required by the earlier R13/R14 Goldberg benchmark, extracted byte-for-byte from the R18 package. In each snapshot it performs:

- Topological Goldberg construction with multiple producer groups, items, destination markets and capital holders.
- F-072 evaluation of routes against current **congestion and prices**. Original versus cached evaluators are compared using complete checkpoint equality.
- Damped flow allocation, with frozen physical/price state **recomputed** for the next checkpoint.
- Conversion of production into destination sales and fixed-point F-084F-style financial allocation. Conservation and full sale are asserted in every snapshot.
- Independent R2 oracle and settlement replay **on the small scenario** to check recorded gaps and financial provenance.
- Fixed iteration-budget trajectory and damping sensitivity (three alphas in CI), no convergence assertions.

`python ci/run_r20.py --profile smoke` runs a small local test. `--profile ci` executes small (42 nodes), medium (162), and large (642), plus sensitivity (default). `--profile extended` includes 1442 nodes and larger update budgets. The `--output` argument chooses where the JSON + ZIP evidences are saved.

`python -m unittest discover -s tests -v` performs integrity, financial, independent replay and prefix checks. Python 3.10+; no third-party dependencies.

## Performance interpretation

The R20 observations are *Python* fixed-budget multi-market sweeps. They are more realistic than the R19 standalone ratio-conversion microbenchmark but do **not** measure the R19 C# IEEE-754 code inside an integrated C# solver. Their wall-clock samples depend on machine/runner. The cached and baseline outcomes must agree economically; faster timings are not an acceptance gate. Gaps are recorded, **not presumed to decrease each step**. The original R13 scenario class caps iterations at 10.

The R19 C# ratio-conversion parity remains certified separately by PR #308. No mathematical claim that fixed-point C# conversion determines overall clearing throughput is made by this R20 lab.

## Gate governance

- **Open:** F-059 financial ties, tariff intensity F-073, congestion exponents, global or local equilibrium, original Gate08 dataset replay, integrated C# solver port, performance of game runtime, multi-turn worldstate.
- **Preserved:** F-041, F-043, F-049, F-060/F-062, F-071, F-072, F-081; total production sold, destination gross = credits; no buyer wallets/inventories.
- **No changes:** authoritative `GlobalArena.Kernel`, `GlobalArena.Runtime`, `GlobalArena.Simulation`, Godot, journal, saves or treasuries.
- **CI:** Windows/Linux automated Python-only contract and bounded sweeps. Does not install or exercise .NET because this package has no integrated .NET solver.

R20 is an evidence-gathering checkpoint, not a release gate. Never merge automatically.
