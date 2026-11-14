#!/usr/bin/env python3
"""GA Gate09 R33 - deterministic EXOGENOUS fixture sensitivity matrix.

Price P=sqrt(D/Q), F-072 choices, edge congestion exponent, F-084F
settlement and all R20-R32 modules are unchanged. Frozen price/transport
hybrid market rankings are used for OBSERVATION ONLY. No C# integration.
"""
from __future__ import annotations

import argparse
from dataclasses import replace
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import platform
import sys
from time import perf_counter
import traceback
import zipfile

GATE09 = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(GATE09 / "r32" / "ci"))
import run_r32 as R32

R29 = R32.R29
R21 = R32.R21
R22 = R32.R31.R22
R24 = R32.R24
R26 = R32.R26
from clearing_r5 import ClearingSettings, _flow_checksum
from gate09 import Network
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14

VARIANTS = (
    "baseline",
    "demand_uniform_4x",
    "demand_mosaic",
    "supply_mosaic",
    "capacity_quarter",
    "capacity_4x",
    "capacity_mosaic",
    "combined_stress",
)
POLICIES = ("decay_sqrt", "backtracking", "progressive_global")
DEMAND_MOSAIC = (0.35, 0.7, 1.3, 2.4)
SUPPLY_MOSAIC = (0.5, 0.75, 1.25, 2.0)
CAPACITY_MOSAIC = (0.25, 0.5, 1.5, 4.0)


def fixture_fingerprint(net):
    """Stable identifier for ALL changed numeric inputs and unchanged law."""
    record = (
        tuple((k, m.demand) for k, m in sorted(net.markets.items())),
        tuple((gid, g.supply, g.origin, g.commodity, g.seller)
              for gid, g in sorted(net.groups.items())),
        tuple((eid, e.a, e.b, e.c_free, e.capacity,
               e.tariff_micro, e.owners)
              for eid, e in sorted(net.edges.items())),
    )
    return hashlib.sha256(repr(record).encode("utf-8")).hexdigest()


