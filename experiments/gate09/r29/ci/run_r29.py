#!/usr/bin/env python3
"""Global Arena Gate09 R29: frozen-state structural instability attribution.

Observational only: compares immutable F-072 best-response targets, route
switching, price/load shocks, congestion and A-B-A target reversals across
fully repriced R21/R26 trajectories. No solver policy is modified. Target
cycles are not financial cycles or a proof of nonconvergence.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
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
sys.path.insert(0, str(GATE09 / "r28" / "ci"))
import run_r28 as R28

R21 = R28.R21
R22 = R28.R22
R24 = R28.R24
R26 = R28.R26
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum, _mix
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

POLICIES = ("decay_sqrt", "backtracking", "fixed_1_8", "progressive_global")
PROBE_ALPHAS = (1 / 8, 1 / 16)
EPS = 1e-12


def market_route_parts(before, after):
    """Partition switches; never silently label same-route metadata a route switch."""
    market = before.destination != after.destination
    route = tuple(before.edges) != tuple(after.edges)
    if market and route:
        return "both"
    if market:
        return "market_only"
    if route:
        return "route_only"
    return "metadata_only" if before != after else "unchanged"


def target_signature(targets):
    """SHA256 of the discrete best-response vector (not the flow vector)."""
    entries = tuple((gid, target.destination, tuple(target.edges))
                    for gid, target in sorted(targets.items()))
    return hashlib.sha256(repr(entries).encode("utf-8")).hexdigest()


def aba_reversals(older, previous, current):
    """Groups with target A -> B -> A, without asserting a real price cycle."""
    if older is None:
        return ()
    if set(older) != set(previous) or set(older) != set(current):
        raise AssertionError("Producer set drift")
    return tuple(gid for gid in sorted(current)
                 if older[gid] == current[gid] != previous[gid])


def pearson(xs, ys):
    """Descriptive association of trajectories, not a causal estimate."""
    if len(xs) != len(ys) or len(xs) < 3:
        return None
    mx = statistics.fmean(xs)
    my = statistics.fmean(ys)
    cov = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    sx = math.sqrt(sum((x - mx) ** 2 for x in xs))
    sy = math.sqrt(sum((y - my) ** 2 for y in ys))
    if sx <= 1e-15 or sy <= 1e-15:
        return None
    return max(-1.0, min(1.0, cov / (sx * sy)))


def top_price_delta(before, after):
    """Report the market with largest finite repricing shock."""
    if set(before["prices"]) != set(after["prices"]):
        raise AssertionError("Market quote keys changed across frozen snapshots")
    points = []
    for market in sorted(before["prices"]):
        p = before["prices"][market]
        q = after["prices"][market]
        if not all(math.isfinite(x) and x > 0 for x in (p, q)):
            raise AssertionError("Invalid price")
        points.append((abs(q - p) / max(abs(p), EPS), repr(market)))
    val, market = max(points, key=lambda row: (row[0], row[1]))
    return {"relative_change": val, "market_key": market}


def transition(net, before, after, older=None):
    """Mechanistic co-occurrence, never proof that congestion caused switching."""
    left = before["target"]
    right = after["target"]
    if set(left) != set(right) or set(left) != set(net.groups):
        raise AssertionError("Different producer domain")
    parts = {"market_only": 0, "route_only": 0, "both": 0,
             "metadata_only": 0, "unchanged": 0}
    changed = []
    for gid in sorted(left):
        kind = market_route_parts(left[gid], right[gid])
        parts[kind] += 1
        if kind != "unchanged":
            changed.append(gid)
    stress = R22.edge_stress(net, before, count=5)
    top_edges = {e["edge_id"] for e in stress["most_loaded_edges"]}
    stress_exposed = [
        gid for gid in changed
        if top_edges.intersection(set(left[gid].edges) |
                                  set(right[gid].edges))
    ]
    leaders = {
        g["group_id"] for g in R22.attribute_gap(
            net, after, top=min(5, len(net.groups)))["leading_groups"]
    }
    flipping = aba_reversals(
        None if older is None else older["target"], left, right)
    before_stress = R22.edge_stress(net, before)
    after_stress = R22.edge_stress(net, after)
    big_price = top_price_delta(before, after)
    out = {
        "switches": len(changed),
        "switch_fraction": len(changed) / len(left),
        "switch_partition": parts,
        "leader_switches": len(leaders.intersection(changed)),
        "exposed_to_top5_preexisting_stress_edges": len(stress_exposed),
        "exposure_fraction_of_switches": (
            len(stress_exposed) / len(changed) if changed else 0.0),
        "two_step_aba_reversals": len(flipping),
        "two_step_aba_group_ids": list(flipping),
        "two_step_aba_fraction": len(flipping) / len(left),
        "overloaded_edge_count_before": before_stress[
            "edge_count_above_or_at_natural_capacity"],
        "overloaded_edge_count_after": after_stress[
            "edge_count_above_or_at_natural_capacity"],
        "maximum_load_capacity_ratio_before": before_stress[
            "maximum_load_capacity_ratio"],
        "maximum_load_capacity_ratio_after": after_stress[
            "maximum_load_capacity_ratio"],
        "largest_price_shock": big_price,
        "old_tie_groups": len(before["tie_groups"]),
        "new_tie_groups": len(after["tie_groups"]),
        "target_hash_before": target_signature(left),
        "target_hash_after": target_signature(right),
    }
    if sum(parts.values()) != len(left):
        raise AssertionError("Incomplete best-target change partition")
    return out


def local_probe(net, flows, observed, settings):
    """Costed counterfactual: what switches if the old state moves a little?

    Uses two independent full R14 repricings, NEVER commits either proposal.
    These cannot be conflated with R21/R26 trajectory evaluations.
    """
    original_hash = _flow_checksum(flows)
    choices = []
    for alpha in PROBE_ALPHAS:
        new_flows = _mix(flows, net, observed["target"], alpha)
        priced = evaluate_r14(net, new_flows, settings, ())
        delta = _change_metrics(net, flows, new_flows)
        changed = transition(net, observed, priced)
        choices.append({
            "alpha": alpha,
            "next_global_gap": priced["relative_gap"],
            "next_max_group_gap": priced["max_group_gap"],
            "switch_fraction": changed["switch_fraction"],
            "market_switches": (changed["switch_partition"]["market_only"] +
                                changed["switch_partition"]["both"]),
            "route_only_switches": changed["switch_partition"]["route_only"],
            "max_relative_price_change": delta["max_relative_price_change"],
            "max_relative_load_change": delta["max_relative_load_change"],
            "max_relative_quantity_change": delta["max_relative_quantity_change"],
            "top5_stress_edge_exposure": changed[
                "exposed_to_top5_preexisting_stress_edges"],
        })
    if _flow_checksum(flows) != original_hash:
        raise AssertionError("Diagnostic counterfactual mutated source state")
    return {"source_flow_sha256": original_hash,
            "candidate_oracle_evaluations": len(choices), "proposals": choices}


def step_forward(net, flows, observation, settings, policy, step):
    if policy == "progressive_global":
        candidate, scored, delta, choice = R26.choose_progressive(
            net, flows, observation, settings, policy, len(R26.ALPHAS))
        return candidate, scored, delta, choice["candidate_count"], \
            choice["selected_alpha"]
    candidate, scored, alpha, proposals, forced = R21.choose_step(
        net, flows, observation, settings, policy, step)
    delta = _change_metrics(net, flows, candidate)
    return candidate, scored, delta, proposals, alpha


def diagnostics(rows):
    """Cross-step descriptive associations; preserve nil when variance is zero."""
    moves = [r for r in rows if r["step"] > 0]
    switch = [r["transition"]["switch_fraction"] for r in moves]
    price = [r["price_change"] for r in moves]
    load = [r["load_change"] for r in moves]
    regret_increase = [max(0.0, r["relative_gap"] -
                           rows[i]["relative_gap"])
                       for i, r in enumerate(rows[1:])]
    bounce = [r["transition"]["two_step_aba_fraction"] for r in moves]
    steady = [r for r in moves if r["transition"]["switches"] == 0]
    changed = [r for r in moves if r["transition"]["switches"] > 0]
    hashes = [r["target_hash"] for r in rows]
    repeat2 = sum(hashes[i] == hashes[i-2] != hashes[i-1]
                  for i in range(2, len(hashes)))
    repeat3 = sum(hashes[i] == hashes[i-3]
                  for i in range(3, len(hashes)))
    return {
        "total_switches": sum(r["transition"]["switches"] for r in moves),
        "steps_with_switches": len(changed),
        "steps_without_switches": len(steady),
        "total_group_two_step_aba_reversals": sum(
            r["transition"]["two_step_aba_reversals"] for r in moves),
        "whole_target_vector_exact_period2_occurrences": repeat2,
        "whole_target_vector_exact_lag3_matches": repeat3,
        "correlation_switch_fraction_price_change": pearson(switch, price),
        "correlation_switch_fraction_load_change": pearson(switch, load),
        "correlation_switch_fraction_uphill_gap": pearson(
            switch, regret_increase),
        "correlation_aba_fraction_price_change": pearson(bounce, price),
        "mean_price_change_on_switch_steps": (
            statistics.fmean(r["price_change"] for r in changed)
            if changed else None),
        "mean_price_change_on_no_switch_steps": (
            statistics.fmean(r["price_change"] for r in steady)
            if steady else None),
        "max_price_change": max(price, default=0),
        "max_load_change": max(load, default=0),
        "max_group_aba_in_single_step": max((
            r["transition"]["two_step_aba_reversals"] for r in moves),
            default=0),
        "descriptive_only": True,
        "evidence_of_exact_full_flow_periodicity": False,
        "proof_that_switches_cause_price_shocks": False,
    }


def run_case(cfg, policy, steps, audit_every=16, probe_every=24):
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Invalid policy or step budget")
    if type(audit_every) is not int or audit_every <= 0:
        raise ValueError("Invalid financial audit cadence")
    if type(probe_every) is not int or probe_every <= 0:
        raise ValueError("Invalid counterfactual cadence")
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    observed = evaluate_r14(net, flows, settings, ())
    prev_obs = None
    older_obs = None
    previous_target = None
    delta = None
    alpha_used = None
    rows = []
    audits = []
    probes = []
    proposal_oracles = 1
    probe_oracles = 0
    total_stable = 0
    first_stable = None
    t0 = perf_counter()

    for step in range(steps + 1):
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, observed)
            audit = R21.settlement_check(net, flows)
            audits.append({"step": step, **audit})
        if observed["reachable_unpriced"] or any(
                not math.isfinite(v) or v <= 0
                for v in observed["prices"].values()):
            raise AssertionError("Invalid positive reachable prices")
        attribution = R22.attribute_gap(net, observed)
        stress = R22.edge_stress(net, observed)
        trans = transition(net, prev_obs, observed, older_obs) if prev_obs else None
        stable = R24.numerical_pass(observed, delta)
        total_stable = total_stable + 1 if stable else 0
        if total_stable >= 3 and first_stable is None:
            first_stable = step
        rows.append({
            "step": step, "flow_sha256": _flow_checksum(flows),
            "target_hash": target_signature(observed["target"]),
            "relative_gap": observed["relative_gap"],
            "max_group_gap": observed["max_group_gap"],
            "top_three_regret_share": attribution[
                "top_three_share_of_total_regret"],
            "price_change": None if delta is None else
                delta["max_relative_price_change"],
            "load_change": None if delta is None else
                delta["max_relative_load_change"],
            "quantity_change": None if delta is None else
                delta["max_relative_quantity_change"],
            "max_load_capacity_ratio": stress["maximum_load_capacity_ratio"],
            "alpha_used": alpha_used,
            "transition": trans,
            "numerical_five_thresholds_met": stable,
            "consecutive_numerical_rechecks": total_stable,
            "proposal_oracles_used": proposal_oracles,
        })
        if step % probe_every == 0:
            probe = local_probe(net, flows, observed, settings)
            probes.append({"step": step, **probe})
            probe_oracles += probe["candidate_oracle_evaluations"]
        if step == steps:
            break
        old_flow = flows
        candidate, future, delta, calls, alpha = step_forward(
            net, flows, observed, settings, policy, step + 1)
        if calls < 1:
            raise AssertionError("Uncharged proposal evaluation")
        proposal_oracles += calls
        older_obs, prev_obs = prev_obs, observed
        previous_target = observed["target"]
        flows, observed, alpha_used = candidate, future, alpha

    if proposal_oracles < steps + 1:
        raise AssertionError("Insufficient proposal accounting")
    if len(audits) == 0 or audits[-1]["step"] != steps:
        raise AssertionError("Final immutable audit missing")
    reference = R21.run_trajectory(cfg, policy, steps, audit_every=steps) \
        if policy in R21.POLICIES else None
    if reference is not None:
        if len(reference["rows"]) != len(rows):
            raise AssertionError("R21 length mismatch")
        for original, actual in zip(reference["rows"], rows):
            for key in ("flow_sha256", "relative_gap", "max_group_gap"):
                if original[key] != actual[key]:
                    raise AssertionError("R21 immutable trajectory drift")
    summary = diagnostics(rows)
    last = rows[-1]
    return {
        "policy": policy,
        "nodes": 10 * cfg.m * cfg.m + 2,
        "groups": len(net.groups),
        "steps": steps,
        "proposal_oracles_used": proposal_oracles,
        "counterfactual_probe_oracles_used": probe_oracles,
        "combined_oracles_used": proposal_oracles + probe_oracles,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": last["relative_gap"],
        "final_max_group_gap": last["max_group_gap"],
        "final_flow_sha256": last["flow_sha256"],
        "final_target_sha256": last["target_hash"],
        "first_three_joint_stable_step": first_stable,
        "financial_audits": audits,
        "diagnostics": summary,
        "counterfactual_probes": probes,
        "rows": rows,
        "elapsed_ms_observational": round((perf_counter() - t0) * 1000, 3),
        "causal_attribution_proven": False,
        "economic_equilibrium_proven": False,
        "financial_F059_ratified": False,
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
        raise ValueError("Invalid profile")
    matrix = []
    for case_name, steps in scenarios:
        data = []
        for policy in POLICIES:
            r = run_case(R21.CASES[case_name], policy, steps)
            data.append(r)
            d = r["diagnostics"]
            print("[{}:{}] gap={:.7g} worst={:.7g} switches={} "
                  "ABA={} lag2={} corrSwitchPrice={} "
                  "proposalCalls={} probeCalls={} stable={}".format(
                  case_name, policy, r["final_gap"],
                  r["final_max_group_gap"], d["total_switches"],
                  d["total_group_two_step_aba_reversals"],
                  d["whole_target_vector_exact_period2_occurrences"],
                  "n/a" if d["correlation_switch_fraction_price_change"] is None
                  else "{:.4g}".format(
                      d["correlation_switch_fraction_price_change"]),
                  r["proposal_oracles_used"],
                  r["counterfactual_probe_oracles_used"],
                  r["first_three_joint_stable_step"]), flush=True)
        matrix.append({"case": case_name, "policies": data})

    small = run_case(R21.CASES["small"], "decay_sqrt", 4, audit_every=2,
                     probe_every=2)
    long = run_case(R21.CASES["small"], "decay_sqrt", 7, audit_every=2,
                    probe_every=2)
    if small["rows"] != long["rows"][:len(small["rows"])]:
        raise AssertionError("Structural diagnostic prefix drift")
    return {"comparisons": matrix, "prefix_replay": True,
            "r20_oracle_unchanged": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R29_STRUCTURAL_INSTABILITY_DIAGNOSTIC_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "causal_attribution_proven": False,
        "economic_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("Upstream R20 manifest changed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_STRUCTURAL_DIAGNOSTICS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R29] FAILED:", repr(exc), flush=True)
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    report_path = args.output / ("GA-Gate09-R29-" + args.profile +
                                 "-" + timestamp + ".json")
    report_path.write_text(json.dumps(report, indent=2,
                                      ensure_ascii=False, sort_keys=True) +
                           "\n", encoding="utf-8")
    zip_path = args.output / ("GA-Gate09-R29-Evidencias-" + timestamp + ".zip")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(report_path, report_path.name)
    print("[R29] STATUS:", report["status"], flush=True)
    print("[R29] EVIDENCE:", zip_path, flush=True)
    return 0 if report["status"] == "PASSED_STRUCTURAL_DIAGNOSTICS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
