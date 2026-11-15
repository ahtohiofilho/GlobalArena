#!/usr/bin/env python3
"""ADR061: ONE high-ROI experiment, no more open-ended profiling.

Temporary, standalone route-weight precomputation for the original R13
path audit, preserving its *exact Dijkstra/canonical-path algorithm*.
The original independent ADR042/F072 verifier is run AFTER restoration
of the original R13.path_audit. A patched-run certificate alone is
NEVER sufficient to approve this experiment.

Baseline and candidate use identical physical worker networks, original
ADR051 solver / preconditioner, original quartic/cost/tariffs/budgets.
The experiment is NOT a production implementation or a C#/Godot SLA.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
from hashlib import sha256
import heapq
import json
from math import inf, isfinite
from pathlib import Path
import platform
import sys
from time import perf_counter, process_time
import traceback
from unittest.mock import patch
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "adr055_market_384" / "ci"))
import run_adr055 as R55
import clearing_r5 as R13
import certificate_r2 as R2
from gate09 import Column

SCHEMA = "GA_GATE09_ADR061_R13_ROUTE_WEIGHT_PRECOMPUTE_EXACT_F072_V1"
POLICIES = ("baseline", "precompute")
FIXTURES = {
    "192": {
        "config": R55.R54.R53.CONFIGS[R55.R54.CANDIDATE],
        "budget": R55.R54.LIMITS_192,
        "workers": 4096, "demand_nodes": 192, "production_nodes": 48,
        "groups": 96, "markets": 384,
    },
    "384": {
        "config": R55.CONFIG_384, "budget": R55.LIMITS_384,
        "workers": 4096, "demand_nodes": 384, "production_nodes": 96,
        "groups": 192, "markets": 768,
    },
}
R51 = R55.R54.R53.R52.ADR51
ORIGINAL_PATH_AUDIT = R13.path_audit
ADR42 = R51.R49.ADR48.ADR42
TOL = R55.TOL


class R13WeightsOncePerSnapshot:
    """Use original arithmetic and tie/ordering rules; only memoize weights.

    Missing edge weights are not fabricated; every weight is evaluated
    from the exact edge.congestion_cost + float(edge.weight_for(seller))
    expression from original certificate_r2.path_audit.
    Only one *current* non-empty snapshot is retained. Empty load maps
    use separate frozen zero-load weights because _reachable creates a
    new empty dict for each producer. Do NOT reuse after a load change.
    """

    def __init__(self):
        self.net = None
        self.snapshot = None
        self.seller_tables = {}
        self.empty_tables = {}
        self.path_calls = 0
        self.weights_computed = 0
        self.weight_table_hits = 0
        self.weight_table_misses = 0

    def _weights(self, net, seller, loads, exponent):
        if net is not self.net:
            self.net = net
            self.snapshot = None
            self.seller_tables = {}
            self.empty_tables = {}
        if not loads:
            tables = self.empty_tables
        else:
            if loads is not self.snapshot:
                self.snapshot = loads
                self.seller_tables = {}
            tables = self.seller_tables
        key = (seller, exponent)
        if key in tables:
            self.weight_table_hits += 1
            return tables[key]
        self.weight_table_misses += 1
        weights = {}
        for eid, edge in net.edges.items():
            # EXACT expression and order of original R2.path_audit
            w = (edge.congestion_cost(loads.get(eid, 0), exponent)
                 + float(edge.weight_for(seller)))
            if w <= 0 or not isfinite(w):
                raise ValueError("Invalid edge weight")
            weights[eid] = w
        self.weights_computed += len(weights)
        tables[key] = weights
        return weights

    def __call__(self, net, origin, civil, loads, exponent=4):
        self.path_calls += 1
        if exponent != 4:
            raise ValueError("Only frozen original quartic route cost")
        weights = self._weights(net, civil, loads, exponent)
        # Identical two-phase R2 Dijkstra, floating operations,
        # tolerance, neighbor ordering and canonical tie breaks.
        distance = {origin: 0.0}
        heap = [(0.0, origin)]
        while heap:
            d, u = heapq.heappop(heap)
            if d > distance[u] + R2.ABS_TOL:
                continue
            for v, eid in net.adj.get(u, ()):
                cost = d + weights[eid]
                if cost < distance.get(v, inf) - max(
                        R2.ABS_TOL, R2.REL_TOL * abs(cost)):
                    distance[v] = cost
                    heapq.heappush(heap, (cost, v))
        canon = {origin: ()}
        route_ways = {origin: 1}
        for u in sorted(distance, key=lambda x: (distance[x], x)):
            if u not in canon:
                continue
            for v, eid in net.adj.get(u, ()):
                if v not in distance or distance[u] >= distance[v]:
                    continue
                if R2.near(distance[u] + weights[eid], distance[v]):
                    candidate = canon[u] + (eid,)
                    if v not in canon or (
                            len(candidate), candidate) < (
                            len(canon[v]), canon[v]):
                        canon[v] = candidate
                    route_ways[v] = min(
                        2, route_ways.get(v, 0) + route_ways[u])
        return distance, canon, route_ways

    def counters(self):
        return {
            "R13_path_audit_calls": self.path_calls,
            "edge_weights_computed": self.weights_computed,
            "weight_table_reuse_hits": self.weight_table_hits,
            "weight_table_creation_misses": self.weight_table_misses,
            "one_nonempty_snapshot_at_a_time": True,
            "empty_load_tables_separate": True,
        }


def original_post_certification(net, candidate):
    """Rebuild the exact committed positive flows, run ORIGINAL independent F072.

    This must only be called OUTSIDE the temporary R13.path_audit patch.
    In addition to the solver's original R14/R13 cross-check (temporarily
    accelerated R13), this independently verifies with the true R13.
    """
    if R13.path_audit is not ORIGINAL_PATH_AUDIT:
        raise AssertionError("Original R13 oracle not restored for final F072")
    if candidate.get("final_allocations") is None:
        raise AssertionError("No certified final allocations")
    flows = {}
    for gid, destination, edges, quantity in candidate["final_allocations"]:
        col = Column(gid, destination, tuple(edges))
        if col in flows or quantity <= 0:
            raise AssertionError("Duplicate or non-positive final column")
        flows[col] = float(quantity)
    if R13._flow_checksum(flows) != candidate["final_flow_sha256"]:
        raise AssertionError("Final funded flow hash cannot be reproduced")
    original = ADR42.static_certificate(net, flows)
    if (original["status"] != "PASS_STATIC_F072_FIRST_ORDER_ONLY"
        or not original["static_complementarity_approximate"]
        or not isfinite(original["gap_global_complementarity"])
        or not isfinite(original["gap_worst_group_complementarity"])
        or original["gap_global_complementarity"] > TOL
        or original["gap_worst_group_complementarity"] > TOL):
        raise AssertionError("ORIGINAL UNPATCHED independent F072 rejected")
    return original


def exercise(fixture, policy):
    if fixture not in FIXTURES or policy not in POLICIES:
        raise ValueError("Only declared 192/384 and original/precompute variants")
    if R13.path_audit is not ORIGINAL_PATH_AUDIT:
        raise AssertionError("Untrusted external patch to original R13")
    if R51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Frozen original source manifest altered")
    conf = FIXTURES[fixture]
    net, flows, ledger = R55.R54.R53.R52.build_population(conf["config"])
    workers = ledger.pop("source_workers")
    if (len(workers) != conf["workers"]
        or ledger["geometry_nodes"] != 5762
        or ledger["workers"] != conf["workers"]
        or ledger["production_nodes_count"] != conf["production_nodes"]
        or ledger["demand_nodes_count"] != conf["demand_nodes"]
        or len(net.groups) != conf["groups"]
        or len(net.markets) != conf["markets"]
        or not ledger["physical_production_nodes"].issubset(
            ledger["physical_demand_nodes"])
        or not ledger["travel_time_does_not_duplicate_demand"]):
        raise AssertionError("Physical worker supply/demand fixture changed")
    del workers
    input_hash = R13._flow_checksum(flows)
    route = R13WeightsOncePerSnapshot()
    before_rss = R55.peak_process_rss_bytes()
    start_wall, start_cpu = perf_counter(), process_time()
    if policy == "precompute":
        # ONLY original R13 path_audit binding changes temporarily.
        # Its numeric result must be reproduced by the unpatched original
        # static certificate AFTER restoration, on the same exact net/flow.
        with patch.object(R13, "path_audit", route):
            solved = R51.solve_chunked(net, flows, conf["budget"])
    else:
        solved = R51.solve_chunked(net, flows, conf["budget"])
    solve_wall_s = perf_counter() - start_wall
    solve_cpu_s = process_time() - start_cpu
    if R13.path_audit is not ORIGINAL_PATH_AUDIT:
        raise AssertionError("Original path audit was not restored")
    if input_hash != R13._flow_checksum(flows):
        raise AssertionError("Original funded production was mutated")
    if not R51.is_certified(solved) or not R55.R54.R53.approve_original_F072(solved):
        if solved.get("final_flow_sha256") is not None:
            raise AssertionError("Uncertified final allocations published")
        raise AssertionError("Original solver F072 not certified")
    # Second FULL, INDEPENDENT, **UNPATCHED** original economic certificate:
    start_post_wall, start_post_cpu = perf_counter(), process_time()
    reference = original_post_certification(net, solved)
    post_wall_s = perf_counter() - start_post_wall
    post_cpu_s = process_time() - start_post_cpu
    orig = solved["certificate"]
    for k, field in (
        ("global_F072_gap", "gap_global_complementarity"),
        ("worst_group_F072_gap", "gap_worst_group_complementarity"),
    ):
        if orig[k] != reference[field]:
            raise AssertionError("Patched-path certificate differs from original "+k)
    if solved["original_congestion_exponent"] != 4:
        raise AssertionError("Original quartic congestion changed")
    end_rss = R55.peak_process_rss_bytes()
    return {
        "schema": SCHEMA,
        "status": "PASS_ADR061_ORIGINAL_F072_WITH_UNPATCHED_POST_CERTIFICATION",
        "fixture": fixture,
        "policy": policy,
        "workers": ledger["workers"],
        "production_nodes": ledger["production_nodes_count"],
        "demand_nodes": ledger["demand_nodes_count"],
        "groups": len(net.groups),
        "markets": len(net.markets),
        "worker_ledger_sha256": ledger["worker_ledger_sha256"],
        "initial_flow_sha256": input_hash,
        "original_final_F072_flow_sha256": solved["final_flow_sha256"],
        "original_static_F072_certificate": reference["status"],
        "original_global_gap": reference["gap_global_complementarity"],
        "original_worst_group_gap": reference["gap_worst_group_complementarity"],
        "original_work_counters": solved["work"],
        "original_generations": solved["generations"],
        "original_budget_limits": vars(conf["budget"]),
        "original_route_algorithm_and_tie_order_unchanged": True,
        "R13_path_audit_restored_before_original_post_certificate": True,
        "original_independent_ADR042_FULL_post_certificate": True,
        "original_R14_and_tariffs_untouched": True,
        "original_quartic_exponent": 4,
        "experimental_route_counters": (
            route.counters() if policy == "precompute" else None),
        "observed_solver_wall_s": round(solve_wall_s, 5),
        "observed_solver_cpu_s": round(solve_cpu_s, 5),
        "unpatched_independent_original_post_certificate_wall_s":
            round(post_wall_s, 5),
        "unpatched_independent_original_post_certificate_cpu_s":
            round(post_cpu_s, 5),
        "native_process_rss_before": before_rss,
        "native_process_rss_after": end_rss,
        "RSS_is_process_lifetime_highwater_and_NOT_cache_object_size": True,
        "192_and_384_fixtures_not_game_performance_SLA": True,
        "CSharp_Godot_original_untouched": True,
        "financial_F059_pending": True,
        "2048_demand_nodes_F072_not_claimed": True,
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
        "schema": SCHEMA, "fixture": args.fixture, "policy": args.policy,
        "utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "status": "FAILED_ADR061_ORIGINAL_F072_OR_NUMERICAL_EQUIVALENCE",
        "production_approved": False,
    }
    try:
        report.update(exercise(args.fixture, args.policy))
    except Exception as exc:
        report["status"] = "FAILED_ADR061_ORIGINAL_F072_OR_NUMERICAL_EQUIVALENCE"
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[ADR061] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output = args.output / (
        "GA-Gate09-ADR061-" + args.fixture + "-" + args.policy + "-" + stamp + ".json")
    output.write_text(
        json.dumps(report, indent=2, sort_keys=True, ensure_ascii=False)+"\n",
        encoding="utf-8")
    archive = args.output / (
        "GA-Gate09-ADR061-Evidencias-" + args.fixture + "-" +
        args.policy + "-" + stamp + ".zip")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(output, output.name)
    if report["status"].startswith("PASS_"):
        print("[ADR061:{}:{}] PASS original_independent_F072={} worst={} "
              "final_flow={} cpu_s={} solver_wall_s={} post_original_cpu_s={} "
              "R13_weight_reuse={} RSS_peak_B={}".format(
                  args.fixture, args.policy, report["original_global_gap"],
                  report["original_worst_group_gap"],
                  report["original_final_F072_flow_sha256"],
                  report["observed_solver_cpu_s"],
                  report["observed_solver_wall_s"],
                  report["unpatched_independent_original_post_certificate_cpu_s"],
                  report["experimental_route_counters"],
                  report["native_process_rss_after"][0]), flush=True)
    print("[ADR061] STATUS:", report["status"], flush=True)
    return 0 if report["status"].startswith("PASS_") else 1


if __name__ == "__main__":
    raise SystemExit(main())
