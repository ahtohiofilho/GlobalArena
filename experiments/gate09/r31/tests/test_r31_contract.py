from pathlib import Path
import math
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r31 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import FrozenRouteCache, evaluate_r14


class R31CrossingDecompositionContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def frozen(self):
        net, flows = construct_scenario(self.cfg)
        settings = ClearingSettings(exponent=self.cfg.exponent)
        obs = evaluate_r14(net, flows, settings, ())
        return net, flows, settings, obs

    def transitioned(self, policy="decay_sqrt"):
        net, flows, settings, before = self.frozen()
        new_flow, after, delta, calls, alpha = R.R29.step_forward(
            net, flows, before, settings, policy, 1)
        return net, flows, settings, before, new_flow, after, calls, alpha

    def test_r20_immutable_16_file_manifest(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_market_pair_at_frozen_snapshot_is_exact_score_identity(self):
        net, flows, settings, obs = self.frozen()
        cache = FrozenRouteCache(net, obs["loads"], settings.exponent)
        gid = min(net.groups)
        group = net.groups[gid]
        destinations = sorted(
            m[0] for m in net.markets if m[1] == group.commodity)
        pair = R.market_pair_at_snapshot(
            net, obs, cache, gid, destinations[0], destinations[1])
        self.assertAlmostEqual(
            pair["log_advantage_new_over_old"],
            pair["log_relative_price_new_over_old"] -
            pair["log_relative_transport_new_over_old"], delta=1e-11)
        self.assertAlmostEqual(
            pair["new_score"],
            pair["new_price"] / (1 + pair["new_cost"]), delta=1e-12)
        self.assertGreater(cache.counters()["shortest_path_scans"], 0)

    def test_price_quantity_sqrt_identity_and_transport_split(self):
        net, flows, settings, obs, new_flows, after, _, _ = self.transitioned()
        report = R.transition_crossings(net, obs, after, settings.exponent)
        self.assertGreater(report["market_switches"], 0)
        self.assertTrue(report["all_endpoint_rankings_reversed"])
        for point in report["pairs"]:
            self.assertAlmostEqual(
                point["delta_log_advantage"],
                point["relative_price_log_effect"] +
                point["relative_transport_log_effect"], delta=R.GAP_TOL)
            self.assertAlmostEqual(
                point["relative_price_log_effect"],
                point["quantity_implied_log_price_effect"], delta=R.GAP_TOL)
            self.assertAlmostEqual(
                point["relative_transport_log_effect"],
                point["fixed_old_path_transport_log_effect"] +
                point["canonical_reoptimization_log_effect"], delta=R.GAP_TOL)
            self.assertLessEqual(
                point["before_log_advantage_new_over_old"], R.GAP_TOL)
            self.assertGreaterEqual(
                point["after_log_advantage_new_over_old"], -R.GAP_TOL)
            self.assertFalse(point["second_best_route_enumerated"])
            self.assertFalse(point["causal_attribution_established"])

    def test_price_and_transport_counts_partition_all_market_switches(self):
        net, flows, settings, obs, _, after, _, _ = self.transitioned("backtracking")
        report = R.transition_crossings(net, obs, after, settings.exponent)
        self.assertEqual(
            report["price_dominated_switches"] +
            report["transport_dominated_switches"],
            report["market_switches"])
        self.assertEqual(
            report["market_switches"],
            sum(obs["target"][gid].destination != after["target"][gid].destination
                for gid in net.groups))
        self.assertGreaterEqual(
            report["extra_shortest_path_scans"], 1)
        self.assertLessEqual(
            report["canonical_path_reoptimizations_within_market_pair"],
            report["market_switches"])

    def test_fractional_alpha_samples_are_pure_and_billed(self):
        net, flows, settings, obs, newer, next_obs, calls, alpha = self.transitioned()
        pairs = R.transition_crossings(net, obs, next_obs)["pairs"]
        original = _flow_checksum(flows)
        committed = _flow_checksum(newer)
        with patch.object(R, "evaluate_r14", wraps=R.evaluate_r14) as audit:
            probes = R.crossing_substeps(
                net, flows, obs, newer, next_obs, settings, alpha, pairs, 1)
            self.assertEqual(audit.call_count, 3)
        self.assertEqual(probes["additional_r14_oracle_calls"], 3)
        self.assertEqual(len(probes["fractional_snapshots"]), 3)
        self.assertEqual(
            [point["fraction_of_committed_alpha"]
             for point in probes["fractional_snapshots"]],
            list(R.FRACTIONAL_STEPS))
        self.assertEqual(probes["source_flow_sha256"], original)
        self.assertEqual(probes["committed_flow_sha256"], committed)
        self.assertEqual(_flow_checksum(flows), original)
        self.assertEqual(_flow_checksum(newer), committed)
        self.assertFalse(not probes["no_intermediate_flow_committed"])
        self.assertGreater(probes["additional_shortest_path_scans"], 0)

    def test_independently_repriced_final_state_and_money(self):
        net, flows, settings, obs, successor, after, _, _ = self.transitioned()
        R.R21.frozen_check(net, successor, after)
        settlement = R.R21.settlement_check(net, successor)
        self.assertEqual(settlement["gross_micro"], settlement["credits_micro"])
        reconstructed = evaluate_r14(net, successor, settings, ())
        self.assertEqual(after["target"], reconstructed["target"])
        self.assertEqual(after["prices"], reconstructed["prices"])
        self.assertEqual(after["loads"], reconstructed["loads"])

    def test_original_r30_trajectory_gaps_and_flows_unchanged(self):
        new = R.run_case(self.cfg, "decay_sqrt", 5,
                         audit_every=2, probe_steps=(1, 2, 4))
        prior = R.R30.run_case(self.cfg, "decay_sqrt", 5,
                               audit_every=2, probe_every=2)
        self.assertEqual(new["final_gap"], prior["final_gap"])
        self.assertEqual(new["final_max_group_gap"], prior["final_max_group_gap"])
        self.assertEqual(new["final_flow_sha256"], prior["final_flow_sha256"])
        self.assertEqual(new["proposal_oracles_used"],
                         prior["proposal_oracle_evaluations"])
        self.assertEqual(new["financial_audits"][-1]["step"], 5)
        self.assertEqual(
            new["substep_probe_oracles_used"],
            len(new["uncommitted_probes"]) * 3)
        self.assertGreater(new["extra_independent_shortest_path_scans"], 0)
        self.assertFalse(new["global_equilibrium_proven"])
        self.assertFalse(new["financial_F059_ratified"])
        self.assertFalse(new["production_approved"])

    def test_prefix_determinism_regardless_of_end_horizon(self):
        small = R.run_case(self.cfg, "fixed_1_8", 4,
                           audit_every=2, probe_steps=(1, 2, 4))
        longer = R.run_case(self.cfg, "fixed_1_8", 7,
                            audit_every=2, probe_steps=(1, 2, 4))
        self.assertEqual(small["rows"], longer["rows"][:5])

    def test_backtracking_reference_matches_r21(self):
        ours = R.run_case(self.cfg, "backtracking", 5,
                          audit_every=2, probe_steps=(1, 2, 4))
        theirs = R.R21.run_trajectory(self.cfg, "backtracking", 5,
                                       audit_every=2)
        self.assertEqual(ours["final_flow_sha256"],
                         theirs["final_flow_sha256"])
        self.assertEqual(ours["final_gap"], theirs["final_gap"])

    def test_progressive_reference_matches_r26_and_charges_trials(self):
        ours = R.run_case(self.cfg, "progressive_global", 5,
                          audit_every=2, probe_steps=(1, 2, 4))
        baseline = R.R26.run_case(self.cfg, "progressive_global", 5, 45,
                                  audit_every=2)
        self.assertEqual(ours["final_gap"], baseline["final_gap"])
        self.assertEqual(ours["final_flow_sha256"],
                         baseline["final_flow_sha256"])
        self.assertEqual(ours["proposal_oracles_used"],
                         baseline["oracles_used"])

    def test_market_switch_events_are_bijective_with_pair_records(self):
        report = R.run_case(self.cfg, "decay_sqrt", 7,
                            audit_every=2, probe_steps=(1, 6))
        numbers = report["summary"]
        self.assertEqual(numbers["total_market_switches"],
                         numbers["market_switches_price_effect_dominated"] +
                         numbers["market_switches_transport_effect_dominated"])
        self.assertEqual(
            numbers["substep_oracle_evaluations"],
            report["substep_probe_oracles_used"])
        self.assertFalse(numbers["price_and_quantity_counterfactual_causality_proven"])
        self.assertTrue(all(
            r["crossings"]["all_endpoint_rankings_reversed"]
            for r in report["rows"][1:]))


if __name__ == "__main__":
    unittest.main()
