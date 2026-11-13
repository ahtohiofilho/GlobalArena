#!/usr/bin/env python3
"""GA Gate09 R24: bounded trust-region *numerical* clearing experiment.

R20 F-072/F-084F and R21/R22/R23 laboratories are untouched. Neither this
policy nor its candidate merit is an economic law, an equilibrium theorem,
an authoritative runtime change, or a ratification of financial ties F-059.
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

R21_PATH = Path(__file__).resolve().parents[2] / "r21" / "ci"
R22_PATH = Path(__file__).resolve().parents[2] / "r22" / "ci"
sys.path[:0] = [str(R21_PATH), str(R22_PATH)]
import run_r21 as R21
import run_r22 as R22
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum, _mix
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

TARGET = 1e-4
CANDIDATE_ALPHAS = (1 / 4, 1 / 8, 1 / 16, 1 / 32, 1 / 64)
POLICIES = ("decay_sqrt", "backtracking", "trust_joint", "trust_global")
FRONTIERS = (25, 49, 97, 193, 385)


def numerical_pass(obs, delta):
    """Five necessary numerical conditions, with no financial certification."""
    return bool(
        delta is not None
        and obs["relative_gap"] <= TARGET
        and obs["max_group_gap"] <= TARGET
        and delta["max_relative_price_change"] <= TARGET
        and delta["max_relative_load_change"] <= TARGET
        and delta["max_relative_quantity_change"] <= TARGET
    )


def choose_trust(net, flows, obs, settings, policy, radius):
    """Compare five candidate NEXT snapshots using the same frozen input.

    Feasibility limits simultaneous price, load and quantity movement.
    The two numerical ranking criteria are deliberately *not* economic
    preferences or player options. No candidate is suppressed from the audit.
    """
    if policy not in ("trust_joint", "trust_global"):
        raise ValueError("Not a trust experiment")
    if not 0.002 <= radius <= 0.2:
        raise ValueError("Invalid numerical trust radius")
    trials = []
    for alpha in CANDIDATE_ALPHAS:
        candidate = _mix(flows, net, obs["target"], alpha)
        successor = evaluate_r14(net, candidate, settings, ())
        movement = _change_metrics(net, flows, candidate)
        volatility = max(movement["max_relative_price_change"],
                         movement["max_relative_load_change"],
                         movement["max_relative_quantity_change"])
        original_global = max(obs["relative_gap"], 1e-12)
        original_group = max(obs["max_group_gap"], 1e-12)
        global_ratio = successor["relative_gap"] / original_global
        worst_ratio = successor["max_group_gap"] / original_group
        merit = max(global_ratio, worst_ratio) if policy == "trust_joint" else global_ratio
        trials.append({
            "alpha": alpha, "flow": candidate, "observation": successor,
            "delta": movement, "volatility": volatility,
            "merit": merit, "global_ratio": global_ratio,
            "worst_group_ratio": worst_ratio,
            "within_radius": volatility <= radius + 1e-15,
        })
    admissible = [x for x in trials if x["within_radius"]]
    # If the radius is too small, select the minimum-volatility candidate and
    # record this violation explicitly; never freeze the flow to fake stability.
    forced = len(admissible) == 0
    if forced:
        selected = min(trials, key=lambda x: (x["volatility"], x["merit"], -x["alpha"]))
    else:
        selected = min(admissible, key=lambda x: (x["merit"], -x["alpha"]))
    next_radius = radius
    if forced or selected["merit"] > 1.02:
        next_radius = max(0.002, radius * 0.75)
    elif selected["merit"] < 0.99 and selected["volatility"] < radius / 2:
        next_radius = min(0.2, radius * 1.25)
    # Candidate count is five even when the first alpha would have sufficed.
    # These evaluations must all be charged to the research work budget.
    trial_audit = [{
        "alpha": x["alpha"],
        "relative_gap": x["observation"]["relative_gap"],
        "max_group_gap": x["observation"]["max_group_gap"],
        "volatility": x["volatility"],
        "within_radius": x["within_radius"],
        "merit_ratio": x["merit"],
    } for x in trials]
    return (selected["flow"], selected["observation"], selected["delta"], {
        "alpha": selected["alpha"],
        "radius_before": radius,
        "radius_after": next_radius,
        "forced_radius_violation": forced,
        "selected_merit_ratio": selected["merit"],
        "selected_global_ratio": selected["global_ratio"],
        "selected_worst_group_ratio": selected["worst_group_ratio"],
        "candidate_count": len(trials),
        "trials": trial_audit,
    })


def step_policy(net, flows, obs, settings, policy, step, radius):
    if policy in ("decay_sqrt", "backtracking"):
        successor, scored, alpha, candidates, forced_uphill = R21.choose_step(
            net, flows, obs, settings, policy, step)
        delta = _change_metrics(net, flows, successor)
        return successor, scored, delta, {
            "alpha": alpha,
            "radius_before": None, "radius_after": None,
            "forced_radius_violation": False,
            "forced_uphill_step": forced_uphill,
            "candidate_count": candidates, "trials": [],
        }
    return choose_trust(net, flows, obs, settings, policy, radius)


def validate_money_and_parity(net, flows, obs):
    R21.frozen_check(net, flows, obs)
    return R21.settlement_check(net, flows)


def frontier_snapshots(rows):
    """Same oracle-evaluation budget comparisons; no interpolated metrics."""
    result = {}
    for b in FRONTIERS:
        eligible = [r for r in rows if r["oracles_used"] <= b]
        if eligible:
            r = eligible[-1]
            result[str(b)] = {
                "step": r["step"], "oracles_used": r["oracles_used"],
                "relative_gap": r["relative_gap"],
                "max_group_gap": r["max_group_gap"],
                "price_change": r["price_change"],
                "load_change": r["load_change"],
                "quantity_change": r["quantity_change"],
                "numerical_five_thresholds_met": r["numerical_five_thresholds_met"],
            }
    return result


def run_case(cfg, policy, max_updates, oracle_budget, audit_every=16):
    if policy not in POLICIES or type(max_updates) is not int or not 1 <= max_updates <= 256:
        raise ValueError("Invalid policy or update budget")
    if type(oracle_budget) is not int or oracle_budget < 6 or oracle_budget > 2048:
        raise ValueError("Invalid oracle work budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit interval")
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    oracle_count = 1
    radius = 0.05
    rows = []
    audits = []
    total_radius_violations = 0
    total_forced_uphill = 0
    stable = 0
    first_stable = None
    current_delta = None
    current_step_info = None
    previous_target = None
    t0 = perf_counter()

    for step in range(max_updates + 1):
        if step % audit_every == 0 or step == max_updates:
            money = validate_money_and_parity(net, flows, obs)
            audits.append({"step": step, **money})
        if obs["reachable_unpriced"] or any(
            not math.isfinite(v) or v <= 0 for v in obs["prices"].values()
        ):
            raise AssertionError("Market price not finite and positive")
        attribution = R22.attribute_gap(net, obs)
        stress = R22.edge_stress(net, obs)
        is_stable = numerical_pass(obs, current_delta)
        stable = stable + 1 if is_stable else 0
        if stable >= 3 and first_stable is None:
            first_stable = step
        switches = (0 if previous_target is None else sum(
            previous_target[k] != obs["target"][k] for k in obs["target"]))
        rows.append({
            "step": step,
            "oracles_used": oracle_count,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "price_change": None if current_delta is None else
                current_delta["max_relative_price_change"],
            "load_change": None if current_delta is None else
                current_delta["max_relative_load_change"],
            "quantity_change": None if current_delta is None else
                current_delta["max_relative_quantity_change"],
            "tie_group_count": len(obs["tie_groups"]),
            "best_route_switches": switches,
            "top_three_regret_share": attribution["top_three_share_of_total_regret"],
            "max_load_capacity_ratio": stress["maximum_load_capacity_ratio"],
            "numerical_five_thresholds_met": is_stable,
            "stable_rechecks": stable,
            "alpha_used": None if current_step_info is None else current_step_info["alpha"],
            "trust_radius_before": None if current_step_info is None else
                current_step_info["radius_before"],
            "trust_radius_after": None if current_step_info is None else
                current_step_info["radius_after"],
            "forced_radius_violation": False if current_step_info is None else
                current_step_info["forced_radius_violation"],
            "candidate_count": 0 if current_step_info is None else
                current_step_info["candidate_count"],
            "candidate_trials": [] if current_step_info is None else
                current_step_info["trials"],
        })
        if step == max_updates:
            break
        # Avoid an unaccounted evaluation by reserving the maximum possible
        # number of trial oracle calls BEFORE the update.
        reserved = 5 if policy.startswith("trust_") else 4 if policy == "backtracking" else 1
        if oracle_count + reserved > oracle_budget:
            break
        old_target = obs["target"]
        successor, new_obs, next_delta, info = step_policy(
            net, flows, obs, settings, policy, step + 1, radius)
        if oracle_count + info["candidate_count"] > oracle_budget:
            raise AssertionError("Oracle budget exceeded")
        oracle_count += info["candidate_count"]
        previous_target = old_target
        total_radius_violations += int(info["forced_radius_violation"])
        total_forced_uphill += int(info.get("forced_uphill_step", False))
        if info["radius_after"] is not None:
            radius = info["radius_after"]
        flows, obs = successor, new_obs
        current_delta = next_delta
        current_step_info = info

    # If the budget stopped early between periodic audits, additionally
    # reconcile the last snapshot. Never report audit success without it.
    if audits[-1]["step"] != rows[-1]["step"]:
        money = validate_money_and_parity(net, flows, obs)
        audits.append({"step": rows[-1]["step"], **money})
    last = rows[-1]
    best_idx = min(range(len(rows)), key=lambda i: (rows[i]["relative_gap"], i))
    return {
        "policy": policy,
        "nodes": 10 * cfg.m * cfg.m + 2,
        "producer_groups": len(net.groups),
        "updates_executed": len(rows) - 1,
        "oracles_used": oracle_count,
        "max_updates_requested": max_updates,
        "oracle_budget": oracle_budget,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": last["relative_gap"],
        "final_max_group_gap": last["max_group_gap"],
        "best_gap_observed": rows[best_idx]["relative_gap"],
        "best_gap_step": best_idx,
        "final_price_change": last["price_change"],
        "final_load_change": last["load_change"],
        "final_quantity_change": last["quantity_change"],
        "final_top_three_regret_share": last["top_three_regret_share"],
        "total_best_route_switches": sum(r["best_route_switches"] for r in rows),
        "forced_radius_violations": total_radius_violations,
        "forced_uphill_steps": total_forced_uphill,
        "first_three_joint_stable_step": first_stable,
        "financial_audits": audits,
        "oracle_budget_frontiers": frontier_snapshots(rows),
        "rows": rows,
        "final_flow_sha256": last["flow_sha256"],
        "elapsed_ms_observational": round((perf_counter() - t0) * 1000, 3),
        "equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def check_r21_reference(cfg, policy, steps):
    if policy not in ("decay_sqrt", "backtracking"):
        raise ValueError("No reference available")
    old = R21.run_trajectory(cfg, policy, steps, audit_every=steps)
    new = run_case(cfg, policy, steps, oracle_budget=5 * steps + 1,
                   audit_every=steps)
    if len(old["rows"]) != len(new["rows"]):
        raise AssertionError("Wrong R21 trajectory length")
    for x, y in zip(old["rows"], new["rows"]):
        for k in ("relative_gap", "max_group_gap", "flow_sha256"):
            if x[k] != y[k]:
                raise AssertionError("R21 reference drift at step {}".format(y["step"]))
    return True


def exercise(profile):
    if profile == "smoke":
        scenarios = (("small", 8, 33),)
    elif profile == "ci":
        scenarios = (("small", 96, 385), ("medium", 48, 193))
    elif profile == "extended":
        scenarios = (("small", 160, 641), ("medium", 96, 385),
                     ("large", 24, 97))
    else:
        raise ValueError("Unknown profile")

    reports = []
    for case_name, steps, budget in scenarios:
        cases = []
        for policy in POLICIES:
            result = run_case(R21.CASES[case_name], policy, steps, budget)
            cases.append(result)
            print(
                "[{}:{}] gap={:.7g} max={:.7g} "
                "priceChange={:.5g} loadChange={:.5g} "
                "evals={} updates={} stable={} forced={}".format(
                    case_name, policy, result["final_gap"],
                    result["final_max_group_gap"],
                    result["final_price_change"], result["final_load_change"],
                    result["oracles_used"], result["updates_executed"],
                    result["first_three_joint_stable_step"],
                    result["forced_radius_violations"],
                ), flush=True)
        reports.append({"case": case_name, "policies": cases,
                        "same_oracle_budget_frontiers": list(FRONTIERS)})
    for policy in ("decay_sqrt", "backtracking"):
        check_r21_reference(R21.CASES["small"], policy, 6)
    shorter = run_case(R21.CASES["small"], "trust_joint", 4, 81)
    longer = run_case(R21.CASES["small"], "trust_joint", 7, 81)
    if shorter["rows"] != longer["rows"][:len(shorter["rows"])]:
        raise AssertionError("Trust trajectory prefix drift")
    return {"scenarios": reports, "r21_reference_replay": True,
            "trust_prefix_verified": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"), default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R24_BOUNDED_TRUST_REGION_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "profile": args.profile, "result": "FAILED",
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "authoritative_runtime_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("R20 provenance drift")
        report.update(exercise(args.profile))
        report["result"] = "PASSED_DIAGNOSTIC_CONTRACTS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R24] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    audit_path = args.output / ("GA-Gate09-R24-" + args.profile + "-" + stamp + ".json")
    audit_path.write_text(json.dumps(report, indent=2, sort_keys=True,
                                     ensure_ascii=False) + "\n", encoding="utf-8")
    archive_path = args.output / ("GA-Gate09-R24-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(audit_path, audit_path.name)
    print("[R24] STATUS:", report["result"], flush=True)
    print("[R24] EVIDENCE:", archive_path, flush=True)
    return 0 if report["result"] == "PASSED_DIAGNOSTIC_CONTRACTS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