def fixture_variant(cfg, name):
    """Build an untouched reference fixture, then change only INPUT data.

    Demand in each market is a fixed exogenous level, not an in-run
    demand boost, purchase reservation, wallet, or extra buyer.
    Production amounts are reallocated to preserve 100% sale.
    Capacities affect only the original nonlinear congestion law.
    Topology, origin, owner and tariff rules are NEVER changed.
    """
    if name not in VARIANTS:
        raise ValueError("Unregistered fixture variant")
    if cfg.exponent != 4:
        raise ValueError("R21/R13 independent reference assumes p=4")
    base, original_flows = construct_scenario(cfg)
    original_digest = fixture_fingerprint(base)
    if name == "baseline":
        return base, original_flows, {
            "variant": name, "reference_fingerprint": original_digest,
            "fixture_fingerprint": original_digest,
            "market_demand_factors": [1.] * len(base.markets),
            "group_supply_factors": [1.] * len(base.groups),
            "edge_capacity_factors": [1.] * len(base.edges),
            "input_axes_changed": [],
        }

    demand_factors = {}
    for ix, key in enumerate(sorted(base.markets)):
        factor = (
            4.0 if name == "demand_uniform_4x"
            else DEMAND_MOSAIC[ix % len(DEMAND_MOSAIC)]
            if name in ("demand_mosaic", "combined_stress")
            else 1.0)
        demand_factors[key] = factor
    supply_factors = {}
    for ix, gid in enumerate(sorted(base.groups)):
        factor = (SUPPLY_MOSAIC[ix % len(SUPPLY_MOSAIC)]
                  if name in ("supply_mosaic", "combined_stress")
                  else 1.0)
        supply_factors[gid] = factor
    capacity_factors = {}
    for ix, eid in enumerate(sorted(base.edges)):
        factor = (
            0.25 if name == "capacity_quarter"
            else 4.0 if name == "capacity_4x"
            else CAPACITY_MOSAIC[ix % len(CAPACITY_MOSAIC)]
            if name == "capacity_mosaic"
            else 0.2 if name == "combined_stress"
            else 1.0)
        capacity_factors[eid] = factor

    edges = [replace(edge, capacity=edge.capacity * capacity_factors[eid])
             for eid, edge in sorted(base.edges.items())]
    groups = [replace(g, supply=g.supply * supply_factors[gid])
              for gid, g in sorted(base.groups.items())]
    markets = [replace(m, demand=m.demand * demand_factors[key])
               for key, m in sorted(base.markets.items())]
    net = Network(edges, groups, markets)
    flows = {
        col: amount * supply_factors[col.group_id]
        for col, amount in sorted(original_flows.items())
    }

    if (set(net.edges) != set(base.edges) or
        set(net.markets) != set(base.markets) or
        set(net.groups) != set(base.groups)):
        raise AssertionError("Fixture topology or identifier drift")
    for eid in net.edges:
        first, second = base.edges[eid], net.edges[eid]
        if replace(second, capacity=first.capacity) != first:
            raise AssertionError("Non-capacity transport field changed")
    for key in net.markets:
        if replace(net.markets[key], demand=base.markets[key].demand) != base.markets[key]:
            raise AssertionError("Non-demand market field changed")
    for gid in net.groups:
        if replace(net.groups[gid], supply=base.groups[gid].supply) != base.groups[gid]:
            raise AssertionError("Non-supply producer field changed")
    loads, q, prices = net.snapshots(flows)
    if set(q) != set(net.markets) or any(q[k] <= 0 for k in net.markets):
        raise AssertionError("Initial variant has an empty market")
    if any(not math.isfinite(p) or p <= 0 for p in prices.values()):
        raise AssertionError("Unpriced scenario")
    original_again, flows_again = construct_scenario(cfg)
    if (fixture_fingerprint(original_again) != original_digest or
        _flow_checksum(flows_again) != _flow_checksum(original_flows)):
        raise AssertionError("Underlying canonical fixture was mutated")
    axes = [
        axis for axis, factors in (
            ("market_demand", demand_factors.values()),
            ("producer_supply", supply_factors.values()),
            ("edge_capacity", capacity_factors.values()),
        ) if any(v != 1.0 for v in factors)
    ]
    return net, flows, {
        "variant": name, "reference_fingerprint": original_digest,
        "fixture_fingerprint": fixture_fingerprint(net),
        "market_demand_factors": list(demand_factors.values()),
        "group_supply_factors": list(supply_factors.values()),
        "edge_capacity_factors": list(capacity_factors.values()),
        "input_axes_changed": axes,
    }


def verify_price_law(net, obs):
    """Independently verify fixed exogenous D and the unchanged price law."""
    if set(obs["prices"]) != set(net.markets):
        raise AssertionError("Market keys changed")
    for key, market in sorted(net.markets.items()):
        q = obs["quantities"].get(key, 0.0)
        p = obs["prices"][key]
        if not (q > 0 and math.isfinite(q) and math.isfinite(p) and p > 0):
            raise AssertionError("Undefined price or sold amount")
        if not math.isclose(p, math.sqrt(market.demand / q),
                            rel_tol=1e-12, abs_tol=1e-12):
            raise AssertionError("Price P=sqrt(D/Q) was changed")
    return True


def aggregate_group_counts(rows):
    results = [row["hybrid"]["counts"] for row in rows[1:]]
    if not results:
        raise ValueError("At least one priced update required")
    keys = results[0]
    totals = {key: sum(r[key] for r in results) for key in keys}
    changes = totals["market_switches"]
    return {
        **totals,
        "price_only_full_new_winner_share": (
            totals["price_only_winner_is_actual"]/changes if changes else None),
        "transport_only_full_new_winner_share": (
            totals["transport_only_winner_is_actual"]/changes if changes else None),
        "price_log_magnitude_dominance_share": (
            totals["price_log_effect_dominant"]/changes if changes else None),
        "pair_price_only_crossing_share": (
            (totals["pair_price_only_cross"] +
             totals["pair_both_individual_cross"])/changes if changes else None),
        "pair_transport_only_crossing_share": (
            (totals["pair_transport_only_cross"] +
             totals["pair_both_individual_cross"])/changes if changes else None),
        "no_switch_denominator_is_null_not_zero": changes == 0,
        "hybrid_ranking_is_not_clearing_state": True,
    }


