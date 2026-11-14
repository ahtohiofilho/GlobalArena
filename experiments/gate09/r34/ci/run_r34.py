#!/usr/bin/env python3
"""GA Gate09 R34: controlled capacity-factor sensitivity frontier.

Experimental observational diagnostics ONLY. All input scenarios use the
unchanged F-072 score P/(1+C), P=sqrt(D/Q), congestion c*(1+(B/K)^4),
original ownership and tariffs, original R21/R26 policies and F-084F.
No output of the frozen-price hybrids is fed to the solver.
"""
from __future__ import annotations

import argparse
from dataclasses import replace
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "r33" / "ci"))
import run_r33 as R33

R32 = R33.R32
R29 = R33.R29
R21 = R33.R21
R22 = R33.R22
R24 = R33.R24
from clearing_r5 import ClearingSettings, _flow_checksum
from gate09 import Network
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

# Each value means K_new = factor * K_original, not extra construction
# or an in-game investment decision. Exact enumeration, no adaptivity.
CAPACITY_FACTORS = (0.15, 0.20, 0.25, 0.30, 0.40,
                    0.50, 0.75, 1.0, 1.5, 4.0)
REGIMES = ("original", "heterogeneous_D_and_supply")
POLICIES = ("decay_sqrt", "backtracking", "progressive_global")
FRACTION_THRESHOLDS = (0.95, 0.99)
MAX_FAILURE_WITNESSES_PER_STEP = 6


def capacity_fixture(cfg, regime, factor):
    """Strictly rescale edge.capacity, plus FIXED R33 demand/supply regime.

    Rebuild each independent source fixture from R13, so the graph,
    tariff ownership, c_free, geography and prices are never
    shared/mutated across capacity evaluations.
    """
    if regime not in REGIMES or type(factor) not in (int, float) or (
            not math.isfinite(factor) or factor <= 0):
        raise ValueError("Invalid regime/capacity scale")
    if factor not in CAPACITY_FACTORS:
        raise ValueError("Nonregistered capacity factor")
    base, initial = construct_scenario(cfg)
    if regime == "original":
        source, flows, source_meta = R33.fixture_variant(cfg, "baseline")
    else:
        # Start from R33's deterministic demand/supply mosaic, then
        # RESET its capacity=0.2 to the original reference capacity.
        source, flows, source_meta = R33.fixture_variant(cfg, "combined_stress")
    edges = []
    for eid, edge in sorted(source.edges.items()):
        reference = base.edges[eid]
        if replace(edge, capacity=reference.capacity) != reference:
            raise AssertionError("Noncapacity edge field drift")
        edges.append(replace(reference, capacity=reference.capacity * factor))
    net = Network(
        edges,
        [g for gid, g in sorted(source.groups.items())],
        [m for key, m in sorted(source.markets.items())],
    )
    for eid in base.edges:
        if replace(net.edges[eid], capacity=base.edges[eid].capacity) != base.edges[eid]:
            raise AssertionError("Topology, tariff or free-cost was changed")
    for key in base.markets:
        if regime == "original" and net.markets[key] != base.markets[key]:
            raise AssertionError("Original demand was changed")
    for gid in base.groups:
        if regime == "original" and net.groups[gid] != base.groups[gid]:
            raise AssertionError("Original production was changed")
    net.snapshots(flows)
    return net, flows, {
        "regime": regime,
        "capacity_factor": factor,
        "reference_fixture_fingerprint": R33.fixture_fingerprint(base),
        "source_fixture_fingerprint": source_meta["fixture_fingerprint"],
        "actual_fixture_fingerprint": R33.fixture_fingerprint(net),
        "price_law": "P=sqrt(D/Q)",
        "congestion_law": "c_free*(1+(B/K)^4)",
        "demand_and_supply_fixed_during_trajectory": True,
        "tariffs_and_owners_unchanged": True,
    }


def failure_witnesses(before, after, hybrid):
    """Limited concrete examples of price-only failing the full ranking."""
    witnesses = []
    for gid, record in sorted(hybrid["groups"].items()):
        if not record["is_market_switch"]:
            continue
        if record["four_market_winners"]["price_only"] == record["new_market"]:
            continue
        witnesses.append({
            "gid": gid,
            "old_market": record["old_market"],
            "new_market": record["new_market"],
            "price_only_market": record["four_market_winners"]["price_only"],
            "transport_only_market": record["four_market_winners"]["transport_only"],
            "old_score_log_advantage": record["log_advantage_B_over_A_old"],
            "new_score_log_advantage": record["log_advantage_B_over_A_new"],
            "price_log_component": record["delta_log_relative_price"],
            "transport_log_component": record["delta_log_relative_transport"],
            "pair_crossing_class": record["pair_crossing_class"],
            "full_r14_previous_best": before["target"][gid].destination,
            "full_r14_actual_best": after["target"][gid].destination,
            "an_observational_nonclearing_hybrid": True,
        })
        if len(witnesses) >= MAX_FAILURE_WITNESSES_PER_STEP:
            break
    return witnesses


