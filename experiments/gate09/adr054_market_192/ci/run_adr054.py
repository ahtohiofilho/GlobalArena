#!/usr/bin/env python3
"""Global Arena Gate09 ADR054 — independently certify 192 worker demand nodes.

LAB ONLY. The original 48 and 96 demand node F072 equilibria are mandatory
regressions. The 192 node / 384 market F072 trial is exploratory until independently
certified on both CI operating systems and promoted in a separate commit.
No game runtime integration, economic rule changes, or financial F059 approval.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
from math import isfinite
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "adr053_market_coverage" / "ci"))
import run_adr053 as R53
from clearing_r5 import _flow_checksum

SCHEMA = "GA_GATE09_ADR054_WORKER_192_INDEPENDENT_F072_EXPERIMENT_V1"
CANDIDATE = "spatial_192_demand_nodes"
REFERENCE = ("baseline_48_demand_nodes", "candidate_96_demand_nodes")
SPATIAL_ONLY = "spatial_2048_demand_nodes"
STAGES = {
    "smoke": ((REFERENCE[0], True), (REFERENCE[1], True)),
    "ci": ((REFERENCE[0], True), (REFERENCE[1], True),
           (CANDIDATE, True), (SPATIAL_ONLY, False)),
}
# Algorithmic work units, NOT a wall-clock SLA or a C#/Godot frame budget.
# Fixed before the first candidate run; do not silently enlarge on NO-GO.
LIMITS_192 = R53.R52.ADR51.R47.Limits(
    max_h_evaluations=1100,
    max_route_scans=3100,
    max_original_certificates=7,
    max_columns=1500,
    max_generations=6,
    max_newton_steps=185,
    max_backtrack=12,
    max_krylov_products=60000,
    max_cg_iterations=210,
    max_block_coefficients=250000,
)


def registered_budget(name):
    if name in REFERENCE:
        return R53.registered_budget(name)
    if name == CANDIDATE:
        return LIMITS_192
    raise ValueError("2048 demand node ledger has no F072 solving budget")


def solve_candidate_192():
    """Reuse untouched population, solver and ORIGINAL independent certificate."""
    # First validate the existing ADR053 spatial contract, WITHOUT solving it.
    row = R53.run_stage(CANDIDATE, False)
    if (row["demand_nodes"] != 192 or row["production_nodes"] != 48
        or row["aggregated_production_groups"] != 96
        or row["worker_sourced_markets_node_item"] != 384
        or row["physical_workers"] != 4096
        or row["physical_goldberg_nodes"] != 5762
        or row["F072_attempted"]):
        raise AssertionError("ADR053 physical 192-node ledger drift")

    t0 = perf_counter()
    network, flows, source = R53.R52.build_population(R53.CONFIGS[CANDIDATE])
    build_ms = (perf_counter() - t0) * 1000.0
    original_flow_hash = _flow_checksum(flows)
    workers = source.pop("source_workers")
    if (len(workers) != 4096
        or source["worker_ledger_sha256"] != row["worker_ledger_sha256"]
        or original_flow_hash != row["initial_physical_flow_sha256"]
        or not source["physical_production_nodes"].issubset(
            source["physical_demand_nodes"])
        or len(network.groups) != 96 or len(network.markets) != 384):
        raise AssertionError("Rebuilt physical candidate is inconsistent")

    result = R53.R52.ADR51.solve_chunked(network, flows, registered_budget(CANDIDATE))
    passed = R53.approve_original_F072(result)
    if result["status"].startswith("PASS_") and not passed:
        raise AssertionError("Numerical PASS without independent original F072")
    if (not passed and (result.get("final_flow_sha256") is not None
                       or result.get("final_allocations") is not None)):
        raise AssertionError("NO-GO exposed uncertified final allocations")
    if result.get("original_congestion_exponent") != 4:
        raise AssertionError("Quartic congestion unexpectedly changed")
    if _flow_checksum(flows) != original_flow_hash:
        raise AssertionError("Original physical supply was mutated")

    cert = result.get("certificate")
    row.update({
        "population_rebuild_python_ms": round(build_ms, 3),
        "F072_attempted": True,
        "F072_independent_certified": passed,
        "original_F072_global_FINAL_if_pass":
            cert["global_F072_gap"] if passed else None,
        "original_F072_worst_group_FINAL_if_pass":
            cert["worst_group_F072_gap"] if passed else None,
        "last_audited_global_NOT_final_on_NO_GO":
            cert["global_F072_gap"] if cert else None,
        "last_audited_worst_NOT_final_on_NO_GO":
            cert["worst_group_F072_gap"] if cert else None,
        "solver_status": result["status"],
        "solver_elapsed_python_ms": result["elapsed_ms_observed_python"],
        "solver_work": result["work"],
        "solver_generation_summary": result["generations"],
        "provisional_not_authoritative": result["provisional_last_snapshot"],
        "original_congestion_exponent_unchanged": True,
        "budget_limits": vars(registered_budget(CANDIDATE)),
        "F072_certificate_original_only": cert["status"] if cert else None,
    })
    return row


def classify(profile, rows):
    if profile not in STAGES:
        raise ValueError("Unknown ADR054 profile")
    if tuple(r["name"] for r in rows) != tuple(n for n, _ in STAGES[profile]):
        raise ValueError("Missing or reordered independent stage evidence")
    for i, name in enumerate(REFERENCE):
        if type(rows[i]["F072_independent_certified"]) is not bool:
            raise ValueError("Mandatory reference certificate must be boolean")
        if not rows[i]["F072_attempted"]:
            return "NO_GO_MANDATORY_REFERENCE_F072_NOT_ATTEMPTED", False
        if not rows[i]["F072_independent_certified"]:
            return ("NO_GO_MANDATORY_48_F072_REGRESSION" if i == 0
                    else "NO_GO_MANDATORY_96_F072_REGRESSION"), False

    if profile == "smoke":
        return "PASS_ADR054_48_AND_96_F072_REGRESSIONS", True

    candidate, extreme = rows[2], rows[3]
    if (candidate["demand_nodes"] != 192
        or candidate["physical_workers"] != 4096
        or candidate["production_nodes"] != 48
        or candidate["aggregated_production_groups"] != 96
        or candidate["worker_sourced_markets_node_item"] != 384
        or not candidate["F072_attempted"]
        or not candidate["production_nodes_subset_demand_nodes"]
        or not candidate["worker_demand_conserved_without_extra_travel_demand"]
        or candidate["original_congestion_exponent_unchanged"] is not True
        or extreme["demand_nodes"] != 2048
        or extreme["physical_workers"] != 8192
        or extreme["worker_sourced_markets_node_item"] != 4096
        or extreme["F072_attempted"]
        or not extreme["production_nodes_subset_demand_nodes"]
        or not extreme["worker_demand_conserved_without_extra_travel_demand"]):
        return "NO_GO_MANDATORY_PHYSICAL_OR_QUARTIC_CONTRACT", False
    if type(candidate["F072_independent_certified"]) is not bool:
        raise ValueError("192-node F072 result must be an explicit boolean")
    if candidate["F072_independent_certified"]:
        gaps = (candidate["original_F072_global_FINAL_if_pass"],
                candidate["original_F072_worst_group_FINAL_if_pass"])
        if (any(x is None or not isfinite(x) or x > R53.TOLERANCE for x in gaps)
            or candidate["F072_certificate_original_only"]
                    != "PASS_STATIC_F072_FIRST_ORDER_ONLY"):
            return "NO_GO_UNVERIFIED_192_ORIGINAL_F072_CLAIM", False
        return "PASS_EXPLORATORY_ADR054_192_ORIGINAL_F072_CERTIFIED", True
    if (candidate["original_F072_global_FINAL_if_pass"] is not None
        or candidate["original_F072_worst_group_FINAL_if_pass"] is not None):
        return "NO_GO_UNCERTIFIED_192_FINAL_RESULT_LEAK", False
    # Not an economic approval; CI can stay green ONLY if mandatory
    # 48/96 F072 regressions and worker spatial invariants passed.
    return "NO_GO_EXPLORATORY_ADR054_192_F072_NOT_CERTIFIED", True


def exercise(profile):
    if profile not in STAGES:
        raise ValueError("Unknown ADR054 profile")
    if R53.R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest() != 16:
        raise AssertionError("Original R20 manifest is not immutable")
    rows = []
    for name, attempted in STAGES[profile]:
        row = solve_candidate_192() if name == CANDIDATE else R53.run_stage(
            name, attempted)
        rows.append(row)
        work = row["solver_work"] or {}
        print("[ADR054:{}] workers={} production={} demand={} markets={} "
              "F072={} certified={} status={} global={} worst={} H={} "
              "Krylov={} scans={} solver_python_ms={}".format(
            name, row["physical_workers"], row["production_nodes"],
            row["demand_nodes"], row["worker_sourced_markets_node_item"],
            row["F072_attempted"], row["F072_independent_certified"],
            row["solver_status"], row["original_F072_global_FINAL_if_pass"],
            row["original_F072_worst_group_FINAL_if_pass"],
            work.get("H_evaluations_including_rejected_trials"),
            work.get("matrixfree_Jv_and_Jtv_products"),
            work.get("separate_shortest_path_scans"),
            row["solver_elapsed_python_ms"]), flush=True)
    status, mandatory = classify(profile, rows)
    return {
        "schema": SCHEMA,
        "profile": profile,
        "status": status,
        "mandatory_regressions_and_contracts_passed": mandatory,
        "candidate_192_F072_exploratory_certified":
            rows[2]["F072_independent_certified"] if profile == "ci" else None,
        "stages": rows,
        "workers_sole_source_of_market_demands": True,
        "original_quartic_and_price_functions_untouched": True,
        "higher_2048_demand_node_scenario_not_solved": True,
        "CSharp_Godot_unmodified": True,
        "F059_not_ratified": True,
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
        "status": "FAILED_ADR054_CONTRACT",
        "production_approved": False,
    }
    try:
        report.update(exercise(args.profile))
    except Exception as exc:
        report["status"] = "FAILED_ADR054_CONTRACT"
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[ADR054] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output = args.output / ("GA-Gate09-ADR054-" + args.profile + "-" + stamp + ".json")
    output.write_text(json.dumps(report, indent=2, sort_keys=True,
                                 ensure_ascii=False) + "\n", encoding="utf-8")
    archive = args.output / ("GA-Gate09-ADR054-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as zipout:
        zipout.write(output, output.name)
    print("[ADR054] STATUS:", report["status"], flush=True)
    print("[ADR054] EVIDENCE:", archive, flush=True)
    # Exploratory NO-GO does not hide a mandatory-regression failure.
    return 0 if report.get("mandatory_regressions_and_contracts_passed") else 1


if __name__ == "__main__":
    raise SystemExit(main())
