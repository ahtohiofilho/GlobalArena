#!/usr/bin/env python3
"""Global Arena ADR052: 8,192 workers across 5,762 Goldberg nodes.

Lab-only distinct gates:
1) deterministic worker/occupation/travel/conservation on 2,048 tiles;
2) full original ADR042 F072 certification on a small worker-coupled
   fixture and an EXPLORATORY 2,048-worker/12-hub network.

The physically denser 8,192-worker scenario is a CONSTRUCTION/
AGGREGATION stress, NOT a commercial equilibrium certified by F072.
No C#/Godot runtime or real periodic CPU scheduling exists here.
"""
from __future__ import annotations

import argparse
from datetime import datetime,timezone
import json
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"adr051_worker_block"/"ci"))
import run_adr051 as ADR51
from clearing_r5 import _flow_checksum
from population_scale import CONFIGS,build_population

SCHEMA="GA_GATE09_ADR052_PHYSICAL_WORKER_POPULATION_AGGREGATION_AND_F072_V1"
PILOT_NUMERICAL_BUDGET=ADR51.R47.Limits(
    max_h_evaluations=620,
    max_route_scans=1600,
    max_original_certificates=5,
    max_columns=440,
    max_generations=5,
    max_newton_steps=145,
    max_backtrack=10,
    max_krylov_products=26000,
    max_cg_iterations=150,
    max_block_coefficients=150000)
SMALL_NUMERICAL_BUDGET=ADR51.R47.Limits(
    max_h_evaluations=320,
    max_route_scans=450,
    max_original_certificates=5,
    max_columns=160,
    max_generations=4,
    max_newton_steps=95,
    max_backtrack=10,
    max_krylov_products=9500,
    max_cg_iterations=95,
    max_block_coefficients=24000)


def population_report(name,attempt_F072=False):
    if name not in CONFIGS:
        raise ValueError("Only registered original Goldberg populations")
    t0=perf_counter()
    net,flows,metadata=build_population(CONFIGS[name])
    build_ms=round((perf_counter()-t0)*1000,3)
    expected_hash=metadata["initial_real_flow_sha256"]
    workers=metadata.pop("source_workers")
    if len(workers)!=metadata["workers"]:
        raise AssertionError("Worker ledger count drift")
    metadata["physical_production_nodes"]=sorted(
        metadata["physical_production_nodes"])
    metadata["physical_demand_nodes"]=sorted(
        metadata["physical_demand_nodes"])
    result={
        "scenario":name,
        "construction_wall_ms_python":build_ms,
        "population_ledger":{
            **metadata,
            "production_nodes":metadata["production_nodes_count"],
            "demand_nodes":metadata["demand_nodes_count"],
            "aggregated_groups":len(net.groups),
            "priced_markets":len(net.markets),
        },
        "commercial_certificate_attempted":bool(attempt_F072),
        "original_F072_independent_pass":False,
        "final_F072_global_gap_if_pass":None,
        "final_F072_worst_group_gap_if_pass":None,
        "solver_status":"NOT_RUN_FOR_SPATIAL_STRESS",
        "solver_work":None,
        "solver_wall_ms_python":None,
        "provisional_not_committed":None,
        "no_worker_flow_or_market_input_mutated":True,
        "not_CSharp_Godot_runtime":True,
    }
    # The exact economic solver operates on the GROUPS and MARKETS
    # derived from workers, but workers continue to exist in the
    # population ledger. We do not create one market per worker.
    if attempt_F072:
        limits=(SMALL_NUMERICAL_BUDGET if name.startswith("unit_")
                else PILOT_NUMERICAL_BUDGET)
        output=ADR51.solve_chunked(net,flows,limits)
        passed=ADR51.is_certified(output)
        if output["status"].startswith("PASS") and not passed:
            raise AssertionError("Uncertified numerical PASS")
        if not passed and output["final_allocations"] is not None:
            raise AssertionError("NO-GO trial published non-certified flows")
        result.update({
            "original_F072_independent_pass":passed,
            "final_F072_global_gap_if_pass":(
                output["certificate"]["global_F072_gap"] if passed else None),
            "final_F072_worst_group_gap_if_pass":(
                output["certificate"]["worst_group_F072_gap"] if passed else None),
            "solver_status":output["status"],
            "solver_work":output["work"],
            "solver_wall_ms_python":output["elapsed_ms_observed_python"],
            "provisional_not_committed":output["provisional_last_snapshot"],
            "original_unchanged_quartic_congestion":(
                output["original_congestion_exponent"]==4),
            "original_independent_ADR042_certificate":(
                output["certificate"]["status"] if output["certificate"]
                else None),
            "numerical_work_limits":vars(limits),
        })
    if _flow_checksum(flows)!=expected_hash:
        raise AssertionError("Population source supply was mutated")
    if not metadata["production_is_subset_of_demand_nodes"]:
        raise AssertionError("Not all producers demand locally")
    if not metadata["travel_time_does_not_duplicate_demand"]:
        raise AssertionError("Travel duplicated worker demand")
    return result


