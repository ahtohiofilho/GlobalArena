#!/usr/bin/env python3
"""GA Gate 09 R36 — bracket a REAL F-072 market ranking inversion.

For chosen producer groups whose BEST destination actually changed in
an original R21/R26 step, bisect the score difference of the OLD and
NEW destinations on the SAME accepted F-072 update segment. Every
midpoint is repriced by the unchanged R14 oracle and is NEVER committed.
A bracket is not a proof of monotonicity, uniqueness, causal mechanism,
global equilibrium, or F-059 policy ratification.
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
sys.path.insert(0, str(ROOT / "r35" / "ci"))
import run_r35 as R35

R34 = R35.R34
R33 = R35.R33
R31 = R35.R31
R29 = R35.R29
R21 = R35.R21
R24 = R35.R24
from clearing_r5 import ClearingSettings, _flow_checksum, _mix
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14

REGIMES = R34.REGIMES
CAPACITY_FACTORS = (0.20, 0.40, 1.0)
POLICIES = ("decay_sqrt", "backtracking")
TRANSITIONS_PER_TRAJECTORY = 2
GROUPS_PER_TRANSITION = 2
BISECTION_ITERATIONS = 24
ENDPOINT_SIGN_EPS = 1e-11
ROOT_SIGN_DIAGNOSTIC = 1e-8


def market_pair_snapshot(net, obs, settings, gid, old_market, new_market):
    """Two fixed DESTINATIONS, with current cheapest canonical route each."""
    cache = FrozenRouteCache(net, obs["loads"], settings.exponent)
    pair = R31.market_pair_at_snapshot(
        net, obs, cache, gid, old_market, new_market)
    top = obs["groups"][gid]["score"]
    scale = 1e-10*max(1.0, abs(top))
    pair_tie = abs(pair["new_score"]-pair["old_score"]) <= scale
    pair_reaches_top = max(
        pair["new_score"],pair["old_score"]) >= top-scale
    if max(pair["new_score"],pair["old_score"]) > top+1e-11:
        raise AssertionError("F-072 global winner underperformed observed pair")
    return {
        "pair": pair,
        "original_r14_best_market": obs["target"][gid].destination,
        "original_r14_best_score": top,
        "pair_near_tie_using_exact_r14_score_threshold": pair_tie,
        "both_pair_markets_competitive_for_global_best": (
            pair_tie and pair_reaches_top),
        "original_r14_group_has_tie_flag": gid in obs["tie_groups"],
        "pair_tied_but_global_best_is_third": (
            pair_tie and not pair_reaches_top),
        "extra_shortest_path_scans": cache.counters()["shortest_path_scans"],
    }


def crossing_bracket(net, old_flows, old_obs, new_flows, new_obs,
                     settings, accepted_alpha, pair, iterations=BISECTION_ITERATIONS):
    """Deterministic interval subdivision along one ALREADY accepted step.

    Continuity of the TWO fixed market scores holds while their Q are
    positive and shortest-route costs are finite (minimum of finitely
    many continuous path costs). The winning market/route selector may
    be discontinuous. Do not claim a unique zero or monotonic ranking.
    """
    if type(iterations) is not int or not 1 <= iterations <= 40:
        raise ValueError("Bisection work limit outside approved range")
    if not (0 < accepted_alpha <= 1):
        raise ValueError("Cannot interpolate an invalid accepted alpha")
    baseline_hash = _flow_checksum(old_flows)
    accepted_hash = _flow_checksum(new_flows)
    replay = _mix(old_flows, net, old_obs["target"], accepted_alpha)
    if _flow_checksum(replay) != accepted_hash:
        raise AssertionError("Not a genuine frozen R21/R26 accepted segment")
    gid = pair["group"]
    old_dest, new_dest = pair["old_destination"], pair["new_destination"]
    if (old_obs["target"][gid].destination != old_dest or
            new_obs["target"][gid].destination != new_dest or
            old_dest == new_dest):
        raise AssertionError("Not an observed market-winner inversion")
    left_sign = pair["before_log_advantage_new_over_old"]
    right_sign = pair["after_log_advantage_new_over_old"]
    if (left_sign > ENDPOINT_SIGN_EPS or
            right_sign < -ENDPOINT_SIGN_EPS):
        raise AssertionError("Pair scores do not bracket a winner reversal")
    if not math.isfinite(left_sign) or not math.isfinite(right_sign):
        raise AssertionError("Nonfinite ranking advantage")

    bounds = [0.0, 1.0]
    last_left, last_right = left_sign, right_sign
    traces = []
    scans = 0
    # Unlike a 'zero is automatically an F-059 tie' shortcut, this
    # evaluates the R14 highest of ALL markets at each midpoint.
    best_absolute = None
    best_t = None
    best_flow = None
    for i in range(iterations):
        t = (bounds[0]+bounds[1])/2
        candidate = _mix(
            old_flows, net, old_obs["target"], accepted_alpha*t)
        observed = evaluate_r14(net, candidate, settings, ())
        if observed["reachable_unpriced"]:
            raise AssertionError("An intermediate commercial market became unpriced")
        R33.verify_price_law(net, observed)
        point = market_pair_snapshot(
            net, observed, settings, gid, old_dest, new_dest)
        scans += point["extra_shortest_path_scans"]
        slope = point["pair"]["log_advantage_new_over_old"]
        if not math.isfinite(slope):
            raise AssertionError("Infinite intermediate pair score")
        if best_absolute is None or abs(slope) < best_absolute:
            best_absolute = abs(slope)
            best_t = t
            best_flow = candidate
        row = {
            "iteration": i+1,
            "fraction_of_accepted_update": t,
            "flow_sha256": _flow_checksum(candidate),
            "log_score_new_market_over_old": slope,
            "relative_price_log": point["pair"][
                "log_relative_price_new_over_old"],
            "relative_transport_log": point["pair"][
                "log_relative_transport_new_over_old"],
            "old_market_canonical_route": point["pair"]["old_path"],
            "new_market_canonical_route": point["pair"]["new_path"],
            "pair_score_tie_with_original_r14_threshold": point[
                "pair_near_tie_using_exact_r14_score_threshold"],
            "pair_is_globally_competitive_tie": point[
                "both_pair_markets_competitive_for_global_best"],
            "r14_f059_group_flag_present": point[
                "original_r14_group_has_tie_flag"],
            "third_market_has_strictly_better_score_at_pair_tie": point[
                "pair_tied_but_global_best_is_third"],
            "actual_best_market_among_all_candidates": point[
                "original_r14_best_market"],
            "extra_r14_oracle_calls": 1,
        }
        traces.append(row)
        # Pure score-sign subdivision, NEVER use the full best-target
        # discontinuity as if it were the continuously varying score.
        if slope >= 0:
            bounds[1] = t
            last_right = slope
        else:
            bounds[0] = t
            last_left = slope
    if (last_left > ENDPOINT_SIGN_EPS or last_right < -ENDPOINT_SIGN_EPS or
            bounds[0] > bounds[1]):
        raise AssertionError("Lost sign bracket")
    if (_flow_checksum(old_flows) != baseline_hash or
            _flow_checksum(new_flows) != accepted_hash):
        raise AssertionError("A diagnostic changed the original trajectory")

    # ONE independent frozen R13 counterpart and financial settlement
    # on the closest observed zero, charged as a separate validation,
    # not as an extra R14 call.
    root_obs = evaluate_r14(net, best_flow, settings, ())
    R21.frozen_check(net, best_flow, root_obs)
    financial = R21.settlement_check(net, best_flow)
    if financial["gross_micro"] != financial["credits_micro"]:
        raise AssertionError("Counterfactual settlement lost conservation")
    root_pair = market_pair_snapshot(
        net, root_obs, settings, gid, old_dest, new_dest)
    scans += root_pair["extra_shortest_path_scans"]
    original_routes = (
        pair["before"]["old_path"], pair["before"]["new_path"])
    final_routes = (pair["after"]["old_path"], pair["after"]["new_path"])
    path_signatures = {
        (tuple(a["old_market_canonical_route"]),
         tuple(a["new_market_canonical_route"]))
        for a in traces
    }
    path_signatures.add((tuple(original_routes[0]),tuple(original_routes[1])))
    path_signatures.add((tuple(final_routes[0]),tuple(final_routes[1])))
    winners = {
        a["actual_best_market_among_all_candidates"] for a in traces}
    winners.add(old_obs["target"][gid].destination)
    winners.add(new_obs["target"][gid].destination)
    return {
        "group": gid, "old_market": old_dest, "new_market": new_dest,
        "accepted_alpha": accepted_alpha,
        "initial_flow_sha256": baseline_hash,
        "accepted_flow_sha256": accepted_hash,
        "interval_start_fraction": bounds[0],
        "interval_end_fraction": bounds[1],
        "interval_width": bounds[1]-bounds[0],
        "signed_log_advantage_at_low": last_left,
        "signed_log_advantage_at_high": last_right,
        "closest_sample_fraction": best_t,
        "closest_sample_abs_log_advantage": best_absolute,
        "closest_sample_pair_near_exact_r14_tie": root_pair[
            "pair_near_tie_using_exact_r14_score_threshold"],
        "closest_sample_pair_is_global_competitive_tie": root_pair[
            "both_pair_markets_competitive_for_global_best"],
        "closest_sample_pair_tie_has_third_winner": root_pair[
            "pair_tied_but_global_best_is_third"],
        "closest_sample_r14_tie_group_flag": root_pair[
            "original_r14_group_has_tie_flag"],
        "closest_sample_actual_best_market": root_pair[
            "original_r14_best_market"],
        "sampled_distinct_winning_destinations": sorted(winners),
        "sampled_third_market_won": any(
            market not in (old_dest,new_dest) for market in winners),
        "sampled_pair_canonical_path_changed": len(path_signatures)>1,
        "sampled_canonical_path_signature_count": len(path_signatures),
        "sampled_any_r14_pair_tie": any(
            a["pair_score_tie_with_original_r14_threshold"] for a in traces),
        "sampled_any_r14_global_pair_tie": any(
            a["pair_is_globally_competitive_tie"] for a in traces),
        "sampled_any_f059_tie_group_flag": any(
            a["r14_f059_group_flag_present"] for a in traces),
        "sampled_any_third_market_pair_tie": any(
            a["third_market_has_strictly_better_score_at_pair_tie"]
            for a in traces),
        "root_f072_r13_parity_checks": 1,
        "root_f084f_financial_audits": 1,
        "extra_r14_oracle_calls": iterations+1,
        "independent_shortest_path_scans": scans,
        "trace": traces,
        "root_financial_witness": financial,
        "interval_guarantees_one_score_sign_crossing_not_uniqueness": True,
        "same_original_f072_policy_update_not_new_turns": True,
        "root_not_claimed_exact": True,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def inspect_accepted_transition(net, old_flows, old_obs, new_flows,
                                new_obs, settings, alpha, step):
    """At most two switched groups, selected by nearest endpoint score tie."""
    transition = R31.transition_crossings(
        net, old_obs, new_obs, settings.exponent)
    if not transition["market_switches"]:
        raise ValueError("A crossing transition requires changed destinations")
    pair_candidates = sorted(
        transition["pairs"],
        key=lambda x:(min(
            abs(x["before_log_advantage_new_over_old"]),
            abs(x["after_log_advantage_new_over_old"])), x["group"]))
    selected = pair_candidates[:GROUPS_PER_TRANSITION]
    witnesses = [crossing_bracket(
        net, old_flows, old_obs, new_flows, new_obs, settings, alpha, pair)
        for pair in selected]
    if not all(w["interval_width"] <= 2**(-BISECTION_ITERATIONS)+1e-15
               for w in witnesses):
        raise AssertionError("Bisection interval was not reduced to budget")
    return {
        "step": step,
        "market_switches_in_full_transition": transition["market_switches"],
        "route_only_switches_in_full_transition": transition[
            "route_only_switches"],
        "observed_price_dominant_pairs": transition[
            "price_dominated_switches"],
        "group_selection_by_nearest_endpoint_pair_margin": True,
        "selected_group_count": len(witnesses),
        "selected_witnesses": witnesses,
        "extra_r14_oracle_calls": sum(
            x["extra_r14_oracle_calls"] for x in witnesses),
        "extra_independent_shortest_path_scans": (
            transition["extra_shortest_path_scans"]+
            sum(x["independent_shortest_path_scans"] for x in witnesses)),
        "independent_r13_parity_checks": len(witnesses),
        "additional_f084f_checks": len(witnesses),
    }


def run_case(cfg, regime, factor, policy, steps, audit_every=8):
    if (regime not in REGIMES or factor not in CAPACITY_FACTORS or
            policy not in POLICIES or type(steps) is not int
            or not 1 <= steps <= 256 or type(audit_every) is not int
            or audit_every < 1):
        raise ValueError("Unsupported frozen experimental configuration")
    net, flows, fixture = R34.capacity_fixture(cfg, regime, factor)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    rows, audits, events = [], [], []
    proposal_calls = 1
    probe_calls = 0
    independent_scans = 0
    stable_count = 0
    first_stable = None
    last_delta = None
    accepted_alpha = None
    total_switches_all_steps = 0
    t0 = perf_counter()
    for step in range(steps+1):
        R33.verify_price_law(net, obs)
        if step%audit_every==0 or step==steps:
            R21.frozen_check(net,flows,obs)
            money=R21.settlement_check(net,flows)
            audits.append({"step":step,**money})
        stable = R24.numerical_pass(obs,last_delta)
        stable_count=stable_count+1 if stable else 0
        if stable_count>=3 and first_stable is None:
            first_stable=step
        rows.append({
            "step":step,
            "flow_sha256":_flow_checksum(flows),
            "relative_gap":obs["relative_gap"],
            "max_group_gap":obs["max_group_gap"],
            "price_change":None if last_delta is None else
                last_delta["max_relative_price_change"],
            "load_change":None if last_delta is None else
                last_delta["max_relative_load_change"],
            "quantity_change":None if last_delta is None else
                last_delta["max_relative_quantity_change"],
            "accepted_alpha":accepted_alpha,
            "numerical_five_thresholds_met":stable,
            "consecutive_stable_checks":stable_count,
            "proposal_r14_calls_cumulative":proposal_calls,
        })
        if step==steps:
            break
        old_flow, old_obs = flows,obs
        new_flow,new_obs,last_delta,calls,alpha=R29.step_forward(
            net,flows,obs,settings,policy,step+1)
        if calls<1:
            raise AssertionError("Policy proposals not charged")
        proposal_calls+=calls
        switches=sum(
            old_obs["target"][gid].destination !=
            new_obs["target"][gid].destination for gid in net.groups)
        total_switches_all_steps+=switches
        if switches and len(events)<TRANSITIONS_PER_TRAJECTORY:
            event=inspect_accepted_transition(
                net,old_flow,old_obs,new_flow,new_obs,settings,alpha,step+1)
            if event["market_switches_in_full_transition"]!=switches:
                raise AssertionError("Uncounted switched producer")
            probe_calls+=event["extra_r14_oracle_calls"]
            independent_scans+=event["extra_independent_shortest_path_scans"]
            events.append(event)
        flows,obs,accepted_alpha=new_flow,new_obs,alpha
    if audits[-1]["step"]!=steps:
        raise AssertionError("Missing final financial audit")
    witnesses=[w for event in events for w in event["selected_witnesses"]]
    if sum(w["extra_r14_oracle_calls"] for w in witnesses)!=probe_calls:
        raise AssertionError("Incorrect R14 probe accounting")
    return {
        "regime":regime,"capacity_factor":factor,"policy":policy,
        "nodes":10*cfg.m*cfg.m+2,"steps":steps,
        "fixture":fixture,
        "initial_flow_sha256":rows[0]["flow_sha256"],
        "final_flow_sha256":rows[-1]["flow_sha256"],
        "final_global_gap":rows[-1]["relative_gap"],
        "final_worst_group_gap":rows[-1]["max_group_gap"],
        "first_three_joint_stable_step":first_stable,
        "all_market_switches":total_switches_all_steps,
        "selected_crossing_transitions":len(events),
        "selected_producer_crossings":len(witnesses),
        "price_dominated_selected_events":sum(
            e["observed_price_dominant_pairs"] for e in events),
        "sampled_third_market_winner_crossings":sum(
            w["sampled_third_market_won"] for w in witnesses),
        "sampled_pair_route_path_changed_crossings":sum(
            w["sampled_pair_canonical_path_changed"] for w in witnesses),
        "sampled_exact_original_r14_pair_tie_crossings":sum(
            w["sampled_any_r14_pair_tie"] for w in witnesses),
        "sampled_global_competitive_pair_tie_crossings":sum(
            w["sampled_any_r14_global_pair_tie"] for w in witnesses),
        "closest_pair_exact_r14_tie_crossings":sum(
            w["closest_sample_pair_near_exact_r14_tie"] for w in witnesses),
        "extra_r14_probe_calls":probe_calls,
        "original_policy_r14_calls":proposal_calls,
        "total_r14_calls":proposal_calls+probe_calls,
        "independent_r13_probe_checks":len(witnesses),
        "independent_f084f_probe_checks":len(witnesses),
        "extra_shortest_path_scans":independent_scans,
        "events":events,"rows":rows,"financial_audits":audits,
        "elapsed_ms_observational":round((perf_counter()-t0)*1000,3),
        "crossings_sampled_not_exhaustive":True,
        "crossing_root_unique_proven":False,
        "causal_interpretation_proven":False,
        "financial_F059_ratified":False,
        "global_equilibrium_proven":False,
        "production_approved":False,
    }


def summarize(results):
    n=sum(r["selected_producer_crossings"] for r in results)
    return {
        "runs":len(results),
        "runs_with_probed_crossings":sum(
            r["selected_producer_crossings"]>0 for r in results),
        "total_market_switches_original_updates":sum(
            r["all_market_switches"] for r in results),
        "selected_market_crossing_witnesses":n,
        "selected_transitions":sum(
            r["selected_crossing_transitions"] for r in results),
        "any_sampled_third_market_winners":sum(
            r["sampled_third_market_winner_crossings"] for r in results),
        "any_sampled_pair_route_switches":sum(
            r["sampled_pair_route_path_changed_crossings"] for r in results),
        "any_sampled_pair_ties_original_r14_threshold":sum(
            r["sampled_exact_original_r14_pair_tie_crossings"] for r in results),
        "any_sampled_global_competitive_pair_ties":sum(
            r["sampled_global_competitive_pair_tie_crossings"] for r in results),
        "closest_pair_at_exact_r14_tie_threshold":sum(
            r["closest_pair_exact_r14_tie_crossings"] for r in results),
        "original_r14_policy_calls":sum(
            r["original_policy_r14_calls"] for r in results),
        "additional_r14_bisection_oracles":sum(
            r["extra_r14_probe_calls"] for r in results),
        "independent_r13_probe_checks":sum(
            r["independent_r13_probe_checks"] for r in results),
        "independent_f084f_probe_checks":sum(
            r["independent_f084f_probe_checks"] for r in results),
        "additional_shortest_path_scans":sum(
            r["extra_shortest_path_scans"] for r in results),
        "num_converged_three_checkpoints":sum(
            r["first_three_joint_stable_step"] is not None for r in results),
        "all_roots_are_intervals_not_uniqueness_certificates":True,
        "not_iid_samples_not_general_equilibrium":True,
    }


def exercise(profile):
    if profile=="smoke":
        plans=(("small",6),)
        regimes=("original",)
        factors=(0.20,1.0)
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
        raise ValueError("Unknown profile")
    out=[]
    for size,steps in plans:
        for regime in regimes:
            for capacity in factors:
                for policy in policies:
                    r=run_case(R21.CASES[size],regime,capacity,policy,steps)
                    out.append(r)
                    print(
                        "[{}:{}:Kx{}:{}] gap={:.7g} worst={:.7g} "
                        "switches={} transitions={} pairs={} pairTies={} "
                        "thirdMarket={} routeChanged={} policyR14={} "
                        "bracketR14={} scans={} stable={}".format(
                            size,regime,capacity,policy,
                            r["final_global_gap"],r["final_worst_group_gap"],
                            r["all_market_switches"],
                            r["selected_crossing_transitions"],
                            r["selected_producer_crossings"],
                            r["sampled_exact_original_r14_pair_tie_crossings"],
                            r["sampled_third_market_winner_crossings"],
                            r["sampled_pair_route_path_changed_crossings"],
                            r["original_policy_r14_calls"],
                            r["extra_r14_probe_calls"],
                            r["extra_shortest_path_scans"],
                            r["first_three_joint_stable_step"]),flush=True)
    if profile!="smoke":
        first=run_case(
            R21.CASES["small"],"original",1.0,"decay_sqrt",5,audit_every=2)
        old=R34.run_case(
            R21.CASES["small"],"original",1.0,"decay_sqrt",5,audit_every=2)
        if (first["final_flow_sha256"]!=old["final_flow_sha256"] or
            first["final_global_gap"]!=old["final_global_gap"] or
            first["original_policy_r14_calls"]!=old["policy_r14_calls"]):
            raise AssertionError("R34 baseline changed by R36")
        for policy in POLICIES:
            R24.check_r21_reference(R21.CASES["small"],policy,6)
        short=run_case(
            R21.CASES["small"],"original",.20,"decay_sqrt",4,audit_every=2)
        longer=run_case(
            R21.CASES["small"],"original",.20,"decay_sqrt",7,audit_every=2)
        if short["rows"]!=longer["rows"][:5]:
            raise AssertionError("Prefix replay mismatched original flow")
    return {
        "cases":out,"aggregate":summarize(out),
        "r20_f072_price_congestion_tariff_unchanged":True,
        "no_probes_committed":True,
        "f059_unratified":True,
    }


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile",choices=("smoke","ci","extended"),
                        default="ci")
    parser.add_argument("--output",type=Path,default=Path.cwd())
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    report={
        "schema":"GA_GATE09_R36_REAL_MARKET_PAIR_SIGN_BRACKET_V1",
        "generated_utc":datetime.now(timezone.utc).isoformat(),
        "platform":platform.platform(),
        "python":platform.python_version(),
        "profile":args.profile,"status":"FAILED",
        "global_equilibrium_proven":False,
        "crossing_uniqueness_proven":False,
        "financial_F059_ratified":False,
        "causality_proven":False,
        "production_approved":False,
    }
    try:
        report["r20_manifest_files_verified"]=R21.validate_manifest()
        if report["r20_manifest_files_verified"]!=16:
            raise AssertionError("R20 immutable baseline drift")
        report.update(exercise(args.profile))
        report["status"]="PASSED_TARGETED_CROSSING_BRACKETS_ONLY"
    except Exception as exc:
        report["error"]=repr(exc)
        report["traceback"]=traceback.format_exc()
        print("[R36] FAILED:",repr(exc),flush=True)
    stamp=datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path=args.output/("GA-Gate09-R36-"+args.profile+"-"+stamp+".json")
    path.write_text(json.dumps(report,sort_keys=True,indent=2,
                               ensure_ascii=False)+"\n",encoding="utf-8")
    z=args.output/("GA-Gate09-R36-Evidencias-"+stamp+".zip")
    with zipfile.ZipFile(z,"w",zipfile.ZIP_DEFLATED) as archive:
        archive.write(path,path.name)
    print("[R36] STATUS:",report["status"],flush=True)
    print("[R36] EVIDENCE:",z,flush=True)
    return 0 if report["status"]=="PASSED_TARGETED_CROSSING_BRACKETS_ONLY" else 1


if __name__=="__main__":
    raise SystemExit(main())