def run_case(cfg, regime, factor, policy, steps, audit_every=8):
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Unregistered policy or invalid work budget")
    if type(audit_every) is not int or audit_every <= 0:
        raise ValueError("Invalid audit cadence")
    net, flows, fixture = capacity_fixture(cfg, regime, factor)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    initial_hash = _flow_checksum(flows)
    before = None
    delta = None
    alpha = None
    calls = 1
    scans = 0
    audits, rows = [], []
    stable_count = 0
    first_stable = None
    start = perf_counter()

    for step in range(steps + 1):
        R33.verify_price_law(net, obs)
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, obs)
            financial = R21.settlement_check(net, flows)
            audits.append({"step": step, **financial})
        if obs["reachable_unpriced"]:
            raise AssertionError("Frozen market missing a price")
        hybrid = (R32.frozen_hybrid_rankings(net, before, obs, cfg.exponent)
                  if before is not None else None)
        if hybrid is not None:
            scans += hybrid["independent_shortest_path_scans"]
        stress = R22.edge_stress(net, obs)
        numerically_stable = R24.numerical_pass(obs, delta)
        stable_count = stable_count + 1 if numerically_stable else 0
        if stable_count >= 3 and first_stable is None:
            first_stable = step

        rows.append({
            "step": step,
            "flow_sha256": _flow_checksum(flows),
            "relative_gap": obs["relative_gap"],
            "worst_group_gap": obs["max_group_gap"],
            "peak_load_over_capacity": stress["maximum_load_capacity_ratio"],
            "edges_over_capacity": stress[
                "edge_count_above_or_at_natural_capacity"],
            "price_change": None if delta is None else
                delta["max_relative_price_change"],
            "load_change": None if delta is None else
                delta["max_relative_load_change"],
            "quantity_change": None if delta is None else
                delta["max_relative_quantity_change"],
            "selected_alpha": alpha,
            "policy_oracle_calls_cumulative": calls,
            "numerical_five_thresholds_met": numerically_stable,
            "consecutive_numerical_rechecks": stable_count,
            "hybrid_counts": None if hybrid is None else hybrid["counts"],
            "price_failure_witnesses": [] if hybrid is None else
                failure_witnesses(before, obs, hybrid),
        })
        if step == steps:
            break
        new_flows, next_obs, delta, used, alpha = R29.step_forward(
            net, flows, obs, settings, policy, step + 1)
        if used < 1:
            raise AssertionError("Policy R14 calls cannot be free")
        calls += used
        before = obs
        flows, obs = new_flows, next_obs

    if audits[-1]["step"] != steps:
        raise AssertionError("Missing final financial conservation audit")
    counts = {
        k: sum(row["hybrid_counts"][k] for row in rows[1:])
        for k in rows[1]["hybrid_counts"]
    }
    switches = counts["market_switches"]
    if counts["price_only_winner_is_actual"] > switches or (
            counts["transport_only_winner_is_actual"] > switches):
        raise AssertionError("Hybrid successes exceed actual market switches")
    if counts["price_log_effect_dominant"] + counts[
            "transport_log_effect_dominant"] != switches:
        raise AssertionError("Magnitude dominance not partitioned")
    if sum(counts[k] for k in (
        "pair_both_individual_cross", "pair_price_only_cross",
        "pair_transport_only_cross", "pair_neither_individual_cross"
    )) != switches:
        raise AssertionError("Pairwise crossing not partitioned")
    if sum(len(row["price_failure_witnesses"])
           for row in rows[1:]) > switches - counts["price_only_winner_is_actual"]:
        raise AssertionError("Invalid failure witness count")
    return {
        "regime": regime, "capacity_factor": factor, "policy": policy,
        "nodes": 10*cfg.m*cfg.m+2, "steps": steps,
        "fixture": fixture,
        "initial_flow_sha256": initial_hash,
        "final_flow_sha256": rows[-1]["flow_sha256"],
        "initial_global_gap": rows[0]["relative_gap"],
        "final_global_gap": rows[-1]["relative_gap"],
        "final_worst_group_gap": rows[-1]["worst_group_gap"],
        "max_load_over_capacity_across_steps": max(
            row["peak_load_over_capacity"] for row in rows),
        "max_edges_over_capacity_across_steps": max(
            row["edges_over_capacity"] for row in rows),
        "first_three_joint_stable_step": first_stable,
        "policy_r14_calls": calls,
        "hybrid_extra_r14_calls": 0,
        "independent_shortest_path_scans": scans,
        "market_switches": switches,
        "price_only_full_winner_matches": counts["price_only_winner_is_actual"],
        "transport_only_full_winner_matches": counts[
            "transport_only_winner_is_actual"],
        "price_only_full_winner_fraction": (
            counts["price_only_winner_is_actual"]/switches if switches else None),
        "transport_only_full_winner_fraction": (
            counts["transport_only_winner_is_actual"]/switches if switches else None),
        "price_log_magnitude_dominant": counts[
            "price_log_effect_dominant"],
        "pair_price_only_cross": counts["pair_price_only_cross"],
        "pair_transport_only_cross": counts["pair_transport_only_cross"],
        "pair_both_cross": counts["pair_both_individual_cross"],
        "pair_neither_cross": counts["pair_neither_individual_cross"],
        "numerically_observed_not_causal": True,
        "hybrids_are_not_market_clearing_states": True,
        "rows": rows,
        "financial_audits": audits,
        "elapsed_ms_observational": round((perf_counter()-start)*1000, 3),
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }


def aggregate_capacity(regime, factor, records):
    """Pool event counts but preserve no-switch/null and per-policy detail."""
    matches = [r for r in records if r["regime"] == regime and
               r["capacity_factor"] == factor]
    if not matches:
        raise AssertionError("Missing sweep coordinate")
    switches = sum(r["market_switches"] for r in matches)
    price = sum(r["price_only_full_winner_matches"] for r in matches)
    transport = sum(r["transport_only_full_winner_matches"] for r in matches)
    peaks = [r["max_load_over_capacity_across_steps"] for r in matches]
    fractions = [r["price_only_full_winner_fraction"] for r in matches
                 if r["market_switches"]]
    return {
        "regime": regime, "capacity_factor": factor,
        "runs": len(matches), "switches": switches,
        "price_only_winner_matches": price,
        "transport_only_winner_matches": transport,
        "price_only_winner_fraction": price/switches if switches else None,
        "transport_only_winner_fraction": (
            transport/switches if switches else None),
        "mean_run_price_only_fraction": (
            sum(fractions)/len(fractions) if fractions else None),
        "min_nonempty_run_price_only_fraction": (
            min(fractions) if fractions else None),
        "max_peak_load_over_capacity": max(peaks),
        "min_peak_load_over_capacity": min(peaks),
        "runs_with_no_switches": sum(not r["market_switches"] for r in matches),
        "no_switch_share_is_null": not switches,
        "event_counts_not_independent_statistical_samples": True,
    }


def observed_brackets(capacity_rows):
    """Find discrete pass/fail boundaries, NOT a monotonic or causal threshold.

    A performance bracket has adjacent measured capacity factors whose
    pooled price-only winner fraction straddles the selected cutoff.
    Report *all* crossings, even if nonlinear/non-monotone.
    """
    ordered = sorted(capacity_rows, key=lambda r:r["capacity_factor"])
    if len({x["capacity_factor"] for x in ordered}) != len(ordered):
        raise AssertionError("Duplicate capacities in one regime")
    result = {}
    for threshold in FRACTION_THRESHOLDS:
        brackets = []
        for low, high in zip(ordered, ordered[1:]):
            x, y = low["price_only_winner_fraction"], high[
                "price_only_winner_fraction"]
            if x is None or y is None:
                continue
            if (x < threshold) != (y < threshold):
                brackets.append({
                    "capacity_factor_below": low["capacity_factor"],
                    "capacity_factor_above": high["capacity_factor"],
                    "below_fraction": x, "above_fraction": y,
                    "below_peak_load_over_capacity": low[
                        "max_peak_load_over_capacity"],
                    "above_peak_load_over_capacity": high[
                        "max_peak_load_over_capacity"],
                    "crossed_cutoff_not_a_mathematical_phase_transition": True,
                })
        result[str(threshold)] = brackets
    return result


