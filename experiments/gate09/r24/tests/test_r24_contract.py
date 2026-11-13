import math
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r24 as R
from multimarket_r13 import construct_scenario
from clearing_r5 import ClearingSettings, _flow_checksum
from oracle_cache_r14 import evaluate_r14


class TestR24TrustRegion(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def test_frozen_r20_manifest(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_reference_r21_is_bitwise_identical(self):
        self.assertTrue(R.check_r21_reference(self.cfg, "decay_sqrt", 6))
        self.assertTrue(R.check_r21_reference(self.cfg, "backtracking", 6))

    def test_all_five_trials_share_identical_source_state(self):
        net, flows = construct_scenario(self.cfg)
        cfg = ClearingSettings(exponent=self.cfg.exponent)
        observed = evaluate_r14(net, flows, cfg, ())
        before = _flow_checksum(flows)
        updated, next_obs, delta, audit = R.choose_trust(
            net, flows, observed, cfg, "trust_joint", 0.05)
        self.assertEqual(before, _flow_checksum(flows))
        self.assertEqual(len(audit["trials"]), 5)
        self.assertEqual(audit["candidate_count"], 5)
        self.assertEqual(
            [x["alpha"] for x in audit["trials"]],
            list(R.CANDIDATE_ALPHAS))
        self.assertTrue(any(abs(audit["alpha"] - v) < 1e-15
                            for v in R.CANDIDATE_ALPHAS))
        self.assertTrue(0.002 <= audit["radius_after"] <= 0.2)
        self.assertGreaterEqual(next_obs["relative_gap"], 0)
        self.assertTrue(math.isfinite(delta["max_relative_price_change"]))
        check = R.validate_money_and_parity(net, updated, next_obs)
        self.assertEqual(check["gross_micro"], check["credits_micro"])

    def test_forced_radius_violation_is_logged(self):
        net, flows = construct_scenario(self.cfg)
        settings = ClearingSettings(exponent=self.cfg.exponent)
        obs = evaluate_r14(net, flows, settings, ())
        _, _, _, decision = R.choose_trust(
            net, flows, obs, settings, "trust_global", radius=0.002)
        self.assertEqual(decision["forced_radius_violation"],
                         not any(t["within_radius"] for t in decision["trials"]))
        if decision["forced_radius_violation"]:
            self.assertLessEqual(decision["radius_after"], decision["radius_before"])

    def test_five_numerical_gates_require_price_load_quantity(self):
        obs = {"relative_gap": 1e-6, "max_group_gap": 2e-6}
        delta = {
            "max_relative_price_change": 2e-5,
            "max_relative_load_change": 8e-5,
            "max_relative_quantity_change": 9e-5,
        }
        self.assertFalse(R.numerical_pass(obs, None))
        self.assertTrue(R.numerical_pass(obs, delta))
        delta["max_relative_quantity_change"] = 2e-4
        self.assertFalse(R.numerical_pass(obs, delta))
        delta["max_relative_quantity_change"] = 8e-5
        obs["max_group_gap"] = 2e-4
        self.assertFalse(R.numerical_pass(obs, delta))

    def test_budget_enforces_cost_and_fair_frontiers(self):
        for policy in R.POLICIES:
            with self.subTest(policy=policy):
                result = R.run_case(self.cfg, policy, 12, 26, audit_every=4)
                self.assertLessEqual(result["oracles_used"], 26)
                self.assertEqual(result["oracles_used"],
                                 1 + sum(row["candidate_count"]
                                         for row in result["rows"]))
                self.assertEqual(result["financial_audits"][-1]["step"],
                                 result["updates_executed"])
                self.assertEqual(result["rows"][-1]["flow_sha256"],
                                 result["final_flow_sha256"])
                self.assertFalse(result["equilibrium_proven"])
                self.assertFalse(result["financial_F059_ratified"])
                self.assertFalse(result["production_approved"])
                for key, value in result["oracle_budget_frontiers"].items():
                    self.assertLessEqual(value["oracles_used"], int(key))

    def test_trust_prefix_is_deterministic(self):
        short = R.run_case(self.cfg, "trust_joint", 4, 81)
        long = R.run_case(self.cfg, "trust_joint", 7, 81)
        self.assertEqual(short["rows"], long["rows"][:len(short["rows"])])
        self.assertEqual(short["final_flow_sha256"],
                         long["rows"][4]["flow_sha256"])


if __name__ == "__main__":
    unittest.main()
