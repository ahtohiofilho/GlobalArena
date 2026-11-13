#!/usr/bin/env python3
"""Gate 09 R22: explain residual F-072 regret and test joint numerical stability.

Research-only diagnostics. Replays R21's five policies through the immutable
R20/R21 oracles; never changes economic laws, financial ownership or game state.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

R21_DIR = Path(__file__).resolve().parents[2] / "r21"
sys.path.insert(0, str(R21_DIR / "ci"))
import run_r21 as R21
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

TARGET = 1e-4
POLICIES = ("fixed_1_8", "decay_sqrt", "backtracking")


def attribute_gap(net, observation, top=5):
    """Exact reconstruction of F-072's *weighted* regret.

    Do not mistake the worst group's relative gap for its contribution to
    total regret; use the supply*best-score denominator of the R20 oracle.
    """
    if not 1 <= top <= len(net.groups):
        raise ValueError("Invalid top-group budget")
    denominator = 0.0
    numerators = []
    by_good = defaultdict(float)
    by_civil = defaultdict(float)
    for gid, g in sorted(net.groups.items()):
        details = observation["groups"][gid]
        score, group_gap = details["score"], details["gap"]
        if not (math.isfinite(score) and score > 0
                and math.isfinite(group_gap) and group_gap >= 0):
            raise AssertionError("Invalid producer opportunity or regret")
        weight = g.supply * score
        regret = weight * group_gap
        denominator += weight
        by_good[g.commodity] += regret
        by_civil[g.seller] += regret
        numerators.append((gid, regret, group_gap, g))
    if not denominator or not math.isfinite(denominator):
        raise AssertionError("Invalid F-072 denominator")
    observed = sum(x[1] for x in numerators) / denominator
    if not math.isclose(observed, observation["relative_gap"],
                        rel_tol=1e-10, abs_tol=1e-12):
        raise AssertionError("Residual attribution does not reproduce F-072 gap")
    total_regret = sum(x[1] for x in numerators)
    leaders = sorted(numerators, key=lambda x: (-x[1], x[0]))
    return {
        "reconstructed_relative_gap": observed,
        "top_group_share_of_total_regret": (leaders[0][1] / total_regret if total_regret else 0.0),
        "top_three_share_of_total_regret": (
            sum(x[1] for x in leaders[:3]) / total_regret if total_regret else 0.0),
        "leading_groups": [{
            "group_id": gid, "origin": g.origin, "good": g.commodity,
            "civilization": g.seller, "group_relative_gap": gap,
            "contribution_to_total_gap": raw / denominator,
            "share_of_total_regret": raw / total_regret if total_regret else 0.0,
            "target_market": observation["groups"][gid]["best"].destination,
            "target_route_edges": list(observation["groups"][gid]["best"].edges),
        } for gid, raw, gap, g in leaders[:top]],
        "gap_contributions_by_good": {str(k): v / denominator for k, v in sorted(by_good.items())},
        "gap_contributions_by_civilization": {str(k): v / denominator for k, v in sorted(by_civil.items())},
    }


def edge_stress(net, observation, count=5):
    loads = observation["loads"]
    rows = []
    for eid, edge in sorted(net.edges.items()):
        ratio = loads.get(eid, 0.0) / edge.capacity
        if not math.isfinite(ratio) or ratio < 0:
            raise AssertionError("Invalid edge load/capacity")
        rows.append((eid, ratio, 1 + ratio ** 4))
    ranks = sorted(rows, key=lambda r: (-r[1], r[0]))
    return {
        "edge_count_above_or_at_natural_capacity": sum(r[1] >= 1 for r in rows),
        "maximum_load_capacity_ratio": ranks[0][1],
        "maximum_congestion_multiplier": ranks[0][2],
        "most_loaded_edges": [
            {"edge_id": eid, "load_over_capacity": ratio, "physical_cost_multiplier": multiplier}
            for eid, ratio, multiplier in ranks[:count]
        ],
    }


def run_case(cfg, policy, steps, audit_every=16):
    """Measure R21's unchanged policy, not a new solver."""
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Invalid diagnostic policy or budget")
    if type(audit_every) is not int or audit_every <= 0:
        raise ValueError("Invalid audit cadence")
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    current = evaluate_r14(net, flows, settings, ())
    rows = []
    audited_steps = []
    previous = None
    delta = None
    current_alpha = None
    stable_rechecks = 0
    first_numerical_stability = None
    total_policy_proposals = 0
    total_forced_uphill = 0
    prior_target = None
    start = perf_counter()

    for step in range(steps + 1):
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, current)
            money = R21.settlement_check(net, flows)
            audited_steps.append({"step": step, **money})
        regret = attribute_gap(net, current)
        stress = edge_stress(net, current)
        if current["reachable_unpriced"] or any(
                not math.isfinite(price) or price <= 0 for price in current["prices"].values()):
            raise AssertionError("A market is unpriced")

        if previous is None:
            price_delta = load_delta = quantity_delta = None
            switched = 0
        else:
            price_delta = delta["max_relative_price_change"]
            load_delta = delta["max_relative_load_change"]
            quantity_delta = delta["max_relative_quantity_change"]
            switched = sum(prior_target[g] != current["target"][g] for g in current["target"])

        numerical_ok = (step > 0 and
                        current["relative_gap"] <= TARGET and
                        current["max_group_gap"] <= TARGET and
                        price_delta <= TARGET and load_delta <= TARGET and
                        quantity_delta <= TARGET)
        stable_rechecks = stable_rechecks + 1 if numerical_ok else 0
        if stable_rechecks >= 3 and first_numerical_stability is None:
            first_numerical_stability = step

        rows.append({
            "step": step,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": current["relative_gap"],
            "max_group_gap": current["max_group_gap"],
            "price_change": price_delta,
            "load_change": load_delta,
            "quantity_change": quantity_delta,
            "alpha_used": current_alpha,
            "best_route_switches": switched,
            "tie_group_count": len(current["tie_groups"]),
            "numerical_five_thresholds_met": bool(numerical_ok),
            "consecutive_numerical_rechecks": stable_rechecks,
            "attribution": regret,
            "edge_stress": stress,
        })
        if step == steps:
            break

        old_flow, prior_target = flows, current["target"]
        new_flow, new_observation, alpha, proposals, forced = R21.choose_step(
            net, flows, current, settings, policy, step + 1)
        delta = _change_metrics(net, old_flow, new_flow)
        total_policy_proposals += proposals
        total_forced_uphill += int(forced)
        previous = current
        flows, current, current_alpha = new_flow, new_observation, alpha

    final = rows[-1]
    best_index = min(range(len(rows)), key=lambda i: (rows[i]["relative_gap"], i))
    worst_gap_groups = final["attribution"]["leading_groups"]
    return {
        "policy": policy,
        "steps": steps,
        "nodes": 10 * cfg.m * cfg.m + 2,
        "producer_groups": len(net.groups),
        "markets": len(net.markets),
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": final["relative_gap"],
        "final_max_group_gap": final["max_group_gap"],
        "best_observed_gap": rows[best_index]["relative_gap"],
        "best_observed_step": best_index,
        "final_top_three_regret_share": final["attribution"]["top_three_share_of_total_regret"],
        "final_dominant_group_ids": [x["group_id"] for x in worst_gap_groups[:3]],
        "final_max_price_change": final["price_change"],
        "final_max_load_change": final["load_change"],
        "final_max_quantity_change": final["quantity_change"],
        "final_max_load_capacity_ratio": final["edge_stress"]["maximum_load_capacity_ratio"],
        "total_best_route_switches": sum(x["best_route_switches"] for x in rows),
        "observed_numerical_stability_checkpoint": first_numerical_stability,
        "final_consecutive_numerical_rechecks": stable_rechecks,
        "policy_proposals_evaluated": total_policy_proposals,
        "forced_uphill_steps": total_forced_uphill,
        "audited_steps": audited_steps,
        "rows": rows,
        "final_flow_sha256": final["flow_sha256"],
        "diagnostic_elapsed_ms_observed": round((perf_counter() - start) * 1000, 3),
        "economic_equilibrium_certified": False,
        "financial_F059_ratified": False,
        "authoritative_solver_approved": False,
    }


