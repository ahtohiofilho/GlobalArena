# GA-Next R191 — Construction Work

Approved product direction following R190:

- Work is the common effective labor input for construction, production and operations. Internally distinguish time from individual productivity: Work = time × productivity. Time, not Work, is the conserved resource across a worker's future editable command queue. At neutral productivity they are numerically equivalent in whole-unit normalization.
- Initial individual productivity is 1, uniform. No civilization-wide productivity multiplier. Future individual differentiation related to consumption is intentionally deferred; no nutrition or consumption function is approved by this revision.
- Required construction Work = funded investment / explicit money-per-Work conversion rate. The pure quote uses integer micro units, rounds up once per project, and rejects invalid or overflowing values. No default economic conversion rate or extra terrain multiplier is introduced. Existing construction monetary reference already affects the investment needed for a given freight benefit.
- New infrastructure benefit is available ONLY upon full completion of its project, not proportionally to partial progress. Existing infrastructure keeps its prior benefit. Future intraturn integration must make the new benefit available from actual completion time, not retroactively.
- R190 effort names remain in the accounting type for compatibility; they represent effective Work for this construction quote. Residual Work is not automatically residual time under heterogeneous productivity.
- No material inventory, worker occupation, authoritative project persistence, time scheduler, treasury mutation, activation rule in runtime, or UI change is implemented here. Current monetary executor is unchanged until integration gates. Next: persistent construction project contract and funding lifecycle.
