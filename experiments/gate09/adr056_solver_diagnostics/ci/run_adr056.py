#!/usr/bin/env python3
"""ADR056: isolated Python CPU hotspot and population-memory diagnostics.

Original F072 solver, unmodified. 192 and 384 demand-node fixtures each
run in a fresh Python process through the CI matrix. No alternative
economics, preconditioner or numerical budget is installed here.

cProfile records INSTRUMENTED call-time distribution, not uninstrumented
wall time or C#/Godot CPU. tracemalloc runs ONLY for a separate population
construction probe; it is stopped before the certified solver.
Peak native RSS is a process-lifetime high-water mark, not attributable
exclusively to a particular function or object.
"""
from __future__ import annotations

import argparse
import cProfile
from datetime import datetime, timezone
import gc
import json
from pathlib import Path
import platform
import pstats
import sys
from time import perf_counter, process_time
import tracemalloc
import traceback
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
ROOT = GATE09.parents[1]
sys.path.insert(0, str(GATE09 / "adr055_market_384" / "ci"))
import run_adr055 as R55

SCHEMA = "GA_GATE09_ADR056_F072_CPROFILE_AND_CONSTRUCTION_HEAP_V1"
FIXTURES = {
    "192": {
        "config": R55.R54.R53.CONFIGS[R55.R54.CANDIDATE],
        "expected_workers": 4096,
        "demand_nodes": 192,
        "production_nodes": 48,
        "groups": 96,
        "markets": 384,
        "budget": R55.R54.LIMITS_192,
    },
    "384": {
        "config": R55.CONFIG_384,
        "expected_workers": 4096,
        "demand_nodes": 384,
        "production_nodes": 96,
        "groups": 192,
        "markets": 768,
        "budget": R55.LIMITS_384,
    },
}


def rss_observation():
    """Use inherited native process-lifetime RSS; no invented fallback."""
    try:
        rss, method = R55.peak_process_rss_bytes()
        return {"process_peak_rss_bytes": rss, "rss_source": method}
    except (OSError, ValueError, AttributeError) as exc:
        return {
            "process_peak_rss_bytes": None,
            "rss_source": "UNAVAILABLE_" + type(exc).__name__,
        }


def build_probe(key):
    """A separate reconstruction, preserving the production/certificate inputs.

    tracemalloc traces only Python-tracked allocations while the fresh
    fixture is being constructed, not the numerical solver. Snapshot top
    files are not exact retained graph sizes, native heap or allocator RSS.
    """
    config = FIXTURES[key]["config"]
    gc.collect()
    before_rss = rss_observation()
    start_wall, start_cpu = perf_counter(), process_time()
    tracemalloc.start(1)
    try:
        net, flows, meta = R55.R54.R53.R52.build_population(config)
        current, peak = tracemalloc.get_traced_memory()
        snapshot = tracemalloc.take_snapshot()
        stats = snapshot.statistics("filename")
        top_files = [
            {"filename": Path(item.traceback[0].filename).name,
             "python_traced_bytes_at_snapshot": item.size,
             "traced_allocation_blocks": item.count}
            for item in stats[:16]
        ]
        checks = {
            "workers": meta["workers"],
            "production_nodes": meta["production_nodes_count"],
            "demand_nodes": meta["demand_nodes_count"],
            "groups": len(net.groups),
            "markets": len(net.markets),
            "worker_ledger_sha256": meta["worker_ledger_sha256"],
            "initial_flow_sha256": meta["initial_real_flow_sha256"],
            "travel_demand_conserved": meta["travel_time_does_not_duplicate_demand"],
            "producers_subset_demand": meta["physical_production_nodes"].issubset(
                meta["physical_demand_nodes"]),
        }
        expected = FIXTURES[key]
        if (checks["workers"] != expected["expected_workers"]
            or checks["production_nodes"] != expected["production_nodes"]
            or checks["demand_nodes"] != expected["demand_nodes"]
            or checks["groups"] != expected["groups"]
            or checks["markets"] != expected["markets"]
            or not checks["travel_demand_conserved"]
            or not checks["producers_subset_demand"]):
            raise AssertionError("Physical worker demand/offer fixture drift")
    finally:
        tracemalloc.stop()
    seconds = perf_counter() - start_wall
    cpu_seconds = process_time() - start_cpu
    after_rss = rss_observation()
    del net, flows, meta, snapshot, stats
    gc.collect()
    return {
        "fixture": key,
        "original_worker_ledger_sha256": checks["worker_ledger_sha256"],
        "original_initial_flow_sha256": checks["initial_flow_sha256"],
        "physical_contract": checks,
        "build_probe_wall_seconds_with_tracemalloc": round(seconds, 6),
        "build_probe_process_cpu_seconds_with_tracemalloc": round(cpu_seconds, 6),
        "python_traced_current_bytes_at_snapshot": current,
        "python_traced_peak_bytes_during_construction": peak,
        "python_traced_top_allocation_files": top_files,
        "rss_before_probe": before_rss,
        "rss_after_probe": after_rss,
        "tracemalloc_stopped_before_solver": True,
        "traced_python_heap_is_NOT_total_RSS_or_solver_allocation": True,
    }


