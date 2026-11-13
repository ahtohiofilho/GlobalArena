#!/usr/bin/env python3
"""GA Gate 09 R26: budget-aware progressive F-072 line search.

Research-only numerical policies; R20-R25 are imported unchanged.
F-072 prices, congestion, routing, F-084F financial settlement and
full-sale invariants retain their original definitions. Neither the
choice of alpha nor a successful test certifies an equilibrium or F-059.
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
sys.path.insert(0, str(GATE09 / "r25" / "ci"))
import run_r25 as R25

R24 = R25.R24
R21 = R25.R21
R22 = R25.R22
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum, _mix
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

ALPHAS = (1 / 4, 1 / 8, 1 / 16, 1 / 32, 1 / 64)
POLICIES = ("progressive_global", "progressive_joint")
EPS = 1e-12
FRONTIERS = (25, 49, 97, 193, 385)


def acceptable(policy, old_gap, old_worst, next_gap, next_worst):
    """Early-stop rule for a fully repriced NEXT snapshot, never stale state."""
    if policy not in POLICIES:
        raise ValueError("Unknown policy")
    for value in (old_gap, old_worst, next_gap, next_worst):
        if not math.isfinite(value) or value < 0:
            raise ValueError("Invalid numerical regret")
    global_ok = next_gap <= old_gap + EPS
    worst_ok = next_worst <= old_worst + EPS
    return global_ok if policy == "progressive_global" else global_ok and worst_ok


def trial_rank(policy, record):
    """Choose fallback among *already evaluated* candidates only."""
    if policy == "progressive_global":
        return (record["relative_gap"], -record["alpha"])
    return (record["joint_ratio"], record["relative_gap"],
            record["max_group_gap"], -record["alpha"])


def choose_progressive(net, flows, observed, settings, policy, remaining):
    """Evaluate proposals sequentially, up to an explicit finite oracle budget.

    Every candidate's _mix and R14 evaluation uses the SAME old flows,
    prices, and best-path target. All trial snapshots are priced at
    their own new loads/quantities before acceptance. The unused alpha
    candidates incur no evaluator call. No freeze-on-failure is allowed.
    """
    if policy not in POLICIES:
        raise ValueError("Unknown progressive policy")
    if type(remaining) is not int or not 1 <= remaining <= len(ALPHAS):
        raise ValueError("Invalid remaining oracle allowance")

    old_gap = observed["relative_gap"]
    old_worst = observed["max_group_gap"]
    trials = []
    winner = None
    accepted = False
    for alpha in ALPHAS[:remaining]:
        candidate = _mix(flows, net, observed["target"], alpha)
        priced = evaluate_r14(net, candidate, settings, ())
        delta = _change_metrics(net, flows, candidate)
        g = priced["relative_gap"]
        w = priced["max_group_gap"]
        if not all(math.isfinite(x) and x >= 0 for x in (g, w)):
            raise AssertionError("Nonfinite candidate gap")
        joint_ratio = max(g / max(old_gap, EPS),
                          w / max(old_worst, EPS))
        record = {
            "alpha": alpha, "flow": candidate, "observation": priced,
            "delta": delta, "relative_gap": g, "max_group_gap": w,
            "joint_ratio": joint_ratio,
            "accepted": acceptable(policy, old_gap, old_worst, g, w),
        }
        trials.append(record)
        if record["accepted"]:
            accepted = True
            winner = record
            break

    if winner is None:
        winner = min(trials, key=lambda r: trial_rank(policy, r))
    if len(trials) > remaining:
        raise AssertionError("Unbilled candidate evaluation")

    return winner["flow"], winner["observation"], winner["delta"], {
        "selected_alpha": winner["alpha"],
        "candidate_count": len(trials),
        "accepted_early": accepted,
        "fallback_no_acceptable_step": not accepted,
        "candidate_budget_truncated": not accepted and remaining < len(ALPHAS),
        "selected_joint_ratio": winner["joint_ratio"],
        "trials": [{
            "alpha": t["alpha"],
            "relative_gap": t["relative_gap"],
            "max_group_gap": t["max_group_gap"],
            "joint_ratio": t["joint_ratio"],
            "accepted": t["accepted"],
            "price_change": t["delta"]["max_relative_price_change"],
            "load_change": t["delta"]["max_relative_load_change"],
            "quantity_change": t["delta"]["max_relative_quantity_change"],
        } for t in trials],
    }


def run_case(cfg, policy, max_updates, oracle_budget, audit_every=16):
    if policy not in POLICIES:
        raise ValueError("Unknown policy")
    if type(max_updates) is not int or not 1 <= max_updates <= 256:
        raise ValueError("Invalid update budget")
    if type(oracle_budget) is not int or not 2 <= oracle_budget <= 2048:
        raise ValueError("Invalid oracle work budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit cadence")
    network, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(network, flows, settings, ())
    evaluations = 1
    rows, audits = [], []
    previous_target = None
    prior_delta = None
    decision = None
    accepted_count = 0
    fallback_count = 0
    truncated_count = 0
    trial_total = 0
    numerical_streak = 0
    first_stable = None
    start = perf_counter()

    for step in range(max_updates + 1):
        if step % audit_every == 0 or step == max_updates:
            R21.frozen_check(network, flows, obs)
            audited = R21.settlement_check(network, flows)
            audits.append({"step": step, **audited})
        if obs["reachable_unpriced"] or any(
            not math.isfinite(p) or p <= 0 for p in obs["prices"].values()
        ):
            raise AssertionError("Nonpositive market quote")
        attrib = R22.attribute_gap(network, obs)
        stable_now = R24.numerical_pass(obs, prior_delta)
        numerical_streak = numerical_streak + 1 if stable_now else 0
        if numerical_streak >= 3 and first_stable is None:
            first_stable = step
        churn = (0 if previous_target is None else
                 sum(previous_target[gid] != obs["target"][gid]
                     for gid in obs["target"]))
        rows.append({
            "step": step,
            "oracles_used": evaluations,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "price_change": None if prior_delta is None else
                prior_delta["max_relative_price_change"],
            "load_change": None if prior_delta is None else
                prior_delta["max_relative_load_change"],
            "quantity_change": None if prior_delta is None else
                prior_delta["max_relative_quantity_change"],
            "best_route_switches": churn,
            "tie_groups": len(obs["tie_groups"]),
            "top_three_regret_share": attrib["top_three_share_of_total_regret"],
            "numerical_five_thresholds_met": stable_now,
            "consecutive_numerical_rechecks": numerical_streak,
            "decision": decision,
        })

        if step == max_updates or evaluations >= oracle_budget:
            break

        previous_target = obs["target"]
        next_flow, next_obs, next_delta, info = choose_progressive(
            network, flows, obs, settings, policy,
            min(len(ALPHAS), oracle_budget - evaluations))
        evaluations += info["candidate_count"]
        trial_total += info["candidate_count"]
        accepted_count += int(info["accepted_early"])
        fallback_count += int(info["fallback_no_acceptable_step"])
        truncated_count += int(info["candidate_budget_truncated"])
        flows, obs, prior_delta, decision = next_flow, next_obs, next_delta, info

    # A cost-triggered early termination must still reconcile FINAL flows.
    if audits[-1]["step"] != rows[-1]["step"]:
        R21.frozen_check(network, flows, obs)
        audited = R21.settlement_check(network, flows)
        audits.append({"step": rows[-1]["step"], **audited})
    if evaluations != 1 + sum(
        row["decision"]["candidate_count"] for row in rows[1:]
    ):
        raise AssertionError("Incomplete oracle accounting")
    if evaluations > oracle_budget:
        raise AssertionError("Exceeded oracle budget")

    last = rows[-1]
    best_idx = min(range(len(rows)),
                   key=lambda i: (rows[i]["relative_gap"], i))
    return {
        "policy": policy, "nodes": 10 * cfg.m * cfg.m + 2,
        "groups": len(network.groups), "updates_executed": len(rows) - 1,
        "max_updates_requested": max_updates,
        "oracle_budget": oracle_budget, "oracles_used": evaluations,
        "evaluations_per_update": trial_total / max(1, len(rows) - 1),
        "accepted_early_steps": accepted_count,
        "fallback_steps": fallback_count,
        "truncated_search_steps": truncated_count,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": last["relative_gap"],
        "final_max_group_gap": last["max_group_gap"],
        "best_observed_gap": rows[best_idx]["relative_gap"],
        "best_observed_step": best_idx,
        "final_price_change": last["price_change"],
        "final_load_change": last["load_change"],
        "final_quantity_change": last["quantity_change"],
        "first_three_joint_stable_step": first_stable,
        "total_best_route_switches": sum(r["best_route_switches"] for r in rows),
        "financial_audits": audits,
        "oracle_budget_frontiers": R24.frontier_snapshots(rows),
        "rows": rows,
        "final_flow_sha256": last["flow_sha256"],
        "elapsed_ms_observational": round((perf_counter() - start) * 1000, 3),
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "authoritative_solver_approved": False,
    }


def replay_global_acceptance(cfg, steps):
    """When R21 accepts the same first alpha, R26 must match its next state.

    We do not assert arbitrary full-run equality: R26 adds a fifth candidate
    that may legitimately change a fallback when all first four fail.
    """
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    matched = 0
    for step in range(1, steps + 1):
        a_flow, a_obs, a_alpha, consumed, forced = R21.choose_step(
            net, flows, obs, settings, "backtracking", step)
        b_flow, b_obs, _, info = choose_progressive(
            net, flows, obs, settings, "progressive_global", 5)
        if not forced:
            if a_alpha != info["selected_alpha"] or (
                _flow_checksum(a_flow) != _flow_checksum(b_flow)
            ) or a_obs["relative_gap"] != b_obs["relative_gap"]:
                raise AssertionError("First-acceptable R21/R26 drift")
            matched += 1
        flows, obs = a_flow, a_obs
    return matched


def exercise(profile):
    if profile == "smoke":
        plans = (("small", 8, 33),)
    elif profile == "ci":
        plans = (("small", 96, 385), ("medium", 48, 193))
    elif profile == "extended":
        plans = (("small", 160, 641), ("medium", 96, 385),
                 ("large", 24, 97))
    else:
        raise ValueError("Unknown profile")
    output = []
    for label, max_updates, budget in plans:
        cfg = R21.CASES[label]
        comparisons = []
        for policy in ("decay_sqrt", "backtracking", "elastic_opportunity",
                       "progressive_global", "progressive_joint"):
            if policy == "elastic_opportunity":
                raw = R25.run_case(cfg, policy, max_updates, budget)
                frontiers = raw["frontiers"]
                updates = raw["steps"]
            elif policy in R24.POLICIES:
                raw = R24.run_case(cfg, policy, max_updates, budget)
                frontiers = raw["oracle_budget_frontiers"]
                updates = raw["updates_executed"]
            else:
                raw = run_case(cfg, policy, max_updates, budget)
                frontiers = raw["oracle_budget_frontiers"]
                updates = raw["updates_executed"]
            print("[{}:{}] gap={:.7g} worst={:.7g} "
                  "priceChange={:.4g} loadChange={:.4g} "
                  "evals={} updates={} stable={}".format(
                      label, policy, raw["final_gap"],
                      raw["final_max_group_gap"],
                      raw["final_price_change"], raw["final_load_change"],
                      raw["oracles_used"], updates,
                      raw["first_three_joint_stable_step"]),
                  flush=True)
            comparisons.append({
                "policy": policy, "result": raw,
                "oracle_frontiers": frontiers,
            })
        output.append({"scenario": label, "methods": comparisons})

    # Preserve original numerical logic and require deterministic prefixes.
    for original in ("decay_sqrt", "backtracking"):
        R24.check_r21_reference(R21.CASES["small"], original, 6)
    for method in POLICIES:
        short = run_case(R21.CASES["small"], method, 4, 51, audit_every=2)
        long = run_case(R21.CASES["small"], method, 8, 51, audit_every=2)
        if short["rows"] != long["rows"][:len(short["rows"])]:
            raise AssertionError("Progressive trajectory not prefix-consistent")
    matched = replay_global_acceptance(R21.CASES["small"], 8)
    return {
        "scenarios": output,
        "upstream_r21_reference_verified": True,
        "progressive_prefix_verified": True,
        "r21_first_acceptance_replay_matches": matched,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R26_PROGRESSIVE_ORACLE_BUDGET_SEARCH_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("R20 frozen baseline drift")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTIC_CONTRACTS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R26] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    output_file = args.output / (
        "GA-Gate09-R26-" + args.profile + "-" + stamp + ".json")
    output_file.write_text(json.dumps(report, ensure_ascii=False,
                                      sort_keys=True, indent=2) + "\n",
                           encoding="utf-8")
    zip_file = args.output / ("GA-Gate09-R26-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(zip_file, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(output_file, output_file.name)
    print("[R26] STATUS:", report["status"], flush=True)
    print("[R26] EVIDENCE:", zip_file, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTIC_CONTRACTS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
