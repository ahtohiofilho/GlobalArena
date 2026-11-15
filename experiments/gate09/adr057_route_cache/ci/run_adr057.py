#!/usr/bin/env python3
"""ADR-057: non-mutating cold/repeated route-cache diagnostics, lab only.

Fresh original 192/384 worker-ledger networks. No new economic solver and
no altered routing or preconditioner. Independent mandatory F072
certificates are checked by a separate unchanged ADR055 workflow job.

FrozenRouteCache.paths is called first and repeated on the SAME fixed
loads. cProfile counts are instrumented; a reduction in Dijkstra calls
is only a proxy for reuse, not a certified cache hit-rate or a proof of
commercial solver acceleration. Python-traced cache allocations do NOT
represent the full solver's peak memory or C#/Godot memory.
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
from sys import getsizeof
from time import perf_counter, process_time
import traceback
import tracemalloc
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "adr056_solver_diagnostics" / "ci"))
import run_adr056 as R56
from clearing_r5 import _flow_checksum
from oracle_cache_r14 import FrozenRouteCache

SCHEMA = "GA_GATE09_ADR057_ORIGINAL_FROZEN_ROUTE_CACHE_DIAGNOSTICS_V1"
FIXTURES = R56.FIXTURES
WATCH = {
    ("oracle_cache_r14.py", "paths"),
    ("oracle_cache_r14.py", "weights"),
    ("gate09.py", "dijkstra"),
    ("gate09.py", "weight_for"),
    ("gate09.py", "congestion_cost"),
    ("gate09.py", "validate_column"),
}
EXPERIMENT_ONLY = True


def profile_counts(profile):
    """Exact calls observed by cProfile for a *fixed* phase of this probe."""
    totals = {}
    for (filename, line, name), (primitive, total, self_s, cumulative_s, callers) in pstats.Stats(profile).stats.items():
        key = (Path(filename).name, name)
        if key in WATCH:
            row = totals.setdefault("/".join(key), {
                "primitive_calls": 0, "total_calls": 0,
                "profile_self_s": 0.0, "profile_cumulative_s": 0.0,
            })
            row["primitive_calls"] += int(primitive)
            row["total_calls"] += int(total)
            row["profile_self_s"] += self_s
            row["profile_cumulative_s"] += cumulative_s
    for row in totals.values():
        for field in ("profile_self_s", "profile_cumulative_s"):
            row[field] = round(row[field], 6)
    return totals


def cache_shallow_fields(cache):
    """Introspect only top-level cache fields; no recursive size assertions."""
    try:
        fields = vars(cache)
    except TypeError:
        return {"fields_introspectable": False, "fields": []}
    out = []
    for name, value in sorted(fields.items()):
        row = {
            "field": name,
            "type": type(value).__name__,
            "shallow_python_bytes": getsizeof(value),
            "shallow_is_NOT_reachable_object_graph_size": True,
        }
        if isinstance(value, (dict, list, tuple, set, frozenset)):
            row["entry_count"] = len(value)
        out.append(row)
    return {"fields_introspectable": True, "fields": out}


def phase(cache, queries):
    """Never modify the frozen physical ledger or skip any source/seller key."""
    profiler = cProfile.Profile()
    start_wall, start_cpu = perf_counter(), process_time()
    sizes = []
    profiler.enable()
    try:
        for origin, seller in queries:
            dist, paths, ways = cache.paths(origin, seller)
            if origin not in dist or origin not in paths:
                raise AssertionError("Frozen route oracle lost its origin")
            sizes.append((len(dist), len(paths), len(ways),
                          dist[origin], paths[origin]))
    finally:
        profiler.disable()
    wall = perf_counter() - start_wall
    cpu = process_time() - start_cpu
    return {
        "instrumented_wall_s": round(wall, 6),
        "instrumented_process_cpu_s": round(cpu, 6),
        "calls_requested": len(queries),
        "distinct_origin_seller_keys": len(set(queries)),
        "route_result_signatures": sizes,
        "watched_function_calls": profile_counts(profiler),
        "cprofile_alters_measured_wall_time": True,
    }


def rss():
    return R56.rss_observation()


def diagnostic(fixture):
    if fixture not in FIXTURES:
        raise ValueError("Only registered ADR056 fixed-worker scenarios")
    if R56.R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Original source manifest changed")
    conf = FIXTURES[fixture]
    net, flows, meta = R56.R55.R54.R53.R52.build_population(conf["config"])
    src_workers = meta.pop("source_workers")
    if (len(src_workers) != 4096 or meta["workers"] != 4096
        or meta["demand_nodes_count"] != conf["demand_nodes"]
        or meta["production_nodes_count"] != conf["production_nodes"]
        or len(net.groups) != conf["groups"]
        or len(net.markets) != conf["markets"]
        or not meta["physical_production_nodes"].issubset(
            meta["physical_demand_nodes"])
        or not meta["travel_time_does_not_duplicate_demand"]):
        raise AssertionError("Original worker-coupled physical contract drift")

    hash_before = _flow_checksum(flows)
    loads, quantities, prices = net.snapshots(flows)
    queries = tuple(
        (net.groups[gid].origin, net.groups[gid].seller)
        for gid in sorted(net.groups))
    if len(queries) != conf["groups"]:
        raise AssertionError("Every producer group must be audited")
    before_rss = rss()
    gc.collect()
    tracemalloc.start(1)
    try:
        cache = FrozenRouteCache(net, loads, 4)
        alloc_after_creation, _ = tracemalloc.get_traced_memory()
        first = phase(cache, queries)
        alloc_after_first, peak_after_first = tracemalloc.get_traced_memory()
        fields_after_first = cache_shallow_fields(cache)
        second = phase(cache, queries)
        alloc_after_second, total_peak = tracemalloc.get_traced_memory()
        fields_after_second = cache_shallow_fields(cache)
        snapshot = tracemalloc.take_snapshot()
        top_files = [
            {"filename": Path(s.traceback[0].filename).name,
             "python_traced_bytes": s.size, "allocation_blocks": s.count}
            for s in snapshot.statistics("filename")[:12]
        ]
    finally:
        tracemalloc.stop()
    if first["route_result_signatures"] != second["route_result_signatures"]:
        raise AssertionError("Repeated frozen shortest paths differ")
    if _flow_checksum(flows) != hash_before:
        raise AssertionError("Frozen cache probe mutated funded worker flows")

    def report_phase(p):
        d = dict(p)
        # Signatures were compared internally; do not emit gigantic route
        # payloads, as result length is NOT route count or cache size.
        d.pop("route_result_signatures")
        return d

    first_counts = first["watched_function_calls"]
    second_counts = second["watched_function_calls"]
    key = "gate09.py/dijkstra"
    dijkstra1 = first_counts.get(key, {}).get("total_calls", 0)
    dijkstra2 = second_counts.get(key, {}).get("total_calls", 0)
    return {
        "schema": SCHEMA,
        "status": "PASS_ADR057_UNCHANGED_ORIGINAL_CACHE_DIAGNOSTIC",
        "fixture": fixture,
        "geometry_nodes": meta["geometry_nodes"],
        "edges": len(net.edges),
        "adjacency_entries": sum(len(v) for v in net.adj.values()),
        "workers": meta["workers"],
        "production_nodes": meta["production_nodes_count"],
        "demand_nodes": meta["demand_nodes_count"],
        "producer_groups": len(net.groups),
        "markets_node_product": len(net.markets),
        "initial_funded_columns": len(flows),
        "distinct_origin_seller_queries": len(set(queries)),
        "worker_ledger_sha256": meta["worker_ledger_sha256"],
        "initial_funded_flow_sha256": hash_before,
        "original_quartic_transport_edges": meta[
            "uses_original_quartic_transport_and_tariff_edges"],
        "production_is_subset_of_demand": True,
        "traveler_demand_not_duplicated": True,
        "original_frozen_route_cache_class_unmodified": True,
        "first_pass_cprofile": report_phase(first),
        "same_snapshot_repeated_pass_cprofile": report_phase(second),
        "dijkstra_calls_first_pass": dijkstra1,
        "dijkstra_calls_repeated_pass": dijkstra2,
        "dijkstra_call_reduction_proxy_only_not_measured_cache_hit_rate": True,
        "cache_top_level_fields_after_first": fields_after_first,
        "cache_top_level_fields_after_repeated": fields_after_second,
        "python_traced_bytes_post_cache_construct": alloc_after_creation,
        "python_traced_bytes_after_first_pass": alloc_after_first,
        "python_traced_bytes_after_repeated_pass": alloc_after_second,
        "python_traced_peak_bytes_through_first_pass": peak_after_first,
        "python_traced_peak_bytes_total_probe": total_peak,
        "python_traced_top_files_end_of_probe": top_files,
        "python_traced_heap_excludes_graph_built_before_tracing": True,
        "python_traced_heap_excludes_full_original_F072_solver": True,
        "process_peak_rss_before_probe": before_rss,
        "process_peak_rss_after_probe": rss(),
        "process_peak_rss_is_lifetime_highwater_not_delta": True,
        "original_F072_certificates_here": "NOT_ATTEMPTED_CACHE_OBSERVATION_ONLY",
        "independent_F072_regression_in_separate_CI_job": True,
        "original_solver_and_F059_untouched": True,
        "production_approved": False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture", required=True, choices=tuple(FIXTURES))
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    data = {
        "schema": SCHEMA,
        "status": "FAILED_ADR057_CACHE_DIAGNOSTIC",
        "fixture": args.fixture,
        "platform": platform.platform(),
        "python": platform.python_version(),
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "production_approved": False,
    }
    try:
        data.update(diagnostic(args.fixture))
    except Exception as exc:
        data["status"] = "FAILED_ADR057_CACHE_DIAGNOSTIC"
        data["error"] = repr(exc)
        data["traceback"] = traceback.format_exc()
        print("[ADR057] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output = args.output / f"GA-Gate09-ADR057-{args.fixture}-{stamp}.json"
    output.write_text(json.dumps(data, indent=2, sort_keys=True,
                                  ensure_ascii=False)+"\n", encoding="utf-8")
    archive = args.output / f"GA-Gate09-ADR057-Evidencias-{args.fixture}-{stamp}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(output, output.name)
    if data["status"].startswith("PASS_"):
        first, repeated = (
            data["first_pass_cprofile"], data["same_snapshot_repeated_pass_cprofile"])
        print("[ADR057:{}] PASS group_queries={} unique_keys={} "
              "Dijkstra_first={} Dijkstra_repeated={} "
              "profiled_wall_first={} profiled_wall_repeated={} "
              "traced_after_first={} traced_after_repeated={} "
              "RSS_peak_B={}".format(
                  args.fixture, first["calls_requested"],
                  first["distinct_origin_seller_keys"],
                  data["dijkstra_calls_first_pass"],
                  data["dijkstra_calls_repeated_pass"],
                  first["instrumented_wall_s"],
                  repeated["instrumented_wall_s"],
                  data["python_traced_bytes_after_first_pass"],
                  data["python_traced_bytes_after_repeated_pass"],
                  data["process_peak_rss_after_probe"]["process_peak_rss_bytes"]),
              flush=True)
    print("[ADR057] STATUS:", data["status"], flush=True)
    print("[ADR057] EVIDENCE:", archive, flush=True)
    return 0 if data["status"].startswith("PASS_") else 1


if __name__ == "__main__":
    raise SystemExit(main())
