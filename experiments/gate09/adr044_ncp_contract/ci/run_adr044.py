#!/usr/bin/env python3
"""Gate09 ADR-044: NCP/VI KERNEL CONTRACT + bounded 2-market root WITNESS.

General networks: READ-ONLY complementarity residuals with ALL available
funded routes plus cheapest compatible-market paths. The original ADR043
inverse VI and ADR042 static F072 certificate remain independent.
Only the rigorously restricted 1-producer / 2-destination / 2-branch
toy has an executable 1-D bracketing reference. It is NOT a general
game solver, does not update authoritative C#/Godot, and is NOT
the fourth R38-R40 best-response numerical method.
"""
from __future__ import annotations
import argparse
from datetime import datetime,timezone
import json
import math
from pathlib import Path
import platform
import sys
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"inverse_vi_contract"/"ci"))
import verify_inverse_vi as ADR43
ADR42=ADR43.ADR42
from clearing_r5 import ClearingSettings,_flow_checksum,_reachable
from oracle_cache_r14 import FrozenRouteCache
from gate09 import Column

SCHEMA="GA_GATE09_ADR044_NCP_CONTRACT_TOY_ROOT_V1"
NCP_NORM_SCALE="per_group_supply_and_max_1_H"
DEFAULT_TOY_BISECTION_LIMIT=64
DEFAULT_TOY_COST_TOL=1e-12
DEFAULT_F072_TOL=1e-4


def _fb(a,b):
    """Fischer-Burmeister phi: zero iff a>=0,b>=0,a*b=0."""
    if not (math.isfinite(a) and math.isfinite(b)):
        raise ValueError("Nonfinite complementarity input")
    return math.hypot(a,b)-a-b


