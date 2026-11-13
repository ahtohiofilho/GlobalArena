#!/usr/bin/env python3
"""Gate09 R23: controlled, supply-conserving group-priority clearing experiments.

R20's unchanged F-072/F-084F economic rules define all frozen snapshots.
R21 supplies the reference updates; R22 supplies residual attribution.
No new demand, price curve, tariff rule, buyer wallets or authoritative writes.
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

R22_DIR = Path(__file__).resolve().parents[2] / "r22"
sys.path.insert(0, str(R22_DIR / "ci"))
import run_r22 as R22

R21 = R22.R21
from clearing_r5 import ClearingSettings, _mix, _change_metrics, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

TARGET = 1e-4
POLICIES = (
    "fixed_1_8", "decay_sqrt", "targeted_quartile",
    "inverse_quartile", "uniform_matched",
)
HIGH = 1.0 / 8.0
LOW = 1.0 / 32.0


def priority_set(net, observation):
    """Rank producers by *absolute contribution*, not relative group regret."""
    groups = sorted(net.groups)
    if not groups:
        raise AssertionError("No producers")
    ranked = sorted(
        groups,
        key=lambda gid: (
            -net.groups[gid].supply
            * observation["groups"][gid]["score"]
            * observation["groups"][gid]["gap"],
            gid,
        ),
    )
    return tuple(ranked[:max(1, (len(groups) + 3) // 4)])


def targeted_alpha(net, observation, reverse=False):
    leaders = set(priority_set(net, observation))
    if reverse:
        return {gid: LOW if gid in leaders else HIGH for gid in sorted(net.groups)}
    return {gid: HIGH if gid in leaders else LOW for gid in sorted(net.groups)}


def supply_matched_alpha(net, observation):
    rates = targeted_alpha(net, observation)
    production = sum(g.supply for g in net.groups.values())
    if production <= 0:
        raise AssertionError("Nonpositive production")
    return sum(g.supply * rates[gid] for gid, g in net.groups.items()) / production


def mix_by_group(net, flows, targets, rates):
    """R20 damped update with per-group alpha; exact supply correction.

    Every producer moves existing flow toward its *existing R20 F-072 target*.
    No new preference, capacity, demand or price term is introduced.
    """
    if set(rates) != set(net.groups) or set(targets) != set(net.groups):
        raise ValueError("Missing or extraneous producer update")
    if any(not math.isfinite(a) or not 0 < a <= 1 for a in rates.values()):
        raise ValueError("Invalid per-group alpha")
    out = {column: amount * (1.0 - rates[column.group_id])
           for column, amount in flows.items()}
    for gid, group in net.groups.items():
        column = targets[gid]
        if column.group_id != gid:
            raise ValueError("Mismatched target producer")
        out[column] = out.get(column, 0.0) + rates[gid] * group.supply
    for gid, group in sorted(net.groups.items()):
        columns = sorted(c for c in out if c.group_id == gid)
        delta = group.supply - sum(out[c] for c in columns)
        out[targets[gid]] += delta
        if any(out[c] < 0 for c in columns):
            raise AssertionError("Negative flow after group update")
    net.snapshots(out)
    return out


def propose(net, flows, obs, cfg, policy, step):
    """One post-update R14 oracle evaluation, except independent probes."""
    if policy == "fixed_1_8":
        alpha = HIGH
        new = _mix(flows, net, obs["target"], alpha)
    elif policy == "decay_sqrt":
        alpha = (1.0 / 4.0) / math.sqrt(1.0 + (step - 1) / 8.0)
        new = _mix(flows, net, obs["target"], alpha)
    elif policy == "targeted_quartile":
        rates = targeted_alpha(net, obs)
        alpha = supply_matched_alpha(net, obs)
        new = mix_by_group(net, flows, obs["target"], rates)
    elif policy == "inverse_quartile":
        rates = targeted_alpha(net, obs, reverse=True)
        alpha = sum(net.groups[g].supply * a for g, a in rates.items()) / sum(
            g.supply for g in net.groups.values())
        new = mix_by_group(net, flows, obs["target"], rates)
    elif policy == "uniform_matched":
        alpha = supply_matched_alpha(net, obs)
        new = _mix(flows, net, obs["target"], alpha)
    else:
        raise ValueError("Unknown policy")
    next_obs = evaluate_r14(net, new, cfg, ())
    return new, next_obs, alpha


def same_state_probe(net, flows, obs, cfg, step):
    """Compare one-step counterfactual outcomes at an identical frozen state.

    These trial evaluations are *not* accepted as trajectory updates. They
    must not be confused with independent long-term convergence evidence.
    """
    choices = {}
    for policy in ("fixed_1_8", "targeted_quartile",
                   "inverse_quartile", "uniform_matched"):
        trial_flow, trial_obs, alpha = propose(
            net, flows, obs, cfg, policy, step + 1)
        moves = _change_metrics(net, flows, trial_flow)
        choices[policy] = {
            "next_relative_gap": trial_obs["relative_gap"],
            "next_max_group_gap": trial_obs["max_group_gap"],
            "next_price_change": moves["max_relative_price_change"],
            "next_load_change": moves["max_relative_load_change"],
            "supply_weighted_alpha": alpha,
        }
    return {
        "step": step,
        "same_frozen_state": True,
        "starting_flow_sha256": _flow_checksum(flows),
        "priority_group_ids": list(priority_set(net, obs)),
        "outcomes": choices,
    }


def run_case(case, policy, steps, audit_every=16, probe_every=16):
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Invalid policy or iteration budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit cadence")
    if type(probe_every) is not int or probe_every < 1:
        raise ValueError("Invalid probe cadence")
    net, flows = construct_scenario(case)
    settings = ClearingSettings(exponent=case.exponent)
    current = evaluate_r14(net, flows, settings, ())
    rows = []
    audits = []
    probes = []
    previous_delta = None
    previous_target = None
    previous_alpha = None
    stable = 0
    first_stable = None
    start = perf_counter()

    for step in range(steps + 1):
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, current)
            money = R21.settlement_check(net, flows)
            audits.append({"step": step, **money})

        attribution = R22.attribute_gap(net, current)
        stress = R22.edge_stress(net, current)
        if current["reachable_unpriced"] or any(
            not math.isfinite(v) or v <= 0 for v in current["prices"].values()
        ):
            raise AssertionError("Unpriced reachable market")

        price_delta = None if previous_delta is None else previous_delta["max_relative_price_change"]
        load_delta = None if previous_delta is None else previous_delta["max_relative_load_change"]
        quantity_delta = None if previous_delta is None else previous_delta["max_relative_quantity_change"]
        numerical_now = bool(
            step > 0
            and current["relative_gap"] <= TARGET
            and current["max_group_gap"] <= TARGET
            and price_delta <= TARGET and load_delta <= TARGET
            and quantity_delta <= TARGET
        )
        stable = stable + 1 if numerical_now else 0
        if stable >= 3 and first_stable is None:
            first_stable = step

        switches = 0 if previous_target is None else sum(
            previous_target[gid] != current["target"][gid]
            for gid in current["target"]
        )
        rows.append({
            "step": step,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": current["relative_gap"],
            "max_group_gap": current["max_group_gap"],
            "price_change": price_delta,
            "load_change": load_delta,
            "quantity_change": quantity_delta,
            "best_route_switches": switches,
            "tie_group_count": len(current["tie_groups"]),
            "supply_weighted_alpha_used": previous_alpha,
            "numerical_five_thresholds_met": numerical_now,
            "consecutive_numerical_rechecks": stable,
            "top_three_regret_share": attribution["top_three_share_of_total_regret"],
            "top_group_ids": [g["group_id"] for g in attribution["leading_groups"][:3]],
            "max_load_capacity_ratio": stress["maximum_load_capacity_ratio"],
        })
        if policy == "fixed_1_8" and step < steps and step % probe_every == 0:
            probes.append(same_state_probe(net, flows, current, settings, step))
        if step == steps:
            break
        previous_target = current["target"]
        updated, updated_obs, alpha = propose(
            net, flows, current, settings, policy, step + 1)
        previous_delta = _change_metrics(net, flows, updated)
        previous_alpha = alpha
        flows, current = updated, updated_obs

    best_idx = min(range(len(rows)), key=lambda i: (rows[i]["relative_gap"], i))
    last = rows[-1]
    return {
        "policy": policy,
        "nodes": 10 * case.m * case.m + 2,
        "producer_groups": len(net.groups),
        "steps": steps,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": last["relative_gap"],
        "final_max_group_gap": last["max_group_gap"],
        "best_gap_observed": rows[best_idx]["relative_gap"],
        "best_gap_step": best_idx,
        "final_price_change": last["price_change"],
        "final_load_change": last["load_change"],
        "final_quantity_change": last["quantity_change"],
        "final_top_three_regret_share": last["top_three_regret_share"],
        "total_best_route_switches": sum(row["best_route_switches"] for row in rows),
        "first_three_joint_stable_step": first_stable,
        "final_flow_sha256": last["flow_sha256"],
        "main_oracle_evaluations": steps + 1,
        "counterfactual_oracle_evaluations": len(probes) * 4,
        "elapsed_ms_observational": round((perf_counter() - start) * 1000, 3),
        "rows": rows,
        "financial_audits": audits,
        "same_state_probes": probes,
        "numerical_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def verify_reference(case, policy, steps):
    if policy not in ("fixed_1_8", "decay_sqrt"):
        raise ValueError("No R21 reference for this policy")
    baseline = R21.run_trajectory(case, policy, steps, audit_every=max(steps, 1))
    ours = run_case(case, policy, steps, audit_every=max(steps, 1),
                    probe_every=max(steps, 1))
    for left, right in zip(baseline["rows"], ours["rows"]):
        for key in ("flow_sha256", "relative_gap", "max_group_gap"):
            if left[key] != right[key]:
                raise AssertionError("Unmodified R21 trajectory drift at step "
                                     + str(right["step"]))
    if len(baseline["rows"]) != len(ours["rows"]):
        raise AssertionError("Reference length mismatch")
    return True


def verify_uniform_mix(case):
    net, flows = construct_scenario(case)
    cfg = ClearingSettings(exponent=case.exponent)
    obs = evaluate_r14(net, flows, cfg, ())
    expected = _mix(flows, net, obs["target"], HIGH)
    actual = mix_by_group(
        net, flows, obs["target"], {gid: HIGH for gid in net.groups}
    )
    if expected != actual or _flow_checksum(expected) != _flow_checksum(actual):
        raise AssertionError("Uniform per-group update differs from R20")
    return True


def exercise(profile):
    if profile == "smoke":
        plans = (("small", 8),)
    elif profile == "ci":
        plans = (("small", 96), ("medium", 48))
    elif profile == "extended":
        plans = (("small", 192), ("medium", 96), ("large", 24))
    else:
        raise ValueError("Unknown diagnostic profile")
    results = []
    for case_name, steps in plans:
        policies = []
        for policy in POLICIES:
            result = run_case(R21.CASES[case_name], policy, steps)
            policies.append(result)
            print("[{}:{}] gap {:.7g} / max {:.7g}, priceChange {:.5g}, loadChange {:.5g}, "
                  "stable={}, top3={:.1%}, proposals={}+{}".format(
                  case_name, policy, result["final_gap"],
                  result["final_max_group_gap"],
                  result["final_price_change"], result["final_load_change"],
                  result["first_three_joint_stable_step"],
                  result["final_top_three_regret_share"],
                  result["main_oracle_evaluations"],
                  result["counterfactual_oracle_evaluations"]), flush=True)
        reference = next(p for p in policies if p["policy"] == "fixed_1_8")
        comparison = []
        for p in policies:
            comparison.append({
                "policy": p["policy"],
                "global_gap_ratio_to_fixed": p["final_gap"] / reference["final_gap"],
                "max_group_gap_ratio_to_fixed": p["final_max_group_gap"] /
                    reference["final_max_group_gap"],
                "price_change_ratio_to_fixed": p["final_price_change"] /
                    reference["final_price_change"],
                "load_change_ratio_to_fixed": p["final_load_change"] /
                    reference["final_load_change"],
                "numerical_stability_observed": p["first_three_joint_stable_step"] is not None,
            })
        results.append({
            "case": case_name,
            "policies": policies,
            "comparisons_to_fixed": comparison,
        })
    verify_uniform_mix(R21.CASES["small"])
    verify_reference(R21.CASES["small"], "fixed_1_8", 6)
    verify_reference(R21.CASES["small"], "decay_sqrt", 6)
    return {
        "cases": results,
        "unmodified_r20_uniform_mix_verified": True,
        "unmodified_r21_reference_replays": True,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R23_TARGETED_RELAXATION_CONTROLS_V1",
        "platform": platform.platform(),
        "python": platform.python_version(),
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "profile": args.profile,
        "status": "FAILED",
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "authoritative_solver_approved": False,
    }
    try:
        report["frozen_r20_manifest_files"] = R21.validate_manifest()
        if report["frozen_r20_manifest_files"] != 16:
            raise AssertionError("Frozen upstream R20 changed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_RESEARCH_CONTROLS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R23] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    file_path = args.output / ("GA-Gate09-R23-" + args.profile + "-" + stamp + ".json")
    file_path.write_text(json.dumps(report, ensure_ascii=False, sort_keys=True,
                                    indent=2) + "\n", encoding="utf-8")
    archive_path = args.output / ("GA-Gate09-R23-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(file_path, file_path.name)
    print("[R23] STATUS:", report["status"], flush=True)
    print("[R23] EVIDENCE:", archive_path, flush=True)
    return 0 if report["status"] == "PASSED_RESEARCH_CONTROLS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
