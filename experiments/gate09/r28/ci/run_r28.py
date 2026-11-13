#!/usr/bin/env python3
"""GA Gate 09 R28: guarded candidate search with cumulative regret per oracle.

Only numerical choice of damping alpha changes. F-072 quotes, shortest routes,
congestion and F-084F monetary settlement are evaluated by immutable R20.
R21 through R27 imports are untouched; no C# authority or F-059 ratification.
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

R27_CI = Path(__file__).resolve().parents[2] / "r27" / "ci"
sys.path.insert(0, str(R27_CI))
import run_r27 as R27

R26 = R27.R26
R24 = R27.R24
R21 = R27.R21
R22 = R27.R22
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum, _mix
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

POLICIES = ("guard_decay_global", "guard_decay_joint", "guard_fixed_global")
FIXED_ALPHAS = R26.ALPHAS
TARGET = 1e-4
MIN_RELATIVE_PROGRESS = 0.01
EPS = 1e-12
FRONTIERS = R26.FRONTIERS


def candidate_order(policy, step):
    """Use R21 deterministic decay as candidate zero, or fixed descending grid.

    Only the *alpha* is predicted. Prices, loads, quantities, scores and
    route choices from previous snapshots are never reused as evaluation.
    A duplicate alpha at steps 1 or 25 costs only one oracle call.
    """
    if policy not in POLICIES or type(step) is not int or step < 1:
        raise ValueError("Invalid policy or step")
    if policy == "guard_fixed_global":
        return FIXED_ALPHAS
    decay = (1 / 4) / math.sqrt(1 + (step - 1) / 8)
    return (decay,) + tuple(a for a in FIXED_ALPHAS if a != decay)


def sufficient(policy, old_global, old_worst, new_global, new_worst):
    """One-percent meaningful aggregate decrease, with optional group veto.

    This is an EXPERIMENTAL numerical stopping rule, not a modification of
    trade demand or social welfare. A state already below 1e-4 may accept
    nonincrease, but only the separate five-gate test measures stability.
    """
    if policy not in POLICIES:
        raise ValueError("Unknown policy")
    if any(not math.isfinite(x) or x < 0 for x in
           (old_global, old_worst, new_global, new_worst)):
        raise ValueError("Invalid gap")
    meaningful = new_global <= (old_global * (1 - MIN_RELATIVE_PROGRESS) + EPS)
    if old_global <= TARGET:
        meaningful = new_global <= old_global + EPS
    if policy == "guard_decay_joint":
        return meaningful and new_worst <= old_worst + EPS
    return meaningful


def choose_guarded(net, flows, observed, settings, policy, step, remaining):
    order = candidate_order(policy, step)
    if type(remaining) is not int or not 1 <= remaining <= len(order):
        raise ValueError("Invalid oracle remaining budget")

    trials = []
    accepted = None
    old_gap, old_worst = observed["relative_gap"], observed["max_group_gap"]
    for alpha in order[:remaining]:
        next_flows = _mix(flows, net, observed["target"], alpha)
        priced = evaluate_r14(net, next_flows, settings, ())
        changes = _change_metrics(net, flows, next_flows)
        gap, worst = priced["relative_gap"], priced["max_group_gap"]
        okay = sufficient(policy, old_gap, old_worst, gap, worst)
        trials.append({
            "alpha": alpha, "flows": next_flows, "observation": priced,
            "delta": changes, "gap": gap, "worst": worst,
            "joint_ratio": max(gap / max(old_gap, EPS),
                               worst / max(old_worst, EPS)),
            "accepted": okay,
        })
        if okay:
            accepted = trials[-1]
            break

    if accepted is None:
        if policy == "guard_decay_joint":
            accepted = min(trials, key=lambda x: (
                x["joint_ratio"], x["gap"], x["worst"], -x["alpha"]))
        else:
            accepted = min(trials, key=lambda x: (x["gap"], -x["alpha"]))

    audit = {
        "selected_alpha": accepted["alpha"],
        "candidate_order_examined": [x["alpha"] for x in trials],
        "candidate_count": len(trials),
        "stopped_by_progress_gate": accepted["accepted"],
        "fallback_no_sufficient_progress": not accepted["accepted"],
        "budget_truncated": not accepted["accepted"] and remaining < len(order),
        "selected_joint_ratio": accepted["joint_ratio"],
        "candidate_trials": [{
            "alpha": t["alpha"], "gap": t["gap"], "worst": t["worst"],
            "accepted": t["accepted"],
            "price_change": t["delta"]["max_relative_price_change"],
            "load_change": t["delta"]["max_relative_load_change"],
            "quantity_change": t["delta"]["max_relative_quantity_change"],
        } for t in trials],
    }
    return (accepted["flows"], accepted["observation"],
            accepted["delta"], audit)


def cumulative_work_gap(rows):
    """Area-under-gap per *spent* oracle, with held last committed state.

    This non-anticipatory cost proxy counts every discarded candidate:
    their oracle cost accrues at the old accepted gap until an update
    commits. It does not represent C# CPU time or financial utility.
    """
    total = 0.0
    work = 0
    for old, new in zip(rows, rows[1:]):
        used = new["oracles_used"] - old["oracles_used"]
        if used < 1:
            raise AssertionError("Missing candidate work")
        total += used * old["relative_gap"]
        work += used
    return total / work if work else rows[0]["relative_gap"]


def budget_snapshots(rows):
    """Real evaluated states only, plus comparable cumulative work metric."""
    data = R24.frontier_snapshots(rows)
    for k, point in data.items():
        subset = [r for r in rows if r["oracles_used"] <= int(k)]
        point["cumulative_global_gap_per_oracle"] = cumulative_work_gap(subset)
        point["observed_end_work"] = subset[-1]["oracles_used"]
    return data


def run_case(cfg, policy, max_updates, oracle_budget, audit_every=16):
    if policy not in POLICIES:
        raise ValueError("Unknown policy")
    if type(max_updates) is not int or not 1 <= max_updates <= 256:
        raise ValueError("Invalid iteration budget")
    if type(oracle_budget) is not int or not 2 <= oracle_budget <= 2048:
        raise ValueError("Invalid oracle budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid financial audit interval")

    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    oracles = 1
    rows, audits = [], []
    delta = None
    decision = None
    old_target = None
    successful = 0
    fallback = 0
    truncated = 0
    stable_checks = 0
    first_stable = None
    start = perf_counter()

    for step in range(max_updates + 1):
        if step % audit_every == 0 or step == max_updates:
            R21.frozen_check(net, flows, obs)
            settlement = R21.settlement_check(net, flows)
            audits.append({"step": step, **settlement})
        if obs["reachable_unpriced"] or any(
                not math.isfinite(p) or p <= 0 for p in obs["prices"].values()):
            raise AssertionError("A reachable market lacks finite price")
        attribution = R22.attribute_gap(net, obs)
        stable = R24.numerical_pass(obs, delta)
        stable_checks = stable_checks + 1 if stable else 0
        if stable_checks >= 3 and first_stable is None:
            first_stable = step
        switches = (0 if old_target is None else sum(
            old_target[g] != obs["target"][g] for g in obs["target"]))
        rows.append({
            "step": step, "oracles_used": oracles,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "price_change": None if delta is None else
                delta["max_relative_price_change"],
            "load_change": None if delta is None else
                delta["max_relative_load_change"],
            "quantity_change": None if delta is None else
                delta["max_relative_quantity_change"],
            "best_route_switches": switches,
            "tie_groups": len(obs["tie_groups"]),
            "top_three_regret_share": attribution["top_three_share_of_total_regret"],
            "numerical_five_thresholds_met": stable,
            "consecutive_numerical_rechecks": stable_checks,
            "decision": decision,
        })
        if step == max_updates or oracles >= oracle_budget:
            break
        old_target = obs["target"]
        flows, obs, delta, decision = choose_guarded(
            net, flows, obs, settings, policy, step + 1,
            min(len(candidate_order(policy, step + 1)), oracle_budget - oracles))
        oracles += decision["candidate_count"]
        successful += int(decision["stopped_by_progress_gate"])
        fallback += int(decision["fallback_no_sufficient_progress"])
        truncated += int(decision["budget_truncated"])

    if audits[-1]["step"] != rows[-1]["step"]:
        R21.frozen_check(net, flows, obs)
        settlement = R21.settlement_check(net, flows)
        audits.append({"step": rows[-1]["step"], **settlement})
    if oracles > oracle_budget or oracles != (
        1 + sum(row["decision"]["candidate_count"] for row in rows[1:])
    ):
        raise AssertionError("Oracle evaluation budget/accounting violation")
    best_index = min(range(len(rows)), key=lambda i: (rows[i]["relative_gap"], i))
    last = rows[-1]
    return {
        "policy": policy,
        "nodes": 10 * cfg.m * cfg.m + 2,
        "producer_groups": len(net.groups),
        "updates_executed": len(rows) - 1,
        "max_updates_requested": max_updates,
        "oracles_used": oracles,
        "oracle_budget": oracle_budget,
        "evaluations_per_update": (oracles - 1) / max(1, len(rows) - 1),
        "progress_gated_steps": successful,
        "fallback_steps": fallback,
        "truncated_steps": truncated,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": last["relative_gap"],
        "final_max_group_gap": last["max_group_gap"],
        "best_gap": rows[best_index]["relative_gap"],
        "best_gap_step": best_index,
        "final_price_change": last["price_change"],
        "final_load_change": last["load_change"],
        "final_quantity_change": last["quantity_change"],
        "first_three_joint_stable_step": first_stable,
        "cumulative_global_gap_per_oracle": cumulative_work_gap(rows),
        "oracle_budget_frontiers": budget_snapshots(rows),
        "total_best_route_switches": sum(r["best_route_switches"] for r in rows),
        "final_flow_sha256": last["flow_sha256"],
        "rows": rows, "financial_audits": audits,
        "elapsed_ms_observational": round((perf_counter() - start) * 1000, 3),
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "authoritative_solver_approved": False,
    }


def cold_start_integrity(cfg):
    """Both policy orderings begin with the historical exact R21 decay alpha."""
    a = candidate_order("guard_decay_global", 1)
    b = candidate_order("guard_decay_joint", 1)
    if a != FIXED_ALPHAS or b != FIXED_ALPHAS:
        raise AssertionError("Cold-start decay alpha ordering mismatch")
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    trial_flows, trial_obs, _, audit = choose_guarded(
        net, flows, obs, settings, "guard_decay_global", 1, 1)
    baseline_flows, baseline_obs, baseline_alpha, calls, forced = R21.choose_step(
        net, flows, obs, settings, "decay_sqrt", 1)
    if (audit["selected_alpha"] != baseline_alpha or
        _flow_checksum(trial_flows) != _flow_checksum(baseline_flows) or
        trial_obs["relative_gap"] != baseline_obs["relative_gap"] or
        calls != 1):
        raise AssertionError("R21 frozen decay proposal not replayed")
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

    comparisons = []
    for name, updates, work in plans:
        cfg = R21.CASES[name]
        methods = []
        for policy in ("decay_sqrt", "backtracking",
                       "progressive_global", "warm_global",
                       "guard_decay_global", "guard_decay_joint",
                       "guard_fixed_global"):
            if policy in POLICIES:
                r = run_case(cfg, policy, updates, work)
            elif policy in R24.POLICIES:
                r = R24.run_case(cfg, policy, updates, work)
            elif policy in R26.POLICIES:
                r = R26.run_case(cfg, policy, updates, work)
            else:
                r = R27.run_case(cfg, policy, updates, work)
            print(
                "[{}:{}] gap={:.7g} worst={:.7g} "
                "priceChange={:.5g} loadChange={:.5g} "
                "evals={} updates={} stable={}".format(
                    name, policy, r["final_gap"], r["final_max_group_gap"],
                    r["final_price_change"], r["final_load_change"],
                    r["oracles_used"], r["updates_executed"],
                    r["first_three_joint_stable_step"]), flush=True)
            methods.append({"policy": policy, "result": r})
        comparisons.append({"case": name, "policies": methods})
    cold_start_integrity(R21.CASES["small"])
    for policy in POLICIES:
        small = run_case(R21.CASES["small"], policy, 4, 91, audit_every=2)
        large = run_case(R21.CASES["small"], policy, 7, 91, audit_every=2)
        if small["rows"] != large["rows"][:len(small["rows"])]:
            raise AssertionError("Guard trajectory prefix not deterministic")
    for reference in ("decay_sqrt", "backtracking"):
        R24.check_r21_reference(R21.CASES["small"], reference, 6)
    return {
        "comparisons": comparisons,
        "frozen_r21_reference_replay": True,
        "decay_first_candidate_identical_to_r21": True,
        "guarded_prefix_determinism": True,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R28_PROGRESS_PER_ORACLE_GUARD_V1",
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
            raise AssertionError("Upstream R20 integrity failed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTIC_CONTRACTS_ONLY"
    except Exception as e:
        report["error"] = repr(e)
        report["traceback"] = traceback.format_exc()
        print("[R28] FAILED:", repr(e), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = args.output / ("GA-Gate09-R28-" + args.profile + "-" + stamp + ".json")
    path.write_text(json.dumps(report, sort_keys=True, ensure_ascii=False,
                               indent=2) + "\n", encoding="utf-8")
    zpath = args.output / ("GA-Gate09-R28-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(path, path.name)
    print("[R28] STATUS:", report["status"], flush=True)
    print("[R28] EVIDENCE:", zpath, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTIC_CONTRACTS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