def run_case(cfg, variant, policy, steps, audit_every=12):
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Invalid reference policy/step budget")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid financial audit interval")

    net, flows, meta = fixture_variant(cfg, variant)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    old_obs = None
    delta = None
    alpha_used = None
    proposals = 1
    scans = 0
    audits, rows = [], []
    streak = 0
    first_stable = None
    t0 = perf_counter()
    for step in range(steps + 1):
        verify_price_law(net, obs)
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, obs)
            fiscal = R21.settlement_check(net, flows)
            audits.append({"step": step, **fiscal})
        if obs["reachable_unpriced"]:
            raise AssertionError("Nonempty market lacks a quote")

        hybrid = (R32.frozen_hybrid_rankings(net, old_obs, obs, settings.exponent)
                  if old_obs is not None else None)
        if hybrid:
            scans += hybrid["independent_shortest_path_scans"]
        stress = R22.edge_stress(net, obs)
        stable = R24.numerical_pass(obs, delta)
        streak = streak + 1 if stable else 0
        if streak >= 3 and first_stable is None:
            first_stable = step
        rows.append({
            "step": step, "flow_sha256": _flow_checksum(flows),
            "target_sha256": R29.target_signature(obs["target"]),
            "global_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "max_load_over_capacity": stress["maximum_load_capacity_ratio"],
            "loaded_edge_count_at_or_above_capacity": stress[
                "edge_count_above_or_at_natural_capacity"],
            "price_change": None if delta is None else
                delta["max_relative_price_change"],
            "load_change": None if delta is None else
                delta["max_relative_load_change"],
            "quantity_change": None if delta is None else
                delta["max_relative_quantity_change"],
            "alpha_used": alpha_used,
            "proposal_oracles_used": proposals,
            "joint_five_thresholds_met": stable,
            "consecutive_rechecks": streak,
            "hybrid": hybrid,
        })
        if step == steps:
            break

        proposal, next_obs, delta, calls, alpha = R29.step_forward(
            net, flows, obs, settings, policy, step + 1)
        if calls < 1:
            raise AssertionError("Free R14 proposal")
        proposals += calls
        old_obs = obs
        flows, obs, alpha_used = proposal, next_obs, alpha

    if audits[-1]["step"] != steps:
        raise AssertionError("Final F084F audit missing")
    summary = aggregate_group_counts(rows)
    return {
        "variant": variant, "policy": policy,
        "fixture": meta, "nodes": 10*cfg.m*cfg.m+2,
        "groups": len(net.groups), "steps": steps,
        "initial_gap": rows[0]["global_gap"],
        "final_gap": rows[-1]["global_gap"],
        "final_max_group_gap": rows[-1]["max_group_gap"],
        "final_max_load_over_capacity": rows[-1]["max_load_over_capacity"],
        "peak_load_over_capacity": max(
            r["max_load_over_capacity"] for r in rows),
        "final_flow_sha256": rows[-1]["flow_sha256"],
        "initial_flow_sha256": rows[0]["flow_sha256"],
        "first_three_joint_stable_step": first_stable,
        "proposal_oracle_calls": proposals,
        "additional_hybrid_oracle_calls": 0,
        "independent_shortest_path_scans": scans,
        "summary": summary, "rows": rows,
        "financial_audits": audits,
        "elapsed_ms_observational": round((perf_counter()-t0)*1000,3),
        "causal_inference_proven": False,
        "global_equilibrium_proven": False,
        "f059_financial_ties_ratified": False,
        "production_approved": False,
    }


