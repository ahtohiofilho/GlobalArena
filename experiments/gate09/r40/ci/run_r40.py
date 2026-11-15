#!/usr/bin/env python3
"""GA Gate09 R40: THIRD and FINAL numerical solver family + architecture gate.

Entropic/proportional simultaneous ALL-COLUMN flow update, with a small
NUMERICAL full-support prior (physically reallocated group production):
  w_gc = [(1-rho)*x_gc + rho*supply_g/|C_g|]
          * exp(eta*(actual_frozen_score_gc/best_frozen_score_g - 1))
  next_gc = supply_g*w_gc/sum_C w_gc.
The support contains currently positive routes AND canonical R14 routes
for every reachable compatible market. Each eta candidate is FULLY
repriced by immutable original F-072 R14; no invented buyer funds or D.
The prior is numerical only, not a preference or economic allocation
rule. NO global convergence theorem is asserted for entropy here.

Same eight R38/R39 fixtures, same R14 ceilings and three frozen
incumbent controls. If this third solver family does not satisfy the
predeclared five-test streak and beat best controls, issue FINAL
REDESIGN_SOLVER_FORMULATION decision. No fourth candidate disguised as
a parameter sweep or reopening diagnostic R30-R37.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT / "r39" / "ci"))
import run_r39 as R39

R38=R39.R38
R34=R39.R34
R33=R39.R33
R24=R39.R24
R21=R39.R21
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum
from gate09 import Column
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14

CANDIDATE="full_support_entropic_proportional"
METHODS=(CANDIDATE, "decay_sqrt", "backtracking", "progressive_global")
REGIMES=R38.REGIMES
CAPACITY_FACTORS=R38.CAPACITY_FACTORS
SIZES=R38.SIZES
MAX_UPDATES=R38.MAX_UPDATES
ETAS=(2.0,8.0,24.0,48.0)
NUMERICAL_SUPPORT_PRIOR=0.04
STRICT_MERIT_IMPROVEMENT=1e-12
FAMILY_NUMBER=3
MAX_FAMILIES=3
FINAL_NO_GO="NO_GO_THREE_SOLVER_FAMILIES_REDESIGN_SOLVER_FORMULATION"
EXPERIMENTAL_GO="PROVISIONAL_GO_TO_INDEPENDENT_SOLVER_VALIDATION"


def full_support_proportional_update(net,flows,obs,exponent,eta,
                                     prior=NUMERICAL_SUPPORT_PRIOR):
    """Deterministic positive per-group simplex reallocation, one frozen score.

    All original currently funded routes and the single canonical
    shortest path of each priced reachable market are supported.
    The KL-prox-like exponential step uses seller-specific actual
    generalized route cost. A strictly positive prior reuses that
    producer's existing supply (not newly created mass). This is a
    tested regularized numerical map, not pure unregularized mirror
    descent or an alternative definition of competitive equilibrium.

    Returns the candidate and SHORT-PATH scans done OUTSIDE original
    R14, billed separately from the subsequent full R14 trial.
    """
    if (not isinstance(eta,(int,float)) or
        not math.isfinite(eta) or eta<=0 or
        not isinstance(prior,(int,float)) or
        not math.isfinite(prior) or not (0<prior<1)):
        raise ValueError("Positive finite entropy parameters required")
    original_hash=_flow_checksum(flows)
    cache=FrozenRouteCache(net,obs["loads"],exponent)
    out={}
    stats={"groups":0,"supported_columns":0,"new_canonical_columns":0,
           "numerical_prior_fraction":prior,
           "eta":float(eta),
           "min_trial_column_quantity":None}
    for gid,group in sorted(net.groups.items()):
        distances,paths,ways=cache.paths(group.origin,group.seller)
        weights=cache.weights(group.seller)
        existing={col for col,mass in flows.items()
                  if col.group_id==gid and mass>0}
        supported=set(existing)
        for market in sorted(net.markets):
            if market[1]!=group.commodity or market[0] not in paths:
                continue
            if not math.isfinite(obs["prices"][market]):
                raise AssertionError("R14 returned an unpriced reachable market")
            candidate=Column(gid,market[0],paths[market[0]])
            net.validate_column(candidate)
            supported.add(candidate)
        if not supported or not existing:
            raise AssertionError("Positive funded columns and reachable markets required")
        cols=sorted(supported)
        highest=obs["groups"][gid]["score"]
        if not math.isfinite(highest) or highest<=0:
            raise AssertionError("Invalid original F072 group optimum")
        nominal_floor=prior*group.supply/len(cols)
        unscaled=[]
        for col in cols:
            physical_cost=sum(weights[eid] for eid in col.edges)
            actual=obs["prices"][(col.destination,group.commodity)]/(
                1+physical_cost)
            if not math.isfinite(actual) or actual<=0:
                raise AssertionError("A funded route had no finite score")
            if actual > highest+1e-10*max(1.,highest):
                raise AssertionError("Alternative route beats original R14 best")
            frozen_mass=(1-prior)*flows.get(col,0.)+nominal_floor
            # The exponent is non-positive, guarding against overflow.
            modifier=math.exp(eta*(actual/highest-1))
            if modifier<=0:
                raise AssertionError("Positive canonical floor underflow")
            weight=frozen_mass*modifier
            if weight<=0 or not math.isfinite(weight):
                raise AssertionError("Entropic column weight not positive finite")
            unscaled.append((col,weight))
        denominator=math.fsum(weight for _,weight in unscaled)
        if not denominator>0 or not math.isfinite(denominator):
            raise AssertionError("Cannot normalize producer simplex")
        for col,weight in unscaled:
            amount=group.supply*(weight/denominator)
            if amount<=0:
                raise AssertionError("True market mass became zero")
            out[col]=amount
        # Exact same supply-correction convention as F072 _mix.
        correction=group.supply-sum(out[col] for col in cols)
        leading=max(cols,key=lambda col:(out[col],-col.destination,
                                         tuple(-x for x in col.edges)))
        out[leading]+=correction
        if any(out[col]<=0 for col in cols):
            raise AssertionError("Normalization lost real funded production")
        stats["groups"]+=1
        stats["supported_columns"]+=len(cols)
        stats["new_canonical_columns"]+=len(supported-existing)
        local_min=min(out[col] for col in cols)
        stats["min_trial_column_quantity"]=(
            local_min if stats["min_trial_column_quantity"] is None
            else min(stats["min_trial_column_quantity"],local_min))
    if _flow_checksum(flows)!=original_hash:
        raise AssertionError("Numeric iteration mutated the frozen source")
    loads,quantities,prices=net.snapshots(out)
    if any(not math.isfinite(p) or p<=0 for p in prices.values()):
        raise AssertionError("Candidate created an unpriced product market")
    stats["independent_shortest_path_scans"]=cache.counters()[
        "shortest_path_scans"]
    stats["all_markets_price_positive"]=True
    stats["candidate_flow_sha256"]=_flow_checksum(out)
    stats["no_new_supply_or_demand"]=True
    return out,stats


def entropic_trial(net,flows,obs,settings,remaining):
    """Charge each R14 candidate. Prefer strict improvement, else explicit fallback.

    The selection criterion is R38's *numerical* M=globalGap+.3*worstGap;
    it is neither new price/demand nor player trade preference.
    """
    if type(remaining) is not int or remaining<1:
        raise ValueError("Must have at least one R14 call to propose a step")
    before=_flow_checksum(flows)
    old_merit=R38.numerical_merit(obs)
    old_gap=obs["relative_gap"]
    sampled=[]
    winner=None
    scans=0
    for eta in ETAS[:min(len(ETAS),remaining)]:
        moved,work=full_support_proportional_update(
            net,flows,obs,settings.exponent,eta)
        scans+=work["independent_shortest_path_scans"]
        priced=evaluate_r14(net,moved,settings,())
        R33.verify_price_law(net,priced)
        delta=_change_metrics(net,flows,moved)
        merit=R38.numerical_merit(priced)
        record={
            "eta":eta,
            "merit":merit,
            "relative_gap":priced["relative_gap"],
            "worst_group_gap":priced["max_group_gap"],
            "flow_sha256":_flow_checksum(moved),
            "relative_price_change":delta["max_relative_price_change"],
            "relative_load_change":delta["max_relative_load_change"],
            "relative_quantity_change":delta["max_relative_quantity_change"],
            "strict_numerical_improvement":(
                merit<old_merit-STRICT_MERIT_IMPROVEMENT),
            "nonworsening_original_global_gap":(
                priced["relative_gap"]<=old_gap+STRICT_MERIT_IMPROVEMENT),
            "candidate_has_positive_real_mass_in_all_supported_columns":True,
            "candidate_independent_shortest_path_scans":work[
                "independent_shortest_path_scans"],
            "candidate_supported_columns":work["supported_columns"],
            "candidate_new_canonical_columns":work["new_canonical_columns"],
            "_flow":moved,"_obs":priced,"_delta":delta,
        }
        sampled.append(record)
        if (record["strict_numerical_improvement"] and
            record["nonworsening_original_global_gap"] and
            record["flow_sha256"]!=before):
            winner=record
            break
    if not sampled:
        raise AssertionError("No paid entropic proposal")
    selected=(winner if winner is not None else min(
        sampled,key=lambda rec:(rec["merit"],rec["relative_gap"],
                                rec["worst_group_gap"],rec["eta"])))
    has_moved=selected["flow_sha256"]!=before
    if _flow_checksum(flows)!=before:
        raise AssertionError("Oracle candidate mutated original flows")
    diagnostics=[{k:v for k,v in r.items() if not k.startswith("_")}
                 for r in sampled]
    metadata={
        "selected":has_moved,
        "status":(
            "STRICT_ENTROPIC_MERIT_IMPROVEMENT" if winner is not None
            else "BEST_REPRICED_ENTROPIC_FALLBACK" if has_moved
            else "STALLED_WITH_NONZERO_COMMERCIAL_GAP"),
        "oracle_calls":len(sampled),
        "candidate_R14_calls":len(sampled),
        "independent_shortest_path_scans":scans,
        "prior":NUMERICAL_SUPPORT_PRIOR,
        "chosen_eta":selected["eta"],
        "accepted_early":winner is not None,
        "fallback":winner is None,
        "old_merit":old_merit,
        "selected_merit":selected["merit"],
        "strict_merit_improved":selected["strict_numerical_improvement"],
        "all_candidates_full_R14_repricing":True,
        "trials":diagnostics,
    }
    return (selected["_flow"] if has_moved else None,
            selected["_obs"] if has_moved else obs,
            selected["_delta"] if has_moved else None,
            metadata)


def run_candidate(cfg,regime,factor,oracle_budget,audit_every=8):
    if (regime not in REGIMES or factor not in CAPACITY_FACTORS or
        type(oracle_budget) is not int or
        not 16<=oracle_budget<=2048 or
        type(audit_every) is not int or audit_every<1):
        raise ValueError("Unsupported third-family solution experiment")
    net,flows,fixture=R34.capacity_fixture(cfg,regime,factor)
    settings=ClearingSettings(exponent=cfg.exponent)
    obs=evaluate_r14(net,flows,settings,())
    evaluations=1
    steps=0
    last_delta=None
    stable_streak=0
    first_stable=None
    rows=[]
    financial=[]
    decisions=[]
    line_search_fallbacks=0
    scans=0
    t0=perf_counter()
    stop="UNSPECIFIED"
    while True:
        R33.verify_price_law(net,obs)
        stable_now=R24.numerical_pass(obs,last_delta)
        stable_streak=stable_streak+1 if stable_now else 0
        if stable_streak>=3 and first_stable is None:
            first_stable=steps
        rows.append({
            "step":steps,
            "R14_oracles_used":evaluations,
            "flow_sha256":_flow_checksum(flows),
            "global_relative_gap":obs["relative_gap"],
            "worst_group_gap":obs["max_group_gap"],
            "numerical_merit":R38.numerical_merit(obs),
            "relative_price_change":None if last_delta is None else
                last_delta["max_relative_price_change"],
            "relative_load_change":None if last_delta is None else
                last_delta["max_relative_load_change"],
            "relative_quantity_change":None if last_delta is None else
                last_delta["max_relative_quantity_change"],
            "all_five_original_thresholds_met":stable_now,
            "consecutive_joint_thresholds":stable_streak,
        })
        if steps%audit_every==0:
            R21.frozen_check(net,flows,obs)
            result=R21.settlement_check(net,flows)
            financial.append({"step":steps,**result})
        if first_stable is not None:
            stop="THREE_CONSECUTIVE_ORIGINAL_FIVE_METRIC_PASSES"
            break
        if evaluations>=oracle_budget:
            stop="R14_BUDGET_EXHAUSTED"
            break
        if steps>=MAX_UPDATES:
            stop="MAX_UPDATES_REACHED"
            break
        successor,scored,delta,record=entropic_trial(
            net,flows,obs,settings,oracle_budget-evaluations)
        used=record["oracle_calls"]
        if not 1<=used<=oracle_budget-evaluations:
            raise AssertionError("Oracle overspend or free candidate")
        evaluations+=used
        scans+=record["independent_shortest_path_scans"]
        decisions.append({
            "step_candidate":steps+1,
            "R14_calls":used,
            "decision":record,
        })
        line_search_fallbacks+=int(record["fallback"])
        if not record["selected"]:
            stop=record["status"]
            break
        if successor is None or delta is None:
            raise AssertionError("Missing accepted experimental flow")
        if _flow_checksum(successor)==_flow_checksum(flows):
            raise AssertionError("Identical flow cannot be accepted")
        steps+=1
        flows,obs,last_delta=successor,scored,delta
    if not financial or financial[-1]["step"]!=steps:
        R21.frozen_check(net,flows,obs)
        result=R21.settlement_check(net,flows)
        financial.append({"step":steps,**result})
    if evaluations!=1+sum(row["R14_calls"] for row in decisions):
        raise AssertionError("Missing or extra R14 calls")
    if evaluations>oracle_budget:
        raise AssertionError("R14 budget violated")
    if financial[-1]["gross_micro"]!=financial[-1]["credits_micro"]:
        raise AssertionError("F084F failed")
    return {
        "method":CANDIDATE,"regime":regime,"capacity_factor":factor,
        "nodes":10*cfg.m*cfg.m+2,
        "fixture":fixture,
        "maximum_oracle_budget":oracle_budget,
        "actual_oracle_calls":evaluations,
        "accepted_updates":steps,
        "fallback_trials":line_search_fallbacks,
        "stop_reason":stop,
        "first_three_joint_stable_step":first_stable,
        "original_five_thresholds_observed":first_stable is not None,
        "final_global_gap":obs["relative_gap"],
        "final_worst_group_gap":obs["max_group_gap"],
        "final_merit":R38.numerical_merit(obs),
        "final_flow_sha256":_flow_checksum(flows),
        "first_flow_sha256":rows[0]["flow_sha256"],
        "original_R14_calls_including_discarded":evaluations,
        "additional_independent_shortest_path_scans":scans,
        "financial_audits":financial,"rows":rows,"decisions":decisions,
        "elapsed_ms_observational":round((perf_counter()-t0)*1000,3),
        "numerical_family":FAMILY_NUMBER,
        "no_unproven_mirror_descent_convergence_theorem":True,
        "global_equilibrium_proven":False,
        "F059_financial_policy_ratified":False,
        "CSharp_kernel_parity_proven":False,
        "production_approved":False,
    }


def architecture_decision(records):
    """Binding THIRD-FAMILY decision. Never silently open R41 fourth solver.

    GO requires every fixture to meet three consecutive ORIGINAL
    five-threshold updated snapshots AND beat best available baseline
    independently in global and worst-group gaps. The controls retain
    original R21/R26 logic and a matched HARD R14 oracle ceiling.
    """
    by_fixture={}
    for row in records:
        key=(row["nodes"],row["regime"],row["capacity_factor"])
        methods=by_fixture.setdefault(key,{})
        if row["method"] in methods:
            raise AssertionError("Duplicate fixture/method")
        methods[row["method"]]=row
    decisions=[]
    for (nodes,regime,capacity),methods in sorted(by_fixture.items()):
        if set(methods)!=set(METHODS):
            raise AssertionError("Incomplete candidate/control fixture")
        if len({r["maximum_oracle_budget"] for r in methods.values()})!=1:
            raise AssertionError("Unequal original oracle budgets")
        if len({r["first_flow_sha256"] for r in methods.values()})!=1:
            raise AssertionError("Different initial allocations")
        if len({r["fixture"]["actual_fixture_fingerprint"]
                for r in methods.values()})!=1:
            raise AssertionError("Different economic fixtures")
        candidate=methods[CANDIDATE]
        control=[methods[m] for m in METHODS if m!=CANDIDATE]
        global_best=min(r["final_global_gap"] for r in control)
        worst_best=min(r["final_worst_group_gap"] for r in control)
        decisions.append({
            "nodes":nodes,"regime":regime,"capacity_factor":capacity,
            "r14_call_ceiling_each":candidate["maximum_oracle_budget"],
            "r40_used_r14_calls":candidate["actual_oracle_calls"],
            "r40_global_gap":candidate["final_global_gap"],
            "r40_worst_group_gap":candidate["final_worst_group_gap"],
            "r40_stability_achieved":candidate["original_five_thresholds_observed"],
            "best_original_control_global_gap":global_best,
            "best_original_control_worst_gap":worst_best,
            "r40_beats_best_global":candidate["final_global_gap"]<global_best,
            "r40_beats_best_worst":candidate["final_worst_group_gap"]<worst_best,
            "reference_controls":[{
                "method":r["method"],"used_r14_calls":r["actual_oracle_calls"],
                "global_gap":r["final_global_gap"],
                "worst_gap":r["final_worst_group_gap"],
                "stable_original_gate":r["original_five_thresholds_observed"]}
                for r in control],
        })
    success=bool(decisions) and all(
        v["r40_stability_achieved"] and v["r40_beats_best_global"]
        and v["r40_beats_best_worst"] for v in decisions)
    return {
        "fixtures":decisions,
        "fixture_count":len(decisions),
        "final_solver_family_count":MAX_FAMILIES,
        "R38_family_decision":"NO_GO",
        "R39_family_decision":"NO_GO",
        "R40_candidate_decision":(
            "PROVISIONAL_GO_TO_FURTHER_SOLVER_VALIDATION" if success
            else "NO_GO_UNDER_PREDECLARED_ORACLE_BUDGET"),
        "final_architecture_decision":(
            "SELECT_R40_FOR_SEPARATE_VALIDATION_NOT_PRODUCTION" if success
            else FINAL_NO_GO),
        "next_workflow_if_final_no_go":(
            "DESIGN_REVIEW_EQUILIBRIUM_AND_NUMERICAL_SOLVER_FORMULATION"
            if not success else
            "INDEPENDENT_SOLVER_VALIDATION_F059_SEPARATELY"),
        "no_fourth_numerical_candidate_authorized":True,
        "diagnostics_R30_R37_not_reopened":True,
        "adopt_change_to_canonical_price_or_demand":False,
        "change_authoritative_CSharp_or_Godot":False,
        "financial_F059_ratified":False,
        "global_existence_uniqueness_convergence_proven":False,
        "production_approved":False,
        "no_automatic_merge":True,
    }


def exercise(profile):
    plans={
        "smoke":(("small",33),),
        "ci":(("small",SIZES["small"]),("medium",SIZES["medium"])),
        "extended":(("small",385),("medium",257)),
    }
    if profile not in plans:
        raise ValueError("Unknown experimental work profile")
    regimes=("original",) if profile=="smoke" else REGIMES
    factors=(1.0,) if profile=="smoke" else CAPACITY_FACTORS
    methods=(CANDIDATE,"decay_sqrt") if profile=="smoke" else METHODS
    cases=[]
    for label,budget in plans[profile]:
        for regime in regimes:
            for factor in factors:
                for method in methods:
                    case=(run_candidate(
                        R21.CASES[label],regime,factor,budget)
                        if method==CANDIDATE else R38.run_method(
                            R21.CASES[label],regime,factor,method,budget))
                    cases.append(case)
                    print(
                        "[{}:{}:Kx{}:{}] calls={}/{} steps={} "
                        "gap={:.7g} worst={:.7g} stable={} stop={}".format(
                            label,regime,factor,method,
                            case["actual_oracle_calls"],case["maximum_oracle_budget"],
                            case["accepted_updates"],case["final_global_gap"],
                            case["final_worst_group_gap"],
                            case["original_five_thresholds_observed"],
                            case["stop_reason"]),flush=True)
    if profile=="smoke":
        return {"cases":cases,
                "aggregate":{"smoke_only":True,"runs":len(cases)}}
    for policy in ("decay_sqrt","backtracking"):
        R24.check_r21_reference(R21.CASES["small"],policy,6)
    return {
        "cases":cases,
        "aggregate":architecture_decision(cases),
        "three_family_solver_search_closed":True,
        "third_solver_is_real_numeric_update_not_diagnostic":True,
        "original_R20_R39_economics_untouched":True,
    }


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--profile",choices=("smoke","ci","extended"),default="ci")
    p.add_argument("--output",type=Path,default=Path.cwd())
    args=p.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    output={
        "schema":"GA_GATE09_R40_FINAL_ENTROPIC_SOLVER_ARCHITECTURE_DECISION_V1",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),"python":platform.python_version(),
        "profile":args.profile,"status":"FAILED",
        "max_solver_families_predeclared":MAX_FAMILIES,
        "fourth_candidate_not_authorized":True,
        "financial_F059_ratified":False,
        "global_equilibrium_proven":False,
        "production_approved":False,
    }
    try:
        output["r20_manifest_sha256_verified"]=R21.validate_manifest()
        if output["r20_manifest_sha256_verified"]!=16:
            raise AssertionError("Original R20 source drift")
        output.update(exercise(args.profile))
        output["status"]="PASSED_FINAL_THIRD_FAMILY_VALIDATION_AND_ARCH_DECISION"
    except Exception as exc:
        output["error"]=repr(exc)
        output["traceback"]=traceback.format_exc()
        print("[R40] FAILED:",repr(exc),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    json_path=args.output/("GA-Gate09-R40-"+args.profile+"-"+stamp+".json")
    json_path.write_text(json.dumps(
        output,sort_keys=True,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    archive=args.output/("GA-Gate09-R40-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(json_path,json_path.name)
    print("[R40] STATUS:",output["status"],flush=True)
    print("[R40] EVIDENCE:",archive,flush=True)
    if output["status"].startswith("PASSED") and args.profile!="smoke":
        print("[R40] ARCHITECTURE_DECISION:",
              output["aggregate"]["final_architecture_decision"],flush=True)
    return 0 if output["status"]=="PASSED_FINAL_THIRD_FAMILY_VALIDATION_AND_ARCH_DECISION" else 1


if __name__=="__main__":
    raise SystemExit(main())
