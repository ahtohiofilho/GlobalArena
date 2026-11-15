#!/usr/bin/env python3
"""GA Gate09 R38: FIRST real numerical solver candidate, not diagnostics.

Coordinate exchange method: choose a worst F072 funded route by actual
weighted regret, transfer CONSERVATIVE producer mass to the frozen best
R14 route, fully reprice a bounded line search, accept only a strict
reduction in a predeclared *numerical* merit. Equal hard oracle budgets
versus original R21/R26 controls. Reject failures instead of inventing
a false convergence by shrinking steps to zero. F072 economy unchanged.
"""
from __future__ import annotations

import argparse
from datetime import datetime,timezone
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"r37"/"ci"))
import run_r37 as R37

R36=R37.R36
R35=R36.R35
R34=R36.R34
R33=R36.R33
R29=R36.R29
R21=R36.R21
R24=R36.R24
R26=R29.R26
from clearing_r5 import ClearingSettings,_flow_checksum,_change_metrics
from oracle_cache_r14 import evaluate_r14

METHODS=("coordinate_exchange","decay_sqrt","backtracking","progressive_global")
CANDIDATE="coordinate_exchange"
REGIMES=R34.REGIMES
CAPACITY_FACTORS=(0.20,1.0)
SIZES={"small":129,"medium":97}
ALPHAS=(0.50,0.25,0.125,0.0625)
DIRECTIONS_PER_STEP=2
MAX_UPDATES=256
MERIT_WORST_WEIGHT=0.30
STRICT_MERIT_IMPROVEMENT=1e-12
TARGET=1e-4


def numerical_merit(obs):
    """Ranking criterion for numerical experiments, NOT a new market rule."""
    a,b=obs["relative_gap"],obs["max_group_gap"]
    if not all(math.isfinite(v) and v>=0 for v in (a,b)):
        raise AssertionError("Invalid F072 commercial regret")
    return a+MERIT_WORST_WEIGHT*b


def donor_directions(net,flows,obs,limit=DIRECTIONS_PER_STEP):
    """Choose concrete supplied columns with maximal frozen regret.

    A global best-response market may share destination with a current
    suboptimal ROUTE; canonical Column identity handles this correctly.
    """
    if type(limit) is not int or limit<1:
        raise ValueError("Invalid donor work limit")
    priced=[]
    for gid,group in sorted(net.groups.items()):
        optimum=obs["target"][gid]
        top=obs["groups"][gid]["score"]
        for column,mass in sorted(flows.items()):
            if column.group_id!=gid or mass<=0 or column==optimum:
                continue
            route_cost=sum(
                net.edges[e].congestion_cost(
                    obs["loads"].get(e,0.),4)+
                float(net.edges[e].weight_for(group.seller))
                for e in column.edges)
            actual=obs["prices"][
                (column.destination,group.commodity)]/(1+route_cost)
            impact=mass*max(0.,top-actual)
            if impact>0:
                priced.append({
                    "gid":gid,"source":column,"sink":optimum,
                    "source_amount":mass,"weighted_regret":impact,
                    "best_score":top,"actual_score":actual,
                })
    # At most one coordinate chosen from each group; top group regrets
    # are globally ranked, not randomized or modified economic tastes.
    priced.sort(key=lambda x:(
        -x["weighted_regret"],x["gid"],x["source"]))
    unique=[]
    chosen=set()
    for row in priced:
        if row["gid"] not in chosen:
            unique.append(row)
            chosen.add(row["gid"])
        if len(unique)>=limit:
            break
    return unique