def cross_fixture_summary(results):
    """Equal-weight per-fixture overview plus event-weighted descriptive rollup.

    If one scenario has no switches, its fraction is NULL, not zero.
    Common random seeds and repeated policies imply no i.i.d. claim.
    """
    rows = [entry for entry in results if entry["summary"]["market_switches"] > 0]
    total = sum(r["summary"]["market_switches"] for r in rows)
    price = sum(r["summary"]["price_only_winner_is_actual"] for r in rows)
    transport = sum(r["summary"]["transport_only_winner_is_actual"] for r in rows)
    return {
        "fixture_policy_runs": len(results),
        "runs_with_market_switches": len(rows),
        "runs_without_market_switches": len(results) - len(rows),
        "total_market_switch_events": total,
        "event_weighted_price_only_full_winner_share": (
            price/total if total else None),
        "event_weighted_transport_only_full_winner_share": (
            transport/total if total else None),
        "unweighted_mean_of_run_price_only_winner_share": (
            sum(r["summary"]["price_only_full_new_winner_share"]
                for r in rows)/len(rows) if rows else None),
        "unweighted_mean_of_run_transport_only_winner_share": (
            sum(r["summary"]["transport_only_full_new_winner_share"]
                for r in rows)/len(rows) if rows else None),
        "minimum_nonempty_run_price_only_full_winner_share": (
            min(r["summary"]["price_only_full_new_winner_share"]
                for r in rows) if rows else None),
        "all_runs_share_synthetic_generator_not_independent_samples": True,
        "observational_only": True,
    }


def exercise(profile):
    plans = {
        "smoke": (("small", 6),),
        "ci": (("small", 48), ("medium", 24)),
        "extended": (("small", 96), ("medium", 48), ("large", 12)),
    }
    if profile not in plans:
        raise ValueError("Unsupported profile")
    selected = ("baseline", "demand_mosaic", "capacity_quarter") \
        if profile == "smoke" else VARIANTS
    records = []
    matrix = []
    for label, steps in plans[profile]:
        for variant in selected:
            policies = []
            for policy in POLICIES:
                row = run_case(R21.CASES[label], variant, policy, steps)
                records.append(row)
                s = row["summary"]
                print(
                    "[{}:{}:{}] gap={:.7g} worst={:.7g} "
                    "switches={} priceWinner={} transportWinner={} "
                    "priceDominated={} peakLoad={:.5g} "
                    "oracles={} scans={} stable={}".format(
                        label, variant, policy, row["final_gap"],
                        row["final_max_group_gap"], s["market_switches"],
                        s["price_only_winner_is_actual"],
                        s["transport_only_winner_is_actual"],
                        s["price_log_effect_dominant"],
                        row["peak_load_over_capacity"],
                        row["proposal_oracle_calls"],
                        row["independent_shortest_path_scans"],
                        row["first_three_joint_stable_step"]), flush=True)
                policies.append(row)
            matrix.append({"case": label, "variant": variant, "policies": policies})

    baseline = run_case(R21.CASES["small"], "baseline", "decay_sqrt", 5,
                        audit_every=2)
    original = R32.run_case(R21.CASES["small"], "decay_sqrt", 5, audit_every=2)
    if (baseline["final_flow_sha256"] != original["final_flow_sha256"] or
        baseline["final_gap"] != original["final_gap"] or
        baseline["proposal_oracle_calls"] != original["proposal_oracles_used"] or
        baseline["summary"]["market_switches"] !=
            original["summary"]["market_switches"]):
        raise AssertionError("Baseline R32 frozen trajectory changed")
    for policy in ("decay_sqrt", "backtracking"):
        R24.check_r21_reference(R21.CASES["small"], policy, 6)
    return {
        "cases": matrix, "aggregate": cross_fixture_summary(records),
        "baseline_r32_replay": True, "r21_replay": True,
        "r20_f072_immutable": True,
        "variant_count": len(selected),
        "reference_policies": list(POLICIES),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke","ci","extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R33_EXOGENOUS_FIXTURE_ROBUSTNESS_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "all_hybrids_are_nonclearing_score_comparisons": True,
        "causality_proven": False, "global_equilibrium_proven": False,
        "financial_F059_ratified": False, "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("Frozen R20 manifest changed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_VARIANT_DIAGNOSTIC_CONTRACTS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R33] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = args.output / ("GA-Gate09-R33-" + args.profile + "-" + stamp + ".json")
    path.write_text(json.dumps(report, sort_keys=True, ensure_ascii=False,
                               indent=2) + "\n", encoding="utf-8")
    zip_path = args.output / ("GA-Gate09-R33-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(path, path.name)
    print("[R33] STATUS:", report["status"], flush=True)
    print("[R33] EVIDENCE:", zip_path, flush=True)
    return 0 if report["status"] == "PASSED_VARIANT_DIAGNOSTIC_CONTRACTS_ONLY" else 1


if __name__ == "__main__":
    raise SystemExit(main())
