# GA-Next — Primary economy contract and roadmap (2026-11-26)

User approved the preceding design proposal in full. Durable backlog: #692. Current development checkpoint: Visual 02B.2 #693, HEAD 62055c8, two successful workflows (R213 job 140198704512: both 100-cycle reference builds, label geometry and strategic hierarchy pass); Windows label and atlas hierarchy acceptance pending. R232F and strategic trade visibility accepted qualitatively on Windows at turn 100.

## Approved base-game design

- Exactly 20 primary commodities: Wood, Stone, Maize, Sorghum, Wheat, Rice, Soybean, Barley; Gold, Silver, Copper, Tin, Iron, Coal; Oil & Gas, Aluminum, Phosphate, Lithium, Nickel, Uranium. No Silicon. Oil & Gas one commodity; Aluminum extraction artwork depicts bauxite. Era groups are thematic rather than mandatory gates.
- Geopolitics of extraction and territorial control, not physical industrial recipes. Deterministic fine-first correlated heterogeneous mineral patches, discontinuous deposits, possible co-occurrence, concentration and extraction difficulty, shared configurable generator. No depletion initially.
- Approved replacement: abstract demand driven by the physical inventory of society, not era curves, a development index or direct commodity-consumption dependencies. Workers contribute by physical worker-time, productive and transport infrastructure by installed capital and type, and military units by count and type when implemented. One generic additive, data-driven commodity profile per source, attributed spatially and consolidated before market clearing. No physical inputs, stocks, mandatory fuel or extra market solve. Existing eight unit demands remain until an explicitly tested opt-in policy. No dynamic monetary rescaling during a match.
- Existing transportation and extraction infrastructure are base scope. Beneficiamento, the third infrastructure type, is deferred as a future expansion alongside energy systems; potential value capture by specialized industrial civilizations and one abstract processing layer, no commitment to a particular implementation. Full industrial chains out of scope.
- Preserve immutable world, canonical hashing, replay, authoritative economic settlement, authorized visibility, ordinary player/AI orders, and temporal worker-time semantics. No speculative industrial or energy code.

## Dependency sequence (engineering proposal)

1. Complete Visual 02: A site names (CI pass); B atlas hierarchy; C depth/lighting; D typography; E continuity of panels/windows. Windows acceptance in batches.
2. R233 economic AI foundation on the approved eight-good economy, using ordinary orders, limited participant information and cheap deterministic heuristics. Keep resource selection catalog-driven, not an eight-item hardcoded assumption.
3. Mineral contract: canonical catalog and deterministic deposits, then extraction productivity, then abstract demand. Gate each increment, preserving legacy fixtures and replay. Validate absent deposits, multiple deposits, low/zero demand semantics against existing market numerical contracts, and scale 20 commodities in geographically dispersed/multi-civilization scenarios. Exact mineral parameters, inventory demand coefficients, capital normalization, idle assets, endpoint/ownership attribution and timing remain open design work before code. Test self-induced profitability, double counting, feedback, positive basal demand and spatial market scaling before integrating.
4. Extend AI to exploitation, trade and strategic expansion around minerals; mobility/logistics including maritime departure port quality proposal #690; military foundations. No forced historical progression.
5. Future expansion backlog: beneficiation and energy. Reevaluate ROI only once base gameplay demonstrates a need.

Artwork for all 20 primary commodities can proceed now in parallel, without requiring decisions about deposit calibration, demand coefficients or future industry. English is canonical in the game UI.

## Visual 02B — CI accepted

The turn-100 Windows globe showed all trade routes but the thickest ink dominated the terrain. Keep the same audited cargo mapping and all authorized edges. Only strategic core width changes to 0.5 + 0.5 * tactical width for positive cargo (zero remains zero), from a 12px cap to 6.5px; casing 0.75px each side instead of 1px. Fixed mapping, no per-turn rescaling, preserve monotonicity within the original saturation range, tactical widths unchanged. Existing clipping, great-circle projection, black/white passes, visibility, and actual M-window pixel acceptance retained. Require both 100-cycle baseline builds and final dense cartographic/style assertions. CI passed at 62055c8; Windows visual acceptance pending. Numeric widths are provisional design tuning, not new economic parameters. No kernel changes, no merge.

## Inventory demand acceptance gates

Use authoritative inventory and existing temporal worker evidence, not new per-worker market solves. Test equal inventory under different turn numbers, localized demand, ownership transfers, construction completion boundary, idle and destroyed assets, no duplicate endpoint attribution, absent/zero-supply commodities, and deterministic replay. Prototype coefficient/normalization choices before kernel integration; specifically test whether an extraction asset can profit primarily by generating demand for its own output. Do not treat any coefficient, cap, capital response or endpoint split as calibrated. Future beneficiation and energy remain deferred. See issue #692 for approval.
