# R235B — Generic infrastructure capital response

Base #721 69f4965: 3 workflows / 3 checks passed. Transport construction UI acceptance still requires manual use.

## Confirmed design

Productive capital is node/activity based, not per crop. Agriculture shares a single capital pool across maize, sorghum, wheat, rice, soybean and barley at the same node. Forestry and mining have independent activity capital. Preserve the same general response function for present and future productive infrastructure; no per-commodity coefficients or age-based gates in that function.

Proposed symmetric response to activated capital I with positive monetary construction reference B:

- M = sqrt(1 + I/B).
- Productive output per unit of effort: P(I) = P0 * M.
- Existing R112 physical freight: C(I) = C0 / M.

Natural productivity P0 remains determined by existing resource/geographic suitability; zero natural output remains zero. A shared agricultural capital pool gives the same multiplier to each crop, not the same output. Construction effort, timing and geographic constraints are independent of the multiplier. Only completed activated capital may contribute; pending funded construction confers no benefit. No artificial game-stage cap; finite numeric range and checked overflow remain technical constraints. Do not convert time directly to money; references use the same canonical monetary micro-units and construction funding/work framework as transport, but their absolute values are calibration parameters, not necessarily equal across activities. No production reference values selected in this increment.

## Implementation

Pure FoundationInfrastructureCapitalResponse computes both dual responses with exact integer square roots and BigInteger intermediates, one floor at output/cost micro-unit resolution; freight retains its one-micro minimum. The authoritative R112 projector delegates its existing arithmetic to this function, keeping all prior edge validation and zero-capital behavior. Its existing integer-root helper delegates to the same implementation, preserving compatibility. Tests exercise natural baseline, zero suitability, exact values, diminishing returns, monotonicity, scaling capital and reference together, overflow, original R112 floor invariants, all six actual natural crop yields, and wood/stone illustrative natural yields.

## Remaining integration

This is mathematical foundation, not a deployed productive capital ledger. Next increment must establish a canonical node/activity target, exclusive ownership and activated/pending capital, construction reference policy, persistence and hash/replay. Then extend authoritative funding, worker Build approach, shared temporal execution and activation to node projects; integrate production and automatic base activity selection using the same opening-state activated capital without double applying the multiplier or introducing extra market solves; expose in City > Production and add AI investment assessment. Decide activation boundary, ownership and treatment of workforce/capital interactions in that integration. No productive output change in existing worlds, no Godot activation, no local compilation or merge. CI pending.


## R235C — Canonical productive ledger foundation

Base #722 2899bae: all 13 triggered workflows / 17 checks passed. Stable activity codes Agriculture, Forestry and Mining identify the productive target together with the physical node. All six crops share Agriculture. Explicit positive monetary construction reference per activity is configured once at empty initial deployment; no default production calibration selected. The first reservation holds exclusive title for that node/activity; only one unactivated project is allowed per target, and additional investment is reserved only by the same investor. No ownership transfer or quotas. The existing exclusive node labor policy remains separate and unchanged; ownership of a productive target does not authorize work by foreign workers at that node. Future command admission must enforce geography, labor and ownership consistently.

Immutable project history derives reserved owner, activated and pending capital; completion alone gives no active capital until explicit commissioning. Amounts and required/completed effort are positive or bounded as appropriate; reject overflow of cumulative capital, duplicate projects, foreign reinvestment, concurrent pending projects, overwork and repeat activation. References and projects are canonically sorted. Detached pure ledger operations are not authoritative financial or worker commands. Productive IDs are local to this ledger and cannot be passed to existing edge Build orders as if they were transport project IDs.

WorldState can deploy only an empty ledger, in a signed fine-first persistent monetary world before economic settlement, and cannot inject populated ledgers through the public deployment method. Preserve ledger reference through all immutable world copy paths including births, transport, treasury, orders, travel and revision. Hash v31 envelope commits references and project fields, including owner, location, activity, amounts, effort and activation. Uninitialized worlds retain existing hash bytes and formats. Tests cover detached ledger transitions and empty deployment/hash sensitivity/copy preservation. No populated-world journal replay claim yet: R235D must introduce typed construction order targets and atomic funding/debit, physical work and activation events, with independent replay before any project may enter WorldState. R235E will apply only activated capital to natural output and AI automatic activity ranking. No changes to actual production, market, worker execution, existing construction or UI in this increment. No local compilation or merge; CI pending.
