# GA-Next R228.2 — operational marginal revenue reference and workload gate

Base: R227.1, HEAD 1636b38. R228.1 was an exact one-idle-interval reference, not a runtime assignment policy.

## Scope
- Rank eligible land transport and port operation alongside the existing crop/native productive candidates only with explicit operational policy. Use the existing signed physical crossing eligibility and the same canonical temporal financial reconciliation, fixed opening infrastructure, effort conservation and owner-credit metric. No separate tariff revenue model.
- Keep the existing API, immutable inputs, authoritative hash, journal, market and scheduling behavior. No automatic mode activation, no repeated evaluation loop in gameplay, no extra solve in the actual settlement. Reference results are not financial authority.
- Expose deterministic counts of full financial evaluations (one baseline plus one per eligible candidate for a land interval); no clock in the economic model. The test logs elapsed time for the six-worker fixture, not a performance SLO.
- For an interval with positive available effort, the economics may legitimately return a zero or negative operational marginal credit; do not fabricate positive benefit or force a role.

## Gate before activation
Reference cost grows with eligible candidates per evaluated interval; with many workers a per-worker complete market solve is unlikely to be economical. Do not connect this oracle to the scheduler yet. Next evaluate representative multi-worker scaling and an economical frozen-baseline alternative against the exact oracle (ranking agreement, owner-credit error, deterministic order, residual time and replay). No performance threshold is canonized until measured. Preserve manual choices and priority of explicit tasks. Do not introduce dynamic congestion or iterate allocations to convergence.

Visual backlog remains #631 and #632. The R227.1 manual successive-cycle visual check is still open; its automated checks are green.
