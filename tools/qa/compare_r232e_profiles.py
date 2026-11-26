"""Compare the actual E-handler profile logs from the same CI runner."""
import json
import re
import statistics
import sys
from pathlib import Path

PHASES = ("turn_total", "host_close", "worker_calendar", "automatic_allocation",
          "market_distribution", "financial_accounting", "demography",
          "independent_replay", "canonical_hash", "trade_publication",
          "ui_attach", "ui_refresh")
EXPECTED_ASSEMBLIES = {"GlobalArena.Godot", "GlobalArena.Simulation",
                       "GlobalArena.Runtime", "GlobalArena.World"}
CYCLES = set(range(1, 101))

def parse(path, mode, reference):
    hashes, phases, stages, builds = {}, {}, {}, {}
    for line in Path(path).read_text().splitlines():
        if m := re.fullmatch(r"GA_NEXT_R232_TURN cycle=(\d+) .* replay=True hash=WorldStateHash \{ FormatVersion = 26, HexDigest = ([A-F0-9]{64}) \} inclusive=true", line):
            cycle = int(m[1])
            if cycle in hashes:
                raise ValueError(f"{mode}: duplicate turn {cycle}")
            hashes[cycle] = m[2]
        if m := re.fullmatch(r"GA_NEXT_R232_PHASE cycle=(\d+) phase=(\S+) calls=(\d+) elapsed_us=(\d+) allocated_bytes=(\d+)", line):
            key = (int(m[1]), m[2])
            if key in phases:
                raise ValueError(f"{mode}: duplicate phase {key}")
            phases[key] = tuple(map(int, m.groups()[2:]))
        if m := re.fullmatch(r"GA_NEXT_R232D_CYCLE cycle=(\d+) stage=(\S+) own_sites=(\d+) workers=(\d+) edges=(\d+) traveled=(True|False) replay=true", line):
            cycle = int(m[1])
            if cycle in stages:
                raise ValueError(f"{mode}: duplicate stage {cycle}")
            stages[cycle] = m.groups()[1:]
        if m := re.fullmatch(r"GA_NEXT_R232E_BUILD mode=(\S+) assembly=(\S+) jit_optimizer_disabled=(True|False)", line):
            if m[1] != mode or m[2] in builds:
                raise ValueError(f"{mode}: invalid loaded assembly record")
            builds[m[2]] = m[3]
    if hashes != reference or set(hashes) != CYCLES:
        raise ValueError(f"{mode}: canonical reference mismatch")
    if set(stages) != set(range(51, 101)):
        raise ValueError(f"{mode}: missing expansion or stationary stages")
    expected_flag = "True" if mode == "debug" else "False"
    if set(builds) != EXPECTED_ASSEMBLIES or any(v != expected_flag for v in builds.values()):
        raise ValueError(f"{mode}: managed build flags mismatch: {builds}")
    for cycle in CYCLES:
        for phase in PHASES:
            if (cycle, phase) not in phases or phases[cycle, phase][0] < 1:
                raise ValueError(f"{mode}: missing {cycle}/{phase}")
    return phases, stages, builds

def main():
    debug_path, optimized_path, reference_path, output_path = sys.argv[1:]
    reference = {int(c): h for c, h in
                 (line.split() for line in Path(reference_path).read_text().splitlines())}
    debug, debug_stages, debug_builds = parse(debug_path, "debug", reference)
    optimized, optimized_stages, optimized_builds = parse(optimized_path, "optimized", reference)
    if debug_stages != optimized_stages:
        raise ValueError("Managed builds produced different spatial/economic stages")
    windows = {
        "stationary_46_50": list(range(46, 51)),
        "travel_expansion": [c for c, s in debug_stages.items() if s[4] == "True"],
        "stationary_96_100": list(range(96, 101)),
    }
    report = {"cycles": 100, "reference_hashes_match": True,
              "loaded_assemblies": {"debug": debug_builds, "optimized": optimized_builds},
              "windows": {}}
    for label, cycles in windows.items():
        if not cycles:
            raise ValueError(f"Missing comparison window {label}")
        summary = {}
        for phase in PHASES:
            def stats(samples):
                return {"elapsed_us": statistics.median(samples[c, phase][1] for c in cycles),
                        "allocated_bytes": statistics.median(samples[c, phase][2] for c in cycles)}
            summary[phase] = {"debug": stats(debug), "optimized": stats(optimized)}
        report["windows"][label] = {"cycles": cycles, "phases": summary}
        a = summary["turn_total"]["debug"]["elapsed_us"]
        b = summary["turn_total"]["optimized"]["elapsed_us"]
        print(f"GA_NEXT_R232E_WINDOW name={label} debug_us={a} optimized_us={b}")
    Path(output_path).write_text(json.dumps(report, indent=2) + "\n")
    print("GA_NEXT_R232E_COMPARISON_PASS cycles=100 hashes_equal=true loaded_flags=true same_scenario=true")

if __name__ == "__main__":
    main()
