#!/usr/bin/env python3
"""Global Arena Gate09 ADR053 — grow worker-funded DEMAND-node coverage.

Commercial comparisons control topology (5,762 Goldberg nodes) and
population (2,048 workers) while expanding physically occupied markets:
  12 hubs -> 48 demand tiles / 96 node-commodity markets (mandatory);
  24 hubs -> 96 demand tiles / 192 markets (new exploratory F072 trial).
Separate spatial-construction probes cover 192 and 2,048 demand tiles.
No commercial equilibrium is claimed for the unsolved spatial probes.

Only worker-derived supply/demand from ADR052, original F072 economics,
ADR042 certificate and ADR051 experimental SPD solver are reused. This
is NOT an interruptible C#/Godot economic worker scheduler.
"""
from __future__ import annotations

import argparse
from datetime import datetime,timezone
import json
from math import isfinite
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"adr052_population_scale"/"ci"))
import run_adr052 as R52
from population_scale import PopulationConfig
from clearing_r5 import _flow_checksum

SCHEMA="GA_GATE09_ADR053_WORKER_MARKET_COVERAGE_AND_F072_REGRESSION_V1"
TOLERANCE=1e-4
CONFIGS={
    "baseline_48_demand_nodes":PopulationConfig(24,12,2048),
    "candidate_96_demand_nodes":PopulationConfig(24,24,2048),
    "spatial_192_demand_nodes":PopulationConfig(24,48,4096),
    "spatial_2048_demand_nodes":PopulationConfig(24,512,8192),
}
STAGES={
    "smoke":(("baseline_48_demand_nodes",True),),
    "ci":( ("baseline_48_demand_nodes",True),
           ("candidate_96_demand_nodes",True),
           ("spatial_192_demand_nodes",False),
           ("spatial_2048_demand_nodes",False) ),
}
# Named units are algorithmic hard stops, NOT milliseconds or C# CPU SLAs.
LIMITS_96=R52.ADR51.R47.Limits(
    max_h_evaluations=760,
    max_route_scans=1900,
    max_original_certificates=6,
    max_columns=750,
    max_generations=5,
    max_newton_steps=145,
    max_backtrack=10,
    max_krylov_products=34000,
    max_cg_iterations=145,
    max_block_coefficients=145000,
)


def registered_budget(name):
    if name=="baseline_48_demand_nodes":
        return R52.PILOT_NUMERICAL_BUDGET
    if name=="candidate_96_demand_nodes":
        return LIMITS_96
    raise ValueError("No F072 solve allocated to spatial-only stage")


def approve_original_F072(result):
    cert=result.get("certificate")
    return bool(
        R52.ADR51.is_certified(result)
        and result.get("final_flow_sha256") is not None
        and result.get("final_allocations") is not None
        and cert and cert.get("status")=="PASS_STATIC_F072_FIRST_ORDER_ONLY"
        and isfinite(cert["global_F072_gap"])
        and isfinite(cert["worst_group_F072_gap"])
        and cert["global_F072_gap"]<=TOLERANCE
        and cert["worst_group_F072_gap"]<=TOLERANCE
        and result.get("original_congestion_exponent")==4
    )


