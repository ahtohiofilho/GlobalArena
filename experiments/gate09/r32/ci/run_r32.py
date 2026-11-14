#!/usr/bin/env python3
"""GA Gate09 R32: frozen-quote/transport hybrid best-market rankings.

OBSERVATIONAL score accounting. Re-rank every producer's possible markets
with (old/new price) X (old/new shortest transport costs), without applying
mixed quotes, changing flows or altering any F-072 price or route rule.
Hybrids are deliberately NOT self-consistent market-clearing states.
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
sys.path.insert(0, str(GATE09 / "r31" / "ci"))
import run_r31 as R31

R30 = R31.R30
R29 = R31.R29
R21 = R31.R21
R24 = R31.R24
R26 = R31.R26
from clearing_r5 import ClearingSettings, _flow_checksum
from gate09 import Column
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14

POLICIES = R31.POLICIES
EPS = 1e-11


def _near(a, b, tol=EPS):
    return math.isclose(a, b, rel_tol=0.0, abs_tol=tol)


def _classify_pair(price_crosses, transport_crosses):
    if price_crosses and transport_crosses:
        return "both_individual_cross"
    if price_crosses:
        return "price_only_cross"
    if transport_crosses:
        return "transport_only_cross"
    return "neither_individual_cross"


def frozen_hybrid_rankings(net, before, after, exponent):
    """Compute all FOUR rankings using unchanged F072 scores for each group.

    Baseline OLD = old prices/old paths; actual NEW = new prices/new paths;
    frozen-transport PRICE-ONLY = new prices/old paths;
    frozen-price TRANSPORT-ONLY = old prices/new paths.

    No extra evaluate_r14 oracle is invoked: original prices, loads,
    quantities and best-path target are taken read-only from fully
    repriced snapshots. We charge independent shortest-path scans.
    """
    if exponent not in (3, 4, 5) or set(before["target"]) != set(after["target"]):
        raise ValueError("Invalid exponent or producer domain")
    old_cache = FrozenRouteCache(net, before["loads"], exponent)
    new_cache = FrozenRouteCache(net, after["loads"], exponent)
    groups = {}
    counts = {
        "producer_groups": len(net.groups),
        "market_switches": 0,
        "market_unchanged": 0,
        "pair_both_individual_cross": 0,
        "pair_price_only_cross": 0,
        "pair_transport_only_cross": 0,
        "pair_neither_individual_cross": 0,
        "price_frozen_winner_is_actual": 0,
        "transport_frozen_winner_is_actual": 0,
        "price_only_winner_is_actual": 0,
        "transport_only_winner_is_actual": 0,
        "price_only_winner_stays_old": 0,
        "transport_only_winner_stays_old": 0,
        "full_winner_price_only_new_market": 0,
        "full_winner_transport_only_new_market": 0,
        "quantity_price_identity_verified": 0,
        "price_log_effect_dominant": 0,
        "transport_log_effect_dominant": 0,
    }
    for gid, group in sorted(net.groups.items()):
        dist_old, paths_old, _ = old_cache.paths(group.origin, group.seller)
        dist_new, paths_new, _ = new_cache.paths(group.origin, group.seller)
        options = {
            "old_actual": [],
            "price_only": [],
            "transport_only": [],
            "new_actual": [],
        }
        score_by_market = {}
        for market in sorted(net.markets):
            if market[1] != group.commodity:
                continue
            destination = market[0]
            if destination not in dist_old or destination not in dist_new:
                raise AssertionError("Frozen reachability changed")
            p_old, p_new = before["prices"][market], after["prices"][market]
            q_old, q_new = before["quantities"][market], after["quantities"][market]
            if any(not math.isfinite(v) or v <= 0 for v in
                   (p_old, p_new, q_old, q_new)):
                raise AssertionError("Undefined positive price or market quantity")
            c_old, c_new = dist_old[destination], dist_new[destination]
            scores = {
                "old_actual": p_old / (1 + c_old),
                "price_only": p_new / (1 + c_old),
                "transport_only": p_old / (1 + c_new),
                "new_actual": p_new / (1 + c_new),
            }
            score_by_market[destination] = scores
            for variant, score in scores.items():
                path = paths_new[destination] if variant in (
                    "transport_only", "new_actual") else paths_old[destination]
                options[variant].append(
                    (score, Column(gid, destination, path)))

        ordered = {}
        for variant, available in options.items():
            available.sort(key=lambda item: (-item[0], item[1]))
            ordered[variant] = available
        old_column = ordered["old_actual"][0][1]
        new_column = ordered["new_actual"][0][1]
        if old_column != before["target"][gid] or new_column != after["target"][gid]:
            raise AssertionError("Hybrid reference rank does not replay R14")
        if (ordered["old_actual"][0][0] != before["groups"][gid]["score"] or
                ordered["new_actual"][0][0] != after["groups"][gid]["score"]):
            raise AssertionError("Hybrid score diverges from R14")

        prev_dest, next_dest = old_column.destination, new_column.destination
        ranking_winners = {
            name: records[0][1].destination
            for name, records in ordered.items()
        }
        row = {
            "group": gid,
            "old_market": prev_dest,
            "new_market": next_dest,
            "is_market_switch": prev_dest != next_dest,
            "four_market_winners": ranking_winners,
            "frozen_price_counterfactual_winner_equals_actual": (
                ranking_winners["transport_only"] == next_dest),
            "frozen_transport_counterfactual_winner_equals_actual": (
                ranking_winners["price_only"] == next_dest),
            "mixed_rankings_are_not_clearing_states": True,
        }
        counts["price_frozen_winner_is_actual"] += int(
            ranking_winners["transport_only"] == next_dest)
        counts["transport_frozen_winner_is_actual"] += int(
            ranking_winners["price_only"] == next_dest)
        if prev_dest == next_dest:
            counts["market_unchanged"] += 1
            groups[gid] = row
            continue

        counts["market_switches"] += 1
        old_score_A = score_by_market[prev_dest]["old_actual"]
        old_score_B = score_by_market[next_dest]["old_actual"]
        new_score_A = score_by_market[prev_dest]["new_actual"]
        new_score_B = score_by_market[next_dest]["new_actual"]
        price_score_A = score_by_market[prev_dest]["price_only"]
        price_score_B = score_by_market[next_dest]["price_only"]
        cost_score_A = score_by_market[prev_dest]["transport_only"]
        cost_score_B = score_by_market[next_dest]["transport_only"]

        l0 = math.log(old_score_B / old_score_A)
        l1 = math.log(new_score_B / new_score_A)
        lp = math.log(price_score_B / price_score_A)
        lc = math.log(cost_score_B / cost_score_A)
        # L_priceOnly = L_old + dPrice; L_transportOnly = L_old + dCost.
        delta_price = lp - l0
        delta_transport = lc - l0
        if not _near(l1 - l0, delta_price + delta_transport):
            raise AssertionError("Hybrid rankings fail exact log decomposition")
        if l0 > EPS or l1 < -EPS:
            raise AssertionError("Reference market winner is not pairwise best")

        # P=sqrt(D/Q), D constant: exact additive relative-log-price identity.
        qB_before = before["quantities"][(next_dest, group.commodity)]
        qB_after = after["quantities"][(next_dest, group.commodity)]
        qA_before = before["quantities"][(prev_dest, group.commodity)]
        qA_after = after["quantities"][(prev_dest, group.commodity)]
        term_B = -0.5 * math.log(qB_after / qB_before)
        term_A = +0.5 * math.log(qA_after / qA_before)
        if not _near(delta_price, term_A + term_B):
            raise AssertionError("Frozen quantity-to-price identity failed")
        counts["quantity_price_identity_verified"] += 1

        price_pair_crosses = lp >= -EPS
        transport_pair_crosses = lc >= -EPS
        label = _classify_pair(price_pair_crosses, transport_pair_crosses)
        names = {
            "both_individual_cross": "pair_both_individual_cross",
            "price_only_cross": "pair_price_only_cross",
            "transport_only_cross": "pair_transport_only_cross",
            "neither_individual_cross": "pair_neither_individual_cross",
        }
        counts[names[label]] += 1
        counts["price_only_winner_is_actual"] += int(
            ranking_winners["price_only"] == next_dest)
        counts["transport_only_winner_is_actual"] += int(
            ranking_winners["transport_only"] == next_dest)
        counts["price_only_winner_stays_old"] += int(
            ranking_winners["price_only"] == prev_dest)
        counts["transport_only_winner_stays_old"] += int(
            ranking_winners["transport_only"] == prev_dest)
        counts["full_winner_price_only_new_market"] += int(
            ranking_winners["price_only"] == next_dest)
        counts["full_winner_transport_only_new_market"] += int(
            ranking_winners["transport_only"] == next_dest)
        is_price_dominant = abs(delta_price) >= abs(delta_transport)
        counts["price_log_effect_dominant"] += int(is_price_dominant)
        counts["transport_log_effect_dominant"] += int(not is_price_dominant)
        row.update({
            "log_advantage_B_over_A_old": l0,
            "log_advantage_B_over_A_new": l1,
            "log_advantage_price_only": lp,
            "log_advantage_transport_only": lc,
            "delta_log_advantage": l1 - l0,
            "delta_log_relative_price": delta_price,
            "delta_log_relative_transport": delta_transport,
            "quantity_effect_old_market_A": term_A,
            "quantity_effect_new_market_B": term_B,
            "quantity_price_identity_closes": True,
            "pair_price_only_B_over_A": price_pair_crosses,
            "pair_transport_only_B_over_A": transport_pair_crosses,
            "pair_crossing_class": label,
            "price_component_has_greater_absolute_magnitude": is_price_dominant,
            "possible_third_market_in_full_counterfactual": (
                ranking_winners["price_only"] not in (prev_dest, next_dest) or
                ranking_winners["transport_only"] not in (prev_dest, next_dest)),
        })
        groups[gid] = row

    if counts["market_switches"] + counts["market_unchanged"] != len(net.groups):
        raise AssertionError("Group partition mismatch")
    if sum(counts[k] for k in (
            "pair_both_individual_cross", "pair_price_only_cross",
            "pair_transport_only_cross", "pair_neither_individual_cross")) != (
            counts["market_switches"]):
        raise AssertionError("Incomplete pair-hybrid partition")
    if counts["price_log_effect_dominant"] + counts[
            "transport_log_effect_dominant"] != counts["market_switches"]:
        raise AssertionError("Incomplete magnitude classification")
    scans = (old_cache.counters()["shortest_path_scans"] +
             new_cache.counters()["shortest_path_scans"])
    return {
        "counts": counts,
        "groups": groups,
        "independent_shortest_path_scans": scans,
        "extra_r14_oracle_calls": 0,
        "full_market_ranking_recomputed": True,
        "only_read_existing_frozen_quotes_and_loads": True,
        "mixed_quotes_are_not_economic_clearing_states": True,
        "causality_proven": False,
    }


def aggregate(rows):
    later = [row["hybrid"] for row in rows[1:]]
    keys = later[0]["counts"].keys() if later else ()
    totals = {k: sum(x["counts"][k] for x in later) for k in keys}
    market_switches = totals.get("market_switches", 0)
    return {
        **totals,
        "fraction_switches_pair_price_only_cross": (
            (totals["pair_price_only_cross"] +
             totals["pair_both_individual_cross"]) / market_switches
            if market_switches else None),
        "fraction_switches_pair_transport_only_cross": (
            (totals["pair_transport_only_cross"] +
             totals["pair_both_individual_cross"]) / market_switches
            if market_switches else None),
        "fraction_switches_full_price_only_winner_actual": (
            totals["price_only_winner_is_actual"] / market_switches
            if market_switches else None),
        "fraction_switches_full_transport_only_winner_actual": (
            totals["transport_only_winner_is_actual"] / market_switches
            if market_switches else None),
        "these_are_score_hybrids_not_market_equilibria": True,
    }


def run_case(cfg, policy, steps, audit_every=16):
    if policy not in POLICIES or type(steps) is not int or not 1 <= steps <= 256:
        raise ValueError("Invalid policy or steps")
    if type(audit_every) is not int or audit_every < 1:
        raise ValueError("Invalid audit interval")
    net, flows = construct_scenario(cfg)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    previous = None
    last_delta = None
    last_alpha = None
    proposals = 1
    scans = 0
    audited = []
    rows = []
    streak = 0
    stable_first = None
    start = perf_counter()

    for step in range(steps + 1):
        if step % audit_every == 0 or step == steps:
            R21.frozen_check(net, flows, obs)
            financial = R21.settlement_check(net, flows)
            audited.append({"step": step, **financial})
        if obs["reachable_unpriced"] or any(
                not math.isfinite(v) or v <= 0 for v in obs["prices"].values()):
            raise AssertionError("Invalid market quote")
        stability = R24.numerical_pass(obs, last_delta)
        streak = streak + 1 if stability else 0
        if streak >= 3 and stable_first is None:
            stable_first = step
        hybrid = (frozen_hybrid_rankings(net, previous, obs, settings.exponent)
                  if previous is not None else None)
        if hybrid:
            scans += hybrid["independent_shortest_path_scans"]
        rows.append({
            "step": step, "flow_sha256": _flow_checksum(flows),
            "target_sha256": R29.target_signature(obs["target"]),
            "relative_gap": obs["relative_gap"],
            "max_group_gap": obs["max_group_gap"],
            "price_change": None if last_delta is None else
                last_delta["max_relative_price_change"],
            "load_change": None if last_delta is None else
                last_delta["max_relative_load_change"],
            "quantity_change": None if last_delta is None else
                last_delta["max_relative_quantity_change"],
            "alpha_used": last_alpha,
            "hybrid": hybrid,
            "joint_five_numerical_checks_met": stability,
            "consecutive_stable_rechecks": streak,
            "proposal_oracles_used": proposals,
        })
        if step == steps:
            break
        new_flow, new_obs, delta, calls, alpha = R29.step_forward(
            net, flows, obs, settings, policy, step + 1)
        if calls < 1:
            raise AssertionError("Lost oracle trial evaluations")
        proposals += calls
        previous = obs
        flows, obs, last_delta, last_alpha = new_flow, new_obs, delta, alpha

    if audited[-1]["step"] != steps or proposals < steps+1:
        raise AssertionError("Missing final financial audit or oracle calls")
    return {
        "policy": policy, "nodes": 10*cfg.m*cfg.m+2,
        "producer_groups": len(net.groups), "steps": steps,
        "initial_gap": rows[0]["relative_gap"],
        "final_gap": rows[-1]["relative_gap"],
        "final_max_group_gap": rows[-1]["max_group_gap"],
        "final_flow_sha256": rows[-1]["flow_sha256"],
        "first_three_joint_stable_step": stable_first,
        "proposal_oracles_used": proposals,
        "algebraic_hybrid_additional_oracle_calls": 0,
        "independent_shortest_path_scans": scans,
        "financial_audits": audited, "rows": rows,
        "summary": aggregate(rows),
        "elapsed_ms_observational": round((perf_counter() - start)*1000,3),
        "causality_proven": False,
        "f059_ratified": False, "global_equilibrium_proven": False,
        "production_approved": False,
    }


def exercise(profile):
    plans = {
        "smoke": (("small", 8),),
        "ci": (("small", 96), ("medium", 48)),
        "extended": (("small", 160), ("medium", 96), ("large", 24)),
    }
    if profile not in plans:
        raise ValueError("Invalid profile")
    cases = []
    for label, steps in plans[profile]:
        policies = []
        for policy in POLICIES:
            result = run_case(R21.CASES[label], policy, steps)
            s = result["summary"]
            print(
                "[{}:{}] gap={:.7g} worst={:.7g} "
                "switches={} priceOnlyPair={} costOnlyPair={} bothPair={} "
                "neitherPair={} fullPriceOnlyActual={} fullCostOnlyActual={} "
                "priceDominated={} extraR14={} scans={} stable={}".format(
                    label, policy, result["final_gap"],
                    result["final_max_group_gap"],
                    s["market_switches"],
                    s["pair_price_only_cross"],
                    s["pair_transport_only_cross"],
                    s["pair_both_individual_cross"],
                    s["pair_neither_individual_cross"],
                    s["price_only_winner_is_actual"],
                    s["transport_only_winner_is_actual"],
                    s["price_log_effect_dominant"],
                    result["algebraic_hybrid_additional_oracle_calls"],
                    result["independent_shortest_path_scans"],
                    result["first_three_joint_stable_step"]), flush=True)
            policies.append(result)
        cases.append({"case": label, "policies": policies})

    for policy in ("decay_sqrt", "backtracking"):
        R24.check_r21_reference(R21.CASES["small"], policy, 6)
    small = run_case(R21.CASES["small"], "decay_sqrt", 4, audit_every=2)
    long = run_case(R21.CASES["small"], "decay_sqrt", 7, audit_every=2)
    if small["rows"] != long["rows"][:5]:
        raise AssertionError("Counterfactual diagnostic prefix drift")
    return {
        "comparisons": cases, "r21_replay": True,
        "hybrid_prefix_determinism": True,
        "original_r20_f072_immutable": True,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke","ci","extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R32_FROZEN_QUOTE_AND_TRANSPORT_HYBRID_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "hybrid_rankings_are_not_clearing_states": True,
        "causality_proven": False, "global_equilibrium_proven": False,
        "financial_F059_ratified": False, "production_approved": False,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("Frozen upstream manifest changed")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_DIAGNOSTIC_CONTRACTS_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R32] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = args.output / ("GA-Gate09-R32-" + args.profile + "-" + stamp + ".json")
    path.write_text(json.dumps(report, indent=2, sort_keys=True,
                               ensure_ascii=False) + "\n", encoding="utf-8")
    z = args.output / ("GA-Gate09-R32-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(z,"w",zipfile.ZIP_DEFLATED) as archive:
        archive.write(path,path.name)
    print("[R32] STATUS:", report["status"], flush=True)
    print("[R32] EVIDENCE:", z, flush=True)
    return 0 if report["status"] == "PASSED_DIAGNOSTIC_CONTRACTS_ONLY" else 1


if __name__=="__main__":
    raise SystemExit(main())
