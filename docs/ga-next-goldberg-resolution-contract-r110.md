# GA-Next — Goldberg resolution contract (R110)

## Architecture decision

The physical fine-first geography is authoritative. A strategic Goldberg map is an aggregation/selection layer derived from the same signed physical world, **never an input to terrain generation**. The renderer, human/AI turn host, trade overlays, and physical worker identities must agree on the same world generation request, fine topology, and refinement contract.

The Goldberg pair is a **session-start choice** and must not change during a committed session. One immutable startup request and refinement should be passed through the globe, tactical map, and physical workforce. Do not recreate an independent Goldberg grid in a presentation component.

## Supported/approved versus experimental

- Current production/default reference: strategic G(3,0), physical G(18,0), class-I scale 6.
- Audited alternative **QA only**: G(2,0) to G(12,0). The real Godot R103 test closed two signed economic cycles, with private settled route counts 57 and 37 in its deterministic fixture.
- Class-I scale candidates 2, 3, 4, 5: research diagnostics only, **not permitted** in `PhysicalTacticalIncidenceMapper.Materialize`. The existing scale-six ownership partition has frozen audit signatures and cannot be broadened by changing a constant.
- Neither arbitrary G(m,n) nor mid-game resolution change is approved. Class III and mixed-axis cases require separate lineage evidence.

## Evidence gates before a future scale can be enabled

1. Authoritative lineage with canonical tile IDs; replay and cross-platform hash invariance.
2. Complete fine-tile coverage, one-to-three coarse incidences per fine tile, anchor provenance and unique signatures for strategic edges and vertices. Do not assume the scale-six counts hold for other scales.
3. Every coarse ownership footprint connected on **physical** adjacency, and every strategic neighboring pair has physical contacts.
4. Real fine polygon edges and vertices yield consistent territorial boundaries: no invalid endpoints, unintended non-neighbor contacts, duplicated segments, disconnected paths, or unaccounted branching. A diagnostic lowest-ID tie-break on shared tiles **is not** a gameplay property rule.
5. The same signed geography is used by worker positioning, movement, infrastructure edges, economic settlement, participant-private settled freight and both visual map modes. Execute at least two economic cycles and compare the published state hashes, replay and authorized trade edges.
6. Numerical and performance limits must remain viable at the intended large-world scale. Do not chase marginal optimization at the expense of determinism or fairness.
7. Visual inspection in Godot before production enablement, particularly sea ice, poles, graduated terrain, spherical navigation and border alignment.

## R106–R109 research scope and stopping rule

R106 examines incidence counts and coarse edge/vertex signatures; R107 examines connected regions and real fine adjacency; R108 examines diagnostic center-to-center corridors; R109 examines actual fine polygon sides and vertices. These are **different evidence layers**, not independent reasons to add arbitrary resolutions.

R109's one-owner diagnostic projection does not replace multi-owner authoritative incidence; diagnostic polygon lines must not be imported into sovereignty or movement rules without a separately approved semantics.

**Stop** generating successive topology diagnostics unless a concrete defect or specific target resolution fails a gate above. Resume game functionality on the existing scale-six contract. Existing exploratory results are archived in their Draft PRs, with no automatic merge to `main`.

## Immediate gameplay focus

Make human-controlled physical worker reassignment inspectable and reversible before turn settlement. A selected worker's pending occupation can be canceled explicitly through the same authorized host command already available via `X`. No worker census or economic state changes before the real collective `E` turn.
