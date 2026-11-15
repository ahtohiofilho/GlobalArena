#!/usr/bin/env python3
"""Global Arena ADR055 — 384 demand nodes (768 markets), fixed 4096 workers.

LAB ONLY. Original 48, 96 and 192 node F072 independently certified
regressions are mandatory. The 384-node equilibrium is now MANDATORY after independent
certification on Ubuntu and Windows. Original price sqrt(D/Q), quartic congestion, seller
tariffs, R13/R14 separation and original F072 certification stay frozen.
Native process-level CPU and peak RSS are observations, NOT game SLAs.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
from math import isfinite
from pathlib import Path
import platform
import sys
from time import perf_counter, process_time
import traceback
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "adr054_market_192" / "ci"))
import run_adr054 as R54
from clearing_r5 import _flow_checksum

SCHEMA = "GA_GATE09_ADR055_WORKER_384_MANDATORY_F072_WITH_NATIVE_METRICS_V2"
CANDIDATE = "candidate_384_demand_nodes_fixed_4096_workers"
REFERENCE = (R54.REFERENCE[0], R54.REFERENCE[1], R54.CANDIDATE)
SPATIAL_ONLY = R54.SPATIAL_ONLY
CONFIG_384 = R54.R53.PopulationConfig(24, 96, 4096)
STAGES = {
    "smoke": tuple((n, True) for n in REFERENCE),
    "ci": tuple((n, True) for n in REFERENCE) +
          ((CANDIDATE, True), (SPATIAL_ONLY, False)),
}
TOL = R54.R53.TOLERANCE
# Research work ceilings are declared BEFORE the first actual 384-node solve.
# Inherited ADR047 validation caps: Krylov <=60000, SPD blocks <=250000.
# These units do NOT define a frame-time or a scheduler for C#/Godot.
LIMITS_384 = R54.R53.R52.ADR51.R47.Limits(
    max_h_evaluations=1600,
    max_route_scans=6000,
    max_original_certificates=8,
    max_columns=3000,
    max_generations=6,
    max_newton_steps=240,
    max_backtrack=12,
    max_krylov_products=60000,
    max_cg_iterations=240,
    max_block_coefficients=250000,
)


def registered_budget(name):
    if name in R54.REFERENCE:
        return R54.R53.registered_budget(name)
    if name == R54.CANDIDATE:
        return R54.LIMITS_192
    if name == CANDIDATE:
        return LIMITS_384
    raise ValueError("No F072 budget for spatial-only 2048 demand nodes")


def peak_process_rss_bytes():
    """Observed OS PROCESS lifetime high-water RSS (not individual stage memory).

    Linux: getrusage ru_maxrss is KiB; Windows: PeakWorkingSetSize bytes.
    Return (None, reason) if unsupported; never synthesize an RSS estimate.
    """
    name = platform.system()
    if name in ("Linux", "Darwin"):
        import resource
        peak = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
        factor = 1024 if name == "Linux" else 1
        return int(peak * factor), "resource.RUSAGE_SELF.ru_maxrss"
    if name == "Windows":
        import ctypes
        from ctypes import wintypes
        class Counters(ctypes.Structure):
            _fields_ = [
                ("cb", wintypes.DWORD),
                ("PageFaultCount", wintypes.DWORD),
                ("PeakWorkingSetSize", ctypes.c_size_t),
                ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t),
                ("PeakPagefileUsage", ctypes.c_size_t),
            ]
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.GetCurrentProcess.argtypes = ()
        kernel.GetCurrentProcess.restype = ctypes.c_void_p
        psapi = ctypes.WinDLL("psapi", use_last_error=True)
        psapi.GetProcessMemoryInfo.argtypes = (
            ctypes.c_void_p, ctypes.POINTER(Counters), wintypes.DWORD)
        psapi.GetProcessMemoryInfo.restype = wintypes.BOOL
        item = Counters()
        item.cb = ctypes.sizeof(item)
        ok = psapi.GetProcessMemoryInfo(
            kernel.GetCurrentProcess(), ctypes.byref(item), item.cb)
        if not ok:
            raise OSError(ctypes.get_last_error(), "GetProcessMemoryInfo failed")
        return int(item.PeakWorkingSetSize), "psapi.GetProcessMemoryInfo.PeakWorkingSetSize"
    return None, "UNSUPPORTED_OS_NO_MEMORY_CLAIM"


def observation_start():
    wall = perf_counter()
    cpu = process_time()
    try:
        mem, source = peak_process_rss_bytes()
    except (OSError, AttributeError, ValueError) as exc:
        mem, source = None, "OS_MEMORY_UNAVAILABLE: " + type(exc).__name__
    return wall, cpu, mem, source


def observation_end(start):
    wall, cpu, rss0, source0 = start
    try:
        rss1, source1 = peak_process_rss_bytes()
    except (OSError, AttributeError, ValueError) as exc:
        rss1, source1 = None, "OS_MEMORY_UNAVAILABLE: " + type(exc).__name__
    return {
        "python_process_wall_seconds": round(perf_counter() - wall, 4),
        "python_process_cpu_seconds": round(process_time() - cpu, 4),
        "process_peak_rss_bytes_before_stage": rss0,
        "process_peak_rss_bytes_after_stage": rss1,
        "rss_measurement_source": source1,
        "rss_is_process_lifetime_highwater_not_stage_allocation": True,
        "cpu_is_process_time_not_game_thread_sla": True,
        "memory_available": rss1 is not None,
    }


def solve_candidate():
    t0 = perf_counter()
    net, flows, ledger = R54.R53.R52.build_population(CONFIG_384)
    build_ms = (perf_counter() - t0) * 1000.
    workers = ledger.pop("source_workers")
    source_hash = _flow_checksum(flows)
    if (len(workers) != 4096 or ledger["workers"] != 4096
        or ledger["geometry_nodes"] != 5762
        or ledger["production_nodes_count"] != 96
        or ledger["demand_nodes_count"] != 384
        or len(net.groups) != 192 or len(net.markets) != 768
        or len(flows) != 768
        or not ledger["physical_production_nodes"].issubset(
            ledger["physical_demand_nodes"])
        or not ledger["travel_time_does_not_duplicate_demand"]):
        raise AssertionError("Worker-coupled 384-node candidate ledger drift")
    result = R54.R53.R52.ADR51.solve_chunked(net, flows, LIMITS_384)
    certified = R54.R53.approve_original_F072(result)
    if result["status"].startswith("PASS_") and not certified:
        raise AssertionError("False PASS without original independent certificate")
    if not certified and (
        result.get("final_flow_sha256") is not None
        or result.get("final_allocations") is not None
    ):
        raise AssertionError("Uncertified 384 allocations disclosed as final")
    if result.get("original_congestion_exponent") != 4:
        raise AssertionError("Original quartic exponent is not preserved")
    if source_hash != _flow_checksum(flows):
        raise AssertionError("Solving changed worker-aggregated source flows")
    cert = result.get("certificate")
    return {
        "name": CANDIDATE,
        "physical_goldberg_nodes": 5762,
        "physical_workers": 4096,
        "production_nodes": 96,
        "demand_nodes": 384,
        "aggregated_production_groups": 192,
        "worker_sourced_markets_node_item": 768,
        "real_funded_initial_columns": len(flows),
        "producing_workers": ledger["producing_workers"],
        "nonproducing_workers": ledger["nonproducing_workers"],
        "worker_ledger_sha256": ledger["worker_ledger_sha256"],
        "initial_physical_flow_sha256": source_hash,
        "original_demands_by_item": ledger["market_demand_by_item"],
        "worker_demand_conserved_without_extra_travel_demand": True,
        "production_nodes_subset_demand_nodes": True,
        "population_build_python_ms": round(build_ms, 3),
        "F072_attempted": True,
        "F072_independent_certified": certified,
        "original_F072_global_FINAL_if_pass":
            cert["global_F072_gap"] if certified else None,
        "original_F072_worst_group_FINAL_if_pass":
            cert["worst_group_F072_gap"] if certified else None,
        "last_audited_global_NOT_final_on_NO_GO":
            cert["global_F072_gap"] if cert else None,
        "last_audited_worst_NOT_final_on_NO_GO":
            cert["worst_group_F072_gap"] if cert else None,
        "F072_certificate_original_only": cert["status"] if cert else None,
        "solver_status": result["status"],
        "solver_elapsed_python_ms": result["elapsed_ms_observed_python"],
        "solver_work": result["work"],
        "solver_generation_summary": result["generations"],
        "provisional_not_authoritative": result["provisional_last_snapshot"],
        "original_congestion_exponent_unchanged": True,
        "budget_limits": vars(LIMITS_384),
        "no_game_snapshot_committed": True,
    }


def certify_reference_row(row):
    """Existing solvers provide original independent certificate; recheck gaps."""
    return bool(
        row.get("F072_attempted") is True
        and row.get("F072_independent_certified") is True
        and row.get("original_F072_global_FINAL_if_pass") is not None
        and row.get("original_F072_worst_group_FINAL_if_pass") is not None
        and isfinite(row["original_F072_global_FINAL_if_pass"])
        and isfinite(row["original_F072_worst_group_FINAL_if_pass"])
        and row["original_F072_global_FINAL_if_pass"] <= TOL
        and row["original_F072_worst_group_FINAL_if_pass"] <= TOL
    )


def classify(profile, entries):
    if profile not in STAGES:
        raise ValueError("Unknown ADR055 profile")
    if tuple(r["name"] for r in entries) != tuple(n for n, _ in STAGES[profile]):
        raise ValueError("Stage list missing, reordered or contaminated")
    for i, name in enumerate(REFERENCE):
        if type(entries[i].get("F072_independent_certified")) is not bool:
            raise ValueError("Mandatory original F072 regression status missing")
        if not certify_reference_row(entries[i]):
            return f"NO_GO_MANDATORY_{(48, 96, 192)[i]}_F072_REGRESSION", False
    if profile == "smoke":
        return "PASS_ADR055_MANDATORY_48_96_192_F072", True
    row, spatial = entries[3], entries[4]
    if (row["physical_goldberg_nodes"] != 5762
        or row["physical_workers"] != 4096
        or row["production_nodes"] != 96
        or row["demand_nodes"] != 384
        or row["aggregated_production_groups"] != 192
        or row["worker_sourced_markets_node_item"] != 768
        or not row["F072_attempted"]
        or not row["production_nodes_subset_demand_nodes"]
        or not row["worker_demand_conserved_without_extra_travel_demand"]
        or row["original_congestion_exponent_unchanged"] is not True
        or spatial["demand_nodes"] != 2048
        or spatial["physical_workers"] != 8192
        or spatial["worker_sourced_markets_node_item"] != 4096
        or spatial["F072_attempted"]
        or spatial["F072_independent_certified"]
        or not spatial["production_nodes_subset_demand_nodes"]
        or not spatial["worker_demand_conserved_without_extra_travel_demand"]):
        return "NO_GO_MANDATORY_WORKER_SPATIAL_OR_QUARTIC_CONTRACT", False
    if type(row.get("F072_independent_certified")) is not bool:
        raise ValueError("Candidate F072 certification flag must be explicit bool")
    if row["F072_independent_certified"]:
        a = row["original_F072_global_FINAL_if_pass"]
        b = row["original_F072_worst_group_FINAL_if_pass"]
        if (a is None or b is None or not isfinite(a) or not isfinite(b)
            or a > TOL or b > TOL
            or row["F072_certificate_original_only"] !=
                "PASS_STATIC_F072_FIRST_ORDER_ONLY"):
            return "NO_GO_UNVERIFIED_384_F072_CLAIM", False
        return "PASS_ADR055_384_ORIGINAL_F072_CERTIFIED_MANDATORY", True
    if (row["original_F072_global_FINAL_if_pass"] is not None
        or row["original_F072_worst_group_FINAL_if_pass"] is not None):
        return "NO_GO_UNCERTIFIED_384_FINAL_DISCLOSURE", False
    # Following dual-OS independent original F072 PASS, 384 is mandatory.
    # An uncertified 384 result must fail CI; never publish final flows/gaps.
    return "NO_GO_MANDATORY_ADR055_384_F072_REGRESSION", False


def exercise(profile):
    if R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Original R20 source fingerprint changed")
    rows = []
    for name, _ in STAGES[profile]:
        start = observation_start()
        if name in R54.REFERENCE:
            row = R54.R53.run_stage(name, True)
        elif name == R54.CANDIDATE:
            row = R54.solve_candidate_192()
        elif name == CANDIDATE:
            row = solve_candidate()
        else:
            row = R54.R53.run_stage(SPATIAL_ONLY, False)
        row["observed_python_process_metrics"] = observation_end(start)
        rows.append(row)
        w = row.get("solver_work") or {}
        met = row["observed_python_process_metrics"]
        print("[ADR055:{}] workers={} supply_nodes={} demand_nodes={} markets={} "
              "F072={} certified={} status={} gap_global={} gap_worst={} "
              "H={} Krylov={} scans={} cpu_s={} wall_s={} peak_rss_B={} "
              "solver_python_ms={}".format(
                  name, row["physical_workers"], row["production_nodes"],
                  row["demand_nodes"], row["worker_sourced_markets_node_item"],
                  row["F072_attempted"], row["F072_independent_certified"],
                  row["solver_status"],
                  row["original_F072_global_FINAL_if_pass"],
                  row["original_F072_worst_group_FINAL_if_pass"],
                  w.get("H_evaluations_including_rejected_trials"),
                  w.get("matrixfree_Jv_and_Jtv_products"),
                  w.get("separate_shortest_path_scans"),
                  met["python_process_cpu_seconds"],
                  met["python_process_wall_seconds"],
                  met["process_peak_rss_bytes_after_stage"],
                  row["solver_elapsed_python_ms"]), flush=True)
    status, accepted = classify(profile, rows)
    return {
        "schema": SCHEMA,
        "status": status,
        "profile": profile,
        "mandatory_regressions_and_contracts_passed": accepted,
        "candidate_384_mandatory_F072_certified":
            rows[3]["F072_independent_certified"] if profile == "ci" else None,
        "stages": rows,
        "fixed_population_for_192_vs_384": True,
        "two_item_worker_sourced_markets": True,
        "original_quartic_and_price_functions_untouched": True,
        "higher_2048_demand_node_scenario_not_solved": True,
        "F059_not_ratified": True,
        "CSharp_Godot_unmodified": True,
        "production_approved": False,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile", choices=tuple(STAGES), default="ci")
    p.add_argument("--output", type=Path, default=Path.cwd())
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": SCHEMA,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "status": "FAILED_ADR055_CONTRACT",
        "production_approved": False,
    }
    try:
        report.update(exercise(args.profile))
    except Exception as err:
        report["status"] = "FAILED_ADR055_CONTRACT"
        report["error"] = repr(err)
        report["traceback"] = traceback.format_exc()
        print("[ADR055] FAILED:", repr(err), flush=True)
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    json_path = args.output / ("GA-Gate09-ADR055-" + args.profile + "-" + timestamp + ".json")
    json_path.write_text(json.dumps(report, indent=2, sort_keys=True,
                                    ensure_ascii=False) + "\n", encoding="utf-8")
    zip_path = args.output / ("GA-Gate09-ADR055-Evidencias-" + timestamp + ".zip")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(json_path, json_path.name)
    print("[ADR055] STATUS:", report["status"], flush=True)
    print("[ADR055] EVIDENCE:", zip_path, flush=True)
    return 0 if report.get("mandatory_regressions_and_contracts_passed") else 1


if __name__ == "__main__":
    raise SystemExit(main())
