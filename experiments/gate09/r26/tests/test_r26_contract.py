import math
from pathlib import Path
import sys
from unittest.mock import patch
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ci"))
import run_r26 as R
from clearing_r5 import ClearingSettings, _flow_checksum
from multimarket_r13 import construct_scenario
from oracle_cache_r14 import evaluate_r14


class R26ProgressiveContracts(unittest.TestCase):
    def setUp(self):
        self.cfg = R.R21.CASES["small"]

    def get_frozen(self):
        net, flows = construct_scenario(self.cfg)
        settings = ClearingSettings(exponent=self.cfg.exponent)
        return net, flows, settings, evaluate_r14(net, flows, settings, ())

    def test_r20_immutable_provenance(self):
        self.assertEqual(R.R21.validate_manifest(), 16)

    def test_acceptance_checks_aggregate_and_worst_separately(self):
        self.assertTrue(R.acceptable("progressive_global", .2, .3, .19, .4))
        self.assertFalse(R.acceptable("progressive_joint", .2, .3, .19, .4))
        self.assertTrue(R.acceptable("progressive_joint", .2, .3, .19, .29))
        self.assertFalse(R.acceptable("progressive_joint", .2, .3, .201, .29))
        with self.assertRaises(ValueError):
            R.acceptable("unknown", .2, .3, .1, .2)

    def test_early_stop_is_one_paid_oracle_call(self):
        net, flows, settings, observed = self.get_frozen()
        source_sha = _flow_checksum(flows)
        # Candidate reduces the global gap but worsens the worst producer.
        cheap = {
            "relative_gap": observed["relative_gap"] * .9,
            "max_group_gap": observed["max_group_gap"] * 1.1,
        }
        with patch.object(R, "evaluate_r14", return_value=cheap) as oracle:
            _, _, _, decision = R.choose_progressive(
                net, flows, observed, settings, "progressive_global", 5)
            self.assertEqual(oracle.call_count, 1)
            self.assertEqual(decision["candidate_count"], 1)
            self.assertTrue(decision["accepted_early"])
            self.assertEqual(decision["selected_alpha"], .25)
        self.assertEqual(_flow_checksum(flows), source_sha)

    def test_joint_rejects_global_only_and_counts_truncated_trial(self):
        net, flows, settings, observed = self.get_frozen()
        future = {
            "relative_gap": observed["relative_gap"] * .9,
            "max_group_gap": observed["max_group_gap"] * 1.1,
        }
        with patch.object(R, "evaluate_r14", return_value=future) as oracle:
            _, _, _, info = R.choose_progressive(
                net, flows, observed, settings, "progressive_joint", 2)
            self.assertEqual(oracle.call_count, 2)
            self.assertFalse(info["accepted_early"])
            self.assertTrue(info["fallback_no_acceptable_step"])
            self.assertTrue(info["candidate_budget_truncated"])
            self.assertEqual(len(info["trials"]), 2)

    def test_real_candidates_are_not_stale_or_mutating(self):
        net, flows, settings, observed = self.get_frozen()
        before = _flow_checksum(flows)
        for method in R.POLICIES:
            with self.subTest(method=method):
                out, priced, delta, info = R.choose_progressive(
                    net, flows, observed, settings, method, 5)
                self.assertEqual(before, _flow_checksum(flows))
                self.assertTrue(1 <= info["candidate_count"] <= 5)
                self.assertEqual(info["candidate_count"], len(info["trials"]))
                self.assertTrue(math.isfinite(priced["relative_gap"]))
                self.assertTrue(math.isfinite(delta["max_relative_load_change"]))
                R.R21.frozen_check(net, out, priced)
                fiscal = R.R21.settlement_check(net, out)
                self.assertEqual(fiscal["gross_micro"], fiscal["credits_micro"])

    def test_budget_never_exceeded_and_last_state_is_audited(self):
        for method in R.POLICIES:
            with self.subTest(method=method):
                r = R.run_case(self.cfg, method, 8, 9, audit_every=3)
                self.assertLessEqual(r["oracles_used"], 9)
                self.assertEqual(r["oracles_used"], 1 + sum(
                    row["decision"]["candidate_count"] for row in r["rows"][1:]))
                self.assertEqual(r["financial_audits"][-1]["step"],
                                 r["updates_executed"])
                self.assertEqual(r["final_flow_sha256"], r["rows"][-1]["flow_sha256"])
                self.assertFalse(r["global_equilibrium_proven"])
                self.assertFalse(r["financial_F059_ratified"])
                self.assertFalse(r["authoritative_solver_approved"])
                for key, point in r["oracle_budget_frontiers"].items():
                    self.assertLessEqual(point["oracles_used"], int(key))

    def test_one_remaining_candidate_is_billed_exactly_once(self):
        for policy in R.POLICIES:
            result = R.run_case(self.cfg, policy, 8, 2)
            self.assertEqual(result["updates_executed"], 1)
            self.assertEqual(result["oracles_used"], 2)
            self.assertEqual(result["financial_audits"][-1]["step"], 1)

    def test_progressive_trajectory_prefix_determinism(self):
        for policy in R.POLICIES:
            short = R.run_case(self.cfg, policy, 4, 51, audit_every=2)
            long = R.run_case(self.cfg, policy, 8, 51, audit_every=2)
            self.assertEqual(short["rows"], long["rows"][:len(short["rows"])])

    def test_unmodified_r21_reference_and_first_acceptance(self):
        self.assertTrue(R.R24.check_r21_reference(self.cfg, "decay_sqrt", 6))
        self.assertTrue(R.R24.check_r21_reference(self.cfg, "backtracking", 6))
        count = R.replay_global_acceptance(self.cfg, 8)
        self.assertTrue(isinstance(count, int) and 0 <= count <= 8)


if __name__ == "__main__":
    unittest.main()
