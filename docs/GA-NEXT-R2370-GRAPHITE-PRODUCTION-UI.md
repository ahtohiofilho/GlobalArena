# GA-Next R237.0 — Graphite production UI contract

Status: project owner approved the Graphite visual direction and conceptual full-dashboard mockup on 2026-11-28. Technical contract for implementation; actual Godot visual acceptance remains pending.
Governance: #747, approval comment 7234616870; #736 (strategic markers), #737 (Construction Project).
Baseline: R236G #746, acc2747ce81b257d34de694d154a7d0e4f056aa0. This increment is documentation only.

## 1. Scope and non-goals
Build release-target UI incrementally, not another debug skin. Retain actual tactical renderer, North-Up, focus/drag/zoom, M and E behavior, right-click behavior, authoritative selection, economic rules, privacy, hashes, replay, turn processing and command semantics. Do not recalculate the market, change commodities, implement new identities, naval/fog/AI rules, or merge without owner authorization. Mockup terrain, flag, names, numbers and artwork are illustrative, not new game mechanics. World informational view is distinct from the globe.

## 2. Shared design tokens
Use one shared Godot Theme resource with named style roles; compose reusable controls rather than copying style overrides into each view. Keep visual assets separate from simulation and snapshot data. Approved reference colors (sRGB):
| Role | Hex |
|---|---|
| Surface/base | #181A1C |
| Surface/panel | #292C2F |
| Surface/raised | #393D40 |
| Surface/recess | #111315 |
| Text/primary | #DFE1E3 |
| Text/secondary | #A1A5A9 |
| Semantic/positive | #89AB87 |
| Semantic/attention | #D5AD66 |
| Semantic/negative | #C47470 |

Semantic accents convey meaning only; do not use them as decorative category colors or civilization ownership. Civilization color comes from authorized model identity, not UI theme. Resource icons retain source colors. Use text/icon/shape alongside color for semantic states.

Typography: clean legible sans for controls and tables, stable-width numeric figures when supported; title wordmark may be distinct but not period-specific. Initial sizes in reference UI pixels: auxiliary 12, body 14, important operational 16, headings 18–22. Do not assume a particular bundled font until licensing, coverage and actual Godot rendering are verified. English canonical UI. Allow long labels, ellipsis only with access to full value, no clipped important controls. Avoid uppercase paragraphs. Align numeric columns right.

Spacing base 4; use 4/8/12/16/24. Reference shell at 1920x1080: top 56, bottom 36, right 320; compact controls 28–36, command icons 20–24. Before frame margins the tactical area is about 76% of the screen. These are initial targets, not fixed physical pixels at every resolution. Use container-driven sizing and tested scale settings, not independently scaled coordinates. Right column stable during selection/navigation; resizing only via deliberate supported action, never automatically. At smaller viewports prioritize map and essential commands; scroll only bounded content (queue/details), keep key actions reachable. Initial QA targets 1920x1080 and 1366x768 plus actual Windows resolution; proposed secondary resolution subject to available test environment.

## 3. Material and component rules
Continuous charcoal frame, subtle fine grain, low-relief compartments, restrained highlights and soft shadow. Prefer reusable static textures, StyleBoxFlat/StyleBoxTexture and simple baked relief; no compulsory expensive shader, full-screen blur or per-control render target. Small corner radius (2–4 initial px); thin dividers only where meaningful. Avoid stacked cards, thick borders, ornamental gradients, oversized buttons. Texture never reduces contrast or competes with map. Use consistent padding and alignment. Use original mockup as visual target, not a flattened UI screenshot.

| State | Behavior |
|---|---|
| Normal | Graphite raised surface, legible label, restrained highlight |
| Hover | Slight luminance increase; tooltip only when informative |
| Pressed | Recessed appearance; no size/layout movement |
| Selected | Stable outline/indicator and text; not merely hover |
| Focused | Visible keyboard focus independent of hover/selection |
| Disabled | Lower emphasis, no invocation; explain unavailable action where useful |

Proposed state colors for calibration: hover #4D5155, pressed #151719, selected border #D5AD66, focus #C3C7CA, disabled foreground #777B7E (disabled only). No state changes geometry or moves map viewport. Hover/press transitions short and optional (initial 80–120 ms); no delay to command dispatch; respect reduced-motion preference if exposed. Do not add runtime animation merely to meet specification. Keyboard focus must not trap map controls; when text entry or modal owns input, do not leak shortcuts. Mouse hover never issues commands.

Buttons: primary only for the locally decisive action, notably End Turn; secondary compact icon/text; destructive distinct with existing command confirmation when required. Icons alone need accessible name/tooltip. Tables: compact headers, aligned numbers, selected row, no per-row cards. Progress: authoritative fraction and label, no invented ETA; distinguish queued, moving, working, constructing, complete. Tooltip: brief anchored contextual explanation, initial 0.5–1 s hover delay; keep within usable bounds, avoid input interception; interactive HUDs separate from informational tooltip. Popups: anchored, dismissible, no implicit economic submission. Windows: common title, close, breadcrumb/history where applicable, consistent tab treatment and scrolling; content based on authorized read model; no auto-commit on open/back/close.

