#!/usr/bin/env python3
"""Gate09 R27: warm-start candidate order; full immutable F-072 repricing.

Numerical policy experiment, not a game mechanic. An alpha can only be
accepted after R14 recomputes prices, routes and congestion at its NEW flow.
R20-R26 modules, economics and F-084F monetary settlement are immutable.
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

R26_CI = Path(__file__).resolve().parents[2] / "r26" / "ci"
sys.path.insert(0, str(R26_CI))
import run_r26 as R26

R21 = R26.R21
R22 = R26.R22
R24 = R26.R24
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum, _mix
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

POLICIES = ("warm_global", "warm_joint")
ALPHAS = R26.ALPHAS
FRONTIERS = R26.FRONTIERS


def candidate_order(last_success_alpha):
    """Cost-free prioritization: recent acceptable alpha nearest first.

    No cached price, market, route, or congestion quantity enters scoring.
    On cold start, retain R26's exact descending sequence. Every alpha
    remains eligible; an older rejected step is never assumed valid today.
    """
    if last_success_alpha is None:
        return ALPHAS
    if last_success_alpha not in ALPHAS:
        raise ValueError("Anchor not part of the sanctioned candidate grid")
    return tuple(sorted(ALPHAS, key=lambda a: (
        abs(math.log2(a / last_success_alpha)), -a)))


def choose_warm(net, flows, observed, settings, policy, anchor, remaining):
    if policy not in POLICIES:
        raise ValueError("Unsupported warm-start policy")
    if type(remaining) is not int or not 1 <= remaining <= len(ALPHAS):
        raise ValueError("Invalid evaluation allowance")
    orig_gap = observed["relative_gap"]
    orig_worst = observed["max_group_gap"]
    search_policy = ("progressive_global" if policy == "warm_global"
                     else "progressive_joint")
    order = candidate_order(anchor)
    trials = []
    chosen = None
    accepted = False

    # A candidate is re-evaluated, not recycled, even if its alpha
    # was useful at the previous frozen snapshot.
    for alpha in order[:remaining]:
        proposed = _mix(flows, net, observed["target"], alpha)
        next_observation = evaluate_r14(net, proposed, settings, ())
        delta = _change_metrics(net, flows, proposed)
        gap = next_observation["relative_gap"]
        worst = next_observation["max_group_gap"]
        good = R26.acceptable(search_policy, orig_gap, orig_worst, gap, worst)
        ratio = max(gap / max(orig_gap, R26.EPS),
                    worst / max(orig_worst, R26.EPS))
        record = {
            "alpha": alpha, "flows": proposed, "observation": next_observation,
            "delta": delta, "relative_gap": gap, "max_group_gap": worst,
            "joint_ratio": ratio, "accepted": good,
        }
        trials.append(record)
        if good:
            chosen, accepted = record, True
            break

    if chosen is None:
        chosen = min(trials, key=lambda t: R26.trial_rank(search_policy, t))
    # Failed fallback keeps prior success anchor, avoiding feedback from a
    # step that did not meet the numerical acceptance rule.
    new_anchor = chosen["alpha"] if accepted else anchor
    if len(trials) > remaining or chosen["alpha"] not in order[:len(trials)]:
        raise AssertionError("Unaccounted warm-start candidate")
    return chosen["flows"], chosen["observation"], chosen["delta"], new_anchor, {
        "selected_alpha": chosen["alpha"],
        "prior_accepted_anchor": anchor,
        "next_accepted_anchor": new_anchor,
        "tested_order": list(order[:len(trials)]),
        "candidate_count": len(trials),
        "accepted_early": accepted,
        "fallback_no_acceptable_step": not accepted,
        "truncated_search": not accepted and remaining < len(ALPHAS),
        "selected_joint_ratio": chosen["joint_ratio"],
        "trials": [{
            "alpha": t["alpha"], "relative_gap": t["relative_gap"],
            "max_group_gap": t["max_group_gap"], "accepted": t["accepted"],
            "price_change": t["delta"]["max_relative_price_change"],
            "load_change": t["delta"]["max_relative_load_change"],
            "quantity_change": t["delta"]["max_relative_quantity_change"],
        } for t in trials],
    }


def run_case(cfg, policy, updates, budget, audit_every=16):
    if policy not in POLICIES:
        raise ValueError("Unsupported policy")
    if type(updates) is not int or not 1 <= updates <= 256:
        raise ValueError("Invalid update budget")
    if type(budget) is not int or not 2 <= budget <= 2048:
        raise ValueError("Invalid oracle budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit cadence")

    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    evaluations = 1
    anchor = None
    previous_target = None
    delta = None
    decision = None
    rows = []
    audits = []
    early = 0
    fallback = 0
    truncated = 0
    streak = 0
    first_stable = None
    start = perf_counter()

    for step in range(updates + 1):
        if step % audit_every == 0 or step == updates:
            R21.frozen_check(net, flows, obs)
            settled = R21.settlement_check(net, flows)
            audits.append({"step": step, **settled})
        if obs["reachable_unpriced"] or any(
                not math.isfinite(v) or v <= 0 for v in obs["prices"].values()):
            raise AssertionError("Unpriced reachable market")
        attrs = R22.attribute_gap(net, obs)
        numerically_stable = R24.numerical_pass(obs, delta)
        streak = streak + 1 if numerically_stable else 0
        if streak >= 3 and first_stable is None:
            first_stable = step
        route_changes = (0 if previous_target is None else sum(
            previous_target[g] != obs["target"][g] for g in obs["target"]))
        rows.append({
            "step": step, "oracles_used": evaluations,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "price_change": None if delta is None else
                delta["max_relative_price_change"],
            "load_change": None if delta is None else
                delta["max_relative_load_change"],
            "quantity_change": None if delta is None else
                delta["max_relative_quantity_change"],
            "best_route_switches": route_changes,
            "tie_groups": len(obs["tie_groups"]),
            "top_three_regret_share": attrs["top_three_share_of_total_regret"],
            "numerical_five_thresholds_met": numerically_stable,
            "consecutive_numerical_rechecks": streak,
            "decision": decision,
        })
        if step == updates or evaluations >= budget:
            break

        previous_target = obs["target"]
        flows, obs, delta, anchor, decision = choose_warm(
            net, flows, obs, settings, policy, anchor,
            min(len(ALPHAS), budget - evaluations))
        evaluations += decision["candidate_count"]
        early += int(decision["accepted_early"])
        fallback += int(decision["fallback_no_acceptable_step"])
        truncated += int(decision["truncated_search"])

    if audits[-1]["step"] != rows[-1]["step"]:
        R21.frozen_check(net, flows, obs)
        final_audit = R21.settlement_check(net, flows)
        audits.append({"step": rows[-1]["step"], **final_audit})
    if evaluations != 1 + sum(r["decision"]["candidate_count"]
                              for r in rows[1:]):
        raise AssertionError("Incorrect oracle evaluation accounting")
    if evaluations > budget:
        raise AssertionError("Exceeded oracle budget")

    last = rows[-1]
    best_index = min(range(len(rows)), key=lambda i: (
        rows[i]["relative_gap"], i))
    return {
        "policy": policy, "nodes": 10 * cfg.m * cfg.m + 2,
        "group_count": len(net.groups),
        "updates_executed": len(rows) - 1,
        "oracles_used": evaluations, "oracle_budget": budget,
        "max_updates_requested": updates,
        "evaluations_per_update": (evaluations - 1) / max(1, len(rows) - 1),
        "early_acceptances": early,
        "fallback_steps": fallback,
        "truncated_search_steps": truncated,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": last["relative_gap"],
        "final_max_group_gap": last["max_group_gap"],
        "best_observed_gap": rows[best_index]["relative_gap"],
        "best_observed_step": best_index,
        "final_price_change": last["price_change"],
        "final_load_change": last["load_change"],
        "final_quantity_change": last["quantity_change"],
        "first_three_joint_stable_step": first_stable,
        "total_best_route_switches": sum(r["best_route_switches"] for r in rows),
        "financial_audits": audits,
        "oracle_budget_frontiers": R24.frontier_snapshots(rows),
        "rows": rows, "final_flow_sha256": last["flow_sha256"],
        "elapsed_ms_observational": round((perf_counter() - start) * 1000, 3),
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def cold_start_replay(cfg):
    """Cold search must coincide exactly with R26's first accepted candidate."""
    net, flows = construct_scenario(cfg)
    cfg_settings = ClearingSettings(exponent=cfg.exponent)
    original = evaluate_r14(net, flows, cfg_settings, ())
    for warm, base in (("warm_global", "progressive_global"),
                       ("warm_joint", "progressive_joint")):
        w_flows, w_obs, w_delta, anchor, audit = choose_warm(
            net, flows, original, cfg_settings, warm, None, 5)
        b_flows, b_obs, b_delta, baseline = R26.choose_progressive(
            net, flows, original, cfg_settings, base, 5)
        if (audit["selected_alpha"] != baseline["selected_alpha"] or
            audit["candidate_count"] != baseline["candidate_count"] or
            _flow_checksum(w_flows) != _flow_checksum(b_flows) or
            w_obs["relative_gap"] != b_obs["relative_gap"] or
            w_obs["max_group_gap"] != b_obs["max_group_gap"] or
            w_delta != b_delta):
            raise AssertionError("Cold-start policy not equal to R26 baseline")
    return True


