#!/usr/bin/env python3
"""Gate 09 R25: elastic trust-radius feasibility and opportunity diagnostics.

This is a non-authoritative Python numerical experiment. Economic contracts,
R20/R21/R22/R23/R24 source files, F-072 objectives and F-084F settlement
remain unchanged. A passing run does not certify equilibrium or F-059 ties.
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

R24_CI = Path(__file__).resolve().parents[2] / "r24" / "ci"
sys.path.insert(0, str(R24_CI))
import run_r24 as R24
from clearing_r5 import ClearingSettings, _mix, _change_metrics, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

R21 = R24.R21
R22 = R24.R22
ALPHAS = R24.CANDIDATE_ALPHAS
POLICIES = ("elastic_feasible", "elastic_opportunity")
LOW_RADIUS = 0.002
HIGH_RADIUS = 0.5
INITIAL_RADIUS = 0.05
BUDGETS = (25, 49, 97, 193, 385)


def candidate_bank(net, flows, observed, settings):
    """One fresh oracle per candidate; source snapshots never mutated."""
    bank = []
    for alpha in ALPHAS:
        proposed_flows = _mix(flows, net, observed["target"], alpha)
        new_obs = evaluate_r14(net, proposed_flows, settings, ())
        delta = _change_metrics(net, flows, proposed_flows)
        volatility = max(
            delta["max_relative_price_change"],
            delta["max_relative_load_change"],
            delta["max_relative_quantity_change"],
        )
        group_before = max(observed["max_group_gap"], 1e-12)
        overall_before = max(observed["relative_gap"], 1e-12)
        global_ratio = new_obs["relative_gap"] / overall_before
        group_ratio = new_obs["max_group_gap"] / group_before
        # Necessary numerical progress measure, NEVER economic utility.
        merit = max(global_ratio, group_ratio)
        if not all(math.isfinite(x) and x >= 0 for x in
                   (volatility, merit, global_ratio, group_ratio)):
            raise AssertionError("Nonfinite candidate diagnostic")
        bank.append({
            "alpha": alpha, "flow": proposed_flows, "observation": new_obs,
            "delta": delta, "volatility": volatility, "merit": merit,
            "global_ratio": global_ratio, "group_ratio": group_ratio,
        })
    return bank


def pick_candidate(bank, radius, policy):
    """Select from common 5-candidate bank and record every radius exception.

    Feasibility expansion raises the effective soft radius to the smallest
    real movement. Opportunity expansion allows globally better joint merit
    when the initial feasible subset contains no improving candidate.
    Hard ceiling violations are recorded instead of hiding them.
    """
    if policy not in POLICIES or not LOW_RADIUS <= radius <= HIGH_RADIUS:
        raise ValueError("Invalid trust policy or radius")
    min_move = min(t["volatility"] for t in bank)
    required_radius = max(radius, min_move)
    ceiling_exceeded = required_radius > HIGH_RADIUS
    effective_radius = min(required_radius, HIGH_RADIUS)
    feasible = [t for t in bank if t["volatility"] <= effective_radius + 1e-15]
    if not feasible:
        # Only possible if every candidate exceeds the hard maximum.
        feasible = [min(bank, key=lambda t: (t["volatility"], t["merit"], -t["alpha"]))]
    choice = min(feasible, key=lambda t: (t["merit"], -t["alpha"]))
    expanded_opportunity = False
    if policy == "elastic_opportunity" and choice["merit"] >= 1.0:
        global_best = min(bank, key=lambda t: (t["merit"], -t["alpha"]))
        if global_best["merit"] < choice["merit"] - 1e-12 and global_best["merit"] < 1.0:
            choice = global_best
            expanded_opportunity = True

    selected_movement = choice["volatility"]
    # The next radius can contract slowly, but never under the actually
    # necessary movement floor. This avoids the R24 perpetual-shrink mode.
    movement_floor = max(min_move * 1.05, selected_movement * 1.10)
    next_radius = max(LOW_RADIUS, min(HIGH_RADIUS,
                        max(radius * 0.90, movement_floor)))
    return choice, {
        "alpha": choice["alpha"],
        "radius_before": radius,
        "effective_radius": effective_radius,
        "radius_after": next_radius,
        "expanded_for_feasibility": min_move > radius + 1e-15,
        "expanded_for_opportunity": expanded_opportunity,
        "hard_radius_violation": ceiling_exceeded or selected_movement > HIGH_RADIUS,
        "selected_move": selected_movement,
        "minimum_candidate_move": min_move,
        "selected_merit_ratio": choice["merit"],
        "selected_global_ratio": choice["global_ratio"],
        "selected_group_ratio": choice["group_ratio"],
        "candidate_evaluations": len(bank),
        "candidate_audit": [
            {"alpha": t["alpha"], "gap": t["observation"]["relative_gap"],
             "worst_group_gap": t["observation"]["max_group_gap"],
             "movement": t["volatility"], "joint_merit": t["merit"],
             "within_previous_radius": t["volatility"] <= radius + 1e-15}
            for t in bank
        ],
    }


def run_case(cfg, policy, max_updates, oracle_budget, audit_every=16):
    if policy not in POLICIES:
        raise ValueError("Unknown research policy")
    if type(max_updates) is not int or not 1 <= max_updates <= 256:
        raise ValueError("Invalid update budget")
    if type(oracle_budget) is not int or not 6 <= oracle_budget <= 2048:
        raise ValueError("Invalid oracle budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit frequency")

    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    current = evaluate_r14(net, flows, settings, ())
    radius = INITIAL_RADIUS
    oracle_count = 1
    rows, audits = [], []
    current_delta, previous_target, last_step = None, None, None
    stable_count = 0
    first_three_stable = None
    expansion_feasible = 0
    expansion_progress = 0
    ceiling_violations = 0
    start = perf_counter()

    for step in range(max_updates + 1):
        if step % audit_every == 0 or step == max_updates:
            R21.frozen_check(net, flows, current)
            audit = R21.settlement_check(net, flows)
            audits.append({"step": step, **audit})
        if current["reachable_unpriced"] or any(
                not math.isfinite(price) or price <= 0
                for price in current["prices"].values()):
            raise AssertionError("Unpriced market")
        attribution = R22.attribute_gap(net, current)
        numerical = R24.numerical_pass(current, current_delta)
        stable_count = stable_count + 1 if numerical else 0
        if stable_count >= 3 and first_three_stable is None:
            first_three_stable = step
        switches = 0 if previous_target is None else sum(
            previous_target[k] != current["target"][k]
            for k in current["target"])
        rows.append({
            "step": step, "oracles_used": oracle_count,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": current["relative_gap"],
            "max_group_gap": current["max_group_gap"],
            "price_change": None if current_delta is None else
                current_delta["max_relative_price_change"],
            "load_change": None if current_delta is None else
                current_delta["max_relative_load_change"],
            "quantity_change": None if current_delta is None else
                current_delta["max_relative_quantity_change"],
            "numerical_five_thresholds_met": numerical,
            "stable_rechecks": stable_count,
            "best_route_switches": switches,
            "top_three_regret_share": attribution["top_three_share_of_total_regret"],
            "tie_groups": len(current["tie_groups"]),
            "step_decision": last_step,
        })
        if step == max_updates or oracle_count + len(ALPHAS) > oracle_budget:
            break
        bank = candidate_bank(net, flows, current, settings)
        chosen, decision = pick_candidate(bank, radius, policy)
        expansion_feasible += int(decision["expanded_for_feasibility"])
        expansion_progress += int(decision["expanded_for_opportunity"])
        ceiling_violations += int(decision["hard_radius_violation"])
        oracle_count += len(bank)
        previous_target = current["target"]
        current_delta = chosen["delta"]
        flows, current = chosen["flow"], chosen["observation"]
        radius = decision["radius_after"]
        last_step = decision

    # Last checkpoint must be independently reconciled, even if the cost
    # budget stopped before a scheduled audit.
    if audits[-1]["step"] != rows[-1]["step"]:
        R21.frozen_check(net, flows, current)
        financial = R21.settlement_check(net, flows)
        audits.append({"step": rows[-1]["step"], **financial})
    last = rows[-1]
    best_index = min(range(len(rows)), key=lambda i: (rows[i]["relative_gap"], i))
    return {
        "policy": policy,
        "nodes": 10 * cfg.m * cfg.m + 2,
        "steps": len(rows) - 1,
        "oracle_budget": oracle_budget,
        "oracles_used": oracle_count,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": last["relative_gap"],
        "final_max_group_gap": last["max_group_gap"],
        "best_gap": rows[best_index]["relative_gap"],
        "best_gap_step": best_index,
        "final_price_change": last["price_change"],
        "final_load_change": last["load_change"],
        "final_quantity_change": last["quantity_change"],
        "feasibility_expansions": expansion_feasible,
        "opportunity_expansions": expansion_progress,
        "hard_ceiling_violations": ceiling_violations,
        "first_three_joint_stable_step": first_three_stable,
        "last_soft_radius": radius,
        "rows": rows,
        "financial_audits": audits,
        "frontiers": R24.frontier_snapshots(rows),
        "final_flow_sha256": last["flow_sha256"],
        "elapsed_ms_observational": round((perf_counter() - start) * 1000, 3),
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def validate_budget_and_prefix(cfg):
    short = run_case(cfg, "elastic_feasible", 4, 81, audit_every=2)
    long = run_case(cfg, "elastic_feasible", 7, 81, audit_every=2)
    if short["rows"] != long["rows"][:len(short["rows"])]:
        raise AssertionError("Prefix of elastic strategy drifted")
    for policy in POLICIES:
        r = run_case(cfg, policy, 12, 26)
        if r["oracles_used"] != 1 + r["steps"] * len(ALPHAS):
            raise AssertionError("Missing or unbilled oracle evaluation")
        if r["oracles_used"] > 26:
            raise AssertionError("Oracle budget exceeded")
        if r["financial_audits"][-1]["step"] != r["steps"]:
            raise AssertionError("Final state not settled")
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
        raise ValueError("Invalid profile")
    output = []
    for label, iterations, budget in plans:
        scenario = R21.CASES[label]
        rows = []
        for method in ("decay_sqrt", "backtracking", "trust_joint",
                       "elastic_feasible", "elastic_opportunity"):
            if method in POLICIES:
                result = run_case(scenario, method, iterations, budget)
                fronts = result["frontiers"]
            else:
                result = R24.run_case(scenario, method, iterations, budget)
                fronts = result["oracle_budget_frontiers"]
            print("[{}:{}] gap={:.7g} worst={:.7g} "
                  "priceChange={:.4g} loadChange={:.4g} "
                  "evals={} updates={} stable={}".format(
                      label, method, result["final_gap"],
                      result["final_max_group_gap"],
                      result["final_price_change"], result["final_load_change"],
                      result["oracles_used"],
                      result.get("steps", result.get("updates_executed")),
                      result.get("first_three_joint_stable_step")), flush=True)
            rows.append({"method": method, "results": result,
                         "cost_frontiers": fronts})
        output.append({"case": label, "methods": rows})
    validate_budget_and_prefix(R21.CASES["small"])
    return {"comparisons": output, "budget_and_prefix_verified": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R25_ELASTIC_TRUST_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "global_equilibrium_proven": False, "financial_F059_ratified": False,
        "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("Upstream baseline drift")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTICS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R25] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = args.output / ("GA-Gate09-R25-" + args.profile + "-" + stamp + ".json")
    path.write_text(json.dumps(report, indent=2, sort_keys=True,
                               ensure_ascii=False) + "\n", encoding="utf-8")
    archive_path = args.output / ("GA-Gate09-R25-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(path, path.name)
    print("[R25] STATUS:", report["status"], flush=True)
    print("[R25] EVIDENCE:", archive_path, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTICS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
