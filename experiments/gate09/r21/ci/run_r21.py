#!/usr/bin/env python3
"""GA Gate 09 R21: diagnostic trajectories, not a production clearing solver.

R20's immutable R13/R14 engine remains the source of truth for each frozen
snapshot. This runner extends only the iteration horizon, explores alternate
relaxation schedules and audits a subset of snapshots against the independent
original evaluator and fixed-point financial settlement.
"""
from __future__ import annotations

import argparse
from dataclasses import asdict, replace
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

R20 = Path(__file__).resolve().parents[2] / "r20"
sys.path.insert(0, str(R20 / "baseline"))
sys.path.insert(0, str(R20 / "ci"))

from multimarket_r13 import R13Scenario, construct_scenario
from clearing_r5 import ClearingSettings, _evaluate, _mix, _change_metrics, _flow_checksum
from oracle_cache_r14 import evaluate_r14
from finance_fixed import frozen_float_flows_to_raw, settle
from gate09 import MICRO
from run_r20 import validate_manifest

POLICIES = ("fixed_1_16", "fixed_1_8", "fixed_1_4", "decay_sqrt", "backtracking")
CASES = {
    "small": R13Scenario(m=2, products=2, markets_per_product=3,
                         groups_per_product=6, civilizations=3, iterations=2),
    "medium": R13Scenario(m=4, products=3, markets_per_product=4,
                          groups_per_product=8, civilizations=4, iterations=2),
    "large": R13Scenario(m=8, products=4, markets_per_product=5,
                         groups_per_product=10, civilizations=6, iterations=2),
}
TARGET = 1e-4


def frozen_check(net, flows, observed):
    """Check R14 numerical oracle against original R13 on the SAME flow."""
    original = _evaluate(net, flows, ClearingSettings(exponent=4), ())
    for name in ("relative_gap", "max_group_gap"):
        if not math.isclose(original[name], observed[name], rel_tol=0, abs_tol=1e-12):
            raise AssertionError("R13/R14 divergence: " + name)
    for name in ("target", "tie_groups", "active_columns"):
        if original[name] != observed[name]:
            raise AssertionError("R13/R14 divergence: " + name)
    if original["prices"] != observed["prices"] or original["loads"] != observed["loads"]:
        raise AssertionError("R13/R14 frozen state divergence")


def settlement_check(net, flows):
    """Experimental F-084F projection and explicit conservation witness."""
    markets, sales, contracts = frozen_float_flows_to_raw(net, flows)
    result = settle(markets, sales, contracts)
    expected = sum(round(g.supply * MICRO) for g in net.groups.values())
    sold = sum(s.quantity_raw for s in sales)
    if sold != expected:
        raise AssertionError("Lost or duplicated physical production")
    if result["total_gross_raw"] != result["total_credits_raw"]:
        raise AssertionError("Financial conservation violation")
    if any(s["gross_raw"] < 0 for s in result["sales"]):
        raise AssertionError("Negative gross receipt")
    return {
        "sold_micro": sold,
        "gross_micro": result["total_gross_raw"],
        "credits_micro": result["total_credits_raw"],
        "sale_count": len(sales),
    }


def choose_step(net, flows, observed, settings, policy, step):
    """Return proposal and chosen alpha; never modify the R20 frozen modules."""
    if policy == "fixed_1_16":
        alphas = (1 / 16,)
    elif policy == "fixed_1_8":
        alphas = (1 / 8,)
    elif policy == "fixed_1_4":
        alphas = (1 / 4,)
    elif policy == "decay_sqrt":
        alphas = ((1 / 4) / math.sqrt(1 + (step - 1) / 8),)
    elif policy == "backtracking":
        alphas = (1 / 4, 1 / 8, 1 / 16, 1 / 32)
    else:
        raise ValueError("Unknown diagnostic policy")

    # Fixed / decreasing schedule: one proposal and one R14 evaluation.
    if policy != "backtracking":
        alpha = alphas[0]
        candidate = _mix(flows, net, observed["target"], alpha)
        return candidate, evaluate_r14(net, candidate, settings, ()), alpha, 1, False

    # Deliberately expensive counterfactual: evaluate candidates at their NEW
    # prices and loads. If no candidate decreases gap, keep the best trial
    # rather than freezing a non-equilibrium state.
    trials = []
    for alpha in alphas:
        candidate = _mix(flows, net, observed["target"], alpha)
        evaluation = evaluate_r14(net, candidate, settings, ())
        trials.append((evaluation["relative_gap"], alpha, candidate, evaluation))
        if evaluation["relative_gap"] <= observed["relative_gap"] + 1e-12:
            return candidate, evaluation, alpha, len(trials), False
    gap, alpha, candidate, evaluation = min(trials, key=lambda x: (x[0], -x[1]))
    return candidate, evaluation, alpha, len(trials), True


