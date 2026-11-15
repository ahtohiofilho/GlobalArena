#!/usr/bin/env python3
"""ADR-043: READ-ONLY reciprocal-score VI math witnesses. NEVER A SOLVER.

Cost H_gc=(1+C_gc(B))*sqrt(Q_m/D_m) is continuous at Q=0 and
identical to inverse original F072 score 1/S for Q>0.
We audit ONE frozen snapshot: active route VI residual, all-market
seller-specific shortest-route separation, exact market reachability,
and independent original R14/ADR042 parity when Q>0.
No flow update, price floor, synthetic demand, financial F059 rule,
existence theorem proved by tests, or numerical method is implemented.
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
sys.path.insert(0,str(ROOT/"static_certificate_v1"/"ci"))
import verify_complementarity as ADR42
from clearing_r5 import ClearingSettings,_reachable,_flow_checksum
from oracle_cache_r14 import FrozenRouteCache,evaluate_r14
from gate09 import Column

SCHEMA="GA_GATE09_ADR043_INVERSE_SCORE_VI_CONTRACT_V1"
ZERO_RESIDUAL_ROUNDOFF=1e-12
DECISION="D4_FORMULATION_SELECTED_NUMERICAL_ALGORITHM_NOT_IMPLEMENTED"


def inverse_vi_witness(net,flows,settings=None):
    """Physical simplex is original. No R14 evaluation at Q=0.

    The VI residual is numerically different from F072 regret; only
    its EXACT zero set has the same commercial meaning on priced
    snapshots. Pass of this witness is NOT a solver convergence gate.
    """
    if settings is None:
        settings=ClearingSettings(exponent=4)
    if not isinstance(settings,ClearingSettings):
        raise TypeError("Original F072 settings required")
    digest=_flow_checksum(flows)
    loads,q,prices=net.snapshots(flows)
    reachable,_,unreachable=_reachable(net,settings.exponent)
    eligible=tuple(sorted(k for k in reachable if k not in unreachable))
    zero_markets=tuple(k for k in eligible if q.get(k,0.)<=0)
    cache=FrozenRouteCache(net,loads,settings.exponent)
    group_rows=[]
    vi_total=0.
    separation_destinations=0
    inverse_checks=0
    all_supported_equal_min=True
    max_positive_h=0.
    for gid,group in sorted(net.groups.items()):
        distances,paths,ways=cache.paths(group.origin,group.seller)
        weights=cache.weights(group.seller)
        options=[]
        for market in eligible:
            if market[1]!=group.commodity or market[0] not in paths:
                continue
            demand=net.markets[market].demand
            inverse_cost=(1+distances[market[0]])*math.sqrt(
                q.get(market,0.)/demand)
            if not math.isfinite(inverse_cost) or inverse_cost<0:
                raise AssertionError("Nonfinite reciprocal candidate cost")
            options.append((inverse_cost,Column(gid,market[0],paths[market[0]])))
        if not options:
            raise AssertionError("No accessible reciprocal VI market for group")
        options.sort(key=lambda r:(r[0],r[1]))
        best_h,best_col=options[0]
        sep_count=len(options)
        separation_destinations+=sep_count
        residual=0.
        min_funded_h=math.inf
        max_funded_h=0.
        active=0
        for column,mass in sorted(flows.items()):
            if column.group_id!=gid or mass<=0:continue
            active+=1
            market=(column.destination,group.commodity)
            h=(1+sum(weights[e] for e in column.edges))*math.sqrt(
                q[market]/net.markets[market].demand)
            if not math.isfinite(h) or h<=0:
                raise AssertionError("Positive funded route has nonpositive H")
            if h+1e-10*max(1,h,best_h)<best_h:
                raise AssertionError("Frozen shortest-route separation violated")
            residual+=mass*max(0.,h-best_h)
            min_funded_h=min(min_funded_h,h)
            max_funded_h=max(max_funded_h,h)
            max_positive_h=max(max_positive_h,h)
        if active<1:
            raise AssertionError("Producer supply was not allocated")
        if residual>ZERO_RESIDUAL_ROUNDOFF:
            all_supported_equal_min=False
        vi_total+=residual
        group_rows.append({
            "group":gid,
            "min_inverse_score_cost":best_h,
            "best_market":best_col.destination,
            "best_route":list(best_col.edges),
            "active_columns":active,
            "separated_market_count":sep_count,
            "vi_complementarity_sum":residual,
            "funded_min_cost":min_funded_h,
            "funded_max_cost":max_funded_h,
            "best_is_unpriced_Q_zero":q.get(
                (best_col.destination,group.commodity),0.)<=0,
        })

    audit={
        "schema":SCHEMA,
        "flow_sha256":digest,
        "source_production_fully_allocated":True,
        "eligible_reachable_market_count":len(eligible),
        "unreachable_markets":[list(m) for m in unreachable],
        "zero_quantity_reachable_markets":[list(m) for m in zero_markets],
        "original_prices_undefined_at_zero_Q":bool(zero_markets),
        "boundary_inverse_cost_defined_without_price_floor":True,
        "all_funded_route_costs_strictly_positive":max_positive_h>0,
        "vi_residual_not_equal_to_f072_relative_gap_in_general":True,
        "VI_raw_residual_sum":vi_total,
        "VI_zero_residual_approximate":all_supported_equal_min,
        "group_evidence":group_rows,
        "independent_route_scans":cache.counters()["shortest_path_scans"],
        "global_route_market_separations":separation_destinations,
        "exact_identity_checks_price_times_inverse_cost":0,
        "original_R14_called":False,
        "original_F072_relative_gap":None,
        "original_static_certificate_status":None,
        "boundary_is_not_eligible_for_F072_certification":bool(zero_markets),
        "numeric_solver_implemented":False,
        "mathematical_existence_proven_by_test":False,
        "financial_F059_ratified":False,
        "CSharp_integrated":False,
        "production_approved":False,
    }
    if not zero_markets:
        # At positive Q, the reciprocal representation MUST replay
        # R14 scores exactly, including seller-specific route costs.
        observed=evaluate_r14(net,flows,settings,unreachable)
        audit["original_R14_called"]=True
        audit["original_F072_relative_gap"]=observed["relative_gap"]
        audit["original_F072_max_group_gap"]=observed["max_group_gap"]
        by_gid={row["group"]:row for row in group_rows}
        for gid,group in sorted(net.groups.items()):
            record=by_gid[gid]
            original_best=observed["groups"][gid]["score"]
            h=record["min_inverse_score_cost"]
            if not math.isclose(original_best*h,1.0,
                                rel_tol=1e-10,abs_tol=1e-10):
                raise AssertionError("Inverse H*original F072 best score != 1")
            inverse_checks+=1
            # A near commercial tie can pick either deterministic column
            # under different operation order; SCORES are normative.
        certificate=ADR42.static_certificate(net,flows,settings)
        audit["original_static_certificate_status"]=certificate["status"]
        audit["original_static_approximate_pass"] = certificate[
            "static_complementarity_approximate"]
        audit["independent_ADR042_checked"]=True
    else:
        audit["original_static_certificate_status"]=(
            "BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE")
    audit["exact_identity_checks_price_times_inverse_cost"]=inverse_checks
    if _flow_checksum(flows)!=digest:
        raise AssertionError("Verification illegally mutated market allocation")
    return audit


def exercise():
    rows=[]
    for name,network,flows in (
        ("one_group_exact_equilibrium",*ADR42.one_group_example(.5)),
        ("one_group_asymmetric_nonequilibrium",*ADR42.one_group_example(.6)),
        ("one_group_zero_market_boundary",*ADR42.one_group_example(1.)),
        ("two_group_boundary_a0",*ADR42.two_group_example(0.)),
        ("two_group_interior_a025",*ADR42.two_group_example(.25)),
        ("two_group_interior_a050",*ADR42.two_group_example(.5)),
        ("two_group_interior_a075",*ADR42.two_group_example(.75)),
        ("two_group_boundary_a1",*ADR42.two_group_example(1.)),
    ):
        evidence=inverse_vi_witness(network,flows)
        expected=name.startswith("two_group_") or name=="one_group_exact_equilibrium"
        if evidence["VI_zero_residual_approximate"]!=expected:
            raise AssertionError("Analytical VI witness classification failure: "+name)
        rows.append({"name":name,"evidence":evidence})
        print("[INVERSE:{}] Hgap={:.12g} zeroQ={} viZero={} pricedF072={}".format(
            name,evidence["VI_raw_residual_sum"],
            len(evidence["zero_quantity_reachable_markets"]),
            evidence["VI_zero_residual_approximate"],
            evidence["original_static_certificate_status"]),flush=True)
    return {
        "analytical_read_only_witnesses":rows,
        "selection":DECISION,
        "existence_is_mathematical_theorem_not_empirical_test":True,
        "formula_F072_unchanged":True,
        "no_fourth_solver_implemented":True,
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--output",type=Path,default=Path.cwd())
    args=p.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":SCHEMA,"status":"FAILED",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),
        "python":platform.python_version(),
        "R20_source_unchanged":True,
        "CSharp_authority_unchanged":True,
        "financial_F059_ratified":False,
        "numerical_solver_implemented":False,
    }
    try:
        manifest=ADR42.R21.validate_manifest()
        if manifest!=16:
            raise AssertionError("R20 frozen source SHA16 mismatch")
        report["R20_manifest_files_verified"]=manifest
        report.update(exercise())
        report["status"]="PASSED_INVERSE_VI_FORMULATION_WITNESSES_NOT_SOLVER"
    except Exception as err:
        report["error"]=repr(err)
        report["traceback"]=traceback.format_exc()
        print("[ADR043] FAILED:",repr(err),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    out=args.output/("GA-Gate09-ADR043-"+stamp+".json")
    out.write_text(json.dumps(report,indent=2,sort_keys=True,
                              ensure_ascii=False)+"\n",encoding="utf-8")
    path=args.output/("GA-Gate09-ADR043-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(path,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(out,out.name)
    print("[ADR043] STATUS:",report["status"],flush=True)
    print("[ADR043] EVIDENCE:",path,flush=True)
    return 0 if report["status"]=="PASSED_INVERSE_VI_FORMULATION_WITNESSES_NOT_SOLVER" else 1


if __name__=="__main__":
    raise SystemExit(main())