## 4. Production shell
Top: civilization identity (flag/color name when authoritative data available), treasury, workforce/population, turn, End Turn. No fabricated procedural flag or cultural identity. Inspect and reuse legacy algorithms when supplied (24 cultures, 24 colors, names in 24 languages). Until then show existing truthful identity; leave unavailable fields absent. End Turn invokes existing single authoritative path; prevent duplicate dispatch while processing; preserve keyboard E.
Map: same viewport/renderer, camera, movement, hit testing, North-Up, M switch and focus sync; no strategic tile borders enabled by default in tactical view. Overlay panels consume input only within their own interactive bounds. Map hover/selection remains stable.
Right: independently retained operational selection, compact Worker art recess beside status and action controls, queue and progress. No duplicate full inspector. Empty state compact and honest. Selection persists across Explorer navigation; no auto-open/resize on city click. Existing actions remain reachable, including queue edits, movement, assignment and explicit cancel.
Bottom: compact context, notices and genuinely useful quick commands; no duplicated permanent economic dashboard or slogans. Avoid End Turn duplication. Debug controls stay in diagnostics path, not silently lost.
Explorer: stable typed entity identity (World, Civilization, City, Production, Worker, Terrain, Connection, Route, Construction), one canonical detail per entity, multiple links/direct map entry; breadcrumbs/history reflect navigation path, not ownership. World is informational distinct from globe. No hidden/foreign-private data revealed via links, tooltip, history or counts. Do not introduce new simulation state via navigation. Construction project view exposes target, funding, progress, workforce and actions; intentional funding-only allowed after explicit confirmation at submission/leave boundary as defined in #737; ordinary close/navigation never silently commits. Exact trigger to be tested in R237.3. City Production and Routes tabs use existing committed information; no independent market computation.

## 5. GA-Arte asset policy
User supplied HTML transfer package of 20 canonical SVG sources, names and index, viewBox 0 0 32 32. Food: wheat, rice, barley, soy, sorghum, corn. Base: stone, wood. Extractive: gold, silver, copper, tin, iron, coal, oil-gas, aluminum, phosphate, lithium, nickel, uranium. Preserve source geometry, fills, strokes, proportions and data-fill attributes. Source package is not yet committed by this PR. Keep originals immutable; generate separate derived monochrome assets if needed. HTML CSS rules: all descendant strokes #E5E5E5; data-fill=accent fill #E5E5E5; data-fill=dark fill #151515. This CSS is a reference, not guaranteed to work in Godot. Compare output at 24 and 44 px (and actual 20 px command size where used); check loss of detail in monochrome, especially strokes on originally unstroked shapes. Record provenance and checksums when importing; do not imply new kernel resources by catalog existence. Do not redraw without a demonstrated problem.
Worker: original full-body art in spirit of Civilization III/IV era pre-rendered unit art; readable silhouette, civilization-color layer, reusable for panel and tactical map; no military catalog yet. Mockup artwork is only an art-direction reference. Produce and test actual asset before calling it canonical. GA-Arte is transverse, not a final pass.

## 6. Acceptance and delivery gates
R237.0 documentation: approval of visual direction recorded; shared tokens, states, layout and acceptance contract versioned. No runtime changes; review documentation diff against #747. Runtime validation not claimed.
R237.1: production shell and shared Theme in small PRs; preserve old command paths until tested; actual Godot smoke and Windows screenshots at real resolution. Verify top/bottom/right layout, no clipping/overlap, map area and all existing shortcuts/clicks, focus sync, no duplicate turn, authorized data. Do not implement new renderer or full Explorer simultaneously.
R237.2: right panel and original Worker art, queue controls, selection retention.
R237.3: canonical Explorer views, staged one at a time; direct/multiple paths, history, privacy, #737 funding-only confirmation.
R237.4: asset catalog normalization continuously alongside slices.
R237.5: #736 globe marker publication and hemisphere/zoom correctness; LOD only with documented visual or performance need.
R237.6: integrated usability, visual consistency, performance and Windows acceptance; diagnostics retained separately.

For each runtime slice: automated regression checks, actual Godot scene/interaction acceptance where relevant, manual Windows visual acceptance, links to PR/CI and launch instructions when applicable. Compare the same scene/resolution before and after on target Intel UHD/OpenGL setup; record frame time and UI responsiveness, map hit tests and resource cost; investigate measured regressions before approval, no invented FPS or blanket LOD. Test at least normal/hover/pressed/selected/focus/disabled states, keyboard and mouse, empty/long lists, long labels, turn closure and map switch. Preserve deterministic replay/hash and privacy checks. No merge without authorization. Report CI status truthfully; documentation-only gate does not replace runtime gate. Existing economic/naval/fog/conflict backlog remains separate.