def run_stage(name,attempt_F072):
    if name not in CONFIGS:
        raise ValueError("Unknown physical-worker density stage")
    config=CONFIGS[name]
    begin=perf_counter()
    network,flows,ledger=R52.build_population(config)
    initial_hash=_flow_checksum(flows)
    construction_ms=(perf_counter()-begin)*1000.
    workers=ledger.pop("source_workers")
    if len(workers)!=ledger["workers"] or ledger["geometry_nodes"]!=5762:
        raise AssertionError("Physical population inventory mismatch")
    if not ledger["physical_production_nodes"].issubset(
        ledger["physical_demand_nodes"]):
        raise AssertionError("Worker demand must cover producer tiles")
    production_sites=ledger["production_nodes_count"]
    demand_sites=ledger["demand_nodes_count"]
    expected_sites=4*config.hubs
    if (production_sites!=config.hubs or demand_sites!=expected_sites
        or len(network.groups)!=2*config.hubs
        or len(network.markets)!=2*expected_sites):
        raise AssertionError("Occupancy and market aggregation drift")
    if sum(ledger["activities"].values())!=config.workers:
        raise AssertionError("Worker activities not fully accounted")
    # Materialize explicit entry WITHOUT huge worker records or maps.
    summary={
        "name":name,
        "physical_goldberg_nodes":ledger["geometry_nodes"],
        "goldberg_edges":ledger["geometry_edges"],
        "physical_workers":ledger["workers"],
        "producing_workers":ledger["producing_workers"],
        "nonproducing_workers":ledger["nonproducing_workers"],
        "activities":ledger["activities"],
        "production_nodes":production_sites,
        "demand_nodes":demand_sites,
        "physical_nodes_without_demand":5762-demand_sites,
        "demand_node_coverage_ratio":demand_sites/5762.,
        "aggregated_production_groups":len(network.groups),
        "worker_sourced_markets_node_item":len(network.markets),
        "real_funded_initial_columns":len(flows),
        "worker_ledger_sha256":ledger["worker_ledger_sha256"],
        "initial_physical_flow_sha256":initial_hash,
        "original_demands_by_item":ledger["market_demand_by_item"],
        "population_build_python_ms":round(construction_ms,3),
        "worker_demand_conserved_without_extra_travel_demand":True,
        "production_nodes_subset_demand_nodes":True,
        "F072_attempted":attempt_F072,
        "F072_independent_certified":False,
        "original_F072_global_FINAL_if_pass":None,
        "original_F072_worst_group_FINAL_if_pass":None,
        "solver_status":"NOT_RUN_SPATIAL_LEDGER_ONLY",
        "solver_elapsed_python_ms":None,
        "solver_work":None,
        "solver_generation_summary":None,
        "provisional_not_authoritative":None,
        "no_game_snapshot_committed":True,
        "CSharp_Godot_untouched":True,
        "F059_not_ratified":True,
    }
    if attempt_F072:
        limits=registered_budget(name)
        result=R52.ADR51.solve_chunked(network,flows,limits)
        passed=approve_original_F072(result)
        if result["status"].startswith("PASS_") and not passed:
            raise AssertionError("False economic PASS without original certificate")
        if not passed and (result["final_flow_sha256"] is not None or
                           result["final_allocations"] is not None):
            raise AssertionError("Uncertified allocation disclosed as final")
        certificate=result.get("certificate")
        summary.update({
            "F072_independent_certified":passed,
            "original_F072_global_FINAL_if_pass":
                certificate["global_F072_gap"] if passed else None,
            "original_F072_worst_group_FINAL_if_pass":
                certificate["worst_group_F072_gap"] if passed else None,
            "last_audited_global_NOT_final_on_NO_GO":
                certificate["global_F072_gap"] if certificate else None,
            "last_audited_worst_NOT_final_on_NO_GO":
                certificate["worst_group_F072_gap"] if certificate else None,
            "solver_status":result["status"],
            "solver_elapsed_python_ms":
                result["elapsed_ms_observed_python"],
            "solver_work":result["work"],
            "solver_generation_summary":result["generations"],
            "provisional_not_authoritative":
                result["provisional_last_snapshot"],
            "original_congestion_exponent_unchanged":
                result["original_congestion_exponent"]==4,
            "budget_limits":vars(limits),
        })
    if _flow_checksum(flows)!=initial_hash:
        raise AssertionError("Original worker supply mutated by the solver")
    return summary


