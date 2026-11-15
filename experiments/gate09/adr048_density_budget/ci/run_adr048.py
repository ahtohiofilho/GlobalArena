#!/usr/bin/env python3
"""Global Arena Gate09 ADR-048: scale REAL economic density on Goldberg worlds.

The ADR-047 block/MCP solver and F072 independent certifier remain
UNMODIFIED. This driver adds deterministic R13 dense producer/market
fixtures, strict commercial PASS/NO-GO and measured work ledgers.

The turn budget model is EX POST planning only: it is NOT an asynchronous,
preemptible or Godot-integrated scheduler. It NEVER accepts an uncertified
economic snapshot or commits it to the game.
"""
from __future__ import annotations
import argparse
from dataclasses import dataclass
from datetime import datetime,timezone
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
sys.path.insert(0,str(ROOT/"adr047_block_mcp"/"ci"))
import run_adr047 as ADR47
R45=ADR47.R45
R46=ADR47.R46
ADR42=ADR47.ADR42
from clearing_r5 import _flow_checksum
from multimarket_r13 import R13Scenario,construct_scenario

SCHEMA="GA_GATE09_ADR048_DENSITY_BUDGET_AND_CYCLE_RESERVE_V1"
ORIGINAL_TOLERANCE=1e-4

# R13 controls real graph topology, groups and demand/cost rules:
# The study varies their DENSITY, not the prices or equation parameters.
FIXTURES={
    "baseline_2562_48_24":R13Scenario(
        m=16,products=4,markets_per_product=6,groups_per_product=12,
        civilizations=6,iterations=0,exponent=4),
    "density_2562_96_32":R13Scenario(
        m=16,products=4,markets_per_product=8,groups_per_product=24,
        civilizations=6,iterations=0,exponent=4),
    "density_2562_144_48":R13Scenario(
        m=16,products=6,markets_per_product=8,groups_per_product=24,
        civilizations=6,iterations=0,exponent=4),
    "geography_5762_144_48":R13Scenario(
        m=24,products=6,markets_per_product=8,groups_per_product=24,
        civilizations=6,iterations=0,exponent=4),
}
PROFILES={
    "smoke":("baseline_2562_48_24",),
    "ci":("density_2562_96_32",),
    "extended":("density_2562_96_32","density_2562_144_48"),
    "geography":("geography_5762_144_48",),
}


@dataclass(frozen=True)
class CycleReservation:
    """Hypothetical periodic-economy budget; NEVER frames/real scheduler.

    The caller chooses desired CPU milliseconds per GAME ECONOMIC CYCLE.
    Other systems are guaranteed a distinct reserved planning amount.
    This rule admits only a FINISHED independently certified candidate.
    No in-progress computation can be paused, cancelled or resumed here.
    """
    total_cpu_budget_ms:float
    reserved_other_layers_ms:float

    def __post_init__(self):
        if (type(self.total_cpu_budget_ms) not in (int,float)
            or type(self.reserved_other_layers_ms) not in (int,float)
            or not math.isfinite(self.total_cpu_budget_ms)
            or not math.isfinite(self.reserved_other_layers_ms)
            or self.total_cpu_budget_ms<=0
            or self.reserved_other_layers_ms<0
            or self.reserved_other_layers_ms>=self.total_cpu_budget_ms):
            raise ValueError("Invalid illustrative cycle budget")

    @property
    def economy_ms(self):
        return self.total_cpu_budget_ms-self.reserved_other_layers_ms


def model_cycle_reservation(economic_f072_certified,observed_compute_ms,policy):
    """Only illustrative admission after a completed lab computation.

    NEVER a host scheduler guarantee. This budget is in observations of
    the Python GitHub runner, not measured game-thread CPU time.
    """
    if type(economic_f072_certified) is not bool:
        raise ValueError("The original F072 certification must be boolean")
    if (type(observed_compute_ms) not in (int,float)
        or not math.isfinite(observed_compute_ms)
        or observed_compute_ms<0):
        raise ValueError("Invalid measured compute time")
    if not isinstance(policy,CycleReservation):
        raise ValueError("Missing cycle reservation")
    if not economic_f072_certified:
        decision="BLOCK_UNCERTIFIED_ECONOMIC_SNAPSHOT"
    elif observed_compute_ms>policy.economy_ms:
        decision="DEFER_CERTIFIED_SNAPSHOT_AS_TOO_COSTLY_FOR_PLAN"
    else:
        decision="ADMISSIBLE_FOR_HYPOTHETICAL_PERIODIC_CYCLE"
    return {
        "decision":decision,
        "total_cycle_cpu_ms_hypothetical":policy.total_cpu_budget_ms,
        "reserved_for_non_economic_layers_ms":
            policy.reserved_other_layers_ms,
        "available_for_economic_solver_ms":policy.economy_ms,
        "observed_Python_runner_wall_ms":observed_compute_ms,
        "remaining_economic_budget_ms":policy.economy_ms-observed_compute_ms,
        "game_thread_executed":False,
        "asynchronous_preemption_implemented":False,
        "actual_CSharp_CPU_ms_known":False,
        "no_game_snapshot_committed":True,
        "render_frames_not_measured":True,
        "never_accept_uncertified_commercial_state":True,
    }


