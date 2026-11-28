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