def complementarity_contract(net,flows,settings=None):
    """Check normalised FB and global omitted-route separation; NEVER solve.

    The FB norm is merely a mathematical diagnostic. F072 pass/fail
    is determined by ORIGINAL ADR042, not a made-up threshold on H.
    A zero-Q candidate may be evaluated through H, but NEVER quoted
    through original R14/ADR042.
    """
    if settings is None:
        settings=ClearingSettings(exponent=4)
    before=_flow_checksum(flows)
    loads,q,_=net.snapshots(flows)
    reachable,_,unreachable=_reachable(net,settings.exponent)
    markets=tuple(sorted(m for m in net.markets if m not in unreachable))
    missing=tuple(m for m in markets if q.get(m,0)<=0)
    cache=FrozenRouteCache(net,loads,settings.exponent)
    rows=[]
    max_fb=0.
    summed_vi=0.
    separation_count=0
    candidate_columns=0
    for gid,g in sorted(net.groups.items()):
        distances,paths,_=cache.paths(g.origin,g.seller)
        weights=cache.weights(g.seller)
        included=set(c for c,v in flows.items() if c.group_id==gid and v>0)
        canonical={}
        for key in markets:
            if key[1]!=g.commodity or key[0] not in paths:
                continue
            c=Column(gid,key[0],paths[key[0]])
            canonical[c]=True
        universe=sorted(included.union(canonical))
        if not universe:
            raise AssertionError("No route support for physically funded group")
        costs={}
        for col in universe:
            m=(col.destination,g.commodity)
            h=(1+sum(weights[e] for e in col.edges))*math.sqrt(
                q.get(m,0)/net.markets[m].demand)
            if not math.isfinite(h) or h<0:
                raise AssertionError("Nonfinite H in NCP candidate")
            costs[col]=h
        min_h=min(costs.values())
        scale=max(1.,max(costs.values()))
        group_fb=0.
        group_vi=0.
        funded=0
        for col in universe:
            a=flows.get(col,0.)/g.supply
            slack=(costs[col]-min_h)/scale
            phi=_fb(a,slack)
            group_fb=max(group_fb,abs(phi))
            if a>0:
                funded+=1
                group_vi+=flows[col]*max(0.,costs[col]-min_h)
        if funded<1:raise AssertionError("Missing physical group output")
        max_fb=max(max_fb,group_fb)
        summed_vi+=group_vi
        separation_count+=len(canonical)
        candidate_columns+=len(universe)
        rows.append({
            "group":gid,"mu_minimum_H":min_h,
            "max_absolute_normalized_FB":group_fb,
            "raw_VI_regret":group_vi,
            "funded_columns":funded,
            "canonical_reachable_market_columns":len(canonical),
            "restricted_plus_global_columns_checked":len(universe),
            "unfunded_canonical_best_route_considered":True,
        })
    if _flow_checksum(flows)!=before:
        raise AssertionError("Read-only NCP changed input flow")
    report={
        "schema":SCHEMA,
        "flow_sha256":before,
        "group_count":len(net.groups),
        "market_count":len(net.markets),
        "reachable_market_count":len(markets),
        "unpriced_reachable_markets":[list(m) for m in missing],
        "status":("BOUNDARY_Q_ZERO_NOT_F072_CERTIFIED" if missing
                  else "INTERIOR_NCP_WITNESS"),
        "max_absolute_Fischer_Burmeister_residual":max_fb,
        "raw_VI_regret":summed_vi,
        "ncp_normalization":NCP_NORM_SCALE,
        "global_canonical_market_separations":separation_count,
        "all_support_columns_checked":candidate_columns,
        "independent_shortest_path_scans":cache.counters()["shortest_path_scans"],
        "R14_oracles_in_NCP_residual":0,
        "economic_F072_certification":None,
        "financial_F059_ratified":False,
        "global_convergence_proven":False,
        "CSharp_integrated":False,
        "production_approved":False,
        "no_flow_update":True,
        "groups":rows,
    }
    if not missing:
        certificate=ADR42.static_certificate(net,flows,settings)
        report["economic_F072_certification"]={
            "status":certificate["status"],
            "relative_gap":certificate["gap_global_complementarity"],
            "max_group_gap":certificate["gap_worst_group_complementarity"],
            "financial_tie_groups":certificate["financial_F059_tie_groups"],
            "independent_R13_checks":certificate["independent_R13_checks"],
            "original_R14_calls":certificate["original_R14_oracle_calls"],
            "F084F_financial_checks":certificate["F084F_financial_checks"],
        }
        report["static_commercial_pass"]=certificate[
            "static_complementarity_approximate"]
    else:
        report["static_commercial_pass"]=False
    return report


def _two_branch_original_costs(net,gid,columns,x_left):
    """Only a path-disjoint one-group/two-market/two-edge analytic fixture.

    Returns H_left-H_right. No R14 is called when a market is empty;
    only the continuous H representation is evaluated.
    """
    g=net.groups[gid]
    left,right=columns
    masses={left:x_left,right:g.supply-x_left}
    loads,q,_=net.snapshots(masses)
    H=[]
    for col in columns:
        market=(col.destination,g.commodity)
        cost=sum(net.edges[eid].congestion_cost(loads.get(eid,0.),4)+
                 float(net.edges[eid].weight_for(g.seller))
                 for eid in col.edges)
        h=(1+cost)*math.sqrt(q.get(market,0.)/net.markets[market].demand)
        H.append(h)
    return H[0]-H[1]