def execute(profile):
    if profile not in ("smoke","ci"):
        raise ValueError("Unknown experiment profile")
    if ADR51.R49.ADR48.ADR42.R21.validate_manifest()!=16:
        raise AssertionError("Frozen R20 source fingerprint changed")
    rows=[]
    stages=[
        ("unit_162_nodes_128_workers",True),
    ]
    if profile=="ci":
        stages.extend([
            ("spatial_5762_nodes_8192_workers",False),
            ("pilot_5762_nodes_2048_workers",True),
        ])
    for name,attempt in stages:
        row=population_report(name,attempt)
        rows.append(row)
        pop=row["population_ledger"]
        print("[ADR052:{}] workers={} source_nodes={} demand_nodes={} "
              "groups={} market_pairs={} construction_ms={} "
              "F072_attempt={} F072_PASS={} status={} "
              "gap_global={} worst={} solver_ms={}".format(
                name,pop["workers"],pop["production_nodes"],
                pop["demand_nodes"],pop["aggregated_groups"],
                pop["priced_markets"],row["construction_wall_ms_python"],
                row["commercial_certificate_attempted"],
                row["original_F072_independent_pass"],
                row["solver_status"],row["final_F072_global_gap_if_pass"],
                row["final_F072_worst_group_gap_if_pass"],
                row["solver_wall_ms_python"]),flush=True)
    small=rows[0]["original_F072_independent_pass"]
    spatial=(profile=="smoke" or
             (rows[1]["population_ledger"]["workers"]==8192 and
              rows[1]["population_ledger"]["demand_nodes"]==2048 and
              rows[1]["population_ledger"]["production_nodes"]==512 and
              rows[1]["population_ledger"]["priced_markets"]==4096 and
              not rows[1]["commercial_certificate_attempted"]))
    pilot=(None if profile=="smoke" else
           rows[2]["original_F072_independent_pass"])
    status=(
        "NO_GO_MANDATORY_SMALL_WORKER_F072_OR_SPATIAL_LEDGER"
        if not (small and spatial) else
        "PASS_ADR052_WORKER_SPATIAL_AND_PILOT_F072"
        if pilot is True else
        "PASS_ADR052_LEDGER_WITH_PILOT_ECONOMIC_NO_GO"
        if pilot is False else
        "PASS_ADR052_SMALL_WORKER_F072_CONTRACT")
    return {
        "schema":SCHEMA,
        "status":status,
        "profile":profile,
        "mandatory_small_worker_F072_certified":small,
        "mandatory_8192_worker_spatial_ledger_valid":spatial,
        "exploratory_2048_worker_F072_certified":pilot,
        "scenarios":rows,
        "F072_original_and_quartic_unchanged":True,
        "all_demand_sourced_from_workers":True,
        "spatial_8192_worker_economy_not_solved":True,
        "no_claim_of_representative_population_density":True,
        "no_automatic_game_CSharp_scheduler":True,
        "F059_financial_ownership_not_ratified":True,
        "production_approved":False,
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile",choices=("smoke","ci"),default="ci")
    p.add_argument("--output",type=Path,default=Path.cwd())
    a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,
        "status":"FAILED_WORKER_POPULATION_CONTRACT",
        "platform":platform.platform(),
        "python":platform.python_version(),
        "utc":datetime.now(timezone.utc).isoformat(),
        "game_engine_untouched":True,
    }
    try:
        report.update(execute(a.profile))
    except Exception as e:
        report["status"]="FAILED_WORKER_POPULATION_CONTRACT"
        report["error"]=repr(e)
        report["traceback"]=traceback.format_exc()
        print("[ADR052] FAILED:",repr(e),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path=a.output/("GA-Gate09-ADR052-"+a.profile+"-"+stamp+".json")
    path.write_text(
        json.dumps(report,indent=2,sort_keys=True,ensure_ascii=False)+"\n",
        encoding="utf-8")
    archive=a.output/("GA-Gate09-ADR052-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(path,path.name)
    print("[ADR052] STATUS:",report["status"],flush=True)
    print("[ADR052] EVIDENCE:",archive,flush=True)
    # The pilot 2048 F072 is exploratory; an explicit failed mandatory
    # small-worker certificate / 8192 spatial ledger blocks this CI.
    return 1 if report["status"].startswith((
        "FAILED_","NO_GO_MANDATORY_")) else 0


if __name__=="__main__":
    raise SystemExit(main())
