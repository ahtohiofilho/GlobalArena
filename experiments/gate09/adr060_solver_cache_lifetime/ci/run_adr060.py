#!/usr/bin/env python3
"""ADR-060 LAB: observe ORIGINAL F072 solver route-cache lifetime.

The original R45 module creates FrozenRouteCache instances for route
candidate generation and global route separation. This lab replaces
ONLY the Python name R45.FrozenRouteCache with a transparent *factory*
that returns the ORIGINAL cache type unmodified. It tracks cache
liveness using weak references, distinct load epochs, cold construction
CPU, and process-lifetime native RSS.

Two experimental factory schedules are compared in separate processes:
  baseline             original cache lifetime and Python GC policy;
  gc_before_cache       gc.collect() before each original cache creation.

This is NOT the release_first ownership wrapper of ADR059: a collector
cannot forcibly discard still-referenced caches. If existing caches
are live at a new construction, that is reported explicitly. There is
no added cache reuse, no changed route oracle, no new numerical solver,
and no F059 or C#/Godot change. The independent ORIGINAL F072 certificate
is REQUIRED for any PASS. A 2048-node F072 claim is never made.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gc
from hashlib import sha256
import json
from math import isfinite
from pathlib import Path
import platform
import sys
from time import perf_counter, process_time
import traceback
from unittest.mock import patch
import weakref
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "adr059_epoch_cache" / "ci"))
import run_adr059 as R59
from clearing_r5 import _flow_checksum

SCHEMA = "GA_GATE09_ADR060_ORIGINAL_F072_SOLVER_CACHE_LIVENESS_V1"
POLICIES = ("baseline", "gc_before_cache")
FIXTURES = R59.FIXTURES
R55 = R59.R58.R57.R56.R55
R51 = R55.R54.R53.R52.ADR51
R45 = R51.R45
ORIGINAL_CACHE_TYPE = R45.FrozenRouteCache
TOL = R55.TOL


class ObservedCacheFactory:
    """No cache proxy; original constructor, object and routing methods."""

    def __init__(self, policy, memory_source):
        if policy not in POLICIES:
            raise ValueError("Unregistered original solver cache observation")
        self.policy = policy
        self.memory_source = memory_source
        self.weakrefs = []
        self.events = []
        self.maximum_simultaneously_live = 0
        self.live_before_next_cache_max = 0
        self.gc_calls = 0
        self.native_rss_peak_bytes = None

    def live(self):
        return sum(item() is not None for item in self.weakrefs)

    def __call__(self, net, loads, exponent):
        if exponent != 4:
            raise AssertionError("Original quartic route cost changed")
        live_before = self.live()
        self.live_before_next_cache_max = max(
            self.live_before_next_cache_max, live_before)
        if self.policy == "gc_before_cache":
            gc.collect()
            self.gc_calls += 1
        after_gc_live = self.live()
        # GC is NOT authorized to mutate or prematurely destroy
        # still-owned original cache objects; no fake release claim.
        start = perf_counter()
        cache = ORIGINAL_CACHE_TYPE(net, loads, exponent)
        construction_ms = (perf_counter() - start) * 1000
        try:
            ref = weakref.ref(cache)
        except TypeError as exc:
            raise AssertionError("Original cache type must be weak-referenceable") from exc
        self.weakrefs.append(ref)
        live_after = self.live()
        self.maximum_simultaneously_live = max(
            self.maximum_simultaneously_live, live_after)
        try:
            rss_bytes, rss_method = self.memory_source()
        except (OSError, AttributeError, ValueError):
            rss_bytes, rss_method = None, "NATIVE_RSS_UNAVAILABLE"
        if rss_bytes is not None:
            self.native_rss_peak_bytes = max(
                self.native_rss_peak_bytes or 0, rss_bytes)
        epoch = R59.load_epoch_token(loads)
        self.events.append({
            "cache_sequence": len(self.events) + 1,
            "load_epoch_sha256_within_same_network": epoch,
            "live_original_caches_before_creation": live_before,
            "live_original_caches_after_optional_gc": after_gc_live,
            "live_original_caches_immediately_after_creation": live_after,
            "original_cache_class_name": type(cache).__name__,
            "original_cache_module": type(cache).__module__,
            "original_cache_constructor_elapsed_ms": round(construction_ms, 5),
            "native_process_lifetime_peak_rss_bytes_at_creation": rss_bytes,
            "native_rss_source": rss_method,
        })
        return cache

    def report(self):
        return {
            "original_cache_type_returned_unchanged": True,
            "R45_only_cache_factory_instrumented": True,
            "original_ADR042_independent_auditor_NOT_monkeypatched": True,
            "constructed_R45_route_cache_instances": len(self.events),
            "distinct_physical_edge_load_epochs":
                len({item["load_epoch_sha256_within_same_network"]
                     for item in self.events}),
            "maximum_simultaneously_live_R45_caches":
                self.maximum_simultaneously_live,
            "maximum_old_R45_caches_live_before_next_creation":
                self.live_before_next_cache_max,
            "original_R45_caches_still_live_after_solve": self.live(),
            "explicit_gc_before_each_cache_construction": self.gc_calls,
            "process_native_rss_highwater_during_cache_creation_bytes":
                self.native_rss_peak_bytes,
            "observed_original_cache_construction_events": self.events,
            "no_actual_solver_cache_replacement_or_reuse": True,
        }


def native_rss():
    """Native process-lifetime RSS measurement inherited from ADR055."""
    return R55.peak_process_rss_bytes()


def run_solver(fixture, policy):
    if fixture not in FIXTURES or policy not in POLICIES:
        raise ValueError("Unregistered original solver scenario/policy")
    if R51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Original source fingerprint drift")
    if R45.FrozenRouteCache is not ORIGINAL_CACHE_TYPE:
        raise AssertionError("External or concurrent original R45 cache patch")
    conf = FIXTURES[fixture]
    net, flows, meta = R55.R54.R53.R52.build_population(conf["config"])
    workers = meta.pop("source_workers")
    if (len(workers) != 4096 or meta["workers"] != 4096
        or meta["geometry_nodes"] != 5762
        or meta["production_nodes_count"] != conf["production_nodes"]
        or meta["demand_nodes_count"] != conf["demand_nodes"]
        or len(net.groups) != conf["groups"]
        or len(net.markets) != conf["markets"]
        or not meta["physical_production_nodes"].issubset(
            meta["physical_demand_nodes"])
        or not meta["travel_time_does_not_duplicate_demand"]
        or meta["initial_real_flow_sha256"] != _flow_checksum(flows)):
        raise AssertionError("Real worker demand/production ledger drift")
    del workers
    gc.collect()

    original_hash = _flow_checksum(flows)
    before_rss, method = native_rss()
    observed = ObservedCacheFactory(policy, native_rss)
    wall0, cpu0 = perf_counter(), process_time()
    try:
        # Only R45 route generation/separation cache *construction*
        # is observed. The original ADR042 independent F072 verifier
        # is NOT patched; the exact original cache instances are returned.
        with patch.object(R45, "FrozenRouteCache", observed):
            result = R51.solve_chunked(net, flows, conf["budget"])
    finally:
        # unittest.mock.patch guarantees restoration also on exception.
        if R45.FrozenRouteCache is not ORIGINAL_CACHE_TYPE:
            raise AssertionError("Original R45 cache type not restored")

    elapsed_wall = perf_counter() - wall0
    elapsed_cpu = process_time() - cpu0
    after_rss, after_method = native_rss()
    if original_hash != _flow_checksum(flows):
        raise AssertionError("Original worker-funded flows mutated")
    certified = (
        R51.is_certified(result)
        and R55.R54.R53.approve_original_F072(result)
    )
    if not certified:
        if result.get("final_flow_sha256") is not None:
            raise AssertionError("Uncertified F072 final allocation was published")
        raise AssertionError("Mandatory original independent F072 certificate failed")
    certificate = result["certificate"]
    if (result.get("original_congestion_exponent") != 4
        or certificate.get("status") != "PASS_STATIC_F072_FIRST_ORDER_ONLY"
        or not isfinite(certificate["global_F072_gap"])
        or not isfinite(certificate["worst_group_F072_gap"])
        or certificate["global_F072_gap"] > TOL
        or certificate["worst_group_F072_gap"] > TOL):
        raise AssertionError("Original independent F072 tolerance/physics changed")
    witness = observed.report()
    if witness["constructed_R45_route_cache_instances"] <= 0:
        raise AssertionError("Cache instrumentation did not intercept original solver")
    if witness["distinct_physical_edge_load_epochs"] <= 0:
        raise AssertionError("No original solver load snapshot was observed")
    if policy == "gc_before_cache" and (
        witness["explicit_gc_before_each_cache_construction"]
        != witness["constructed_R45_route_cache_instances"]):
        raise AssertionError("GC policy did not execute before every cache")
    if policy == "baseline" and witness[
            "explicit_gc_before_each_cache_construction"] != 0:
        raise AssertionError("Baseline executed unexpected explicit GC")
    return {
        "schema": SCHEMA,
        "status": "PASS_ADR060_ORIGINAL_SOLVER_F072_AND_CACHE_LIVENESS_OBSERVED",
        "fixture": fixture,
        "policy": policy,
        "original_5762_node_goldberg_world": True,
        "workers": meta["workers"],
        "production_nodes": meta["production_nodes_count"],
        "demand_nodes": meta["demand_nodes_count"],
        "aggregated_production_groups": len(net.groups),
        "markets_node_item": len(net.markets),
        "worker_ledger_sha256": meta["worker_ledger_sha256"],
        "initial_flow_sha256": original_hash,
        "final_original_F072_flow_sha256": result["final_flow_sha256"],
        "original_independent_F072_status": certificate["status"],
        "original_independent_F072_global_gap": certificate["global_F072_gap"],
        "original_independent_F072_worst_group_gap":
            certificate["worst_group_F072_gap"],
        "original_solver_status": result["status"],
        "original_work_counters": result["work"],
        "original_generation_summary": result["generations"],
        "original_quartic_congestion_exponent": 4,
        "original_budget_limits": vars(conf["budget"]),
        "observed_solver_process_cpu_seconds": round(elapsed_cpu, 5),
        "observed_solver_wall_seconds": round(elapsed_wall, 5),
        "observed_solver_native_process_rss_highwater_before_bytes": before_rss,
        "observed_solver_native_process_rss_highwater_after_bytes": after_rss,
        "native_rss_source": after_method,
        "cache_liveness_observations": witness,
        "gc_before_cache_is_not_ADR059_owned_release_first": True,
        "no_claim_of_reduced_CSharp_Godot_turn_latency": True,
        "not_F059_financial_ratification": True,
        "2048_node_F072_not_attempted": True,
        "CSharp_Godot_untouched": True,
        "production_approved": False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture", required=True, choices=tuple(FIXTURES))
    parser.add_argument("--policy", required=True, choices=POLICIES)
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": SCHEMA,
        "utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "status": "FAILED_ADR060_ORIGINAL_SOLVER_CACHE_DIAGNOSTIC",
        "production_approved": False,
    }
    try:
        report.update(run_solver(args.fixture, args.policy))
    except Exception as exc:
        report["status"] = "FAILED_ADR060_ORIGINAL_SOLVER_CACHE_DIAGNOSTIC"
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[ADR060] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    out = args.output / (
        "GA-Gate09-ADR060-" + args.fixture + "-" + args.policy + "-" + stamp + ".json")
    out.write_text(json.dumps(report, indent=2, sort_keys=True,
                              ensure_ascii=False) + "\n", encoding="utf-8")
    archive = args.output / (
        "GA-Gate09-ADR060-Evidencias-" + args.fixture + "-"
        + args.policy + "-" + stamp + ".zip")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as packed:
        packed.write(out, out.name)
    if report["status"].startswith("PASS_"):
        stats = report["cache_liveness_observations"]
        work = report["original_work_counters"]
        print("[ADR060:{}:{}] PASS original_F072={} worst={} final_hash={} "
              "R45_cache_constructions={} distinct_load_epochs={} "
              "live_before_next_max={} max_live={} live_after={} "
              "H={} Krylov={} scans={} cpu_s={} wall_s={} "
              "rss_peak_B={} gc_before_creates={}".format(
                  args.fixture, args.policy,
                  report["original_independent_F072_global_gap"],
                  report["original_independent_F072_worst_group_gap"],
                  report["final_original_F072_flow_sha256"],
                  stats["constructed_R45_route_cache_instances"],
                  stats["distinct_physical_edge_load_epochs"],
                  stats["maximum_old_R45_caches_live_before_next_creation"],
                  stats["maximum_simultaneously_live_R45_caches"],
                  stats["original_R45_caches_still_live_after_solve"],
                  work.get("H_evaluations_including_rejected_trials"),
                  work.get("matrixfree_Jv_and_Jtv_products"),
                  work.get("separate_shortest_path_scans"),
                  report["observed_solver_process_cpu_seconds"],
                  report["observed_solver_wall_seconds"],
                  report["observed_solver_native_process_rss_highwater_after_bytes"],
                  stats["explicit_gc_before_each_cache_construction"]),
              flush=True)
    print("[ADR060] STATUS:", report["status"], flush=True)
    print("[ADR060] EVIDENCE:", archive, flush=True)
    return 0 if report["status"].startswith("PASS_") else 1


if __name__ == "__main__":
    raise SystemExit(main())