def coordinate_trial(net,flows,obs,settings,remaining):
    """Full R14 trial calls limited to remaining budget, NO free probes."""
    if type(remaining) is not int or remaining<1:
        raise ValueError("No allowed oracle calls")
    directions=donor_directions(net,flows,obs)
    old_digest=_flow_checksum(flows)
    old_merit=numerical_merit(obs)
    if not directions:
        return None,obs,None,{
            "selected":False,"status":"NO_POSITIVE_REGRET_COORDINATE",
            "oracle_calls":0,"trials":[],"strict_merit_improved":False,
        }
    trials=[]
    for item in directions:
        for frac in ALPHAS:
            if len(trials)>=remaining:
                break
            moved=R35.transfer(
                net,flows,item["source"],item["sink"],
                item["source_amount"]*frac)
            priced=evaluate_r14(net,moved,settings,())
            R33.verify_price_law(net,priced)
            delta=_change_metrics(net,flows,moved)
            trial={
                "gid":item["gid"],
                "source_market":item["source"].destination,
                "target_market":item["sink"].destination,
                "source_route":list(item["source"].edges),
                "target_route":list(item["sink"].edges),
                "direction_original_weighted_regret":item["weighted_regret"],
                "fraction_of_source_mass":frac,
                "moved_mass":item["source_amount"]*frac,
                "merit":numerical_merit(priced),
                "global_gap":priced["relative_gap"],
                "worst_group_gap":priced["max_group_gap"],
                "flow_sha256":_flow_checksum(moved),
                "price_change":delta["max_relative_price_change"],
                "load_change":delta["max_relative_load_change"],
                "quantity_change":delta["max_relative_quantity_change"],
                "_flow":moved,"_obs":priced,"_delta":delta,
            }
            trials.append(trial)
        if len(trials)>=remaining:
            break
    if _flow_checksum(flows)!=old_digest:
        raise AssertionError("Trial changed upstream original flow")
    if not trials:
        raise AssertionError("Positive direction but no attempted candidate")
    best=min(trials,key=lambda row:(
        row["merit"],row["global_gap"],row["worst_group_gap"],
        row["gid"],-row["fraction_of_source_mass"]))
    accepted=best["merit"] < old_merit-STRICT_MERIT_IMPROVEMENT
    audit=[{k:v for k,v in row.items() if not k.startswith("_")}
           for row in trials]
    decision={
        "selected":accepted,
        "status":("STRICT_NUMERICAL_MERIT_REDUCTION" if accepted
                  else "STALLED_ALL_CANDIDATES_NONIMPROVING"),
        "oracle_calls":len(trials),
        "directions_considered":len(directions),
        "fraction_candidates_per_direction":list(ALPHAS),
        "strict_merit_improved":accepted,
        "old_merit":old_merit,"best_candidate_merit":best["merit"],
        "truncated_by_budget":len(trials)<len(directions)*len(ALPHAS),
        "trials":audit,
    }
    if accepted:
        decision["selected_group"]=best["gid"]
        decision["selected_alpha"]=best["fraction_of_source_mass"]
    return (best["_flow"],best["_obs"],best["_delta"],decision) if accepted else (
        None,obs,None,decision)


def incumbent_step(net,flows,obs,settings,method,step,remaining):
    """Replay ORIGINAL policies, with an explicit call budget upper bound."""
    if method not in METHODS or method==CANDIDATE:
        raise ValueError("Not an original policy")
    if method=="decay_sqrt" and remaining<1:
        return None
    if method=="backtracking" and remaining<4:
        # Original R21 can use 1..4 calls; do not overspend by guessing.
        return None
    if method=="progressive_global":
        if remaining<1:
            return None
        flow,observed,delta,chosen=R26.choose_progressive(
            net,flows,obs,settings,method,min(remaining,len(R26.ALPHAS)))
        return flow,observed,delta,{
            "selected":True,"status":"UNCHANGED_R26",
            "oracle_calls":chosen["candidate_count"],
            "selected_alpha":chosen["selected_alpha"],
            "trials":chosen["trials"],
        }
    flow,observed,alpha,calls,forced=R21.choose_step(
        net,flows,obs,settings,method,step)
    delta=_change_metrics(net,flows,flow)
    if calls>remaining:
        raise AssertionError("Original policy exceeded budget")
    return flow,observed,delta,{
        "selected":True,
        "status":"UNCHANGED_R21",
        "oracle_calls":calls,
        "selected_alpha":alpha,
        "forced_uphill_fallback":forced,
        "trials":[],
    }


