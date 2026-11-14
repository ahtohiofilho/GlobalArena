#!/usr/bin/env python3
"""GA Gate 09 R31: algebraic crossing analysis of frozen F-072 market rankings.

Research-only: exact F-072 market score p/(1+canonical shortest cost) at
OLD and NEW snapshots, decomposed into relative price and transport terms.
A few counterfactual *fractional steps* localize crossings between snapshots.
They are not game time, not causal experiments, and not applied to flows.
R20-R30 and C# source are untouched. F-059 is still unresolved.
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
sys.path.insert(0, str(GATE09 / "r30" / "ci"))
import run_r30 as R30

R29 = R30.R29
R21 = R30.R21
R22 = R30.R22
R24 = R30.R24
R26 = R30.R26
from clearing_r5 import ClearingSettings, _change_metrics, _flow_checksum, _mix
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14

POLICIES = R30.POLICIES
FRACTIONAL_STEPS = (0.25, 0.5, 0.75)
DEFAULT_PROBE_STEPS = (1, 6, 12, 24, 36, 48, 72, 96)
GAP_TOL = 1e-11


def market_pair_at_snapshot(net, observed, cache, gid, old_destination, new_destination):
    """Evaluate two fixed DESTINATIONS at a frozen current load/price state.

    Routes are shortest canonical paths at THAT snapshot for each destination.
    There is no assumption that they remain identical across snapshots.
    """
    group = net.groups[gid]
    distances, paths, ways = cache.paths(group.origin, group.seller)
    if old_destination not in paths or new_destination not in paths:
        raise AssertionError("Selected market not reachable under frozen loads")
    old_key = (old_destination, group.commodity)
    new_key = (new_destination, group.commodity)
    old_p, new_p = observed["prices"][old_key], observed["prices"][new_key]
    old_q, new_q = observed["quantities"][old_key], observed["quantities"][new_key]
    if not all(math.isfinite(x) and x > 0 for x in
               (old_p, new_p, old_q, new_q)):
        raise AssertionError("Pair requires positive finite prices and volumes")
    old_cost, new_cost = distances[old_destination], distances[new_destination]
    old_score = old_p / (1 + old_cost)
    new_score = new_p / (1 + new_cost)
    relative_log_price = math.log(new_p / old_p)
    relative_log_transport = math.log1p(new_cost) - math.log1p(old_cost)
    log_advantage = math.log(new_score / old_score)
    if not math.isclose(
            log_advantage, relative_log_price - relative_log_transport,
            rel_tol=0, abs_tol=GAP_TOL):
        raise AssertionError("Score log-ratio does not decompose exactly")
    return {
        "gid": gid,
        "old_destination": old_destination,
        "new_destination": new_destination,
        "old_price": old_p, "new_price": new_p,
        "old_quantity": old_q, "new_quantity": new_q,
        "old_cost": old_cost, "new_cost": new_cost,
        "old_path": list(paths[old_destination]),
        "new_path": list(paths[new_destination]),
        "old_score": old_score, "new_score": new_score,
        "log_advantage_new_over_old": log_advantage,
        "log_relative_price_new_over_old": relative_log_price,
        "log_relative_transport_new_over_old": relative_log_transport,
        "old_shortest_multiplicity_capped": ways[old_destination],
        "new_shortest_multiplicity_capped": ways[new_destination],
    }


def pair_decomposition(net, before, after, before_cache, after_cache,
                       gid, old_destination, new_destination):
    """Exact accounting identity: change of ln(S_B/S_A) = price + transport.

    The transport difference is further partitioned into:
    (a) cost movement on the OLD canonical paths repriced at NEW loads,
    (b) adjustment when the NEW canonical paths differ. Both include
    existing physical cost and tariff weights; neither is a causal share.
    """
    old = market_pair_at_snapshot(net, before, before_cache, gid,
                                  old_destination, new_destination)
    new = market_pair_at_snapshot(net, after, after_cache, gid,
                                  old_destination, new_destination)
    group = net.groups[gid]
    after_weights = after_cache.weights(group.seller)
    old_paths_at_new_cost = {
        "old": sum(after_weights[e] for e in old["old_path"]),
        "new": sum(after_weights[e] for e in old["new_path"]),
    }
    fixed_transport_ratio = (
        math.log1p(old_paths_at_new_cost["new"]) -
        math.log1p(old_paths_at_new_cost["old"]))
    delta_score = (new["log_advantage_new_over_old"] -
                   old["log_advantage_new_over_old"])
    delta_price = (new["log_relative_price_new_over_old"] -
                   old["log_relative_price_new_over_old"])
    transport_effect = -(new["log_relative_transport_new_over_old"] -
                         old["log_relative_transport_new_over_old"])
    fixed_path_effect = -(fixed_transport_ratio -
                          old["log_relative_transport_new_over_old"])
    path_reoptimization_effect = -(
        new["log_relative_transport_new_over_old"] - fixed_transport_ratio)
    demand_identity = -0.5 * (
        math.log(new["new_quantity"] / old["new_quantity"]) -
        math.log(new["old_quantity"] / old["old_quantity"]))
    for lhs, rhs, label in (
        (delta_score, delta_price + transport_effect, "price + transport"),
        (transport_effect, fixed_path_effect + path_reoptimization_effect,
         "fixed cost + reoptimization"),
        (delta_price, demand_identity, "sqrt(D / Q) repricing"),
    ):
        if not math.isclose(lhs, rhs, rel_tol=0, abs_tol=GAP_TOL):
            raise AssertionError("Invalid F-072 decomposition: " + label)
    was_old_best = old["log_advantage_new_over_old"] <= GAP_TOL
    now_new_best = new["log_advantage_new_over_old"] >= -GAP_TOL
    if not (was_old_best and now_new_best):
        raise AssertionError("Changed-market endpoint best response mismatch")
    market_route_reoptimized = (
        old["old_path"] != new["old_path"] or
        old["new_path"] != new["new_path"])
    return {
        "group": gid,
        "old_destination": old_destination,
        "new_destination": new_destination,
        "before": old, "after": new,
        "before_log_advantage_new_over_old": old["log_advantage_new_over_old"],
        "after_log_advantage_new_over_old": new["log_advantage_new_over_old"],
        "delta_log_advantage": delta_score,
        "relative_price_log_effect": delta_price,
        "relative_transport_log_effect": transport_effect,
        "fixed_old_path_transport_log_effect": fixed_path_effect,
        "canonical_reoptimization_log_effect": path_reoptimization_effect,
        "quantity_implied_log_price_effect": demand_identity,
        "absolute_price_effect_dominates": abs(delta_price) >= abs(transport_effect),
        "old_or_new_market_canonical_path_changed": market_route_reoptimized,
        "endpoint_ranking_reversed": was_old_best and now_new_best,
        "exact_algebraic_decomposition": True,
        "second_best_route_enumerated": False,
        "causal_attribution_established": False,
    }


def transition_crossings(net, before, after):
    old_target = before["target"]
    new_target = after["target"]
    if set(old_target) != set(new_target):
        raise AssertionError("Producer set changed")
    cache_before = FrozenRouteCache(net, before["loads"], 4)
    cache_after = FrozenRouteCache(net, after["loads"], 4)
    records = []
    same_destination_route_switches = 0
    for gid in sorted(old_target):
        old = old_target[gid]
        new = new_target[gid]
        if old.destination == new.destination:
            if old.edges != new.edges:
                same_destination_route_switches += 1
            continue
        records.append(pair_decomposition(
            net, before, after, cache_before, cache_after, gid,
            old.destination, new.destination))
    expected = sum(old_target[gid].destination != new_target[gid].destination
                   for gid in old_target)
    if len(records) != expected:
        raise AssertionError("Missing market-pair crossing")
    return {
        "market_switches": len(records),
        "route_only_switches": same_destination_route_switches,
        "price_dominated_switches": sum(
            int(x["absolute_price_effect_dominates"]) for x in records),
        "transport_dominated_switches": sum(
            int(not x["absolute_price_effect_dominates"]) for x in records),
        "canonical_path_reoptimizations_within_market_pair": sum(
            int(x["old_or_new_market_canonical_path_changed"]) for x in records),
        "all_endpoint_rankings_reversed": all(
            r["endpoint_ranking_reversed"] for r in records),
        "extra_shortest_path_scans": (
            cache_before.counters()["shortest_path_scans"] +
            cache_after.counters()["shortest_path_scans"]),
        "pairs": records,
    }


def crossing_substeps(net, old_flows, old_observed, new_flows, new_observed,
                      settings, alpha, pair_records, step):
    """Fractional alpha samples are UNCOMMITTED numerical snapshots.

    This is a deterministic curve through the SAME old F-072 target and
    the accepted alpha; not a historical market simulation, consumer
    choice process, or trajectory of independent strategic decisions.
    """
    if not 0 < alpha <= 1:
        raise ValueError("No valid committed alpha to sample")
    assert _flow_checksum(
        _mix(old_flows, net, old_observed["target"], alpha)) == _flow_checksum(
            new_flows), "Proposal cannot be replayed by original F-072 mix"
    old_digest, new_digest = _flow_checksum(old_flows), _flow_checksum(new_flows)
    sampled = []
    snapshots = []
    additional_shortest_path_scans = 0
    for t in FRACTIONAL_STEPS:
        intermediate = _mix(
            old_flows, net, old_observed["target"], alpha * t)
        intermediate_obs = evaluate_r14(net, intermediate, settings, ())
        frozen_cache = FrozenRouteCache(net, intermediate_obs["loads"],
                                        settings.exponent)
        vals = {}
        for row in pair_records:
            pair = market_pair_at_snapshot(
                net, intermediate_obs, frozen_cache, row["group"],
                row["old_destination"], row["new_destination"])
            vals[row["group"]] = pair["log_advantage_new_over_old"]
        additional_shortest_path_scans += frozen_cache.counters()[
            "shortest_path_scans"]
        sampled.append({
            "fraction_of_committed_alpha": t,
            "alpha_from_old_snapshot": alpha * t,
            "uncommitted_flow_sha256": _flow_checksum(intermediate),
            "relative_gap_at_uncommitted_snapshot": intermediate_obs["relative_gap"],
            "pair_log_advantages": vals,
        })
        snapshots.append(vals)
    if (_flow_checksum(old_flows) != old_digest or
            _flow_checksum(new_flows) != new_digest):
        raise AssertionError("Uncommitted counterfactual mutated committed path")
    first_cross = []
    for pair in pair_records:
        gid = pair["group"]
        points = [
            (0.0, pair["before_log_advantage_new_over_old"]),
            *[(r["fraction_of_committed_alpha"],
               snap[gid]) for r, snap in zip(sampled, snapshots)],
            (1.0, pair["after_log_advantage_new_over_old"]),
        ]
        intervals = [[points[i-1][0], points[i][0]]
                     for i in range(1, len(points))
                     if points[i-1][1] < 0 <= points[i][1]]
        first_cross.append({
            "group": gid,
            "sign_change_intervals": intervals,
            "first_observed_crossing_interval": (
                intervals[0] if intervals else None),
            "fractional_samples_are_not_committed_steps": True,
        })
    return {
        "step": step,
        "accepted_alpha": alpha,
        "source_flow_sha256": old_digest,
        "committed_flow_sha256": new_digest,
        "additional_r14_oracle_calls": len(FRACTIONAL_STEPS),
        "additional_shortest_path_scans": additional_shortest_path_scans,
        "fractional_snapshots": sampled,
        "crossing_intervals": first_cross,
        "no_claim_of_unique_crossing": True,
        "no_intermediate_flow_committed": True,
    }


def summarize(rows, substeps):
    transitions = [r for r in rows if r["step"] > 0]
    total_switches = sum(r["crossings"]["market_switches"]
                         for r in transitions)
    price_dominated = sum(r["crossings"]["price_dominated_switches"]
                          for r in transitions)
    route_only = sum(r["crossings"]["route_only_switches"]
                     for r in transitions)
    reopt = sum(r["crossings"][
        "canonical_path_reoptimizations_within_market_pair"]
        for r in transitions)
    abs_price_shocks = [
        abs(rec["relative_price_log_effect"]) for r in transitions
        for rec in r["crossings"]["pairs"]]
    abs_transport_shocks = [
        abs(rec["relative_transport_log_effect"]) for r in transitions
        for rec in r["crossings"]["pairs"]]
    return {
        "total_market_switches": total_switches,
        "total_route_only_switches": route_only,
        "market_switches_price_effect_dominated": price_dominated,
        "market_switches_transport_effect_dominated": total_switches - price_dominated,
        "market_pairs_with_canonical_path_reoptimization": reopt,
        "fraction_price_effect_dominated": (
            price_dominated / total_switches if total_switches else None),
        "mean_abs_log_relative_price_effect": (
            statistics.fmean(abs_price_shocks) if abs_price_shocks else None),
        "mean_abs_log_relative_transport_effect": (
            statistics.fmean(abs_transport_shocks) if abs_transport_shocks else None),
        "substep_transitions_sampled": len(substeps),
        "substep_oracle_evaluations": sum(
            x["additional_r14_oracle_calls"] for x in substeps),
        "individual_pair_crossing_intervals_observed": sum(
            len(y["sign_change_intervals"]) for x in substeps
            for y in x["crossing_intervals"]),
        "mathematical_decomposition_only": True,
        "price_and_quantity_counterfactual_causality_proven": False,
        "convergence_or_equilibrium_proven": False,
    }


def run_case(cfg, policy, steps, audit_every=16, probe_steps=DEFAULT_PROBE_STEPS):
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Invalid policy or iterations")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit cadence")
    if not isinstance(probe_steps, (tuple, list)) or any(
            type(x) is not int or x < 1 for x in probe_steps):
        raise ValueError("Invalid probe checkpoints")
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    old_obs, old_flows = None, None
    decision_alpha, current_delta = None, None
    rows, audits, substeps = [], [], []
    stable_streak = 0
    first_joint = None
    proposal_oracles = 1
    probe_oracles = 0
    margin_scans = 0
    start = perf_counter()

    for step in range(steps + 1):
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, obs)
            audited = R21.settlement_check(net, flows)
            audits.append({"step": step, **audited})
        R30.market_alternatives(net, obs, settings.exponent)
        if obs["reachable_unpriced"] or any(
            not math.isfinite(p) or p <= 0 for p in obs["prices"].values()):
            raise AssertionError("A market lacks a positive finite price")

        crossings = (transition_crossings(net, old_obs, obs)
                     if old_obs is not None else None)
        margin_scans += 0 if crossings is None else crossings[
            "extra_shortest_path_scans"]
        is_stable = R24.numerical_pass(obs, current_delta)
        stable_streak = stable_streak + 1 if is_stable else 0
        if stable_streak >= 3 and first_joint is None:
            first_joint = step
        rows.append({
            "step": step,
            "flow_sha256": _flow_checksum(flows),
            "target_sha256": R29.target_signature(obs["target"]),
            "relative_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "alpha_used": decision_alpha,
            "price_change": None if current_delta is None else
                current_delta["max_relative_price_change"],
            "load_change": None if current_delta is None else
                current_delta["max_relative_load_change"],
            "quantity_change": None if current_delta is None else
                current_delta["max_relative_quantity_change"],
            "crossings": crossings,
            "numerical_five_thresholds_met": is_stable,
            "consecutive_numerical_rechecks": stable_streak,
            "proposal_oracles_used": proposal_oracles,
        })

        if (old_obs is not None and crossings["market_switches"] > 0
                and step in probe_steps):
            probe = crossing_substeps(
                net, old_flows, old_obs, flows, obs,
                settings, decision_alpha, crossings["pairs"], step)
            substeps.append(probe)
            probe_oracles += probe["additional_r14_oracle_calls"]
            margin_scans += probe["additional_shortest_path_scans"]

        if step == steps:
            break
        prev_flows, prev_obs = flows, obs
        next_flows, next_obs, delta, calls, alpha = R29.step_forward(
            net, flows, obs, settings, policy, step + 1)
        if calls < 1:
            raise AssertionError("Uncharged proposal candidate")
        proposal_oracles += calls
        old_obs, old_flows = prev_obs, prev_flows
        flows, obs, current_delta, decision_alpha = (
            next_flows, next_obs, delta, alpha)

    if audits[-1]["step"] != steps:
        raise AssertionError("Final financial checkpoint missing")
    if proposal_oracles < steps + 1 or probe_oracles != (
        len(substeps) * len(FRACTIONAL_STEPS)
    ):
        raise AssertionError("Incorrect F072 oracle accounting")
    summary = summarize(rows, substeps)
    return {
        "policy": policy, "nodes": 10 * cfg.m * cfg.m + 2,
        "producer_groups": len(net.groups), "steps": steps,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": rows[-1]["relative_gap"],
        "final_max_group_gap": rows[-1]["max_group_gap"],
        "final_flow_sha256": rows[-1]["flow_sha256"],
        "first_three_joint_stable_step": first_joint,
        "proposal_oracles_used": proposal_oracles,
        "substep_probe_oracles_used": probe_oracles,
        "total_primary_r14_evaluations": proposal_oracles + probe_oracles,
        "extra_independent_shortest_path_scans": margin_scans,
        "summary": summary, "rows": rows,
        "uncommitted_probes": substeps,
        "financial_audits": audits,
        "elapsed_ms_observational": round((perf_counter()-start)*1000, 3),
        "second_best_route_enumerated": False,
        "price_or_congestion_causality_proven": False,
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def exercise(profile):
    if profile == "smoke":
        plans = (("small", 8),)
    elif profile == "ci":
        plans = (("small", 96), ("medium", 48))
    elif profile == "extended":
        plans = (("small", 160), ("medium", 96), ("large", 24))
    else:
        raise ValueError("Unsupported profile")
    comparisons = []
    for label, steps in plans:
        policies = []
        for policy in POLICIES:
            r = run_case(R21.CASES[label], policy, steps)
            s = r["summary"]
            print("[{}:{}] gap={:.7g} worst={:.7g} "
                  "marketSwitches={} priceDominated={} transportDominated={} "
                  "pathReopt={} midSteps={} probeOracles={} "
                  "proposalOracles={} stable={}".format(
                label, policy, r["final_gap"], r["final_max_group_gap"],
                s["total_market_switches"],
                s["market_switches_price_effect_dominated"],
                s["market_switches_transport_effect_dominated"],
                s["market_pairs_with_canonical_path_reoptimization"],
                s["substep_transitions_sampled"],
                r["substep_probe_oracles_used"],
                r["proposal_oracles_used"],
                r["first_three_joint_stable_step"]), flush=True)
            policies.append(r)
        comparisons.append({"case": label, "policies": policies})

    short = run_case(R21.CASES["small"], "decay_sqrt", 4,
                     audit_every=2, probe_steps=(1, 2, 4))
    long = run_case(R21.CASES["small"], "decay_sqrt", 7,
                    audit_every=2, probe_steps=(1, 2, 4))
    if short["rows"] != long["rows"][:len(short["rows"])]:
        raise AssertionError("Non-deterministic R31 prefix")
    for base in ("decay_sqrt", "backtracking"):
        R24.check_r21_reference(R21.CASES["small"], base, 6)
    return {"comparisons": comparisons, "r20_f072_immutable": True,
            "r21_replay": True, "prefix_replay": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R31_MARKET_CROSSING_LOG_DECOMPOSITION_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "causality_proven": False, "global_equilibrium_proven": False,
        "financial_F059_ratified": False, "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("R20 immutable baseline drift")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_ALGEBRAIC_DIAGNOSTICS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R31] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    f = args.output / ("GA-Gate09-R31-" + args.profile + "-" + stamp + ".json")
    f.write_text(json.dumps(report, indent=2, sort_keys=True,
                            ensure_ascii=False) + "\n", encoding="utf-8")
    z = args.output / ("GA-Gate09-R31-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(z, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(f, f.name)
    print("[R31] STATUS:", report["status"], flush=True)
    print("[R31] EVIDENCE:", z, flush=True)
    return 0 if report["status"] == "PASSED_ALGEBRAIC_DIAGNOSTICS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
