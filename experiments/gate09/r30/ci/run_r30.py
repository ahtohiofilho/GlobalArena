#!/usr/bin/env python3
"""GA Gate09 R30 - frozen top-two MARKET scores and incumbent ROUTE witnesses.

OBSERVATION ONLY: the R14 winner is reconstructed using its exact
FrozenRouteCache path/score formula and tie threshold. The runner does
NOT enumerate the second-shortest path: capped route multiplicity and
the previous canonical path are separately labeled route evidence.
Neither analysis nor probes modify any R20-R29 economic or solver law.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import platform
import statistics
import sys
from time import perf_counter
import traceback
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "r29" / "ci"))
import run_r29 as R29

R21 = R29.R21
R22 = R29.R22
R24 = R29.R24
R26 = R29.R26
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum, _mix
from gate09 import Column
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14

POLICIES = R29.POLICIES
PROBE_ALPHAS = (1 / 8, 1 / 16)
EXACT_TIE_REL = 1e-10        # MUST reproduce baseline max(1, abs(score))
NEAR_MARGIN_REL = 1e-4       # Diagnostic only, not a market preference
EPS = 1e-12


def near_baseline_score(best, other):
    """Tie predicate exactly as R14's between-market score check."""
    return abs(best - other) <= EXACT_TIE_REL * max(1., abs(best))


def market_alternatives(net, observed, exponent, old_targets=None):
    """Rank canonical shortest route to each MARKET, not every route.

    The first two scores compare distinct destinations for the SAME
    commodity. A shortest-path multiplicity flag identifies possible
    equal-cost routes to the winner, without claiming knowledge of the
    second-best noncanonical path. If an old target exists, independently
    score THAT concrete prior route on the current frozen prices/loads.
    """
    loads, prices = observed["loads"], observed["prices"]
    cache = FrozenRouteCache(net, loads, exponent)
    rows = {}
    baseline_ties = set()
    old_targets = {} if old_targets is None else old_targets
    if old_targets and set(old_targets) != set(net.groups):
        raise AssertionError("Different group domain for previous targets")
    for gid, group in sorted(net.groups.items()):
        distances, paths, ways = cache.paths(group.origin, group.seller)
        candidates = []
        for market in sorted(net.markets):
            if market[1] != group.commodity or market[0] not in paths:
                continue
            score = prices[market] / (1 + distances[market[0]])
            if not (math.isfinite(score) and score > 0):
                raise AssertionError("Invalid market score")
            col = Column(gid, market[0], paths[market[0]])
            candidates.append((score, col, ways[market[0]]))
        if not candidates:
            raise AssertionError("Producer with no accessible market")
        candidates.sort(key=lambda t: (-t[0], t[1]))
        best, winner, multiplicity = candidates[0]
        if winner != observed["target"][gid]:
            raise AssertionError("Market ranking changed original R14 winner")
        if best != observed["groups"][gid]["score"]:
            raise AssertionError("Market ranking changed original R14 winner score")
        if winner != observed["groups"][gid]["best"]:
            raise AssertionError("R14 group best-column mismatch")
        runner_score, runner_col = (
            (candidates[1][0], candidates[1][1])
            if len(candidates) > 1 else (None, None))
        absolute_margin = best - runner_score if runner_score is not None else None
        relative_margin = absolute_margin / best if runner_score is not None else None
        market_tie = (runner_score is not None and
                      near_baseline_score(best, runner_score))
        route_tie = multiplicity > 1
        if market_tie or route_tie:
            baseline_ties.add(gid)
        old = old_targets.get(gid)
        incumbent_score = None
        incumbent_score_loss = None
        incumbent_market_loss = None
        incumbent_route_loss = None
        if old is not None:
            if old.group_id != gid or (old.destination, group.commodity) not in prices:
                raise AssertionError("Invalid previous canonical target")
            weights = cache.weights(group.seller)
            incumbent_weight = sum(weights[e] for e in old.edges)
            incumbent_score = prices[(old.destination, group.commodity)] / (1 + incumbent_weight)
            incumbent_score_loss = (best - incumbent_score) / best
            # Among canonical paths, compare the old destination's current
            # cheapest path to the NEW winner; this isolates market ranking.
            market_cost = distances[old.destination]
            same_market_score = prices[(old.destination, group.commodity)] / (1 + market_cost)
            incumbent_market_loss = (best - same_market_score) / best
            if old.destination == winner.destination:
                # Exact current value of *one known* alternative path,
                # NOT the second-best route among all simple paths.
                incumbent_route_loss = (best - incumbent_score) / best
        if relative_margin is not None and relative_margin < -1e-14:
            raise AssertionError("Bad descending market-score rank")
        rows[gid] = {
            "best_destination": winner.destination,
            "best_route_edges": list(winner.edges),
            "best_score": best,
            "second_market_destination": (
                runner_col.destination if runner_col is not None else None),
            "second_market_score": runner_score,
            "market_margin_absolute": absolute_margin,
            "market_margin_relative": relative_margin,
            "market_near_tie_at_original_r14_threshold": bool(market_tie),
            "best_route_multiplicity_capped_at_two": multiplicity,
            "best_route_tie_flag": route_tie,
            "baseline_f059_tie_flag": market_tie or route_tie,
            "previous_target_destination": old.destination if old else None,
            "previous_target_current_score": incumbent_score,
            "previous_target_score_loss_relative": incumbent_score_loss,
            "previous_destination_canonical_score_loss_relative": incumbent_market_loss,
            "incumbent_same_destination_route_loss_relative": incumbent_route_loss,
            "second_best_route_not_enumerated": True,
        }
    if baseline_ties != set(observed["tie_groups"]):
        raise AssertionError("Reconstructed market/route ties do not match R14")
    if len(rows) != len(net.groups):
        raise AssertionError("Missing producer margin")
    counters = cache.counters()
    return {
        "groups": rows,
        "exact_r14_tie_groups": len(baseline_ties),
        "route_tied_groups": sum(int(v["best_route_tie_flag"]) for v in rows.values()),
        "market_near_tied_groups": sum(
            int(v["market_near_tie_at_original_r14_threshold"]) for v in rows.values()),
        "market_gap_below_diagnostic_1e4": sum(
            int(v["market_margin_relative"] is not None
                and v["market_margin_relative"] <= NEAR_MARGIN_REL)
            for v in rows.values()),
        "extra_shortest_path_scans": counters["shortest_path_scans"],
        "extra_seller_weight_tables": counters["seller_weight_tables"],
        "not_a_second_best_route_search": True,
    }