def exercise(profile):
    if profile == "smoke":
        plans = (("small", 8, 33),)
    elif profile == "ci":
        plans = (("small", 96, 385), ("medium", 48, 193))
    elif profile == "extended":
        plans = (("small", 160, 641), ("medium", 96, 385),
                 ("large", 24, 97))
    else:
        raise ValueError("Unsupported profile")
    results = []
    for label, updates, budget in plans:
        cfg = R21.CASES[label]
        compared = []
        for policy in ("decay_sqrt", "backtracking",
                       "progressive_global", "progressive_joint",
                       "warm_global", "warm_joint"):
            if policy in POLICIES:
                result = run_case(cfg, policy, updates, budget)
            elif policy in R24.POLICIES:
                result = R24.run_case(cfg, policy, updates, budget)
            else:
                result = R26.run_case(cfg, policy, updates, budget)
            print("[{}:{}] gap={:.7g} worst={:.7g} "
                  "priceChange={:.4g} loadChange={:.4g} "
                  "evals={} updates={} stable={}".format(
                      label, policy, result["final_gap"],
                      result["final_max_group_gap"],
                      result["final_price_change"], result["final_load_change"],
                      result["oracles_used"], result["updates_executed"],
                      result["first_three_joint_stable_step"]), flush=True)
            compared.append({
                "policy": policy, "result": result,
                "budget_frontiers": result["oracle_budget_frontiers"],
            })
        results.append({"case": label, "policies": compared})
    cold_start_replay(R21.CASES["small"])
    for policy in POLICIES:
        shorter = run_case(R21.CASES["small"], policy, 4, 51, audit_every=2)
        longer = run_case(R21.CASES["small"], policy, 7, 51, audit_every=2)
        if shorter["rows"] != longer["rows"][:len(shorter["rows"])]:
            raise AssertionError("Warm-start prefix drift")
    for base in ("decay_sqrt", "backtracking"):
        R24.check_r21_reference(R21.CASES["small"], base, 6)
    return {
        "scenarios": results,
        "frozen_r21_reference_replay": True,
        "r26_cold_start_replay": True,
        "warm_start_prefix_replay": True,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R27_WARM_START_REPRICE_GUARD_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("Immutable R20 baseline drift")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTICS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R27] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    file_path = args.output / ("GA-Gate09-R27-" + args.profile + "-" + stamp + ".json")
    file_path.write_text(json.dumps(report, indent=2, sort_keys=True,
                                   ensure_ascii=False) + "\n", encoding="utf-8")
    zip_path = args.output / ("GA-Gate09-R27-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(file_path, file_path.name)
    print("[R27] STATUS:", report["status"], flush=True)
    print("[R27] EVIDENCE:", zip_path, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTICS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