def run_trajectory(cfg, policy, steps, audit_every=16):
    if policy not in POLICIES or not isinstance(steps, int) or not 1 <= steps <= 256:
        raise ValueError("Unsupported policy or diagnostic budget")
    if not isinstance(audit_every, int) or audit_every < 1:
        raise ValueError("Invalid audit cadence")
    start = perf_counter()
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    current = evaluate_r14(net, flows, settings, ())
    rows = []
    audits = []
    trial_evaluations = 0
    target_churn = 0
    uphill_forced = 0
    sustained = 0
    first_stable_step = None
    prior_delta = None
    prior_alpha = None

    for step in range(steps + 1):
        if step % audit_every == 0 or step == steps:
            frozen_check(net, flows, current)
            audit = settlement_check(net, flows)
            audits.append({"step": step, **audit})
        else:
            audit = None

        if not all(math.isfinite(v) and v >= 0 for v in
                   (current["relative_gap"], current["max_group_gap"])):
            raise AssertionError("Invalid commercial gap")
        if current["reachable_unpriced"] or any(
                not math.isfinite(v) or v <= 0 for v in current["prices"].values()):
            raise AssertionError("Unpriced reachable market")

        gap_ok = (current["relative_gap"] <= TARGET and
                  current["max_group_gap"] <= TARGET)
        stable_here = bool(prior_delta is not None and gap_ok and
                           prior_delta["max_relative_price_change"] <= TARGET and
                           prior_delta["max_relative_load_change"] <= TARGET)
        sustained = sustained + 1 if stable_here else 0
        if sustained >= 3 and first_stable_step is None:
            first_stable_step = step
        rows.append({
            "step": step,
            "alpha_used": prior_alpha,
            "relative_gap": current["relative_gap"],
            "max_group_gap": current["max_group_gap"],
            "tie_groups": len(current["tie_groups"]),
            "active_columns": current["active_columns"],
            "flow_sha256": _flow_checksum(flows),
            "max_relative_price_change": None if prior_delta is None else
                prior_delta["max_relative_price_change"],
            "max_relative_load_change": None if prior_delta is None else
                prior_delta["max_relative_load_change"],
        })
        if step == steps:
            break

        old_target = current["target"]
        next_flows, next_eval, alpha, n_evaluations, forced = choose_step(
            net, flows, current, settings, policy, step + 1)
        prior_delta = _change_metrics(net, flows, next_flows)
        target_churn += sum(old_target[gid] != next_eval["target"][gid]
                            for gid in old_target)
        trial_evaluations += n_evaluations
        uphill_forced += int(forced)
        flows, current = next_flows, next_eval
        prior_alpha = alpha

    gaps = [r["relative_gap"] for r in rows]
    max_gaps = [r["max_group_gap"] for r in rows]
    best_step = min(range(len(gaps)), key=lambda i: (gaps[i], i))
    return {
        "policy": policy,
        "scenario": asdict(cfg),
        "diagnostic_steps": steps,
        "rows": rows,
        "audits": audits,
        "initial_gap": gaps[0],
        "final_gap": gaps[-1],
        "best_gap": gaps[best_step],
        "best_step": best_step,
        "final_max_group_gap": max_gaps[-1],
        "uphill_gap_steps": sum(b > a + 1e-12 for a, b in zip(gaps, gaps[1:])),
        "target_churn_total": target_churn,
        "backtracking_forced_uphill_steps": uphill_forced,
        "oracle_evaluations_for_proposals": trial_evaluations,
        "first_three_stable_observed_step": first_stable_step,
        "final_flow_sha256": rows[-1]["flow_sha256"],
        "elapsed_ms_observational": round((perf_counter() - start) * 1000, 3),
        "equilibrium_convergence_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def prefix_check(cfg, policy):
    shorter = run_trajectory(cfg, policy, 6, audit_every=4)
    longer = run_trajectory(cfg, policy, 9, audit_every=4)
    assert shorter["rows"] == longer["rows"][:7], "Trajectory prefix drift"
    return True


def exercise(profile):
    if profile == "smoke":
        plans = (("small", 12),)
    elif profile == "ci":
        plans = (("small", 96), ("medium", 48))
    elif profile == "extended":
        plans = (("small", 192), ("medium", 96), ("large", 24))
    else:
        raise ValueError("Unknown profile")
    results = []
    for case, steps in plans:
        paths = []
        for policy in POLICIES:
            result = run_trajectory(CASES[case], policy, steps)
            paths.append(result)
            print(f"[{case}:{policy}] gap {result['initial_gap']:.6f} -> "
                  f"{result['final_gap']:.6f}, best {result['best_gap']:.6f} "
                  f"at {result['best_step']}, audited {len(result['audits'])}",
                  flush=True)
        results.append({"case": case, "paths": paths})
    assert prefix_check(CASES["small"], "fixed_1_8")
    assert prefix_check(CASES["small"], "backtracking")
    return {"scenarios": results, "prefix_checks_passed": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R21_CONVERGENCE_DIAGNOSTICS_V1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "python": platform.python_version(),
        "platform": platform.platform(),
        "profile": args.profile,
        "status": "FAILED",
        "scope": "Python research with independent R20 oracle and financial audits",
        "productionApproved": False,
        "f059FinancialTiePolicyRatified": False,
        "globalEquilibriumProven": False,
        "csharpSolverBenchmarked": False,
    }
    try:
        report["r20ManifestFilesVerified"] = validate_manifest()
        if report["r20ManifestFilesVerified"] != 16:
            raise AssertionError("Unexpected frozen upstream manifest")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTICS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R21] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    json_path = args.output / f"GA-Gate09-R21-{args.profile}-{stamp}.json"
    json_path.write_text(json.dumps(report, ensure_ascii=False,
                                    indent=2, sort_keys=True) + "\n", encoding="utf-8")
    zip_path = args.output / f"GA-Gate09-R21-Evidencias-{stamp}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(json_path, json_path.name)
    print("[R21] STATUS:", report["status"], flush=True)
    print("[R21] EVIDENCE:", zip_path, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTICS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