def transition_attribution(net, prior, current, prior_margins, current_margins,
                           older=None):
    """Classify changed markets and concrete prior routes at NEW snapshot.

    Counting cheap 1e-4 market margins on route-only changes would be a
    category error; we compare the actual incumbent route's score there.
    """
    partition = R29.transition(net, prior, current, older)
    counts = {
        "changed_market": 0, "changed_route_only": 0,
        "market_switch_at_baseline_exact_tie": 0,
        "market_switch_at_diagnostic_near_margin": 0,
        "market_switch_not_near": 0,
        "route_only_incumbent_near_1e4": 0,
        "route_only_incumbent_not_near": 0,
        "route_only_best_route_has_equal_cost_multiplicity": 0,
        "market_switch_previous_destination_close_1e4": 0,
        "aba_groups": partition["two_step_aba_reversals"],
        "aba_market_near_margin": 0,
        "aba_route_only_incumbent_near": 0,
    }
    moves = []
    aba_ids = set(partition["two_step_aba_group_ids"])
    for gid in sorted(current["target"]):
        before, after = prior["target"][gid], current["target"][gid]
        kind = R29.market_route_parts(before, after)
        if kind not in ("market_only", "route_only", "both"):
            continue
        info = current_margins["groups"][gid]
        former = prior_margins["groups"][gid]
        if kind in ("market_only", "both"):
            counts["changed_market"] += 1
            is_tie = info["market_near_tie_at_original_r14_threshold"]
            near = (info["market_margin_relative"] is not None and
                    info["market_margin_relative"] <= NEAR_MARGIN_REL)
            counts["market_switch_at_baseline_exact_tie"] += int(is_tie)
            counts["market_switch_at_diagnostic_near_margin"] += int(near)
            counts["market_switch_not_near"] += int(not near)
            prev_dest_close = (
                info["previous_destination_canonical_score_loss_relative"] is not None
                and info["previous_destination_canonical_score_loss_relative"]
                <= NEAR_MARGIN_REL)
            counts["market_switch_previous_destination_close_1e4"] += int(prev_dest_close)
            if gid in aba_ids:
                counts["aba_market_near_margin"] += int(near)
        if kind == "route_only":
            counts["changed_route_only"] += 1
            loss = info["incumbent_same_destination_route_loss_relative"]
            if loss is None:
                raise AssertionError("Missing incumbent route witness")
            near = loss <= NEAR_MARGIN_REL
            counts["route_only_incumbent_near_1e4"] += int(near)
            counts["route_only_incumbent_not_near"] += int(not near)
            counts["route_only_best_route_has_equal_cost_multiplicity"] += int(
                info["best_route_tie_flag"])
            if gid in aba_ids:
                counts["aba_route_only_incumbent_near"] += int(near)
        moves.append({
            "group": gid, "kind": kind, "aba": gid in aba_ids,
            "prior_market_margin_relative": former["market_margin_relative"],
            "new_market_margin_relative": info["market_margin_relative"],
            "new_best_market_exact_tie": info["market_near_tie_at_original_r14_threshold"],
            "new_best_route_tie": info["best_route_tie_flag"],
            "former_target_new_score_loss": info["previous_target_score_loss_relative"],
            "old_destination_new_canonical_score_loss": info[
                "previous_destination_canonical_score_loss_relative"],
            "incumbent_route_new_score_loss": info[
                "incumbent_same_destination_route_loss_relative"],
        })
    if counts["changed_market"] != (
        partition["switch_partition"]["market_only"] +
        partition["switch_partition"]["both"]):
        raise AssertionError("Changed-market attribution undercount")
    if counts["changed_route_only"] != partition["switch_partition"]["route_only"]:
        raise AssertionError("Changed-route attribution undercount")
    return {"partition": partition, "counts": counts, "changed_groups": moves}


