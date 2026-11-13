import math
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r27 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class R27WarmStartContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def frozen(self):
        net, flows = construct_scenario(self.cfg)
        cfg = ClearingSettings(exponent=self.cfg.exponent)
        return net, flows, cfg, evaluate_r14(net, flows, cfg, ())

    def test_immutable_r20_manifest(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_cold_order_matches_original_r26(self):
        self.assertEqual(R.candidate_order(None), R.ALPHAS)
        self.assertTrue(R.cold_start_replay(self.cfg))

    def test_candidate_order_is_complete_deterministic_and_anchored(self):
        for anchor in R.ALPHAS:
            order = R.candidate_order(anchor)
            self.assertEqual(order[0], anchor)
            self.assertEqual(len(order), len(R.ALPHAS))
            self.assertEqual(set(order), set(R.ALPHAS))
            self.assertEqual(order, R.candidate_order(anchor))
        with self.assertRaises(ValueError):
            R.candidate_order(0.1)

    def test_cached_alpha_never_reuses_price_or_flow(self):
        net, flows, settings, obs = self.frozen()
        digest = _flow_checksum(flows)
        with patch.object(R, "evaluate_r14", wraps=R.evaluate_r14) as fresh:
            new_flows, new_obs, delta, anchor, audit = R.choose_warm(
                net, flows, obs, settings, "warm_global", 1 / 16, 5)
            self.assertEqual(fresh.call_count, audit["candidate_count"])
        self.assertEqual(digest, _flow_checksum(flows))
        self.assertEqual(audit["tested_order"][0], 1 / 16)
        self.assertEqual(audit["candidate_count"], len(audit["trials"]))
        self.assertGreater(new_obs["relative_gap"], -1e-15)
        R.R21.frozen_check(net, new_flows, new_obs)
        money = R.R21.settlement_check(net, new_flows)
        self.assertEqual(money["gross_micro"], money["credits_micro"])
        self.assertTrue(math.isfinite(delta["max_relative_price_change"]))

    def test_global_acceptance_does_not_skip_repricing(self):
        net, flows, settings, obs = self.frozen()
        fake = {
            "relative_gap": obs["relative_gap"] * .9,
            "max_group_gap": obs["max_group_gap"] * 1.1,
        }
        with patch.object(R, "evaluate_r14", return_value=fake) as price:
            _, _, _, anchor, audit = R.choose_warm(
                net, flows, obs, settings, "warm_global", 1 / 32, 5)
        self.assertEqual(price.call_count, 1)
        self.assertTrue(audit["accepted_early"])
        self.assertEqual(audit["selected_alpha"], 1 / 32)
        self.assertEqual(anchor, 1 / 32)

    def test_joint_fallback_keeps_last_accepted_anchor(self):
        net, flows, settings, obs = self.frozen()
        fake = {
            "relative_gap": obs["relative_gap"] * .9,
            "max_group_gap": obs["max_group_gap"] * 1.1,
        }
        with patch.object(R, "evaluate_r14", return_value=fake) as price:
            _, _, _, anchor, audit = R.choose_warm(
                net, flows, obs, settings, "warm_joint", 1 / 16, 2)
        self.assertEqual(price.call_count, 2)
        self.assertEqual(audit["tested_order"], [1 / 16, 1 / 8])
        self.assertFalse(audit["accepted_early"])
        self.assertTrue(audit["fallback_no_acceptable_step"])
        self.assertTrue(audit["truncated_search"])
        self.assertEqual(anchor, 1 / 16)

    def test_work_budget_and_audited_final_snapshot(self):
        for policy in R.POLICIES:
            with self.subTest(policy=policy):
                result = R.run_case(self.cfg, policy, 12, 9, audit_every=3)
                self.assertLessEqual(result["oracles_used"], 9)
                self.assertEqual(result["oracles_used"],
                                 1 + sum(r["decision"]["candidate_count"]
                                         for r in result["rows"][1:]))
                self.assertEqual(result["financial_audits"][-1]["step"],
                                 result["updates_executed"])
                self.assertEqual(result["final_flow_sha256"],
                                 result["rows"][-1]["flow_sha256"])
                self.assertFalse(result["global_equilibrium_proven"])
                self.assertFalse(result["financial_F059_ratified"])
                self.assertFalse(result["production_approved"])

    def test_last_single_available_candidate_is_charged(self):
        for policy in R.POLICIES:
            result = R.run_case(self.cfg, policy, 8, 2)
            self.assertEqual(result["oracles_used"], 2)
            self.assertEqual(result["updates_executed"], 1)
            self.assertEqual(result["financial_audits"][-1]["step"], 1)

    def test_warm_prefix_is_identical_across_horizons(self):
        for policy in R.POLICIES:
            short = R.run_case(self.cfg, policy, 4, 51, audit_every=2)
            long = R.run_case(self.cfg, policy, 7, 51, audit_every=2)
            self.assertEqual(short["rows"], long["rows"][:5])

    def test_frozen_r21_reference_contracts(self):
        self.assertTrue(R.R24.check_r21_reference(self.cfg, "decay_sqrt", 6))
        self.assertTrue(R.R24.check_r21_reference(self.cfg, "backtracking", 6))


if __name__ == "__main__":
    unittest.main()
