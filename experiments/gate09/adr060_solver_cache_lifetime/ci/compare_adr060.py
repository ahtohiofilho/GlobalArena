#!/usr/bin/env python3
"""Compare certified actual-F072 solver cache observations at the same HEAD.

Original F072 final allocation hash, original independent gaps and all
original deterministic solver work counters must agree exactly between
the baseline and an extra-Python-GC-before-cache policy.

RSS and time are native process observations from DIFFERENT CI jobs.
Those numbers are not a causal memory saving or a C#/Godot benchmark.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_adr060 as A

SCHEMA="GA_GATE09_ADR060_ORIGINAL_F072_CACHE_LIVENESS_POLICY_COMPARISON_V1"


def load_archives(directory):
    records={}
    for filename in sorted(directory.glob("GA-Gate09-ADR060-Evidencias-*.zip")):
        with zipfile.ZipFile(filename) as z:
            names=[x for x in z.namelist() if x.endswith(".json")]
            if len(names)!=1:
                raise ValueError("Exactly one original F072 observation per ZIP")
            row=json.loads(z.read(names[0]))
        if row.get("schema")!=A.SCHEMA or row.get("policy") not in A.POLICIES:
            raise ValueError("Invalid original ADR060 policy evidence")
        if row["policy"] in records:
            raise ValueError("Duplicate ADR060 policy archive")
        records[row["policy"]]=row
    if set(records)!=set(A.POLICIES):
        raise ValueError("Original baseline and GC variant both required")
    return records


def compare(records, fixture):
    if fixture not in A.FIXTURES or set(records)!=set(A.POLICIES):
        raise ValueError("Invalid F072 fixture or missing policy")
    baseline, variant = records["baseline"], records["gc_before_cache"]
    f=A.FIXTURES[fixture]
    for row in (baseline,variant):
        if (row["status"]!="PASS_ADR060_ORIGINAL_SOLVER_F072_AND_CACHE_LIVENESS_OBSERVED"
            or row["fixture"]!=fixture or row["workers"]!=4096
            or row["production_nodes"]!=f["production_nodes"]
            or row["demand_nodes"]!=f["demand_nodes"]
            or row["aggregated_production_groups"]!=f["groups"]
            or row["markets_node_item"]!=f["markets"]
            or row["original_independent_F072_status"]!="PASS_STATIC_F072_FIRST_ORDER_ONLY"
            or row["original_quartic_congestion_exponent"]!=4
            or row["production_approved"]
            or not row["gc_before_cache_is_not_ADR059_owned_release_first"]):
            raise AssertionError("Original solver F072 economic contract rejected")

    must_match=(
        "worker_ledger_sha256","initial_flow_sha256",
        "final_original_F072_flow_sha256",
        "original_independent_F072_status",
        "original_independent_F072_global_gap",
        "original_independent_F072_worst_group_gap",
        "original_solver_status",
        "original_work_counters",
        "original_generation_summary",
        "original_quartic_congestion_exponent",
        "original_budget_limits",
    )
    for field in must_match:
        if baseline[field]!=variant[field]:
            raise AssertionError("Original solver differs with GC schedule: "+field)
    for row in (baseline,variant):
        for field in (
            "original_independent_F072_global_gap",
            "original_independent_F072_worst_group_gap",
        ):
            if row[field] is None or not 0 <= row[field] <= A.TOL:
                raise AssertionError("Missing/invalid original independent F072 gap")

    base=baseline["cache_liveness_observations"]
    alt=variant["cache_liveness_observations"]
    if not (
        base["original_cache_type_returned_unchanged"]
        and alt["original_cache_type_returned_unchanged"]
        and base["R45_only_cache_factory_instrumented"]
        and alt["R45_only_cache_factory_instrumented"]
        and base["original_ADR042_independent_auditor_NOT_monkeypatched"]
        and alt["original_ADR042_independent_auditor_NOT_monkeypatched"]
        and base["explicit_gc_before_each_cache_construction"]==0
        and alt["explicit_gc_before_each_cache_construction"]
            ==alt["constructed_R45_route_cache_instances"]
    ):
        raise AssertionError("Instrumented R45 cache or GC policy not enforced")
    if (base["constructed_R45_route_cache_instances"]==0
        or base["constructed_R45_route_cache_instances"]
            !=alt["constructed_R45_route_cache_instances"]):
        raise AssertionError("Cache construction counts differ unexpectedly")

    events_a=base["observed_original_cache_construction_events"]
    events_b=alt["observed_original_cache_construction_events"]
    keys_a=[r["load_epoch_sha256_within_same_network"] for r in events_a]
    keys_b=[r["load_epoch_sha256_within_same_network"] for r in events_b]
    if keys_a!=keys_b:
        raise AssertionError("Original physical load epochs differ under GC schedule")

    a_rss=baseline["observed_solver_native_process_rss_highwater_after_bytes"]
    b_rss=variant["observed_solver_native_process_rss_highwater_after_bytes"]
    cpu_a=baseline["observed_solver_process_cpu_seconds"]
    cpu_b=variant["observed_solver_process_cpu_seconds"]
    return {
        "schema":SCHEMA,
        "status":"PASS_ADR060_ORIGINAL_F072_EQUIVALENCE_WITH_OBSERVED_CACHE_LIFETIMES",
        "fixture":fixture,
        "F072_original_independent_and_final_allocations_identical":True,
        "original_solver_work_counters_identical":True,
        "original_R45_cache_snapshot_sequence_identical":True,
        "baseline_R45_cache_constructions":base["constructed_R45_route_cache_instances"],
        "distinct_original_solver_edge_load_epochs":
            base["distinct_physical_edge_load_epochs"],
        "baseline_max_simultaneously_live_R45_caches":
            base["maximum_simultaneously_live_R45_caches"],
        "baseline_old_R45_caches_live_before_next_creation":
            base["maximum_old_R45_caches_live_before_next_creation"],
        "GC_policy_max_simultaneously_live_R45_caches":
            alt["maximum_simultaneously_live_R45_caches"],
        "GC_policy_old_R45_caches_live_before_next_creation":
            alt["maximum_old_R45_caches_live_before_next_creation"],
        "natural_original_R45_cache_release_before_next_create_observed":
            base["maximum_old_R45_caches_live_before_next_creation"]==0,
        "baseline_native_process_RSS_highwater_B":a_rss,
        "extra_GC_native_process_RSS_highwater_B":b_rss,
        "observed_RSS_highwater_delta_B_baseline_minus_extra_GC":
            None if a_rss is None or b_rss is None else a_rss-b_rss,
        "baseline_solver_process_CPU_s":cpu_a,
        "extra_GC_solver_process_CPU_s":cpu_b,
        "observed_CPU_delta_s_baseline_minus_extra_GC":round(cpu_a-cpu_b,5),
        "cross_job_RSS_CPU_differences_not_causal_proof":True,
        "no_ADR059_50_percent_peak_saving_claim_for_real_solver":True,
        "no_new_SLA_or_production_approval":True,
        "production_approved":False,
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--fixture",required=True,choices=tuple(A.FIXTURES))
    p.add_argument("--directory",required=True,type=Path)
    p.add_argument("--output",default=Path.cwd(),type=Path)
    args=p.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    result={"schema":SCHEMA,"fixture":args.fixture,
            "status":"FAILED_ADR060_ORIGINAL_F072_POLICY_EQUIVALENCE",
            "production_approved":False}
    try:
        result.update(compare(load_archives(args.directory),args.fixture))
    except Exception as exc:
        result["error"]=repr(exc)
        print("[ADR060-COMPARE] FAILED:",repr(exc),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    target=args.output/("GA-Gate09-ADR060-COMPARISON-"+args.fixture+"-"+stamp+".json")
    target.write_text(json.dumps(result,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    print("[ADR060-COMPARE] STATUS:",result["status"],flush=True)
    if result["status"].startswith("PASS_"):
        print("[ADR060-COMPARE] fixture={} F072_final_same={} "
              "cache_creations={} live_before_next={} peak_live={} "
              "rss_baseline_B={} rss_gc_B={} cpu_baseline_s={} cpu_gc_s={}".format(
                  args.fixture,
                  result["F072_original_independent_and_final_allocations_identical"],
                  result["baseline_R45_cache_constructions"],
                  result["baseline_old_R45_caches_live_before_next_creation"],
                  result["baseline_max_simultaneously_live_R45_caches"],
                  result["baseline_native_process_RSS_highwater_B"],
                  result["extra_GC_native_process_RSS_highwater_B"],
                  result["baseline_solver_process_CPU_s"],
                  result["extra_GC_solver_process_CPU_s"]),flush=True)
    return 0 if result["status"].startswith("PASS_") else 1


if __name__=="__main__":
    raise SystemExit(main())