def counterfactual_market_margin(net, flows, observed, settings, every_step):
    """New independent R14 repricing per alpha; return GROUP margin evidence."""
    sha = _flow_checksum(flows)
    result = []
    for alpha in PROBE_ALPHAS:
        proposed = _mix(flows, net, observed["target"], alpha)
        future = evaluate_r14(net, proposed, settings, ())
        margin = market_alternatives(net, future, settings.exponent,
                                     old_targets=observed["target"])
        structural = transition_attribution(
            net, observed, future,
            market_alternatives(net, observed, settings.exponent),
            margin)
        result.append({
            "alpha": alpha,
            "flow_sha256": _flow_checksum(proposed),
            "next_gap": future["relative_gap"],
            "next_max_group_gap": future["max_group_gap"],
            "next_market_near_ties": margin["market_near_tied_groups"],
            "next_route_ties": margin["route_tied_groups"],
            "next_market_margin_below_1e4": margin["market_gap_below_diagnostic_1e4"],
            "changed_market": structural["counts"]["changed_market"],
            "changed_route_only": structural["counts"]["changed_route_only"],
            "market_switch_at_diagnostic_near_margin": structural[
                "counts"]["market_switch_at_diagnostic_near_margin"],
            "route_only_incumbent_near_1e4": structural[
                "counts"]["route_only_incumbent_near_1e4"],
            "additional_market_margin_shortest_path_scans": (
                margin["extra_shortest_path_scans"]),
        })
    if _flow_checksum(flows) != sha:
        raise AssertionError("Probes mutated the reference trajectory")
    return {
        "step": every_step, "source_flow_sha256": sha,
        "extra_r14_probe_evaluations": len(result),
        "probes": result,
    }


