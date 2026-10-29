#!/usr/bin/env python3
"""GA-VS08 source-contract regression for original Godot tactical overlay lifts.

This is NOT an image-based or Godot 4.7.2 Windows QA substitute. It catches
accidental reintroduction of elevated 'hovering' highlights or grid edges.
"""
from __future__ import annotations

import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
base = (ROOT / "tools/GlobalArena.Godot/Scripts/TacticalContextPreview.cs").read_text(encoding="utf-8")
rings = (ROOT / "tools/GlobalArena.Godot/Scripts/TacticalContextPreview.WorkerRings.cs").read_text(encoding="utf-8")

names = [
    "PhysicalEdgeSurfaceLift",
    "InspectedTileSurfaceLift",
    "TerritoryBoundarySurfaceLift",
    "WorkerRibbonSurfaceLift",
]
values: list[float] = []
for name in names:
    match = re.search(
        rf"private const float {re.escape(name)} = (0\.\d+)f;",
        base,
    )
    if match is None:
        raise SystemExit(f"FAIL: missing physical-overlay lift contract: {name}")
    values.append(float(match.group(1)))

if not (0.0 < values[0] < values[1] < values[2] < values[3] < 0.001):
    raise SystemExit(f"FAIL: overlays no longer cling to terrain in sorted depth: {values}")

if "0.012f" in rings or "0.006f" in base:
    raise SystemExit("FAIL: obsolete elevated worker ribbon or physical grid lift returned")
if rings.count("new Vector3(a.X, WorkerRibbonSurfaceLift, a.Y)") != 2:
    raise SystemExit("FAIL: worker ribbon A vertices no longer use shared surface lift")
if rings.count("new Vector3(b.X, WorkerRibbonSurfaceLift, b.Y)") != 1:
    raise SystemExit("FAIL: worker ribbon B vertex no longer uses shared surface lift")
if rings.count("new Vector3(innerA.X, WorkerRibbonSurfaceLift, innerA.Y)") != 1:
    raise SystemExit("FAIL: worker ribbon inner A no longer uses shared surface lift")
if rings.count("new Vector3(innerB.X, WorkerRibbonSurfaceLift, innerB.Y)") != 2:
    raise SystemExit("FAIL: worker ribbon inner B no longer uses shared surface lift")
if "new Vector3(a.X, InspectedTileSurfaceLift, a.Y)" not in base:
    raise SystemExit("FAIL: inspection outline diverged from grounded layers")
if "new Vector3(b.X, InspectedTileSurfaceLift, b.Y)" not in base:
    raise SystemExit("FAIL: inspection outline diverged from grounded layers")
if base.count("PhysicalEdgeSurfaceLift") < 5:
    raise SystemExit("FAIL: base/resident physical grid edge lift not synchronized")
if "const float surfaceEpsilon = TerritoryBoundarySurfaceLift" not in base:
    raise SystemExit("FAIL: territory borders diverged from grounded layers")

old_parallax = 0.012 * math.sin(math.pi / 4)
new_parallax = values[3] * math.sin(math.pi / 4)
print("GA-VS08-OVERLAY-DEPTH-CONTRACT-PASS")
print("Terrain plane Y=0; physical edge, inspection, territory, workforce offsets:",
      ", ".join(f"{name}={v:.5f}" for name, v in zip(names, values)))
print(f"Approximate 45-degree ribbon parallax: old={old_parallax:.6f}, "
      f"new={new_parallax:.6f}, reduction={old_parallax/new_parallax:.0f}x")
print("Static depth-contract verification only; visual Godot/Windows smoke still required.")