def hotspots(profile):
    """Return original cProfile function statistics with clear timing labels."""
    stat = pstats.Stats(profile)
    entries = [
        {
            "file": Path(filename).name,
            "line": lineno,
            "function": function,
            "primitive_calls": cc,
            "total_calls": nc,
            "profile_self_elapsed_seconds": round(tt, 6),
            "profile_cumulative_elapsed_seconds": round(ct, 6),
            "solver_source": (
                "experiments/gate09/" in filename.replace("\\", "/")
                or Path(filename).name in (
                    "clearing_r5.py", "gate09.py", "multimarket_r13.py")),
        }
        for (filename, lineno, function), (cc, nc, tt, ct, _) in stat.stats.items()
    ]
    by_cumulative = sorted(entries, key=lambda x: (
        -x["profile_cumulative_elapsed_seconds"],
        -x["total_calls"], x["function"]))
    by_self = sorted(entries, key=lambda x: (
        -x["profile_self_elapsed_seconds"],
        -x["total_calls"], x["function"]))
    # Provide solver-source subsets too, not only outer wrappers/stdlib.
    return {
        "top_20_cumulative_functions_all": by_cumulative[:20],
        "top_20_self_functions_all": by_self[:20],
        "top_25_cumulative_solver_source": [
            x for x in by_cumulative if x["solver_source"]][:25],
        "top_25_self_solver_source": [
            x for x in by_self if x["solver_source"]][:25],
        "cprofile_is_instrumented_wall_timing_not_CSharp_CPU": True,
        "cumulative_time_nested_nonadditive": True,
    }


def solve_profiled(key, probe):
    """Call ONLY the certified original wrappers, under existing budgets."""
    if key not in FIXTURES:
        raise ValueError("Registered fixture must be 192 or 384")
    profile = cProfile.Profile()
    before_rss = rss_observation()
    wall0, cpu0 = perf_counter(), process_time()
    try:
        profile.enable()
        if key == "192":
            row = R55.R54.solve_candidate_192()
        else:
            row = R55.solve_candidate()
    finally:
        profile.disable()
    wall_s, cpu_s = perf_counter() - wall0, process_time() - cpu0
    after_rss = rss_observation()
    expected = FIXTURES[key]
    if (row["physical_workers"] != expected["expected_workers"]
        or row["production_nodes"] != expected["production_nodes"]
        or row["demand_nodes"] != expected["demand_nodes"]
        or row["aggregated_production_groups"] != expected["groups"]
        or row["worker_sourced_markets_node_item"] != expected["markets"]
        or row["worker_ledger_sha256"] != probe["original_worker_ledger_sha256"]
        or row["initial_physical_flow_sha256"] != probe["original_initial_flow_sha256"]
        or not row["F072_attempted"]
        or not row["production_nodes_subset_demand_nodes"]
        or not row["worker_demand_conserved_without_extra_travel_demand"]
        or row["original_congestion_exponent_unchanged"] is not True):
        raise AssertionError("Profiled candidate physical or quartic drift")

    # The independent original ADR042 certificate, not a seed/smoothed
    # residual, is the SOLE basis for this already-mandatory F072 result.
    if not R55.certify_reference_row(row):
        if (row["original_F072_global_FINAL_if_pass"] is not None
            or row["original_F072_worst_group_FINAL_if_pass"] is not None):
            raise AssertionError("Uncertified original F072 final gaps leaked")
        raise AssertionError("Mandatory original F072 not independently certified")
    if row["F072_certificate_original_only"] != "PASS_STATIC_F072_FIRST_ORDER_ONLY":
        raise AssertionError("Mandatory original F072 certificate not original")

    trace = hotspots(profile)
    return {
        "fixture": key,
        "original_F072_independent_certified": True,
        "original_F072_global_gap": row["original_F072_global_FINAL_if_pass"],
        "original_F072_worst_group_gap":
            row["original_F072_worst_group_FINAL_if_pass"],
        "original_quartic_exponent_unchanged": True,
        "actual_solver_status": row["solver_status"],
        "existing_numerical_work_counters": row["solver_work"],
        "predeclared_original_solver_budget": vars(expected["budget"]),
        "profiled_run_wall_seconds": round(wall_s, 6),
        "profiled_run_process_cpu_seconds": round(cpu_s, 6),
        "baseline_uninstrumented_solver_wall_ms_NOT_measured_here": None,
        "process_rss_before_profiled_run": before_rss,
        "process_rss_after_profiled_run": after_rss,
        "rss_is_process_lifetime_highwater_not_exact_solver_heap": True,
        "profile_calls_and_hotspots": trace,
        "tracemalloc_disabled_during_solver": not tracemalloc.is_tracing(),
        "production_approved": False,
    }