def summarize(rows):
    changes = [r for r in rows if r["step"] > 0]
    count = lambda key: sum(r["attribution"]["counts"][key] for r in changes)
    margin_counts = {
        "market_switches": count("changed_market"),
        "market_switch_exact_tie": count("market_switch_at_baseline_exact_tie"),
        "market_switch_close_1e4": count("market_switch_at_diagnostic_near_margin"),
        "market_switch_not_close_1e4": count("market_switch_not_near"),
        "route_only_switches": count("changed_route_only"),
        "route_only_known_incumbent_near_1e4": count("route_only_incumbent_near_1e4"),
        "route_only_known_incumbent_not_near": count("route_only_incumbent_not_near"),
        "route_only_best_has_cost_optimal_route_multiplicity": count(
            "route_only_best_route_has_equal_cost_multiplicity"),
        "total_group_aba_reversals": count("aba_groups"),
        "aba_market_near_margin": count("aba_market_near_margin"),
        "aba_route_only_incumbent_near": count("aba_route_only_incumbent_near"),
    }
    return {
        **margin_counts,
        "fraction_market_switch_close": (
            margin_counts["market_switch_close_1e4"] /
            margin_counts["market_switches"] if margin_counts["market_switches"] else None),
        "fraction_route_only_incumbent_near": (
            margin_counts["route_only_known_incumbent_near_1e4"] /
            margin_counts["route_only_switches"] if margin_counts["route_only_switches"] else None),
        "mean_market_margin_all_snapshots": statistics.fmean(
            row["market_margin_relative"]
            for record in rows for row in record["market_evidence"]["groups"].values()
            if row["market_margin_relative"] is not None),
        "final_exact_r14_tied_groups": rows[-1][
            "market_evidence"]["exact_r14_tie_groups"],
        "final_market_near_tied_groups": rows[-1][
            "market_evidence"]["market_near_tied_groups"],
        "final_route_tied_groups": rows[-1][
            "market_evidence"]["route_tied_groups"],
        "causal_congestion_or_price_attribution_proven": False,
        "complete_second_best_route_enumerated": False,
    }


def run_case(cfg, policy, steps, audit_every=16, probe_every=24):
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Invalid policy / step budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit cadence")
    if type(probe_every) is not int or probe_every < 1:
        raise ValueError("Invalid probe cadence")
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    prior, older = None, None
    prior_margin = None
    alpha = None
    change = None
    proposals = 1
    probes_used = 0
    shortest_path_scans = 0
    stable_count = 0
    first_stable = None
    rows, probes, audits = [], [], []
    start = perf_counter()
    for step in range(steps + 1):
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, obs)
            financial = R21.settlement_check(net, flows)
            audits.append({"step": step, **financial})
        if obs["reachable_unpriced"] or any(
            not math.isfinite(p) or p <= 0 for p in obs["prices"].values()
        ):
            raise AssertionError("Reachable market with invalid quote")
        margin = market_alternatives(net, obs, settings.exponent,
                                     old_targets=None if prior is None
                                     else prior["target"])
        shortest_path_scans += margin["extra_shortest_path_scans"]
        structural = (transition_attribution(net, prior, obs, prior_margin, margin,
                                             older=older)
                      if prior is not None else None)
        stable = R24.numerical_pass(obs, change)
        stable_count = stable_count + 1 if stable else 0
        if stable_count >= 3 and first_stable is None:
            first_stable = step
        rows.append({
            "step": step,
            "flow_sha256": _flow_checksum(flows),
            "target_sha256": R29.target_signature(obs["target"]),
            "relative_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "alpha_used": alpha,
            "price_change": None if change is None else
                change["max_relative_price_change"],
            "load_change": None if change is None else
                change["max_relative_load_change"],
            "quantity_change": None if change is None else
                change["max_relative_quantity_change"],
            "market_evidence": margin,
            "attribution": structural,
            "numerical_five_thresholds_met": stable,
            "consecutive_numerical_rechecks": stable_count,
            "accepted_proposal_oracles_used": proposals,
        })
        if step % probe_every == 0:
            probe = counterfactual_market_margin(net, flows, obs, settings, step)
            probes.append(probe)
            probes_used += probe["extra_r14_probe_evaluations"]
        if step == steps:
            break
        proposed, next_obs, change, calls, alpha = R29.step_forward(
            net, flows, obs, settings, policy, step + 1)
        if calls < 1:
            raise AssertionError("Uncharged trial calls")
        proposals += calls
        older, prior, prior_margin = prior, obs, margin
        flows, obs = proposed, next_obs

    if audits[-1]["step"] != steps:
        raise AssertionError("Missing final financial audit")
    # Reference replay is a costed additional independent *test*, not
    # part of the primary path's oracle accounting.
    frozen = R21.run_trajectory(cfg, policy, steps, audit_every=steps) \
        if policy in R21.POLICIES else None
    if frozen:
        if len(frozen["rows"]) != len(rows):
            raise AssertionError("R21 row-count drift")
        for a, b in zip(frozen["rows"], rows):
            if any(a[k] != b[k] for k in (
                    "flow_sha256", "relative_gap", "max_group_gap")):
                raise AssertionError("R21 replay discrepancy")
    summary = summarize(rows)
    return {
        "policy": policy, "nodes": 10 * cfg.m * cfg.m + 2,
        "groups": len(net.groups), "steps": steps,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": rows[-1]["relative_gap"],
        "final_max_group_gap": rows[-1]["max_group_gap"],
        "final_flow_sha256": rows[-1]["flow_sha256"],
        "final_target_sha256": rows[-1]["target_sha256"],
        "first_three_joint_stable_step": first_stable,
        "proposal_oracle_evaluations": proposals,
        "counterfactual_probe_oracle_evaluations": probes_used,
        "total_primary_r14_evaluations": proposals + probes_used,
        "extra_shortest_path_scans_for_market_margin": shortest_path_scans,
        "financial_audits": audits,
        "summary": summary,
        "probes": probes,
        "rows": rows,
        "elapsed_ms_observational": round((perf_counter() - start) * 1000, 3),
        "economic_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "causality_proven": False,
        "production_approved": False,
    }