def replay_r21(cfg, policy, steps):
    """R21 policy and R22 diagnostic wrapper must produce same full trajectory."""
    prior = R21.run_trajectory(cfg, policy, steps, audit_every=max(1, steps))
    current = run_case(cfg, policy, steps, audit_every=max(1, steps))
    if prior["final_flow_sha256"] != current["final_flow_sha256"]:
        raise AssertionError("R21/R22 final flow mismatch")
    for expected, actual in zip(prior["rows"], current["rows"]):
        for key in ("flow_sha256", "relative_gap", "max_group_gap"):
            if expected[key] != actual[key]:
                raise AssertionError("R21/R22 trajectory mismatch at " + str(actual["step"]))
        if expected["max_relative_price_change"] != actual["price_change"]:
            raise AssertionError("R21/R22 price trajectory mismatch")
        if expected["max_relative_load_change"] != actual["load_change"]:
            raise AssertionError("R21/R22 load trajectory mismatch")
    if len(prior["rows"]) != len(current["rows"]):
        raise AssertionError("Mismatched step budget")
    return True


def exercise(profile):
    if profile == "smoke":
        cases = (("small", 8),)
    elif profile == "ci":
        cases = (("small", 96), ("medium", 48))
    elif profile == "extended":
        cases = (("small", 192), ("medium", 96), ("large", 24))
    else:
        raise ValueError("Unknown profile")
    result = []
    for case_name, steps in cases:
        series = []
        for policy in POLICIES:
            report = run_case(R21.CASES[case_name], policy, steps)
            series.append(report)
            print("[{}:{}] gap {:.6g} -> {:.6g} worst_group {:.6g}, "
                  "top3 {:.1%}, switches {}, priceDelta {:.3g}, loadDelta {:.3g}, "
                  "stable={}".format(case_name, policy, report["initial_gap"],
                   report["final_gap"], report["final_max_group_gap"],
                   report["final_top_three_regret_share"],
                   report["total_best_route_switches"], report["final_max_price_change"],
                   report["final_max_load_change"],
                   report["observed_numerical_stability_checkpoint"]), flush=True)
        result.append({"case": case_name, "policies": series})
    for policy in POLICIES:
        replay_r21(R21.CASES["small"], policy, 6)
    return {"comparisons": result, "r21_trajectory_replay": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"), default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R22_RESIDUAL_ATTRIBUTION_AND_JOINT_STABILITY_V1",
        "started_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "economic_equilibrium_certified": False,
        "financial_F059_ratified": False,
        "authoritative_solver_approved": False,
    }
    try:
        report["upstream_r20_manifest_files"] = R21.validate_manifest()
        if report["upstream_r20_manifest_files"] != 16:
            raise AssertionError("Upstream package changed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTIC_CONTRACTS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R22] FAILED:", repr(exc), flush=True)
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    jsonpath = args.output / ("GA-Gate09-R22-" + args.profile + "-" + timestamp + ".json")
    jsonpath.write_text(json.dumps(report, indent=2, sort_keys=True,
                                   ensure_ascii=False) + "\n", encoding="utf-8")
    zippath = args.output / ("GA-Gate09-R22-Evidencias-" + timestamp + ".zip")
    with zipfile.ZipFile(zippath, "w", zipfile.ZIP_DEFLATED) as handle:
        handle.write(jsonpath, jsonpath.name)
    print("[R22] STATUS:", report["status"], flush=True)
    print("[R22] EVIDENCE:", zippath, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTIC_CONTRACTS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
