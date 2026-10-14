# GA-WORLDGEN-01B — Experimental terrain decision

Status: **experimental candidate accepted for continued development**. This is not authorization to replace the canonical `DeterministicWorldGenerator` or alter gameplay world state.

## Product contract

A single physical planetary surface will ultimately provide the canonical geography for both the global strategic board and the distinct tactical board entered from each strategic territory. Strategic presentation must derive from that surface; tactical boards must not independently regenerate unrelated geography.

## Approved experimental reference

- Independently seeded macro, medium, and micro impulse counts (experimental ranges: macro 0–10; medium 20–200; micro 400–4000).
- Radial spherical contributions with a smooth influence function; no coastline bias.
- Relative scale weights: macro **0.50**, medium **2.00**, micro **3.00**.
- Terrain previews are experimental; current G(48,0) preview has **23042** cells and is not a canonical tactical world.
- Compare land fraction 25%, 35%, and 45%; 35% is the benchmark reference, not a mandatory planet property.
- Existing baseline comparison modes are retained. Two-pass experiments are not selected.

## Evidence and limitations

Automated tests and previous visual review support adopting the balanced configuration as the development reference. This does not validate rivers, moisture, temperature, biomes, strategic-to-tactical incidence, physical area weighting, or canonical runtime integration. Experimental benchmark percentages count cells rather than area; equal-height sea-level ties require a canonical physical policy before production.

## Scope and governance

Consolidation is limited to the ten reviewed C# files and this decision document. Other local untracked render, GPU, geometric and viewer files are explicitly excluded. QA must not stage, commit or push; Formal Close requires a separate gated script after QA approval. Any future promotion to canonical WorldGen requires a new explicit QA and architecture decision.

Next planned work: latitude-dependent temperature, spatially continuous moisture and derived biome classification, initially experimental and tied to the same physical surface.