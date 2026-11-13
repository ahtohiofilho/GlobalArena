import math
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r22 as R
from multimarket_r13 import construct_scenario
from clearing_r5 import ClearingSettings
from oracle_cache_r14 import evaluate_r14


class R22ContractTests(unittest.TestCase):
    def test_frozen_upstream_hashes(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_group_attribution_reconstructs_f072_without_artificial_residual(self):
        net, flows = construct_scenario(R.R21.CASES["small"])
        obs = evaluate_r14(net, flows, ClearingSettings(exponent=4), ())
        attribution = R.attribute_gap(net, obs)
        self.assertAlmostEqual(attribution["reconstructed_relative_gap"],
                               obs["relative_gap"], delta=1e-12)
        self.assertAlmostEqual(
            sum(attribution["gap_contributions_by_good"].values()),
            obs["relative_gap"], delta=1e-12)
        self.assertAlmostEqual(
            sum(attribution["gap_contributions_by_civilization"].values()),
            obs["relative_gap"], delta=1e-12)
        self.assertLessEqual(attribution["top_three_share_of_total_regret"], 1.0 + 1e-12)
        self.assertGreaterEqual(attribution["top_group_share_of_total_regret"], 0)

    def test_edge_stress_is_observational(self):
        net, flows = construct_scenario(R.R21.CASES["small"])
        obs = evaluate_r14(net, flows, ClearingSettings(exponent=4), ())
        stress = R.edge_stress(net, obs)
        self.assertTrue(0 <= stress["edge_count_above_or_at_natural_capacity"]
                        <= len(net.edges))
        self.assertGreaterEqual(stress["maximum_congestion_multiplier"], 1)
        self.assertLessEqual(len(stress["most_loaded_edges"]), 5)

    def test_replays_original_r21_trajectory(self):
        for policy in R.POLICIES:
            with self.subTest(policy=policy):
                self.assertTrue(R.replay_r21(R.R21.CASES["small"], policy, 6))

    def test_financial_and_numerical_gates_remain_separate(self):
        result = R.run_case(R.R21.CASES["small"], "fixed_1_8", 4, audit_every=1)
        self.assertEqual(len(result["audited_steps"]), 5)
        for audit in result["audited_steps"]:
            self.assertEqual(audit["gross_micro"], audit["credits_micro"])
            self.assertGreater(audit["sold_micro"], 0)
        self.assertFalse(result["economic_equilibrium_certified"])
        self.assertFalse(result["financial_F059_ratified"])
        self.assertFalse(result["authoritative_solver_approved"])
        self.assertIsNone(result["rows"][0]["price_change"])
        self.assertFalse(result["rows"][0]["numerical_five_thresholds_met"])

    def test_prefix_replay(self):
        cfg = R.R21.CASES["small"]
        short = R.run_case(cfg, "decay_sqrt", 4)
        long = R.run_case(cfg, "decay_sqrt", 7)
        self.assertEqual(short["rows"], long["rows"][:len(short["rows"])])


if __name__ == "__main__":
    unittest.main()
