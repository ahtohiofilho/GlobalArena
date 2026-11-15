# ADR-057 — frozen route-cache microprobe (lab only)

A targeted follow-up to ADR-056 hotspot observations. No new economy,
no solver acceleration, no new scale, no C#/Godot modifications.

The original ADR-052 worker-ledger fixtures for 192 and 384 demand nodes
both retain 4,096 workers. The experiment creates a **FrozenRouteCache**
for one unchanged physical snapshot, calls `paths(origin,seller)` for
every producer group, then repeats the same queries. We measure
instrumented wall/CPU, original path/Dijkstra function call counts, and
cache Python allocations (tracemalloc started AFTER network build).

A measured decline in Dijkstra calls is only a proxy for possible reuse,
not a cache hit rate. A drop in repeated-pass time does not establish
solver-speed improvement. Cache fields use shallow getsizeof only, while
RSS measures the process lifetime high-water. None of these measurements
captures the original F072 solver's exact memory allocation.

CI also runs the unchanged ADR-055 48/96/192/384 mandatory ORIGINAL
independent F072 regression on Windows and Ubuntu. Microprobe is NOT
another F072 certificate. The 2,048-demand-node scenario remains a
non-solved spatial ledger.

From repository root, Python 3.12 standard library:

    python -m unittest discover -s experiments/gate09/adr057_route_cache/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr057_route_cache/ci/run_adr057.py --fixture 192 --output .
    python -u experiments/gate09/adr057_route_cache/ci/run_adr057.py --fixture 384 --output .

Produces JSON and ZIP evidence. Always lab-only, PR Draft stacked on
ADR-056 PR #345, no F059 ratification, no C#/Godot production merge.
See docs/ga-gate09-adr057-route-cache-draft.md.