def run_method(cfg,regime,factor,method,oracle_budget,audit_every=8):
    if (regime not in REGIMES or factor not in CAPACITY_FACTORS or
        method not in METHODS or type(oracle_budget) is not int or
        not 16<=oracle_budget<=2048 or type(audit_every) is not int or
        audit_every<1):
        raise ValueError("Unsupported solution trial configuration")
    net,flows,meta=R34.capacity_fixture(cfg,regime,factor)
    settings=ClearingSettings(exponent=cfg.exponent)
    obs=evaluate_r14(net,flows,settings,())
    used=1
    steps=0
    accepted=0
    stalled=0
    last_delta=None
    streak=0
    first_stable=None
    decisions=[]
    rows=[]
    financial=[]
    stop="MAX_UPDATES_OR_BUDGET"
    t0=perf_counter()
    while True:
        R33.verify_price_law(net,obs)
        stable=R24.numerical_pass(obs,last_delta)
        streak=streak+1 if stable else 0
        if streak>=3 and first_stable is None:
            first_stable=steps
        rows.append({
            "step":steps,
            "R14_calls_cumulative":used,
            "flow_sha256":_flow_checksum(flows),
            "relative_gap":obs["relative_gap"],
            "max_group_gap":obs["max_group_gap"],
            "numerical_merit":numerical_merit(obs),
            "relative_price_change":None if last_delta is None else
                last_delta["max_relative_price_change"],
            "relative_load_change":None if last_delta is None else
                last_delta["max_relative_load_change"],
            "relative_quantity_change":None if last_delta is None else
                last_delta["max_relative_quantity_change"],
            "five_joint_thresholds_met":stable,
            "consecutive_joint_passes":streak,
        })
        if steps%audit_every==0:
            R21.frozen_check(net,flows,obs)
            check=R21.settlement_check(net,flows)
            financial.append({"step":steps,**check})
        if first_stable is not None:
            stop="OBSERVED_THREE_CONSECUTIVE_FIVE_THRESHOLD_PASSES"
            break
        if used>=oracle_budget:
            stop="ORACLE_BUDGET_EXHAUSTED"
            break
        if steps>=MAX_UPDATES:
            stop="MAX_UPDATES_REACHED"
            break
        remaining=oracle_budget-used
        proposal=(coordinate_trial(net,flows,obs,settings,remaining)
                  if method==CANDIDATE else
                  incumbent_step(net,flows,obs,settings,method,steps+1,remaining))
        if proposal is None:
            stop="ORACLE_BUDGET_REMAINDER_BELOW_POLICY_MAX_CALLS"
            break
        next_flow,next_obs,delta,info=proposal
        calls=info["oracle_calls"]
        if calls<0 or calls>remaining:
            raise AssertionError("Work budget was exceeded")
        used+=calls
        decisions.append({
            "step_candidate":steps+1,
            "oracle_calls":calls,
            "decision":info,
        })
        if not info["selected"]:
            stalled+=1
            stop=info["status"]
            break
        if calls<1 or next_flow is None or delta is None:
            raise AssertionError("Committed a free or missing trial")
        if _flow_checksum(next_flow)==_flow_checksum(flows):
            raise AssertionError("Bogus accepted step had identical flow")
        accepted+=1
        flows,obs,last_delta=next_flow,next_obs,delta
        steps+=1
    if not financial or financial[-1]["step"]!=steps:
        R21.frozen_check(net,flows,obs)
        check=R21.settlement_check(net,flows)
        financial.append({"step":steps,**check})
    if used>oracle_budget:
        raise AssertionError("Oracle budget overspend")
    if financial[-1]["gross_micro"]!=financial[-1]["credits_micro"]:
        raise AssertionError("F084F violated")
    if method==CANDIDATE:
        if any(d["decision"]["selected"] and not
               d["decision"]["strict_merit_improved"] for d in decisions):
            raise AssertionError("Coordinate candidate accepted a non-improvement")
        chosen_merits=[x["numerical_merit"] for x in rows]
        if any(chosen_merits[i]>chosen_merits[i-1]+1e-12
               for i in range(1,len(chosen_merits))):
            raise AssertionError("Accepted numerical merit was not monotone")
    return {
        "method":method,"regime":regime,"capacity_factor":factor,
        "nodes":10*cfg.m*cfg.m+2,
        "fixture":meta,
        "maximum_oracle_budget":oracle_budget,
        "actual_oracle_calls":used,
        "accepted_updates":accepted,
        "stalled_or_rejected_steps":stalled,
        "stop_reason":stop,
        "first_three_joint_stable_step":first_stable,
        "original_five_thresholds_observed":first_stable is not None,
        "final_global_gap":obs["relative_gap"],
        "final_worst_group_gap":obs["max_group_gap"],
        "final_merit":numerical_merit(obs),
        "final_flow_sha256":_flow_checksum(flows),
        "first_flow_sha256":rows[0]["flow_sha256"],
        "original_r14_trials_including_discarded":used,
        "financial_audits":financial,"rows":rows,
        "decisions":decisions,
        "elapsed_ms_observational":round((perf_counter()-t0)*1000,3),
        "convergence_proven":False,
        "F059_policy_ratified":False,
        "CSharp_parity_proven":False,
        "production_approved":False,
    }


