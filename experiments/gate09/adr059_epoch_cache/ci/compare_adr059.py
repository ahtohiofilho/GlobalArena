#!/usr/bin/env python3
"""ADR059 cross-policy same-HEAD comparator; independent OS/fixture processes.

Artifacts from distinct GitHub Actions jobs are compared on identical
worker, seller-weight, route-response and physical-flow fingerprints.
A memory improvement is an OBSERVATION, not a source-level F072 speedup
or a game runtime SLA. Never infer retained native heap from RSS highwater.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import zipfile

import run_adr059 as A

SCHEMA = "GA_GATE09_ADR059_CROSS_POLICY_SEMANTICS_AND_PYTHON_HEAP_COMPARE_V1"


def load_policy_archives(directory):
    records = {}
    for filename in sorted(directory.glob("GA-Gate09-ADR059-Evidencias-*.zip")):
        with zipfile.ZipFile(filename) as archive:
            members = [n for n in archive.namelist() if n.endswith(".json")]
            if len(members) != 1:
                raise ValueError("Expected one ADR059 JSON per evidence archive")
            data = json.loads(archive.read(members[0]))
        if data.get("schema") != A.SCHEMA or data.get("policy") not in A.POLICIES:
            raise ValueError("Wrong original ADR059 policy evidence")
        if data["policy"] in records:
            raise ValueError("Duplicate policy evidence")
        records[data["policy"]] = data
    if set(records) != set(A.POLICIES):
        raise ValueError("Missing overlap/release_first evidence")
    return records


def compare(records, fixture):
    if fixture not in A.FIXTURES:
        raise ValueError("No comparison for unregistered fixture")
    if set(records) != set(A.POLICIES):
        raise ValueError("Must compare both original policies")
    overlap = records["overlap"]
    serial = records["release_first"]
    for r in (overlap, serial):
        if (r["status"] != "PASS_ADR059_ORIGINAL_FROZEN_EPOCH_CACHE_POLICY_LAB"
            or r["fixture"] != fixture
            or r["workers"] != 4096
            or r["demand_nodes"] != A.FIXTURES[fixture]["demand_nodes"]
            or r["markets_node_item"] != A.FIXTURES[fixture]["markets"]
            or r["original_quartic_exponent"] != 4
            or not r["old_epoch_token_rejected_after_switch"]
            or not r["different_epoch_tokens"]
            or r["production_approved"]
            or not r["A_and_B_not_original_F072_certificates"]
            or not r["original_independent_F072_separate_CI_regression"]):
            raise AssertionError("A policy violated its original physical gate")
    if not overlap["old_cache_still_owned_during_B"]:
        raise AssertionError("Overlap baseline lost old cache before B")
    if serial["old_cache_still_owned_during_B"]:
        raise AssertionError("Release-first still kept old cache during B")
    if (not serial["release_A_before_B_build_executed"]
        or serial["traced_python_bytes_after_release_A_before_B"] is None):
        raise AssertionError("Release-first failed to free before new cache")
    if overlap["release_A_before_B_build_executed"]:
        raise AssertionError("Overlap incorrectly used release-first")

    must_match = [
        "goldberg_geometry_nodes", "workers", "demand_nodes",
        "production_nodes", "groups", "markets_node_item",
        "worker_ledger_sha256", "original_flow_sha256",
        "changed_flow_sha256", "snapshot_A_epoch_sha256",
        "snapshot_B_epoch_sha256", "original_seller_weights_A",
        "original_seller_weights_B", "sellers_with_changed_original_weights",
    ]
    for key in must_match:
        if overlap[key] != serial[key]:
            raise AssertionError("Cross-policy physical or original weight mismatch: " + key)
    for phase in ("first_A_cold", "repeat_A_warm",
                  "first_B_cold", "repeat_B_warm", "B_after_A_retire"):
        a = overlap[phase]
        b = serial[phase]
        if (a["route_response_digest_sampled"] !=
                b["route_response_digest_sampled"]
            or a["group_queries"] != b["group_queries"]):
            raise AssertionError("Original route digest changes by policy: " + phase)

    overlap_heap = overlap["python_tracemalloc_bytes"]
    serial_heap = serial["python_tracemalloc_bytes"]
    high_overlap = overlap_heap["peak_total"]
    high_serial = serial_heap["peak_total"]
    if high_overlap <= 0 or high_serial <= 0:
        raise AssertionError("Missing Python-traced peak measurements")
    difference = high_overlap - high_serial
    return {
        "schema": SCHEMA,
        "fixture": fixture,
        "status": (
            "PASS_ADR059_POLICY_EQUIVALENCE_AND_PYTHON_HEAP_REDUCTION"
            if difference > 0
            else "NO_GO_ADR059_PYTHON_HEAP_REDUCTION_NOT_OBSERVED"),
        "original_route_and_weights_equivalent_across_policies": True,
        "worker_flows_and_load_epochs_identical_across_policies": True,
        "old_epoch_inaccessible_in_both_policies": True,
        "release_first_policy_proved_old_cache_absent_during_B": True,
        "python_peak_overlap_bytes": high_overlap,
        "python_peak_release_first_bytes": high_serial,
        "python_traced_peak_difference_bytes": difference,
        "python_traced_peak_reduction_fraction": round(difference / high_overlap, 6),
        "observed_reduction_not_a_game_CSharp_performance_guarantee": True,
        "process_RSS_is_lifetime_highwater_and_not_reclaimed_memory": True,
        "original_F072_regressions_are_separate_mandatory_jobs": True,
        "production_approved": False,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--fixture", required=True, choices=tuple(A.FIXTURES))
    p.add_argument("--directory", type=Path, required=True)
    p.add_argument("--output", type=Path, default=Path.cwd())
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    outcome = {"schema": SCHEMA, "fixture": args.fixture,
               "status": "FAILED_ADR059_CROSS_POLICY_EQUIVALENCE",
               "production_approved": False}
    try:
        outcome.update(compare(load_policy_archives(args.directory), args.fixture))
    except Exception as exc:
        outcome["status"] = "FAILED_ADR059_CROSS_POLICY_EQUIVALENCE"
        outcome["error"] = repr(exc)
        print("[ADR059-COMPARE] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = args.output / ("GA-Gate09-ADR059-COMPARISON-" + args.fixture + "-" + stamp + ".json")
    path.write_text(json.dumps(outcome, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    print("[ADR059-COMPARE] STATUS:", outcome["status"], flush=True)
    if "python_traced_peak_difference_bytes" in outcome:
        print("[ADR059-COMPARE] fixture={} overlap_peak_B={} release_first_peak_B={} "
              "delta_B={} saved_fraction={} original_routes_equivalent={}".format(
                args.fixture, outcome["python_peak_overlap_bytes"],
                outcome["python_peak_release_first_bytes"],
                outcome["python_traced_peak_difference_bytes"],
                outcome["python_traced_peak_reduction_fraction"],
                outcome["original_route_and_weights_equivalent_across_policies"]),
              flush=True)
    # An observed NO-GO in memory savings is honest research evidence,
    # not a false passing performance claim. Semantic failure IS CI red.
    return 1 if outcome["status"].startswith("FAILED_") else 0


if __name__ == "__main__":
    raise SystemExit(main())