def bracketed_two_market_reference(net,initial_flows,
                                  max_evaluations=DEFAULT_TOY_BISECTION_LIMIT,
                                  root_cost_tolerance=DEFAULT_TOY_COST_TOL):
    """A deliberately TINY scalar root reference, NOT generic MCP solver.

    Initial state can have Q=0. A valid final candidate must pass
    ADR042 original full market certificate; no H-only success.
    """
    if (type(max_evaluations) is not int or
        max_evaluations<4 or max_evaluations>4096 or
        not math.isfinite(root_cost_tolerance) or
        not 0<root_cost_tolerance<=1e-4):
        raise ValueError("Invalid fixed toy root budget/tolerance")
    initial_digest=_flow_checksum(initial_flows)
    net.snapshots(initial_flows)
    if len(net.groups)!=1 or len(net.markets)!=2 or len(net.edges)!=2:
        raise ValueError("Reference only allows one producer and two branches")
    gid,g=next(iter(sorted(net.groups.items())))
    if any(edge.a!=g.origin and edge.b!=g.origin
           for edge in net.edges.values()):
        raise ValueError("Reference requires both edges incident at origin")
    # Two different markets on the two distinct neighboring nodes,
    # with no self-market, cycles, overlapping routes or other products.
    destinations=sorted(net.markets)
    if len({m[1] for m in destinations})!=1 or destinations[0][1]!=g.commodity:
        raise ValueError("Reference requires one commodity")
    paths=[]
    for key in destinations:
        direct=sorted(edge.id for edge in net.edges.values()
                      if key[0] in (edge.a,edge.b) and g.origin in(edge.a,edge.b))
        if len(direct)!=1 or key[0]==g.origin:
            raise ValueError("Reference requires exactly one direct edge per market")
        paths.append(Column(gid,key[0],(direct[0],)))
    if paths[0].edges==paths[1].edges:
        raise ValueError("Reference requires disjoint unique branches")
    initial_H=ADR43.inverse_vi_witness(net,initial_flows)
    left,right=0.,g.supply
    fleft=_two_branch_original_costs(net,gid,paths,left)
    fright=_two_branch_original_costs(net,gid,paths,right)
    used=2
    if not (fleft<0<fright):
        raise AssertionError("No H-root bracketing: unsupported toy model")
    trace=[]
    x=None
    fx=None
    for index in range(max_evaluations-2):
        mid=(left+right)/2
        if mid in (left,right):break
        f=_two_branch_original_costs(net,gid,paths,mid)
        used+=1
        trace.append({
            "eval":used,"left_mass":mid,"H_left_minus_H_right":f,
            "bracket_width_before":right-left,
        })
        if abs(f)<=root_cost_tolerance:
            x,fx=mid,f
            break
        if f>0:right=mid
        else:left=mid
    if x is None:
        # No free 65th evaluation when the 64-call budget is exhausted.
        # The last paid midpoint is the only candidate that can be used.
        if not trace:
            raise AssertionError("Bracketing made no priced midpoint")
        x=trace[-1]["left_mass"]
        fx=trace[-1]["H_left_minus_H_right"]
    if abs(fx)>root_cost_tolerance:
        status="NO_GO_TOY_BUDGET_OR_PRECISION"
        final_certificate=None
    else:
        proposal={paths[0]:x,paths[1]:g.supply-x}
        final_certificate=ADR42.static_certificate(net,proposal)
        status=("PASS_TOY_ROOT_AND_INDEPENDENT_F072_CERTIFICATE"
                if final_certificate["status"]=="PASS_STATIC_F072_FIRST_ORDER_ONLY"
                else "NO_GO_ORIGINAL_F072_CERTIFICATE_REJECTED")
    if _flow_checksum(initial_flows)!=initial_digest:
        raise AssertionError("Toy scalar experiment mutated source")
    return {
        "status":status,
        "initial_flow_sha256":initial_digest,
        "input_initial_has_Q_zero":bool(initial_H["zero_quantity_reachable_markets"]),
        "original_H_boundary_witness":{
            "raw_VI_regret":initial_H["VI_raw_residual_sum"],
            "boundary_Q0_count":len(initial_H["zero_quantity_reachable_markets"])},
        "candidate_left_mass":x,
        "candidate_right_mass":g.supply-x,
        "final_scalar_root_absolute_H_cost_difference":abs(fx),
        "H_evaluations_used_including_endpoints":used,
        "H_evaluation_budget":max_evaluations,
        "F072_independent_certificate":(
            None if final_certificate is None else {
                "status":final_certificate["status"],
                "relative_gap":final_certificate["gap_global_complementarity"],
                "max_group_gap":final_certificate["gap_worst_group_complementarity"],
                "R13_checks":final_certificate["independent_R13_checks"],
                "R14_calls":final_certificate["original_R14_oracle_calls"],
                "F084F_checks":final_certificate["F084F_financial_checks"],
            }),
        "trace":trace,
        "no_numeric_price_floor":True,
        "not_general_NCP_solver":True,
        "no_authoritative_game_state_mutation":True,
        "F059_not_ratified":True,
    }