def comparative_decision(data):
    """No production GO without five-metric streak on EVERY target fixture."""
    fixtures={}
    for result in data:
        label=(result["nodes"],result["regime"],result["capacity_factor"])
        fixtures.setdefault(label,{})[result["method"]]=result
    comparisons=[]
    for (nodes,regime,factor),methods in sorted(fixtures.items()):
        if set(methods)!=set(METHODS):
            raise AssertionError("Unbalanced incumbent/candidate comparison")
        candidate=methods[CANDIDATE]
        controls=[methods[m] for m in METHODS if m!=CANDIDATE]
        if len({r["maximum_oracle_budget"] for r in methods.values()})!=1:
            raise AssertionError("Comparison has unequal R14 work ceilings")
        comparisons.append({
            "nodes":nodes,"regime":regime,"capacity_factor":factor,
            "oracle_budget":candidate["maximum_oracle_budget"],
            "candidate_oracle_calls":candidate["actual_oracle_calls"],
            "candidate_gap":candidate["final_global_gap"],
            "candidate_worst_gap":candidate["final_worst_group_gap"],
            "candidate_achieved_three_joint_stable":candidate[
                "original_five_thresholds_observed"],
            "baseline_best_global_gap":min(x["final_global_gap"]
                                           for x in controls),
            "baseline_best_worst_group_gap":min(x["final_worst_group_gap"]
                                               for x in controls),
            "candidate_better_than_best_baseline_global":(
                candidate["final_global_gap"]<
                min(x["final_global_gap"] for x in controls)),
            "candidate_better_than_best_baseline_worst":(
                candidate["final_worst_group_gap"]<
                min(x["final_worst_group_gap"] for x in controls)),
            "baseline_all_policy_summaries":[{
                "method":x["method"],
                "actual_oracle_calls":x["actual_oracle_calls"],
                "final_global_gap":x["final_global_gap"],
                "final_worst_group_gap":x["final_worst_group_gap"],
                "stable":x["original_five_thresholds_observed"],
            } for x in controls],
        })
    success=all(
        x["candidate_achieved_three_joint_stable"] and
        x["candidate_better_than_best_baseline_global"] and
        x["candidate_better_than_best_baseline_worst"]
        for x in comparisons)
    return {
        "fixtures":comparisons,
        "experimental_criterion_met":success,
        "candidate_decision":(
            "PROVISIONAL_GO_TO_MORE_VALIDATION" if success
            else "NO_GO_UNDER_PREDECLARED_ORACLE_BUDGET"),
        "unresolved_next_if_no_go":"R39_DIFFERENT_NUMERICAL_METHOD_FAMILY",
        "max_three_candidate_families_including_R38":True,
        "no_manipulation_of_price_demand_or_tariff":True,
        "economic_global_convergence_NOT_proven":True,
    }


