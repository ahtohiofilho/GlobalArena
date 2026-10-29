#!/usr/bin/env python3
"""GA-VS02 black-box C# bridge contract and order replay smoke."""
import json
import math
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
DLL = ROOT / "GlobalArena.Console" / "bin" / "Release" / "net10.0" / "GlobalArena.Console.dll"

def request(*orders, exit_code=0):
    p = subprocess.run(
        ["dotnet", str(DLL), "godot-bridge", "0", *orders],
        cwd=ROOT, text=True, capture_output=True, check=False)
    assert p.returncode == exit_code, (p.returncode, p.stderr, p.stdout)
    if exit_code:
        assert not p.stdout.strip(), "A failed command must not publish a partial snapshot"
        return None
    data = json.loads(p.stdout)
    assert data["schema"] == "GA-VS02-DEV-1"
    return data

initial = request()
assert initial["settlementCount"] == 1
assert initial["totalWorkers"] == 8
assert initial["worldRevision"] == 0
assert initial["previewTurns"] == 0
assert initial["cellCount"] == len(initial["cells"])
assert initial["cellCount"] == len(set(c["id"] for c in initial["cells"]))
assert all(len(c["boundary"]) in (5, 6) for c in initial["cells"])
assert all(len(v) == 3 for c in initial["cells"] for v in c["boundary"])
assert all(abs(sum(x*x for x in c["center"]) - 1) < 1e-9 for c in initial["cells"])
cell_by_id = {c["id"]: c for c in initial["cells"]}
assert all(n in cell_by_id for c in initial["cells"] for n in c["neighbors"])
assert all(c["id"] in cell_by_id[n]["neighbors"]
           for c in initial["cells"] for n in c["neighbors"])
first = next(c for c in initial["cells"] if c["pointId"] == 1)
assert first["owner"] == 1 and first["workers"] == 8
target = initial["frontiers"][0]["targetCellIds"][0]
assert cell_by_id[target]["foundable"]
assert cell_by_id[target]["land"]
command = f"f:1:{target}:3"
expanded = request(command)
assert expanded == request(command), "Replay must be deterministic"
assert expanded["settlementCount"] == 2
assert expanded["worldRevision"] == 1
assert expanded["totalWorkers"] == 8
assert next(c for c in expanded["cells"] if c["id"] == target)["workers"] == 3
assert next(c for c in expanded["cells"] if c["id"] == first["id"])["workers"] == 5
assert sum(x["rawQuantity"] for x in expanded["demand"]) == 4_000_000
preview = request(command, "p")
assert preview["previewTurns"] == 1
assert preview["worldRevision"] == 1
assert preview["supply"] == expanded["supply"]
assert preview["demand"] == expanded["demand"]

# Second attempt to occupy the same tile must refuse WITHOUT JSON output.
request(command, command, exit_code=2)
request("f:1:99999:3", exit_code=2)
request(f"f:1:{target}:99", exit_code=2)
request("not-a-command", exit_code=2)
print(f"GA-VS02-BRIDGE-PASS: {initial['cellCount']} canonical cells, "
      f"founding at tile {target}, workers 8, previews + fail-closed")