def exercise():
    if ADR42.R21.validate_manifest()!=16:
        raise AssertionError("Frozen R20 manifest changed")
    examples=[]
    for name,net,flows in (
        ("symmetric",*ADR42.one_group_example(.5)),
        ("asymmetric",*ADR42.one_group_example(.6)),
        ("one_reachable_zero",*ADR42.one_group_example(1.)),
        ("two_group_nonunique_A",*ADR42.two_group_example(.0)),
        ("two_group_nonunique_mid",*ADR42.two_group_example(.5)),
    ):
        r=complementarity_contract(net,flows)
        examples.append({"name":name,"residual":r})
        print("[ADR044:{}] NCP_inf={:.10g} F072={} Qzero={}".format(
            name,r["max_absolute_Fischer_Burmeister_residual"],
            r["economic_F072_certification"]["status"] if
            r["economic_F072_certification"] else "BLOCKED",
            len(r["unpriced_reachable_markets"])),flush=True)
    toy=bracketed_two_market_reference(*ADR42.one_group_example(.6))
    zero=bracketed_two_market_reference(*ADR42.one_group_example(1.))
    if not (toy["status"]=="PASS_TOY_ROOT_AND_INDEPENDENT_F072_CERTIFICATE" and
            zero["status"]=="PASS_TOY_ROOT_AND_INDEPENDENT_F072_CERTIFICATE"):
        raise AssertionError("Reference root failed to recover original F072")
    print("[ADR044:TOY] from 0.6 and 1.0: independent original F072 PASS",
          " H evaluations",toy["H_evaluations_used_including_endpoints"],
          zero["H_evaluations_used_including_endpoints"],flush=True)
    return {
        "static_complementarity_cases":examples,
        "toy_bracketed_root_from_asymmetry":toy,
        "toy_bracketed_root_from_unpriced_boundary":zero,
        "first_ncp_contract_not_general_solver":True,
        "next_gate":"BOUNDED_GENERAL_MCP_VI_COLUMN_GENERATION_IMPL",
        "original_F072_and_R13_R14_F084F_unchanged":True,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "python":platform.python_version(),
        "platform":platform.platform(),
        "status":"FAILED",
        "CSharp_modified":False,
        "economy_F072_modified":False,
        "global_solution_claimed":False,
        "F059_financial_policy_ratified":False,
    }
    try:
        report.update(exercise())
        report["status"]="PASSED_ADR044_CONTRACT_AND_BOUNDED_TOY_ROOT"
    except Exception as err:
        report["error"]=repr(err)
        report["traceback"]=traceback.format_exc()
        print("[ADR044] FAILED:",repr(err),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    json_path=args.output/("GA-Gate09-ADR044-"+stamp+".json")
    json_path.write_text(json.dumps(report,sort_keys=True,indent=2,
                                    ensure_ascii=False)+"\n",encoding="utf-8")
    archive=args.output/("GA-Gate09-ADR044-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as arc:
        arc.write(json_path,json_path.name)
    print("[ADR044] STATUS:",report["status"],flush=True)
    print("[ADR044] EVIDENCE:",archive,flush=True)
    return 0 if report["status"]=="PASSED_ADR044_CONTRACT_AND_BOUNDED_TOY_ROOT" else 1


if __name__=="__main__":
    raise SystemExit(main())
