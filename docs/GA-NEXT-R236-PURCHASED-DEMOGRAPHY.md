# R236 — Purchased demographic growth

Base: R235H #735 c47c9f589b44c725ffaae33337aea2b90a5a1806. All five triggered checks approved; Windows economic checkpoint reached turn 30, with productive activation and adjacent human trade observed. AI occupied two sites early and no further expansion was observed. The existing expansion policy requires three available workers in one node; fixed census is a plausible constraint, not a journal-proven cause. Keep current transport, movement, market and AI guard parameters until purchased growth can be observed. Visual issues #736/#737 and legacy market issue #729 remain separate; exact barley volume and latency not measured in Windows.

Authoritative user decision #724: deliberate purchases, lifetime acquisition count instead of surviving census, no free births in purchased mode, same rules for human and AI, weighted random eligible individual with weight equal to turns since last selected birth, no +1. Preserve zero-weight newborns and selected parents. Existing alternative demographic modes stay opt-in and mutually exclusive.

## R236A — Pure price quote

Implement only the provisional linear member of the proposed power family, alpha = 1. Given explicit positive C0 and B, and caller-supplied next one-based historical index N, each marginal micro-money price is ceil(C0 * (B + N - 1) / B). Sum these individually rounded prices for a batch. Exact integer arithmetic, logarithmic floor-sum calculation, explicit overflow, no census input or default calibration. The authoritative historical baseline convention (including initial workers) and C0/B are still open; no runtime setting chosen. Other exponents can be evaluated in a later increment. Tests verify composition, rounding and extreme inputs. No treasury debit, worker creation, state/hash changes or new gameplay flag.

## Subsequent gated increments

- R236B: canonical historical acquisition ledger, initial baseline and persistent waiting counters; compatibility and hash tests. Decide initial index, extinction and eligibility semantics explicitly before enabling.
- R236C: weighted individual lottery on authorized eligible physical land presence, deterministic replay, no zero weight selection; benchmark weighted versus equal-worker fallback. Propose distinct positive-weight parents per batch, no newborn participation in the same close, and reject a batch that exceeds available eligible parents before debit; exact timing and eligibility require validation. No hidden +1.
- R236D: atomic purchase admission, pre-market funding and post-close newborn publication, no free births in purchased mode, cash sufficiency, journal and replay, no debt. Resolve any eligibility changes between admission and birth before implementing debit semantics.
- R236E: player purchase command and minimum UI, birth marker; R236F: AI purchase decision with same quote and authority, competitive trade-off with other strategic actions; R236G: integrated tests, performance and Windows observation. Names are provisional.
- R237: Explorer and GA-Arte integrated; project screen #737 and strategic marker density #736, incremental visual asset production.

No local compile or merge. R236A CI pending.


## R236B — Canonical historical acquisition ledger

Base #738 20a40dd: seven workflows / eleven checks approved. Explicit opening deployment only, in a signed persistent world with authoritative worker placement and a treasury before first settlement. Each civilization retains its initial worker count, plus a separate monotone cumulative purchased count. Provisional baseline convention: initial workers consume the first historical price indices, so a civilization with three opening workers quotes its first purchase at N=4. A smaller living census does not lower the price; extinct accounts retain history, but extinction/re-entry policy remains open. The two positive linear price parameters must be explicitly supplied; no default or launcher change.

The immutable ledger is propagated through all WorldState copy transitions. Its detached increment cannot be independently attached to a world: paid acquisitions must later be committed with debit and newborns. Reuse existing per-worker waiting counters, initialized to zero, without incrementing or selecting parents yet. Legacy free-birth append and demographic close reject purchased worlds. Existing undeployed worlds and hashes remain unchanged; opt-in envelope v32 hashes price parameters and sorted historical accounts, using existing individual counter encoding. No new market solve.

Tests cover initial index, detached monotonic accounts, independent civilization counts, price inputs, overflow, opening admission, counter hashing, preservation through ordinary state copies and productive funding, calendar/market settlement and replay, and rejection of free growth. No actual purchases or births; no wait advancement before R236C/D. No loss/death implementation, extinction rule, cost calibration, Godot deployment or AI purchase. No local build or merge; CI pending.
