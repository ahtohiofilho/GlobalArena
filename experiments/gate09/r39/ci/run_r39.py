#!/usr/bin/env python3
"""GA Gate09 R39: SECOND solver family, two-stage best-response extragradient.

PREDICT y = mix(x, F072_best(x), alpha); reprice y using ORIGINAL R14.
CORRECT z = mix(x, F072_best(y), alpha); reprice z using ORIGINAL R14.
Select z by an explicit numerical merit; NEVER commit y and NEVER use
hybrid prices. This is an extragradient-STYLE lookahead operator,
NOT a proof that F072 is a monotone VI or Korpelevich theorem applies.
Matched HARD R14 oracle ceilings against untouched R21/R26 controls.
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

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"r38"/"ci"))
import run_r38 as R38

R37=R38.R37
R34=R38.R34
R33=R38.R33
R21=R38.R21
R24=R38.R24
R26=R38.R26
from clearing_r5 import ClearingSettings, _mix, _change_metrics, _flow_checksum
from oracle_cache_r14 import evaluate_r14

CANDIDATE="predictor_corrector_best_response"
METHODS=(CANDIDATE,"decay_sqrt","backtracking","progressive_global")
ALPHAS=(0.25,0.125,0.0625,0.03125)
WORK_PER_TRIAL=2
STRICT_NUMERICAL_TOL=1e-12
TARGET=1e-4
REGIMES=R38.REGIMES
CAPACITY_FACTORS=R38.CAPACITY_FACTORS
SIZES=R38.SIZES
MAX_UPDATES=R38.MAX_UPDATES
FAMILY_NUMBER=2
MAX_FAMILIES=3


def predictor_corrector_trial(net,flows,obs,settings,remaining):
    """Paid, exact R14 forecast and correction; no use of stale route data.

    Both stages are full R14 states. BOTH moves start at x, not y,
    distinguishing the corrected update from two chained R21 steps.
    Accept first strict improvement in M AND nonworsening global gap.
    If none: commit the best already-priced nonzero candidate, visibly
    flagging any uphill fallback. This prevents fake convergence by
    freezing a nonstationary flow.
    """
    if type(remaining) is not int or remaining<WORK_PER_TRIAL:
        raise ValueError("Need at least two full R14 oracles per trial")
    old_hash=_flow_checksum(flows)
    old_merit=R38.numerical_merit(obs)
    old_gap=obs["relative_gap"]
    trials=[]
    first_acceptable=None
    for alpha in ALPHAS:
        if (len(trials)+1)*WORK_PER_TRIAL>remaining:
            break
        predictor=_mix(flows,net,obs["target"],alpha)
        pred_obs=evaluate_r14(net,predictor,settings,())
        R33.verify_price_law(net,pred_obs)
        # True simultaneous correction: start from OLD x and apply T(y)
        # rather than following the predictor or mixing with T(x).
        correction=_mix(flows,net,pred_obs["target"],alpha)
        corrected_obs=evaluate_r14(net,correction,settings,())
        R33.verify_price_law(net,corrected_obs)
        movement=_change_metrics(net,flows,correction)
        merit=R38.numerical_merit(corrected_obs)
        proposal={
            "alpha":alpha,
            "predictor_flow_sha256":_flow_checksum(predictor),
            "predictor_best_target_sha256":R38.R29.target_signature(
                pred_obs["target"]),
            "predictor_gap":pred_obs["relative_gap"],
            "predictor_worst_gap":pred_obs["max_group_gap"],
            "correction_flow_sha256":_flow_checksum(correction),
            "corrector_best_target_sha256":R38.R29.target_signature(
                corrected_obs["target"]),
            "corrected_gap":corrected_obs["relative_gap"],
            "corrected_worst_gap":corrected_obs["max_group_gap"],
            "merit":merit,
            "max_relative_price_change":movement["max_relative_price_change"],
            "max_relative_load_change":movement["max_relative_load_change"],
            "max_relative_quantity_change":movement["max_relative_quantity_change"],
            "forecast_and_correction_are_two_distinct_R14_oracle_calls":True,
            "prediction_only_never_committed":True,
            "strict_improvement":merit<old_merit-STRICT_NUMERICAL_TOL,
            "nonworsening_original_global_F072_gap":(
                corrected_obs["relative_gap"]<=old_gap+STRICT_NUMERICAL_TOL),
            "_flow":correction,"_obs":corrected_obs,"_delta":movement,
        }
        trials.append(proposal)
        if (proposal["strict_improvement"] and
            proposal["nonworsening_original_global_F072_gap"] and
            proposal["correction_flow_sha256"]!=old_hash):
            first_acceptable=proposal
            break
    if not trials:
        raise AssertionError("Unpaid predictor-corrector stage")
    selected=(first_acceptable if first_acceptable is not None else min(
        trials,key=lambda x:(
            x["merit"],x["corrected_gap"],x["corrected_worst_gap"],
            -x["alpha"])))
    if _flow_checksum(flows)!=old_hash:
        raise AssertionError("Prediction mutated original allocation")
    audit=[{key:value for key,value in row.items() if not key.startswith("_")}
           for row in trials]
    accepted=selected["correction_flow_sha256"]!=old_hash
    if first_acceptable is not None and not accepted:
        raise AssertionError("Accepted zero movement")
    outcome={
        "selected":accepted,
        "status":(
            "STRICT_PREDICTOR_CORRECTOR_IMPROVEMENT" if first_acceptable is not None
            else "BEST_EVALUATED_FALLBACK_UPHILL_ALLOWED" if accepted
            else "STALLED_IDENTICAL_CORRECTION_WITH_NONZERO_REGRET"),
        "oracle_calls":WORK_PER_TRIAL*len(trials),
        "predictor_oracle_calls":len(trials),
        "corrector_oracle_calls":len(trials),
        "strict_improvement":selected["strict_improvement"],
        "old_global_gap":old_gap,
        "old_merit":old_merit,
        "accepted_early":first_acceptable is not None,
        "fallback":first_acceptable is None,
        "selected_alpha":selected["alpha"],
        "selected_corrected_gap":selected["corrected_gap"],
        "selected_merit":selected["merit"],
        "candidates_tested":len(trials),
        "selected_prediction_hash":selected["predictor_flow_sha256"],
        "selected_correction_hash":selected["correction_flow_sha256"],
        "all_candidate_stages_are_fully_repriced":True,
        "no_vi_monotonicity_theorem_assumed":True,
        "trials":audit,
    }
    return (
        selected["_flow"] if accepted else None,
        selected["_obs"] if accepted else obs,
        selected["_delta"] if accepted else None,
        outcome,
    )


def run_candidate(cfg,regime,factor,oracle_budget,audit_every=8):
    if (regime not in REGIMES or factor not in CAPACITY_FACTORS or
        type(oracle_budget) is not int or not 16<=oracle_budget<=2048 or
        type(audit_every) is not int or audit_every<1):
        raise ValueError("Unregistered solver trial configuration")
    net,flows,meta=R34.capacity_fixture(cfg,regime,factor)
    settings=ClearingSettings(exponent=cfg.exponent)
    obs=evaluate_r14(net,flows,settings,())
    calls=1
    steps=0
    streak=0
    first_stable=None
    last_delta=None
    rows=[]
    audits=[]
    decisions=[]
    early=0
    fallback=0
    stalled=0
    t0=perf_counter()
    reason="UNSPECIFIED"
    while True:
        R33.verify_price_law(net,obs)
        passed=R24.numerical_pass(obs,last_delta)
        streak=streak+1 if passed else 0
        if streak>=3 and first_stable is None:
            first_stable=steps
        rows.append({
            "step":steps,
            "cumulative_R14_calls":calls,
            "flow_sha256":_flow_checksum(flows),
            "relative_gap":obs["relative_gap"],
            "max_group_gap":obs["max_group_gap"],
            "merit":R38.numerical_merit(obs),
            "max_relative_price_change":None if last_delta is None else
                last_delta["max_relative_price_change"],
            "max_relative_load_change":None if last_delta is None else
                last_delta["max_relative_load_change"],
            "max_relative_quantity_change":None if last_delta is None else
                last_delta["max_relative_quantity_change"],
            "five_original_stability_metrics_met":passed,
            "consecutive_checks":streak,
        })
        if steps%audit_every==0:
            R21.frozen_check(net,flows,obs)
            financial=R21.settlement_check(net,flows)
            audits.append({"step":steps,**financial})
        if first_stable is not None:
            reason="THREE_CONSECUTIVE_FIVE_METRIC_CHECKPOINTS"
            break
        if calls>=oracle_budget:
            reason="ORACLE_BUDGET_EXHAUSTED"
            break
        if steps>=MAX_UPDATES:
            reason="MAX_UPDATES_REACHED"
            break
        remaining=oracle_budget-calls
        if remaining<WORK_PER_TRIAL:
            reason="REMAINDER_TOO_SMALL_FOR_TWO_STAGE_CANDIDATE"
            break
        next_flow,next_obs,delta,decision=predictor_corrector_trial(
            net,flows,obs,settings,remaining)
        n=decision["oracle_calls"]
        if not 2<=n<=remaining or n%2:
            raise AssertionError("Incomplete or unbilled predictor-corrector evaluation")
        calls+=n
        decisions.append({
            "candidate_update":steps+1,
            "charged_R14_calls":n,
            "decision":decision,
        })
        early+=int(decision["accepted_early"])
        fallback+=int(decision["fallback"])
        if not decision["selected"]:
            stalled+=1
            reason=decision["status"]
            break
        if (next_flow is None or delta is None or
            _flow_checksum(next_flow)==_flow_checksum(flows)):
            raise AssertionError("Attempted a nonphysical accepted step")
        steps+=1
        flows,obs,last_delta=next_flow,next_obs,delta
    if not audits or audits[-1]["step"]!=steps:
        R21.frozen_check(net,flows,obs)
        financial=R21.settlement_check(net,flows)
        audits.append({"step":steps,**financial})
    if calls>oracle_budget or calls != 1+sum(
            record["charged_R14_calls"] for record in decisions):
        raise AssertionError("R14 call accounting drift")
    if audits[-1]["gross_micro"]!=audits[-1]["credits_micro"]:
        raise AssertionError("F084F settlement conservation failed")
    if any(record["decision"]["fallback"] and
           not record["decision"]["selected"] and
           record["decision"]["status"]!="STALLED_IDENTICAL_CORRECTION_WITH_NONZERO_REGRET"
           for record in decisions):
        raise AssertionError("Silent fallback rejection")
    return {
        "method":CANDIDATE,
        "regime":regime,"capacity_factor":factor,
        "nodes":10*cfg.m*cfg.m+2,
        "fixture":meta,
        "maximum_oracle_budget":oracle_budget,
        "actual_oracle_calls":calls,
        "accepted_updates":steps,
        "accepted_early_improvement_steps":early,
        "fallback_attempts":fallback,
        "stalled_or_rejected_steps":stalled,
        "stop_reason":reason,
        "first_three_joint_stable_step":first_stable,
        "original_five_thresholds_observed":first_stable is not None,
        "final_global_gap":obs["relative_gap"],
        "final_worst_group_gap":obs["max_group_gap"],
        "final_merit":R38.numerical_merit(obs),
        "final_flow_sha256":_flow_checksum(flows),
        "first_flow_sha256":rows[0]["flow_sha256"],
        "total_r14_evaluations_including_discarded":calls,
        "predictor_R14_calls":sum(
            d["decision"]["predictor_oracle_calls"] for d in decisions),
        "corrector_R14_calls":sum(
            d["decision"]["corrector_oracle_calls"] for d in decisions),
        "financial_audits":audits,
        "rows":rows,
        "decisions":decisions,
        "elapsed_ms_observational":round((perf_counter()-t0)*1000,3),
        "numerical_family":FAMILY_NUMBER,
        "finite_budget_no_vi_theorem":True,
        "global_convergence_proven":False,
        "F059_policy_ratified":False,
        "CSharp_parity_proven":False,
        "production_approved":False,
    }


def comparative_decision(records):
    grouped={}
    for result in records:
        key=(result["nodes"],result["regime"],result["capacity_factor"])
        bucket=grouped.setdefault(key,{})
        if result["method"] in bucket:
            raise AssertionError("Duplicate solver/fixture result")
        bucket[result["method"]]=result
    results=[]
    for (nodes,regime,cap),by_method in sorted(grouped.items()):
        if set(by_method)!=set(METHODS):
            raise AssertionError("Missing reference policy or new candidate")
        if len({x["maximum_oracle_budget"] for x in by_method.values()})!=1:
            raise AssertionError("Unmatched R14 oracle ceiling")
        initial={x["first_flow_sha256"] for x in by_method.values()}
        fixture={x["fixture"]["actual_fixture_fingerprint"]
                 for x in by_method.values()}
        if len(initial)!=1 or len(fixture)!=1:
            raise AssertionError("Unmatched initial economy")
        candidate=by_method[CANDIDATE]
        controls=[by_method[m] for m in METHODS if m!=CANDIDATE]
        best_global=min(x["final_global_gap"] for x in controls)
        best_worst=min(x["final_worst_group_gap"] for x in controls)
        record={
            "nodes":nodes,"regime":regime,"capacity_factor":cap,
            "maximum_R14_calls_per_method":candidate["maximum_oracle_budget"],
            "candidate_used_R14_calls":candidate["actual_oracle_calls"],
            "candidate_global_gap":candidate["final_global_gap"],
            "candidate_worst_group_gap":candidate["final_worst_group_gap"],
            "candidate_stability_achieved":candidate[
                "original_five_thresholds_observed"],
            "best_control_global_gap":best_global,
            "best_control_worst_group_gap":best_worst,
            "candidate_improves_global":candidate["final_global_gap"]<best_global,
            "candidate_improves_worst":candidate["final_worst_group_gap"]<best_worst,
            "controls":[
                {"method":x["method"],"used_calls":x["actual_oracle_calls"],
                 "gap":x["final_global_gap"],"worst":x["final_worst_group_gap"],
                 "five_metric_streak":x["original_five_thresholds_observed"]}
                for x in controls],
        }
        results.append(record)
    success=bool(results) and all(
        x["candidate_stability_achieved"] and
        x["candidate_improves_global"] and x["candidate_improves_worst"]
        for x in results)
    return {
        "fixture_decisions":results,
        "candidate_decision":(
            "PROVISIONAL_GO_TO_MORE_VALIDATION" if success
            else "NO_GO_UNDER_PREDECLARED_ORACLE_BUDGET"),
        "passes_every_fixture_including_five_original_gates":success,
        "candidate_beats_best_global_in_fixture_count":sum(
            x["candidate_improves_global"] for x in results),
        "candidate_beats_best_worst_in_fixture_count":sum(
            x["candidate_improves_worst"] for x in results),
        "candidate_reaches_five_original_gates_in_fixture_count":sum(
            x["candidate_stability_achieved"] for x in results),
        "fixture_count":len(results),
        "solver_families_evaluated":2,
        "maximum_distinct_solver_families_before_architecture_decision":MAX_FAMILIES,
        "next_if_no_go":"R40_LAST_DISTINCT_SOLVER_FAMILY_THEN_ARCHITECTURE_DECISION",
        "comparison_uses_same_oracle_ceiling_not_identical_actual_calls":True,
        "no_economic_policy_change":True,
        "global_existence_convergence_uniqueness_proven":False,
    }


def exercise(profile):
    plans={
        "smoke":(("small",33),),
        "ci":(("small",SIZES["small"]),("medium",SIZES["medium"])),
        "extended":(("small",385),("medium",257)),
    }
    if profile not in plans:
        raise ValueError("Unknown R39 work profile")
    regimes=("original",) if profile=="smoke" else REGIMES
    capacities=(1.0,) if profile=="smoke" else CAPACITY_FACTORS
    methods=(CANDIDATE,"decay_sqrt") if profile=="smoke" else METHODS
    cases=[]
    for size,budget in plans[profile]:
        for regime in regimes:
            for factor in capacities:
                for method in methods:
                    record=(
                        run_candidate(R21.CASES[size],regime,factor,budget)
                        if method==CANDIDATE else
                        R38.run_method(
                            R21.CASES[size],regime,factor,method,budget))
                    cases.append(record)
                    print(
                        "[{}:{}:Kx{}:{}] calls={}/{} steps={} "
                        "gap={:.7g} worst={:.7g} stable={} stop={}".format(
                            size,regime,factor,method,
                            record["actual_oracle_calls"],
                            record["maximum_oracle_budget"],
                            record["accepted_updates"],
                            record["final_global_gap"],record["final_worst_group_gap"],
                            record["original_five_thresholds_observed"],
                            record["stop_reason"]),flush=True)
    if profile=="smoke":
        return {"cases":cases,
                "aggregate":{"smoke_only":True,"runs":len(cases)}}
    for policy in ("decay_sqrt","backtracking"):
        R24.check_r21_reference(R21.CASES["small"],policy,6)
    # Prior unchanged R38 experimental candidate remains reproducible,
    # but is not counted as an original R21/R26 control.
    return {
        "cases":cases,
        "aggregate":comparative_decision(cases),
        "true_two_stage_repriced_method_implemented":True,
        "diagnostic_track_from_R37_remains_closed":True,
        "R20_price_transport_tariff_unchanged":True,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile",choices=("smoke","ci","extended"),
                        default="ci")
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":"GA_GATE09_R39_TWO_STAGE_PREDICTOR_CORRECTOR_BUDGETED_V1",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),"python":platform.python_version(),
        "profile":args.profile,"status":"FAILED",
        "solver_family_number":FAMILY_NUMBER,
        "no_claim_of_classical_extragradient_convergence_theorem":True,
        "global_equilibrium_proven":False,
        "financial_F059_ratified":False,
        "production_approved":False,
    }
    try:
        report["r20_manifest_sha256_verified"]=R21.validate_manifest()
        if report["r20_manifest_sha256_verified"]!=16:
            raise AssertionError("R20 baseline was changed")
        report.update(exercise(args.profile))
        report["status"]="PASSED_PREDICTOR_CORRECTOR_NUMERICAL_COMPARISON_CONTRACTS"
    except Exception as error:
        report["error"]=repr(error)
        report["traceback"]=traceback.format_exc()
        print("[R39] FAILED:",repr(error),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output=args.output/("GA-Gate09-R39-"+args.profile+"-"+stamp+".json")
    output.write_text(json.dumps(report,sort_keys=True,indent=2,
                                 ensure_ascii=False)+"\n",encoding="utf-8")
    archive=args.output/("GA-Gate09-R39-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as z:
        z.write(output,output.name)
    print("[R39] STATUS:",report["status"],flush=True)
    print("[R39] EVIDENCE:",archive,flush=True)
    return 0 if report["status"]=="PASSED_PREDICTOR_CORRECTOR_NUMERICAL_COMPARISON_CONTRACTS" else 1


if __name__=="__main__":
    raise SystemExit(main())
