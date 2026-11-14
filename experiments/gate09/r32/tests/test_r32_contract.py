import math
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r32 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class R32HybridScoreContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def frozen(self):
        net, flows = construct_scenario(self.cfg)
        settings = ClearingSettings(exponent=self.cfg.exponent)
        obs = evaluate_r14(net, flows, settings, ())
        return net, flows, settings, obs

    def transitioned(self, policy="decay_sqrt"):
        net, flows, settings, old = self.frozen()
        newer, future, delta, calls, alpha = R.R29.step_forward(
            net, flows, old, settings, policy, 1)
        return net, flows, settings, old, newer, future, calls, alpha

    def test_manifest_is_frozen(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_pair_classification_is_exhaustive(self):
        self.assertEqual(R._classify_pair(True, True), "both_individual_cross")
        self.assertEqual(R._classify_pair(True, False), "price_only_cross")
        self.assertEqual(R._classify_pair(False, True), "transport_only_cross")
        self.assertEqual(R._classify_pair(False, False), "neither_individual_cross")

    def test_same_frozen_state_returns_identical_four_market_rankings(self):
        net, flows, settings, obs = self.frozen()
        report = R.frozen_hybrid_rankings(net, obs, obs, settings.exponent)
        counts = report["counts"]
        self.assertEqual(counts["market_switches"], 0)
        self.assertEqual(counts["market_unchanged"], len(net.groups))
        self.assertEqual(counts["producer_groups"], len(net.groups))
        self.assertEqual(report["extra_r14_oracle_calls"], 0)
        self.assertGreater(report["independent_shortest_path_scans"], 0)
        for row in report["groups"].values():
            self.assertEqual(len(set(row["four_market_winners"].values())), 1)
            self.assertTrue(row["mixed_rankings_are_not_clearing_states"])

    def test_no_repricing_calls_or_mutation_from_algebraic_hybrid(self):
        net, flows, settings, old, new_flows, new_obs, _, _ = self.transitioned()
        before_digest = _flow_checksum(flows)
        after_digest = _flow_checksum(new_flows)
        before_prices = dict(old["prices"])
        after_prices = dict(new_obs["prices"])
        with patch.object(R, "evaluate_r14",
                          side_effect=AssertionError("Unexpected R14 call")):
            report = R.frozen_hybrid_rankings(
                net, old, new_obs, settings.exponent)
        self.assertEqual(report["extra_r14_oracle_calls"], 0)
        self.assertTrue(report["only_read_existing_frozen_quotes_and_loads"])
        self.assertEqual(before_digest, _flow_checksum(flows))
        self.assertEqual(after_digest, _flow_checksum(new_flows))
        self.assertEqual(old["prices"], before_prices)
        self.assertEqual(new_obs["prices"], after_prices)

    def test_four_way_winner_parity_and_pair_log_identity(self):
        net, flows, settings, old, new_flows, new_obs, _, _ = self.transitioned()
        report = R.frozen_hybrid_rankings(net, old, new_obs, settings.exponent)
        self.assertGreater(report["counts"]["market_switches"], 0)
        for gid, row in report["groups"].items():
            self.assertEqual(row["four_market_winners"]["old_actual"],
                             old["target"][gid].destination)
            self.assertEqual(row["four_market_winners"]["new_actual"],
                             new_obs["target"][gid].destination)
            if row["is_market_switch"]:
                self.assertAlmostEqual(
                    row["delta_log_advantage"],
                    row["delta_log_relative_price"] +
                    row["delta_log_relative_transport"], delta=1e-11)
                self.assertAlmostEqual(
                    row["log_advantage_price_only"],
                    row["log_advantage_B_over_A_old"] +
                    row["delta_log_relative_price"], delta=1e-11)
                self.assertAlmostEqual(
                    row["log_advantage_transport_only"],
                    row["log_advantage_B_over_A_old"] +
                    row["delta_log_relative_transport"], delta=1e-11)

    def test_quantity_price_identity_is_independent_of_transport(self):
        net, flows, settings, old, new_flows, future, _, _ = self.transitioned()
        report = R.frozen_hybrid_rankings(net, old, future, settings.exponent)
        for row in report["groups"].values():
            if row["is_market_switch"]:
                self.assertAlmostEqual(
                    row["delta_log_relative_price"],
                    row["quantity_effect_old_market_A"] +
                    row["quantity_effect_new_market_B"], delta=1e-11)
                self.assertTrue(row["quantity_price_identity_closes"])
        self.assertEqual(
            report["counts"]["quantity_price_identity_verified"],
            report["counts"]["market_switches"])

    def test_pair_partition_never_counts_a_synthetic_third_winner_as_B(self):
        net, flows, settings, old, new_flows, future, _, _ = self.transitioned()
        result = R.frozen_hybrid_rankings(net, old, future, settings.exponent)
        count = result["counts"]
        self.assertEqual(count["market_switches"], sum(
            count[k] for k in (
                "pair_both_individual_cross", "pair_price_only_cross",
                "pair_transport_only_cross", "pair_neither_individual_cross")))
        self.assertEqual(count["market_switches"],
                         count["price_log_effect_dominant"] +
                         count["transport_log_effect_dominant"])
        self.assertLessEqual(count["price_only_winner_is_actual"],
                             count["market_switches"])
        self.assertLessEqual(count["transport_only_winner_is_actual"],
                             count["market_switches"])

    def test_pair_effects_match_unchanged_r31_decomposition(self):
        net, flows, settings, old, new_flows, future, _, _ = self.transitioned(
            "backtracking")
        result = R.frozen_hybrid_rankings(net, old, future, settings.exponent)
        original = R.R31.transition_crossings(
            net, old, future, settings.exponent)
        self.assertEqual(result["counts"]["market_switches"],
                         original["market_switches"])
        for point in original["pairs"]:
            row = result["groups"][point["group"]]
            self.assertAlmostEqual(row["delta_log_relative_price"],
                                   point["relative_price_log_effect"], delta=1e-11)
            self.assertAlmostEqual(row["delta_log_relative_transport"],
                                   point["relative_transport_log_effect"],
                                   delta=1e-11)
            self.assertEqual(
                row["price_component_has_greater_absolute_magnitude"],
                point["absolute_price_effect_dominates"])

    def test_replays_r31_gap_and_f084f_settlement(self):
        ours = R.run_case(self.cfg, "decay_sqrt", 5, audit_every=2)
        prior = R.R31.run_case(self.cfg, "decay_sqrt", 5,
                                audit_every=2, probe_steps=())
        self.assertEqual(ours["final_gap"], prior["final_gap"])
        self.assertEqual(ours["final_flow_sha256"], prior["final_flow_sha256"])
        self.assertEqual(ours["proposal_oracles_used"],
                         prior["proposal_oracles_used"])
        self.assertEqual(ours["financial_audits"][-1]["step"], 5)
        self.assertEqual(ours["algebraic_hybrid_additional_oracle_calls"], 0)
        self.assertFalse(ours["causality_proven"])
        self.assertFalse(ours["f059_ratified"])
        self.assertFalse(ours["production_approved"])

    def test_prefix_independence_of_horizon(self):
        small = R.run_case(self.cfg, "fixed_1_8", 4, audit_every=2)
        large = R.run_case(self.cfg, "fixed_1_8", 7, audit_every=2)
        self.assertEqual(small["rows"], large["rows"][:5])

    def test_backtracking_replays_r21_reference(self):
        ours = R.run_case(self.cfg, "backtracking", 6, audit_every=2)
        original = R.R21.run_trajectory(self.cfg, "backtracking", 6,
                                         audit_every=2)
        self.assertEqual(ours["final_flow_sha256"],
                         original["final_flow_sha256"])
        self.assertEqual(ours["final_gap"], original["final_gap"])

    def test_progressive_reference_replays_r26(self):
        ours = R.run_case(self.cfg, "progressive_global", 5, audit_every=2)
        prior = R.R26.run_case(self.cfg, "progressive_global", 5, 51,
                                audit_every=2)
        self.assertEqual(ours["final_gap"], prior["final_gap"])
        self.assertEqual(ours["final_flow_sha256"],
                         prior["final_flow_sha256"])
        self.assertEqual(ours["proposal_oracles_used"], prior["oracles_used"])


if __name__=="__main__":
    unittest.main()
