#!/usr/bin/env python3
"""GA Gate09 R37 - FINAL bounded envelope diagnostic and solver decision bridge.

R37 closes (does not perpetuate) R30-R36 market-switch attribution.
For actual A->B winners in inherited F-072 steps, reproduce the ORIGINAL
entire market ranking at a sparse accepted-step grid plus the R36
24-bisection bracket. Counterfactuals are read-only. The output provides
a hard transition to R38 NUMERICAL SOLUTION EXPERIMENTS, NOT new diagnosis.
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

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "r36" / "ci"))
import run_r36 as R36

R35 = R36.R35
R34 = R36.R34
R33 = R36.R33
R31 = R36.R31
R29 = R36.R29
R21 = R36.R21
R24 = R36.R24

from clearing_r5 import ClearingSettings, _flow_checksum, _mix
from gate09 import Column
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14

REGIMES = R36.REGIMES
CAPACITY_FACTORS = R36.CAPACITY_FACTORS
POLICIES = R36.POLICIES
TRANSITIONS_PER_RUN = R36.TRANSITIONS_PER_TRAJECTORY
GROUPS_PER_TRANSITION = R36.GROUPS_PER_TRANSITION
BISECTION_STEPS = R36.BISECTION_ITERATIONS
COARSE_T = (0.0, .125, .25, .375, .5, .625, .75, .875, 1.0)
GLOBAL_TIE_RATIO = 1e-10
DECISION = "CLOSE_DIAGNOSTIC_TRACK_START_R38_SOLVER_EXPERIMENTS"


def full_envelope_rank(net, obs, settings, gid, A, B):
    """Exact original F072 ordering of ALL commodity-compatible destinations.

    Score and deterministic (-score, Column) sorting replay unchanged
    R14; the original group tie flag also admits multiple shortest
    canonical paths (not only close best/runner-up destination scores).
    """
    group = net.groups[gid]
    cache = FrozenRouteCache(net, obs["loads"], settings.exponent)
    dist, paths, ways = cache.paths(group.origin, group.seller)
    all_options = []
    for market in sorted(net.markets):
        if market[1] != group.commodity or market[0] not in paths:
            continue
        score = obs["prices"][market] / (1 + dist[market[0]])
        all_options.append(
            (score, Column(gid, market[0], paths[market[0]]),
             ways[market[0]]))
    all_options.sort(key=lambda row: (-row[0], row[1]))
    if len(all_options) < 2:
        raise AssertionError("Full-envelope witness requires alternatives")
    score, best_col, best_route_count = all_options[0]
    if best_col != obs["target"][gid] or score != obs["groups"][gid]["score"]:
        raise AssertionError("Full market rank diverged from immutable R14")
    tol = GLOBAL_TIE_RATIO * max(1., abs(score))
    computed_tie = best_route_count > 1 or any(
        abs(score-row[0]) <= tol for row in all_options[1:])
    if computed_tie != (gid in obs["tie_groups"]):
        raise AssertionError("F072 original global tie group flag mismatch")
    by_dest = {col.destination: (value, col) for value, col, _ in all_options}
    if A not in by_dest or B not in by_dest or A==B:
        raise AssertionError("Actual switched destinations are missing")
    pa, pb = by_dest[A][0], by_dest[B][0]
    pair_near = abs(pa-pb) <= tol
    pair_global = pair_near and max(pa,pb) >= score-tol
    runner_score, runner_col, _ = all_options[1]
    return {
        "group": gid,
        "global_winner_market": best_col.destination,
        "global_winner_route": list(best_col.edges),
        "global_winner_score": score,
        "runner_up_market": runner_col.destination,
        "runner_up_route": list(runner_col.edges),
        "runner_up_score": runner_score,
        "top_runner_up_relative_margin": (score-runner_score)/score,
        "top_runner_up_within_original_tie_threshold": (
            abs(score-runner_score) <= tol),
        "r14_group_tie_flag": computed_tie,
        "r14_winning_route_multiplicity_gt1": best_route_count > 1,
        "score_gap_threshold_original_r14": tol,
        "old_market_score": pa, "new_market_score": pb,
        "old_market_route": list(by_dest[A][1].edges),
        "new_market_route": list(by_dest[B][1].edges),
        "old_new_pair_near_tie": pair_near,
        "old_new_pair_compete_for_global_top": pair_global,
        "pair_near_tie_but_shadowed_by_third": pair_near and not pair_global,
        "third_market_wins": best_col.destination not in (A,B),
        "all_compatible_markets": [
            {"market": col.destination, "score": s,
             "canonical_route": list(col.edges),
             "is_best": idx==0,
             "near_best_original_threshold": abs(score-s)<=tol}
            for idx, (s,col,_) in enumerate(all_options)
        ],
        "independent_route_scans": cache.counters()["shortest_path_scans"],
        "replays_original_complete_R14_ranking": True,
    }


def classify_envelope(sampled, A, B):
    """Descriptive sampled sequence, not unobserved continuous envelope proof."""
    points = sorted(sampled, key=lambda a: a["t"])
    if not points or points[0]["t"] != 0 or points[-1]["t"] != 1:
        raise AssertionError("Envelope must cover both real endpoints")
    winners = [row["ranking"]["global_winner_market"] for row in points]
    if winners[0] != A or winners[-1] != B:
        raise AssertionError("Global original market winners did not replay")
    compressed = [winners[0]]
    for x in winners[1:]:
        if x != compressed[-1]:
            compressed.append(x)
    third = sorted(set(compressed)-{A,B})
    class_name = ("A_to_B_direct_in_sample" if compressed==[A,B]
                  else "A_via_third_to_B_in_sample" if third
                  else "A_B_reentry_in_sample")
    if class_name == "A_B_reentry_in_sample" and len(compressed) <= 2:
        raise AssertionError("Bogus reentry class")
    return {
        "sampled_winner_sequence":compressed,
        "sampled_global_ranking_transitions":len(compressed)-1,
        "sampled_distinct_third_winner_markets":third,
        "sampled_topology_class":class_name,
        "sampled_any_third_market_winner":bool(third),
        "sampled_any_pair_tie_shadowed_by_third":any(
            point["ranking"]["pair_near_tie_but_shadowed_by_third"]
            for point in points),
        "sampled_any_globally_competitive_AB_tie":any(
            point["ranking"]["old_new_pair_compete_for_global_top"]
            for point in points),
        "sampled_any_R14_global_tie_flag":any(
            point["ranking"]["r14_group_tie_flag"] for point in points),
        "sampled_near_global_top2":sum(
            point["ranking"]["top_runner_up_within_original_tie_threshold"]
            for point in points),
        "sampled_min_first_second_margin":min(
            point["ranking"]["top_runner_up_relative_margin"] for point in points),
        "sampled_old_market_path_switches":len({
            tuple(point["ranking"]["old_market_route"]) for point in points
        })>1,
        "sampled_new_market_path_switches":len({
            tuple(point["ranking"]["new_market_route"]) for point in points
        })>1,
        "sampling_not_exhaustive_no_absence_certification":True,
    }


def envelope_samples(net, old_flows, old_obs, new_flows, new_obs,
                     settings, alpha, bracket, pair):
    """One full market R14 pass per new fraction; endpoints already priced.

    Sample sparse 1/8 fraction grid and both bracket endpoints+center+best.
    Re-evaluate no original R14 snapshots at t=0/1; count all extra
    oracle calls and independent shortest scans honestly.
    """
    A,B = pair["old_destination"], pair["new_destination"]
    gid = pair["group"]
    original_hash = _flow_checksum(old_flows)
    accepted_hash = _flow_checksum(new_flows)
    if _flow_checksum(_mix(
            old_flows,net,old_obs["target"],alpha)) != accepted_hash:
        raise AssertionError("Counterfactual grid does not replay original step")
    lo = bracket["interval_start_fraction"]
    hi = bracket["interval_end_fraction"]
    close = bracket["closest_sample_fraction"]
    if not (0<=lo<=hi<=1 and 0<=close<=1):
        raise AssertionError("Root bracket outside original accepted step")
    if hi-lo > 2**(-BISECTION_STEPS)+1e-15:
        raise AssertionError("Bracket resolution unexpectedly weak")
    points = sorted(set(COARSE_T + (lo,(lo+hi)/2,hi,close)))
    audits = []
    extra_calls = 0
    independent_scans = 0
    for t in points:
        if t==0:
            flow,obs = old_flows,old_obs
        elif t==1:
            flow,obs = new_flows,new_obs
        else:
            flow = _mix(old_flows,net,old_obs["target"],alpha*t)
            obs = evaluate_r14(net,flow,settings,())
            R33.verify_price_law(net,obs)
            extra_calls += 1
        ranked = full_envelope_rank(net,obs,settings,gid,A,B)
        independent_scans += ranked["independent_route_scans"]
        audits.append({
            "t":t,
            "original_accepted_alpha_fraction":t,
            "flow_sha256":_flow_checksum(flow),
            "extra_r14_calls":int(t not in (0,1)),
            "ranking":ranked,
        })
    if (_flow_checksum(old_flows)!=original_hash or
        _flow_checksum(new_flows)!=accepted_hash):
        raise AssertionError("Envelope altered the original economic state")
    outcome = classify_envelope(audits,A,B)
    return {
        "group":gid,"old_market":A,"new_market":B,
        "R36_bracket_left":lo,"R36_bracket_right":hi,
        "R36_bracket_width":hi-lo,
        "sampled_points":len(audits),
        "extra_r14_oracles":extra_calls,
        "independent_route_scans":independent_scans,
        "result":outcome,
        "samples":audits,
        "hybrid_counterfactual_is_never_committed":True,
        "does_not_certify_no_unseen_market_switches":True,
    }


def inspect_transition(net, old_flows, old_obs, new_flows, new_obs,
                       settings, alpha, step):
    transitions = R31.transition_crossings(
        net,old_obs,new_obs,settings.exponent)
    pairs = sorted(transitions["pairs"],key=lambda x:(
        min(abs(x["before_log_advantage_new_over_old"]),
            abs(x["after_log_advantage_new_over_old"])),x["group"]))
    if not pairs:
        raise AssertionError("Selected update has no real market switch")
    records = []
    for pair in pairs[:GROUPS_PER_TRANSITION]:
        bracket = R36.crossing_bracket(
            net,old_flows,old_obs,new_flows,new_obs,settings,alpha,pair)
        envelope = envelope_samples(
            net,old_flows,old_obs,new_flows,new_obs,settings,
            alpha,bracket,pair)
        records.append({
            "group":pair["group"],
            "bracket":bracket,
            "full_market_envelope":envelope,
        })
    return {
        "step":step,
        "original_market_switches_at_step":transitions["market_switches"],
        "selected_group_count":len(records),
        "selected_by_nearest_endpoint_ranking_margin":True,
        "records":records,
        "pair_bracket_r14_calls":sum(
            x["bracket"]["extra_r14_oracle_calls"] for x in records),
        "full_envelope_r14_calls":sum(
            x["full_market_envelope"]["extra_r14_oracles"] for x in records),
        "independent_shortest_path_scans":(
            transitions["extra_shortest_path_scans"]+
            sum(x["bracket"]["independent_shortest_path_scans"]+
                x["full_market_envelope"]["independent_route_scans"]
                for x in records)),
        "independent_root_r13_parity_checks":len(records),
        "root_f084f_audits":len(records),
    }


def run_case(cfg,regime,factor,policy,steps,audit_every=8):
    if (regime not in REGIMES or factor not in CAPACITY_FACTORS or
        policy not in POLICIES or type(steps) is not int or
        not 1<=steps<=256 or type(audit_every) is not int or audit_every<1):
        raise ValueError("Unsupported F072 diagnostic configuration")
    net,flows,fixture=R34.capacity_fixture(cfg,regime,factor)
    settings=ClearingSettings(exponent=cfg.exponent)
    obs=evaluate_r14(net,flows,settings,())
    original_calls=1
    bracket_calls=envelope_calls=route_scans=0
    rows,audits,events=[],[],[]
    last_delta=None
    streak=0
    first_stable=None
    total_market_switches=0
    t0=perf_counter()
    for step in range(steps+1):
        if step%audit_every==0 or step==steps:
            R21.frozen_check(net,flows,obs)
            finance=R21.settlement_check(net,flows)
            audits.append({"step":step,**finance})
        R33.verify_price_law(net,obs)
        candidate=R24.numerical_pass(obs,last_delta)
        streak=streak+1 if candidate else 0
        if streak>=3 and first_stable is None:
            first_stable=step
        rows.append({
            "step":step,
            "flow_sha256":_flow_checksum(flows),
            "relative_gap":obs["relative_gap"],
            "max_group_gap":obs["max_group_gap"],
            "max_relative_price_change":None if last_delta is None else
                last_delta["max_relative_price_change"],
            "max_relative_load_change":None if last_delta is None else
                last_delta["max_relative_load_change"],
            "max_relative_quantity_change":None if last_delta is None else
                last_delta["max_relative_quantity_change"],
            "joint_five_conditions":candidate,
            "consecutive_stability":streak,
            "original_policy_oracle_calls_cumulative":original_calls,
        })
        if step==steps:
            break
        old_flow,old_obs=flows,obs
        successor,successor_obs,last_delta,used,alpha=R29.step_forward(
            net,flows,obs,settings,policy,step+1)
        if used<1:
            raise AssertionError("Policy work was not billed")
        original_calls+=used
        switched=sum(
            old_obs["target"][gid].destination!=
            successor_obs["target"][gid].destination
            for gid in net.groups)
        total_market_switches+=switched
        if switched and len(events)<TRANSITIONS_PER_RUN:
            event=inspect_transition(
                net,old_flow,old_obs,successor,successor_obs,
                settings,alpha,step+1)
            bracket_calls+=event["pair_bracket_r14_calls"]
            envelope_calls+=event["full_envelope_r14_calls"]
            route_scans+=event["independent_shortest_path_scans"]
            events.append(event)
        flows,obs=successor,successor_obs
    if audits[-1]["step"]!=steps:
        raise AssertionError("Final conservation witness not generated")
    records=[record for event in events for record in event["records"]]
    selected=len(records)
    return {
        "regime":regime,"capacity_factor":factor,"policy":policy,
        "nodes":10*cfg.m*cfg.m+2,"steps":steps,
        "fixture":fixture,
        "initial_flow_sha256":rows[0]["flow_sha256"],
        "final_flow_sha256":rows[-1]["flow_sha256"],
        "final_gap":rows[-1]["relative_gap"],
        "final_worst_group_gap":rows[-1]["max_group_gap"],
        "first_three_joint_stable_step":first_stable,
        "total_market_switches":total_market_switches,
        "sampled_transitions":len(events),
        "sampled_pairs":selected,
        "sampled_A_via_C_to_B":sum(
            r["full_market_envelope"]["result"][
                "sampled_topology_class"]=="A_via_third_to_B_in_sample"
            for r in records),
        "sampled_A_B_reentries":sum(
            r["full_market_envelope"]["result"][
                "sampled_topology_class"]=="A_B_reentry_in_sample"
            for r in records),
        "sampled_third_market_winner":sum(
            r["full_market_envelope"]["result"][
                "sampled_any_third_market_winner"] for r in records),
        "sampled_pair_tie_shadowed_by_third":sum(
            r["full_market_envelope"]["result"][
                "sampled_any_pair_tie_shadowed_by_third"] for r in records),
        "sampled_globally_competitive_AB_ties":sum(
            r["full_market_envelope"]["result"][
                "sampled_any_globally_competitive_AB_tie"] for r in records),
        "sampled_old_path_switch":sum(
            r["full_market_envelope"]["result"][
                "sampled_old_market_path_switches"] for r in records),
        "sampled_new_path_switch":sum(
            r["full_market_envelope"]["result"][
                "sampled_new_market_path_switches"] for r in records),
        "original_policy_R14_calls":original_calls,
        "R36_reused_bracket_extra_R14_calls":bracket_calls,
        "R37_full_envelope_extra_R14_calls":envelope_calls,
        "total_R14_calls":original_calls+bracket_calls+envelope_calls,
        "independent_shortest_path_scans":route_scans,
        "independent_root_R13_parity_checks":selected,
        "root_F084F_financial_audits":selected,
        "rows":rows,"events":events,"financial_audits":audits,
        "elapsed_ms_observational":round((perf_counter()-t0)*1000,3),
        "chosen_groups_not_random_or_exhaustive":True,
        "diagnostic_track_closed":True,
        "decision":DECISION,
        "F059_financial_ties_ratified":False,
        "global_equilibrium_proven":False,
        "production_approved":False,
    }


def aggregate(records):
    return {
        "runs":len(records),
        "original_market_switches":sum(r["total_market_switches"] for r in records),
        "selected_pairs":sum(r["sampled_pairs"] for r in records),
        "selected_transitions":sum(r["sampled_transitions"] for r in records),
        "sampled_via_third":sum(r["sampled_A_via_C_to_B"] for r in records),
        "sampled_A_B_reentries":sum(r["sampled_A_B_reentries"] for r in records),
        "sampled_third_winners":sum(r["sampled_third_market_winner"] for r in records),
        "sampled_shadowed_pair_ties":sum(r["sampled_pair_tie_shadowed_by_third"] for r in records),
        "sampled_competitive_AB_ties":sum(r["sampled_globally_competitive_AB_ties"] for r in records),
        "sampled_old_market_path_changes":sum(r["sampled_old_path_switch"] for r in records),
        "sampled_new_market_path_changes":sum(r["sampled_new_path_switch"] for r in records),
        "original_policy_R14_calls":sum(r["original_policy_R14_calls"] for r in records),
        "R36_bracket_R14_calls":sum(r["R36_reused_bracket_extra_R14_calls"] for r in records),
        "R37_envelope_R14_calls":sum(r["R37_full_envelope_extra_R14_calls"] for r in records),
        "independent_route_scans":sum(r["independent_shortest_path_scans"] for r in records),
        "stable_runs":sum(r["first_three_joint_stable_step"] is not None for r in records),
        "research_decision":DECISION,
        "numeric_solver_candidates_next_not_diagnostics":True,
        "sampled_paths_do_not_exclude_unsampled_envelope_switches":True,
    }


def exercise(profile):
    if profile=="smoke":
        plans=(("small",6),)
        regimes=("original",)
        factors=(1.0,)
        policies=("decay_sqrt",)
    elif profile=="ci":
        plans=(("small",24),("medium",12))
        regimes=REGIMES
        factors=CAPACITY_FACTORS
        policies=POLICIES
    elif profile=="extended":
        plans=(("small",48),("medium",24),("large",10))
        regimes=REGIMES
        factors=CAPACITY_FACTORS
        policies=POLICIES
    else:
        raise ValueError("Unknown test profile")
    cases=[]
    for size,steps in plans:
        for regime in regimes:
            for factor in factors:
                for policy in policies:
                    record=run_case(
                        R21.CASES[size],regime,factor,policy,steps)
                    cases.append(record)
                    print(
                        "[{}:{}:Kx{}:{}] switches={} selected={} viaThird={} "
                        "reentry={} pairShadowed={} competitiveAB={} "
                        "policyR14={} bracketR14={} envelopeR14={} scans={} "
                        "gap={:.6g} stable={}".format(
                            size,regime,factor,policy,
                            record["total_market_switches"],
                            record["sampled_pairs"],
                            record["sampled_A_via_C_to_B"],
                            record["sampled_A_B_reentries"],
                            record["sampled_pair_tie_shadowed_by_third"],
                            record["sampled_globally_competitive_AB_ties"],
                            record["original_policy_R14_calls"],
                            record["R36_reused_bracket_extra_R14_calls"],
                            record["R37_full_envelope_extra_R14_calls"],
                            record["independent_shortest_path_scans"],
                            record["final_gap"],
                            record["first_three_joint_stable_step"]),flush=True)
    if profile!="smoke":
        pilot=run_case(
            R21.CASES["small"],"original",1.0,"decay_sqrt",5,audit_every=2)
        previous=R36.run_case(
            R21.CASES["small"],"original",1.0,"decay_sqrt",5,audit_every=2)
        if (pilot["final_flow_sha256"]!=previous["final_flow_sha256"] or
            pilot["final_gap"]!=previous["final_global_gap"] or
            pilot["original_policy_R14_calls"]!=previous["original_policy_r14_calls"] or
            pilot["sampled_pairs"]!=previous["selected_producer_crossings"]):
            raise AssertionError("R36 trajectory or crossing population changed")
        for policy in POLICIES:
            R24.check_r21_reference(R21.CASES["small"],policy,6)
        short=run_case(
            R21.CASES["small"],"original",.2,"decay_sqrt",4,audit_every=2)
        long=run_case(
            R21.CASES["small"],"original",.2,"decay_sqrt",7,audit_every=2)
        if short["rows"]!=long["rows"][:5]:
            raise AssertionError("Inconsistent prefix with noncommitted probes")
    return {
        "cases":cases,"aggregate":aggregate(cases),
        "diagnostic_track_closed":True,
        "next_gate_is_numerical_solver_intervention_not_diagnostic_expansion":True,
        "R20_R36_economic_law_unchanged":True,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile",choices=("smoke","ci","extended"),
                        default="ci")
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":"GA_GATE09_R37_ENVELOPE_CLOSURE_AND_R38_DECISION_V1",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),
        "python":platform.python_version(),
        "profile":args.profile,
        "status":"FAILED",
        "diagnostic_track_closed":True,
        "next_step":"R38_COMPETITIVE_NUMERICAL_METHOD_CANDIDATE",
        "global_equilibrium_proven":False,
        "F059_financial_policy_ratified":False,
        "CSharp_integrated_parity_proven":False,
        "production_approved":False,
    }
    try:
        report["r20_manifest_sha256_verified"]=R21.validate_manifest()
        if report["r20_manifest_sha256_verified"]!=16:
            raise AssertionError("R20 baseline source altered")
        report.update(exercise(args.profile))
        report["status"]="PASSED_FINAL_DIAGNOSTIC_CONTRACTS_R38_READY"
    except Exception as error:
        report["error"]=repr(error)
        report["traceback"]=traceback.format_exc()
        print("[R37] FAILED:",repr(error),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output=args.output/("GA-Gate09-R37-"+args.profile+"-"+stamp+".json")
    output.write_text(json.dumps(report,sort_keys=True,indent=2,
                                 ensure_ascii=False)+"\n",encoding="utf-8")
    zip_path=args.output/("GA-Gate09-R37-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(zip_path,"w",zipfile.ZIP_DEFLATED) as archive:
        archive.write(output,output.name)
    print("[R37] STATUS:",report["status"],flush=True)
    print("[R37] EVIDENCE:",zip_path,flush=True)
    return 0 if report["status"]=="PASSED_FINAL_DIAGNOSTIC_CONTRACTS_R38_READY" else 1


if __name__=="__main__":
    raise SystemExit(main())