def _economics_fingerprint(net):
    """Verify that laboratory trials cannot mutate F072/ownership inputs."""
    payload=repr((
        tuple(sorted(net.edges.items())),
        tuple(sorted(net.groups.items())),
        tuple(sorted(net.markets.items())),
    )).encode()
    return sha256(payload).hexdigest()


def limits_for(name):
    """Predeclared, separate numerical work budgets, NOT game CPU budgets."""
    if name=="baseline_2562_48_24":
        return ADR47.Limits(
            max_h_evaluations=650,max_route_scans=1200,
            max_original_certificates=6,max_columns=500,
            max_generations=5,max_newton_steps=165,
            max_backtrack=10,max_krylov_products=36000,
            max_cg_iterations=130,max_block_coefficients=180000)
    if name=="density_2562_96_32":
        return ADR47.Limits(
            max_h_evaluations=740,max_route_scans=2600,
            max_original_certificates=6,max_columns=800,
            max_generations=6,max_newton_steps=165,
            max_backtrack=10,max_krylov_products=48000,
            max_cg_iterations=140,max_block_coefficients=220000)
    if name in ("density_2562_144_48","geography_5762_144_48"):
        return ADR47.Limits(
            max_h_evaluations=820,max_route_scans=4300,
            max_original_certificates=6,max_columns=1150,
            max_generations=6,max_newton_steps=175,
            max_backtrack=10,max_krylov_products=56000,
            max_cg_iterations=160,max_block_coefficients=245000)
    raise ValueError("Unknown registered density fixture")


def run_fixture(name):
    if name not in FIXTURES:
        raise ValueError("Unregistered original R13 density fixture")
    cfg=FIXTURES[name]
    start=perf_counter()
    net,initial_flows=construct_scenario(cfg)
    construction_ms=(perf_counter()-start)*1000
    before=_flow_checksum(initial_flows)
    economic_source=_economics_fingerprint(net)
    original_q=net.snapshots(initial_flows)[1]
    if (len(net.groups)!=cfg.groups_per_product*cfg.products or
        len(net.markets)!=cfg.markets_per_product*cfg.products or
        any(original_q.get(m,0.)<=0 for m in net.markets)):
        raise AssertionError("R13 physical production/demand fixture drift")
    if not all(m.demand>0 for m in net.markets.values()):
        raise AssertionError("R13 market demand invalid")
    settings=limits_for(name)
    observed=ADR47.solve_block(net,initial_flows,limits=settings,restricted=True)
    if _flow_checksum(initial_flows)!=before:
        raise AssertionError("Numerical search mutated physical input")
    if _economics_fingerprint(net)!=economic_source:
        raise AssertionError("Numerical search mutated original economics")
    certificate=observed["certificate"]
    is_pass=bool(
        observed["status"].startswith("PASS_")
        and certificate
        and certificate["status"]=="PASS_STATIC_F072_FIRST_ORDER_ONLY"
        and certificate["global_F072_gap"]<=ORIGINAL_TOLERANCE
        and certificate["worst_group_F072_gap"]<=ORIGINAL_TOLERANCE
        and observed["final_flow_sha256"] is not None
    )
    if observed["status"].startswith("PASS") and not is_pass:
        raise AssertionError("FALSE certified commercial pass")
    if not is_pass and observed["final_allocations"] is not None:
        raise AssertionError("NO-GO returned published candidate")
    # Only ex-post sensitivity: NOT canonized engine CPU reservation.
    reservations=[
        model_cycle_reservation(is_pass,observed["elapsed_ms_observed_python"],
                                CycleReservation(total,other))
        for total,other in ((10000.,4000.),(60000.,15000.),
                            (120000.,30000.),(300000.,60000.))
    ]
    return {
        "fixture":name,
        "geometry_nodes":10*cfg.m*cfg.m+2,
        "geometry_edges":len(net.edges),
        "physical_producer_groups":len(net.groups),
        "priced_markets":len(net.markets),
        "commodities":cfg.products,
        "civilizations":cfg.civilizations,
        "theoretical_canonical_columns":cfg.groups_per_product*cfg.products*
            cfg.markets_per_product,
        "initial_funded_columns":sum(v>0 for v in initial_flows.values()),
        "mean_markets_per_producer":cfg.markets_per_product,
        "R13_input_economics_fingerprint_sha256":economic_source,
        "R13_original_initial_flows_sha256":before,
        "fixture_construction_wall_ms":round(construction_ms,3),
        "solver_wall_ms_observed_python":observed["elapsed_ms_observed_python"],
        "numerical_status":observed["status"],
        "static_original_F072_go":is_pass,
        "final_global_F072_gap_if_quoted":(
            certificate["global_F072_gap"] if is_pass else None),
        "final_worst_group_F072_gap_if_quoted":(
            certificate["worst_group_F072_gap"] if is_pass else None),
        "last_certified_original_snapshot_status":(
            certificate["status"] if certificate else None),
        "last_certified_original_gap_not_necessarily_final":(
            certificate["global_F072_gap"] if certificate else None),
        "last_certified_original_worst_group_gap_not_necessarily_final":(
            certificate["worst_group_F072_gap"] if certificate else None),
        "provisional_snapshot_never_committed":
            observed["provisional_last_snapshot"],
        "work":observed["work"],
        "generations":observed["generations"],
        "work_limit_declaration":vars(settings),
        "illustrative_expost_cycle_reservations":reservations,
        "original_price_P_sqrt_D_over_Q":True,
        "original_congestion_exponent":4,
        "original_F072_global_and_worst_tolerance":ORIGINAL_TOLERANCE,
        "independent_R13_R14_and_F084F_used":is_pass,
        "F059_finance_policy_ratified":False,
        "game_CSharp_or_Godot_executed":False,
        "runtime_scheduler_or_yield_implemented":False,
        "production_approved":False,
    }