def run(key):
    if key not in FIXTURES:
        raise ValueError("Only 192 or 384 original worker scenarios")
    if R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Frozen original R20 manifest changed")
    rss_initial = rss_observation()
    probe = build_probe(key)
    result = solve_profiled(key, probe)
    result["construction_probe"] = probe
    result["rss_before_population_probe"] = rss_initial
    return {
        "schema": SCHEMA,
        "fixture": key,
        "status": "PASS_ADR056_DIAGNOSTIC_ORIGINAL_F072_CERTIFIED",
        "output": result,
        "no_alternative_preconditioner_or_numerical_rules": True,
        "no_claim_of_uninstrumented_runtime_efficiency": True,
        "no_2048_node_F072_claim": True,
        "F059_financial_policy_pending": True,
        "CSharp_Godot_untouched": True,
        "production_approved": False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture", required=True, choices=tuple(FIXTURES))
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": SCHEMA,
        "fixture": args.fixture,
        "utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "status": "FAILED_ADR056_DIAGNOSTIC_CONTRACT",
        "production_approved": False,
    }
    try:
        report.update(run(args.fixture))
    except Exception as exc:
        report["status"] = "FAILED_ADR056_DIAGNOSTIC_CONTRACT"
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[ADR056] FAILED:", repr(exc), flush=True)
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    target = args.output / (
        "GA-Gate09-ADR056-" + args.fixture + "-" + timestamp + ".json")
    target.write_text(json.dumps(report, indent=2, sort_keys=True,
                                 ensure_ascii=False) + "\n", encoding="utf-8")
    archive = args.output / (
        "GA-Gate09-ADR056-Evidencias-" + args.fixture + "-" + timestamp + ".zip")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as output:
        output.write(target, target.name)
    if report["status"].startswith("PASS_"):
        r = report["output"]
        w = r["existing_numerical_work_counters"]
        print("[ADR056:{}] PASS original_F072 global={} worst={} "
              "H={} krylov={} scans={} profiled_cpu_s={} profiled_wall_s={} "
              "RSS_peak={} construction_traced_peak={} top_self={}".format(
                  args.fixture, r["original_F072_global_gap"],
                  r["original_F072_worst_group_gap"],
                  w.get("H_evaluations_including_rejected_trials"),
                  w.get("matrixfree_Jv_and_Jtv_products"),
                  w.get("separate_shortest_path_scans"),
                  r["profiled_run_process_cpu_seconds"],
                  r["profiled_run_wall_seconds"],
                  r["process_rss_after_profiled_run"]["process_peak_rss_bytes"],
                  r["construction_probe"]["python_traced_peak_bytes_during_construction"],
                  [
                      (x["file"], x["function"], x["profile_self_elapsed_seconds"])
                      for x in r["profile_calls_and_hotspots"]["top_25_self_solver_source"][:8]
                  ]), flush=True)
    print("[ADR056] STATUS:", report["status"], flush=True)
    print("[ADR056] EVIDENCE:", archive, flush=True)
    return 0 if report["status"].startswith("PASS_") else 1


if __name__ == "__main__":
    raise SystemExit(main())
