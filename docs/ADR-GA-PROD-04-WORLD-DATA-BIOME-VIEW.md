# ADR - GA-PROD-04 - Canonical World Data and Biome View

Date: 2026-10-12
Status: Accepted following end-of-cycle human visual approval

## Decision

Godot is a read-only consumer of StrategicBiomes, StrategicHabitability and StrategicPhysicalFields.Elevation supplied by GlobalArena.World/WorldGenerationResult. All lookups use canonical StrategicCellId. The territory inspector presents raw normalized scalar values with their denominator, avoiding invented units or game semantics. The user may switch between stable cell-ID diagnostic colors and the nine canonical biome categories. The selected cell remains highlighted by temporary fill color only, without permanent outlines, insets, duplicate topology or geometry authority.

## Evidence and limitations

GA-PROD-04 QA R1 and biome view Recovery QA R3 passed build, 1,157 regression tests and Godot headless runtime. The end-of-cycle interactive visual acceptance is a distinct user-attested gate. No verified commercial-grade UI, biome shading transitions, hydrology graphics, animation-wide driver coverage, or high-resolution performance is claimed.

## Follow-up

Reassess efficient GPU color changes, scalable picking, accessibility and visual palette at larger topology resolution; measure GPP through the existing 1000-point maturity framework only when explicitly calibrated.