def exercise(profile):
    if profile not in PROFILES:
        raise ValueError("Unknown density benchmark profile")
    if ADR42.R21.validate_manifest()!=16:
        raise AssertionError("Frozen original Gate09 R20 SHA manifest changed")
    result=[]
    for case in PROFILES[profile]:
        r=run_fixture(case)
        result.append(r)
        work=r["work"]
        print("[ADR048:{}] status={} F072_GO={} global={} worst={} "
              "nodes={} groups={} markets={} cols_peak={} "
              "H={} Krylov={} block={} route_scans={} ms={}".format(
                  case,r["numerical_status"],r["static_original_F072_go"],
                  r["final_global_F072_gap_if_quoted"],
                  r["final_worst_group_F072_gap_if_quoted"],
                  r["geometry_nodes"],r["physical_producer_groups"],
                  r["priced_markets"],work["active_columns_highwater"],
                  work["H_evaluations_including_rejected_trials"],
                  work["matrixfree_Jv_and_Jtv_products"],
                  work["normal_block_coefficients_worked"],
                  work["separate_shortest_path_scans"],
                  r["solver_wall_ms_observed_python"]),flush=True)
    return {
        "profile":profile,
        "samples":result,
        "certified_original_F072_cases":sum(
            x["static_original_F072_go"] for x in result),
        "sample_count":len(result),
        "all_density_fixtures_independently_F072_certified":all(
            x["static_original_F072_go"] for x in result),
        "R20_original_manifest_files":16,
        "fixed_economics_not_adjusted":True,
        "large_economic_density_is_distinct_from_node_count":True,
        "CPU_budget_analysis_only_not_real_scheduler":True,
        "CSharp_implementation_not_authorized":True,
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile",choices=tuple(PROFILES),default="ci")
    p.add_argument("--output",type=Path,default=Path.cwd())
    a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,
        "utc":datetime.now(timezone.utc).isoformat(),
        "python":platform.python_version(),
        "platform":platform.platform(),
        "status":"FAILED",
        "financial_F059_ratified":False,
        "CSharp_or_Godot_integrated":False,
        "actual_game_runtime_scheduler_present":False,
    }
    try:
        report.update(exercise(a.profile))
        # CI itself tests integrity. Density GO is a SEPARATE classification,
        # visible in the same output; no silent F072 PASS for a NO-GO case.
        report["status"]=(
            "PASS_ALL_DENSITY_F072_INDEPENDENTLY_CERTIFIED"
            if report["all_density_fixtures_independently_F072_certified"]
            else "NO_GO_DENSITY_SCALE_BUT_CONTRACT_VERIFIED")
    except Exception as error:
        report["status"]="FAILED_BENCHMARK_CONTRACT"
        report["error"]=repr(error)
        report["traceback"]=traceback.format_exc()
        print("[ADR048] CONTRACT FAILED:",repr(error),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path=a.output/("GA-Gate09-ADR048-"+a.profile+"-"+stamp+".json")
    path.write_text(json.dumps(report,indent=2,sort_keys=True,
                               ensure_ascii=False)+"\n",encoding="utf-8")
    archive=a.output/("GA-Gate09-ADR048-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(path,path.name)
    print("[ADR048] STATUS:",report["status"],flush=True)
    print("[ADR048] EVIDENCE:",archive,flush=True)
    return 1 if report["status"]=="FAILED_BENCHMARK_CONTRACT" else 0


if __name__=="__main__":
    raise SystemExit(main())
