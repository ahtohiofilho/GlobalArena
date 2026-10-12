# ADR - GA-PROD-05 - Four Canonical World Layers

Date: 2026-10-12
Status: Accepted on explicit human end-of-cycle visual approval

## Decision

Expose four read-only visualization modes on the same continuous opaque Goldberg globe: per-cell diagnostic color, biome, habitability and elevation. The latter three consume canonical world generation data by StrategicCellId. Preserve the existing click/drag distinction, selection highlight, no-border shared-vertex geometry and navigation controls. Colors are presentation-only and do not modify world truth.

## Evidence and limits

QA R1 reported 1157 automated regressions, Release solution, Godot Debug/Release and headless runtime marker GA_PROD_WORLD_LAYERS_READY_05 modes=4. Headless start does not prove GUI interaction: manual approval is independently required. Scalar visualizations are preliminary diagnostics and not calibrated terrain, accessibility or performance claims.

## Follow up

Plan legend/units semantics, UI layout and higher-resolution performance acceptance as future gates. Recalibrate the official 1000-GPP project progress through the approved capability methodology only.
