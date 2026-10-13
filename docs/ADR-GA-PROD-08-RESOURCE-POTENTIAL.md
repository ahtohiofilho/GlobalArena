# ADR - GA-PROD-08 - Canonical General Resource Potential Layer

Date: 2026-10-13
Status: Accepted subject to operator-attested integrated visual review

## Decision

Add a fifth Godot strategic map mode for general resource potential from WorldGenerationResult.StrategicResourcePotentialFields.GeneralPotential.GetRawValue(cell.CellId). Render a relative 0-100% index in panel and gradient legend while keeping GlobalArena.World authoritative for data generation, topology and semantics. The renderer is a read-only client and never infers confirmed deposits, mineral quantities, inventories or economic outcomes from this index. The existing four modes, picking, highlight, navigation and opaque continuous G(3,0) surface remain unaffected.

## Evidence and risks

GA-PROD-08 QA R1: 1,157/1,157 regressions, Release solution, Debug/Release Godot builds, headless runtime GA_PROD_RESOURCE_VIEW_READY_08 modes=5 cells=92. Human F5 inspection is operator-attested with -VisualAccepted and is not a substitute for automated accessibility or cross-device tests. No arbitrary GPP increase or global risk rating revision.
