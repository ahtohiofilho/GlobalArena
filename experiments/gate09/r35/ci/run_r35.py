#!/usr/bin/env python3
"""GA Gate 09 R35 - local conservative flow-directional sensitivity.

At F072 frozen snapshots, add a small INTERIOR allocation to a second
destination for one producer, then make symmetric, zero-sum transfers
between two concrete columns. Compare centered finite differences of
log market price and generalized shortest-path costs to analytic slopes.
All evaluations are separate, costed R14 oracle calls, never committed
to the trajectory. This is a differential diagnostic, not an economic
policy, convergence proof, or causal experiment.
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
sys.path.insert(0, str(GATE09 / "r34" / "ci"))
import run_r34 as R34

R33 = R34.R33
R32 = R34.R32
R31 = R32.R31
R29 = R34.R29
R21 = R34.R21
R24 = R34.R24
from clearing_r5 import ClearingSettings, _flow_checksum
from gate09 import Column
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14

FACTORS = (0.15, 0.25, 0.40, 0.50, 1.0, 4.0)
REGIMES = R34.REGIMES
POLICIES = ("decay_sqrt", "backtracking")
EPS_FRACTIONS = (1/16, 1/8)
GROUPS_PER_CHECKPOINT = 2
PRICE_SLOPE_TOL = 0.002
TRANSPORT_SLOPE_TOL = 0.025


def transfer(net, flows, source, destination, amount):
    """Move signed physical quantity between columns of THE SAME producer.

    Source and destination may include different fixed routes. This is
    only a hypothetical flow vector, never a production/demand increase.
    """
    if (source == destination or source.group_id != destination.group_id or
        not math.isfinite(amount)):
        raise ValueError("Invalid conservative allocation transfer")
    if source not in flows or flows[source] <= 0:
        raise ValueError("Positive source needed")
    out = dict(flows)
    out[source] -= amount
    out[destination] = out.get(destination, 0.) + amount
    if out[source] < 0 or out[destination] < 0:
        raise ValueError("Perturbation would create negative allocation")
    group = net.groups[source.group_id]
    cols = sorted(c for c in out if c.group_id == group.id)
    correction = group.supply - sum(out[c] for c in cols)
    out[source] += correction
    if out[source] < 0:
        raise AssertionError("Floating correction made negative flow")
    net.snapshots(out)
    return out


def select_directions(net, flows, observed, limit=GROUPS_PER_CHECKPOINT):
    """Deterministic high-regret groups, with a distinct reachable destination.

    This is targeted diagnostic sampling, NOT random or representative.
    The source is the largest already-funded column, while the sink
    uses the F072 canonical route to an alternative market.
    """
    if type(limit) is not int or limit < 1:
        raise ValueError("Bad diagnostic group limit")
    cache = FrozenRouteCache(net, observed["loads"], 4)
    ranked = sorted(net.groups, key=lambda gid:
                    (-observed["groups"][gid]["gap"], gid))
    choices = []
    for gid in ranked:
        owned = sorted(
            ((c, amount) for c, amount in flows.items()
             if c.group_id == gid and amount > 0),
            key=lambda x: (-x[1], x[0]))
        if not owned:
            raise AssertionError("Producer has no allocated supply")
        source, amount = owned[0]
        g = net.groups[gid]
        distances, paths, _ = cache.paths(g.origin, g.seller)
        alternatives = []
        for market in sorted(net.markets):
            if (market[1] != g.commodity or market[0] == source.destination
                    or market[0] not in paths):
                continue
            col = Column(gid, market[0], paths[market[0]])
            net.validate_column(col)
            score = observed["prices"][market] / (1 + distances[market[0]])
            alternatives.append((score, col))
        if not alternatives:
            continue
        alternatives.sort(key=lambda v: (-v[0], v[1]))
        choices.append({
            "group": gid,
            "source": source,
            "sink": alternatives[0][1],
            "source_amount": amount,
            "chosen_from_highest_regret": True,
        })
        if len(choices) >= limit:
            break
    return choices, cache.counters()["shortest_path_scans"]


def _edge_incidence(source, sink):
    result = {}
    for eid in source.edges:
        result[eid] = result.get(eid, 0) - 1
    for eid in sink.edges:
        result[eid] = result.get(eid, 0) + 1
    return result


def local_analytic_slopes(net, observation, source, sink, exponent, cache):
    """Analytic derivative along fixed SOURCE -> SINK amount direction.

    Market prices: derivative log(P_B/P_A) = -.5*(1/Q_B+1/Q_A).
    Edge cost: c_free*(1+(B/K)^p), derivative in amount direction
       c_free*p*(B/K)^(p-1)*(dB/damount)/K.
    Tariff is independent of flow, so its derivative is zero.
    Apply edge derivative to the CURRENT canonical paths to A/B;
    at a path switch this formula is only a one-sided branch slope.
    """
    g = net.groups[source.group_id]
    if sink.group_id != source.group_id or source.destination == sink.destination:
        raise ValueError("Local cross-market direction required")
    key_a = (source.destination, g.commodity)
    key_b = (sink.destination, g.commodity)
    qa = observation["quantities"][key_a]
    qb = observation["quantities"][key_b]
    price = -0.5*(1/qa + 1/qb)
    incidence = _edge_incidence(source, sink)
    dweight = {}
    for eid, edge in sorted(net.edges.items()):
        load = observation["loads"].get(eid, 0.)
        dweight[eid] = (
            edge.c_free * exponent * (load/edge.capacity)**(exponent-1)
            * incidence.get(eid, 0)/edge.capacity)
    dist, paths, _ = cache.paths(g.origin, g.seller)
    cost_a, cost_b = dist[source.destination], dist[sink.destination]
    dca = sum(dweight[e] for e in paths[source.destination])
    dcb = sum(dweight[e] for e in paths[sink.destination])
    transport = -dcb/(1+cost_b) + dca/(1+cost_a)
    return {
        "price": price,
        "transport": transport,
        "total_score": price+transport,
        "d_cost_old_market": dca,
        "d_cost_new_market": dcb,
        "max_absolute_load_over_capacity_per_unit": max(
            (abs(incidence.get(eid,0))/edge.capacity
             for eid, edge in net.edges.items()), default=0),
        "path_old": list(paths[source.destination]),
        "path_new": list(paths[sink.destination]),
        "source_sink_edge_incidence": [
            {"edge_id": eid, "signed_load_change_per_unit": sign}
            for eid, sign in sorted(incidence.items()) if sign],
    }


def _pair(net, observation, gid, source, sink, exponent):
    cache = FrozenRouteCache(net, observation["loads"], exponent)
    snapshot = R31.market_pair_at_snapshot(
        net, observation, cache, gid, source.destination, sink.destination)
    return snapshot, cache.counters()["shortest_path_scans"]


def _difference(values_plus, values_minus, epsilon, key):
    return (values_plus[key] - values_minus[key])/(2*epsilon)


def inspect_direction(net, original_flows, original_obs,
                      settings, direction):
    """One interior anchor and two symmetric widths, five paid R14 calls.

    A source with positive mass supports the anchor. The small initial
    transfer to sink makes BOTH signed perturbations feasible, so
    central differences are not evaluated at a Q=0 boundary.
    """
    source, sink = direction["source"], direction["sink"]
    group = net.groups[source.group_id]
    if source.destination == sink.destination:
        raise AssertionError("Diagnostic must affect two prices")
    initial_hash = _flow_checksum(original_flows)
    anchor_shift = min(0.02*group.supply, 0.1*direction["source_amount"])
    if anchor_shift <= 0:
        raise AssertionError("No positive interior anchor")
    anchor = transfer(net, original_flows, source, sink, anchor_shift)
    anchor_observation = evaluate_r14(net, anchor, settings, ())
    if anchor_observation["reachable_unpriced"]:
        raise AssertionError("Unpriced anchor market")
    R33.verify_price_law(net, anchor_observation)
    anchor_pair, scans = _pair(net, anchor_observation,
                              group.id, source, sink, settings.exponent)
    center_cache = FrozenRouteCache(
        net, anchor_observation["loads"], settings.exponent)
    analytic = local_analytic_slopes(
        net, anchor_observation, source, sink, settings.exponent, center_cache)
    scans += center_cache.counters()["shortest_path_scans"]
    anchor_hash = _flow_checksum(anchor)
    samples = []
    price_errors, transport_errors = [], []
    kink_count = 0
    probes = 1
    for fraction in EPS_FRACTIONS:
        epsilon = anchor_shift*fraction
        if epsilon <= 0:
            raise AssertionError("Nonpositive finite difference width")
        plus = transfer(net, anchor, source, sink, epsilon)
        minus = transfer(net, anchor, source, sink, -epsilon)
        positive = evaluate_r14(net, plus, settings, ())
        negative = evaluate_r14(net, minus, settings, ())
        probes += 2
        R33.verify_price_law(net, positive)
        R33.verify_price_law(net, negative)
        pos, count1 = _pair(net, positive, group.id, source, sink,
                             settings.exponent)
        neg, count2 = _pair(net, negative, group.id, source, sink,
                             settings.exponent)
        scans += count1 + count2
        fd_price = _difference(
            pos, neg, epsilon, "log_relative_price_new_over_old")
        fd_transport = -_difference(
            pos, neg, epsilon, "log_relative_transport_new_over_old")
        fd_total = _difference(
            pos, neg, epsilon, "log_advantage_new_over_old")
        if not math.isclose(
                fd_total, fd_price+fd_transport,
                rel_tol=0, abs_tol=1e-9*max(1,abs(fd_total))):
            raise AssertionError("Finite difference total does not decompose")
        # Positive and negative perturbations must keep the same SELLABLE
        # production and fully priced markets; no new demand or buyer.
        plus_sources = sum(v for c,v in plus.items() if c.group_id==group.id)
        minus_sources = sum(v for c,v in minus.items() if c.group_id==group.id)
        if (abs(plus_sources-group.supply)>1e-9 or
            abs(minus_sources-group.supply)>1e-9):
            raise AssertionError("Production conservation violated")
        price_error = abs(fd_price-analytic["price"])
        price_errors.append(price_error)
        if price_error > PRICE_SLOPE_TOL*max(1,abs(analytic["price"])):
            raise AssertionError("Frozen sqrt(D/Q) slope diverged")
        path_changed = (
            pos["old_path"] != analytic["path_old"] or
            pos["new_path"] != analytic["path_new"] or
            neg["old_path"] != analytic["path_old"] or
            neg["new_path"] != analytic["path_new"])
        winner_changed = len({
            anchor_observation["target"][group.id].destination,
            positive["target"][group.id].destination,
            negative["target"][group.id].destination}) > 1
        if path_changed:
            kink_count += 1
        error = abs(fd_transport-analytic["transport"])
        if not path_changed:
            transport_errors.append(error)
            if error > TRANSPORT_SLOPE_TOL*max(1,abs(analytic["transport"])):
                raise AssertionError("Smooth congestion-cost slope diverged")
        samples.append({
            "epsilon_fraction_of_interior_shift": fraction,
            "epsilon_quantity": epsilon,
            "two_extra_r14_oracle_calls": 2,
            "plus_flow_sha256": _flow_checksum(plus),
            "minus_flow_sha256": _flow_checksum(minus),
            "centered_fd_log_relative_price": fd_price,
            "centered_fd_log_relative_transport_score": fd_transport,
            "centered_fd_log_relative_market_score": fd_total,
            "abs_price_slope_error": price_error,
            "abs_transport_slope_error": error,
            "canonical_market_path_changed": path_changed,
            "target_market_changed_between_probes": winner_changed,
            "transport_derivative_not_certified_if_path_changed": path_changed,
            "quantity_price_identity_unchanged": True,
        })
    if (_flow_checksum(original_flows) != initial_hash or
        _flow_checksum(anchor) != anchor_hash):
        raise AssertionError("Temporary probes mutated the frozen source")
    return {
        "group": group.id, "commodity": group.commodity,
        "from_market": source.destination, "to_market": sink.destination,
        "source_column_edges": list(source.edges),
        "sink_column_edges": list(sink.edges),
        "source_positive_mass_at_original_snapshot": direction["source_amount"],
        "interior_anchor_mass_shift": anchor_shift,
        "original_flow_sha256": initial_hash,
        "anchor_flow_sha256": anchor_hash,
        "anchor_price": anchor_pair["log_relative_price_new_over_old"],
        "anchor_score_advantage": anchor_pair[
            "log_advantage_new_over_old"],
        "analytic_slopes_at_anchor": analytic,
        "epsilons": samples,
        "additional_r14_oracle_calls": probes,
        "extra_shortest_path_scans": scans,
        "market_path_kink_observations": kink_count,
        "target_market_switch_probes": sum(
            int(s["target_market_changed_between_probes"]) for s in samples),
        "max_abs_price_slope_error": max(price_errors),
        "max_abs_smooth_transport_slope_error": (
            max(transport_errors) if transport_errors else None),
        "no_flow_committed_from_probes": True,
        "no_causal_attribution_proven": True,
    }


def inspect_checkpoint(net, flows, obs, settings, step):
    """Bounded selected producers, distinct from global equilibrium test."""
    source_hash = _flow_checksum(flows)
    directions, selection_scans = select_directions(
        net, flows, obs, GROUPS_PER_CHECKPOINT)
    witnesses = [inspect_direction(net, flows, obs, settings, d)
                 for d in directions]
    if _flow_checksum(flows) != source_hash:
        raise AssertionError("Probe changed the policy trajectory")
    return {
        "step": step, "selection_is_deterministic_high_regret_not_random": True,
        "source_flow_sha256": source_hash,
        "direction_count": len(witnesses),
        "paid_extra_r14_oracle_calls": sum(
            x["additional_r14_oracle_calls"] for x in witnesses),
        "independent_shortest_path_scans": selection_scans + sum(
            x["extra_shortest_path_scans"] for x in witnesses),
        "directions": witnesses,
    }


def run_case(cfg, regime, factor, policy, steps, audit_every=8):
    """Policy is read-only; probe at step 0 and final, never update from probes."""
    if (policy not in POLICIES or factor not in FACTORS or
        regime not in REGIMES or type(steps) is not int or
        not 1 <= steps <= 256):
        raise ValueError("Unsupported diagnostic run")
    net, flows, fixture = R34.capacity_fixture(cfg, regime, factor)
    settings = ClearingSettings(exponent=cfg.exponent)
    obs = evaluate_r14(net, flows, settings, ())
    last_delta = None
    policy_calls = 1
    probe_calls = 0
    scans = 0
    audits = []
    rows = []
    checkpoints = []
    streak = 0
    first_stable = None
    t0 = perf_counter()
    for step in range(steps+1):
        if step%audit_every==0 or step==steps:
            R21.frozen_check(net, flows, obs)
            settlement = R21.settlement_check(net, flows)
            audits.append({"step": step, **settlement})
        R33.verify_price_law(net, obs)
        stable = R24.numerical_pass(obs, last_delta)
        streak = streak+1 if stable else 0
        if streak >= 3 and first_stable is None:
            first_stable = step
        row = {
            "step": step,
            "flow_sha256": _flow_checksum(flows),
            "global_f072_gap": obs["relative_gap"],
            "worst_producer_gap": obs["max_group_gap"],
            "max_relative_price_change": (
                None if last_delta is None else
                last_delta["max_relative_price_change"]),
            "max_relative_load_change": (
                None if last_delta is None else
                last_delta["max_relative_load_change"]),
            "max_relative_quantity_change": (
                None if last_delta is None else
                last_delta["max_relative_quantity_change"]),
            "numerical_five_thresholds_met": stable,
            "consecutive_joint_thresholds_met": streak,
            "policy_r14_calls_cumulative": policy_calls,
        }
        rows.append(row)
        if step in (0, steps):
            checked = inspect_checkpoint(net, flows, obs, settings, step)
            checkpoints.append(checked)
            probe_calls += checked["paid_extra_r14_oracle_calls"]
            scans += checked["independent_shortest_path_scans"]
        if step == steps:
            break
        new_flows, new_obs, last_delta, calls, alpha = R29.step_forward(
            net, flows, obs, settings, policy, step+1)
        if calls < 1:
            raise AssertionError("Uncharged solver proposal")
        policy_calls += calls
        flows, obs = new_flows, new_obs
    if audits[-1]["step"] != steps:
        raise AssertionError("Missing final financial audit")
    if probe_calls != sum(x["paid_extra_r14_oracle_calls"]
                          for x in checkpoints):
        raise AssertionError("Inconsistent oracle attribution")
    witnesses = [d for cp in checkpoints for d in cp["directions"]]
    return {
        "regime": regime, "capacity_factor": factor, "policy": policy,
        "nodes": 10*cfg.m*cfg.m+2, "steps": steps,
        "fixture": fixture,
        "initial_flow_sha256": rows[0]["flow_sha256"],
        "final_flow_sha256": rows[-1]["flow_sha256"],
        "final_gap": rows[-1]["global_f072_gap"],
        "final_worst_gap": rows[-1]["worst_producer_gap"],
        "first_three_joint_stable_step": first_stable,
        "policy_r14_calls": policy_calls,
        "conservative_probe_r14_calls": probe_calls,
        "total_r14_calls": policy_calls+probe_calls,
        "extra_shortest_path_scans": scans,
        "number_of_probed_directions": len(witnesses),
        "number_of_market_path_kink_observations": sum(
            x["market_path_kink_observations"] for x in witnesses),
        "number_of_target_market_switch_probes": sum(
            x["target_market_switch_probes"] for x in witnesses),
        "max_abs_price_slope_error": max(
            (x["max_abs_price_slope_error"] for x in witnesses), default=None),
        "max_abs_smooth_transport_slope_error": max(
            (x["max_abs_smooth_transport_slope_error"] for x in witnesses
             if x["max_abs_smooth_transport_slope_error"] is not None),
            default=None),
        "rows": rows, "checkpoints": checkpoints,
        "financial_audits": audits,
        "elapsed_ms_observational": round((perf_counter()-t0)*1000,3),
        "scope_selected_high_regret_groups_not_global": True,
        "economic_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "causality_proven": False,
        "production_approved": False,
    }


def aggregate(results):
    regimes = {}
    for regime in REGIMES:
        entries = [r for r in results if r["regime"] == regime]
        if not entries:
            continue
        by_factor = {}
        for factor in FACTORS:
            matches = [r for r in entries if r["capacity_factor"] == factor]
            if not matches:
                continue
            by_factor[str(factor)] = {
                "runs": len(matches),
                "directions": sum(x["number_of_probed_directions"]
                                  for x in matches),
                "path_kinks": sum(x["number_of_market_path_kink_observations"]
                                  for x in matches),
                "target_switch_probes": sum(x["number_of_target_market_switch_probes"]
                                            for x in matches),
                "max_abs_price_slope_error": max(
                    (x["max_abs_price_slope_error"] for x in matches
                     if x["max_abs_price_slope_error"] is not None), default=None),
                "max_abs_smooth_transport_slope_error": max(
                    (x["max_abs_smooth_transport_slope_error"] for x in matches
                     if x["max_abs_smooth_transport_slope_error"] is not None),
                    default=None),
                "analytical_directional_slope_is_not_a_global_derivative": True,
            }
        regimes[regime] = by_factor
    return {
        "total_runs": len(results),
        "total_probed_directions": sum(
            r["number_of_probed_directions"] for r in results),
        "total_smooth_or_kinked_subprobes": sum(
            r["number_of_probed_directions"]*len(EPS_FRACTIONS)
            for r in results),
        "total_path_kink_subprobes": sum(
            r["number_of_market_path_kink_observations"] for r in results),
        "total_target_switch_subprobes": sum(
            r["number_of_target_market_switch_probes"] for r in results),
        "policy_r14_calls": sum(r["policy_r14_calls"] for r in results),
        "probe_r14_calls": sum(r["conservative_probe_r14_calls"] for r in results),
        "independent_shortest_path_scans": sum(
            r["extra_shortest_path_scans"] for r in results),
        "regimes": regimes,
        "not_random_sampling_not_statistical_independence": True,
        "no_global_smoothness_conclusion": True,
    }


def exercise(profile):
    plans = {
        "smoke": (("small", 3),),
        "ci": (("small", 8), ("medium", 6)),
        "extended": (("small", 16), ("medium", 12), ("large", 4)),
    }
    if profile not in plans:
        raise ValueError("Unknown diagnostic profile")
    factors = FACTORS if profile != "smoke" else (0.25, 1.0)
    regimes = REGIMES if profile != "smoke" else ("original",)
    policies = POLICIES if profile != "smoke" else ("decay_sqrt",)
    results = []
    for case, steps in plans[profile]:
        for regime in regimes:
            for factor in factors:
                for policy in policies:
                    record = run_case(
                        R21.CASES[case], regime, factor, policy, steps)
                    results.append(record)
                    print(
                        "[{}:{}:Kx{}:{}] gap={:.7g} worst={:.7g} "
                        "directions={} kinks={} targetCross={} "
                        "policyR14={} probeR14={} scans={} stable={}".format(
                            case, regime, factor, policy,
                            record["final_gap"], record["final_worst_gap"],
                            record["number_of_probed_directions"],
                            record["number_of_market_path_kink_observations"],
                            record["number_of_target_market_switch_probes"],
                            record["policy_r14_calls"],
                            record["conservative_probe_r14_calls"],
                            record["extra_shortest_path_scans"],
                            record["first_three_joint_stable_step"]), flush=True)
    if profile != "smoke":
        baseline = run_case(
            R21.CASES["small"], "original", 1., "decay_sqrt", 5, audit_every=2)
        old = R34.run_case(
            R21.CASES["small"], "original", 1., "decay_sqrt", 5, audit_every=2)
        if (baseline["final_flow_sha256"] != old["final_flow_sha256"] or
            baseline["final_gap"] != old["final_global_gap"] or
            baseline["policy_r14_calls"] != old["policy_r14_calls"]):
            raise AssertionError("R34 frozen baseline changed by R35 probes")
        for policy in POLICIES:
            R24.check_r21_reference(R21.CASES["small"], policy, 6)
        short = run_case(
            R21.CASES["small"], "original", 0.25, "decay_sqrt",
            4, audit_every=2)
        long = run_case(
            R21.CASES["small"], "original", 0.25, "decay_sqrt",
            7, audit_every=2)
        if short["rows"] != long["rows"][:len(short["rows"])]:
            raise AssertionError("Diagnostic flow prefixes are not deterministic")
    return {
        "cases": results,
        "aggregate": aggregate(results),
        "r20_economics_frozen": True,
        "original_policy_paths_unchanged": True,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=("smoke", "ci", "extended"),
                        default="ci")
    parser.add_argument("--output", type=Path, default=Path.cwd())
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    report = {
        "schema": "GA_GATE09_R35_CONSERVATIVE_LOCAL_DIFFERENTIAL_V1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(), "python": platform.python_version(),
        "profile": args.profile, "status": "FAILED",
        "global_equilibrium_proven": False,
        "financial_F059_ratified": False,
        "production_approved": False,
        "no_causal_claims": True,
    }
    try:
        report["r20_manifest_files_verified"] = R21.validate_manifest()
        if report["r20_manifest_files_verified"] != 16:
            raise AssertionError("R20 manifest drift")
        report.update(exercise(args.profile))
        report["status"] = "PASSED_LOCAL_DIFFERENTIAL_DIAGNOSTIC_ONLY"
    except Exception as exc:
        report["error"] = repr(exc)
        report["traceback"] = traceback.format_exc()
        print("[R35] FAILED:", repr(exc), flush=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    p = args.output / ("GA-Gate09-R35-" + args.profile + "-" + stamp + ".json")
    p.write_text(json.dumps(report, sort_keys=True, indent=2,
                            ensure_ascii=False) + "\n", encoding="utf-8")
    z = args.output / ("GA-Gate09-R35-Evidencias-" + stamp + ".zip")
    with zipfile.ZipFile(z,"w",zipfile.ZIP_DEFLATED) as archive:
        archive.write(p,p.name)
    print("[R35] STATUS:", report["status"], flush=True)
    print("[R35] EVIDENCE:", z, flush=True)
    return 0 if report["status"] == "PASSED_LOCAL_DIFFERENTIAL_DIAGNOSTIC_ONLY" else 1


if __name__=="__main__":
    raise SystemExit(main())