def exercise(profile):
    if profile == "smoke":
        scenarios = (("small", 8),)
    elif profile == "ci":
        scenarios = (("small", 96), ("medium", 48))
    elif profile == "extended":
        scenarios = (("small", 160), ("medium", 96), ("large", 24))
    else:
        raise ValueError("Unsupported profile")
    results = []
    for label, steps in scenarios:
        cases = []
        for policy in POLICIES:
            result = run_case(R21.CASES[label], policy, steps)
            summary = result["summary"]
            print("[{}:{}] gap={:.7g} worst={:.7g} "
                  "marketSwitches={} marketNear={} routeOnly={} "
                  "incumbentNear={} ABA={} marketTies={} routeTies={} "
                  "proposalOracles={} probeOracles={} stable={}".format(
                      label, policy, result["final_gap"],
                      result["final_max_group_gap"],
                      summary["market_switches"],
                      summary["market_switch_close_1e4"],
                      summary["route_only_switches"],
                      summary["route_only_known_incumbent_near_1e4"],
                      summary["total_group_aba_reversals"],
                      summary["final_market_near_tied_groups"],
                      summary["final_route_tied_groups"],
                      result["proposal_oracle_evaluations"],
                      result["counterfactual_probe_oracle_evaluations"],
                      result["first_three_joint_stable_step"]), flush=True)
            cases.append(result)
        results.append({"case": label, "policies": cases})
    short = run_case(R21.CASES["small"], "decay_sqrt", 4,
                     audit_every=2, probe_every=2)
    long = run_case(R21.CASES["small"], "decay_sqrt", 7,
                    audit_every=2, probe_every=2)
    if short["rows"] != long["rows"][:len(short["rows"])]:
        raise AssertionError("Non-deterministic margin prefixes")
    return {"comparisons": results, "prefix_replay": True,
            "r20_f072_oracle_unmodified": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R30_TOP_TWO_MARKET_AND_INCUMBENT_ROUTE_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "second_best_route_enumerated": False,
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("Frozen R20 source changed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTIC_CONTRACTS_ONLY"
    except Exception as e:
        report["error"] = repr(e)
        report["traceback"] = traceback.format_exc()
        print("[R30] FAILED:", repr(e), flush=True)
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    outfile = args.output / ("GA-Gate09-R30-" + args.profile +
                             "-" + timestamp + ".json")
    outfile.write_text(json.dumps(report, sort_keys=True, indent=2,
                                  ensure_ascii=False) + "\n", encoding="utf-8")
    zipfile_path = args.output / ("GA-Gate09-R30-Evidencias-" + timestamp + ".zip")
    with zipfile.ZipFile(zipfile_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(outfile, outfile.name)
    print("[R30] STATUS:", report["status"], flush=True)
    print("[R30] EVIDENCE:", zipfile_path, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTIC_CONTRACTS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
