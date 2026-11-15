#!/usr/bin/env python3
"""GA Gate09 ADR-049: 5,762-node geography and max-R13 density gate.

Only the existing, independently verified ADR047 block-MCP solver
is invoked. This driver changes geographic scale and (in a separate
optional profile) economic density of deterministic R13 scenarios.
P=sqrt(D/Q), quartic edge cost, tariffs, ownership, full supply,
R13/R14 and F084F remain identical.

NOT a C# engine benchmark, interruptible solver, Godot scheduler, or
production integration. Incomplete outcomes are explicitly NO-GO.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
from hashlib import sha256
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"adr048_density_budget"/"ci"))
import run_adr048 as ADR48
R47=ADR48.ADR47
R45=ADR48.R45
R13=ADR48.R13Scenario
construct_scenario=ADR48.construct_scenario
_flow_checksum=ADR48._flow_checksum

SCHEMA="GA_GATE09_ADR049_GEOGRAPHY_AND_HIGH_DENSITY_SCALE_V1"
F072_TOLERANCE=1e-4
CASES={
    "geography_5762_144_48":ADR48.FIXTURES["geography_5762_144_48"],
    "maximum_R13_density_2562_240_120":R13(
        m=16, products=10, markets_per_product=12,
        groups_per_product=24, civilizations=6,
        iterations=0, exponent=4),
}
PROFILES={
    "geography":("geography_5762_144_48",),
    "maximum_density":("maximum_R13_density_2562_240_120",),
}
BUDGETS={
    # Same as ADR048 144-producer case: control topological growth alone.
    "geography_5762_144_48":R47.Limits(
        max_h_evaluations=820,max_route_scans=4300,
        max_original_certificates=6,max_columns=1150,
        max_generations=6,max_newton_steps=175,
        max_backtrack=10,max_krylov_products=56000,
        max_cg_iterations=160,max_block_coefficients=245000),
    # A NEW research ceiling for a 240-group/120-market stress
    # experiment. This is NOT evidence of performance at 1000+ agents.
    "maximum_R13_density_2562_240_120":R47.Limits(
        max_h_evaluations=980,max_route_scans=6500,
        max_original_certificates=6,max_columns=1600,
        max_generations=6,max_newton_steps=185,
        max_backtrack=10,max_krylov_products=58000,
        max_cg_iterations=180,max_block_coefficients=248000),
}


def physical_properties(net,flows,cfg):
    """Ground the case in R13 physical sources and deterministic topology."""
    nq=net.snapshots(flows)[1]
    if len(net.edges)!=30*cfg.m*cfg.m:
        raise AssertionError("Goldberg edge count changed")
    if len(net.groups)!=cfg.products*cfg.groups_per_product:
        raise AssertionError("Producer count changed")
    if len(net.markets)!=cfg.products*cfg.markets_per_product:
        raise AssertionError("Market count changed")
    if len(flows)!=len(net.groups):
        raise AssertionError("One real initially funded route per producer")
    if set(nq)!=set(net.markets) or any(q<=0 for q in nq.values()):
        raise AssertionError("A reachable market has no initial real sales")
    if any(m.demand<=0 for m in net.markets.values()):
        raise AssertionError("Market demand must be physically positive")
    if cfg.exponent!=4:
        raise AssertionError("Quartic congestion changed")
    for c in flows:
        net.validate_column(c)
    return {
        "nodes":10*cfg.m*cfg.m+2,
        "edges":len(net.edges),
        "producer_groups":len(net.groups),
        "markets":len(net.markets),
        "commodities":cfg.products,
        "civilizations":cfg.civilizations,
        "initial_funded_columns":len(flows),
        "canonical_destination_options":
            cfg.products*cfg.groups_per_product*cfg.markets_per_product,
        "mean_compatible_markets_per_producer":cfg.markets_per_product,
    }


def approve_original_F072(out):
    """Only original independent ADR042 has authority for PASS."""
    cert=out.get("certificate")
    return bool(
        out["status"].startswith("PASS_")
        and cert is not None
        and cert.get("status")=="PASS_STATIC_F072_FIRST_ORDER_ONLY"
        and out["final_flow_sha256"] is not None
        and out["final_allocations"] is not None
        and cert.get("global_F072_gap") is not None
        and cert.get("worst_group_F072_gap") is not None
        and math.isfinite(cert["global_F072_gap"])
        and math.isfinite(cert["worst_group_F072_gap"])
        and cert["global_F072_gap"]<=F072_TOLERANCE
        and cert["worst_group_F072_gap"]<=F072_TOLERANCE
        and out["original_congestion_exponent"]==4
        and out["input_unmodified"]
    )


def execute(name):
    if name not in CASES:
        raise ValueError("Unregistered scale configuration")
    cfg=CASES[name]
    limits=BUDGETS[name]
    begin=perf_counter()
    net,flows=construct_scenario(cfg)
    fixture_ms=(perf_counter()-begin)*1000
    properties=physical_properties(net,flows,cfg)
    source_hash=_flow_checksum(flows)
    source_fingerprint=ADR48._economics_fingerprint(net)
    out=R47.solve_block(net,flows,limits=limits,restricted=True)
    certified=approve_original_F072(out)
    if out["status"].startswith("PASS_") and not certified:
        raise AssertionError("False economic PASS not accepted")
    if not certified and (out["final_flow_sha256"] is not None
                          or out["final_allocations"] is not None):
        raise AssertionError("Uncertified flows must not be published")
    if _flow_checksum(flows)!=source_hash:
        raise AssertionError("Original economic input flows modified")
    if ADR48._economics_fingerprint(net)!=source_fingerprint:
        raise AssertionError("Original R13 economic parameters changed")
    cert=out["certificate"]
    return {
        "name":name,
        **properties,
        "numerical_status":out["status"],
        "original_F072_independent_certified":certified,
        "original_F072_global_gap_final_if_pass":
            cert["global_F072_gap"] if certified else None,
        "original_F072_worst_group_gap_final_if_pass":
            cert["worst_group_F072_gap"] if certified else None,
        "last_audited_original_F072_gap_NOT_necessarily_final":
            cert["global_F072_gap"] if cert else None,
        "last_audited_original_worst_group_NOT_necessarily_final":
            cert["worst_group_F072_gap"] if cert else None,
        "provisional_uncommitted":out["provisional_last_snapshot"],
        "R13_initial_hash_sha256":source_hash,
        "original_economy_fingerprint_sha256":source_fingerprint,
        "original_R13_R14_F084F_check_for_final":certified,
        "solver_work":out["work"],
        "generations":out["generations"],
        "last_trace":out["trace_tail"][-1] if out["trace_tail"] else None,
        "fixture_construction_wall_ms_observed":round(fixture_ms,3),
        "solver_python_wall_ms_observed":out["elapsed_ms_observed_python"],
        "limits":vars(limits),
        "price_P_sqrt_D_over_Q":True,
        "congestion_exponent":4,
        "economic_tolerance_global_AND_worst":F072_TOLERANCE,
        "financial_F059_ratified":False,
        "CSharp_Godot_executed":False,
        "runtime_preemption_or_scheduler_present":False,
        "production_approved":False,
    }


def classify(profile,rows):
    """Geography 5762/144 is a NEW mandatory regression on its own profile.

    Optional 240/120 remains separately labelled even if the CI runner
    is green; it cannot inherit a 144-producer PASS.
    """
    if profile not in PROFILES:
        raise ValueError("Unknown profile")
    if len(rows)!=len(PROFILES[profile]):
        raise ValueError("Incomplete profile")
    observed={r["name"]:r["original_F072_independent_certified"]
              for r in rows}
    if set(observed)!=set(PROFILES[profile]):
        raise ValueError("Unregistered or missing scenario")
    if profile=="geography":
        if observed["geography_5762_144_48"]:
            return "PASS_5762_GEOGRAPHY_ORIGINAL_F072_CERTIFIED",True
        return "NO_GO_5762_GEOGRAPHY_COMMERCIAL_GATE",False
    if all(observed.values()):
        return "PASS_240_GROUP_120_MARKET_STRESS_CERTIFIED",True
    return "NO_GO_240_GROUP_STRESS_NOT_CERTIFIED",False


def run(profile):
    if ADR48.ADR42.R21.validate_manifest()!=16:
        raise AssertionError("Immutable R20 baseline SHA16 files drifted")
    rows=[]
    for name in PROFILES[profile]:
        result=execute(name)
        rows.append(result)
        work=result["solver_work"]
        print("[ADR049:{}] status={} F072_GO={} global={} worst={} "
              "nodes={} groups={} markets={} active={} H={} "
              "Krylov={} block={} scans={} ms={}".format(
                  name,result["numerical_status"],
                  result["original_F072_independent_certified"],
                  result["original_F072_global_gap_final_if_pass"],
                  result["original_F072_worst_group_gap_final_if_pass"],
                  result["nodes"],result["producer_groups"],
                  result["markets"],work["active_columns_highwater"],
                  work["H_evaluations_including_rejected_trials"],
                  work["matrixfree_Jv_and_Jtv_products"],
                  work["normal_block_coefficients_worked"],
                  work["separate_shortest_path_scans"],
                  result["solver_python_wall_ms_observed"]
              ),flush=True)
    status,passed=classify(profile,rows)
    return {
        "schema":SCHEMA,"profile":profile,"status":status,
        "mandatory_economic_gate_pass":passed,
        "scenarios":rows,
        "R20_source_manifest_unchanged_16":True,
        "original_F072_quartic_congestion_unchanged":True,
        "only_lab_CI_Python_executed":True,
        "no_CSharp_Godot_CPU_cycle_benchmarks":True,
        "no_game_thread_scheduler":True,
        "financial_F059_unratified":True,
        "production_approved":False,
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile",choices=tuple(PROFILES),default="geography")
    p.add_argument("--output",type=Path,default=Path.cwd())
    a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,"profile":a.profile,
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "python":platform.python_version(),"platform":platform.platform(),
        "status":"FAILED_BENCHMARK_CONTRACT",
        "production_approved":False,
        "F059_unratified":True,
    }
    try:
        report.update(run(a.profile))
    except Exception as e:
        report["status"]="FAILED_BENCHMARK_CONTRACT"
        report["error"]=repr(e)
        report["traceback"]=traceback.format_exc()
        print("[ADR049] CONTRACT ERROR:",repr(e),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path=a.output/("GA-Gate09-ADR049-"+a.profile+"-"+stamp+".json")
    path.write_text(json.dumps(report,indent=2,sort_keys=True,
                               ensure_ascii=False)+"\n",encoding="utf-8")
    archive=a.output/("GA-Gate09-ADR049-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(path,path.name)
    print("[ADR049] STATUS:",report["status"],flush=True)
    print("[ADR049] EVIDENCE:",archive,flush=True)
    return 0 if report.get("mandatory_economic_gate_pass",False) else 1


if __name__=="__main__":
    raise SystemExit(main())