def classify(profile,entries):
    if profile not in STAGES:
        raise ValueError("Unregistered cover-growth profile")
    names=tuple(x["name"] for x in entries)
    expected=tuple(name for name,attempt in STAGES[profile])
    if names!=expected:
        raise ValueError("Incomplete or reordered stage evidence")
    reference=entries[0]["F072_independent_certified"]
    if type(reference) is not bool:
        raise ValueError("Reference F072 must be explicit boolean")
    if not reference:
        return "NO_GO_MANDATORY_48_DEMAND_NODE_F072_REGRESSION",False
    if profile=="smoke":
        return "PASS_ADR053_MANDATORY_48_NODE_F072_REFERENCE",True
    candidate=entries[1]["F072_independent_certified"]
    if type(candidate) is not bool:
        raise ValueError("Candidate F072 must be explicit boolean")
    # Physical 96/192/2048-node ledgers must pass regardless of F072
    # performance. Stage 192 and 2048 MUST NEVER be labelled F072 PASS.
    if (not all(x["production_nodes_subset_demand_nodes"]
                and x["worker_demand_conserved_without_extra_travel_demand"]
                for x in entries)
        or entries[1]["demand_nodes"]!=96
        or entries[2]["demand_nodes"]!=192
        or entries[3]["demand_nodes"]!=2048
        or entries[2]["F072_attempted"] or entries[3]["F072_attempted"]):
        return "NO_GO_MANDATORY_PHYSICAL_DEMAND_COVERAGE",False
    # The first independent CI certified 96 demand nodes on BOTH OS.
    # Promote this new commercial equilibrium to a mandatory regression.
    # An uncertified candidate may never leave the gate green.
    if not candidate:
        return "NO_GO_MANDATORY_96_DEMAND_NODE_F072_REGRESSION",False
    return "PASS_ADR053_96_DEMAND_NODE_ORIGINAL_F072_CERTIFIED",True


def exercise(profile):
    if profile not in STAGES:
        raise ValueError("Unregistered population benchmark profile")
    if R52.ADR51.R49.ADR48.ADR42.R21.validate_manifest()!=16:
        raise AssertionError("Original R20 SHA manifest changed")
    rows=[]
    for name,attempt in STAGES[profile]:
        row=run_stage(name,attempt)
        rows.append(row)
        work=row["solver_work"] or {}
        print("[ADR053:{}] workers={} producing_nodes={} demand_nodes={} "
              "groups={} markets={} F072_attempt={} F072_GO={} "
              "global={} worst={} status={} H={} Krylov={} scans={} "
              "solver_ms={} build_ms={}".format(
                name,row["physical_workers"],row["production_nodes"],
                row["demand_nodes"],row["aggregated_production_groups"],
                row["worker_sourced_markets_node_item"],attempt,
                row["F072_independent_certified"],
                row["original_F072_global_FINAL_if_pass"],
                row["original_F072_worst_group_FINAL_if_pass"],
                row["solver_status"],
                work.get("H_evaluations_including_rejected_trials"),
                work.get("matrixfree_Jv_and_Jtv_products"),
                work.get("separate_shortest_path_scans"),
                row["solver_elapsed_python_ms"],
                row["population_build_python_ms"]),flush=True)
    status,mandatory=classify(profile,rows)
    return {
        "schema":SCHEMA,
        "profile":profile,
        "status":status,
        "mandatory_regression_passed":mandatory,
        "exploratory_96_node_F072_certified":(
            rows[1]["F072_independent_certified"] if profile=="ci" else None),
        "stages":rows,
        "original_F072_and_quartic_unchanged":True,
        "workers_sole_source_of_demands":True,
        "not_equilibrium_claim_for_192_or_2048_demand_nodes":True,
        "no_CSharp_Godot_or_periodic_solver":True,
        "production_approved":False,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile",choices=tuple(STAGES),default="ci")
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),
        "python":platform.python_version(),
        "status":"FAILED_ADR053_CONTRACT",
        "production_approved":False,
    }
    try:
        report.update(exercise(args.profile))
    except Exception as exc:
        report["status"]="FAILED_ADR053_CONTRACT"
        report["error"]=repr(exc)
        report["traceback"]=traceback.format_exc()
        print("[ADR053] FAILED:",repr(exc),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path=args.output/("GA-Gate09-ADR053-"+args.profile+"-"+stamp+".json")
    path.write_text(json.dumps(report,indent=2,sort_keys=True,
                               ensure_ascii=False)+"\n",encoding="utf-8")
    archive=args.output/("GA-Gate09-ADR053-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(path,path.name)
    print("[ADR053] STATUS:",report["status"],flush=True)
    print("[ADR053] EVIDENCE:",archive,flush=True)
    return 0 if (report["status"]!="FAILED_ADR053_CONTRACT" and
                 report.get("mandatory_regression_passed",False)) else 1


if __name__=="__main__":
    raise SystemExit(main())