def exercise(profile):
    plans={
        "smoke":(("small",33),),
        "ci":(("small",SIZES["small"]),("medium",SIZES["medium"])),
        "extended":(("small",385),("medium",257)),
    }
    if profile not in plans:
        raise ValueError("Unknown comparison profile")
    regimes=("original",) if profile=="smoke" else REGIMES
    factors=(1.0,) if profile=="smoke" else CAPACITY_FACTORS
    methods=METHODS if profile!="smoke" else (CANDIDATE,"decay_sqrt")
    results=[]
    for size,budget in plans[profile]:
        for regime in regimes:
            for factor in factors:
                for method in methods:
                    row=run_method(
                        R21.CASES[size],regime,factor,method,budget)
                    results.append(row)
                    print(
                        "[{}:{}:Kx{}:{}] oracles={}/{} steps={} "
                        "gap={:.7g} worst={:.7g} merit={:.7g} "
                        "jointStable={} stopped={}".format(
                            size,regime,factor,method,
                            row["actual_oracle_calls"],row["maximum_oracle_budget"],
                            row["accepted_updates"],row["final_global_gap"],
                            row["final_worst_group_gap"],row["final_merit"],
                            row["original_five_thresholds_observed"],
                            row["stop_reason"]),flush=True)
    if profile=="smoke":
        return {"cases":results,
                "aggregate":{"smoke_only":True,"runs":len(results)}}
    # Original unchanged reference replay: these policies continue with
    # the same target, alpha and full R14 revaluation on matched prefix.
    for policy in ("decay_sqrt","backtracking"):
        R24.check_r21_reference(R21.CASES["small"],policy,6)
    return {
        "cases":results,
        "aggregate":comparative_decision(results),
        "numeric_candidate_actually_implemented":True,
        "previous_r37_diagnostic_track_closed":True,
        "R20_price_and_transport_unchanged":True,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile",choices=("smoke","ci","extended"),
                        default="ci")
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":"GA_GATE09_R38_COORDINATE_SOLVER_MATCHED_ORACLE_BUDGET_V1",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),"python":platform.python_version(),
        "profile":args.profile,"status":"FAILED",
        "no_diagnostic_track_reopening":True,
        "financial_F059_ratified":False,
        "global_equilibrium_proven":False,
        "CSharp_production_parity_proven":False,
        "production_approved":False,
    }
    try:
        report["r20_manifest_sha256_verified"]=R21.validate_manifest()
        if report["r20_manifest_sha256_verified"]!=16:
            raise AssertionError("Original R20 source changed")
        report.update(exercise(args.profile))
        report["status"]="PASSED_NUMERICAL_METHOD_COMPARISON_CONTRACTS"
    except Exception as error:
        report["error"]=repr(error)
        report["traceback"]=traceback.format_exc()
        print("[R38] FAILED:",repr(error),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output=args.output/("GA-Gate09-R38-"+args.profile+"-"+stamp+".json")
    output.write_text(json.dumps(report,sort_keys=True,indent=2,
                                 ensure_ascii=False)+"\n",encoding="utf-8")
    z=args.output/("GA-Gate09-R38-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(z,"w",zipfile.ZIP_DEFLATED) as arc:
        arc.write(output,output.name)
    print("[R38] STATUS:",report["status"],flush=True)
    print("[R38] EVIDENCE:",z,flush=True)
    return 0 if report["status"]=="PASSED_NUMERICAL_METHOD_COMPARISON_CONTRACTS" else 1


if __name__=="__main__":
    raise SystemExit(main())
