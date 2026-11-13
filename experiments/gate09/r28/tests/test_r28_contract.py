import math
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r28 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class R28GuardedSearchContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def frozen(self):
        net, flows = construct_scenario(self.cfg)
        settings = ClearingSettings(exponent=self.cfg.exponent)
        obs = evaluate_r14(net, flows, settings, ())
        return net, flows, settings, obs

    def test_r20_16_file_baseline(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_decay_first_candidate_matches_r21(self):
        self.assertTrue(R.cold_start_integrity(self.cfg))
        self.assertEqual(R.candidate_order("guard_fixed_global", 1), R.FIXED_ALPHAS)
        self.assertEqual(R.candidate_order("guard_decay_global", 1), R.FIXED_ALPHAS)

    def test_candidate_order_no_duplicate_and_deterministic(self):
        for step in (1, 2, 8, 25, 96):
            sequence = R.candidate_order("guard_decay_joint", step)
            self.assertEqual(len(sequence), len(set(sequence)))
            self.assertTrue(all(0 < a <= .25 for a in sequence))
            self.assertEqual(sequence, R.candidate_order("guard_decay_joint", step))
        self.assertEqual(R.candidate_order("guard_decay_joint", 25)[0], 1/8)
        with self.assertRaises(ValueError):
            R.candidate_order("missing", 3)

    def test_sufficient_progress_requires_more_than_tiny_improvement(self):
        self.assertFalse(R.sufficient("guard_decay_global", .2, .4, .199, .3))
        self.assertTrue(R.sufficient("guard_decay_global", .2, .4, .197, .6))
        self.assertFalse(R.sufficient("guard_decay_joint", .2, .4, .197, .6))
        self.assertTrue(R.sufficient("guard_decay_joint", .2, .4, .197, .39))
        self.assertTrue(R.sufficient("guard_decay_joint", 1e-5, 2e-5,
                                     1e-5, 2e-5))
        with self.assertRaises(ValueError):
            R.sufficient("bogus", .2, .4, .19, .4)

    def test_one_accepted_trial_means_one_oracle_call(self):
        net, flows, settings, obs = self.frozen()
        original_digest = _flow_checksum(flows)
        future = {"relative_gap": obs["relative_gap"] * .90,
                  "max_group_gap": obs["max_group_gap"] * .9}
        with patch.object(R, "evaluate_r14", return_value=future) as called:
            _, _, _, result = R.choose_guarded(net, flows, obs, settings,
                                                "guard_decay_joint", 2, 6)
            self.assertEqual(called.call_count, 1)
        self.assertEqual(result["candidate_count"], 1)
        self.assertTrue(result["stopped_by_progress_gate"])
        self.assertFalse(result["fallback_no_sufficient_progress"])
        self.assertEqual(_flow_checksum(flows), original_digest)

    def test_truncated_fallback_is_not_claimed_success(self):
        net, flows, settings, obs = self.frozen()
        future = {"relative_gap": obs["relative_gap"] * .999,
                  "max_group_gap": obs["max_group_gap"] * 1.1}
        with patch.object(R, "evaluate_r14", return_value=future) as called:
            _, _, _, result = R.choose_guarded(net, flows, obs, settings,
                                                "guard_decay_global", 3, 2)
            self.assertEqual(called.call_count, 2)
        self.assertEqual(len(result["candidate_trials"]), 2)
        self.assertFalse(result["stopped_by_progress_gate"])
        self.assertTrue(result["fallback_no_sufficient_progress"])
        self.assertTrue(result["budget_truncated"])

    def test_real_trial_reprices_and_preserves_money(self):
        net, flows, settings, obs = self.frozen()
        before = _flow_checksum(flows)
        for method in R.POLICIES:
            with self.subTest(method=method):
                proposed, priced, delta, info = R.choose_guarded(
                    net, flows, obs, settings, method, 1, 6)
                self.assertEqual(_flow_checksum(flows), before)
                self.assertTrue(math.isfinite(priced["relative_gap"]))
                self.assertTrue(1 <= info["candidate_count"] <= 6)
                self.assertGreaterEqual(
                    delta["max_relative_price_change"], 0)
                R.R21.frozen_check(net, proposed, priced)
                fiscal = R.R21.settlement_check(net, proposed)
                self.assertEqual(fiscal["gross_micro"], fiscal["credits_micro"])

    def test_area_under_regret_counts_discarded_candidates(self):
        rows = [{"oracles_used": 1, "relative_gap": .3},
                {"oracles_used": 4, "relative_gap": .2},
                {"oracles_used": 5, "relative_gap": .1}]
        # 3 calls at 0.3, then one call at 0.2: (0.9+0.2)/4.
        self.assertAlmostEqual(R.cumulative_work_gap(rows), .275, delta=1e-12)
        self.assertEqual(R.cumulative_work_gap(rows[:1]), .3)

    def test_cost_budget_and_final_financial_audit(self):
        for policy in R.POLICIES:
            with self.subTest(policy=policy):
                result = R.run_case(self.cfg, policy, 9, 12, audit_every=3)
                self.assertLessEqual(result["oracles_used"], 12)
                self.assertEqual(result["oracles_used"],
                                 1 + sum(x["decision"]["candidate_count"]
                                         for x in result["rows"][1:]))
                self.assertEqual(result["financial_audits"][-1]["step"],
                                 result["updates_executed"])
                self.assertFalse(result["global_equilibrium_proven"])
                self.assertFalse(result["financial_F059_ratified"])
                self.assertFalse(result["authoritative_solver_approved"])
                for key, value in result["oracle_budget_frontiers"].items():
                    self.assertLessEqual(value["oracles_used"], int(key))

    def test_one_remaining_candidate_billed_and_audited(self):
        for policy in R.POLICIES:
            result = R.run_case(self.cfg, policy, 9, 2)
            self.assertEqual(result["oracles_used"], 2)
            self.assertEqual(result["updates_executed"], 1)
            self.assertEqual(result["financial_audits"][-1]["step"], 1)

    def test_prefix_replay_independent_of_horizon(self):
        for policy in R.POLICIES:
            with self.subTest(policy=policy):
                short = R.run_case(self.cfg, policy, 4, 91, audit_every=2)
                long = R.run_case(self.cfg, policy, 8, 91, audit_every=2)
                self.assertEqual(short["rows"], long["rows"][:len(short["rows"])])

    def test_original_r21_reference_unchanged(self):
        for policy in ("decay_sqrt", "backtracking"):
            self.assertTrue(R.R24.check_r21_reference(self.cfg, policy, 6))


if __name__ == "__main__":
    unittest.main()
