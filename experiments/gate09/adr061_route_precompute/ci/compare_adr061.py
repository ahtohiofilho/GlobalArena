#!/usr/bin/env python3
"""ADR061 single-shot ROI gate: exact F072 parity plus measured CPU speed.

Semantics are mandatory and fail closed. Speed in separate CI runners is
only an indicative screen; the ROI decision is NOT C#/Godot authorization.
Declare NO_GO explicitly unless 384 candidate gains >=20% CPU on BOTH
OS with acceptable memory footprint; avoid further microprofiler loops.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_adr061 as A

SCHEMA = "GA_GATE09_ADR061_ORIGINAL_F072_CPU_ROI_GATE_V1"
MIN_INDICATIVE_CPU_GAIN_384 = 0.20
MAX_INDICATIVE_RSS_INCREASE = 0.10


def read_evidence(directory):
    records = {}
    for path in directory.glob("GA-Gate09-ADR061-Evidencias-*.zip"):
        with zipfile.ZipFile(path) as archive:
            members = [name for name in archive.namelist() if name.endswith(".json")]
            if len(members) != 1:
                raise ValueError("One JSON expected per original F072 artifact")
            item = json.loads(archive.read(members[0]))
        if item.get("schema") != A.SCHEMA or item.get("policy") not in A.POLICIES:
            raise ValueError("Unrecognized F072 policy artifact")
        if item["policy"] in records:
            raise ValueError("Duplicate policy evidence")
        records[item["policy"]] = item
    if set(records) != set(A.POLICIES):
        raise ValueError("Need baseline and precompute on same fixture/OS/HEAD")
    return records


def compare(rows, fixture):
    if fixture not in A.FIXTURES or set(rows) != set(A.POLICIES):
        raise ValueError("Unknown original fixture or incomplete policies")
    base, trial = rows["baseline"], rows["precompute"]
    fixed = A.FIXTURES[fixture]
    for row in (base, trial):
        if (row.get("status") !=
                "PASS_ADR061_ORIGINAL_F072_WITH_UNPATCHED_POST_CERTIFICATION"
            or row.get("fixture") != fixture
            or row.get("workers") != fixed["workers"]
            or row.get("demand_nodes") != fixed["demand_nodes"]
            or row.get("production_nodes") != fixed["production_nodes"]
            or row.get("groups") != fixed["groups"]
            or row.get("markets") != fixed["markets"]
            or row.get("production_approved")
            or row.get("original_static_F072_certificate")
                != "PASS_STATIC_F072_FIRST_ORDER_ONLY"
            or row.get("original_quartic_exponent") != 4
            or not row.get("original_independent_ADR042_FULL_post_certificate")
            or not row.get("R13_path_audit_restored_before_original_post_certificate")):
            raise AssertionError("Unpatched ORIGINAL F072/physical contract missing")
    must_identical = (
        "worker_ledger_sha256",
        "initial_flow_sha256",
        "original_final_F072_flow_sha256",
        "original_static_F072_certificate",
        "original_global_gap",
        "original_worst_group_gap",
        "original_work_counters",
        "original_generations",
        "original_budget_limits",
        "original_quartic_exponent",
    )
    for key in must_identical:
        if base[key] != trial[key]:
            raise AssertionError("Original F072 economic/numerical drift: " + key)
    if any(base[k] is None or base[k] > A.TOL or base[k] < 0 for k in
           ("original_global_gap", "original_worst_group_gap")):
        raise AssertionError("Original F072 certificate has unacceptable gaps")
    instrument = trial.get("experimental_route_counters")
    if (not instrument or
        instrument["weight_table_reuse_hits"] <= 0 or
        instrument["R13_path_audit_calls"] <= 0):
        raise AssertionError("No real R13 weight-table reuse measured")
    base_cpu, trial_cpu = (base["observed_solver_cpu_s"],
                           trial["observed_solver_cpu_s"])
    if base_cpu <= 0 or trial_cpu <= 0:
        raise AssertionError("Non-positive solver CPU measurement")
    ratio = (base_cpu - trial_cpu) / base_cpu
    rss_base = base["native_process_rss_after"][0]
    rss_trial = trial["native_process_rss_after"][0]
    if not rss_base or not rss_trial:
        raise AssertionError("Missing native process RSS measurement")
    memory = (rss_trial - rss_base) / rss_base
    # A single run can screen a candidate but cannot prove robust speed.
    candidate = (ratio >= MIN_INDICATIVE_CPU_GAIN_384
                 and memory <= MAX_INDICATIVE_RSS_INCREASE)
    return {
        "schema": SCHEMA,
        "fixture": fixture,
        "status": (
            "PASS_ADR061_ORIGINAL_F072_PARITY_AND_INDICATIVE_CPU_ROI"
            if candidate else
            "NO_GO_ADR061_CPU_ROI_NOT_DEMONSTRATED"),
        "F072_original_post_certificate_identical": True,
        "original_final_allocation_and_numerical_work_identical": True,
        "R13_route_weight_reuse_hits": instrument["weight_table_reuse_hits"],
        "baseline_original_solver_cpu_s": base_cpu,
        "precomputed_original_solver_cpu_s": trial_cpu,
        "observed_cpu_gain_fraction_one_run": round(ratio, 6),
        "baseline_process_lifetime_peak_rss_bytes": rss_base,
        "precomputed_process_lifetime_peak_rss_bytes": rss_trial,
        "observed_rss_increase_fraction_one_run": round(memory, 6),
        "indicative_min_cpu_gain_threshold": MIN_INDICATIVE_CPU_GAIN_384,
        "max_indicative_RSS_increase": MAX_INDICATIVE_RSS_INCREASE,
        "indicative_single_run_not_causal_and_not_game_runtime": True,
        "no_additional_microprofiling_if_ROI_not_demonstrated": True,
        "production_approved": False,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--fixture", required=True, choices=tuple(A.FIXTURES))
    p.add_argument("--directory", type=Path, required=True)
    p.add_argument("--output", type=Path, default=Path.cwd())
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    output = {
        "schema": SCHEMA,
        "status": "FAILED_ADR061_ORIGINAL_F072_PARITY",
        "fixture": args.fixture,
        "production_approved": False,
    }
    try:
        output.update(compare(read_evidence(args.directory), args.fixture))
    except Exception as exc:
        output["error"] = repr(exc)
        print("[ADR061-COMPARE] FAILED:", repr(exc), flush=True)
    path = args.output / ("GA-Gate09-ADR061-COMPARISON-" + args.fixture + "-" +
                          datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S") + ".json")
    path.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n",
                    encoding="utf-8")
    print("[ADR061-COMPARE] STATUS:", output["status"], flush=True)
    if "observed_cpu_gain_fraction_one_run" in output:
        print("[ADR061-COMPARE] fixture={} baseline_CPU_s={} candidate_CPU_s={} "
              "gain={} baseline_RSS={} candidate_RSS={} weight_hits={}".format(
                  args.fixture, output["baseline_original_solver_cpu_s"],
                  output["precomputed_original_solver_cpu_s"],
                  output["observed_cpu_gain_fraction_one_run"],
                  output["baseline_process_lifetime_peak_rss_bytes"],
                  output["precomputed_process_lifetime_peak_rss_bytes"],
                  output["R13_route_weight_reuse_hits"]), flush=True)
    # Underwhelming speed is an HONEST NO_GO, not a false claim of failure
    # of the original economy. Any semantic divergence is CI red.
    return 1 if output["status"].startswith("FAILED_") else 0


if __name__ == "__main__":
    raise SystemExit(main())
