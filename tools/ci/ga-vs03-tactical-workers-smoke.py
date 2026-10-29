#!/usr/bin/env python3
"""GA-VS03 assert physical tactical worker simulation, no strategic city state."""
import json
import pathlib
import subprocess
ROOT = pathlib.Path(__file__).resolve().parents[2]
DLL = ROOT / "GlobalArena.Console/bin/Release/net10.0/GlobalArena.Console.dll"

def request(focus, *orders, expected=0):
    p = subprocess.run(["dotnet", str(DLL), "tactical-bridge", "0", str(focus), *orders],
                       text=True, capture_output=True, cwd=ROOT, check=False)
    assert p.returncode == expected, (p.returncode,p.stdout[:400],p.stderr[:700])
    if expected:
        assert not p.stdout.strip(), "Fail-closed bridge must not publish partial state"
        return None
    data=json.loads(p.stdout)
    assert data["schema"]=="GA-VS03-PHYSICAL-1"
    return data

initial=request(1)
assert len(initial["coarse"])==42
assert initial["globalFineCount"]==1442
assert initial["totalWorkers"]==8
assert initial["occupiedPhysicalTiles"]==1
assert sum(x["workers"] for x in initial["coarse"])==8
initial_tile=initial["initialTile"]
worker_sector=next(x["id"] for x in initial["coarse"] if x["workers"])
initial=request(worker_sector)
source=next(x for x in initial["physical"] if x["id"]==initial_tile)
assert source["workers"]==8
assert len(initial["assignments"])==3
assert all(row["tile"]==initial_tile for row in initial["assignments"])
assert sum(x["raw"] for x in initial["demand"])==4_000_000
assert len({x["id"] for x in initial["physical"]})==len(initial["physical"])
assert any(len(x["incidents"]) > 1 for x in initial["physical"]), "Missing shared physical border tiles"
target=next(x["id"] for x in initial["physical"]
            if x["id"] in source["adjacent"] and x["workable"])
move=f"m:{initial_tile}:{target}:1:4:3"
after=request(worker_sector,move)
assert after==request(worker_sector,move), "State must replay deterministically"
assert after["revision"]==1
assert after["totalWorkers"]==8
assert after["occupiedPhysicalTiles"]==2
assert sum(x["workers"] for x in after["coarse"])==8
assert sum(x["workers"] for x in after["assignments"])==8
assert sum(x["raw"] for x in after["demand"])==4_000_000
assert sum(x["workers"] for x in after["assignments"] if x["tile"]==target)==3
assert sum(x["workers"] for x in after["assignments"] if x["tile"]==initial_tile)==5
preview=request(worker_sector,move,"p")
assert preview["revision"]==1
assert preview["previews"]==1
assert preview["assignments"]==after["assignments"]
assert preview["supply"]==after["supply"]

# Coarse view is only a projection. Switching focus changes visible fine
# polygons, never workforce, jobs or the worker count in strategic buckets.
other_focus=next(x["id"] for x in initial["coarse"] if x["id"]!=worker_sector)
projected=request(other_focus,move)
assert projected["coarse"]==after["coarse"]
assert projected["assignments"]==after["assignments"]
assert projected["totalWorkers"]==after["totalWorkers"]

request(worker_sector,move,move,expected=2) # source now only one farmer
request(worker_sector,f"m:{initial_tile}:{target}:1:4:5",expected=2)
request(worker_sector,f"m:{initial_tile}:{initial_tile}:1:4:1",expected=2)
request(worker_sector,"m:1:999999:1:4:1",expected=2)
request(worker_sector,"not-an-order",expected=2)
print(f"GA-VS03-TACTICAL-PASS: 1442 fine IDs, 42 summaries, "
      f"worker origin={initial_tile}, destination={target}, 8=5+3, all invalid orders refused")
