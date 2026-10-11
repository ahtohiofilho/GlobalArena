# ADR GA-MESH-001 â€” Spherical oblique 3D tactical prototype

Date: 2026-10-11
Status: accepted as technical prototype, not final renderer.

## Decision
Version the oblique camera contract and its tests, plus tools/GlobalArena.TacticalViewport3D with the reviewed depth-tested cell borders. The fine spherical mesh retains authoritative topology and canonical identities; no flat 2D polygon is promoted to world geometry. Default obliquity targets 45 degrees above the local tangent plane and remains configurable. The existing 2D interactive prototype and experimental NativeGlobe viewer remain separate.

## Evidence
QA: 1157/1157 suite tests, Release build clean, edge rendering implementation statically checked. Screenshot focus 29: tile 1794 selected, 271 physical tiles drawn, internal hex borders visually present. Visual approval is limited to the displayed example, not exhaustive runtime or frame-rate testing.

## Limitations and direction
The rasterizer is software-based and experimental. Full camera near-plane triangle clipping, performance measurements, reliable orbit/pan controls, GPU depth rendering, continuous terrain outside selected neighborhoods, LOD, relief, and texture data are not complete. Future GPU render architecture must consume spherical canonical vertices and stable physical IDs via a renderer-neutral geometry/selection boundary. Avoid expanding the WinForms prototype into the game's final renderer.