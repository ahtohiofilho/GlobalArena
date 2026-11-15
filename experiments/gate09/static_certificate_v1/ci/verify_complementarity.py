#!/usr/bin/env python3
"""GA Gate09 ADR-042 — STATIC commercial-equilibrium certificate, NOT A SOLVER.

Given frozen physical flows, independently audit the precise F072
complementarity regret against original R13 and R14, with global market
separation by original seller-specific least-cost paths and F084F
settlement. Never modify flows, artificially seed Q, choose an alpha,
propose a new allocation, or require 3 artificial committed updates.

Status PASS is a finite-precision snapshot FIRST-ORDER certificate,
not a proof of economic existence, uniqueness, dynamic stability,
financial F059 ratification, or authoritative C#/Godot parity.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import platform
import sys
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"r34"/"ci"))
import run_r34 as R34

R21=R34.R21
from clearing_r5 import ClearingSettings,_evaluate,_reachable,_flow_checksum
from gate09 import Edge,Group,Market,Column,Network
from oracle_cache_r14 import FrozenRouteCache,evaluate_r14

TOLERANCE=1e-4
SCHEMA="GA_GATE09_ADR042_STATIC_COMPLEMENTARITY_CERTIFICATE_V1"
APPROVAL="D1_D2_D3_ADOPTED_STATIC_CERTIFICATE_D4_SOLVER_SELECTION_PENDING"


def one_group_example(split):
    """2-destination original F072 economy, self-funded production only."""
    if not math.isfinite(split) or not 0<=split<=1:
        raise ValueError("Invalid supply share")
    net=Network(
        [Edge(1,0,1,c_free=1.,capacity=1.),
         Edge(2,0,2,c_free=1.,capacity=1.)],
        [Group(1,0,0,0,1.)],
        [Market(1,0,1.),Market(2,0,1.)])
    return net,{
        Column(1,1,(1,)):split,
        Column(1,2,(2,)):1-split,
    }


def two_group_example(a):
    """Continuum of physically distinct equal-score equilibria."""
    if not math.isfinite(a) or not 0<=a<=1:
        raise ValueError("Invalid equilibrium family parameter")
    net=Network(
        [Edge(1,0,1,c_free=1.,capacity=1.),
         Edge(2,0,2,c_free=1.,capacity=1.)],
        [Group(1,0,0,0,1.),Group(2,0,0,1,1.)],
        [Market(1,0,1.),Market(2,0,1.)])
    return net,{
        Column(1,1,(1,)):a,
        Column(1,2,(2,)):1-a,
        Column(2,1,(1,)):1-a,
        Column(2,2,(2,)):a,
    }


def static_certificate(net,flows,settings=None,tolerance=TOLERANCE):
    """Independent frozen R14/R13 full-route separation and first-order GAP.

    Entire producer supply is accounted for; no missing reachable market
    is silently ignored. A market with Q=0 is BLOCKED, not price-floored.
    Financial F059 tie status is reported independently from commercial
    first-order pass. The ledger is never mutably applied to any treasury.
    """
    if settings is None:
        settings=ClearingSettings(exponent=4)
    if not isinstance(settings,ClearingSettings):
        raise TypeError("Original ClearingSettings required")
    if not isinstance(tolerance,(float,int)) or not (
            math.isfinite(tolerance) and 0<=tolerance<1):
        raise ValueError("Invalid first-order tolerance")
    # Snapshot validation throws on missing physical supply, negative flow,
    # invalid route, or nonfinite amounts. Such states are NOT equilibria.
    initial_hash=_flow_checksum(flows)
    loads,quantities,prices=net.snapshots(flows)
    reachable,_,unreachable=_reachable(net,settings.exponent)
    unpriced=tuple(sorted(
        key for key in reachable
        if not (quantities.get(key,0.)>0 and math.isfinite(prices[key]))))
    common={
        "schema":SCHEMA,
        "flow_sha256":initial_hash,
        "market_count":len(net.markets),
        "reachable_market_count":len(reachable),
        "structurally_unreachable_markets":[list(k) for k in unreachable],
        "unpriced_reachable_markets":[list(k) for k in unpriced],
        "supply_conserved":True,
        "fractional_supply_sold":1.0,
        "tolerance":tolerance,
        "static_complementarity_approximate":False,
        "original_R24_three_step_gate_changed":False,
        "solver_implemented":False,
        "financial_F059_ratified":False,
        "global_existence_or_uniqueness_proven":False,
        "dynamic_convergence_proven":False,
        "CSharp_parity_proven":False,
        "production_approved":False,
    }
    if unpriced:
        if _flow_checksum(flows)!=initial_hash:
            raise AssertionError("Source was mutated during price-domain check")
        return {
            **common,
            "status":"BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE",
            "missing_price_not_replaced_by_floor":True,
            "original_R14_oracle_calls":0,
            "independent_R13_checks":0,
            "F084F_financial_checks":0,
        }
    observed=evaluate_r14(net,flows,settings,unreachable)
    independent=_evaluate(net,flows,settings,unreachable)
    for key in ("relative_gap","max_group_gap"):
        if not math.isclose(
                observed[key],independent[key],rel_tol=0,abs_tol=1e-12):
            raise AssertionError("Frozen R13/R14 regret mismatch: "+key)
    for key in ("target","tie_groups","active_columns",
                "loads","prices","quantities","structurally_unreachable_markets"):
        if observed[key]!=independent[key]:
            raise AssertionError("Frozen R13/R14 snapshot mismatch: "+key)

    cache=FrozenRouteCache(net,loads,settings.exponent)
    global_regret=0.
    global_best_weight=0.
    max_gap=0.
    max_active_relative_score_shortfall=0.
    group_rows=[]
    # Source ordering and scoring method reproduce R14; independent
    # separation checks every compatible reachable destination.
    funded_rows=sorted(flows.items())
    for gid,group in sorted(net.groups.items()):
        distances,paths,ways=cache.paths(group.origin,group.seller)
        weights=cache.weights(group.seller)
        ranked=[]
        for market in sorted(net.markets):
            if market in unreachable or market[1]!=group.commodity:
                continue
            if market[0] not in paths:
                continue
            s=prices[market]/(1+distances[market[0]])
            ranked.append((s,Column(gid,market[0],paths[market[0]])))
        if not ranked:
            raise AssertionError("No viable quoted market in separation")
        ranked.sort(key=lambda x:(-x[0],x[1]))
        highest,best=ranked[0]
        if best!=observed["target"][gid] or not math.isclose(
                highest,observed["groups"][gid]["score"],
                abs_tol=1e-12,rel_tol=1e-12):
            raise AssertionError("Independent global-route separation failed")
        positive=0
        regret=0.
        largest_product=0.
        for col,mass in funded_rows:
            if col.group_id!=gid or mass<=0:
                continue
            positive+=1
            actual=prices[(col.destination,group.commodity)]/(
                1+sum(weights[e] for e in col.edges))
            if actual>highest+1e-9*max(1,highest):
                raise AssertionError("Funded route beats R14 global separation")
            deficit=max(0.,highest-actual)
            regret+=mass*deficit
            largest_product=max(largest_product,mass*deficit)
            max_active_relative_score_shortfall=max(
                max_active_relative_score_shortfall,deficit/highest)
        group_gap=regret/(group.supply*highest)
        if not math.isclose(
                group_gap,observed["groups"][gid]["gap"],
                rel_tol=0,abs_tol=1e-12):
            raise AssertionError("Computed complementarity disagrees with R14")
        global_regret+=regret
        global_best_weight+=group.supply*highest
        max_gap=max(max_gap,group_gap)
        group_rows.append({
            "group":gid,
            "lambda_max_f072_score":highest,
            "original_R14_target_destination":best.destination,
            "original_R14_target_route":list(best.edges),
            "global_market_destinations_separated":len(ranked),
            "positive_allocations":positive,
            "positive_flow_regret_numerator":regret,
            "max_complementarity_product":largest_product,
            "normalized_complementarity_residual":group_gap,
            "r14_financial_co_optimality_flag":gid in observed["tie_groups"],
        })
    global_gap=global_regret/global_best_weight
    if not math.isclose(global_gap,observed["relative_gap"],
                        rel_tol=0,abs_tol=1e-12):
        raise AssertionError("Complementarity global sum disagrees with R14")
    if not math.isclose(max_gap,observed["max_group_gap"],
                        rel_tol=0,abs_tol=1e-12):
        raise AssertionError("Complementarity worst group disagrees with R14")

    fin=R21.settlement_check(net,flows)
    if fin["sold_micro"]!=sum(round(g.supply*1_000_000)
                             for g in net.groups.values()):
        raise AssertionError("F084F sold physical supply mismatch")
    if fin["gross_micro"]!=fin["credits_micro"]:
        raise AssertionError("F084F gross is not conserved")
    approved=(global_gap<=tolerance and max_gap<=tolerance)
    if _flow_checksum(flows)!=initial_hash:
        raise AssertionError("Static verifier modified producer flows")
    return {
        **common,
        "status":("PASS_STATIC_F072_FIRST_ORDER_ONLY" if approved
                  else "FAIL_STATIC_F072_COMMERCIAL_REGRET"),
        "static_complementarity_approximate":approved,
        "gap_global_complementarity":global_gap,
        "gap_worst_group_complementarity":max_gap,
        "original_R14_global_gap":observed["relative_gap"],
        "original_R14_worst_group_gap":observed["max_group_gap"],
        "max_active_relative_score_shortfall_not_a_separate_gate":
            max_active_relative_score_shortfall,
        "group_evidence":group_rows,
        "financial_unresolved_tie_group_count":len(observed["tie_groups"]),
        "financial_F059_tie_groups":list(observed["tie_groups"]),
        "F084F_conservation_witness":fin,
        "original_R14_oracle_calls":1,
        "independent_R13_checks":1,
        "F084F_financial_checks":1,
        "independent_shortest_path_scans":
            cache.counters()["shortest_path_scans"],
        "all_compatible_markets_globally_separated":True,
        "no_fake_R24_streak_required":True,
        "no_alternative_flow_committed":True,
    }


def exercise():
    # No flow updates anywhere in this runner: ALL are frozen input
    # snapshots. These controlled examples are mathematical witnesses,
    # not sample statistics of the full Global Arena world.
    scenarios=[]
    for label,split in (
        ("one_group_exact_symmetry",.5),
        ("one_group_asymmetry",.6),
        ("two_groups_boundary_A",0.),
        ("two_groups_quarter",.25),
        ("two_groups_midpoint",.5),
        ("two_groups_three_quarters",.75),
        ("two_groups_boundary_B",1.),
    ):
        net,flows=(one_group_example(split) if label.startswith("one_group")
                   else two_group_example(split))
        witness=static_certificate(net,flows)
        expected_pass=label!="one_group_asymmetry"
        if witness["static_complementarity_approximate"]!=expected_pass:
            raise AssertionError("Analytical symmetric equilibrium classification drift")
        scenarios.append({"label":label,"certificate":witness})
        print("[ANALYTIC:{}] status={} global={:.10g} worst={:.10g} "
              "tieGroups={}".format(
                  label,witness["status"],witness["gap_global_complementarity"],
                  witness["gap_worst_group_complementarity"],
                  witness["financial_unresolved_tie_group_count"]),flush=True)
    net,flows=one_group_example(1.)
    blocked=static_certificate(net,flows)
    if blocked["status"]!="BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE":
        raise AssertionError("Missing market wrongly approved or price-floored")
    scenarios.append({"label":"one_group_reachable_market_zero",
                      "certificate":blocked})
    print("[ANALYTIC:Q_ZERO] status="+blocked["status"],flush=True)
    return {
        "analytical_cases":scenarios,
        "decision":APPROVAL,
        "static_certificate_not_numerical_solver":True,
        "F059_ratified":False,
        "dynamic_three_updates_are_separate_gate":True,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "python":platform.python_version(),"platform":platform.platform(),
        "status":"FAILED",
        "fourth_solver_family_implemented":False,
        "global_equilibrium_existence_proven":False,
        "financial_F059_ratified":False,
        "production_approved":False,
    }
    try:
        report["r20_manifest_files_verified"]=R21.validate_manifest()
        if report["r20_manifest_files_verified"]!=16:
            raise AssertionError("R20 immutable manifest changed")
        report.update(exercise())
        report["status"]="PASSED_STATIC_F072_CERTIFICATE_CONTRACTS"
    except Exception as e:
        report["error"]=repr(e)
        report["traceback"]=traceback.format_exc()
        print("[ADR-042] FAILED:",repr(e),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path=args.output/("GA-Gate09-ADR042-"+stamp+".json")
    path.write_text(json.dumps(report,indent=2,sort_keys=True,
                               ensure_ascii=False)+"\n",encoding="utf-8")
    archive=args.output/("GA-Gate09-ADR042-evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(path,path.name)
    print("[ADR-042] STATUS:",report["status"],flush=True)
    print("[ADR-042] EVIDENCE:",archive,flush=True)
    return 0 if report["status"]=="PASSED_STATIC_F072_CERTIFICATE_CONTRACTS" else 1


if __name__=="__main__":
    raise SystemExit(main())
