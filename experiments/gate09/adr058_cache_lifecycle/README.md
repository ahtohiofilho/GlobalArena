# ADR-058 — two real loads, two separate FrozenRouteCache snapshots

Laboratory-only follow-up to ADR-057. It checks cache invalidation and
memory retention when real worker-financed routes change. The original
market equilibrium and independent certificates are NOT modified.

For 192 and 384 physically demanding nodes (both 4,096 workers on the
5,762-node Goldberg topology), construct the original worker flow
allocation A, then an independent *physically valid* allocation B by
moving 25% of a single funded column's original volume to a second
funded column within **each same producer group**. This preserves all
worker demand and group supply. Do NOT claim that A/B are F072 equilibria.

Construct cache A from loads A, warm and repeat its fixed keys. Construct
a NEW cache B from loads B, warm and repeat; compare original seller-
specific edge weights to prove that loads matter. The original cache A
must NEVER be used to serve B. Release A and observe Python-traced heap
with B still alive; verify B's responses remain stable.

tracemalloc starts AFTER building the original network: tracked bytes
include probe/cache allocations but not full graph, native allocations,
or the F072 solver. RSS from the OS is process-lifetime peak and will
not prove physical memory reclamation after GC. The compact path-result
digest covers sampled destinations and is NOT independent F072 proof.

The CI runs 15 structural/negative contracts (Windows/Ubuntu), separate
192 and 384 cache snapshot probes on each OS, and the untouched ADR055
independent ORIGINAL F072 mandatory regressions for 48/96/192/384 nodes.
No new numerical work budget is introduced and no 2,048-node F072 is run.

Python 3.12, standard library; from repository root:

    python -m unittest discover -s experiments/gate09/adr058_cache_lifecycle/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr058_cache_lifecycle/ci/run_adr058.py --fixture 192 --output .
    python -u experiments/gate09/adr058_cache_lifecycle/ci/run_adr058.py --fixture 384 --output .

JSON and ZIP evidence, no production approval, no F059 ratification,
no C#/Godot modifications; Draft PR stacked on #346 without merge.