def overview(results):
    by_regime = {}
    for regime in REGIMES:
        rows = [aggregate_capacity(regime, factor, results)
                for factor in CAPACITY_FACTORS]
        aggregate_switches = sum(r["switches"] for r in rows)
        aggregate_price = sum(r["price_only_winner_matches"] for r in rows)
        aggregate_transport = sum(r["transport_only_winner_matches"] for r in rows)
        by_regime[regime] = {
            "capacities": rows,
            "adjacent_sampled_capacity_brackets": observed_brackets(rows),
            "switches": aggregate_switches,
            "price_only_matches": aggregate_price,
            "transport_only_matches": aggregate_transport,
            "event_weighted_price_only_fraction": (
                aggregate_price / aggregate_switches
                if aggregate_switches else None),
            "minimum_nonempty_capacity_fraction": min(
                (r["price_only_winner_fraction"] for r in rows
                 if r["price_only_winner_fraction"] is not None),
                default=None),
            "no_monotonicity_presumed": True,
        }
    total_switches = sum(r["market_switches"] for r in results)
    total_price = sum(r["price_only_full_winner_matches"] for r in results)
    total_transport = sum(r["transport_only_full_winner_matches"] for r in results)
    return {
        "regimes": by_regime,
        "total_runs": len(results),
        "total_switches": total_switches,
        "price_only_full_winner_matches": total_price,
        "transport_only_full_winner_matches": total_transport,
        "event_weighted_price_only_fraction": (
            total_price/total_switches if total_switches else None),
        "event_weighted_transport_only_fraction": (
            total_transport/total_switches if total_switches else None),
        "policy_r14_calls": sum(r["policy_r14_calls"] for r in results),
        "hybrid_extra_r14_calls": 0,
        "independent_shortest_path_scans": sum(
            r["independent_shortest_path_scans"] for r in results),
        "shared_fixtures_and_policies_not_independent_samples": True,
    }


def exercise(profile):
    plans = {
        "smoke": (("small", 5),),
        "ci": (("small", 24), ("medium", 12)),
        "extended": (("small", 48), ("medium", 24), ("large", 8)),
    }
    if profile not in plans:
        raise ValueError("Unknown profile")
    regimes = REGIMES if profile != "smoke" else ("original",)
    factors = CAPACITY_FACTORS if profile != "smoke" else (0.20, 0.50, 1.0)
    methods = POLICIES if profile != "smoke" else ("decay_sqrt",)
    results = []
    for label, updates in plans[profile]:
        for regime in regimes:
            for factor in factors:
                for policy in methods:
                    row = run_case(
                        R21.CASES[label], regime, factor, policy, updates)
                    results.append(row)
                    print(
                        "[{}:{}:Kx{}:{}] switches={} price={} transport={} "
                        "peakRatio={:.5g} finalGap={:.7g} worst={:.7g} "
                        "r14={} scans={} stable={}".format(
                            label, regime, factor, policy,
                            row["market_switches"],
                            row["price_only_full_winner_matches"],
                            row["transport_only_full_winner_matches"],
                            row["max_load_over_capacity_across_steps"],
                            row["final_global_gap"],
                            row["final_worst_group_gap"],
                            row["policy_r14_calls"],
                            row["independent_shortest_path_scans"],
                            row["first_three_joint_stable_step"]), flush=True)

    if profile == "smoke":
        return {"cases": results,
                "aggregate": {"smoke_only": True, "runs": len(results)},
                "reference_replay": True}
    base = run_case(R21.CASES["small"], "original", 1.0,
                    "decay_sqrt", 5, audit_every=2)
    ref = R33.run_case(R21.CASES["small"], "baseline",
                       "decay_sqrt", 5, audit_every=2)
    if (base["final_flow_sha256"] != ref["final_flow_sha256"] or
        base["final_global_gap"] != ref["final_gap"] or
        base["market_switches"] != ref["summary"]["market_switches"] or
        base["policy_r14_calls"] != ref["proposal_oracle_calls"]):
        raise AssertionError("Frozen R33 1x capacity baseline drift")
    for policy in ("decay_sqrt", "backtracking"):
        R24.check_r21_reference(R21.CASES["small"], policy, 6)
    short = run_case(R21.CASES["small"], "original", 0.25,
                     "decay_sqrt", 4, audit_every=2)
    long = run_case(R21.CASES["small"], "original", 0.25,
                    "decay_sqrt", 7, audit_every=2)
    if short["rows"] != long["rows"][:len(short["rows"])]:
        raise AssertionError("Nondeterministic R34 prefix")
    report = {
        "cases": results,
        "reference_replay": True,
        "r20_f072_unchanged": True,
        "prefix_deterministic": True,
    }
    if profile == "ci":
        report["aggregate"] = overview(results)
    else:
        # Extended combines a third network size, still same factors.
        report["aggregate"] = overview(results)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R34_CAPACITY_SWEEP_OBSERVATIONAL_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "causal_attribution_proven": False,
        "phase_transition_proven": False,
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("Frozen R20 manifest failed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_CAPACITY_SWEEP_DIAGNOSTICS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R34] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = args.output / ("GA-Gate09-R34-" + args.profile + "-" + stamp + ".json")
    path.write_text(json.dumps(report, sort_keys=True, indent=2,
                               ensure_ascii=False) + "\n", encoding="utf-8")
    z = args.output / ("GA-Gate09-R34-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(z, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(path, path.name)
    print("[R34] STATUS:", report["status"], flush=True)
    print("[R34] EVIDENCE:", z, flush=True)
    return 0 if report["status"] == "PASSED_CAPACITY_SWEEP_DIAGNOSTICS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
