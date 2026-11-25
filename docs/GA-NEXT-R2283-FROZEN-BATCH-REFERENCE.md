# GA-Next R228.3 — exact frozen-calendar batch reference

Base: R228.2, HEAD 1f33477 (12/12 workflows; 16/16 checks). Six-worker reference log: 7 financial evaluations / 289 ms for crops/native; 9 / 404 ms with operations (single interval, different test calendars). These are CI observations, not a latency guarantee.

The reference now admits a batch of independent idle intervals on one complete immutable calendar. Reuse the baseline financial result once, with owner-scoped credits. For full-turn idle intervals only, reuse exact rankings for identical owner, node, demand multiplier, commodity, output and dormant occupation. Swapping these equivalent worker identities cannot change aggregated market supply/demand or operational effort; candidate owner is unchanged. No reuse across owners/nodes or partial intervals. No reuse across calls/cycles. Do not reuse worker-specific nutrition or demographic evidence.

Tests compare all rows against independent single-interval evaluations in concentrated homogeneous fixtures (6 and 24 workers), exercise partial intervals, reject duplicate requests, and log exact evaluation counts/timings. Homogeneity is intentionally favorable; heterogeneous or fragmented workloads may still need one solve per candidate. There is no approximation, automatic allocation, iterative reassignment, change to scheduler, authoritative settlement, state hash or replay. A representative heterogeneous scaling gate and a low-cost ranking alternative remain open before any runtime activation. Avoid pursuing micro-optimizations if the full-market-per-candidate model remains economically unsuitable.

Cartographic backlog #631/#632 remains separate. Successive-cycle manual visual QA of R227.1 remains open.
