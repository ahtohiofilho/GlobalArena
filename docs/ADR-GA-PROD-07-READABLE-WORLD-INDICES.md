# ADR - GA-PROD-07 - Readable World Indices

Date: 2026-10-12
Status: Accepted subject to operator-attested integrated visual review

## Decision

Expose habitability and relative elevation as one-decimal percentage labels derived from immutable StrategicScalarField raw values divided by StrategicScalarField.Denominator. Keep raw values unchanged, with GlobalArena.World authoritative. Never conflate relative elevation with meters, or habitability with a population metric. Preserve four world map views, legends, strategic picking, highlight, drag and zoom.

## Verification and limitations

QA R1 confirmed 1,157/1,157 regressions, Release solution and Debug/Release Godot builds, and headless runtime. Visual acceptance is attested by the -VisualAccepted operator flag and does not establish accessibility or high-resolution production performance. The 1,000-GPP official baseline remains unchanged until capability maturity recalibration.
