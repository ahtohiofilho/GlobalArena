#!/usr/bin/env python3
"""ADR-058 LAB: original frozen route cache across two VALID physical load snapshots.

Observe memory retention, cache cold/warm behavior and snapshot invalidation.
Never reuse a frozen cache with a different load vector. Do not change
the route oracle, solver, prices, congestion, tariffs or original F072 gates.

The two valid worker-sourced flow allocations are NOT equilibria and
are never called F072 certificates. Original 48/96/192/384 F072 mandatory
regressions run independently in the same dual-OS CI workflow.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import datetime, timezone
import gc
from hashlib import sha256
import json
from math import isfinite
from pathlib import Path
import platform
import sys
from time import perf_counter, process_time
import tracemalloc
import traceback
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "adr057_route_cache" / "ci"))
import run_adr057 as R57
from clearing_r5 import _flow_checksum
from oracle_cache_r14 import FrozenRouteCache

SCHEMA = "GA_GATE09_ADR058_TWO_PHYSICAL_LOAD_SNAPSHOTS_CACHE_LIFECYCLE_V1"
FIXTURES = R57.FIXTURES
TRANSFER_FRACTION_OF_ORIGINAL_FUNDED_COLUMN = 0.25
MAX_SIGNED_SAMPLE_TARGETS = 8


def physical_pair(net, original):
    """Move 25% of one funded column to another IN EACH group.

    Preserve every producer's physical supply and market demand ledger.
    Never change the worker records, ownership, routes or the network.
    Both allocations remain positive at all funded markets. A changed
    allocation is not an independently F072-certified equilibrium.
    """
    groups = defaultdict(list)
    for col in original:
        groups[col.group_id].append(col)
    updated = dict(original)
    changes = []
    for gid in sorted(net.groups):
        cols = sorted(groups[gid], key=repr)
        if len(cols) < 2:
            raise AssertionError("No alternate physically funded column")
        donor, receiver = cols[:2]
        delta = original[donor] * TRANSFER_FRACTION_OF_ORIGINAL_FUNDED_COLUMN
        if not (delta > 0 and isfinite(delta)):
            raise AssertionError("Non-positive flow shift")
        updated[donor] -= delta
        updated[receiver] += delta
        changes.append((gid, repr(donor), repr(receiver), delta))
        if (updated[donor] <= 0 or updated[receiver] <= 0
            or abs(sum(updated[c] for c in cols) - net.groups[gid].supply)
                > max(1e-10, net.groups[gid].supply * 1e-10)):
            raise AssertionError("Physical group supply changed")
    if set(updated) != set(original):
        raise AssertionError("New route columns were fabricated")
    if _flow_checksum(updated) == _flow_checksum(original):
        raise AssertionError("Changed flow snapshot identical to original")
    loads_a, qa, prices_a = net.snapshots(original)
    loads_b, qb, prices_b = net.snapshots(updated)
    if set(qa) != set(qb) or not all(
        qa[k] > 0 and qb[k] > 0 and isfinite(prices_a[k])
        and isfinite(prices_b[k]) for k in qa):
        raise AssertionError("Changed worker flows left unpriced market")
    changed_edges = sum(
        loads_a[eid] != loads_b[eid] for eid in net.edges)
    if changed_edges <= 0:
        raise AssertionError("Flow changes did not alter edge loads")
    return updated, loads_a, loads_b, {
        "physical_group_shifts": len(changes),
        "fraction_of_original_funded_donor_flow": (
            TRANSFER_FRACTION_OF_ORIGINAL_FUNDED_COLUMN),
        "changed_edge_loads": changed_edges,
        "market_count_positive_both": len(qa),
        "original_flow_sha256": _flow_checksum(original),
        "perturbed_flow_sha256": _flow_checksum(updated),
        "perturbed_allocation_F072_original_certificate": "NOT_ATTEMPTED",
        "initial_allocation_F072_original_certificate": "NOT_ATTEMPTED",
    }


def compact_signature(distances, paths, origin, seller):
    """Compare cold and warm cache outputs without storing entire path maps."""
    if origin not in distances or origin not in paths:
        raise AssertionError("Cache response missing source")
    keys = sorted(paths)[:MAX_SIGNED_SAMPLE_TARGETS]
    sample = [
        (key, distances.get(key), paths[key])
        for key in keys
    ]
    return sha256(repr((origin, seller, len(distances), len(paths),
                        sample)).encode("utf-8")).hexdigest()


def observe_pass(cache, queries):
    """Only ORIGINAL FrozenRouteCache.paths, no independent router."""
    start_wall, start_cpu = perf_counter(), process_time()
    signed = []
    for origin, seller in queries:
        distances, paths, _ = cache.paths(origin, seller)
        signed.append(compact_signature(distances, paths, origin, seller))
    return {
        "elapsed_wall_seconds": round(perf_counter() - start_wall, 6),
        "elapsed_process_cpu_seconds": round(process_time() - start_cpu, 6),
        "group_queries": len(queries),
        "unique_origin_seller_pairs": len(set(queries)),
        "signed_route_response_digest": sha256(
            "\n".join(signed).encode("utf-8")).hexdigest(),
        "unprofiled_wall_time_NOT_CSharp_SLA": True,
    }


def weight_drift(cache_a, cache_b, sellers):
    """Observe original oracle's seller-specific edge costs across loads.

    Distinct weights demonstrate that reusing cache A for snapshot B would
    be semantically invalid EVEN when some shortest path IDs coincide.
    This is not an assertion that every shortest path changed.
    """
    by_seller = []
    total_changed = 0
    for seller in sellers:
        a = cache_a.weights(seller)
        b = cache_b.weights(seller)
        if set(a) != set(b):
            raise AssertionError("Seller weight edge sets differed")
        count = sum(a[eid] != b[eid] for eid in a)
        if any(not (isfinite(a[eid]) and isfinite(b[eid]))
               for eid in a):
            raise AssertionError("Non-finite original route edge weight")
        total_changed += count
        by_seller.append({
            "seller": seller, "edges": len(a),
            "edge_weights_changed_for_new_loads": count,
        })
    if not total_changed:
        raise AssertionError("Changed physical loads gave unchanged seller weights")
    return {"by_seller": by_seller,
            "total_changed_edge_weights_across_sellers": total_changed,
            "frozen_old_cache_must_NOT_be_reused_on_new_loads": True}


def check_frozen(original, repeated, label):
    if (original["signed_route_response_digest"]
            != repeated["signed_route_response_digest"]
        or original["group_queries"] != repeated["group_queries"]):
        raise AssertionError("Frozen paths drifted on repeat: " + label)


def report_fixture(fixture):
    if fixture not in FIXTURES:
        raise ValueError("Only original 192/384 physical worker fixtures allowed")
    if R57.R56.R55.R54.R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Original source manifest changed")
    f = FIXTURES[fixture]
    net, initial, meta = R57.R56.R55.R54.R53.R52.build_population(f["config"])
    workers = meta.pop("source_workers")
    if (len(workers) != 4096 or meta["workers"] != 4096
        or meta["demand_nodes_count"] != f["demand_nodes"]
        or meta["production_nodes_count"] != f["production_nodes"]
        or len(net.groups) != f["groups"] or len(net.markets) != f["markets"]
        or not meta["travel_time_does_not_duplicate_demand"]
        or not meta["physical_production_nodes"].issubset(
            meta["physical_demand_nodes"])
        or meta["initial_real_flow_sha256"] != _flow_checksum(initial)):
        raise AssertionError("Original worker/population contract drift")

    modified, loads_a, loads_b, physical = physical_pair(net, initial)
    queries = tuple((net.groups[gid].origin, net.groups[gid].seller)
                    for gid in sorted(net.groups))
    all_sellers = tuple(sorted(set(seller for _, seller in queries)))
    del workers
    gc.collect()
    rss_before = R57.rss()
    tracemalloc.start(1)
    try:
        cache_a = FrozenRouteCache(net, loads_a, 4)
        a_first = observe_pass(cache_a, queries)
        a_warm = observe_pass(cache_a, queries)
        check_frozen(a_first, a_warm, "snapshot A")
        traced_after_a, peak_a = tracemalloc.get_traced_memory()
        cache_a_fields = R57.cache_shallow_fields(cache_a)

        # Important: load B MUST construct a NEW cache. There is NO
        # attempt to inject new loads into the already frozen A cache.
        cache_b = FrozenRouteCache(net, loads_b, 4)
        b_first = observe_pass(cache_b, queries)
        b_warm = observe_pass(cache_b, queries)
        check_frozen(b_first, b_warm, "snapshot B")
        drift = weight_drift(cache_a, cache_b, all_sellers)
        traced_both_live, peak_both = tracemalloc.get_traced_memory()
        cache_b_fields = R57.cache_shallow_fields(cache_b)
        # Snapshot A stays accessible until now; drop it to observe
        # whether the Python tracked heap drops with only B retained.
        del cache_a
        gc.collect()
        traced_after_release, peak_after_release = tracemalloc.get_traced_memory()
        # Cache B remains usable after A is removed.
        b_after_release = observe_pass(cache_b, queries)
        check_frozen(b_first, b_after_release, "B after release of A")
        traced_after_final, peak_final = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()
    if (_flow_checksum(initial) != physical["original_flow_sha256"]
        or _flow_checksum(modified) != physical["perturbed_flow_sha256"]):
        raise AssertionError("Cache probe mutated original physical flows")
    return {
        "schema": SCHEMA,
        "status": "PASS_ADR058_TWO_FROZEN_SNAPSHOTS_LIFECYCLE_OBSERVED",
        "fixture": fixture,
        "physical_goldberg_nodes": meta["geometry_nodes"],
        "physical_workers": meta["workers"],
        "demand_nodes": meta["demand_nodes_count"],
        "production_nodes": meta["production_nodes_count"],
        "aggregated_groups": len(net.groups),
        "market_pairs": len(net.markets),
        "flow_allocation_pair": physical,
        "worker_ledger_sha256": meta["worker_ledger_sha256"],
        "seller_count": len(all_sellers),
        "snapshot_A_first_cold": a_first,
        "snapshot_A_repeated_warm": a_warm,
        "snapshot_B_first_cold_NEW_CACHE": b_first,
        "snapshot_B_repeated_warm": b_warm,
        "snapshot_B_after_A_cache_release": b_after_release,
        "seller_specific_weight_drift": drift,
        "cache_A_shallow_fields": cache_a_fields,
        "cache_B_shallow_fields": cache_b_fields,
        "traced_python_bytes_after_A_warm": traced_after_a,
        "traced_python_peak_during_A": peak_a,
        "traced_python_bytes_with_A_and_B_live": traced_both_live,
        "traced_python_peak_with_both_live": peak_both,
        "traced_python_bytes_after_A_release_gc": traced_after_release,
        "traced_python_peak_after_release": peak_after_release,
        "traced_python_bytes_after_B_reuse": traced_after_final,
        "traced_python_peak_total": peak_final,
        "observed_python_heap_drop_after_release_bytes": (
            max(0, traced_both_live - traced_after_release)),
        "tracemalloc_started_after_network_construction": True,
        "tracemalloc_does_not_measure_full_solver_or_native_heap": True,
        "rss_before_probes": rss_before,
        "rss_after_probes": R57.rss(),
        "rss_is_process_lifetime_highwater_not_current_memory": True,
        "cache_is_original_frozen_route_cache_no_mutation": True,
        "commercial_F072_attempted_in_this_cache_probe": False,
        "original_F072_mandatory_regression_is_separate_CI_job": True,
        "same_physical_workers_not_multiple_demand_sources": True,
        "no_F059_ratification": True,
        "CSharp_Godot_untouched": True,
        "production_approved": False,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--fixture", choices=tuple(FIXTURES), required=True)
    p.add_argument("--output", type=Path, default=Path.cwd())
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": SCHEMA,
        "utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "status": "FAILED_ADR058_DIAGNOSTIC_CONTRACT",
        "production_approved": False,
    }
    try:
        report.update(report_fixture(args.fixture))
    except Exception as exc:
        report["status"] = "FAILED_ADR058_DIAGNOSTIC_CONTRACT"
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[ADR058] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    target = args.output / ("GA-Gate09-ADR058-" + args.fixture + "-" + stamp + ".json")
    target.write_text(json.dumps(report, indent=2, sort_keys=True,
                                 ensure_ascii=False) + "\n", encoding="utf-8")
    archive = args.output / ("GA-Gate09-ADR058-Evidencias-" + args.fixture + "-" + stamp + ".zip")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(target, target.name)
    if report["status"].startswith("PASS_"):
        print("[ADR058:{}] PASS changed_edge_loads={} seller_weight_drift={} "
              "A_cold={} A_warm={} B_cold={} B_warm={} "
              "python_heap_A={} both={} after_release={} reclaimed={} rss_peak={} "
              "F072_SOLVE_NOT_ATTEMPTED".format(
                  args.fixture,
                  report["flow_allocation_pair"]["changed_edge_loads"],
                  report["seller_specific_weight_drift"][
                      "total_changed_edge_weights_across_sellers"],
                  report["snapshot_A_first_cold"]["elapsed_wall_seconds"],
                  report["snapshot_A_repeated_warm"]["elapsed_wall_seconds"],
                  report["snapshot_B_first_cold_NEW_CACHE"]["elapsed_wall_seconds"],
                  report["snapshot_B_repeated_warm"]["elapsed_wall_seconds"],
                  report["traced_python_bytes_after_A_warm"],
                  report["traced_python_bytes_with_A_and_B_live"],
                  report["traced_python_bytes_after_A_release_gc"],
                  report["observed_python_heap_drop_after_release_bytes"],
                  report["rss_after_probes"]["process_peak_rss_bytes"]), flush=True)
    print("[ADR058] STATUS:", report["status"], flush=True)
    print("[ADR058] EVIDENCE:", archive, flush=True)
    return 0 if report["status"].startswith("PASS_") else 1


if __name__ == "__main__":
    raise SystemExit(main())
